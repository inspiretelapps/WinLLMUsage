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
        Task.FromResult(FindCredentials() is not null);

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var token = FindCredentials();
        if (token is null)
        {
            return ProviderSnapshot.Error(Provider, "Devin credentials were not found.", ErrorCategory.NotLoggedIn);
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + token,
            ["Accept"] = "application/json",
            ["Content-Type"] = "application/json",
        };
        var response = await _http.SendAsync(
            JsonRequest.PostJson("https://api.devin.ai/ada/GetUserStatus", "{}", headers, TimeSpan.FromSeconds(15)),
            cancellationToken).ConfigureAwait(false);
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

        MetricLine.AppendNoDataIfNeeded(lines);
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

        lines.Add(MetricLine.Progress(label, used ?? 0, 100, ProgressFormat.Percent, Iso8601.DateFrom(window.Value.GetString("resets_at", "resetsAt"))));
    }

    private string? FindCredentials()
    {
        var toml = Path.Combine(_paths.UserProfile, ".devin", "credentials.toml");
        if (File.Exists(toml))
        {
            try
            {
                var model = Toml.ToModel(File.ReadAllText(toml));
                if (model.TryGetValue("token", out var token) && token is string text && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (model.TryGetValue("api_key", out var key) && key is string api && !string.IsNullOrWhiteSpace(api))
                {
                    return api;
                }
            }
            catch (Exception)
            {
            }
        }

        return Environment.GetEnvironmentVariable("DEVIN_API_KEY");
    }
}
