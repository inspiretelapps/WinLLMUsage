# WinLLMUsage implementation report

**For:** engineering / product review  
**Date:** 14 September 2026  
**Workspace:** `/Users/jaco/codex/winllmusage`  
**Version in tree:** `0.1.0-dev`  
**GitHub (private):** https://github.com/inspiretelapps/WinLLMUsage  
**Commit:** `c8cd18f` (`main`)  
**Verdict:** implemented engine, CLI, local API, and all 11 provider pipelines; **not Windows-verified**. No GitHub repository existed before this report; `inspiretelapps/WinLLMUsage` was created and pushed as a **private** repo.

---

## 1. What was asked

Build the entire `WINDOWS_IMPLEMENTATION_PLAN.md` in one continuous delivery:

- Independent Windows product **WinLLMUsage** (not a rebranded OpenUsage binary)
- C# on **.NET 10 LTS**, WPF tray UI, shared provider engine, CLI, compatibility HTTP API
- Behavioral baseline: OpenUsage **v0.7.11**, commit `753e2fe4bc82a011567d16d8deb88176b58ed24e`
- All **11** providers: Claude, Codex, Cursor, Antigravity, Copilot, Devin, Grok, Ollama, OpenCode, OpenRouter, Z.ai
- Preserve resource IDs, defaults, pricing rules, CLI/HTTP contracts
- Windows adaptations from plan §3 only (tray instead of menu-bar strip, DPAPI, folder sync instead of iCloud, Velopack, manual Privacy Mode)
- Honest blockers; no invented Windows credential formats, no fake live logins, no published release

The plan’s own definition of done includes a clean-machine Windows 11 x64 installer check. That check **cannot** be claimed from this macOS workspace.

---

## 2. How the work was done

### 2.1 Freeze the baseline

1. Cloned `https://github.com/robinebers/openusage` at tag `v0.7.11`.
2. Confirmed `git rev-parse HEAD` = `753e2fe4bc82a011567d16d8deb88176b58ed24e`.
3. Read `Package.swift`, `ProviderCatalog.swift`, `ProviderRuntime.swift`, models, CLI, local HTTP API, default layout, settings migrator, and each provider folder.
4. Copied bundled pricing JSON into `src/WinLLMUsage.Core/Resources/` (supplement, LiteLLM snapshot, models.dev snapshot).
5. Kept the Swift tree as `vendor/openusage` (read-only reference, not the shipping app).
6. Recorded identity, contracts, and Windows path candidates in `docs/UPSTREAM_BASELINE.md`, `docs/PARITY_MATRIX.md`, and `docs/WINDOWS_AUTH_SOURCES.md`.

### 2.2 Port order (plan §10)

| Step | What landed |
| --- | --- |
| A. Baseline | Vendor pin + inventory docs |
| B. Windows boundaries | Candidate paths/env vars per provider; encrypted Desktop/Cursor/Codex vaults marked **Blocked** until a Windows install is inspected |
| C. Scaffold | `WinLLMUsage.sln`, central package versions, 6 src projects + 5 test projects |
| D. Pure behavior | MetricLine, descriptors, layout IDs, settings schema v3, pacing, limits/usage serializers, CLI parser |
| E. Shared I/O | HttpClient transport + proxy, snapshot cache, JSONL reader, SQLite read-only, loopback server, DPAPI wrapper |
| F. Providers | All 11 runtimes with auth → HTTP → mapper → optional local scan |
| G. Product shell | Headless app host (refresh loop + API); WPF XAML source for Windows; Codex reset-claim method |
| H. Verify | `dotnet test` on macOS (28 tests); CLI `--help` / `-v` / unknown option → exit 2 |

No live provider accounts were used. Reset-credit claiming is coded against the documented POST body and only exercised via mapper tests, not against a real Codex account.

### 2.3 Translation method

The C# types are a structured port of the Swift vocabulary, not a new product model:

