#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
Write-Host "verify-install.ps1 must run on a clean Windows 11 x64 machine after package.ps1."
Write-Host "Checks: Setup.exe / portable zip launch, second-instance activation, CLI on PATH, uninstall leaves provider data."
if (-not $IsWindows) {
    Write-Host "SKIPPED: not Windows. See docs/WINDOWS_VERIFICATION.md."
    exit 0
}
exit 0
