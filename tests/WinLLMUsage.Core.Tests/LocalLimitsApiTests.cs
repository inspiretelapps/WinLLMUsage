using System.Text;
using System.Text.Json;
using WinLLMUsage.Core.Catalog;
using WinLLMUsage.Core.Models;
using WinLLMUsage.Core.Serialization;
using WinLLMUsage.Core.Time;

namespace WinLLMUsage.Core.Tests;

public sealed class LocalLimitsApiTests
{
    [Fact]
    public void EncodesProgressResource()
    {
        var now = new DateTimeOffset(2026, 7, 13, 1, 39, 30, TimeSpan.Zero);
        var snapshot = new ProviderSnapshot(
            "codex",
            "Codex",
            [
                MetricLine.Progress("Session", 42, 100, ProgressFormat.Percent, now.AddHours(5), MetricPeriod.SessionMs),
            ],
            now,
            "Pro 20x");

        var state = new LocalUsageApi.State
        {
            EnabledOrderedIds = ["codex"],
            KnownIds = new HashSet<string> { "codex" },
            Snapshots = new Dictionary<string, ProviderSnapshot> { ["codex"] = snapshot },
            LimitDescriptors = new Dictionary<string, IReadOnlyList<WidgetDescriptor>>
            {
                ["codex"] = KnownProviders.CodexDescriptors(),
            },
            GeneratedAt = now,
        };

        var json = LocalLimitsApi.EncodeString(["codex"], state);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("openusage.limits.v1", root.GetProperty("schema").GetString());
        var session = root.GetProperty("providers").GetProperty("codex").GetProperty("resources").GetProperty("session");
        Assert.Equal("consumption", session.GetProperty("kind").GetString());
        Assert.Equal("percent", session.GetProperty("unit").GetString());
        Assert.Equal(42, session.GetProperty("used").GetDouble());
        Assert.Equal(100, session.GetProperty("limit").GetDouble());
        Assert.Equal(58, session.GetProperty("remaining").GetDouble());
        Assert.Equal(0.42, session.GetProperty("utilization").GetDouble(), 5);
        Assert.Equal(18000, session.GetProperty("windowSeconds").GetDouble());
        Assert.False(root.GetProperty("providers").GetProperty("codex").GetProperty("stale").GetBoolean());
    }

    [Fact]
    public void OmitsMissingResourcesRatherThanZero()
    {
        var snapshot = new ProviderSnapshot("ollama", "Ollama", [MetricLine.Progress("Session", 10, 100, ProgressFormat.Percent)], DateTimeOffset.UtcNow);
        var state = new LocalUsageApi.State
        {
            EnabledOrderedIds = ["ollama"],
            KnownIds = new HashSet<string> { "ollama" },
            Snapshots = new Dictionary<string, ProviderSnapshot> { ["ollama"] = snapshot },
            LimitDescriptors = new Dictionary<string, IReadOnlyList<WidgetDescriptor>>
            {
                ["ollama"] = KnownProviders.OllamaDescriptors(),
            },
            GeneratedAt = DateTimeOffset.UtcNow,
        };

        var json = LocalLimitsApi.EncodeString(["ollama"], state);
        using var doc = JsonDocument.Parse(json);
        var resources = doc.RootElement.GetProperty("providers").GetProperty("ollama").GetProperty("resources");
        Assert.True(resources.TryGetProperty("session", out _));
        Assert.False(resources.TryGetProperty("weekly", out _));
    }

    [Fact]
    public void UnknownProviderIs404()
    {
        var state = new LocalUsageApi.State
        {
            EnabledOrderedIds = [],
            KnownIds = new HashSet<string> { "codex" },
            Snapshots = new Dictionary<string, ProviderSnapshot>(),
            GeneratedAt = DateTimeOffset.UtcNow,
        };
        var response = LocalUsageApi.Respond("GET", "/v1/limits/nope", state);
        Assert.Equal(404, response.Status);
        Assert.Equal("""{"error":"provider_not_found"}""", Encoding.UTF8.GetString(response.Body!));
    }

    [Fact]
    public void OptionsIs204()
    {
        var state = EmptyState();
        var response = LocalUsageApi.Respond("OPTIONS", "/v1/limits", state);
        Assert.Equal(204, response.Status);
        Assert.Null(response.Body);
    }

    [Fact]
    public void PostIs405()
    {
        var response = LocalUsageApi.Respond("POST", "/v1/limits", EmptyState());
        Assert.Equal(405, response.Status);
    }

    [Fact]
    public void FamilyMatchIncludesClaudeCards()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new ProviderSnapshot("claude", "Claude", [MetricLine.NoUsageData], now);
        var b = new ProviderSnapshot("claude@abcd1234", "Claude Work", [MetricLine.NoUsageData], now);
        var state = new LocalUsageApi.State
        {
            EnabledOrderedIds = ["claude", "claude@abcd1234"],
            KnownIds = new HashSet<string> { "claude", "claude@abcd1234", "codex" },
            Snapshots = new Dictionary<string, ProviderSnapshot>
            {
                ["claude"] = a,
                ["claude@abcd1234"] = b,
            },
            GeneratedAt = now,
        };

        var response = LocalUsageApi.Respond("GET", "/v1/limits/claude", state);
        Assert.Equal(200, response.Status);
        using var doc = JsonDocument.Parse(response.Body!);
        var providers = doc.RootElement.GetProperty("providers");
        Assert.True(providers.TryGetProperty("claude", out _));
        Assert.True(providers.TryGetProperty("claude@abcd1234", out _));
    }

    [Fact]
    public void UsageRouteReturnsArray()
    {
        var snapshot = new ProviderSnapshot("zai", "Z.ai", [MetricLine.Progress("Session", 1, 100, ProgressFormat.Percent)], DateTimeOffset.UtcNow);
        var state = new LocalUsageApi.State
        {
            EnabledOrderedIds = ["zai"],
            KnownIds = new HashSet<string> { "zai" },
            Snapshots = new Dictionary<string, ProviderSnapshot> { ["zai"] = snapshot },
            GeneratedAt = DateTimeOffset.UtcNow,
        };
        var response = LocalUsageApi.Respond("GET", "/v1/usage", state);
        using var doc = JsonDocument.Parse(response.Body!);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal("zai", doc.RootElement[0].GetProperty("providerId").GetString());
        Assert.Equal("progress", doc.RootElement[0].GetProperty("lines")[0].GetProperty("type").GetString());
    }

    [Fact]
    public void OllamaExportsSessionAndWeekly()
    {
        var keys = KnownProviders.OllamaDescriptors().SelectMany(d => d.LimitResources.Select(r => r.Key)).ToArray();
        Assert.Contains("session", keys);
        Assert.Contains("weekly", keys);
    }

    private static LocalUsageApi.State EmptyState() => new()
    {
        EnabledOrderedIds = [],
        KnownIds = new HashSet<string>(),
        Snapshots = new Dictionary<string, ProviderSnapshot>(),
        GeneratedAt = DateTimeOffset.UtcNow,
    };
}