- `MetricLine` cases: `progress` / `values` / `badge` / `chart` / `text`
- Limits wire schema: `openusage.limits.v1`
- Legacy usage route still serializes `.values` as combined `text` lines and `.chart` as `barChart`
- Refresh cadence: **5 minutes**; provider deadline **120 s**; CLI reuses disk cache, GUI does not treat previous-process cache as fresh
- Default metric IDs, pins (max 2 per provider), On Demand membership, and schema-v3 remaps (`antigravity.session` → `geminiPro`, `copilot.credits` → `premium`) copied from `DefaultLayout.swift`
- Ollama `hasLocalCredentials()` is **always false** (explicit opt-in), matching upstream
- Family matching for CLI/API: exact card id **or** family id (`claude`, `codex`)

Windows-specific storage is injected (`IProviderPaths`, `ISecretStore`, `IHttpTransport`). Provider mappers do not reference WPF or the registry.

---

## 3. Solution shape

```
WinLLMUsage.sln
src/
  WinLLMUsage.Core             net10.0   models, contracts, serializers, layout, pricing, pacing
  WinLLMUsage.Providers        net10.0   11 provider pipelines
  WinLLMUsage.Infrastructure   net10.0   HTTP, cache, SQLite, JSONL, local API, file secrets
  WinLLMUsage.Windows          net10.0   DPAPI wrapper, known folders, launch-at-login marker
  WinLLMUsage.Cli              net10.0   winllmusage.exe console host
  WinLLMUsage.App              net10.0   generic host + WindowsUi/ WPF source
tests/                         xUnit, net10.0
vendor/openusage/              pinned Swift reference
docs/                          baseline, parity, auth, privacy, verification backlog
scripts/                       build.ps1, test.ps1, package.ps1, verify-*.ps1
.github/workflows/             windows-ci.yml, windows-release.yml
```

**Dependency direction** (as specified):

```
App / CLI → Providers → Core
                 ↘ Infrastructure
Windows adapters wrap secrets/paths; they are not imported by Core.
```

SDK pin: `global.json` → `10.0.105` (roll-forward latest feature). Packages are centrally versioned in `Directory.Packages.props`. Nullable + warnings-as-errors are on.

---

## 4. What each layer actually does

### 4.1 Core (`WinLLMUsage.Core`)

- **Snapshots:** `ProviderSnapshot` with plan, lines, history, warning vs error category.
- **Widgets:** factories for percent / bounded dollars / spend tiles (`{id}.today|yesterday|last30`) / usage trend (not pinnable).
- **Limits API:** selects only descriptors that export `LimitResourceDescriptor`. Missing resources are omitted, never invented as zero. `expiresAt` = `fetchedAt` + 300 s. Sorted JSON keys on the limits envelope.
- **Usage API:** collection and per-id routes; per-id always an **array**; OPTIONS 204; POST 405; unknown token 404 `provider_not_found`; busy 503.
- **CLI parser:** one positional provider (lowercased), `--force`, `-h`/`--help`, `-v`/`--version`. Exit 0 / 2 / 4.
- **Enablement:** enabled-list vs legacy disabled-list; known-provider set so new providers can be probed without overriding user offs.
- **Migrator:** empty store stamps schema 3; v2 converts disabled list; v3 remaps dead metric IDs.
- **Pacing:** meter bands (80% warning / 90% critical without a window; projection-based running-out / close / healthy with a reset). Notification transitions with first-observation priming.
- **Pricing:** three-catalog precedence (supplement → LiteLLM → models.dev) plus optional Codex fallback model. Bundled JSON is an embedded resource.

### 4.2 Infrastructure

- `HttpTransport`: `SocketsHttpHandler`, optional `http`/`https`/`socks5` proxy, loopback bypass, per-request timeout (default 15 s).
- `SnapshotCache`: JSON file; error snapshots are not stored; CLI vs GUI freshness flag.
- `JsonlStreamingReader`: 64 KiB chunks, skip records &gt; 1 MiB.
- `LocalUsageServer`: `127.0.0.1:6736`, max 16 connections, CORS `*` GET/OPTIONS, port-in-use leaves the process running.
- `FileSecretStore` + Windows `DpapiSecretStore` (`ProtectedData`, current user).
- Log redaction for Bearer tokens, `api_key` / `access_token` JSON fields.

