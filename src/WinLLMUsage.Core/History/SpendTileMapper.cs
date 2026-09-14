using System.Globalization;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;

namespace WinLLMUsage.Core.History;

public static class SpendTileMapper
{
    public static IReadOnlyList<MetricLine> Lines(
        ProviderUsageHistory? history,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        bool estimated,
        IReadOnlyList<MetricChartPoint>? trend = null,
        string? trendNote = null)
    {
        var lines = new List<MetricLine>();
        var today = UsageHistoryDocument.FormatDay(now, timeZone);
        var yesterday = UsageHistoryDocument.FormatDay(now.AddDays(-1), timeZone);
        var windowStart = UsageHistoryDocument.FormatDay(now.AddDays(-UsageHistoryDocument.PreviousDays), timeZone);

        lines.Add(PeriodLine("Today", history, day => day == today, estimated));
        lines.Add(PeriodLine("Yesterday", history, day => day == yesterday, estimated));
        lines.Add(PeriodLine("Last 30 Days", history, day => string.CompareOrdinal(day, windowStart) >= 0, estimated));
        if (trend is { Count: > 0 })
        {
            lines.Add(MetricLine.Chart("Usage Trend", trend, trendNote));
        }

        return lines;
    }

    private static MetricLine PeriodLine(
        string label,
        ProviderUsageHistory? history,
        Func<string, bool> include,
        bool estimated)
    {
        long tokens = 0;
        double cost = 0;
        var hasCost = false;
        var unknown = new HashSet<string>(StringComparer.Ordinal);
        var models = new Dictionary<string, (long Tokens, double? Cost)>(StringComparer.OrdinalIgnoreCase);

        if (history is not null)
        {
            foreach (var day in history.Series.Daily.Where(d => include(d.Date)))
            {
                tokens += day.TotalTokens;
                if (day.CostUsd is { } usd)
                {
                    cost += usd;
                    hasCost = true;
                }
            }

            if (history.UnknownModelsByDay is not null)
            {
                foreach (var (day, names) in history.UnknownModelsByDay)
                {
                    if (include(day))
                    {
                        unknown.UnionWith(names);
                    }
                }
            }

            if (history.ModelUsage is not null)
            {
                foreach (var day in history.ModelUsage.Daily.Where(d => include(d.Date)))
                {
                    foreach (var model in day.Models)
                    {
                        models.TryGetValue(model.Model, out var existing);
                        models[model.Model] = (existing.Tokens + model.TotalTokens, (existing.Cost ?? 0) + (model.CostUsd ?? 0));
                    }
                }
            }
        }

        var values = new List<MetricValue>
        {
            new(hasCost ? cost : 0, MetricKind.Dollars, Estimated: estimated),
            new(tokens, MetricKind.Count, "tokens"),
        };

        ModelUsageBreakdown? breakdown = models.Count == 0
            ? null
            : new ModelUsageBreakdown(
                tokens,
                hasCost ? cost : null,
                models.Select(kv => new ModelUsageEntry(kv.Key, kv.Value.Tokens, kv.Value.Cost)).OrderByDescending(m => m.TotalTokens).ToArray(),
                estimated ? WidgetData.LocalEstimateNote : "");

        return MetricLine.Values(label, values, unknownModels: unknown.OrderBy(n => n).ToArray(), modelBreakdown: breakdown);
    }

    public static IReadOnlyList<MetricChartPoint> Trend(ProviderUsageHistory? history, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        if (history is null)
        {
            return [];
        }

        var points = new List<MetricChartPoint>();
        for (var i = UsageHistoryDocument.PreviousDays; i >= 0; i--)
        {
            var day = UsageHistoryDocument.FormatDay(now.AddDays(-i), timeZone);
            var entry = history.Series.Daily.FirstOrDefault(d => d.Date == day);
            var tokens = entry?.TotalTokens ?? 0;
            var label = TimeZoneInfo.ConvertTime(now.AddDays(-i), timeZone).ToString("MMM d", CultureInfo.GetCultureInfo("en-US"));
            points.Add(new MetricChartPoint(tokens, label, Formatting.MetricFormatter.Number(tokens, MetricKind.Count, Formatting.FormatStyle.Row) + " tokens"));
        }

        return points;
    }
}
