namespace WinLLMUsage.Core.Layout;

public static class DefaultLayout
{
    public const int MaxPinsPerProvider = 2;
    public const int UndoMaxDepth = 40;

    public static readonly string[] MetricIds =
    [
        "antigravity.geminiPro", "antigravity.geminiWeekly", "antigravity.claude", "antigravity.claudeWeekly",
        "antigravity.trend", "antigravity.today", "antigravity.yesterday", "antigravity.last30",

        "claude.session", "claude.weekly", "claude.fable", "claude.trend",
        "claude.extra", "claude.today", "claude.yesterday", "claude.last30",

        "codex.session", "codex.weekly", "codex.spark", "codex.sparkWeekly", "codex.trend",
        "codex.credits", "codex.rateLimitResets", "codex.today", "codex.yesterday", "codex.last30",

        "cursor.usage", "cursor.auto", "cursor.api", "cursor.grokBot", "cursor.trend",
        "cursor.onDemand", "cursor.today", "cursor.yesterday", "cursor.last30",

        "copilot.premium", "copilot.extra", "copilot.orgCredits", "copilot.orgSpend",
        "copilot.chat", "copilot.completions",

        "devin.daily", "devin.weekly", "devin.extra",

        "grok.weekly", "grok.trend",
        "grok.payAsYouGo", "grok.today", "grok.yesterday", "grok.last30",

        "ollama.session", "ollama.weekly", "ollama.last4Weeks",

        "opencode.session", "opencode.weekly", "opencode.monthly", "opencode.trend",
        "opencode.today", "opencode.yesterday", "opencode.last30",

        "openrouter.credits", "openrouter.balance",
        "openrouter.today", "openrouter.week", "openrouter.month", "openrouter.keyLimit",

        "zai.session", "zai.weekly", "zai.webSearches",
    ];

    public static readonly string[] MigrationBaselineMetricIds =
    [
        "claude.session", "claude.weekly", "claude.trend",
        "claude.extra", "claude.today", "claude.yesterday", "claude.last30",

        "codex.session", "codex.weekly", "codex.trend",
        "codex.credits", "codex.rateLimitResets", "codex.today", "codex.yesterday", "codex.last30",

        "devin.daily", "devin.weekly", "devin.extra",

        "grok.creditsUsed", "grok.trend",
        "grok.payAsYouGo", "grok.today", "grok.yesterday", "grok.last30",

        "cursor.usage", "cursor.auto", "cursor.api", "cursor.trend",
        "cursor.onDemand", "cursor.today", "cursor.yesterday", "cursor.last30",
    ];

    public static readonly string[] PinnedMetricIds =
    [
        "antigravity.geminiPro", "antigravity.geminiWeekly",
        "claude.session", "claude.weekly",
        "codex.session", "codex.weekly",
        "cursor.auto", "cursor.api",
        "copilot.premium",
        "ollama.session", "ollama.weekly",
        "openrouter.credits",
        "zai.session", "zai.weekly",
    ];

    public static readonly string[] ExpandedMetricIds =
    [
        "antigravity.claude", "antigravity.claudeWeekly",
        "antigravity.today", "antigravity.yesterday", "antigravity.last30",
        "claude.sonnet", "claude.today", "claude.yesterday", "claude.last30",
        "codex.spark", "codex.sparkWeekly",
        "codex.credits", "codex.rateLimitResets", "codex.today", "codex.yesterday", "codex.last30",
        "cursor.grokBot", "cursor.onDemand", "cursor.requests", "cursor.credits",
        "cursor.today", "cursor.yesterday", "cursor.last30",
        "copilot.orgCredits", "copilot.orgSpend", "copilot.chat", "copilot.completions",
        "devin.extra",
        "grok.payAsYouGo", "grok.today", "grok.yesterday", "grok.last30",
        "ollama.last4Weeks",
        "opencode.today", "opencode.yesterday", "opencode.last30",
        "openrouter.today", "openrouter.week", "openrouter.month", "openrouter.keyLimit",
        "zai.webSearches",
    ];

    public static readonly string[] CanonicalProviderOrder =
    [
        "claude", "codex", "cursor", "antigravity", "copilot", "devin", "grok", "ollama", "opencode", "openrouter", "zai",
    ];

    public static readonly IReadOnlyDictionary<string, string> SchemaV3Remaps = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["antigravity.session"] = "antigravity.geminiPro",
        ["antigravity.weekly"] = "antigravity.geminiWeekly",
        ["copilot.credits"] = "copilot.premium",
    };

    public static readonly string[] V2KnownProviders =
    [
        "antigravity", "claude", "codex", "copilot", "cursor", "devin", "grok", "openrouter", "zai",
    ];
}