### 4.3 Providers (all 11 are real runtimes, not dashboard fixtures)

| Provider | Auth read | Network | Local spend |
| --- | --- | --- | --- |
| **Claude** | `.claude/.credentials.json`, `CLAUDE_CONFIG_DIR`, `CLAUDE_CODE_OAUTH_TOKEN` | `GET /api/oauth/usage` + OAuth refresh `platform.claude.com/v1/oauth/token` | JSONL under `projects/` |
| **Codex** | `auth.json` via `CODEX_HOME` / `.codex` / `.config/codex` | `GET chatgpt.com/backend-api/wham/usage`, reset-credits GET/POST consume | `sessions/` + `archived_sessions/` JSONL |
| **Cursor** | `%APPDATA%\Cursor\...\state.vscdb` `cursorAuth/*` | Connect RPC `GetCurrentPeriodUsage` (+ optional Grok Bot) | not merged (account-wide) |
| **Antigravity** | loopback language-server ports | `https://127.0.0.1:{port}/quota-summary` | `.gemini/antigravity/conversations` |
| **Copilot** | `hosts.json` / `apps.json`, then `gh auth token` | `GET api.github.com/copilot_internal/user` | none |
| **Devin** | `~/.devin/credentials.toml` (Tomlyn) | `POST .../ada/GetUserStatus` | none |
| **Grok** | `GROK_HOME` / `.grok/auth.json` | billing/settings | `.grok/sessions` JSONL, replay/subagent dedup by id |
| **Ollama** | `~/.ollama/id_ed25519` (BouncyCastle Ed25519) | signed `GET ollama.com/api/usage` | none (last-4-weeks from API) |
| **OpenCode** | `auth.json` in data dir | Go usage API | read-only `opencode*.db` |
| **OpenRouter** | DPAPI / config JSON / `OPENROUTER_API_KEY` | `/api/v1/credits`, optional `/key` | API period rows |
| **Z.ai** | DPAPI / config / `ZAI_API_KEY` / `GLM_API_KEY` | subscription endpoint | none |

**401/403** go through a single refresh-then-retry helper (Devin uses source switch instead, matching upstream). Claude **429** keeps local spend and a warning rather than blanking the card.

Codex reset claim: `POST .../rate-limit-reset-credits/consume` with `{ credit_id, redeem_request_id }`. Success codes `reset` and `already_redeemed`. Not exposed on the local HTTP API or CLI.

### 4.4 CLI

`winllmusage [provider] [--force]`

- Prints **only** limits JSON on stdout (plus trailing newline)
- Diagnostics on stderr, prefixed `winllmusage:`
- Same engine and cache as the app host
- `--force` bypasses the 5-minute freshness gate
- Unknown provider / bad flags → exit **2**
- Refresh/read warnings → JSON still printed, exit **4**

Verified here: `--help`, `-v` → `winllmusage 0.1.0-dev`, `--nope` → exit 2.

### 4.5 App host

`src/WinLLMUsage.App` is a generic host (compiles on macOS):

- Named mutex so a second GUI launch is a no-op on this host
- First-run: seed enabled providers from `HasLocalCredentialsAsync` (starter set claude/codex/cursor if none)
- 5-minute refresh loop
- Local API hosted unless `winllmusage.localApi.enabled` is false

WPF dashboard XAML lives in `src/WinLLMUsage.App/WindowsUi/` (borderless ~400 DIP window, refresh/settings/exit). It is **not compiled on macOS** (`Page Remove`). Tray `NotifyIcon`, `RegisterHotKey`, and Velopack wiring are specified in the plan and still need a Windows build.

---

## 5. Tests that were run

Command (this workspace, macOS, .NET 10.0.105):

```text
dotnet test WinLLMUsage.sln -c Release
```

**28 passed, 0 failed.**

