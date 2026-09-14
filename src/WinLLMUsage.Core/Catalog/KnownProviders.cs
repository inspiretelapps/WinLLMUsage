using WinLLMUsage.Core.Models;

namespace WinLLMUsage.Core.Catalog;

public static class KnownProviders
{
    public static Provider Claude(string id = "claude", string displayName = "Claude") => new(
        id,
        displayName,
        "claude",
        [
            new ProviderLink("Status", "https://status.anthropic.com/"),
            new ProviderLink("Dashboard", "https://claude.ai/settings/usage"),
        ]);

    public static Provider Codex() => new(
        "codex",
        "Codex",
        "codex",
        [
            new ProviderLink("Status", "https://status.openai.com/"),
            new ProviderLink("Dashboard", "https://chatgpt.com/codex/settings/usage"),
        ]);

    public static Provider Cursor() => new(
        "cursor",
        "Cursor",
        "cursor",
        [
            new ProviderLink("Status", "https://status.cursor.com/"),
            new ProviderLink("Dashboard", "https://www.cursor.com/dashboard"),
        ]);

    public static Provider Antigravity() => new("antigravity", "Antigravity", "antigravity");

    public static Provider Copilot() => new(
        "copilot",
        "Copilot",
        "copilot",
        [
            new ProviderLink("Status", "https://www.githubstatus.com/"),
            new ProviderLink("Dashboard", "https://github.com/settings/copilot"),
        ]);

    public static Provider Devin() => new(
        "devin",
        "Devin",
        "devin",
        [new ProviderLink("Dashboard", "https://app.devin.ai/")]);

    public static Provider Grok() => new(
        "grok",
        "Grok",
        "grok",
        [new ProviderLink("Usage", "https://grok.x.ai/")]);

    public static Provider Ollama() => new(
        "ollama",
        "Ollama",
        "ollama",
        [
            new ProviderLink("Usage", "https://ollama.com/"),
            new ProviderLink("API Keys", "https://ollama.com/settings/keys"),
        ]);

    public static Provider OpenCode() => new(
        "opencode",
        "OpenCode",
        "opencode",
        [new ProviderLink("Dashboard", "https://opencode.ai/")]);

    public static Provider OpenRouter() => new(
        "openrouter",
        "OpenRouter",
        "openrouter",
        [
            new ProviderLink("Activity", "https://openrouter.ai/activity"),
            new ProviderLink("Credits", "https://openrouter.ai/credits"),
        ]);

    public static Provider Zai() => new(
        "zai",
        "Z.ai",
        "zai",
        [
            new ProviderLink("Dashboard", "https://z.ai/"),
            new ProviderLink("API Keys", "https://z.ai/manage-apikey/apikey-list"),
        ]);

    public static IReadOnlyList<WidgetDescriptor> ClaudeDescriptors(Provider provider)
    {
        var list = new List<WidgetDescriptor>
        {
            WidgetDescriptorFactory.Percent($"{provider.Id}.session", provider, "Session", sessionStartSignal: SessionStartSignal.MissingResetDate)
                .ExportingLimit("session", "percent"),
            WidgetDescriptorFactory.Percent($"{provider.Id}.weekly", provider, "Weekly")
                .ExportingLimit("weekly", "percent"),
            WidgetDescriptorFactory.Percent($"{provider.Id}.fable", provider, "Fable")
                .ExportingLimit("fable", "percent"),
            WidgetDescriptorFactory.Percent($"{provider.Id}.sonnet", provider, "Sonnet")
                .ExportingLimit("sonnet", "percent"),
            WidgetDescriptorFactory.BoundedDollars($"{provider.Id}.extra", provider, "Extra Usage", 100, metricLabel: "Extra usage spent", valueWord: "spent")
                .ExportingLimit("extraUsage", "usd", source: LimitResourceSource.ProgressOrValue(MetricKind.Dollars)),
            WidgetDescriptorFactory.UsageTrend(provider)
                .ExportingHistory(HistoryScope.MachineLocal, estimatedCost: true, sourceNote: "From your Claude usage history (estimated)"),
        };
        list.AddRange(WidgetDescriptorFactory.SpendTiles(provider));
        return list;
    }

