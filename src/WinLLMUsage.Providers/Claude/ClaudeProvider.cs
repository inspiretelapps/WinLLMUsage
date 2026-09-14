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

namespace WinLLMUsage.Providers.Claude;

public sealed class ClaudeProvider : IProviderRuntime
{
    public const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    public const string RefreshUrl = "https://platform.claude.com/v1/oauth/token";
    public const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;
    private readonly string _id;
    private readonly string _displayName;

    public ClaudeProvider(IHttpTransport http, AppPaths paths, IClock clock, string id = "claude", string displayName = "Claude")
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        _id = id;
        _displayName = displayName;
        Provider = KnownProviders.Claude(id, displayName);
        WidgetDescriptors = KnownProviders.ClaudeDescriptors(Provider);
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(LoadCredentials() is not null);

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var creds = LoadCredentials();
        if (creds is null)
        {
            return ProviderSnapshot.Error(Provider, "Claude is not signed in on this machine.", ErrorCategory.NotLoggedIn);
        }

        if (creds.ExpiresAt is { } expiry && expiry - _clock.Now <= TimeSpan.FromMinutes(5) && !string.IsNullOrWhiteSpace(creds.RefreshToken))
        {
            creds = await RefreshTokenAsync(creds, cancellationToken).ConfigureAwait(false) ?? creds;
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + creds.AccessToken,
            ["Accept"] = "application/json",
            ["Content-Type"] = "application/json",
            ["anthropic-beta"] = "oauth-2025-04-20",
            ["User-Agent"] = "claude-code/2.1.69",
        };
        var response = await ProviderAuthRetry.FetchAsync(
            _http,
            _ => Task.FromResult(JsonRequest.Get(UsageUrl, headers, TimeSpan.FromSeconds(10))),
            async ct =>
            {
                var refreshed = await RefreshTokenAsync(creds, ct).ConfigureAwait(false);
                if (refreshed is null)
                {
                    return false;
                }

                creds = refreshed;
                headers["Authorization"] = "Bearer " + creds.AccessToken;
                return true;
            },
            cancellationToken).ConfigureAwait(false);

        if (response.Status == 429)
        {
            var history = await ScanLogsAsync(cancellationToken).ConfigureAwait(false);
            var lines = SpendTileMapper.Lines(history, _clock.Now, TimeZoneInfo.Local, estimated: true).ToList();
            lines.Insert(0, MetricLine.Text("Live usage", "Live usage rate limited; showing last good local spend."));
            return ProviderSnapshot.Make(Provider, creds.Plan, lines, _clock.Now, history, warning: "Live usage rate limited.");
        }

        if (!response.IsSuccess)
        {
            var history = await ScanLogsAsync(cancellationToken).ConfigureAwait(false);
            if (history is not null)
            {
                var lines = SpendTileMapper.Lines(history, _clock.Now, TimeZoneInfo.Local, estimated: true).ToList();
                return ProviderSnapshot.Make(Provider, creds.Plan, lines, _clock.Now, history, warning: $"Live usage unavailable ({response.Status}).");
            }

            return ProviderSnapshot.Error(Provider, $"Claude usage request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
        }

        using var doc = JsonDocument.Parse(response.Body);
        var mapped = MapUsage(doc.RootElement, creds.Plan);
        var local = await ScanLogsAsync(cancellationToken).ConfigureAwait(false);
        mapped.AddRange(SpendTileMapper.Lines(local, _clock.Now, TimeZoneInfo.Local, estimated: true, SpendTileMapper.Trend(local, _clock.Now, TimeZoneInfo.Local), "From your Claude usage history (estimated)"));
        MetricLine.AppendNoDataIfNeeded(mapped);
        string? warning = creds.Scopes?.Contains("user:profile", StringComparison.Ordinal) == false
            ? "Re-login for live usage"
            : null;
        return ProviderSnapshot.Make(Provider, creds.Plan, mapped, _clock.Now, local, warning);
    }

    public static List<MetricLine> MapUsage(JsonElement root, string? plan)
    {
        var lines = new List<MetricLine>();
        AddPercent(root, "five_hour", "Session", MetricPeriod.SessionMs, lines);
        AddPercent(root, "seven_day", "Weekly", MetricPeriod.WeekMs, lines);
        AddPercent(root, "seven_day_sonnet", "Sonnet", MetricPeriod.WeekMs, lines);
        if (root.TryGetProperty("limits", out var limits) && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in limits.EnumerateArray())
            {
                var kind = item.GetString("kind");
                var display = item.GetObject("scope")?.GetObject("model")?.GetString("display_name");
                if (kind == "weekly_scoped" && display == "Fable")
                {
                    lines.Add(MetricLine.Progress("Fable", item.GetDouble("percent", "utilization") ?? 0, 100, ProgressFormat.Percent, ParseReset(item)));
                }
            }
        }

        if (root.GetObject("extra_usage") is { } extra)
        {
            var usedCents = extra.GetDouble("used_credits") ?? 0;
            var limitCents = extra.GetDouble("monthly_limit");
            var used = usedCents / 100d;
            if (limitCents is { } cap && cap > 0)
            {
                lines.Add(MetricLine.Progress("Extra usage spent", used, cap / 100d, ProgressFormat.Dollars));
            }
            else
            {
                lines.Add(MetricLine.Values("Extra usage spent", [new MetricValue(used, MetricKind.Dollars)]));
            }
        }