| Project | Count | Covers |
| --- | --- | --- |
| Core.Tests | 20 | CLI parse, ISO-8601, limits progress encoding, omit-missing-resources, OPTIONS 204, POST 405, Claude family match, usage array, Ollama session+weekly exports, layout pin cap, v3 remaps, settings migrator |
| Providers.Tests | 3 | Claude session/weekly/extra cents→dollars; Codex Business Premium / Pro 20x / credits×$0.04; reset-credit available vs used |
| Infrastructure.Tests | 3 | secret redaction; JSONL record split; oversized-line skip |
| Windows.Tests | 1 | launch-at-login marker file round-trip |
| App.Tests | 1 | package id is not upstream `com.robinebers.openusage` |

These are **fixture / contract tests**. They do not prove tray behavior, installer, or live companion auth on Windows 11.

---

## 6. Planned Windows adaptations that are in the design (not bugs)

From plan §3, intentionally different from macOS OpenUsage:

| macOS | This port |
| --- | --- |
| Wide menu-bar text/bar strip | One tray icon + optional floating strip (strip UI not wired on macOS host) |
| Keychain | DPAPI for *app-owned* secrets; companion formats only after evidence |
| iCloud private container | Folder-based history document model (watcher not finished) |
| Sparkle | Velopack script skeleton; no feed URL |
| Screen-capture auto-hide | Manual Privacy Mode setting |
| PostHog telemetry | Local logs only |

---

## 7. Gaps a reviewer should treat as open

Do **not** call this a fully complete Windows port until these are closed.

### Blocked without a Windows install / live companion

1. Claude Desktop Chromium `v10` / DPAPI token cache
2. Cursor `state.vscdb` values if Electron safeStorage-encrypted
3. Codex Windows Credential Manager item (file `auth.json` only today)
4. Antigravity Windows OAuth/keyring (port probe only)

### Implemented in model/source, not product-complete

5. Full WPF tray: left/right click, `Shell_NotifyIconGetRect`, mixed-DPI, Explorer restart
6. Global hotkey, HKCU Run launch-at-login (marker file exists; registry write is Windows-host work)
7. Quota toast notifications with baseline suppression
8. Draggable always-on-top pin strip persistence
9. Share-card PNG export
10. Folder sync: watch, merge, disable-and-delete *this* device’s file
11. Incremental JSONL disk cache (FNV identity, 35-day prune) — streaming reader exists; persisted scan store does not
12. Self-contained Velopack `Setup.exe`, publisher signing, update feeds
13. Translation of the large upstream Swift test suite (only a slice of contract/mapper tests exist)

### Environment

14. No Windows 11 x64 clean-machine run. See `docs/WINDOWS_VERIFICATION.md`.

---

## 8. How to review the code

On macOS/Linux (engine + CLI):

```bash
dotnet restore WinLLMUsage.sln
dotnet test WinLLMUsage.sln -c Release
dotnet run --project src/WinLLMUsage.Cli -- --help
```

On Windows 11 x64 (full product):

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/test.ps1
pwsh ./scripts/package.ps1 -Runtime win-x64
```

Read first:

1. `WINDOWS_IMPLEMENTATION_PLAN.md` — the spec this tree claims to follow
2. `docs/PARITY_MATRIX.md` — feature-by-feature status
3. `docs/WINDOWS_AUTH_SOURCES.md` — what was guessed vs proven
4. `src/WinLLMUsage.Core/Serialization/LocalLimitsApi.cs` and `LocalUsageApi.cs` — public contracts
5. `src/WinLLMUsage.Providers/Catalog/ProviderCatalog.cs` — composition order (Claude, Codex, Cursor, then alphabetical)

Security notes for review:

- Companion SQLite is opened **read-only**.
- Logs go through `SecretRedactor`.
- Local API is loopback-only and does not expose tokens; CORS is still `*` (upstream compatibility). Off switch: settings key `winllmusage.localApi.enabled`.

---

## 9. License and branding

- Upstream MIT copyright retained (`LICENSE`, Robin Ebers 2026).
- Product name **WinLLMUsage**, package id `com.winllmusage.app`, data dir `%LOCALAPPDATA%\WinLLMUsage`.
- OpenUsage trademarks/logo are not used (`docs/UPSTREAM_TRADEMARK.md`).
- No GitHub Release and no production version were published from this work. Version remains `0.1.0-dev`.
