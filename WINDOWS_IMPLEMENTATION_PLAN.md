# WinLLMUsage: Windows Port Implementation Plan

Prepared: 14 September 2026  
Reference release: [OpenUsage v0.7.11](https://github.com/robinebers/openusage/releases/tag/v0.7.11)  
Verified source commit: `753e2fe4bc82a011567d16d8deb88176b58ed24e`  
Target workspace: `/Users/jaco/codex/winllmusage`  
Deliverable from this document: an installable Windows application, shared provider engine, CLI, compatibility API, tests, and operating instructions.

## 1. Implementation Decision

Build **WinLLMUsage** in **C# on .NET 10 LTS**, using **WPF** for the desktop interface and a Windows notification-area icon. Translate the reference release's models, provider behavior, pricing rules, and relevant regression tests into a shared C# engine. Rebuild its operating-system integrations for Windows.

The inspected release is a Swift 6.2 package using SwiftUI, AppKit, macOS Keychain, Sparkle, and Apple-specific services. Its active implementation is not Tauri; the repository identifies `tauri-legacy` as the older edition. Starting from that branch would require separately recovering the behavior added through v0.7.11. Use the exact tagged Swift source as the behavioral specification. [Source architecture](https://github.com/robinebers/openusage/blob/v0.7.11/docs/architecture.md), [package manifest](https://github.com/robinebers/openusage/blob/v0.7.11/Package.swift).

WPF is appropriate for this Windows-only utility because it provides native desktop controls, data binding, graphics, and Windows integration. Keep Windows APIs outside the core engine so parsing and provider tests can run on other platforms. .NET 10 is an LTS release. Pin an available serviced .NET 10 SDK and exact package versions when implementation starts. [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/), [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

“In one shot” means **one execution brief, one continuous implementation effort, and one integrated delivery**, with build/test checkpoints inside that effort. It does not mean skipping verification or promising that undocumented Windows credential formats will work without inspecting them. Do not stop after producing a tray mockup or only implementing Claude and Codex.

## 2. Fixed Scope and Assumptions

| Area | Decision |
| --- | --- |
| Primary platform | Supported Windows 11 x64, standard non-administrator account. |
| Architecture | Ship `win-x64`; keep source ready for ARM64, but do not advertise ARM64 support until its native dependencies, installer, and runtime are tested. |
| Windows 10 | Not a release acceptance target. Do not claim support based only on a successful build. |
| Product identity | Working name **WinLLMUsage**, independent icon, package ID, data directory, update feed, and diagnostics. |
| Provider coverage | All 11 reference providers: Claude, Codex, Cursor, Antigravity, Copilot, Devin, Grok, Ollama, OpenCode, OpenRouter, Z.ai. |
| Primary experience | Background tray utility with a compact dashboard, settings, customization, and optional floating pinned-metric strip. |
| Distribution | Self-contained x64 installer and portable ZIP; no separately installed .NET, Node, Python, Swift, SQLite CLI, or Unix tools required. |
| Data | Local collection, local history, provider requests, and public pricing downloads. No account system or hosted backend. |
| WSL | Native Windows discovery first. Support explicitly selected readable WSL log roots; automated WSL login/credential manipulation is outside this delivery. |
| Release execution | Build and verify local artifacts. Publishing and production signing require the owner's actual release configuration; never invent credentials or an update URL. |

Keep the upstream MIT copyright/license with translated or copied material. Use original fork branding and state the upstream origin in About and notices, following the repository's [license](https://github.com/robinebers/openusage/blob/v0.7.11/LICENSE) and [trademark policy](https://github.com/robinebers/openusage/blob/v0.7.11/TRADEMARK.md). Provider names and machine-facing compatibility identifiers remain recognizable; the application must not imply official upstream endorsement.

## 3. What Must Survive the Port

The following are requirements, not optional follow-up work:

- Provider-grouped quota meters, credits/balances, reset times, plan names, errors, warnings, and unavailable-data states.
- Local daily spend and tokens, Today / Yesterday / Last 30 Days, trends, model breakdowns, unknown-pricing notices, and Total Spend.
- Used/remaining display, countdown/exact reset display, pacing, compact/default density, light/dark/system theme, 12/24-hour time, and reduced motion.
- Provider enablement, deterministic provider/metric order, drag reordering, Always Visible / On Demand, default reset, layout undo behavior, and up to two pins per provider.
- First-run local detection and detection of newly introduced providers without overriding existing user choices. Ollama stays explicitly opt-in.
- Claude account/organization separation and source deduplication; stable provider/family matching for automation.
- Immediate cached display, background refresh, manual refresh, successful-data retention on failure, stale indicators, cancellation, and bounded provider timeouts.
- Global shortcut, launch at login, quota notifications, redacted rotating logs, provider links, share-card export/copy, and a working Exit action.
- Shared one-shot CLI and the existing limits/legacy usage JSON contracts.
- OpenRouter/Z.ai API-key settings, proxy configuration, bundled pricing and refresh, Codex fallback-pricing selection.
- Codex reset-credit display and deliberate reset-credit claiming, preserving the reference's confirmation and idempotency behavior.

Derive exact defaults, metric IDs, resource IDs, and formatting from the tagged code. The README is not a complete feature inventory. For example, the Ollama provider exports `session` and `weekly` even though the reference local-API documentation's resource table omits Ollama. Source descriptors and contract tests settle such discrepancies. [Provider catalog](https://github.com/robinebers/openusage/blob/v0.7.11/Sources/OpenUsage/Providers/ProviderCatalog.swift), [Ollama descriptors](https://github.com/robinebers/openusage/blob/v0.7.11/Sources/OpenUsage/Providers/Ollama/OllamaProvider.swift).

### Explicit Windows Adaptations

| macOS behavior | Windows implementation and parity boundary |
| --- | --- |
| Arbitrarily wide menu-bar text/bar strip | One tray icon plus pinned metrics at the dashboard top. Add a user-enabled, draggable, always-on-top compact strip for persistent text/bar visibility. Do not assume Windows offers an equivalent arbitrary-width taskbar slot. |
| `NSPanel` dashboard | Borderless WPF tool window anchored to the tray when possible, otherwise to the relevant monitor work area. |
| Keychain | Exact provider-specific Windows storage adapters; DPAPI for secrets owned by WinLLMUsage. A Keychain service name is not evidence of a Windows Credential Manager target. |
| iCloud private-container sync | Replace with opt-in folder-based normalized-history sync, using a user-selected folder, including a OneDrive-backed folder if desired. Preserve history/account merge semantics. This does **not** automatically join upstream's private iCloud container or sync with the unchanged Mac app. |
| Sparkle updates | Velopack installer/update integration with independent stable/beta feeds; hide update controls in unconfigured development builds. |
| Automatic screen-capture detection | Provide a manual Privacy Mode that hides values in the tray tooltip, dashboard, floating strip, and notifications. Do not promise universal capture detection or protection against every capture application. |
| Apple visual effects and haptics | Windows theme and solid high-contrast surfaces; optional transparency only if reliable. Apple-specific glass, haptics, and decorative Easter eggs are outside functional parity. |
| Upstream analytics/crash collection | Local diagnostics by default; no traffic to the upstream author's telemetry project. Remote telemetry is outside this delivery. |
| macOS CLI symlink | Stable per-user Windows launcher for `winllmusage.exe`, with an optional `openusage` compatibility alias that never overwrites an existing command. |

These are the only planned product-level parity exceptions. Unsupported credential sources discovered during implementation are **additional gaps to report**, not silently approved scope reductions.

## 4. Solution Layout and Dependencies

Create this structure; the upstream checkout is a reference, not the shipping application:

```text
WinLLMUsage.sln
global.json
Directory.Build.props
Directory.Packages.props
NuGet.config
src/
  WinLLMUsage.Core/             # net10.0: models, pricing, mapping, aggregation
  WinLLMUsage.Providers/        # net10.0: provider clients, auth policies, scanners
  WinLLMUsage.Infrastructure/   # net10.0: cache, HTTP, SQLite, files, sync
  WinLLMUsage.Windows/          # net10.0-windows: DPAPI, processes, shell APIs
  WinLLMUsage.App/              # net10.0-windows: WPF, WinForms tray interop
  WinLLMUsage.Cli/              # Windows console host over the same engine
tests/
  WinLLMUsage.Core.Tests/
  WinLLMUsage.Providers.Tests/
  WinLLMUsage.Infrastructure.Tests/
  WinLLMUsage.Windows.Tests/
  WinLLMUsage.App.Tests/
  Fixtures/                    # synthetic/sanitized payloads, logs, databases
scripts/
  build.ps1
  test.ps1
  package.ps1
  verify-install.ps1
  verify-contracts.ps1
docs/
  UPSTREAM_BASELINE.md
  PARITY_MATRIX.md
  WINDOWS_AUTH_SOURCES.md
  BUILD.md
  WINDOWS_VERIFICATION.md
  PRIVACY.md
  RELEASE.md
  THIRD_PARTY_NOTICES.md
.github/workflows/windows-ci.yml
.github/workflows/windows-release.yml
vendor/openusage/               # pinned read-only reference; excluded from packages
artifacts/                     # ignored generated outputs
```

Dependencies should have a specific purpose:

| Component | Selection |
| --- | --- |
| UI | WPF; `CommunityToolkit.Mvvm` for observable view models and commands. |
| Tray | Framework `System.Windows.Forms.NotifyIcon`, isolated behind `ITrayService`; enable WPF/WinForms interop only in the app project. |
| Composition | `Microsoft.Extensions.Hosting`, dependency injection, logging, configuration. |
| HTTP | `HttpClient` / `SocketsHttpHandler`; ASP.NET Core Kestrel for the loopback API. |
| Serialization | `System.Text.Json`; explicitly controlled wire JSON rather than default object serialization. |
| Databases | `Microsoft.Data.Sqlite`, including appropriate native SQLite binaries in published output. |
| Secrets | `System.Security.Cryptography.ProtectedData`; targeted Win32 credential access where a provider format has been verified. |
| Structured files | Maintained TOML/YAML parsers for existing credential formats; do not parse these with ad hoc regular expressions. |
| Ollama signatures | Maintained Ed25519 implementation, such as `BouncyCastle.Cryptography`, with tests against the reference key-container parser and signing vectors. Do not implement the cryptographic primitive. |
| Distribution | Velopack SDK and repository-local `vpk` tool with pinned versions. |
| Tests | xUnit, Microsoft test SDK, and a Windows UI Automation harness where reliable; local HTTP fixtures instead of live-account tests in CI. |

Use nullable reference types, deterministic builds, package lock files, explicit central package versions, and build warnings as errors for project code. Avoid WPF references in the CLI/core and avoid trimming or Native AOT for the first complete delivery. Document any additional dependency and its license. [NotifyIcon](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-10.0), [Velopack Windows overview](https://docs.velopack.io/packaging/operating-systems/windows).

### Dependency Direction

```text
WPF App ----\
             > Composition -> Providers -> Core
CLI --------/        |           |
                     +---- Infrastructure
                     +---- Windows adapters

Local API -> shared serializers -> current snapshots
```

Define narrow injectable contracts: `IProviderRuntime`, `ICredentialSource`, `ICredentialWriter`, `IProviderPaths`, `IUsageScanner`, `IClock`, `IHttpTransport`, `ISnapshotRepository`, `IRefreshCoordinator`, `ISecretStore`, `IHistorySyncTransport`, and platform UI services. Constructors receive dependencies; provider mappers must not reach into WPF, the registry, or global singletons.

## 5. Reference-to-Windows Work Map

Paths below are relative to the tagged upstream repository.

| Reference source | Work to carry over |
| --- | --- |
| `Models/`, `Providers/ProviderRuntime.swift`, `ProviderCatalog.swift` under `Sources/OpenUsage/` | Typed snapshots, metric kinds, descriptors, provider order, account/family IDs. |
| `Sources/OpenUsage/Providers/<Provider>/` | Request schemas, headers, auth selection/refresh rules, optional endpoint handling, mappers, scanner logic. |
| `Sources/OpenUsage/Pricing/` and `Resources/pricing_*.json` | Catalog precedence, aliases, bundled data, fallback selection, per-request pricing. |
| `Sources/OpenUsage/Providers/IncrementalJSONLScanner.swift`, `JSONLStreamingReader.swift`, `JSONLScanCacheStore.swift` | Bounded scanning, invalidation, persisted parse reuse, per-source identity. |
| `Sources/OpenUsage/Stores/WidgetDataStore.swift`, `ProviderSnapshotCache.swift`, `ProviderRefreshDeadline.swift` | Refresh scheduling, successful snapshots, timeout behavior, cache identity. |
| `Sources/OpenUsage/Stores/ProviderAccountsStore.swift`, `Services/ProviderAccountAssembly.swift` | Stable account cards and isolation of account-specific data. |
| `Sources/OpenUsage/Stores/DefaultLayout.swift`, `LayoutStore*`, `App/SettingsMigrator.swift` | Exact defaults, settings migration, pin-ID repair and schema-v3 behavior. |
| `Sources/OpenUsage/Services/LocalLimitsAPI.swift`, `LocalUsageAPI.swift`, `LocalUsageServer.swift` | Wire contracts, route semantics, error status codes. |
| `Sources/OpenUsageCLI/` | One-shot argument behavior, freshness, output and exit codes. |
| `Sources/OpenUsage/Support/Pace*`, `Stores/QuotaNotificationEvaluator.swift` | Pace thresholds, reset windows, notification transition/dedup rules. |
| `Sources/OpenUsage/Services/UsageHistoryAggregator.swift`, `Models/UsageHistoryDocument.swift` and sync stores located during inventory | Machine-local versus account-wide history, account-aware merges, versioned documents. |
| `Sources/OpenUsage/App/`, `Views/`, platform-specific `Services/` | Reimplement user interactions and platform behavior, using upstream screenshots and source as reference. |
| `Tests/OpenUsageTests/`, `Tests/OpenUsageCLITests/` | Translate behavioral regressions into C# tests and language-neutral fixtures. |

Inventory every reference feature into `PARITY_MATRIX.md` with source path, Windows implementation path, status, test evidence, and any approved adaptation from section 3. Use statuses `Not Started`, `Implemented`, `Fixture Verified`, `Windows Verified`, and `Blocked`; a visible card alone does not mean a provider works.

## 6. Credentials and Provider Implementation

### Shared Windows Rules

1. Resolve Windows known folders through platform APIs; do not hardcode `C:\Users`, reinterpret every macOS `Library` path as `%APPDATA%`, or assume every application follows XDG conventions.
2. For app-owned settings use `%LOCALAPPDATA%\WinLLMUsage`. Separate settings, snapshots, scan cache, history, logs, and encrypted secrets. An optional app-data override is for tests/portable use, not an implicit move of provider credentials.
3. Honor provider-supported home overrides. Treat source paths in the table below as candidates until verified against Windows provider source/documentation or an actual Windows installation. Record exact app versions, paths, credential formats, keys, precedence, and validation evidence in `WINDOWS_AUTH_SOURCES.md`.
4. Allow an explicit source-directory override through settings for nonstandard installations. File selection cannot make an unsupported encrypted credential format valid.
5. Read configured process/user/machine environment values without launching a login shell or executing PowerShell profiles. Document restart behavior for environment changes and saved-key precedence.
6. Never enumerate unrelated credentials. Read only exact verified provider credential targets, under the current user. Save WinLLMUsage-owned API keys and derived tokens using current-user DPAPI and restrictive file ACLs. DPAPI protection is per-user protection, not isolation from every other process running as that user. [Microsoft DPAPI guidance](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection).
7. Treat access tokens, refresh tokens, cookies, CSRF arguments, proxy passwords, and raw credential files as secrets. Redact before logging; no secrets in exceptions, fixtures, snapshots, CLI output, telemetry, or sync files.
8. Preserve each source's ownership rules. Read companion SQLite databases read-only. Rotate tokens only where the reference provider's policy permits it and the Windows storage format has been verified. Before writing, reread and compare the source identity/generation, merge unrelated fields, and atomically replace without clobbering a concurrent login.
9. Serialize auth refresh across the GUI and CLI per source/account. WinLLMUsage's process lock cannot force a companion app to cooperate: detect races, stop unsafe writes, and show a re-login action when necessary.
10. Do not auto-start WSL distributions, scrape browsers, elevate privileges, or use generic token-paste workarounds to claim unsupported companion authentication works. Keep partial local spend usable when subscription authentication is unavailable.

Electron uses DPAPI on Windows, but this does not establish a particular application's cache encoding or decryption procedure. Inspect the actual format before implementing a narrowly scoped decoder. Do not reuse macOS AES/keychain assumptions. [Electron safeStorage](https://www.electronjs.org/docs/latest/api/safe-storage).

### Provider Matrix

All endpoint details below describe the **tagged implementation**, not a guarantee that these private APIs will remain stable. Copy request/header/refresh behavior from source and test non-success responses explicitly. Reference behavior is documented in the tagged [provider documentation](https://github.com/robinebers/openusage/tree/v0.7.11/docs/providers).

| Provider | Windows source work | Provider behavior and critical acceptance case |
| --- | --- | --- |
| **Claude** | Start with `CLAUDE_CONFIG_DIR` or user-profile `.claude/.credentials.json`; environment fallback. Inspect Windows Claude Desktop config, account-prefixed token caches, and Cowork paths separately. Desktop tokens stay read-only. | Port `/api/oauth/usage`, Code token refresh, credential fallback and scope handling. Separate account + organization cards; deduplicate the same identity across Code/Desktop. Preserve local spend when limits are unavailable; never assign unattributed sessions to one of several known accounts. |
| **Codex** | Honor `CODEX_HOME`, reference fallback homes, auth files, and only verified Windows secure-store formats. Read `sessions` and `archived_sessions`; include pi and eligible OpenCode OAuth history. | Port `backend-api/wham/usage`, refresh/retry, window classification by duration, credits, Spark windows, Business Premium mapping, and reset-credit flow. API-key-only auth must not masquerade as subscription auth. |
| **Cursor** | Verify `%APPDATA%\Cursor\User\globalStorage\state.vscdb`, custom/portable user-data roots, and current auth keys/encryption. Replace sqlite3 subprocesses with read-only SQLite access. | Connect RPC plus combined REST Enterprise/team fallback and CSV history. Preserve plan-dependent units, optional Grok Bot/credit lookups, refresh retry and partial success. Do not sum account-wide Cursor spend once per machine. |
| **Antigravity** | Discover Windows `language_server`/`agy` processes, marker arguments and listening ports. Verify actual Windows OAuth/keyring targets and `.gemini/antigravity*/conversations` layouts. | Port quota-summary-first behavior and legacy fallbacks; two shared pools with session/weekly windows. Scan all supported conversation stores and protobuf accounting records. Derived auth cache stays tied to the current source login. |
| **Copilot** | Locate Windows Copilot `apps.json`/`hosts.json` and GitHub CLI config. Respect `GH_CONFIG_DIR` where applicable. Prefer a verified `gh auth token --hostname github.com` subprocess fallback over guessing vault encoding; capture output privately with a timeout. | Port `copilot_internal/user`, required client headers, org-billing discovery/cache and optional failures. Handle paid percentage, free quota, and org-managed raw-credit responses without dividing by zero. |
| **Devin** | Verify the Windows CLI `credentials.toml` location and app `state.vscdb`; preserve configured API-server selection. | Port `GetUserStatus`, CLI-to-app auth fallback, daily/weekly behavior, remaining-to-used conversion and extra balance. |
| **Grok** | Verify `.grok/auth.json`, `GROK_HOME`, and session layout on Windows. Preserve unrelated fields/accounts during allowed rotation. | Port billing/settings and refresh retry; count completed subagent/resumed/forked events once per model. Prefer recorded cost, then estimate. |
| **Ollama** | Resolve the actual Windows `.ollama/id_ed25519` source; parse the unencrypted OpenSSH Ed25519 container using the reference's validation rules. | Sign timestamped request URIs for `/api/usage` and `/api/me`; never transmit the private key. Preserve opt-in detection, fractional quotas, no invented reset times, and real zero additional charges. |
| **OpenCode** | Honor `OPENCODE_DATA_DIR`, `XDG_DATA_HOME`, and verify native Windows defaults. Read `auth.json` and every `opencode*.db` channel database read-only. | Go session/weekly/monthly API quotas plus Go/Zen recorded local spend. Attribute eligible `openai` OAuth usage to Codex, exclude OpenAI API-key traffic from Codex totals, and deduplicate across channel stores. |
| **OpenRouter** | Saved DPAPI key, compatible explicitly discovered config files, then `OPENROUTER_API_KEY`; saved-key override/remove UI. | Required `/credits`, optional `/key`; keep usable credit data when optional lookup fails. Preserve real zero spend and optional key cap. |
| **Z.ai** | Saved DPAPI key, compatible config candidates, `ZAI_API_KEY`, then `GLM_API_KEY`. | Port subscription and quota endpoints, `CREDIT_LIMIT`/legacy `TOKENS_LIMIT`, duration-based windows and millisecond resets. Distinguish invalid key, no subscription, malformed response, and absent values. |

Pi is a shared log source, **not a twelfth dashboard provider**. Inspect all referenced auth stores, usage clients, mappers and scanners before considering a provider complete.

### Early Authentication Gate

Before building the full dashboard, exercise the highest-risk Windows boundaries with small diagnostic tests: Claude Desktop decoding, Cursor SQLite/auth, Antigravity process discovery, Codex secure storage, and Ollama signing. Use synthetic DPAPI round-trip fixtures plus sanitized format samples; never commit live secrets.

If a source cannot be established, implement a typed `UnsupportedCredentialSource` state and the supported alternative (for example Claude Code). Continue independent work, record the exact missing evidence, and leave that matrix entry blocked. Do not label the whole port fully verified while these gaps remain.

## 7. Core Engine and Data Correctness

### Models and Refresh

- Preserve typed progress, scalar values, badge, chart and notice representations. Carry stable IDs, account identity, units, window length, reset/expiry times, warning/error state, source scope, estimated status, and refreshed timestamps.
- Model unavailable separately from measured zero, unlimited, stale, not-started, and failed. Never infer a reset time from the refresh time or a zero cost from missing logs.
- Use `DateTimeOffset` for timestamps, 64-bit token counts, and invariant numeric JSON. Retain sufficient precision through aggregation; round only for display. Golden tests decide any intentional numeric tolerance.
- Refresh enabled providers concurrently with a bounded configurable implementation limit. Preserve the five-minute batch cadence, force refresh, immediate fetch on enablement, per-provider progress, and the reference's 120-second provider deadline. Cancellation must stop underlying work and reject late results.
- GUI startup displays cache then refreshes even if it came from a previous process. CLI normal reads reuse entries younger than five minutes. These are intentionally different freshness policies over the same persisted data. [Reference refresh semantics](https://github.com/robinebers/openusage/blob/v0.7.11/docs/refreshing.md).
- Retain last-good values after errors and show their age. Validate account identity before showing cached Claude/Codex data. Account changes cancel old work, discard incompatible cache, and reset relevant cooldowns.
- Use transactionally committed, versioned app-owned SQLite storage for snapshots/account metadata/scan indexes, or an equivalently tested atomic design. Keep peer-combined history out of local snapshot persistence.
- Coordinate GUI/CLI cache writes and auth refresh with current-user-scoped locks. Re-read freshness after acquiring a lock. Store only successful replacements; propagate errors separately.
- Treat 401/403 according to the provider's policy, not a generic infinite retry. Respect 429 cooldowns/`Retry-After`, bound transport retries, and preserve partial data when optional endpoints fail.

### Local History and Pricing

- Translate incremental scanning and deduplication rules, including source identity, parser version, canonical file identity, size, modification time, truncated/rotated files, archived files, and complete-record boundaries.
- Stream JSONL with bounded buffers and record limits. Preserve an unfinished tail until it completes; skip oversized records with an explicit incomplete-history warning.
- Follow explicitly supported symlinks/junctions without recursion loops or counting the same file twice. Handle Windows case sensitivity, Unicode, long paths, spaces, file sharing, and deletion during scanning.
- Use live SQLite read-only transactions and bounded busy retries. Do not copy only the main `.db` file while ignoring active WAL data; use a consistent SQLite backup only when available and appropriate.
- Use the reference's protobuf field interpretations for Antigravity; impose limits and tolerate unknown fields. Do not count prompt-context records as completed generations.
- Reuse parsed events across refreshes and process launches, then reprice from those events. Preserve the reference's history-window eviction and stale scan-identity cleanup semantics.
- Keep local-calendar day boundaries, DST, Today/Yesterday/30-day windows, per-model totals, speed metadata and replay/subagent deduplication consistent with upstream tests.
- Include all three bundled pricing JSON resources and their provenance. Preserve catalog precedence, aliases, cache reads/writes, one-hour cache writes, request-level long-context rules and model-specific priority multipliers. Do not hardcode simplified dollar-per-token rates or apply a priority multiplier twice.
- Port Codex pricing once and call it from native Codex, pi and OpenCode ingestion. Price only eligible OAuth requests; recorded cost and known rates retain reference precedence.
- Unknown prices remain visible and excluded where the reference excludes them. Default Codex fallback model to None; changing it recalculates eligible local history without modifying provider settings or repricing another device's history.
- Public pricing downloads use schema validation, timeouts, cached last-good catalogs and bundled offline fallbacks. Preserve the code's actual per-catalog refresh intervals rather than relying on inconsistent prose documentation. [Pricing reference](https://github.com/robinebers/openusage/blob/v0.7.11/docs/pricing.md).

## 8. Desktop and Windows Integration

1. Configure an explicit-shutdown WPF application. Closing the dashboard hides it; Exit cancels background work, flushes pending cache writes, stops the API, unregisters shortcuts, disposes icons and exits.
2. Use a named current-user/session instance mutex and authenticated/current-user IPC for second-launch activation. A second GUI launch focuses the existing dashboard; CLI execution stays independent.
3. Implement tray left-click toggle and right-click Open, Refresh, Privacy Mode, Settings, and Exit. Handle Explorer restart, missing/overflow tray placement, mixed-DPI monitors and monitor removal. Use `Shell_NotifyIconGetRect` when possible, with a safe work-area fallback.
4. Start with a roughly 400-DIP dashboard, clamp height to work area, and use a scrollable body with a stable footer. Preserve grouped meters, expanders, trends, details, stale/error cues and settings navigation. Verify the final layout against the reference screenshot, while using WinLLMUsage branding.
5. Implement keyboard focus on first open, Ctrl+R refresh, Ctrl+, settings, and Escape backing out of settings/details before closing the dashboard. Outside-click dismissal must not break menus, key recording, confirmation dialogs or drag operations.
6. Implement the optional floating pin strip with the same selected metrics and format settings. Persist position, recover off-screen positions, and hide its values in Privacy Mode. Do not create one tray icon for every metric.
7. Use `RegisterHotKey`/`UnregisterHotKey` for the configurable global shortcut. Detect conflicts and leave the previous working shortcut intact if replacement fails. No global keyboard hook is needed. [Windows hotkey API](https://learn.microsoft.com/en-in/windows/win32/api/winuser/nf-winuser-registerhotkey).
8. Implement launch at login with a per-user startup mechanism pointing to a stable installed launcher. Reconcile the setting with actual registration and remove only this product's startup entry on disable/uninstall.
9. Preserve notification thresholds, first-refresh baseline suppression, deduplication, reset-period rearming and grouped notifications. Use a tested Windows notification adapter, with installer identity/activation wired up where required. Notification clicks open the dashboard; all triggers start off.
10. Provide keyboard-accessible actions in addition to hover details, UI Automation names, logical tab order, high-contrast support and 100/125/150/200% DPI testing. No network, file scan or credential decoding runs synchronously on the UI thread.
11. Render share cards through WPF to PNG/clipboard using current normalized values and original fork branding. Privacy Mode must not silently export hidden account/usage information; require the user's explicit export action and make the preview clear.
12. Reset settings with confirmation, preserving provider credentials/API keys and cache as the reference does. Maintain versioned settings migrations, exact default layout and stale pin-ID remapping.

### Codex Reset-Credit Action

Port `CodexResetClaimService.swift` and its regression tests, including a fresh exact-credit match, account binding, idempotency key reuse and post-claim refresh. The UI must display the selected credit and an inline confirmation before claiming it. Disable repeated clicks and resolve ambiguous responses before retrying. No background job, test using a live account, public local-API route, or CLI read may consume a reset credit. [Reference reset behavior](https://github.com/robinebers/openusage/blob/v0.7.11/docs/providers/codex.md).

## 9. CLI, Local API, Proxy and Sync

### CLI and API Compatibility

Implement `winllmusage [provider-or-family] [--force]` plus help/version. Offer the `openusage` alias only as an explicit install option. The command must work with the GUI closed, print only contract JSON to stdout, send diagnostics to stderr, flush pending scan-cache work, and exit without a resident process.

Preserve exit codes: `0` success, `2` invalid arguments/unknown provider, `4` refresh/local-read failure. Use upstream CLI tests to settle partial-success behavior rather than inventing a new policy.

Implement the read-only server at `127.0.0.1:6736`:

| Route | Contract |
| --- | --- |
| `GET /v1/limits` | Enabled providers, schema `openusage.limits.v1`, stable scalar resources. |
| `GET /v1/limits/:id` | Exact provider or family match, including disabled providers; unknown ID 404. |
| `GET /v1/usage` | Legacy array of UI-oriented snapshots in configured dashboard order. |
| `GET /v1/usage/:id` | Array of every matching snapshot, including zero/one/multiple results; unknown ID 404. |
| `OPTIONS` | 204, compatible CORS response. |
| Other methods/routes | 405/404 with the reference error shape; overload returns 503. |

Keep `generatedAt`, `fetchedAt`, `expiresAt`, `stale`, `errors`, conditional fields and units compatible. Freshness is five minutes. Never invent absent resources as zero or export charts/history into `/v1/limits`. Freeze exact serialized fixtures, including null-versus-omitted behavior. Preserve the reference cap of 16 concurrent connections or a demonstrably equivalent bounded implementation.

Bind explicitly to IPv4 loopback, never `0.0.0.0`. A port collision must leave the GUI/CLI operational and show API-unavailable diagnostics; never terminate the other listener or silently select a different port. The reference's permissive GET/OPTIONS CORS remains the compatibility default: explain in Privacy settings that browser pages may read usage, and provide an API-off toggle. The API exposes no credentials and no mutation/reset endpoint. [CLI contract](https://github.com/robinebers/openusage/blob/v0.7.11/docs/cli.md), [HTTP contract](https://github.com/robinebers/openusage/blob/v0.7.11/docs/local-http-api.md).

### Network and Proxy

Support the reference's proxy object (`enabled`, `url`) in app configuration, plus read-only discovery of `%USERPROFILE%\.openusage\config.json` when no app-specific setting exists. Support and test `http`, `https` and `socks5`, authentication and default ports. Loopback always bypasses the proxy. Do not silently fall back to a direct connection when an enabled valid proxy fails. Redact credentials in proxy URLs. Document any unsupported HTTPS-proxy transport behavior as a parity failure to fix, not a successful implementation.

Keep TLS validation enabled for external endpoints. If the Antigravity local-server protocol requires exceptional certificate handling, scope it to the verified process/loopback endpoint and the reference protocol; never install a global certificate-validation bypass. [Reference proxy behavior](https://github.com/robinebers/openusage/blob/v0.7.11/docs/proxy.md).

### Folder-Based History Sync

Implement a versioned one-document-per-device format using the reference normalized-history model. Keep a stable random device ID in protected local storage, an explicit folder picker, atomic writes, debounced watchers with periodic reconciliation, and visible device/error state. Sync stays off by default.

Only write machine-local normalized history for descriptors marked eligible. Exclude credentials, raw prompts/logs, quota snapshots and account-wide history. Merge account/organization identities according to upstream rules; accept the legacy optional Codex account metadata supported in v0.7.11. Never write peer-combined totals back as local history. Handle malformed/partially delivered files, duplicate/conflict files, offline folders and expired history. Disabling sync removes only this device's document when accessible and immediately returns local-only views; explicitly report a failed removal.

Do not present this as upstream iCloud interoperability. Document the chosen format, retention and whether manual exported upstream documents can be imported; do not claim an import path that has not been tested. [Reference sync semantics](https://github.com/robinebers/openusage/blob/v0.7.11/docs/icloud-sync.md).

## 10. One-Pass Execution Sequence

Each checkpoint must build and test before proceeding, but all checkpoints belong to the same implementation delivery.

| Step | Concrete work | Exit evidence |
| --- | --- | --- |
| **A. Freeze the baseline** | Clone the tag; verify its SHA; record license, source inventory, all feature/default/resource IDs, endpoint contracts and Windows adaptations. Read applicable repository instructions. | `UPSTREAM_BASELINE.md` and complete initial parity matrix. |
| **B. Establish Windows boundaries** | Inspect/fixture the high-risk auth and process sources from section 6; establish native Windows runner access and dependency feasibility. | Auth-source report with proven paths/formats and explicit gaps. |
| **C. Scaffold and package early** | Create projects, DI, settings, redacted logs, instance handling, minimal tray/dashboard, empty CLI/API and an installer skeleton. | Clean build, app opens/exits, clean Windows install/uninstall works. This is a checkpoint, not completion. |
| **D. Port pure behavior** | Models, descriptors, serializers, account identity, defaults/migrations, pacing, aggregators and pricing. Extract synthetic golden fixtures from reference tests. | Core/contract regression tests pass without Windows or provider logins. |
| **E. Build shared I/O** | Credential adapters, path resolver, HTTP/proxy, transactional snapshots, cross-process coordination, JSONL/SQLite scanning and pricing refresh. | Race, cancellation, corruption, WAL, proxy and account-switch tests pass. |
| **F. Implement all providers** | Claude/Codex/Cursor first, then OpenRouter/Z.ai, Copilot/Devin/Grok, OpenCode/Ollama, Antigravity. Complete each pipeline through normalized output, not just its mapper. | Every provider has fixture-tested auth selection, success, failure, missing-data and optional-endpoint behavior. |
| **G. Complete the product** | Dashboard/details, customization, pins/floating strip, settings, notifications, reset-credit action, share cards, folder sync, CLI installation, and privacy behavior. | Feature matrix has implementation paths and test evidence; no connected placeholder controls. |
| **H. Verify on Windows** | Run unit/integration tests, installed-app interaction tests, mixed-DPI checks, offline/relaunch/concurrency cases and available live-provider smoke tests. | Windows verification report, screenshots and test output; live-unverified cases explicitly identified. |
| **I. Deliver artifacts** | Self-contained package, portable ZIP, checksums, dependency/license notices, release notes, build/run/setup/troubleshooting instructions and CI. | Artifacts install and run on a clean target without developer runtimes; final parity report matches evidence. |

Do not invent a fixed completion time for this port: the inspected project has extensive provider-specific behavior and tests. The implementation order reduces rework while retaining the complete requested scope.

## 11. Verification Plan

### Required Automated Coverage

- **Contracts:** all API routes/statuses/CORS; exact family matching; disabled providers; CLI flags, exit codes and stdout purity; wire fixtures for all 11 providers, including Ollama's two exported limits.
- **Authentication:** absent/unreadable/malformed source; wrong scope; expiry; refresh failure; source/account changes mid-refresh; concurrent GUI/CLI rotation; Desktop read-only enforcement; secret redaction.
- **Provider behavior:** success, missing optional data, invalid required fields, 401/403/429, timeout, network failure and supported legacy payloads. Include Cursor request-versus-dollar units, Copilot zero entitlement, Antigravity shared pools, and Ollama absent resets.
- **v0.7.11 regressions:** account-prefixed Claude Desktop caches and deletion precedence; OpenCode OAuth-to-Codex attribution; shared Codex request pricing; Antigravity conversation-store union/model grouping; untouched-meter pacing; Grok replay/subagent deduplication; dead-pin migration; Business Premium display; legacy sync metadata. Treat the tagged pricing resources as the baseline for its model additions.
- **Spend correctness:** parent/subagent/fork replays, archived sessions, advisor usage, pi recorded cost, OpenCode channels, unknown models, fast/priority tiers, long-context boundaries, and incomplete versus actual-zero periods.
- **Files and storage:** giant/partial JSONL lines, truncation, rotation, symlink/junction cycles, Unicode/spaced/long paths, active WAL, locked DB, process crash mid-write, corrupted cache, version migration and cache eviction.
- **Time:** injected clock, expiration boundaries, local midnight, DST, sleep/resume, stale indicators, notification baseline and reset-cycle rearming.
- **Sync:** same-device/conflict-file deduplication, account isolation, account-wide spend exclusion, malformed peers, no recursive re-export, disable/delete failures and local-versus-combined views.
- **Reset claims:** mocked exact credit selection, cancelled confirmation, changed account, used/expired credit, duplicate click, idempotency and ambiguous transport response. Never consume real credits in automated verification.

Use the tagged Swift tests as an oracle where feasible: extract sanitized input/expected-output pairs, run the reference tests on a compatible Mac toolchain if available, and compare C# results. If the reference cannot run, document that expected values were derived from source/tests rather than claiming a differential execution occurred.

### Required Windows Acceptance Scenarios

1. Install and launch on a clean Windows 11 x64 machine without .NET or developer tools. Launch again and verify one GUI instance.
2. With no providers installed, show an actionable empty state. First-run detection does not issue network requests or enable Ollama.
3. With synthetic and available real providers, show quota, balances, spend, reset/detail views and accurate errors. A provider without a live account remains live-unverified, even if fixture tests pass.
4. Shut down the GUI and run the CLI successfully. Start GUI and CLI refreshes together; verify coherent cache and no duplicate token rotation.
5. Go offline, restart, and see cached data with accurate age and failure status. Reconnect/resume and recover without duplicate refresh loops.
6. Change the active Claude/Codex account; old-account values and in-flight results must not appear under the new identity.
7. Verify custom directories, open editor databases, multiple monitors, tray overflow, Explorer restart, 100–200% scaling, light/dark/high contrast and keyboard-only operation.
8. Verify notifications, hotkey conflict handling, launch-at-login on/off, privacy display, copy/export, settings reset and floating-strip position recovery.
9. Occupy port 6736; the app continues and reports API unavailability. Confirm remote network clients cannot reach the API.
10. Test two-device folder sync, provider disablement, malformed peer data and turning sync off without deleting another device's file.
11. Install an upgrade over populated settings/cache; preserve layout, secrets and the CLI launcher. Test interrupted/failed update handling and explicit repair/reinstall recovery.
12. Uninstall; remove this application's running processes, shortcuts/startup entry and installer files, preserving unrelated provider data. Document retained user data and provide a deliberate separate removal option.

### Performance Budgets to Measure

These are proposed acceptance targets, not measured results: warm dashboard open within 300 ms; cached dashboard visible within 2 seconds of launch; settled idle CPU averaging below 1% on the named test machine; no sustained increase in handles/memory during an eight-hour soak. Record machine specs and workload. Scan a synthetic 1 GB JSONL corpus without loading entire files or blocking the UI, and demonstrate that an unchanged second scan reuses persisted results. Set and report a concrete memory budget after the early shell/scanner baseline rather than concealing an over-budget result.

## 12. Build, CI and Distribution

Use a Windows CI runner with the .NET 10 SDK for the full solution. Pure core/provider tests can also run on macOS/Linux. WPF compilation from another OS, if used, does not establish runtime correctness. This planning workspace is macOS; native credential access, tray behavior, installer validation and Windows UI verification require Windows execution.

The implementation must make these commands work from a clean Windows checkout:

```powershell
dotnet tool restore
dotnet restore WinLLMUsage.sln --locked-mode
dotnet build WinLLMUsage.sln -c Release --no-restore
dotnet test WinLLMUsage.sln -c Release --no-build
pwsh ./scripts/package.ps1 -Runtime win-x64
pwsh ./scripts/verify-contracts.ps1
pwsh ./scripts/verify-install.ps1
```

`package.ps1` must publish the WPF app and real console CLI self-contained for `win-x64`, assemble them without overwriting incompatible host/dependency files, include SQLite and cryptography runtime assets, and invoke the pinned Velopack tool. Keep the CLI output in a distinct subdirectory if needed. Test both installed and portable layouts. A stable CLI launcher must propagate arguments, stdout/stderr and exit codes across updates; do not point PATH at a version-specific deployment directory.

CI should validate formatting/build, run tests, generate artifacts/checksums, and retain logs and fixture-mode screenshots. GUI interaction testing requires a usable interactive desktop; do not mark screenshots or UI tests passed merely because a headless build job succeeded. Keep ordinary CI independent of provider accounts, publisher certificates and production feeds.

Deliver:

- `WinLLMUsage-<version>-win-x64-Setup.exe` and a documented portable ZIP.
- Checksums, test results, parity/auth reports, release notes and third-party notices.
- Separate stable/beta update metadata only when actual hosting is configured.
- An unsigned development build when signing is unavailable, clearly labelled as such; production distribution must have the intended publisher signing and verification configuration.

For updates, verify the selected Velopack version's package-integrity and publisher-validation facilities before relying on them. Checksums from the same untrusted feed are not independent authenticity verification. Test tampered packages, wrong publisher, channel selection and failed updates; do not ship an unverified custom updater. Sign on Windows using the owner's configured certificate/service and keep secrets outside the repository. [Velopack signing](https://docs.velopack.io/packaging/signing), [Velopack cross-compilation constraints](https://docs.velopack.io/packaging/cross-compiling).

Do not invent a public product version or tag during implementation; use a clearly local development version until release numbering is provided. Do not publish a GitHub release or reuse upstream distribution infrastructure as part of merely implementing this plan.

## 13. Definition of Done and Honest Blockers

The port is complete only when:

- [ ] It builds reproducibly and produces installer/portable artifacts.
- [ ] All 11 providers have real implementations; no hardcoded sample data is used in normal operation.
- [ ] Reference behavior is represented in the parity matrix, with only the explicit section 3 adaptations accepted as planned differences.
- [ ] The GUI, CLI and API consume the same engine and pass compatibility tests.
- [ ] Local history and pricing regressions pass, including the specific v0.7.11 fixes.
- [ ] Credential reads/writes, account isolation and secret redaction are verified for supported Windows sources.
- [ ] Dashboard/customization/settings/pins/notifications/reset claims/sync have working behavior and appropriate tests.
- [ ] Clean-machine installation, upgrade, launch-at-login, CLI and uninstall are verified on Windows.
- [ ] Final documentation reports which providers were fixture-tested and which were exercised live, with tested companion versions.
- [ ] No secret, upstream telemetry destination, fabricated rate, invented Windows path or unsupported parity claim is shipped.
- [ ] Remaining external prerequisites, such as a live provider account or publisher certificate, are listed precisely with their effect on verification/distribution.

If Windows execution is unavailable, deliver the code/artifacts that can actually be produced and a precise `WINDOWS_VERIFICATION.md` backlog; describe the outcome as **implemented but not Windows verified**. If a required auth adapter remains unresolved, describe the provider/source gap and do not call the result a fully complete port. “One shot” is an execution instruction, not permission to suppress blockers.

## 14. Copy-Paste Implementation Brief

```text
Implement the entire WINDOWS_IMPLEMENTATION_PLAN.md in this repository.

Use OpenUsage v0.7.11 at commit
753e2fe4bc82a011567d16d8deb88176b58ed24e as the behavioral baseline.
Build the independent WinLLMUsage Windows application in C#/.NET 10 WPF.

Carry the work through in one continuous implementation run: baseline inventory,
Windows boundary verification, shared core, all 11 provider pipelines, local usage
and pricing, full desktop experience, CLI/API, folder sync, tests and packaging.
Do not stop at a plan, scaffold, mock UI, or a subset of providers. Use the sequence
and acceptance gates in this document, fixing failures before declaring completion.

Preserve exact upstream provider/resource IDs, defaults, account handling, pricing
rules and API/CLI contracts. Apply the explicit Windows adaptations in section 3.
Resolve Windows auth formats using evidence; do not guess that macOS Keychain or
Electron encryption formats transfer unchanged. Never expose credentials or mutate
read-only companion sources. Test reset-credit claims only against mocks.

Make routine implementation decisions independently. Keep PARITY_MATRIX.md and
WINDOWS_AUTH_SOURCES.md current. Continue useful work around external blockers,
but report unverified providers/platform behavior honestly. Do not manufacture
test results, live logins, signing credentials, update feeds or release versions.

Finish with buildable source, installable artifacts when the environment permits,
test evidence, Windows verification screenshots/results, setup instructions, and
an exact list of any incomplete acceptance criteria. Do not publish a release.
```
