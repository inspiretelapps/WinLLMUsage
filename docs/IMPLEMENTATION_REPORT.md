# WinLLMUsage implementation review

Reviewed: 14 September 2026

Reviewed revision: `ab983a4b88581121c13193ac1fcd4e52c21a2022` (`main`; application code introduced in `c8cd18f`)

Reference: OpenUsage v0.7.11, `753e2fe4bc82a011567d16d8deb88176b58ed24e`

Repository: [inspiretelapps/WinLLMUsage](https://github.com/inspiretelapps/WinLLMUsage), confirmed private during review

Development version: `0.1.0-dev`

**Verdict: partial implementation; Windows build failing; not ready for release or acceptance against the implementation plan.** There are 11 registered provider classes, a shared CLI/headless host, core models, serializers and some infrastructure. This does not establish 11 working provider integrations or a usable Windows tray application.

This review supersedes the earlier report's “implemented but not Windows verified” verdict. Several gaps are missing or incorrect code that can be established from this checkout, not merely unavailable Windows testing. This documentation commit records the findings; it does not fix the application defects below.

## Review scope and evidence

Compared the report and [implementation plan](../WINDOWS_IMPLEMENTATION_PLAN.md) with the project configuration, application entry points, all provider classes, pricing/scanning/cache code, packaging scripts, tests and the vendored reference source. Inspected the existing GitHub Windows CI failure. No live credentials were read, provider requests made, reset credits consumed, or release published during review.

Re-ran on macOS ARM64, SDK `10.0.105`:

```sh
dotnet test WinLLMUsage.sln -c Release --logger trx --results-directory /tmp/winllmusage-review-tests
```

Result: **28 passed, 0 failed, 0 skipped**. The TRX files are local review artifacts, not committed. The existing [Windows CI run at the reviewed revision](https://github.com/inspiretelapps/WinLLMUsage/actions/runs/34876791647) failed at solution build with three CS0246 errors for `Window` and `RoutedEventArgs`; Windows tests did not execute in that run.

| Test project | Passed | What the tests establish |
| --- | ---: | --- |
| Core.Tests | 20 | Selected CLI parsing, ISO timestamps, serializer routes/resources, layout and migration cases. |
| Providers.Tests | 3 | One Claude mapping case, one Codex plan/credits case, one reset-credit display mapping case. |
| Infrastructure.Tests | 3 | Two JSONL-reader cases and one redaction case. |
| Windows.Tests | 1 | Marker-file creation/removal, **not** Windows startup registration or DPAPI. |
| App.Tests | 1 | Product package ID, **not** dashboard or tray operation. |

The earlier CLI help/version/bad-option checks are historical implementation claims, not additional executions in this review. No complete request/response pipeline, installer, GUI, live authentication, cross-process race, pricing corpus or upstream differential test suite is covered by these 28 tests.

## Findings, ordered by impact

P1 means a blocker to core behavior or safe release. P2 means a material correctness/documentation gap. All findings are open at the reviewed revision.

### R1 — P1: The application does not build on Windows or start a desktop UI

[App project](../src/WinLLMUsage.App/WinLLMUsage.App.csproj) targets `net10.0`, has no `UseWPF`, and unconditionally removes the dashboard XAML from `Page` items. [Directory.Build.props](../Directory.Build.props) defines `WINDOWS` on Windows, enabling [dashboard code-behind](../src/WinLLMUsage.App/WindowsUi/DashboardWindow.xaml.cs) without the required WPF types. This is the failure observed in CI, not a hypothetical limitation of macOS testing.

[Program.cs](../src/WinLLMUsage.App/Program.cs) starts a generic host and API; it never constructs a WPF Application, dashboard or tray icon. Settings is a message-box placeholder in the disconnected window. Hotkeys, notifications, floating pins, share cards and a usable settings/customization flow are missing.

**Required fix:** configure an actual Windows desktop target and XAML compilation, connect an STA desktop entry point and lifecycle to the shared host, then implement and test the tray/product flows. Merely excluding the code-behind to make CI green would leave the requested app unimplemented.

### R2 — P1: Multiple provider protocols differ from the pinned source

These are source-level incompatibilities and do not require live accounts to discover:

| Provider | Observed implementation | Reference requirement / consequence |
| --- | --- | --- |
| Antigravity | `DiscoverPort()` discards process matches and probes hardcoded ports, including 8080/8000. Sends GET `/quota-summary` without CSRF. Reads JSON conversation files. | Reference discovers the owning process/arguments/ports, calls `RetrieveUserQuotaSummary` through the language-server RPC with CSRF where required, and scans SQLite/protobuf conversations across stores. Current code can select an unrelated local service and misses the actual protocol/history. |
| OpenCode | Calls `https://opencode.ai/api/usage`; reads top-level `token`/`access_token`/`apiKey`; returns before local scanning when auth is absent or the request fails. | Reference reads `auth.json`'s nested `opencode-go` key, calls `/zen/go/v1/usage`, maps `usage.rolling/weekly/monthly`, and allows hosted local spend without Go quotas. OAuth attribution to Codex is absent. |
| Z.ai | Calls `/api/biz/subscription` and expects top-level session/weekly objects. | Reference separates `/api/biz/subscription/list` and `/api/monitor/usage/quota/limit`, mapping the quota `limits` array. Current code does not request that quota resource. |
| Grok | Calls `https://grok.x.ai/api/billing/settings`; no working refresh callback. | Reference uses `cli-chat-proxy.grok.com/v1/billing?format=credits`, a separate `/v1/settings` request and OAuth refresh. |
| Ollama | Signs `timestamp,path`, sends `Bearer timestamp:signature`, omits `?ts=`, and reads top-level session/weekly fields. | Reference signs `METHOD,request-uri`, includes the timestamp in that URI and public key in the authorization value, and maps `limits.*.usage` fractions plus `activity.cost`. The OpenSSH key-container reader also needs an actual fixture; a generic PEM reader is not proof of compatibility. |
| Devin | Reads `.devin/credentials.toml` fields `token`/`api_key` or an environment key; no app-database fallback. | Reference uses `windsurf_api_key`, configured `api_server_url`, and CLI-to-app source fallback. The Windows path/format is unverified and the source-switch behavior claimed in the earlier report is absent. |

Evidence: [provider implementations](../src/WinLLMUsage.Providers), [reference providers](../vendor/openusage/Sources/OpenUsage/Providers). The required fix is to translate the actual request builders, auth formats, response mappers and fallbacks, with sanitized upstream-shaped fixtures for every pipeline. These classes should not be described as complete integrations.

### R3 — P1: Local spend is not connected to the pricing engine

[ModelPricing.cs](../src/WinLLMUsage.Core/Pricing/ModelPricing.cs) contains rate containers, an estimator and JSON resource-opening helpers. The provider catalog/scanners do not load those resources into an active pricing service or call the estimator. The declared long-context fields are not used by `EstimateCost`; dynamic catalog refresh, request-tier logic and fallback selection are not wired into the product.

[Claude scanning](../src/WinLLMUsage.Providers/Claude/ClaudeProvider.cs) sums input/output and `costUSD ?? 0`; [Codex scanning](../src/WinLLMUsage.Providers/Codex/CodexProvider.cs) accumulates tokens with an unchanged zero cost. Codex only looks for usage fields at the record root, missing the reference's nested `payload.info` accounting. Antigravity similarly produces zero costs and reads the wrong storage format. These paths can report missing/zero costs rather than the reference estimates.

Pi ingestion, shared Codex request pricing, full subagent/fork deduplication, advisor usage, model breakdowns, unknown-model handling and persisted incremental parsing are not implemented to reference parity.

**Required fix:** port reference-shaped scanners and pricing codecs together; test recorded-cost precedence, absent-versus-zero values, cache tokens, long-context/priority boundaries and replay cases before accepting Total Spend.

### R4 — P1: Refreshed credentials are not persisted and Cursor retries its old token

Claude and Codex refresh methods return updated in-memory records but do not write rotated tokens back to their owned auth files. Later refreshes reload the old credential. Cursor's refresh method only returns HTTP success: it neither consumes the new access token nor updates the headers captured by the retry request. Grok's helper callback always returns false. There is no implemented generation-aware persistence or cross-process credential lock.

Evidence: `RefreshTokenAsync` in [Claude](../src/WinLLMUsage.Providers/Claude/ClaudeProvider.cs), [Codex](../src/WinLLMUsage.Providers/Codex/CodexProvider.cs), [Cursor](../src/WinLLMUsage.Providers/Cursor/CursorProvider.cs), and [Grok.RefreshAsync](../src/WinLLMUsage.Providers/Grok/GrokProvider.cs).

**Required fix:** preserve source ownership and expiry metadata, retry with the returned credential, and implement conditional atomic persistence only for sources that permit writes. Test login changes and concurrent refreshes without live credentials. Keep Desktop sources read-only.

### R5 — P1: Account isolation and shared-cache coordination are not wired

[RefreshCoordinator](../src/WinLLMUsage.Infrastructure/Refresh/RefreshCoordinator.cs) stores every snapshot with `identityKey: null`; `SnapshotCache.HasStaleAccountStamp` has no caller. [ProviderCatalog](../src/WinLLMUsage.Providers/Catalog/ProviderCatalog.cs) constructs one Claude provider, not discovered account/organization cards. A family serializer test does not establish account discovery or isolation. An account switch can therefore retain old-account snapshots.

[SnapshotCache](../src/WinLLMUsage.Infrastructure/Cache/SnapshotCache.cs) loads its dictionary once per instance and uses an in-process lock plus a shared `.tmp` filename. GUI and CLI writers have no shared transaction/lock or reread/merge step. Concurrent writers can collide or overwrite each other's changes, and the GUI need not observe CLI updates. Cache reads and refresh error/backoff dictionaries also lack consistent synchronization with concurrent writes.

**Required fix:** wire identities through discovery, cache validation and in-flight refresh cancellation; implement transactional cross-process storage and per-source refresh coordination; test two accounts and two processes with synthetic inputs.

### R6 — P1: No installer is implemented, and install verification always succeeds

[package.ps1](../scripts/package.ps1) publishes two directories and creates a ZIP/checksum. It never invokes Velopack or produces `Setup.exe`, regardless of whether `vpk` is installed. [verify-install.ps1](../scripts/verify-install.ps1) prints a checklist and exits zero on every platform without asserting anything. Thus a successful script invocation would not verify installation, upgrade or uninstall. The package script also needs explicit native-command failure handling before archiving outputs.

**Required fix:** implement a pinned packaging tool/manifest, stable CLI launcher, actual installer/update lifecycle and clean-machine assertions with truthful failure/skip reporting. Signing credentials are an external prerequisite; the missing installer code is not.

### R7 — P2: GUI freshness incorrectly survives a restart

`SnapshotCache.Store` persists `WrittenThisProcess: true`. `EnsureLoaded` deserializes that flag unchanged, so a later GUI cache instance with `allowsPersistedFreshness: false` can treat a prior process's snapshot as fresh. The earlier report claimed the opposite.

**Required fix:** keep this flag process-local or reset it on deserialize. Add a regression that stores a recent snapshot, creates a second cache instance over the file, expects GUI freshness false and CLI freshness true. A temporary console probe against the built Core/Infrastructure assemblies stored a synthetic snapshot and opened a new GUI-policy cache instance over the same file. It printed `GUI cache fresh after reload: True (expected False)`. The probe used temporary data only and made no network requests.

### R8 — P2: Some missing values become fabricated zero usage

Antigravity, Z.ai, Grok, Devin and OpenCode mappers contain `?? 0` fallbacks for missing usage; Z.ai can also supply a default search limit. Ollama defaults missing activity cost to zero. This differs from the plan's absent-data semantics and can make a malformed response look like an unused allowance.

**Required fix:** validate required fields, omit genuinely absent optional metrics and distinguish unavailable from a provider-reported zero. Add upstream-shaped missing-field fixtures; a successfully parsed JSON document is not necessarily a usable response.

### R9 — P2: Codex reset claiming has no safety-flow or transport tests

[ClaimResetAsync](../src/WinLLMUsage.Providers/Codex/CodexProvider.cs) posts supplied credit/request IDs, but does not fetch/re-match a fresh credit list or bind the operation to the account selected in a confirmation UI. There is no connected confirmation flow or demonstrated idempotency/ambiguous-response handling. [CodexMapperTests](../tests/WinLLMUsage.Providers.Tests/CodexMapperTests.cs) tests display mapping only; it does not invoke the claim method.

**Required fix:** keep this method disconnected from product actions until exact-credit/account checks, deliberate confirmation and mocked duplicate/ambiguous-response tests are implemented. Do not validate by consuming a real credit.

### R10 — P2: Accepted Windows adaptations are not implemented features

Folder sync has history types but no folder transport/watch/merge lifecycle. Startup registration is a marker file, not an HKCU Run entry. Privacy settings are not proof that values are hidden across a UI, notifications or exports. Global shortcut, notifications, share cards and the floating strip are absent. First-run detection also falls back to enabling Claude/Codex/Cursor when nothing is detected, contrary to the plan's empty state.

**Required fix:** implement these product flows before their acceptance tests, and track each separately. Moving from iCloud to folder sync is an accepted design change; omitting sync is not.

### R11 — P2: Release/reproducibility and branding claims need correction

There is no checked-in .NET tool manifest or NuGet `packages.lock.json` set despite the proposed locked restore/tool-restore commands. The native WPF build is already failing, so the earlier “On Windows (full product)” instructions are not a working delivery path.

`src/WinLLMUsage.App/Assets/ProviderIcons/openusage.svg` remains in the application asset tree. Its presence contradicts a blanket statement that the upstream logo is absent; this review did not establish that it is displayed or packaged. Remove/replace it before release and audit resulting artifacts, while retaining upstream source/license attribution in the vendor reference.

## Corrected implementation status

| Area | Status at reviewed revision |
| --- | --- |
| Shared models, selected serializers/layout/migration | Partial implementation with selected passing fixtures; full compatibility not established. |
| CLI and headless refresh/API host | Implemented source; broad end-to-end/error/concurrency coverage missing. |
| Claude/Codex providers | Selected mapper tests pass; auth rotation, account handling and spend have open defects. |
| Other nine providers | Partial, live-unverified implementations; several concrete protocol defects in R2. |
| Pricing and history | Helpers/resources and basic scans; required end-to-end calculation and persistence absent. |
| Windows UI | Disconnected source; Windows build fails. |
| Desktop credential stores | DPAPI wrapper for app-owned secrets; companion encrypted-store formats unverified/unimplemented. |
| Sync/startup/hotkeys/notifications/export | Models/placeholders or missing product integration. |
| Installer/updater | Not implemented; portable ZIP script is not an installer. |
| Windows acceptance | Build failure observed in CI; no clean-machine desktop/install/live-provider validation. |

The [parity matrix](PARITY_MATRIX.md), [authentication sources](WINDOWS_AUTH_SOURCES.md), [build notes](BUILD.md) and [Windows verification backlog](WINDOWS_VERIFICATION.md) should be read with this report. “Fixture verified” applies only to the named test cases, never automatically to an entire provider.

## Remediation and acceptance order

1. Repair the actual WPF target/entry point and obtain a passing Windows build without hiding desktop source from compilation.
2. Correct provider requests/auth formats using pinned reference fixtures; implement refresh persistence and account/cache isolation before live-account testing.
3. Wire pricing and scanners with cross-source replay, unknown-cost and nested-log regressions.
4. Finish dashboard/settings/customization and the Windows adaptations, including a safe mocked reset-credit flow.
5. Implement installer/updater/CLI registration and real verification scripts; add reproducible dependency/tool configuration.
6. Run native Windows integration/clean-machine acceptance and document fixture-tested versus live-tested companion versions separately.

No production-ready or complete-port claim is justified until those gates pass. External access to a Windows interactive desktop or companion accounts is needed for final validation, but it is not needed to fix the source discrepancies identified here.
