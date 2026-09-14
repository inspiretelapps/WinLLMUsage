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

namespace WinLLMUsage.Providers.Codex;

public sealed class CodexProvider : IProviderRuntime
{
    public const string UsageUrl = "https://chatgpt.com/backend-api/wham/usage";
    public const string ResetCreditsUrl = "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits";
    public const string ConsumeResetUrl = "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits/consume";
    public const string TokenUrl = "https://auth.openai.com/oauth/token";
    public const string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    public const double CreditUsd = 0.04;

    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public CodexProvider(IHttpTransport http, AppPaths paths, IClock clock)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Codex();
        WidgetDescriptors = KnownProviders.CodexDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(LoadAuth() is { AccessToken: not null });

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var auth = LoadAuth();
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            if (auth is { ApiKeyOnly: true })
            {
                return ProviderSnapshot.Error(Provider, "Codex API-key auth cannot read subscription usage.", ErrorCategory.NotAvailable);
            }

            return ProviderSnapshot.Error(Provider, "Codex is not signed in on this machine.", ErrorCategory.NotLoggedIn);
        }

        var headers = UsageHeaders(auth);
        var response = await ProviderAuthRetry.FetchAsync(
            _http,
            _ => Task.FromResult(JsonRequest.Get(UsageUrl, headers, TimeSpan.FromSeconds(10))),
            async ct =>
            {
                var refreshed = await RefreshTokenAsync(auth, ct).ConfigureAwait(false);
                if (refreshed is null)
                {
                    return false;
                }

                auth = refreshed;
                headers = UsageHeaders(auth);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Codex usage request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
        }

        using var doc = JsonDocument.Parse(response.Body);
        var lines = MapUsage(doc.RootElement, response.Headers);
        try
        {
            var resetHeaders = new Dictionary<string, string>(headers)
            {
                ["OpenAI-Beta"] = "codex-1",
                ["originator"] = "Codex Desktop",
            };
            var resets = await _http.SendAsync(JsonRequest.Get(ResetCreditsUrl, resetHeaders, TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);
            if (resets.IsSuccess)
            {
                using var resetDoc = JsonDocument.Parse(resets.Body);
                lines.Add(MapResetCredits(resetDoc.RootElement));
            }
        }
        catch (Exception)
        {
        }

        var history = await ScanLogsAsync(cancellationToken).ConfigureAwait(false);
        lines.AddRange(SpendTileMapper.Lines(history, _clock.Now, TimeZoneInfo.Local, estimated: true, SpendTileMapper.Trend(history, _clock.Now, TimeZoneInfo.Local), "From your Codex logs (estimated)"));
        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, MapPlan(doc.RootElement.GetString("plan_type")), lines, _clock.Now, history);
    }

    public static List<MetricLine> MapUsage(JsonElement root, IReadOnlyDictionary<string, string>? headers = null)
    {
        var lines = new List<MetricLine>();
        var rate = root.GetObject("rate_limit") ?? root;
        MapWindow(rate.GetObject("primary_window"), rate.GetObject("secondary_window"), headers, lines);
        if (root.TryGetProperty("additional_rate_limits", out var extra) && extra.ValueKind == JsonValueKind.Array)
        {
            JsonElement? sparkPrimary = null;
            JsonElement? sparkSecondary = null;
            foreach (var item in extra.EnumerateArray())
            {
                var name = (item.GetString("limit_name") ?? "") + (item.GetString("metered_feature") ?? "");
                if (!name.Contains("spark", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var seconds = item.GetDouble("limit_window_seconds") ?? 0;
                if (seconds <= MetricPeriod.Session.TotalSeconds * 1.5)
                {
                    sparkPrimary = item;
                }
                else
                {
                    sparkSecondary = item;
                }
            }

            if (sparkPrimary is { } sp)
            {
                lines.Add(ProgressFromWindow("Spark", sp, MetricPeriod.SessionMs));
            }

            if (sparkSecondary is { } ss)
            {
                lines.Add(ProgressFromWindow("Spark Weekly", ss, MetricPeriod.WeekMs));
            }
        }

        var credits = root.GetObject("credits");
        var count = credits?.GetDouble("balance") ?? HeaderDouble(headers, "x-codex-credits-balance") ?? 0;
        if (credits?.GetBoolean("has_credits") == false)
        {
            count = 0;
        }

        lines.Add(MetricLine.Values("Credits", [
            new MetricValue(Math.Floor(count) * CreditUsd, MetricKind.Dollars),
            new MetricValue(Math.Floor(count), MetricKind.Count, "credits"),
        ]));
        return lines;
    }

    public static MetricLine MapResetCredits(JsonElement root)
    {
        var available = root.GetInt64("available_count") ?? 0;
        var expiries = new List<DateTimeOffset>();
        if (root.TryGetProperty("credits", out var credits) && credits.ValueKind == JsonValueKind.Array)
        {
            foreach (var credit in credits.EnumerateArray())
            {
                var status = credit.GetString("status");
                if (status is not null && !status.Equals("available", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var expiry = Iso8601.DateFrom(credit.GetString("expires_at"));
                if (expiry is { } date)
                {
                    expiries.Add(date);
                }
            }

            if (available == 0)
            {
                available = expiries.Count;
            }
        }

        return MetricLine.Values("Rate Limit Resets", [new MetricValue(available, MetricKind.Count, "available")], expiriesAt: expiries);
    }

    public static string? MapPlan(string? planType) => planType switch
    {
        "prolite" => "Pro 5x",
        "pro" => "Pro 20x",
        "self_serve_business_prolite" => "Business Premium",
        null => null,
        _ => string.Join(' ', planType.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..])),
    };

    public async Task<CodexResetClaimResult> ClaimResetAsync(string creditId, string redeemRequestId, CancellationToken cancellationToken)
    {
        var auth = LoadAuth();
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            return new CodexResetClaimResult(false, "Not signed in.");
        }

        var headers = UsageHeaders(auth);
        headers = new Dictionary<string, string>(headers)
        {
            ["OpenAI-Beta"] = "codex-1",
            ["originator"] = "Codex Desktop",
            ["Content-Type"] = "application/json",
        };
        var body = JsonSerializer.Serialize(new { credit_id = creditId, redeem_request_id = redeemRequestId });
        var response = await _http.SendAsync(JsonRequest.PostJson(ConsumeResetUrl, body, headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return new CodexResetClaimResult(false, $"Claim failed ({response.Status}).");
        }

        using var doc = JsonDocument.Parse(response.Body);
        var code = doc.RootElement.GetString("code");
        return code is "reset" or "already_redeemed"
            ? new CodexResetClaimResult(true, code)
            : new CodexResetClaimResult(false, code ?? "ambiguous");
    }

    private static void MapWindow(JsonElement? primary, JsonElement? secondary, IReadOnlyDictionary<string, string>? headers, List<MetricLine> lines)
    {
        var primaryUsed = primary?.GetDouble("used_percent") ?? HeaderDouble(headers, "x-codex-primary-used-percent") ?? 0;
        var secondaryUsed = secondary?.GetDouble("used_percent") ?? HeaderDouble(headers, "x-codex-secondary-used-percent") ?? 0;
        lines.Add(ProgressFromWindow("Session", primary, MetricPeriod.SessionMs, primaryUsed));
        lines.Add(ProgressFromWindow("Weekly", secondary, MetricPeriod.WeekMs, secondaryUsed));
    }

    private static MetricLine ProgressFromWindow(string label, JsonElement? window, int periodMs, double? usedOverride = null)
    {
        var used = usedOverride ?? window?.GetDouble("used_percent") ?? 0;
        DateTimeOffset? reset = null;
        if (window is { } w)
        {
            var unix = w.GetDouble("reset_at");
            reset = unix is null ? Iso8601.DateFrom(w.GetString("reset_at")) : Iso8601.FromUnix(unix.Value);
            if (reset is null)
            {
                var after = w.GetDouble("reset_after_seconds");
                if (after is { } seconds)
                {
                    reset = DateTimeOffset.UtcNow.AddSeconds(seconds);
                }
            }
        }

        return MetricLine.Progress(label, used, 100, ProgressFormat.Percent, reset, periodMs);
    }

    private static double? HeaderDouble(IReadOnlyDictionary<string, string>? headers, string name) =>
        headers is not null && headers.TryGetValue(name, out var value) && double.TryParse(value, out var number) ? number : null;

    private static Dictionary<string, string> UsageHeaders(CodexAuth auth)
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + auth.AccessToken,
            ["Accept"] = "application/json",
            ["User-Agent"] = "OpenUsage",
        };
        if (!string.IsNullOrWhiteSpace(auth.AccountId))
        {
            headers["ChatGPT-Account-Id"] = auth.AccountId!;
        }

        return headers;
    }

    private CodexAuth? LoadAuth()
    {
        foreach (var home in _paths.CandidateHomes("codex"))
        {
            var file = Directory.Exists(home) ? Path.Combine(home, "auth.json") : home;
            if (!file.EndsWith("auth.json", StringComparison.OrdinalIgnoreCase))
            {
                file = Path.Combine(file, "auth.json");
            }

            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var tokens = doc.RootElement.GetObject("tokens") ?? doc.RootElement;
                var access = tokens.GetString("access_token");
                var apiKey = doc.RootElement.GetString("OPENAI_API_KEY");
                return new CodexAuth(access, tokens.GetString("refresh_token"), tokens.GetString("account_id"), file, string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(apiKey));
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private async Task<CodexAuth?> RefreshTokenAsync(CodexAuth current, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            return null;
        }

        var form = $"grant_type=refresh_token&client_id={Uri.EscapeDataString(ClientId)}&refresh_token={Uri.EscapeDataString(current.RefreshToken)}";
        var response = await _http.SendAsync(JsonRequest.PostForm(TokenUrl, form, new Dictionary<string, string>(), TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(response.Body);
        var access = doc.RootElement.GetString("access_token");
        return access is null ? null : current with { AccessToken = access, RefreshToken = doc.RootElement.GetString("refresh_token") ?? current.RefreshToken };
    }

    private async Task<ProviderUsageHistory?> ScanLogsAsync(CancellationToken cancellationToken)
    {
        var byDay = new Dictionary<string, (long Tokens, double Cost)>(StringComparer.Ordinal);
        foreach (var home in _paths.CandidateHomes("codex").Where(Directory.Exists))
        {
            foreach (var folder in new[] { "sessions", "archived_sessions" })
            {
                var dir = Path.Combine(home, folder);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories))
                {
                    await using var stream = File.OpenRead(file);
                    await foreach (var record in JsonlStreamingReader.ReadRecordsAsync(stream, cancellationToken))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(record);
                            var usage = doc.RootElement.GetObject("last_token_usage") ?? doc.RootElement.GetObject("usage");
                            var tokens = usage?.GetInt64("total_tokens", "totalTokens") ?? doc.RootElement.GetInt64("total_tokens") ?? 0;
                            if (tokens == 0)
                            {
                                continue;
                            }

                            var stamp = Iso8601.DateFrom(doc.RootElement.GetString("timestamp")) ?? File.GetLastWriteTimeUtc(file);
                            var day = UsageHistoryDocument.FormatDay(stamp, TimeZoneInfo.Local);
                            byDay.TryGetValue(day, out var existing);
                            byDay[day] = (existing.Tokens + tokens, existing.Cost);
                        }
                        catch (JsonException)
                        {
                        }
                    }
                }
            }
        }

        return byDay.Count == 0
            ? null
            : new ProviderUsageHistory(new DailyUsageSeries(byDay.Select(kv => new DailyUsageEntry(kv.Key, kv.Value.Tokens, kv.Value.Cost)).OrderBy(d => d.Date).ToArray()));
    }

    private sealed record CodexAuth(string? AccessToken, string? RefreshToken, string? AccountId, string Path, bool ApiKeyOnly);
}

public sealed record CodexResetClaimResult(bool Success, string Code);
