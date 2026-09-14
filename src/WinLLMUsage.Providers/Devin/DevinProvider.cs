using System.Text.Json;
using Tomlyn;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Devin;

public sealed class DevinProvider : IProviderRuntime
{
    public const string DefaultApiServer = "https://server.codeium.com";

    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public DevinProvider(IHttpTransport http, AppPaths paths, IClock clock)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Devin();
        WidgetDescriptors = KnownProviders.DevinDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(LoadAuth() is not null);

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var auth = LoadAuth();
        if (auth is null)
        {
            return ProviderSnapshot.Error(Provider, "Run devin auth login or sign in to Devin and try again.", ErrorCategory.NotLoggedIn);
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + auth.Value.ApiKey,
            ["Accept"] = "application/json",
            ["Content-Type"] = "application/json",
        };
        var url = auth.Value.Server.TrimEnd('/') + "/exa.seat_management_pb.SeatManagementService/GetUserStatus";
        var response = await _http.SendAsync(JsonRequest.PostJson(url, "{}", headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Devin status request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
        }

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement.TryGetProperty("result", out var result) ? result : doc.RootElement;
        var lines = new List<MetricLine>();
        AddQuota(root, "daily", "Daily quota", lines);
        AddQuota(root, "weekly", "Weekly quota", lines);
        var extra = root.GetDouble("extra_balance", "extraUsageBalance", "extra");
        if (extra is { } balance)
        {
            lines.Add(MetricLine.Values("Extra usage balance", [new MetricValue(balance, MetricKind.Dollars)]));
        }

        if (lines.Count == 0)
        {
            return ProviderSnapshot.Error(Provider, "Devin status response could not be mapped.", ErrorCategory.Decoding);
        }

        return ProviderSnapshot.Make(Provider, root.GetString("plan", "plan_name"), lines, _clock.Now);
    }

    private static void AddQuota(JsonElement root, string key, string label, List<MetricLine> lines)
    {
        var window = root.GetObject(key);
        if (window is null)
        {
            return;
        }

        var remaining = window.Value.GetDouble("remaining", "remaining_percent");
        var used = window.Value.GetDouble("used", "utilization");
        if (used is null && remaining is { } left)
        {
            used = 100 - left;
        }

        if (used is null)
        {
            return;
        }

        lines.Add(MetricLine.Progress(label, used.Value, 100, ProgressFormat.Percent, Iso8601.DateFrom(window.Value.GetString("resets_at", "resetsAt"))));
    }

    private (string ApiKey, string Server)? LoadAuth()
    {
        var tomlCandidates = new[]
        {
            Path.Combine(_paths.UserProfile, ".local", "share", "devin", "credentials.toml"),
            Path.Combine(_paths.UserProfile, ".devin", "credentials.toml"),
        };
        foreach (var toml in tomlCandidates)
        {
            if (!File.Exists(toml))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(toml);
                var key = ReadToml(text, "windsurf_api_key") ?? ReadToml(text, "api_key");
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var server = ReadToml(text, "api_server_url");
                if (server is not null && !server.StartsWith("https://", StringComparison.Ordinal))
                {
                    server = null;
                }

                return (key, string.IsNullOrWhiteSpace(server) ? DefaultApiServer : server.TrimEnd('/'));
            }
            catch (Exception)
            {
            }
        }

        var env = Environment.GetEnvironmentVariable("DEVIN_API_KEY");
        return string.IsNullOrWhiteSpace(env) ? null : (env, DefaultApiServer);
    }

    private static string? ReadToml(string text, string key)
    {
        try
        {
            var model = Toml.ToModel(text);
            if (model.TryGetValue(key, out var value) && value is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s.Trim();
            }
        }
        catch (Exception)
        {
        }

        foreach (var line in text.Split('\n'))
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim() == key)
            {
                return parts[1].Trim().Trim('"');
            }
        }

        return null;
    }
}
