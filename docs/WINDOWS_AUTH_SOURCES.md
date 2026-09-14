# Windows authentication sources

Reviewed against revision `ab983a4` on 14 September 2026. These are **observed code paths**, not proof of compatibility with installed Windows companions. No live credential was read during this review. See [implementation review](IMPLEMENTATION_REPORT.md), especially R2/R4/R5.

| Provider | Current source/precedence | Current write behavior / gaps |
| --- | --- | --- |
| Claude | `CLAUDE_CONFIG_DIR`, then profile `.claude`; environment token only when the selected credential file is absent | Refresh changes memory only. No generation-aware persistence or Desktop decoder/account discovery. |
| Codex | `CODEX_HOME`, profile `.codex`, profile `.config/codex`; reads `auth.json` | Refresh changes memory only. No verified Windows vault adapter. |
| Cursor | `%APPDATA%\Cursor\User\globalStorage\state.vscdb`, exact `cursorAuth/*` keys | Read-only SQLite; refresh response is discarded and retry keeps old headers. Encrypted values unverified. |
| Antigravity | Probes hardcoded loopback ports; process enumeration does not establish port ownership | No real process/CSRF discovery, Windows OAuth/keyring or derived-token cache. |
| Copilot | Candidate profile `.config/github-copilot`, `GH_CONFIG_DIR`, profile `.config/gh`; file parsing and `gh auth token` fallback | Read-only; no evidence covering all installed Windows formats or full organization-billing behavior. |
| Devin | Profile `.devin/credentials.toml` `token`/`api_key`, then `DEVIN_API_KEY` | Read-only. Reference `windsurf_api_key`, configured server and app DB fallback are absent. |
| Grok | `GROK_HOME` or profile `.grok/auth.json`, top-level token fields | No implemented OAuth refresh or rotation persistence. |
| Ollama | Profile `.ollama/id_ed25519` through generic PEM reader | Key-container compatibility unverified; signing protocol differs from reference. |
| OpenCode | `OPENCODE_DATA_DIR`, `XDG_DATA_HOME/opencode`, profile `.local/share/opencode`, `%LOCALAPPDATA%\opencode` | Read-only; nested `opencode-go` auth is not decoded by the current top-level token loader. |
| OpenRouter | Profile `.config/openusage/openrouter.json`, `.config/winllmusage/openrouter.json`, saved secret, environment | Existing config files precede the saved key. No connected key-management UI. |
| Z.ai | Profile `.config/openusage/zai.json`, `.config/winllmusage/zai.json`, saved secret, `ZAI_API_KEY`, `GLM_API_KEY` | Existing config files precede the saved key. No connected key-management UI. |

Provider classes currently take the concrete `AppPaths` type; the presence of an `IProviderPaths` interface does not mean every provider uses independently injectable path discovery.

App-owned secrets use `DpapiSecretStore` on Windows and a plain `FileSecretStore` on other platforms. The Windows wrapper selects current-user DPAPI, but no DPAPI round-trip/ACL test exists in the current suite. These wrappers do not establish access to companion applications' credential stores. API-key settings UI, credential ownership rules, race-safe token persistence and account isolation remain implementation tasks.
