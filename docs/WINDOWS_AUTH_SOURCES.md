# Windows authentication sources

Inspected against OpenUsage v0.7.11 source and documented Windows known-folder conventions. **Live Windows companion apps were not available in this planning workspace.** Paths below are candidates until a Windows 11 install confirms them.

| Provider | Candidate sources (in order) | Writable? | Status |
| --- | --- | --- | --- |
| Claude | `%USERPROFILE%\.claude\.credentials.json`; `CLAUDE_CONFIG_DIR\.credentials.json`; env `CLAUDE_CODE_OAUTH_TOKEN`; Claude Desktop `%APPDATA%\Claude\` token caches | Code credentials yes; Desktop **read-only** | File/env implemented. Desktop Chromium `v10` / DPAPI **Blocked** |
| Codex | `CODEX_HOME\auth.json`; `%USERPROFILE%\.codex\auth.json`; `%USERPROFILE%\.config\codex\auth.json` | yes, after re-read | File implemented. Windows Credential Manager **Blocked** |
| Cursor | `%APPDATA%\Cursor\User\globalStorage\state.vscdb` keys `cursorAuth/accessToken`, `refreshToken`, `stripeMembershipType` | access token write-back same source | SQLite read-only implemented. Encrypted values **Blocked** until format evidence |
| Antigravity | `language_server` / `agy` process ports; `%USERPROFILE%\.gemini\antigravity*` | derived cache only | Port probe implemented; Windows keyring **Blocked** |
| Copilot | `%USERPROFILE%\.config\github-copilot\hosts.json` / `apps.json`; `GH_CONFIG_DIR`; `gh auth token --hostname github.com` | no (read-only) | Implemented |
| Devin | `%USERPROFILE%\.devin\credentials.toml`; Devin `state.vscdb` | no | TOML implemented |
| Grok | `GROK_HOME\auth.json`; `%USERPROFILE%\.grok\auth.json` | rotation only if generation matches | Implemented |
| Ollama | `%USERPROFILE%\.ollama\id_ed25519` OpenSSH Ed25519 | never transmit key | Implemented |
| OpenCode | `OPENCODE_DATA_DIR`; `%LOCALAPPDATA%\opencode`; `%USERPROFILE%\.local\share\opencode` `auth.json` + `opencode*.db` | db read-only | Implemented |
| OpenRouter | DPAPI secret `openrouter.apiKey`; `~\.config\openusage\openrouter.json`; `OPENROUTER_API_KEY` | saved key yes | Implemented |
| Z.ai | DPAPI secret `zai.apiKey`; config candidates; `ZAI_API_KEY`; `GLM_API_KEY` | saved key yes | Implemented |

WinLLMUsage-owned secrets: `%LOCALAPPDATA%\WinLLMUsage\secrets\` via current-user DPAPI on Windows (`DpapiSecretStore`). DPAPI protects against other users, not against other processes of the same user.
