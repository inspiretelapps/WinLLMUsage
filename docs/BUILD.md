# Build

Requires the .NET 10 SDK (pinned in `global.json` to 10.0.105, roll-forward latest feature).

## macOS / Linux (this workspace)

Core, providers, infrastructure, CLI, and tests compile here:

```bash
dotnet restore WinLLMUsage.sln
dotnet build WinLLMUsage.sln -c Release
dotnet test WinLLMUsage.sln -c Release --no-build
dotnet run --project src/WinLLMUsage.Cli -- --help
```

The headless app host:

```bash
dotnet run --project src/WinLLMUsage.App
```

WPF (`UseWPF`, tray icon, installer) needs Windows. A successful macOS build does **not** prove the tray UI.

## Windows 11 x64

```powershell
dotnet tool restore
dotnet restore WinLLMUsage.sln --locked-mode
dotnet build WinLLMUsage.sln -c Release --no-restore
dotnet test WinLLMUsage.sln -c Release --no-build
pwsh ./scripts/package.ps1 -Runtime win-x64
pwsh ./scripts/verify-contracts.ps1
pwsh ./scripts/verify-install.ps1
```

Self-contained publish does not require a separately installed .NET runtime on the target machine.
