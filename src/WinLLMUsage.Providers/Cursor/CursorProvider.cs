using System.Text.Json;
using Microsoft.Data.Sqlite;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Http;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Cursor;

public sealed class CursorProvider : IProviderRuntime
{
    private readonly IHttpTransport _http;
    private readonly AppPaths _paths;
    private readonly IClock _clock;
    private string? _accessToken;

    public CursorProvider(IHttpTransport http, AppPaths paths, IClock clock)
    {
        _http = http;
        _paths = paths;
        _clock = clock;
        Provider = KnownProviders.Cursor();
        WidgetDescriptors = KnownProviders.CursorDescriptors();
    }

    public Provider Provider { get; }
    public IReadOnlyList<WidgetDescriptor> WidgetDescriptors { get; }

    public Task<bool> HasLocalCredentialsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ReadStateValue("cursorAuth/accessToken") is not null);

    public async Task<ProviderSnapshot> RefreshAsync(bool isManual, CancellationToken cancellationToken)
    {
        var access = _accessToken ?? ReadStateValue("cursorAuth/accessToken");
        if (string.IsNullOrWhiteSpace(access))
        {
            return ProviderSnapshot.Error(Provider, "Cursor is not signed in on this machine.", ErrorCategory.NotLoggedIn);
        }

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer " + access,
            ["Content-Type"] = "application/json",
            ["Connect-Protocol-Version"] = "1",
        };
        var usage = await ProviderAuthRetry.FetchAsync(
            _http,
            _ => Task.FromResult(JsonRequest.PostJson("https://api2.cursor.sh/aiserver.v1.DashboardService/GetCurrentPeriodUsage", "{}", headers, TimeSpan.FromSeconds(10))),
            _ => RefreshTokenAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);
        if (!usage.IsSuccess)
        {
            return ProviderSnapshot.Error(Provider, $"Cursor usage request failed ({usage.Status}).", ProviderAuthRetry.Classify(usage.Status));
        }

        using var doc = JsonDocument.Parse(usage.Body);
        var root = doc.RootElement;
        var lines = new List<MetricLine>();
        AddMeter(root, "totalUsage", "Total usage", "individualUsage", lines);
        AddMeter(root, "autoUsage", "Cursor Models", "auto", lines);
        AddMeter(root, "apiUsage", "Other Models", "api", lines);
        AddMeter(root, "onDemand", "On-demand", "onDemand", lines);
        var plan = ReadStateValue("cursorAuth/stripeMembershipType");

        try
        {
            var grok = await _http.SendAsync(JsonRequest.PostJson("https://api2.cursor.sh/aiserver.v1.DashboardService/GetSandUsageStatus", "{}", headers, TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);
            if (grok.IsSuccess)
            {
                using var grokDoc = JsonDocument.Parse(grok.Body);
                AddMeter(grokDoc.RootElement, "grokBot", "Grok Bot usage", "usedPercent", lines);
            }
        }
        catch (Exception)
        {
        }

        MetricLine.AppendNoDataIfNeeded(lines);
        return ProviderSnapshot.Make(Provider, plan, lines, _clock.Now);
    }

    private static void AddMeter(JsonElement root, string _, string label, string key, List<MetricLine> lines)
    {
        var node = root.GetObject(key) ?? root;
        var percent = node.GetDouble("usedPercent", "percent", "utilization", key);
        if (percent is { } p)
        {
            lines.Add(MetricLine.Progress(label, p, 100, ProgressFormat.Percent, Iso8601.DateFrom(node.GetString("resetsAt", "resets_at"))));
            return;
        }

        var used = node.GetDouble("used", "usedRequests", "usedCents");
        var limit = node.GetDouble("limit", "limitRequests", "limitCents");
        if (used is { } u && limit is { } l && l > 0)
        {
            var format = label.Contains("On-demand", StringComparison.OrdinalIgnoreCase) ? ProgressFormat.Dollars : ProgressFormat.Count("requests");
            var displayUsed = format == ProgressFormat.Dollars ? u / 100d : u;
            var displayLimit = format == ProgressFormat.Dollars ? l / 100d : l;
            lines.Add(MetricLine.Progress(label, displayUsed, displayLimit, format));
        }
    }

    private async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        var refresh = ReadStateValue("cursorAuth/refreshToken");
        if (string.IsNullOrWhiteSpace(refresh))
        {
            return false;
        }

        var body = JsonSerializer.Serialize(new
        {
            grant_type = "refresh_token",
            client_id = "KbZUR41cY7W6zRSdpSUJ7I7mLYBKOCmB",
            refresh_token = refresh,
        });
        var response = await _http.SendAsync(
            JsonRequest.PostJson("https://api2.cursor.sh/oauth/token", body, new Dictionary<string, string> { ["Content-Type"] = "application/json" }, TimeSpan.FromSeconds(15)),
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
        return true;
    }

    private string? ReadStateValue(string key)
    {
        var db = Path.Combine(_paths.RoamingAppData, "Cursor", "User", "globalStorage", "state.vscdb");
        if (!File.Exists(db))
        {
            return null;
        }

        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = db, Mode = SqliteOpenMode.ReadOnly };
            using var connection = new SqliteConnection(cs.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM ItemTable WHERE key = $key LIMIT 1";
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar()?.ToString();
        }
        catch (SqliteException)
        {
            return null;
        }
    }
}
