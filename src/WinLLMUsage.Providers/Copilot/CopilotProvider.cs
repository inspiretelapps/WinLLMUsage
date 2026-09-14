using System.Diagnostics;
using System.Text.Json;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Copilot;

public sealed class CopilotProvider : IProviderRuntime
{
    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;
    private readonly ISettingsStore _settings;

    public CopilotProvider(IHttpTransport http, AppPaths paths, IClock clock, ISettingsStore settings)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        _settings = settings;
        Provider = KnownProviders.Copilot();
        WidgetDescriptors = KnownProviders.CopilotDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(TryReadHostsToken()) || File.Exists(Path.Combine(_paths.UserProfile, ".config", "gh", "hosts.yml")));

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var token = TryReadHostsToken() ?? await TryGhAuthTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return ProviderSnapshot.Error(Provider, "GitHub Copilot is not signed in (gh auth or Copilot hosts.json).", ErrorCategory.NotLoggedIn);
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + token,
            ["Accept"] = "application/json",
            ["Editor-Version"] = "vscode/1.96.0",
            ["Editor-Plugin-Version"] = "copilot/1.270.0",
            ["User-Agent"] = "GitHubCopilotChat/0.22.0",
        };
        var response = await _http.SendAsync(JsonRequest.Get("https://api.github.com/copilot_internal/user", headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Copilot user request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
        }

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        var quota = root.GetObject("quota_snapshots", "quotaSnapshots") ?? root;
        var lines = new List<MetricLine>();
        MapPremium(quota, lines);
        var extra = quota.GetObject("extra", "premium_extra") ?? root.GetObject("extra_usage");
        if (extra is { } extraEl)
        {
            var count = extraEl.GetDouble("used", "count") ?? 0;
            lines.Add(MetricLine.Values("Extra Usage", [new MetricValue(count, MetricKind.Count)]));
        }

        AddPercent(quota, "chat", "Chat", lines);
        AddPercent(quota, "completions", "Completions", lines);

        var org = _settings.Get<string>("copilot.billingOrg");
        if (!string.IsNullOrWhiteSpace(org))
        {
            try
            {
                var orgResponse = await _http.SendAsync(JsonRequest.Get($"https://api.github.com/orgs/{org}/copilot/billing", headers, TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);
                if (orgResponse.IsSuccess)
                {
                    using var orgDoc = JsonDocument.Parse(orgResponse.Body);
                    var credits = orgDoc.RootElement.GetDouble("seat_management_setting", "credits", "credits_used") ?? 0;
                    var spend = orgDoc.RootElement.GetDouble("spend", "total_spend") ?? 0;
                    lines.Add(MetricLine.Values("Org Credits", [new MetricValue(credits, MetricKind.Count, "credits")]));
                    lines.Add(MetricLine.Values("Org Spend", [new MetricValue(spend, MetricKind.Dollars)]));
                }
            }
            catch (Exception)
            {
            }
        }

        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, root.GetString("plan", "copilot_plan"), lines, _clock.Now);
    }

    private static void MapPremium(JsonElement quota, List<MetricLine> lines)
    {
        var premium = quota.GetObject("premium_interactions", "premium", "premiumCredits") ?? quota;
        var percent = premium.GetDouble("percent_remaining", "percentRemaining");
        if (percent is { } remaining)
        {
            lines.Add(MetricLine.Progress("Credits", 100 - remaining, 100, ProgressFormat.Percent));
            return;
        }

        var used = premium.GetDouble("credits_used", "used") ?? 0;
        var limit = premium.GetDouble("entitlement", "limit");
        if (limit is { } cap && cap > 0)
        {
            lines.Add(MetricLine.Progress("Credits", used, cap, ProgressFormat.Count("credits")));
        }
        else
        {
            lines.Add(MetricLine.Values("Credits", [new MetricValue(used, MetricKind.Count)]));
        }
    }

    private static void AddPercent(JsonElement quota, string key, string label, List<MetricLine> lines)
    {
        var window = quota.GetObject(key);
        if (window is null)
        {
            return;
        }

        var remaining = window.Value.GetDouble("percent_remaining");
        var used = remaining is { } left ? 100 - left : window.Value.GetDouble("used", "percent") ?? 0;
        lines.Add(MetricLine.Progress(label, used, 100, ProgressFormat.Percent));
    }

    private string? TryReadHostsToken()
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(_paths.UserProfile, ".config", "github-copilot", "hosts.json"),
                     Path.Combine(_paths.UserProfile, ".config", "github-copilot", "apps.json"),
                     Path.Combine(_paths.RoamingAppData, "GitHub Copilot", "hosts.json"),
                 })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    var token = property.Value.GetString("oauth_token", "token", "access_token");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        return token;
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private static async Task<string?> TryGhAuthTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var start = new ProcessStartInfo("gh", "auth token --hostname github.com")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var token = output.Trim();
            return process.ExitCode == 0 && token.Length > 0 ? token : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
