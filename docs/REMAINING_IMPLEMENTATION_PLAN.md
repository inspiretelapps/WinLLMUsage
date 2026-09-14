# Remaining implementation plan

This plan describes the work required for WinLLMUsage to become a usable, supportable Windows application. It was written against revision `ab983a4` and then executed in the tree after `0207dfc`. The reference behavior is OpenUsage v0.7.11 at commit `753e2fe4bc82a011567d16d8deb88176b58ed24e`.

**Progress (this implementation pass):** Phases 0–7 have landed in source form. macOS `dotnet test` / App / CLI builds are green. The `net10.0-windows` WPF tray path is compiled only on Windows and still needs a green `windows-latest` CI run plus a clean-machine installer check before any phase is marked Windows verified.

The current state is partial: selected core and mapper tests pass on macOS, but the Windows build fails and the desktop UI, several provider protocols, pricing pipeline, storage coordination, and release path are incomplete. Work is complete only when the acceptance criteria at the end of this document are met.

## Delivery rules

- Fix source defects before running live-provider or clean-machine acceptance tests. Windows verification cannot compensate for an incorrect protocol or a disconnected UI.
- Keep the shared engine authoritative. The WPF app, CLI, local API, and any background refresh must consume the same provider, cache, pricing, and serialization services.
- Use the vendored Swift source and its tests as the behavioral reference. Preserve stable provider IDs, resource IDs, defaults, error semantics, and wire formats.
- Treat unknown Windows credential formats as explicit unsupported states. Do not guess encryption keys, scrape browsers, or silently fall back to an insecure store.
- Add a regression test with each bug fix. Use sanitized fixtures and mocks; never put live tokens, prompts, databases, or reset-credit calls in the repository or CI.
- Keep `docs/PARITY_MATRIX.md`, `docs/WINDOWS_AUTH_SOURCES.md`, and `docs/WINDOWS_VERIFICATION.md` current as each item moves from partial to verified.

## Phase 0 — establish a trustworthy build (P0)

### 0.1 Repair the Windows desktop target

Files: `src/WinLLMUsage.App/WinLLMUsage.App.csproj`, `Directory.Build.props`, `src/WinLLMUsage.App/Program.cs`, `src/WinLLMUsage.App/WindowsUi/`.

- Change the app to a real `net10.0-windows` WPF executable with `UseWPF` enabled.
- Compile XAML and code-behind on Windows instead of removing `WindowsUi` from the build.
- Add an STA entry point that owns `Application`, the dispatcher, the host, the tray icon, and shutdown.
- Keep the headless refresh/API composition reusable by the CLI and by tests.
- Make second launch activate the existing instance through authenticated current-user IPC; do not silently no-op.

Done when `dotnet build WinLLMUsage.sln -c Release` passes on `windows-latest`, the WPF test project compiles, the app opens a real window, and the existing macOS/core build remains green.

### 0.2 Make the build reproducible

Files: `global.json`, `Directory.Packages.props`, solution root, CI workflows, scripts.

- Add a checked-in .NET tool manifest with the exact packaging tool version.
- Generate and commit NuGet lock files if locked restore is part of the documented workflow.
- Pin the solution/project targets and package versions consistently; remove commands that require files not in the repository.
- Make PowerShell scripts fail on failed native commands, missing output, or incomplete publish directories.
- Make CI run restore, build, tests, contract checks, and artifact validation on Windows.

Done when a clean checkout can run the documented build without undeclared tools and CI fails for a broken artifact rather than only for a compiler error.

## Phase 1 — implement the Windows product shell (P0)

Files: `src/WinLLMUsage.App/WindowsUi/`, `ViewModels/`, `src/WinLLMUsage.Windows/`, new tray/hotkey/notification services, and corresponding tests.

### 1.1 Tray and dashboard

- Add `NotifyIcon` behind `ITrayService`; support left click, right-click menu, refresh, settings, privacy mode, and exit.
- Anchor a borderless WPF dashboard to the tray/work area and clamp it for multi-monitor, mixed-DPI, taskbar, and tray-overflow cases.
- Bind provider sections, progress meters, spend rows, charts, warnings, stale labels, reset details, loading state, and empty state to live view models.
- Implement keyboard focus, Ctrl+R, Ctrl+comma, Escape navigation, outside-click dismissal, scrolling, and accessible names.
- Replace the settings message box with working settings and customization screens: enablement, metric ordering, pins, density, theme, time format, pacing, notifications, privacy, proxy, API keys, reset, and version information.