    public static IReadOnlyList<WidgetDescriptor> CodexDescriptors()
    {
        var provider = Codex();
        var list = new List<WidgetDescriptor>
        {
            WidgetDescriptorFactory.Percent("codex.session", provider, "Session").ExportingLimit("session", "percent"),
            WidgetDescriptorFactory.Percent("codex.weekly", provider, "Weekly").ExportingLimit("weekly", "percent"),
            WidgetDescriptorFactory.Percent("codex.spark", provider, "Spark").ExportingLimit("spark", "percent"),
            WidgetDescriptorFactory.Percent("codex.sparkWeekly", provider, "Spark Weekly").ExportingLimit("sparkWeekly", "percent"),
            WidgetDescriptorFactory.Values("codex.credits", provider, "Credits")
                .ExportingLimit("credits", "credits", LimitResourceKind.Balance, LimitResourceSource.Value(MetricKind.Count, "credits"))
                .ExportingLimit("creditValue", "usd", LimitResourceKind.Balance, LimitResourceSource.Value(MetricKind.Dollars)),
            WidgetDescriptorFactory.Values(
                    "codex.rateLimitResets",
                    provider,
                    "Rate Limit Resets",
                    selection: ValueSelection.Kind(MetricKind.Count),
                    traySuffix: "resets",
                    showsResetExpiries: true)
                .ExportingLimit("rateLimitResets", "resets", LimitResourceKind.Balance, LimitResourceSource.Value(MetricKind.Count, "available")),
            WidgetDescriptorFactory.UsageTrend(provider)
                .ExportingHistory(HistoryScope.MachineLocal, estimatedCost: true, sourceNote: "From your Codex logs (estimated)"),
        };
        list.AddRange(WidgetDescriptorFactory.SpendTiles(provider));
        return list;
    }

    public static IReadOnlyList<WidgetDescriptor> CursorDescriptors()
    {
        var provider = Cursor();
        var list = new List<WidgetDescriptor>
        {
            WidgetDescriptorFactory.Percent("cursor.usage", provider, "Total usage").ExportingLimit("totalUsage", "percent"),
            WidgetDescriptorFactory.Percent("cursor.auto", provider, "Cursor Models").ExportingLimit("autoUsage", "percent"),
            WidgetDescriptorFactory.Percent("cursor.api", provider, "Other Models").ExportingLimit("apiUsage", "percent"),
            WidgetDescriptorFactory.Percent("cursor.grokBot", provider, "Grok Bot usage").ExportingLimit("grokBot", "percent"),
            WidgetDescriptorFactory.BoundedDollars("cursor.onDemand", provider, "On-demand", 100, metricLabel: "On-demand")
                .ExportingLimit("onDemand", "usd", source: LimitResourceSource.ProgressOrValue(MetricKind.Dollars)),
            WidgetDescriptorFactory.BoundedCount("cursor.requests", provider, "Requests", 500, "requests")
                .ExportingLimit("requests", "requests"),
            WidgetDescriptorFactory.DollarBalance("cursor.credits", provider, "Credits", "left")
                .ExportingLimit("credits", "usd", LimitResourceKind.Balance, LimitResourceSource.Value(MetricKind.Dollars)),
            WidgetDescriptorFactory.UsageTrend(provider)
                .ExportingHistory(HistoryScope.AccountWide, estimatedCost: true, sourceNote: "From your Cursor usage export"),
        };
        list.AddRange(WidgetDescriptorFactory.SpendTiles(provider, WidgetData.CursorUsageHistoryNote));
        return list;
    }

