using System.Text.Json;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Providers.Claude;

namespace WinLLMUsage.Providers.Tests;

public sealed class ClaudeMapperTests
{
    [Fact]
    public void MapsSessionWeeklyAndExtra()
    {
        const string json = """
            {
              "five_hour": { "utilization": 12, "resets_at": "2026-07-13T06:00:00.000Z" },
              "seven_day": { "utilization": 40, "resets_at": "2026-07-20T01:00:00.000Z" },
              "extra_usage": { "is_enabled": true, "used_credits": 120, "monthly_limit": 10000 }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var lines = ClaudeProvider.MapUsage(doc.RootElement, "Pro");
        Assert.Contains(lines, line => line is MetricLine.ProgressLine { Label: "Session", Used: 12 });
        Assert.Contains(lines, line => line is MetricLine.ProgressLine { Label: "Weekly", Used: 40 });
        Assert.Contains(lines, line => line is MetricLine.ProgressLine extra && extra.Label == "Extra usage spent" && extra.Used == 1.2);
    }
}
