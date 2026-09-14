using System.Text.Json;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Settings;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.OpenRouter;

public sealed class OpenRouterProvider : IProviderRuntime
{
    private readonly IHttpTransport _http;
    private readonly ISecretStore _secrets;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public OpenRouterProvider(IHttpTransport http, ISecretStore secrets, AppPaths paths, IClock clock)
    {
        _http = http;
        _secrets = secrets;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.OpenRouter();
        WidgetDescriptors = KnownProviders.OpenRouterDescriptors();
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
            return ProviderSnapshot.Error(Provider, "Add an OpenRouter API key in Customize.", ErrorCategory.NotLoggedIn);
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + key,
            ["Accept"] = "application/json",
        };
        var credits = await _http.SendAsync(JsonRequest.Get("https://openrouter.ai/api/v1/credits", headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        if (!credits.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"OpenRouter credits request failed ({credits.Status}).", ProviderAuthRetry.Classify(credits.Status));
        }

        using var doc = JsonDocument.Parse(credits.Body);
        var data = doc.RootElement.TryGetProperty("data", out var inner) ? inner : doc.RootElement;
        var total = data.GetDouble("total_credits", "totalCredits") ?? 0;
        var used = data.GetDouble("total_usage", "totalUsage") ?? 0;
        var remaining = Math.Max(0, total - used);
        var lines = new List<MetricLine>
        {
            MetricLine.Progress("Credits", used, total > 0 ? total : used, ProgressFormat.Dollars),
            MetricLine.Values("Balance", [new MetricValue(remaining, MetricKind.Dollars, Estimated: false)]),
        };

        try
        {
            var keyInfo = await _http.SendAsync(JsonRequest.Get("https://openrouter.ai/api/v1/key", headers, TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);
            if (keyInfo.IsSuccess)
            {
                using var keyDoc = JsonDocument.Parse(keyInfo.Body);
                var keyData = keyDoc.RootElement.TryGetProperty("data", out var kd) ? kd : keyDoc.RootElement;
                var limit = keyData.GetDouble("limit", "limit_remaining", "usage");
                if (limit is { } cap && cap > 0)
                {
                    var keyUsed = keyData.GetDouble("usage") ?? 0;
                    lines.Add(MetricLine.Progress("Key Limit", keyUsed, cap, ProgressFormat.Dollars));
                }

                var today = keyData.GetDouble("usage_daily", "daily_usage");
                if (today is { } t)
                {
                    lines.Add(MetricLine.Values("Today", [new MetricValue(t, MetricKind.Dollars)]));
                }

                var week = keyData.GetDouble("usage_weekly", "weekly_usage");
                if (week is { } w)
                {
                    lines.Add(MetricLine.Values("This Week", [new MetricValue(w, MetricKind.Dollars)]));
                }

                var month = keyData.GetDouble("usage_monthly", "monthly_usage");
                if (month is { } m)
                {
                    lines.Add(MetricLine.Values("This Month", [new MetricValue(m, MetricKind.Dollars)]));
                }
            }
        }
        catch (Exception)
        {
            // Optional /key lookup: keep credit data.
        }

        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, null, lines, _clock.Now);
    }

    private async Task<string?> LoadKeyAsync(CancellationToken cancellationToken)
    {
        foreach (var path in _paths.CandidateHomes("openrouter"))
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

                if (!text.TrimStart().StartsWith('{'))
                {
                    return text.Trim();
                }
            }
            catch (JsonException)
            {
            }
        }

        var saved = await _secrets.UnprotectAsync(AppSettings.OpenRouterKeyName, cancellationToken).ConfigureAwait(false);
        if (saved is { Length: > 0 })
        {
            return System.Text.Encoding.UTF8.GetString(saved);
        }

        return Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    }
}