        _ = plan;
        return lines;
    }

    private static void AddPercent(JsonElement root, string key, string label, int periodMs, List<MetricLine> lines)
    {
        var window = root.GetObject(key);
        if (window is null)
        {
            return;
        }

        lines.Add(MetricLine.Progress(label, window.Value.GetDouble("utilization", "percent") ?? 0, 100, ProgressFormat.Percent, ParseReset(window.Value), periodMs));
    }

    private static DateTimeOffset? ParseReset(JsonElement element)
    {
        if (element.TryGetProperty("resets_at", out var value))
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                return Iso8601.DateFrom(value.GetString());
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var epoch))
            {
                return Iso8601.FromUnix(epoch);
            }
        }

        return null;
    }

    private ClaudeCredentials? LoadCredentials()
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        var file = string.IsNullOrWhiteSpace(configDir)
            ? Path.Combine(_paths.UserProfile, ".claude", ".credentials.json")
            : Path.Combine(configDir, ".credentials.json");
        if (!File.Exists(file))
        {
            var env = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
            return string.IsNullOrWhiteSpace(env) ? null : new ClaudeCredentials(env, null, null, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var oauth = doc.RootElement.GetObject("claudeAiOauth") ?? doc.RootElement;
            var access = oauth.GetString("accessToken", "access_token");
            if (string.IsNullOrWhiteSpace(access))
            {
                return null;
            }

            var expires = oauth.GetDouble("expiresAt", "expires_at");
            return new ClaudeCredentials(
                access!,
                oauth.GetString("refreshToken", "refresh_token"),
                expires is null ? null : Iso8601.FromUnix(expires.Value),
                TitleCase(oauth.GetString("subscriptionType")),
                oauth.GetString("scopes"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<ClaudeCredentials?> RefreshTokenAsync(ClaudeCredentials current, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            return null;
        }

        var body = JsonSerializer.Serialize(new
        {
            grant_type = "refresh_token",
            refresh_token = current.RefreshToken,
            client_id = ClientId,
            scope = "user:profile user:inference user:sessions:claude_code user:mcp_servers user:file_upload",
        });
        var response = await _http.SendAsync(
            JsonRequest.PostJson(RefreshUrl, body, new Dictionary<string, string> { ["Content-Type"] = "application/json" }, TimeSpan.FromSeconds(15)),
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(response.Body);
        var access = doc.RootElement.GetString("access_token", "accessToken");
        if (string.IsNullOrWhiteSpace(access))
        {
            return null;
        }

        var next = current with { AccessToken = access!, RefreshToken = doc.RootElement.GetString("refresh_token") ?? current.RefreshToken };
        PersistCredentials(next);
        return next;
    }

    private void PersistCredentials(ClaudeCredentials credentials)
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        var file = string.IsNullOrWhiteSpace(configDir)
            ? Path.Combine(_paths.UserProfile, ".claude", ".credentials.json")
            : Path.Combine(configDir, ".credentials.json");
        if (!File.Exists(file))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.RootElement.GetRawText()) ?? [];
            var oauth = new Dictionary<string, object?>
            {
                ["accessToken"] = credentials.AccessToken,
                ["refreshToken"] = credentials.RefreshToken,
            };
            root["claudeAiOauth"] = JsonSerializer.SerializeToElement(oauth);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(root));
            File.Move(tmp, file, overwrite: true);
        }
        catch (Exception)
        {
        }
    }

    private async Task<ProviderUsageHistory?> ScanLogsAsync(CancellationToken cancellationToken)
    {
        var roots = _paths.CandidateHomes("claude").Where(Directory.Exists).ToArray();
        var byDay = new Dictionary<string, (long Tokens, double Cost)>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            var projects = Path.Combine(root, "projects");
            if (!Directory.Exists(projects))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(projects, "*.jsonl", SearchOption.AllDirectories))
            {
                await using var stream = File.OpenRead(file);
                await foreach (var record in JsonlStreamingReader.ReadRecordsAsync(stream, cancellationToken))
                {
                    if (!record.Contains("\"usage\"", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(record);
                        var usage = doc.RootElement.GetObject("usage") ?? doc.RootElement.GetObject("message")?.GetObject("usage");
                        if (usage is null)
                        {
                            continue;
                        }

                        var input = usage.Value.GetInt64("input_tokens") ?? 0;
                        var output = usage.Value.GetInt64("output_tokens") ?? 0;
                        var cost = doc.RootElement.GetDouble("costUSD") ?? 0;
                        var stamp = Iso8601.DateFrom(doc.RootElement.GetString("timestamp")) ?? File.GetLastWriteTimeUtc(file);
                        var day = UsageHistoryDocument.FormatDay(stamp, TimeZoneInfo.Local);
                        byDay.TryGetValue(day, out var existing);
                        byDay[day] = (existing.Tokens + input + output, existing.Cost + cost);
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
        }

        return byDay.Count == 0
            ? null
            : new ProviderUsageHistory(new DailyUsageSeries(byDay.Select(kv => new DailyUsageEntry(kv.Key, kv.Value.Tokens, kv.Value.Cost)).OrderBy(d => d.Date).ToArray()));
    }

    private static string? TitleCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private sealed record ClaudeCredentials(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt, string? Plan, string? Scopes);
}
