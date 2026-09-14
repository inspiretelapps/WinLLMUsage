using System.Text.Json;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Zai;

public sealed class ZaiProvider : IProviderRuntime
{
    private readonly IHttpTransport _http;
    private readonly ISecretStore _secrets;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public ZaiProvider(IHttpTransport http, ISecretStore secrets, AppPaths paths, IClock clock)
    {
        _http = http;
        _secrets = secrets;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Zai();
        WidgetDescriptors = KnownProviders.ZaiDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public async Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(await LoadKeyAsync(cancellationToken).ConfigureAwait(false));

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var key = await LoadKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
        {
            return ProviderSnapshot.Error(Provider, "Add a Z.ai API key in Customize.", ErrorCategory.NotLoggedIn);
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + key,
            ["Accept"] = "application/json",
        };
        var response = await _http.SendAsync(JsonRequest.Get("https://api.z.ai/api/biz/subscription", headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        if (response.Status is 401 or 403)
        {
            return ProviderSnapshot.Error(Provider, "Z.ai API key is invalid.", ErrorCategory.AuthInvalid);
        }

        if (!response.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Z.ai subscription request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
        }

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;
        var lines = new List<MetricLine>();
        MapWindow(root, "session", "Session", lines);
        MapWindow(root, "weekly", "Weekly", lines);
        var searches = root.GetObject("web_searches", "webSearches");
        if (searches is { } search)
        {
            var used = search.GetDouble("used") ?? 0;
            var limit = search.GetDouble("limit", "CREDIT_LIMIT", "TOKENS_LIMIT") ?? 1000;
            var resets = ParseReset(search);
            lines.Add(MetricLine.Progress("Web Searches", used, limit, ProgressFormat.Count("searches"), resets));
        }

        if (lines.Count == 0)
        {
            return ProviderSnapshot.Error(Provider, "No Z.ai subscription is attached to this key.", ErrorCategory.NotAvailable);
        }

        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, root.GetString("plan", "plan_name"), lines, _clock.Now);
    }

    private static void MapWindow(JsonElement root, string key, string label, List<MetricLine> lines)
    {
        var window = root.GetObject(key);
        if (window is null)
        {
            return;
        }

        var used = window.Value.GetDouble("used", "utilization", "percent") ?? 0;
        var limit = window.Value.GetDouble("limit") ?? 100;
        lines.Add(MetricLine.Progress(label, used, limit, ProgressFormat.Percent, ParseReset(window.Value), window.Value.GetInt64("duration_ms") is { } ms ? (int)ms : null));
    }

    private static DateTimeOffset? ParseReset(JsonElement element)
    {
        var text = element.GetString("resets_at", "resetsAt", "reset_at");
        if (text is not null)
        {
            return Iso8601.DateFrom(text);
        }

        var epoch = element.GetDouble("resets_at_ms", "reset_at_ms");
        return epoch is null ? null : Iso8601.FromUnix(epoch.Value);
    }

    private async Task<string?> LoadKeyAsync(CancellationToken cancellationToken)
    {
        foreach (var path in _paths.CandidateHomes("zai"))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(text);
                var key = doc.RootElement.GetString("apiKey", "api_key", "key");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    return key;
                }
            }
            catch (JsonException)
            {
            }
        }

        var saved = await _secrets.UnprotectAsync(AppSettings.ZaiKeyName, cancellationToken).ConfigureAwait(false);
        if (saved is { Length: > 0 })
        {
            return System.Text.Encoding.UTF8.GetString(saved);
        }

        return Environment.GetEnvironmentVariable("ZAI_API_KEY") ?? Environment.GetEnvironmentVariable("GLM_API_KEY");
    }
}