    public static IReadOnlyList<WidgetDescriptor> AntigravityDescriptors()
    {
        var provider = Antigravity();
        var list = new List<WidgetDescriptor>
        {
            WidgetDescriptorFactory.Percent("antigravity.geminiPro", provider, "Session", sessionStartSignal: SessionStartSignal.ZeroUsage)
                .ExportingLimit("geminiSession", "percent"),
            WidgetDescriptorFactory.Percent("antigravity.geminiWeekly", provider, "Weekly")
                .ExportingLimit("geminiWeekly", "percent"),
            WidgetDescriptorFactory.Percent("antigravity.claude", provider, "Claude", sessionStartSignal: SessionStartSignal.ZeroUsage)
                .ExportingLimit("nonGeminiSession", "percent"),
            WidgetDescriptorFactory.Percent("antigravity.claudeWeekly", provider, "Claude Weekly")
                .ExportingLimit("nonGeminiWeekly", "percent"),
            WidgetDescriptorFactory.UsageTrend(provider)
                .ExportingHistory(HistoryScope.MachineLocal, estimatedCost: true, sourceNote: "From your Antigravity conversations (estimated)"),
        };
        list.AddRange(WidgetDescriptorFactory.SpendTiles(provider));
        return list;
    }

    public static IReadOnlyList<WidgetDescriptor> CopilotDescriptors()
    {
        var provider = Copilot();
        return
        [
            WidgetDescriptorFactory.Percent("copilot.premium", provider, "Credits")
                .ExportingLimit("premiumCredits", "credits", source: LimitResourceSource.ProgressOrValue(MetricKind.Count)),
            WidgetDescriptorFactory.Values("copilot.extra", provider, "Extra Usage")
                .ExportingLimit("extraUsage", "count", source: LimitResourceSource.Value(MetricKind.Count)),
            WidgetDescriptorFactory.Values("copilot.orgCredits", provider, "Org Credits")
                .ExportingLimit("orgCredits", "credits", source: LimitResourceSource.Value(MetricKind.Count, "credits")),
            WidgetDescriptorFactory.Values("copilot.orgSpend", provider, "Org Spend")
                .ExportingLimit("orgSpend", "usd", source: LimitResourceSource.Value(MetricKind.Dollars)),
            WidgetDescriptorFactory.Percent("copilot.chat", provider, "Chat").ExportingLimit("chat", "percent"),
            WidgetDescriptorFactory.Percent("copilot.completions", provider, "Completions").ExportingLimit("completions", "percent"),
        ];
    }

    public static IReadOnlyList<WidgetDescriptor> DevinDescriptors()
    {
        var provider = Devin();
        return
        [
            WidgetDescriptorFactory.Percent("devin.daily", provider, "Daily quota").ExportingLimit("daily", "percent"),
            WidgetDescriptorFactory.Percent("devin.weekly", provider, "Weekly quota").ExportingLimit("weekly", "percent"),
            WidgetDescriptorFactory.DollarBalance("devin.extra", provider, "Extra usage balance", "left")
                .ExportingLimit("extraUsageBalance", "usd", LimitResourceKind.Balance, LimitResourceSource.Value(MetricKind.Dollars)),
        ];
    }

    public static IReadOnlyList<WidgetDescriptor> GrokDescriptors()
    {
        var provider = Grok();
        var list = new List<WidgetDescriptor>
        {
            WidgetDescriptorFactory.Percent("grok.weekly", provider, "Weekly limit").ExportingLimit("weekly", "percent"),
            WidgetDescriptorFactory.Badge("grok.payAsYouGo", provider, "Pay as you go"),
            WidgetDescriptorFactory.UsageTrend(provider)
                .ExportingHistory(HistoryScope.MachineLocal, estimatedCost: true, sourceNote: "From your Grok logs (estimated)"),
        };
        list.AddRange(WidgetDescriptorFactory.SpendTiles(provider));
        return list;
    }

