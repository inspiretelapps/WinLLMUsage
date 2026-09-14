#!/usr/bin/env pwsh
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version = "0.7.0"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true
$root = Resolve-Path "$PSScriptRoot/.."
Set-Location $root
$out = Join-Path $root "artifacts/$Runtime"
New-Item -ItemType Directory -Force -Path $out | Out-Null

dotnet publish "$root/src/WinLLMUsage.App/WinLLMUsage.App.csproj" -c $Configuration -r $Runtime --self-contained true -o "$out/app"
dotnet publish "$root/src/WinLLMUsage.Cli/WinLLMUsage.Cli.csproj" -c $Configuration -r $Runtime --self-contained true -o "$out/cli"

if (-not (Test-Path "$out/app/WinLLMUsage.exe") -and -not (Test-Path "$out/app/WinLLMUsage.dll")) {
    throw "App publish directory is missing WinLLMUsage output."
}
if (-not (Test-Path "$out/cli/winllmusage.exe") -and -not (Test-Path "$out/cli/winllmusage.dll")) {
    throw "CLI publish directory is missing winllmusage output."
}

Copy-Item -Force "$out/cli/winllmusage.exe" "$out/app/" -ErrorAction SilentlyContinue
Copy-Item -Force "$out/cli/winllmusage.dll" "$out/app/" -ErrorAction SilentlyContinue

$portable = Join-Path $root "artifacts/WinLLMUsage-$Version-$Runtime-portable.zip"
if (Test-Path $portable) { Remove-Item $portable }
Compress-Archive -Path "$out/app", "$out/cli" -DestinationPath $portable
Get-FileHash $portable -Algorithm SHA256 | ForEach-Object {
    "$($_.Hash)  $(Split-Path $_.Path -Leaf)" | Set-Content "$portable.sha256"
}
Write-Host "Portable ZIP: $portable"

dotnet tool restore
$setupDir = Join-Path $out "setup"
New-Item -ItemType Directory -Force -Path $setupDir | Out-Null
dotnet tool run vpk -- pack --packId WinLLMUsage --packVersion $Version --packDir "$out/app" --mainExe WinLLMUsage.exe --outputDir $setupDir --packTitle "WinLLMUsage"

$setup = Get-ChildItem $setupDir -Filter "*Setup.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $setup) {
    $setup = Get-ChildItem $setupDir -Filter "*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
}
if ($setup) {
    $dest = Join-Path $root "artifacts/WinLLMUsage-$Version-$Runtime-Setup.exe"
    Copy-Item $setup.FullName $dest -Force
    Get-FileHash $dest -Algorithm SHA256 | ForEach-Object {
        "$($_.Hash)  $(Split-Path $_.Path -Leaf)" | Set-Content "$dest.sha256"
    }
    Write-Host "Installer: $dest"
} else {
    Write-Warning "vpk did not produce a Setup.exe. Portable ZIP is still available."
}
