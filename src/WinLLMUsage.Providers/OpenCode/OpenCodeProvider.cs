using Microsoft.Data.Sqlite;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.History;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;

namespace WinLLMUsage.Providers.OpenCode;

public sealed class OpenCodeProvider : IProviderRuntime
{
    public const string UsageUrl = "https://opencode.ai/zen/go/v1/usage";

    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;
    public OpenCodeProvider(IHttpTransport http, AppPaths paths, IClock clock, IPricingService? pricing = null)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        _ = pricing;
        Provider = KnownProviders.OpenCode();
        WidgetDescriptors = KnownProviders.OpenCodeDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(TryGoKey()));

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var key = TryGoKey();
        var lines = new List<MetricLine>();
        string? warning = null;
        if (!string.IsNullOrWhiteSpace(key))
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + key,
                ["Accept"] = "application/json",
            };
            var response = await _http.SendAsync(JsonRequest.Get(UsageUrl, headers, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccess)
            {
                try
                {
                    lines.AddRange(OpenCodeUsageMapper.MapUsage(response.Text));
                }
                catch (Exception)
                {
                    return ProviderSnapshot.Error(Provider, "OpenCode usage response could not be mapped.", ErrorCategory.Decoding);
                }
            }
            else if (response.Status is 401 or 403)
            {
                warning = "OpenCode Go credentials were rejected; showing local spend if available.";
            }
            else
            {
                warning = $"OpenCode usage request failed ({response.Status}).";
            }
        }

        var history = ScanLocalSpend();
        if (history is not null)
        {
            lines.AddRange(SpendTileMapper.Lines(history, _clock.Now, TimeZoneInfo.Local, estimated: false, SpendTileMapper.Trend(history, _clock.Now, TimeZoneInfo.Local), "From your OpenCode logs"));
        }

        if (lines.Count == 0)
        {
            return ProviderSnapshot.Error(Provider, "OpenCode is not signed in.", ErrorCategory.NotLoggedIn);
        }

        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, null, lines, _clock.Now, history, warning);
    }

    public bool HasCodexOAuth()
    {
        var path = AuthPath();
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            return OpenCodeUsageMapper.HasCodexOAuth(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string? TryGoKey()
    {
        var path = AuthPath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return OpenCodeUsageMapper.GoApiKey(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
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

            var nested = Path.Combine(home, "auth.json");
            if (File.Exists(nested))
            {
                return nested;
            }
        }

        return Path.Combine(_paths.UserProfile, ".local", "share", "opencode", "auth.json");
    }

    private ProviderUsageHistory? ScanLocalSpend()
    {
        var homes = _paths.CandidateHomes("opencode").Where(Directory.Exists).ToArray();
        var byDay = new Dictionary<string, (long Tokens, double Cost)>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var home in homes)
        {
            IEnumerable<string> dbs;
            try
            {
                dbs = Directory.EnumerateFiles(home, "opencode*.db", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var db in dbs)
            {
                try
                {
                    var cs = new SqliteConnectionStringBuilder { DataSource = db, Mode = SqliteOpenMode.ReadOnly };
                    using var connection = new SqliteConnection(cs.ToString());
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT id, tokens, cost, created_at, providerID FROM message LIMIT 100000";
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var id = reader.GetValue(0)?.ToString();
                        if (id is not null && !seen.Add(id))
                        {
                            continue;
                        }

                        var tokens = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1));
                        var cost = reader.IsDBNull(2) ? (double?)null : Convert.ToDouble(reader.GetValue(2));
                        var created = reader.IsDBNull(3) ? _clock.Now : DateTimeOffset.TryParse(reader.GetValue(3)?.ToString(), out var parsed) ? parsed : _clock.Now;
                        var day = UsageHistoryDocument.FormatDay(created, TimeZoneInfo.Local);
                        byDay.TryGetValue(day, out var existing);
                        var addCost = cost is { } c && c > 0 ? c : 0;
                        byDay[day] = (existing.Tokens + tokens, existing.Cost + addCost);
                    }
                }
                catch (SqliteException)
                {
                }
            }
        }

        return byDay.Count == 0
            ? null
            : new ProviderUsageHistory(new DailyUsageSeries(byDay.OrderBy(kv => kv.Key).Select(kv => new DailyUsageEntry(kv.Key, kv.Value.Tokens, kv.Value.Cost > 0 ? kv.Value.Cost : null)).ToArray()));
    }
}