    public static IReadOnlyList<WidgetDescriptor> OllamaDescriptors()
    {
        var provider = Ollama();
        return
        [
            WidgetDescriptorFactory.Percent("ollama.session", provider, "Session").ExportingLimit("session", "percent"),
            WidgetDescriptorFactory.Percent("ollama.weekly", provider, "Weekly").ExportingLimit("weekly", "percent"),
            WidgetDescriptorFactory.Values("ollama.last4Weeks", provider, "Last 4 Weeks", valueWord: "spent"),
        ];
    }

    public static IReadOnlyList<WidgetDescriptor> OpenCodeDescriptors()
    {
        var provider = OpenCode();
        var list = new List<WidgetDescriptor>
        {
            WidgetDescriptorFactory.Percent("opencode.session", provider, "Session", sessionStartSignal: SessionStartSignal.ZeroUsage)
                .ExportingLimit("session", "percent"),
            WidgetDescriptorFactory.Percent("opencode.weekly", provider, "Weekly").ExportingLimit("weekly", "percent"),
            WidgetDescriptorFactory.Percent("opencode.monthly", provider, "Monthly").ExportingLimit("monthly", "percent"),
            WidgetDescriptorFactory.UsageTrend(provider)
                .ExportingHistory(HistoryScope.MachineLocal, estimatedCost: false, sourceNote: "From your OpenCode logs"),
        };
        list.AddRange(WidgetDescriptorFactory.SpendTiles(provider));
        return list;
    }

    public static IReadOnlyList<WidgetDescriptor> OpenRouterDescriptors()
    {
        var provider = OpenRouter();
        return
        [
            WidgetDescriptorFactory.BoundedDollars("openrouter.credits", provider, "Credits", 100)
                .ExportingLimit("credits", "usd"),
            WidgetDescriptorFactory.DollarBalance("openrouter.balance", provider, "Balance", "left")
                .ExportingLimit("balance", "usd", LimitResourceKind.Balance, LimitResourceSource.Value(MetricKind.Dollars)),
            WidgetDescriptorFactory.Combined("openrouter.today", provider, "Today", isUsagePeriod: true),
            WidgetDescriptorFactory.Combined("openrouter.week", provider, "This Week", isUsagePeriod: true),
            WidgetDescriptorFactory.Combined("openrouter.month", provider, "This Month", isUsagePeriod: true),
            WidgetDescriptorFactory.BoundedDollars("openrouter.keyLimit", provider, "Key Limit", 100)
                .ExportingLimit("keyLimit", "usd"),
        ];
    }

    public static IReadOnlyList<WidgetDescriptor> ZaiDescriptors()
    {
        var provider = Zai();
        return
        [
            WidgetDescriptorFactory.Percent("zai.session", provider, "Session").ExportingLimit("session", "percent"),
            WidgetDescriptorFactory.Percent("zai.weekly", provider, "Weekly").ExportingLimit("weekly", "percent"),
            WidgetDescriptorFactory.BoundedCount("zai.webSearches", provider, "Web Searches", 1000, "searches")
                .ExportingLimit("webSearches", "searches"),
        ];
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<WidgetDescriptor>> AllDescriptors()
    {
        return new Dictionary<string, IReadOnlyList<WidgetDescriptor>>(StringComparer.Ordinal)
        {
            ["claude"] = ClaudeDescriptors(Claude()),
            ["codex"] = CodexDescriptors(),
            ["cursor"] = CursorDescriptors(),
            ["antigravity"] = AntigravityDescriptors(),
            ["copilot"] = CopilotDescriptors(),
            ["devin"] = DevinDescriptors(),
            ["grok"] = GrokDescriptors(),
            ["ollama"] = OllamaDescriptors(),
            ["opencode"] = OpenCodeDescriptors(),
            ["openrouter"] = OpenRouterDescriptors(),
            ["zai"] = ZaiDescriptors(),
        };
    }
}
