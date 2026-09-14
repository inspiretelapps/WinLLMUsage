# Build

**Review status:** the Windows solution currently fails to build (reviewed revision `ab983a4`). The desktop target/entry point is incomplete. See [implementation review](IMPLEMENTATION_REPORT.md#r1--p1-the-application-does-not-build-on-windows-or-start-a-desktop-ui).

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

The project does not currently enable `UseWPF` or connect the tray/desktop lifecycle. A successful macOS build does **not** prove a functioning desktop application.

## Windows 11 x64 — target workflow, currently blocked

The commands below describe the intended workflow. The tool manifest and package lock files are not checked in, the WPF build needs repair, packaging creates only a portable ZIP, and `verify-install.ps1` performs no assertions. They are not a verified end-to-end build/install path.

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
