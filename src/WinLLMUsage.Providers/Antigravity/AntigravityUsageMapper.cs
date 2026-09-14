using System.Text.Json;
using WinLLMUsage.Core.Formatting;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.Antigravity;

public static class AntigravityUsageMapper
{
    public static readonly (string BucketId, string Label, int PeriodMs)[] SummaryBuckets =
    [
        ("gemini-5h", "Session", MetricPeriod.SessionMs),
        ("gemini-weekly", "Weekly", MetricPeriod.WeekMs),
        ("3p-5h", "Claude", MetricPeriod.SessionMs),
        ("3p-weekly", "Claude Weekly", MetricPeriod.WeekMs),
    ];

    public static IReadOnlyList<MetricLine>? ParseQuotaSummary(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement groupArray;
        if (root.GetObject("response") is { } response && response.TryGetProperty("groups", out var nested) && nested.ValueKind == JsonValueKind.Array)
        {
            groupArray = nested;
        }
        else if (root.TryGetProperty("groups", out var direct) && direct.ValueKind == JsonValueKind.Array)
        {
            groupArray = direct;
        }
        else
        {
            return null;
        }

        var pooled = new Dictionary<string, (double Fraction, DateTimeOffset? Reset)>(StringComparer.Ordinal);
        foreach (var group in groupArray.EnumerateArray())
        {
            if (!group.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var bucket in buckets.EnumerateArray())
            {
                var id = bucket.GetString("bucketId", "bucket_id");
                if (id is null || !SummaryBuckets.Any(s => s.BucketId == id) || pooled.ContainsKey(id))
                {
                    continue;
                }

                var remaining = bucket.GetDouble("remainingFraction", "remaining_fraction");
                if (remaining is null || !double.IsFinite(remaining.Value))
                {
                    continue;
                }

                pooled[id] = (remaining.Value, Iso8601.DateFrom(bucket.GetString("resetTime", "reset_time")));
            }
        }

        var lines = new List<MetricLine>();
        foreach (var spec in SummaryBuckets)
        {
            if (!pooled.TryGetValue(spec.BucketId, out var entry))
            {
                continue;
            }

            var used = MetricFormatter.ClampPercent((1 - entry.Fraction) * 100);
            lines.Add(MetricLine.Progress(spec.Label, used, 100, ProgressFormat.Percent, entry.Reset, spec.PeriodMs));
        }

        return lines;
    }
}
