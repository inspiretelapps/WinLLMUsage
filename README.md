# WinLLMUsage

Windows port of [OpenUsage](https://github.com/robinebers/openusage) v0.7.11 (`753e2fe4`). Independent branding, MIT-licensed translation of the shared engine.

WinLLMUsage is a tray / CLI utility that reads local AI-tool credentials and shows quotas, spend, and reset times for Claude, Codex, Cursor, Antigravity, Copilot, Devin, Grok, Ollama, OpenCode, OpenRouter, and Z.ai.

This is a **development** tree (`0.1.0-dev`). It is **implemented but not Windows verified** from this macOS workspace. See `docs/WINDOWS_VERIFICATION.md`.

**Review packet:** [docs/IMPLEMENTATION_REPORT.md](docs/IMPLEMENTATION_REPORT.md)  
**Repository:** https://github.com/inspiretelapps/WinLLMUsage (private)

## Layout

- `src/WinLLMUsage.Core` — models, limits/usage serializers, layout, pricing, pacing
- `src/WinLLMUsage.Providers` — all 11 provider pipelines
- `src/WinLLMUsage.Infrastructure` — HTTP, SQLite, cache, JSONL, local API
- `src/WinLLMUsage.Windows` — DPAPI, known folders, launch-at-login helper
- `src/WinLLMUsage.Cli` — `winllmusage [provider] [--force]`
- `src/WinLLMUsage.App` — host + local API; WPF dashboard source under `WindowsUi/`
- `vendor/openusage` — pinned read-only Swift reference

## Commands

```bash
dotnet test WinLLMUsage.sln
dotnet run --project src/WinLLMUsage.Cli -- --help
```

Local API (when the app host is running): `http://127.0.0.1:6736/v1/limits`

## License

MIT. Upstream copyright is retained. The OpenUsage name and logo are not used.
