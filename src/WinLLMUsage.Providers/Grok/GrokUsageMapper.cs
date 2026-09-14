using System.Text.Json;
using WinLLMUsage.Core.Formatting;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Grok;

public static class GrokUsageMapper
{
    public const string WeeklyPeriodType = "RATE_LIMIT_PERIOD_TYPE_WEEKLY";

    public static IReadOnlyList<MetricLine> MapCreditsConfig(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var lines = new List<MetricLine>();
        var periodType = root.GetString("periodType", "period_type") ?? "";
        var used = root.GetDouble("usedPercent", "used_percent") ?? root.GetDouble("used") ?? 0;
        if (periodType.Contains("WEEKLY", StringComparison.OrdinalIgnoreCase) || periodType == WeeklyPeriodType)
        {
            lines.Add(MetricLine.Progress(
                "Weekly limit",
                MetricFormatter.ClampPercent(used),
                100,
                ProgressFormat.Percent,
                Iso8601.DateFrom(root.GetString("periodEnd", "period_end")) ?? Iso8601.FromUnix(root.GetDouble("periodEndSeconds") ?? double.NaN),
                root.GetInt64("periodDurationMs") is { } ms ? (int)ms : MetricPeriod.WeekMs));
        }

        var cap = root.GetDouble("onDemandCap", "on_demand_cap") ?? 0;
        lines.Add(MetricLine.Badge(
            "Pay as you go",
            cap > 0 ? $"{(cap == Math.Floor(cap) ? ((int)cap).ToString() : cap.ToString())} cap" : "Disabled",
            cap > 0 ? "#22c55e" : "#a3a3a3"));
        return lines;
    }

    public static string? PlanName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var plan = doc.RootElement.GetString("subscription_tier_display");
            return string.IsNullOrWhiteSpace(plan) ? null : plan.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