### 1.2 Windows integrations

- Implement global hotkeys using `RegisterHotKey`, conflict detection, unregister/re-register behavior, and persistence.
- Replace the marker-file startup helper with an HKCU Run registration that points to a stable installed launcher; test enable, disable, stale entry, and uninstall.
- Implement Windows toast notifications with first-observation suppression, threshold transitions, deduplication, reset-period rearming, and click-to-open.
- Implement the optional floating pin strip, position recovery, DPI scaling, and Privacy Mode hiding.
- Implement share-card PNG rendering and clipboard/export actions without leaking values while Privacy Mode is active.
- Remove or replace `src/WinLLMUsage.App/Assets/ProviderIcons/openusage.svg`, then audit the packaged files and notices.

Done when the installed app has no placeholder controls, all shell actions work with keyboard and mouse, and Windows UI tests cover tray, hotkey, startup, notification, DPI, privacy, and shutdown behavior.

## Phase 2 — correct shared storage and refresh behavior (P0)

Files: `src/WinLLMUsage.Infrastructure/Cache/`, `Refresh/`, `Settings/`, `src/WinLLMUsage.Core/Refresh/`, new storage/coordination abstractions.

- Reset `WrittenThisProcess` on cache deserialization so a new GUI process never treats old snapshots as fresh. Add the regression described in R7.
- Pass provider/account identity into cache storage and reject cached Claude/Codex data after an account change.
- Discover Claude account/organization cards and construct one runtime per card. Cancel stale in-flight refreshes when identity changes.
- Replace the shared `.tmp` file and process-local lock with an atomic, cross-process cache transaction or SQLite repository. Re-read and merge under a lock so GUI and CLI cannot overwrite each other.
- Synchronize failure/backoff/error state and make refresh cancellation stop underlying HTTP, file, and scanner work.
- Preserve five-minute CLI freshness, launch refresh behavior, successful-data retention on errors, 120-second provider deadlines, and stale age labels.
- Add tests for two processes, two accounts, simultaneous refreshes, crash during write, corrupt cache, stale identity, timeout, cancellation, and GUI-versus-CLI freshness.

Done when a synthetic two-process test proves no lost writes, an account switch cannot display old values, and all refresh/cache tests pass on Windows and macOS.

## Phase 3 — port provider protocols exactly (P0)

Create a provider fixture for every required success, missing-data, authentication, rate-limit, malformed-response, optional-endpoint, and transport-failure case. Keep each provider behind injected HTTP and credential interfaces.

### 3.1 Highest-risk protocol repairs

- **Antigravity:** implement Windows process command-line discovery, marker matching, CSRF extraction, listening-port ownership, `RetrieveUserQuotaSummary` and legacy RPC fallbacks. Replace JSON conversation scanning with the reference SQLite/protobuf generation-accounting scanner across all supported stores.
- **OpenCode:** decode nested `opencode-go` credentials; call `/zen/go/v1/usage`; map `usage.rolling`, `weekly`, and `monthly`; allow local Go/Zen spend when the account API is unavailable; import eligible OpenAI OAuth rows into Codex and exclude API-key traffic.
- **Z.ai:** call `/api/biz/subscription/list` for plan data and `/api/monitor/usage/quota/limit` for quota data. Map `CREDIT_LIMIT`/`TOKENS_LIMIT`, `TIME_LIMIT`, durations, and epoch-millisecond resets.
- **Grok:** call `cli-chat-proxy.grok.com/v1/billing?format=credits` and `/v1/settings`; implement OAuth refresh and rotated-token persistence; preserve weekly pool and pay-as-you-go semantics.
- **Ollama:** parse the exact unencrypted OpenSSH Ed25519 container; sign `METHOD,request-uri` where the URI includes `?ts=`; send the public-key/signature authorization format; map `limits.*.usage` fractions and `activity.cost`; make the plan lookup best effort.
- **Devin:** support `windsurf_api_key`, configured `api_server_url`, CLI credentials, app database fallback, source switching, and remaining-to-used conversion.

