using System.Text.Json;
using WinLLMUsage.Core.Formatting;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Time;
using WinLLMUsage.Providers.Shared;

namespace WinLLMUsage.Providers.OpenCode;

public static class OpenCodeUsageMapper
{
    public static string? GoApiKey(string authJson)
    {
        using var doc = JsonDocument.Parse(authJson);
        if (!doc.RootElement.TryGetProperty("opencode-go", out var entry) || entry.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var key = entry.GetString("key");
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    public static bool HasCodexOAuth(string authJson)
    {
        using var doc = JsonDocument.Parse(authJson);
        if (!doc.RootElement.TryGetProperty("openai", out var entry) || entry.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (entry.GetString("type") != "oauth")
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(entry.GetString("access")) || !string.IsNullOrWhiteSpace(entry.GetString("refresh"));
    }

    public static IReadOnlyList<MetricLine> MapUsage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var usage = doc.RootElement.GetObject("usage") ?? throw new InvalidDataException("OpenCode usage response is missing usage.");
        return
        [
            Window(usage, "rolling", "Session", MetricPeriod.SessionMs),
            Window(usage, "weekly", "Weekly", MetricPeriod.WeekMs),
            Window(usage, "monthly", "Monthly", (int)Math.Min(int.MaxValue, MetricPeriod.MonthMs)),
        ];
    }

    private static MetricLine Window(JsonElement usage, string key, string label, int periodMs)
    {
        var window = usage.GetObject(key) ?? throw new InvalidDataException($"OpenCode usage is missing {key}.");
        var percent = window.GetDouble("percent") ?? throw new InvalidDataException($"OpenCode {key} is missing percent.");
        return MetricLine.Progress(
            label,
            MetricFormatter.ClampPercent(percent),
            100,
            ProgressFormat.Percent,
            Iso8601.DateFrom(window.GetString("resetsAt", "resets_at")),
            periodMs);
    }
}
