using System.Text.Json;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.History;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Infrastructure.Scanning;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Grok;

public sealed class GrokProvider : IProviderRuntime
{
    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public GrokProvider(IHttpTransport http, AppPaths paths, IClock clock)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Grok();
        WidgetDescriptors = KnownProviders.GrokDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(AuthPath()));

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var token = await LoadTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return ProviderSnapshot.Error(Provider, "Grok is not signed in on this machine.", ErrorCategory.NotLoggedIn);
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + token,
            ["Accept"] = "application/json",
        };
        var response = await ProviderAuthRetry.FetchAsync(
            _http,
            _ => Task.FromResult(JsonRequest.Get("https://grok.x.ai/api/billing/settings", headers, TimeSpan.FromSeconds(15))),
            _ => Task.FromResult(false),
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Grok billing request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
        }

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        var lines = new List<MetricLine>();
        var weekly = root.GetObject("weekly", "weekly_limit") ?? root;
        var used = weekly.GetDouble("used", "utilization", "percent") ?? 0;
        lines.Add(MetricLine.Progress("Weekly limit", used, 100, ProgressFormat.Percent, Iso8601.DateFrom(weekly.GetString("resets_at", "resetsAt"))));
        var payg = root.GetString("pay_as_you_go", "payAsYouGo", "cap");
        if (!string.IsNullOrWhiteSpace(payg))
        {
            lines.Add(MetricLine.Badge("Pay as you go", payg!));
        }

        var history = await ScanHistoryAsync(cancellationToken).ConfigureAwait(false);
        lines.AddRange(SpendTileMapper.Lines(history, _clock.Now, TimeZoneInfo.Local, estimated: true, SpendTileMapper.Trend(history, _clock.Now, TimeZoneInfo.Local), "From your Grok logs (estimated)"));
        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, root.GetString("plan"), lines, _clock.Now, history);
    }

    private async Task<string?> LoadTokenAsync(CancellationToken cancellationToken)
    {
        var path = AuthPath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
            return doc.RootElement.GetString("token", "accessToken", "access_token");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string AuthPath()
    {
        var home = Environment.GetEnvironmentVariable("GROK_HOME");
        return string.IsNullOrWhiteSpace(home)
            ? Path.Combine(_paths.UserProfile, ".grok", "auth.json")
            : Path.Combine(home, "auth.json");
    }

    private async Task<ProviderUsageHistory?> ScanHistoryAsync(CancellationToken cancellationToken)
    {
        var home = Environment.GetEnvironmentVariable("GROK_HOME");
        var root = string.IsNullOrWhiteSpace(home) ? Path.Combine(_paths.UserProfile, ".grok") : home;
        var sessions = Path.Combine(root, "sessions");
        if (!Directory.Exists(sessions))
        {
            return null;
        }

        var byDay = new Dictionary<string, (long Tokens, double Cost)>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(sessions, "*.jsonl", SearchOption.AllDirectories))
        {
            await using var stream = File.OpenRead(file);
            await foreach (var record in JsonlStreamingReader.ReadRecordsAsync(stream, cancellationToken))
            {
                try
                {
                    using var doc = JsonDocument.Parse(record);
                    var element = doc.RootElement;
                    var type = element.GetString("type", "event");
                    if (type is "subagent" or "resumed" or "forked" or "replay")
                    {
                        var id = element.GetString("id", "event_id") ?? record;
                        if (!seen.Add(id))
                        {
                            continue;
                        }
                    }

                    if (element.GetString("status") is { } status && !status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var tokens = element.GetInt64("total_tokens", "tokens") ?? 0;
                    var cost = element.GetDouble("cost", "costUSD", "cost_usd") ?? 0;
                    var timestamp = Iso8601.DateFrom(element.GetString("timestamp", "created_at")) ?? _clock.Now;
                    var day = UsageHistoryDocument.FormatDay(timestamp, TimeZoneInfo.Local);
                    byDay.TryGetValue(day, out var existing);
                    byDay[day] = (existing.Tokens + tokens, existing.Cost + cost);
                }
                catch (JsonException)
                {
                }
            }
        }

        if (byDay.Count == 0)
        {
            return null;
        }

        var series = new DailyUsageSeries(byDay.OrderBy(kv => kv.Key).Select(kv => new DailyUsageEntry(kv.Key, kv.Value.Tokens, kv.Value.Cost)).ToArray());
        return new ProviderUsageHistory(series);
    }
}
