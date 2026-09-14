#!/usr/bin/env pwsh
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
$out = Join-Path $root "artifacts/$Runtime"
New-Item -ItemType Directory -Force -Path $out | Out-Null

dotnet publish "$root/src/WinLLMUsage.App/WinLLMUsage.App.csproj" -c $Configuration -r $Runtime --self-contained true -o "$out/app"
dotnet publish "$root/src/WinLLMUsage.Cli/WinLLMUsage.Cli.csproj" -c $Configuration -r $Runtime --self-contained true -o "$out/cli"

$portable = Join-Path $root "artifacts/WinLLMUsage-0.1.0-dev-$Runtime-portable.zip"
if (Test-Path $portable) { Remove-Item $portable }
Compress-Archive -Path "$out/app", "$out/cli" -DestinationPath $portable

Get-FileHash $portable -Algorithm SHA256 | ForEach-Object {
    "$($_.Hash)  $(Split-Path $_.Path -Leaf)" | Set-Content "$portable.sha256"
}

Write-Host "Portable ZIP: $portable"
Write-Host "Velopack Setup.exe is produced on Windows when 'vpk' is installed. This unsigned development tree does not publish a feed."
