# WinLLMUsage

Windows port of [OpenUsage](https://github.com/robinebers/openusage) v0.7.11 (`753e2fe4`). Independent branding, MIT-licensed translation of the shared engine.

WinLLMUsage aims to be a Windows tray / CLI utility for AI-tool quotas, spend and reset times. The current tree contains a CLI/headless host and partial implementations for 11 providers; a working tray application has not been delivered.

This is a **development** tree (`0.1.0-dev`). It is **partially implemented, with a known failing Windows build**. The 28 macOS tests cover selected cases, not complete provider or product behavior. See [the implementation review](docs/IMPLEMENTATION_REPORT.md) and [Windows verification backlog](docs/WINDOWS_VERIFICATION.md).

**Review packet:** [docs/IMPLEMENTATION_REPORT.md](docs/IMPLEMENTATION_REPORT.md)  
**Repository:** https://github.com/inspiretelapps/WinLLMUsage (private)

## Layout

- `src/WinLLMUsage.Core` — models, limits/usage serializers, layout, pricing, pacing
- `src/WinLLMUsage.Providers` — 11 partial provider implementations
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

MIT. Upstream copyright is retained. WinLLMUsage uses an independent product name; the review identified an upstream logo asset still present in the application source that must be removed or replaced before release.
