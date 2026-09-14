# Windows verification backlog

This delivery is **implemented but not Windows verified**. The implementation machine is macOS.

## Not run

1. Clean Windows 11 x64 install without .NET.
2. Tray left/right click, Explorer restart, mixed-DPI, monitor removal, `Shell_NotifyIconGetRect`.
3. Global hotkey via `RegisterHotKey`.
4. Launch at login HKCU Run key vs marker file.
5. Toast notifications + click-to-open dashboard.
6. Installed vs portable layout, Velopack upgrade, uninstall.
7. Live Claude Desktop / Cursor / Antigravity / Codex vault decoding.
8. Occupied port 6736 while GUI runs.
9. Two-device folder sync.
10. 100/125/150/200% scaling and high contrast.

## What was verified here

- `dotnet test WinLLMUsage.sln` on macOS (core contracts, layout, migrator, Claude/Codex mappers, JSONL reader, redaction, CLI parse).
- `dotnet build` of CLI and headless app host.
- `winllmusage --help` / `-v` / unknown option exit code 2.

Until the backlog is executed on Windows 11 x64, do not advertise Windows 10 support, ARM64, or a fully complete port.
