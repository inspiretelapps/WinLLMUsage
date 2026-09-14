using WinLLMUsage.Core.Models;
using WinLLMUsage.Providers.Antigravity;
using WinLLMUsage.Providers.Grok;
using WinLLMUsage.Providers.Ollama;
using WinLLMUsage.Providers.OpenCode;
using WinLLMUsage.Providers.Zai;

namespace WinLLMUsage.Providers.Tests;

public sealed class ProtocolMapperTests
{
    [Fact]
    public void ZaiMapsCreditAndTimeLimits()
    {
        const string json = """
            { "data": { "limits": [
              { "type": "CREDIT_LIMIT", "unit": 3, "number": 5, "percentage": 12, "nextResetTime": 1780000000000 },
              { "type": "CREDIT_LIMIT", "unit": 6, "number": 1, "percentage": 40, "nextResetTime": 1780600000000 },
              { "type": "TIME_LIMIT", "currentValue": 3, "usage": 100, "nextResetTime": 1781000000000 }
            ] } }
            """;
        var lines = ZaiUsageMapper.MapQuota(json);
        Assert.Contains(lines, l => l is MetricLine.ProgressLine { Label: "Session", Used: 12 });
        Assert.Contains(lines, l => l is MetricLine.ProgressLine { Label: "Weekly", Used: 40 });
        Assert.Contains(lines, l => l is MetricLine.ProgressLine { Label: "Web Searches", Used: 3, Limit: 100 });
    }

    [Fact]
    public void ZaiDetectsNoCodingPlan()
    {
        Assert.True(ZaiUsageMapper.IsNoCodingPlan("""{"success":false,"code":500,"msg":"No coding plan"}"""));
    }

    [Fact]
    public void OllamaMapsFractionUsageAndOmitsMissing()
    {
        const string json = """{"limits":{"session":{"usage":0.349},"weekly":{"usage":0.2}},"activity":{"cost":"1.25"}}""";
        var lines = OllamaUsageMapper.MapUsage(json);
        var session = Assert.IsType<MetricLine.ProgressLine>(lines.First(l => l.Label == "Session"));
        Assert.InRange(session.Used, 34.8, 35.0);
        Assert.Contains(lines, l => l is MetricLine.ValuesLine v && v.Label == "Last 4 Weeks");
    }

    [Fact]
    public void OpenCodeReadsNestedGoKeyAndMeters()
    {
        Assert.Equal("sk-go", OpenCodeUsageMapper.GoApiKey("""{"opencode-go":{"key":"sk-go"},"openai":{"type":"oauth","access":"tok"}}"""));
        Assert.True(OpenCodeUsageMapper.HasCodexOAuth("""{"openai":{"type":"oauth","access":"tok"}}"""));
        var lines = OpenCodeUsageMapper.MapUsage("""{"usage":{"rolling":{"percent":10},"weekly":{"percent":20},"monthly":{"percent":30}}}""");
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void GrokMapsWeeklyAndDisabledPayg()
    {
        var lines = GrokUsageMapper.MapCreditsConfig("""{"periodType":"RATE_LIMIT_PERIOD_TYPE_WEEKLY","usedPercent":22,"onDemandCap":0}""");
        Assert.Contains(lines, l => l is MetricLine.ProgressLine { Label: "Weekly limit", Used: 22 });
        Assert.Contains(lines, l => l is MetricLine.BadgeLine b && b.BadgeText == "Disabled");
    }

    [Fact]
    public void AntigravityMapsExactBucketIds()
    {
        const string json = "{\"groups\":[{\"buckets\":[{\"bucketId\":\"gemini-5h\",\"remainingFraction\":0.6},{\"bucketId\":\"gemini-weekly\",\"remainingFraction\":0.25}]}]}";
        var lines = AntigravityUsageMapper.ParseQuotaSummary(json);
        Assert.NotNull(lines);
        Assert.Contains(lines, l => l is MetricLine.ProgressLine p && p.Label == "Session" && p.Used == 40);
        Assert.DoesNotContain(lines, l => l.Label == "Claude");
    }
}
