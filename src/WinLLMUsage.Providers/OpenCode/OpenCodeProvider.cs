using System.Text.Json;
using Microsoft.Data.Sqlite;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.History;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.OpenCode;

public sealed class OpenCodeProvider : IProviderRuntime
{
    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public OpenCodeProvider(IHttpTransport http, AppPaths paths, IClock clock)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.OpenCode();
        WidgetDescriptors = KnownProviders.OpenCodeDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(AuthPath()));

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var token = await LoadTokenAsync(cancellationToken).ConfigureAwait(false);
        var lines = new List<MetricLine>();
        string? plan = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + token,
                ["Accept"] = "application/json",
            };
            var response = await _http.SendAsync(JsonRequest.Get("https://opencode.ai/api/usage", headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccess)
            {
                using var doc = JsonDocument.Parse(response.Body);
                var root = doc.RootElement;
                plan = root.GetString("plan");
                AddPercent(root, "session", "Session", MetricPeriod.SessionMs, lines);
                AddPercent(root, "weekly", "Weekly", MetricPeriod.WeekMs, lines);
                AddPercent(root, "monthly", "Monthly", (int)Math.Min(int.MaxValue, MetricPeriod.MonthMs), lines);
            }
            else
            {
                return ProviderSnapshot.Error(Provider, $"OpenCode usage request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
            }
        }
        else
        {
            return ProviderSnapshot.Error(Provider, "OpenCode is not signed in.", ErrorCategory.NotLoggedIn);
        }

        var history = ScanLocalSpend();
        lines.AddRange(SpendTileMapper.Lines(history, _clock.Now, TimeZoneInfo.Local, estimated: false, SpendTileMapper.Trend(history, _clock.Now, TimeZoneInfo.Local), "From your OpenCode logs"));
        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, plan, lines, _clock.Now, history);
    }

    private static void AddPercent(JsonElement root, string key, string label, int periodMs, List<MetricLine> lines)
    {
        var window = root.GetObject(key);
        if (window is null)
        {
            return;
        }

        var used = window.Value.GetDouble("used", "percent", "utilization") ?? 0;
        lines.Add(MetricLine.Progress(label, used, 100, ProgressFormat.Percent, Iso8601.DateFrom(window.Value.GetString("resets_at", "resetsAt")), periodMs));
    }

    private string AuthPath()
    {
        foreach (var home in _paths.CandidateHomes("opencode"))
        {
            var auth = Directory.Exists(home) ? Path.Combine(home, "auth.json") : home;
            if (File.Exists(auth))
            {
                return auth;
            }
        }

        return Path.Combine(_paths.UserProfile, ".local", "share", "opencode", "auth.json");
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
            return doc.RootElement.GetString("token", "access_token", "apiKey");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private ProviderUsageHistory? ScanLocalSpend()
    {
        var homes = _paths.CandidateHomes("opencode").Where(Directory.Exists).ToArray();
        if (homes.Length == 0)
        {
            return null;
        }

        var byDay = new Dictionary<string, (long Tokens, double Cost)>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var home in homes)
        {
            foreach (var db in Directory.EnumerateFiles(home, "opencode*.db", SearchOption.AllDirectories))
            {
                try
                {
                    var cs = new SqliteConnectionStringBuilder
                    {
                        DataSource = db,
                        Mode = SqliteOpenMode.ReadOnly,
                        Cache = SqliteCacheMode.Shared,
                    };
                    using var connection = new SqliteConnection(cs.ToString());
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT id, tokens, cost, created_at FROM message WHERE cost IS NOT NULL LIMIT 100000";
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var id = reader.GetValue(0)?.ToString();
                        if (id is not null && !seen.Add(id))
                        {
                            continue;
                        }

                        var tokens = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1));
                        var cost = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2));
                        var created = reader.IsDBNull(3) ? _clock.Now : DateTimeOffset.TryParse(reader.GetValue(3)?.ToString(), out var parsed) ? parsed : _clock.Now;
                        var day = UsageHistoryDocument.FormatDay(created, TimeZoneInfo.Local);
                        byDay.TryGetValue(day, out var existing);
                        byDay[day] = (existing.Tokens + tokens, existing.Cost + cost);
                    }
                }
                catch (SqliteException)
                {
                }
            }
        }

        if (byDay.Count == 0)
        {
            return null;
        }

        return new ProviderUsageHistory(new DailyUsageSeries(byDay.OrderBy(kv => kv.Key).Select(kv => new DailyUsageEntry(kv.Key, kv.Value.Tokens, kv.Value.Cost)).ToArray()));
    }
}
