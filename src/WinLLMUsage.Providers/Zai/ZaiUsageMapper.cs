using System.Text.Json;
using WinLLMUsage.Core.Formatting;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Zai;

public static class ZaiUsageMapper
{
    public static bool IsNoCodingPlan(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
            {
                return (doc.RootElement.GetString("msg") ?? "").Contains("coding plan", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    public static string? PlanName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in data.EnumerateArray())
            {
                var name = item.GetString("productName");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    public static IReadOnlyList<MetricLine> MapQuota(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var container = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object ? data : root;
        if (!container.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Z.ai quota response is missing limits.");
        }

        if (limits.GetArrayLength() == 0)
        {
            return [MetricLine.NoUsageData];
        }

        var lines = new List<MetricLine>();
        foreach (var entry in limits.EnumerateArray())
        {
            var type = entry.GetString("type", "name");
            if (type is "CREDIT_LIMIT" or "TOKENS_LIMIT")
            {
                var window = Classify(entry);
                if (window is null)
                {
                    continue;
                }

                var percent = entry.GetDouble("percentage");
                if (percent is null)
                {
                    throw new InvalidDataException("Z.ai quota entry is missing percentage.");
                }

                lines.Add(MetricLine.Progress(
                    window.Value.Label,
                    MetricFormatter.ClampPercent(percent.Value),
                    100,
                    ProgressFormat.Percent,
                    entry.GetDouble("nextResetTime") is { } ms ? Iso8601.FromUnix(ms) : null,
                    window.Value.PeriodMs));
            }
            else if (type == "TIME_LIMIT")
            {
                var used = entry.GetDouble("currentValue");
                var limit = entry.GetDouble("usage");
                if (used is null || limit is null || used < 0 || limit < 0)
                {
                    throw new InvalidDataException("Z.ai TIME_LIMIT is missing used/limit.");
                }

                lines.Add(MetricLine.Progress(
                    "Web Searches",
                    used.Value,
                    limit.Value,
                    ProgressFormat.Count("searches"),
                    entry.GetDouble("nextResetTime") is { } ms ? Iso8601.FromUnix(ms) : null,
                    (int)Math.Min(int.MaxValue, MetricPeriod.MonthMs)));
            }
        }

        return lines.Count == 0 ? [MetricLine.NoUsageData] : lines;
    }

    private static (string Label, int PeriodMs)? Classify(JsonElement entry)
    {
        var unit = entry.GetDouble("unit");
        var number = entry.GetDouble("number");
        if (unit is null || number is not > 0)
        {
            throw new InvalidDataException("Z.ai quota window is invalid.");
        }

        var unitMs = unit.Value switch
        {
            3 => 60 * 60 * 1000d,
            4 => 24 * 60 * 60 * 1000d,
            6 => 7 * 24 * 60 * 60 * 1000d,
            5 => 30 * 24 * 60 * 60 * 1000d,
            _ => (double?)null,
        };
        if (unitMs is null)
        {
            return null;
        }

        var periodMs = (int)(unitMs.Value * number.Value);
        return periodMs < MetricPeriod.DayMs ? ("Session", periodMs) : ("Weekly", periodMs);
    }
}
