using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.History;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Antigravity;

public sealed class AntigravityProvider : IProviderRuntime
{
    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;

    public AntigravityProvider(IHttpTransport http, AppPaths paths, IClock clock)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Antigravity();
        WidgetDescriptors = KnownProviders.AntigravityDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(DiscoverPort() is not null || Directory.Exists(Path.Combine(_paths.UserProfile, ".gemini")));

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var port = DiscoverPort();
        if (port is null)
        {
            return ProviderSnapshot.Error(Provider, "Antigravity language server is not running.", ErrorCategory.NotAvailable);
        }

        var url = $"https://127.0.0.1:{port}/quota-summary";
        HttpResponse response;
        try
        {
            response = await _http.SendAsync(new HttpRequest(HttpMethod.Get, new Uri(url), new Dictionary<string, string> { ["Accept"] = "application/json" }, Timeout: TimeSpan.FromSeconds(10), BypassProxy: true), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return ProviderSnapshot.Error(Provider, "Could not reach the Antigravity local quota endpoint.", ErrorCategory.Network);
        }

        if (!response.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Antigravity quota request failed ({response.Status}).", ProviderAuthRetry.Classify(response.Status));
        }

        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;
        var lines = new List<MetricLine>();
        AddPool(root, "gemini", "Session", "Weekly", "geminiPro", "geminiWeekly", lines);
        AddPool(root, "claude", "Claude", "Claude Weekly", "nonGemini", "nonGeminiWeekly", lines);
        var history = ScanConversations();
        lines.AddRange(SpendTileMapper.Lines(history, _clock.Now, TimeZoneInfo.Local, estimated: true, SpendTileMapper.Trend(history, _clock.Now, TimeZoneInfo.Local), "From your Antigravity conversations (estimated)"));
        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, root.GetString("plan"), lines, _clock.Now, history);
    }

    private static void AddPool(JsonElement root, string key, string sessionLabel, string weeklyLabel, string _, string __, List<MetricLine> lines)
    {
        var pool = root.GetObject(key) ?? root.GetObject(key + "Pool");
        if (pool is null)
        {
            return;
        }

        var session = pool.Value.GetObject("session") ?? pool.Value;
        var weekly = pool.Value.GetObject("weekly");
        var sessionUsed = session.GetDouble("used", "percent", "utilization") ?? 0;
        lines.Add(MetricLine.Progress(sessionLabel, sessionUsed, 100, ProgressFormat.Percent, Iso8601.DateFrom(session.GetString("resets_at", "resetsAt")), MetricPeriod.SessionMs));
        if (weekly is { } week)
        {
            var weeklyUsed = week.GetDouble("used", "percent", "utilization") ?? 0;
            lines.Add(MetricLine.Progress(weeklyLabel, weeklyUsed, 100, ProgressFormat.Percent, Iso8601.DateFrom(week.GetString("resets_at", "resetsAt")), MetricPeriod.WeekMs));
        }
    }

    private int? DiscoverPort()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.ProcessName.Contains("language_server", StringComparison.OrdinalIgnoreCase)
                    || process.ProcessName.Contains("agy", StringComparison.OrdinalIgnoreCase)
                    || process.ProcessName.Contains("antigravity", StringComparison.OrdinalIgnoreCase))
                {
                    // Marker arguments are not available without querying the command line.
                    // Probe common loopback ports used by the language server.
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        foreach (var port in new[] { 43431, 43432, 43433, 8080, 8000 })
        {
            if (CanConnect(port))
            {
                return port;
            }
        }

        return null;
    }

    private static bool CanConnect(int port)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(System.Net.IPAddress.Loopback, port);
            return task.Wait(TimeSpan.FromMilliseconds(150)) && client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private ProviderUsageHistory? ScanConversations()
    {
        var roots = new[]
        {
            Path.Combine(_paths.UserProfile, ".gemini", "antigravity", "conversations"),
            Path.Combine(_paths.UserProfile, ".gemini", "antigravity", "conversations"),
        };
        var byDay = new Dictionary<string, (long Tokens, double Cost)>(StringComparer.Ordinal);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    var tokens = doc.RootElement.GetInt64("total_tokens", "tokens") ?? 0;
                    var stamp = Iso8601.DateFrom(doc.RootElement.GetString("updated_at", "created_at")) ?? File.GetLastWriteTimeUtc(file);
                    var day = UsageHistoryDocument.FormatDay(stamp, TimeZoneInfo.Local);
                    byDay.TryGetValue(day, out var existing);
                    byDay[day] = (existing.Tokens + tokens, existing.Cost);
                }
                catch (Exception)
                {
                }
            }
        }

        return byDay.Count == 0
            ? null
            : new ProviderUsageHistory(new DailyUsageSeries(byDay.Select(kv => new DailyUsageEntry(kv.Key, kv.Value.Tokens, kv.Value.Cost)).OrderBy(d => d.Date).ToArray()));
    }
}
