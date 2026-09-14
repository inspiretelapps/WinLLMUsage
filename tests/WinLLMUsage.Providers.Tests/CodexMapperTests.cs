using System.Text.Json;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Providers.Codex;

namespace WinLLMUsage.Providers.Tests;

public sealed class CodexMapperTests
{
    [Fact]
    public void MapsBusinessPremiumAndCredits()
    {
        Assert.Equal("Business Premium", CodexProvider.MapPlan("self_serve_business_prolite"));
        Assert.Equal("Pro 20x", CodexProvider.MapPlan("pro"));
        const string json = """
            {
              "plan_type": "pro",
              "rate_limit": {
                "primary_window": { "used_percent": 10, "limit_window_seconds": 18000 },
                "secondary_window": { "used_percent": 25, "limit_window_seconds": 604800 }
              },
              "credits": { "balance": 10, "has_credits": true }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var lines = CodexProvider.MapUsage(doc.RootElement);
        Assert.Contains(lines, line => line is MetricLine.ProgressLine { Label: "Session", Used: 10 });
        var credits = Assert.IsType<MetricLine.ValuesLine>(lines.Single(l => l.Label == "Credits"));
        Assert.Equal(10, credits.Items.Single(v => v.Kind == MetricKind.Count).Number);
        Assert.Equal(0.4, credits.Items.Single(v => v.Kind == MetricKind.Dollars).Number, 5);
    }

    [Fact]
    public void MapsResetCredits()
    {
        const string json = """
            { "available_count": 2, "credits": [
              { "id": "RateLimitResetCredit_a", "status": "available", "expires_at": "2026-07-14T00:00:00.000Z" },
              { "id": "RateLimitResetCredit_b", "status": "used", "expires_at": "2026-07-13T00:00:00.000Z" }
            ] }
            """;
        using var doc = JsonDocument.Parse(json);
        var line = Assert.IsType<MetricLine.ValuesLine>(CodexProvider.MapResetCredits(doc.RootElement));
        Assert.Equal(2, line.Items[0].Number);
        Assert.Single(line.ExpiriesAt);
    }
}