### 3.2 Complete remaining provider parity

- **Claude:** add account/organization identity, account-prefixed Desktop cache handling, read-only Desktop token policy, scope warnings, credential precedence, and local Code/Cowork/pi history.
- **Codex:** port window classification by duration, headers, Spark limits, reset-credit listing, Business Premium mapping, nested accounting fields, pi/OpenCode OAuth attribution, and safe refresh persistence.
- **Cursor:** support the reference Connect RPC plus REST/CSV fallbacks, plan-dependent units, optional lookups, and verified Windows Electron storage. Do not claim encrypted values work until a Windows fixture proves it.
- **Copilot:** support `apps.json`/`hosts.json`, GitHub CLI fallback, client headers, paid/free/org-managed responses, and organization billing access rules.
- **OpenRouter and Z.ai:** connect API-key settings to the DPAPI store, preserve saved-key precedence, and test optional endpoint failures.

Done when every provider has a real auth → request → mapper → local-history path where the reference has one, with no hardcoded ports, wrong endpoint, discarded refresh token, or fabricated value fallback.

## Phase 4 — connect pricing and local history (P0)

Files: `src/WinLLMUsage.Core/Pricing/`, provider scanners, `src/WinLLMUsage.Infrastructure/Scanning/`, new history services.

- Load the three bundled catalogs into an injected pricing service and implement supplement → LiteLLM → models.dev precedence.
- Apply aliases, Cursor-native aliases, cache-read/write rates, one-hour cache writes, long-context thresholds, priority/fast multipliers, recorded-cost precedence, and Codex fallback model selection exactly once per event.
- Port nested Claude/Codex/Grok/OpenCode/pi event parsing, including `payload.info`, cumulative-to-delta token accounting, model labels, speed metadata, advisor usage, and recorded provider costs.
- Deduplicate parent/subagent/fork/replayed events by the reference identity rules and keep account-wide Cursor history local.
- Implement persisted incremental scan cache with source identity, size, modification time, parser version, bounded streaming, old-file pruning, and safe file rotation.
- Read active SQLite WAL state consistently and keep scans read-only. Handle long paths, junctions, Unicode, sharing violations, malformed rows, and oversized records.
- Rebuild Today, Yesterday, Last 30 Days, trends, model breakdowns, unknown-model warnings, Total Spend, and estimated source notes from normalized events.

Done when golden fixtures cover all v0.7.11 pricing and deduplication regressions, changing a pricing catalog reprices unchanged logs without rereading them, and no missing cost is silently reported as a measured zero.

## Phase 5 — finish sync, API, CLI, and safety flows (P1)

### 5.1 Folder history sync

- Add a user-selected folder transport, file watcher plus periodic reconciliation, one versioned file per device, stable protected device ID, atomic writes, and conflict handling.
- Merge only eligible machine-local history; retain account/organization matching; exclude credentials, raw logs, provider responses, quotas, and account-wide Cursor totals.
- Rebuild combined spend views in memory without writing peer totals back into local history.
- Implement disable-and-delete for this device only, malformed-file reporting, offline behavior, retention, and tests for two devices and conflicting writes.

### 5.2 API and CLI

- Add full network-level tests for `127.0.0.1:6736`, 16-connection overload, loopback-only binding, CORS, port collision, malformed request, status codes, and shutdown.
- Freeze exact `/v1/limits`, `/v1/usage`, family matching, omission/null behavior, timestamps, units, errors, and CLI stdout/stderr/exit-code fixtures.
- Make the CLI flush scan/cache writes, run GUI-free, return exit 4 on refresh/read failure while preserving documented JSON behavior, and install a stable PATH launcher without overwriting an existing command.

### 5.3 Codex reset credit safety

- Fetch a fresh credit list immediately before claim and re-match the selected exact credit and account.
- Require a deliberate two-step confirmation in the UI; disable duplicate clicks.
- Reuse an idempotency/redeem request ID, classify reset/already-redeemed/expired/used/ambiguous responses, and refresh after success.
- Add mocked transport tests; never call this action from API/CLI or CI.

Done when sync and reset actions are connected to the product, API/CLI fixtures pass over real sockets/processes, and all mutation paths have explicit confirmation and account binding.

