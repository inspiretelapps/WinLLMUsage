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
    public const string CreditsUrl = "https://cli-chat-proxy.grok.com/v1/billing?format=credits";
    public const string SettingsUrl = "https://cli-chat-proxy.grok.com/v1/settings";

    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;
    private string? _accessToken;
    private string? _refreshToken;

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
        LoadTokens();
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            return ProviderSnapshot.Error(Provider, "Grok is not signed in on this machine.", ErrorCategory.NotLoggedIn);
        }

        Dictionary<string, string> Headers() => new()
        {
            ["Authorization"] = "Bearer " + _accessToken,
            ["Accept"] = "application/json",
        };

        var credits = await ProviderAuthRetry.FetchAsync(
            _http,
            _ => Task.FromResult(JsonRequest.Get(CreditsUrl, Headers(), TimeSpan.FromSeconds(15))),
            ct => RefreshTokenAsync(ct),
            cancellationToken).ConfigureAwait(false);
        if (!credits.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Grok billing request failed ({credits.Status}).", ProviderAuthRetry.Classify(credits.Status));
        }

        IReadOnlyList<MetricLine> remote;
        try
        {
            remote = GrokUsageMapper.MapCreditsConfig(credits.Text);
        }
        catch (Exception)
        {
            return ProviderSnapshot.Error(Provider, "Grok billing response could not be mapped.", ErrorCategory.Decoding);
        }

        string? plan = null;
        try
        {
            var settings = await _http.SendAsync(JsonRequest.Get(SettingsUrl, Headers(), TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
            if (settings.IsSuccess)
            {
                plan = GrokUsageMapper.PlanName(settings.Text);
            }
        }
        catch (Exception)
        {
        }

        var lines = remote.ToList();
        var history = await ScanHistoryAsync(cancellationToken).ConfigureAwait(false);
        lines.AddRange(SpendTileMapper.Lines(history, _clock.Now, TimeZoneInfo.Local, estimated: true, SpendTileMapper.Trend(history, _clock.Now, TimeZoneInfo.Local), "From your Grok logs (estimated)"));
        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, plan, lines, _clock.Now, history);
    }

    private async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
        {
            return false;
        }

        var body = JsonSerializer.Serialize(new { grant_type = "refresh_token", refresh_token = _refreshToken });
        var response = await _http.SendAsync(
            JsonRequest.PostJson("https://cli-chat-proxy.grok.com/v1/oauth/token", body, new Dictionary<string, string> { ["Content-Type"] = "application/json" }, TimeSpan.FromSeconds(15)),
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return false;
        }

        using var doc = JsonDocument.Parse(response.Body);
        var access = doc.RootElement.GetString("access_token", "accessToken");
        if (string.IsNullOrWhiteSpace(access))
        {
            return false;
        }

        _accessToken = access;
        _refreshToken = doc.RootElement.GetString("refresh_token") ?? _refreshToken;
        PersistTokens();
        return true;
    }

    private void LoadTokens()
    {
        var path = AuthPath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            _accessToken = doc.RootElement.GetString("token", "accessToken", "access_token");
            _refreshToken = doc.RootElement.GetString("refreshToken", "refresh_token");
        }
        catch (JsonException)
        {
        }
    }

    private void PersistTokens()
    {
        var path = AuthPath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.RootElement.GetRawText()) ?? [];
            if (_accessToken is not null)
            {
                map["token"] = JsonSerializer.SerializeToElement(_accessToken);
                map["accessToken"] = JsonSerializer.SerializeToElement(_accessToken);
            }

            if (_refreshToken is not null)
            {
                map["refreshToken"] = JsonSerializer.SerializeToElement(_refreshToken);
            }

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(map));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception)
        {
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
                    var cost = element.GetDouble("cost", "costUSD", "cost_usd");
                    var timestamp = Iso8601.DateFrom(element.GetString("timestamp", "created_at")) ?? _clock.Now;
                    var day = UsageHistoryDocument.FormatDay(timestamp, TimeZoneInfo.Local);
                    byDay.TryGetValue(day, out var existing);
                    byDay[day] = (existing.Tokens + tokens, existing.Cost + (cost is { } c && c > 0 ? c : 0));
                }
                catch (JsonException)
                {
                }
            }
        }

        return byDay.Count == 0
            ? null
            : new ProviderUsageHistory(new DailyUsageSeries(byDay.OrderBy(kv => kv.Key).Select(kv => new DailyUsageEntry(kv.Key, kv.Value.Tokens, kv.Value.Cost > 0 ? kv.Value.Cost : null)).ToArray()));
    }
}
