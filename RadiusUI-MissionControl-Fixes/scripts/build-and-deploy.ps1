#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$RimWorldDir,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if ([string]::IsNullOrWhiteSpace($RimWorldDir)) {
    if ($env:RIMWORLD_DIR) { $RimWorldDir = $env:RIMWORLD_DIR }
    elseif ($env:RimWorldDir) { $RimWorldDir = $env:RimWorldDir }
    elseif ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $RimWorldDir = 'E:\Apps\Steam\steamapps\common\RimWorld'
    }
    else { $RimWorldDir = '/mnt/e/Apps/Steam/steamapps/common/RimWorld' }
}
$RimWorldDir = [IO.Path]::GetFullPath($RimWorldDir)

$python = $null
$pythonPrefix = @()
if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT -and (Get-Command py -CommandType Application -ErrorAction SilentlyContinue)) {
    $python = (Get-Command py -CommandType Application).Source
    $pythonPrefix = @('-3')
}
else {
    foreach ($candidate in @('python3', 'python')) {
        $available = Get-Command $candidate -CommandType Application -ErrorAction SilentlyContinue
        if ($available) { $python = $available.Source; break }
    }
}
if (-not $python) { throw 'Python 3.9 or later is required.' }
if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) { throw '.NET SDK is required.' }

$helper = Join-Path $root 'scripts/deploy.py'
$preflight = @($helper, '--rimworld-dir', $RimWorldDir, '--preflight')
if ($BuildOnly) { $preflight += '--build-only' }
& $python @pythonPrefix @preflight
if ($LASTEXITCODE -ne 0) { throw 'Build/deployment safety validation failed.' }

$buildArguments = @(
    'build', (Join-Path $root 'Source/RadiusUI.MissionControl.Fixes.csproj'),
    '-c', $Configuration, "-p:RimWorldDir=$RimWorldDir", '--nologo'
)
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'Patch build failed.' }
if ($BuildOnly) {
    Write-Host "Build complete: $(Join-Path $root '1.6/Assemblies/RadiusUI.MissionControl.Fixes.dll')"
    return
}
& $python @pythonPrefix $helper '--rimworld-dir' $RimWorldDir
if ($LASTEXITCODE -ne 0) { throw 'Patch deployment failed.' }
