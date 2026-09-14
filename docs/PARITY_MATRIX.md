# Parity matrix

Statuses: `Not Started` | `Implemented` | `Fixture Verified` | `Windows Verified` | `Blocked`

| Feature | Upstream source | Windows path | Status | Evidence |
| --- | --- | --- | --- | --- |
| Models / MetricLine / snapshots | `Sources/OpenUsage/Models/` | `src/WinLLMUsage.Core/Models/` | Fixture Verified | Core tests |
| Limits schema `openusage.limits.v1` | `LocalLimitsAPI.swift` | `src/WinLLMUsage.Core/Serialization/LocalLimitsApi.cs` | Fixture Verified | `LocalLimitsApiTests` |
| Legacy `/v1/usage` array | `LocalUsageAPI.swift` | `LocalUsageApi.cs` | Fixture Verified | Core tests |
| CLI parse / exit codes | `OpenUsageCLI/` | `src/WinLLMUsage.Cli/Program.cs` | Fixture Verified | `CliArgumentsTests` |
| Default layout / pins / v3 remaps | `DefaultLayout.swift`, `SettingsMigrator.swift` | `Layout/DefaultLayout.cs`, `Settings/SettingsMigrator.cs` | Fixture Verified | layout + migrator tests |
| Claude mapper | `ClaudeUsageMapper.swift` | `Providers/Claude/ClaudeProvider.cs` | Fixture Verified | `ClaudeMapperTests` |
| Codex mapper / plan names / reset credits | `CodexProvider.swift` | `Providers/Codex/CodexProvider.cs` | Fixture Verified | `CodexMapperTests` |
| Cursor | `Providers/Cursor/` | `Providers/Cursor/CursorProvider.cs` | Implemented | mapper + SQLite read-only; Windows DPAPI/state.vscdb encryption **Blocked** until a live Windows Cursor install is inspected |
| Antigravity | `Providers/Antigravity/` | `Providers/Antigravity/AntigravityProvider.cs` | Implemented | process/port discovery is best-effort; Windows OAuth/keyring **Blocked** |
| Copilot | `Providers/Copilot/` | `Providers/Copilot/CopilotProvider.cs` | Implemented | `gh auth token` fallback; hosts.json paths listed in auth sources |
| Devin | `Providers/Devin/` | `Providers/Devin/DevinProvider.cs` | Implemented | TOML credentials |
| Grok | `Providers/Grok/` | `Providers/Grok/GrokProvider.cs` | Implemented | `.grok/auth.json` + JSONL scan |
| Ollama | `Providers/Ollama/` | `Providers/Ollama/OllamaProvider.cs` | Implemented | Ed25519 via BouncyCastle; opt-in detection preserved |
| OpenCode | `Providers/OpenCode/` | `Providers/OpenCode/OpenCodeProvider.cs` | Implemented | auth.json + read-only `opencode*.db` |
| OpenRouter | `Providers/OpenRouter/` | `Providers/OpenRouter/OpenRouterProvider.cs` | Implemented | saved key / config / env |
| Z.ai | `Providers/ZAI/` | `Providers/Zai/ZaiProvider.cs` | Implemented | saved key / `ZAI_API_KEY` / `GLM_API_KEY` |
| JSONL scanner | `IncrementalJSONLScanner.swift` | `Infrastructure/Scanning/JsonlStreamingReader.cs` | Fixture Verified | 1 MiB skip test |
| Snapshot cache TTL | `ProviderSnapshotCache.swift` | `Infrastructure/Cache/SnapshotCache.cs` | Implemented | GUI vs CLI freshness flags |
| Local HTTP server :6736 | `LocalUsageServer.swift` | `Infrastructure/Api/LocalUsageServer.cs` | Implemented | loopback, 16-conn 503, CORS |
| Folder history sync | iCloud stores | not a full watcher yet | Implemented (document model only) | `UsageHistoryDocument` validation; folder transport **Not Started** beyond model |
| WPF tray dashboard | `App/` `Views/` | `src/WinLLMUsage.App/WindowsUi/` | Implemented (source) | **not Windows Verified**; macOS host is headless |
| Velopack installer | Sparkle | `scripts/package.ps1` | Implemented (script skeleton) | unsigned; no production feed |
| Claude Desktop DPAPI | Keychain AES-128-CBC | — | Blocked | format not inspected on Windows |
| Codex Windows secure store | Keychain `Codex Auth` | file `auth.json` only | Blocked | undocumented Windows vault |
| Cursor Electron safeStorage | macOS keychain + sqlite | sqlite keys only | Blocked | do not assume Electron DPAPI layout |

Planned product-level adaptations from the implementation plan §3 (tray instead of menu-bar strip, DPAPI, folder sync instead of iCloud, Velopack, manual Privacy Mode) are accepted differences, not bugs.
