#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
dotnet test "$PSScriptRoot/../WinLLMUsage.sln" -c Release
