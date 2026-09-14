#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
dotnet test "$root/tests/WinLLMUsage.Core.Tests/WinLLMUsage.Core.Tests.csproj" -c Release
$cli = "dotnet"
& $cli run --project "$root/src/WinLLMUsage.Cli/WinLLMUsage.Cli.csproj" -c Release --no-build -- --help | Out-Host
& $cli run --project "$root/src/WinLLMUsage.Cli/WinLLMUsage.Cli.csproj" -c Release --no-build -- -v | Out-Host
