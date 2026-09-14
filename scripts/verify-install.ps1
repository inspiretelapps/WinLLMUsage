#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true
if (-not $IsWindows) {
    Write-Host "SKIPPED: verify-install.ps1 requires Windows 11 x64."
    exit 0
}

$root = Resolve-Path "$PSScriptRoot/.."
$zip = Get-ChildItem "$root/artifacts" -Filter "WinLLMUsage-*-portable.zip" | Select-Object -First 1
if (-not $zip) {
    throw "No portable ZIP found. Run scripts/package.ps1 first."
}

$cli = Get-ChildItem "$root/artifacts" -Recurse -Filter "winllmusage.exe" | Select-Object -First 1
if (-not $cli) {
    throw "winllmusage.exe was not published."
}

& $cli.FullName --help | Out-Host
if ($LASTEXITCODE -ne 0) { throw "CLI --help failed" }
& $cli.FullName -v | Out-Host
if ($LASTEXITCODE -ne 0) { throw "CLI -v failed" }
Write-Host "CLI launcher is present and responds."