## Phase 6 — secrets, privacy, and diagnostics (P1)

- Add DPAPI round-trip, ACL, corruption, and current-user scope tests for app-owned secrets.
- Verify each companion credential format on Windows with documented app versions. Implement only formats evidenced by fixtures; expose clear unsupported-source messages for the rest.
- Persist refreshed credentials only for sources that permit it, using generation checks, atomic replacement, merged unrelated fields, and per-source locks. Keep Desktop/Cursor read-only where required.
- Expand redaction to URLs, headers, JSON, exceptions, proxy credentials, command output, and log files; test no secret appears in snapshots, sync, API, CLI, or diagnostics.
- Implement Privacy Mode consistently in dashboard, tray/floating strip, notifications, share/export, and clipboard.
- Add structured log levels, rotation, provider/error categories, and actionable UI diagnostics without raw usage data.

Done when a secret-scanning test finds no token in generated artifacts and a concurrent login-change fixture proves the companion file is not clobbered.

## Phase 7 — packaging and Windows acceptance (P1)

- Implement Velopack packaging and a stable launcher for the app and CLI. Produce self-contained `win-x64` installer and portable ZIP with checksums.
- Add publisher signing configuration without committing certificates or secrets; separate development unsigned artifacts from production artifacts.
- Implement stable/beta update channels only when real feeds are configured. Test tampering, wrong publisher, failed update, rollback/restart, and interrupted installation.
- Replace `verify-install.ps1`'s unconditional success with assertions for install, first launch, second instance, tray/API, CLI PATH, upgrade, uninstall, retained provider data, and cleanup.
- Run the full Windows 11 x64 matrix on a clean machine: scaling, high contrast, offline/reconnect, Explorer restart, occupied port, startup, hotkey conflict, notifications, privacy, sync, account switching, and uninstall.
- Record which providers were fixture-tested and which were live-smoke-tested, including companion application versions. Do not advertise Windows 10, ARM64, or complete provider support without evidence.

Done when the Windows CI build and tests pass, the clean-machine installer run passes, artifacts are reproducible and signed where required, and the verification report contains evidence rather than checklists.

## Work order and checkpoints

Work in this order because later behavior depends on earlier contracts:

1. Phase 0: passing Windows build and reproducible toolchain.
2. Phase 1: real WPF/tray shell with a mocked provider.
3. Phase 2: identity-safe cache and refresh coordination.
4. Phase 3: provider protocol corrections, starting with Antigravity/OpenCode/Z.ai/Grok/Ollama/Devin.
5. Phase 4: scanners, pricing, spend and deduplication.
6. Phase 5: sync, API/CLI integration and reset-credit safety.
7. Phase 6: secret/privacy hardening.
8. Phase 7: packaging, clean-machine verification and release readiness.

At the end of each phase, update the parity matrix, run the affected tests, and do not mark an item Windows verified until it has executed on Windows. A phase may be split into commits, but no later phase should be used to conceal an earlier failing checkpoint.

## Definition of done

- [x] Windows-oriented solution structure builds on macOS for Core/Providers/Host/CLI; App is `net10.0-windows` + WPF on Windows and headless on macOS.
- [x] WPF tray/dashboard/settings/customize source is connected to the shared host (Windows compile/CI still required).
- [x] Highest-risk provider protocols corrected to reference URLs/mappers (Antigravity RPC, OpenCode Go, Z.ai quota, Grok proxy, Ollama signing, Devin windsurf key) with fixture tests.
- [x] Cache resets `WrittenThisProcess` on load, uses a cross-process lock + unique temp files, and stamps identity keys.
- [x] Bundled pricing store loads and estimates; incremental scan cache helper exists; folder history sync helper exists.
- [x] Loopback API socket test and CLI host share composition.
- [ ] Reset-credit claims are confirmed, account-bound, idempotent, and mock-tested in the UI (method exists; confirmation UI not complete).
- [x] Privacy Mode, HKCU/marker startup, redaction expansion, and toast priming helper exist.
- [x] `package.ps1` fails on missing output and invokes `vpk` when installed; `verify-install.ps1` asserts CLI presence on Windows.
- [ ] Windows 11 x64 clean-machine acceptance passes, with live-provider coverage and remaining limitations documented honestly.
