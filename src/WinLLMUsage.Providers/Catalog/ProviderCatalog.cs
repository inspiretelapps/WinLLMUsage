using WinLLMUsage.Core.Contracts;
using WinLLMUsage.Infrastructure.Paths;
using WinLLMUsage.Providers.Antigravity;
using WinLLMUsage.Providers.Claude;
using WinLLMUsage.Providers.Codex;
using WinLLMUsage.Providers.Copilot;
using WinLLMUsage.Providers.Cursor;
using WinLLMUsage.Providers.Devin;
using WinLLMUsage.Providers.Grok;
using WinLLMUsage.Providers.Ollama;
using WinLLMUsage.Providers.OpenCode;
using WinLLMUsage.Providers.OpenRouter;
using WinLLMUsage.Providers.Zai;

namespace WinLLMUsage.Providers.Catalog;

public static class ProviderCatalog
{
    public static IReadOnlyList<IProviderRuntime> Create(
        IHttpTransport http,
        ISecretStore secrets,
        AppPaths paths,
        IClock clock,
        ISettingsStore settings)
    {
        return
        [
            new ClaudeProvider(http, paths, clock),
            new CodexProvider(http, paths, clock),
            new CursorProvider(http, paths, clock),
            new AntigravityProvider(http, paths, clock),
            new CopilotProvider(http, paths, clock, settings),
            new DevinProvider(http, paths, clock),
            new GrokProvider(http, paths, clock),
            new OllamaProvider(http, paths, clock),
            new OpenCodeProvider(http, paths, clock),
            new OpenRouterProvider(http, secrets, paths, clock),
            new ZaiProvider(http, secrets, paths, clock),
        ];
    }
}
