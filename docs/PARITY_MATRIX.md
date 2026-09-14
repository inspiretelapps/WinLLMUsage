# Parity matrix

Reviewed revision: `ab983a4`, 14 September 2026. See [implementation review](IMPLEMENTATION_REPORT.md) for evidence and prioritized findings R1–R11.

Statuses: **Partial** = code exists but required behavior is missing/incorrect; **Selected fixtures pass** = only the named cases are tested; **Not implemented** = no working product path; **Blocked verification** = needs external Windows/companion evidence. No feature is marked Windows verified.

| Feature | Current implementation / evidence | Status | Review finding |
| --- | --- | --- | --- |
| Models, limits/usage serializers | Core types and selected `LocalLimitsApiTests`; not a complete wire-contract suite | Selected fixtures pass | Test evidence |
| CLI parsing/layout/schema migration | CLI, layout and migrator tests | Selected fixtures pass | Test evidence |
| Claude | One mapper test; file/env auth and basic scan; no Desktop/accounts/rotation persistence | Partial | R3–R5 |
| Codex | Two mapper tests; usage/reset HTTP methods; incomplete logs/rotation/accounts | Partial | R3–R5, R9 |
| Cursor | Read-only SQLite and basic RPC; stale-token retry, no complete REST/CSV path | Partial | R4 |
| Antigravity | `RetrieveUserQuotaSummary` Connect RPC + CSRF; exact bucket IDs; process arg discovery (Windows command line still limited) | Partial | Fixture mapper |
| Copilot | Editor-file/gh-token and basic usage code; no complete provider test coverage | Partial | Test evidence |
| Devin | `windsurf_api_key` + `api_server_url` + default `server.codeium.com` | Partial | Protocol updated |
| Grok | `cli-chat-proxy.grok.com` billing+settings; OAuth refresh persistence | Partial | Fixture mapper |
| Ollama | Signs `METHOD,request-uri?ts=`; publicKey:signature; `limits.*.usage` fractions | Selected fixtures pass | Protocol tests |
| OpenCode | Nested `opencode-go` key; `/zen/go/v1/usage` rolling/weekly/monthly; local spend without API | Selected fixtures pass | Protocol tests |
| Z.ai | `/subscription/list` + `/quota/limit`; CREDIT_LIMIT/TIME_LIMIT mapping | Selected fixtures pass | Protocol tests |
| OpenRouter | Config/DPAPI/env loader and credit/key calls; no provider tests | Partial | Test evidence |
| JSONL reader | Split-record and oversized-record tests; no persisted incremental scan cache | Selected fixtures pass | R3 |
| Pricing | Embedded JSON/helper types; no active provider pricing integration | Partial | R3 |
| Snapshots/refresh | Cache/TTL and coordinator; restart/account/concurrency defects | Partial | R5, R7 |
| Local HTTP API | Custom loopback TCP server, CORS and serializer routes; no full network suite | Partial | Test evidence |
| WPF dashboard/tray | Disconnected XAML/code-behind; confirmed Windows compile failure | Partial | R1 |
| Settings/customization/privacy | Settings models; no complete product flow | Partial | R1, R10 |
| Codex reset-credit claim | POST helper without confirmed-account UI/fresh-credit flow or transport tests | Partial | R9 |
| Folder sync | History document types; transport/watch/merge absent | Not implemented | R10 |
| Startup registration | Test exercises marker file only | Not implemented | R10 |
| Global shortcut, notifications, floating strip, share cards | No connected product implementation | Not implemented | R10 |
| Velopack installer/updater | ZIP publish script only; no vpk invocation | Not implemented | R6 |
| Install verification | Prints instructions and always exits zero | Not implemented | R6 |
| Desktop/Cursor/Codex/Antigravity encrypted credential sources | Exact Windows formats need evidence and adapters | Blocked verification | R2, R4 |

Planned Windows adaptations are design decisions, not evidence of implementation. A passing mapper or serializer test does not establish a functioning provider. All 11 providers remain live-unverified.
