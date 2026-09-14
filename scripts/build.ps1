#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
Set-Location $root
dotnet restore WinLLMUsage.sln
dotnet build WinLLMUsage.sln -c Release --no-restore
