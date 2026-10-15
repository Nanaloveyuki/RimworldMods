[CmdletBinding()]
param(
    [string]$RimWorldDir = $(if ($env:RIMWORLD_DIR) { $env:RIMWORLD_DIR } elseif ($env:RimWorldDir) { $env:RimWorldDir } else { 'E:\Apps\Steam\steamapps\common\RimWorld' }),
    [ValidateSet('Debug', 'Release')][string]$Configuration = $(if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Release' }),
    [string]$OxygenDslDir,
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$OxygenDslDir) { $OxygenDslDir = if ($env:OXYGEN_DSL_DIR) { $env:OXYGEN_DSL_DIR } else { Join-Path (Split-Path $root -Parent) 'Oxygen-Dsl-Framework' } }
$python = Get-Command python3 -ErrorAction SilentlyContinue
if (!$python) { $python = Get-Command python -ErrorAction SilentlyContinue }
if (!$python) { throw 'Python 3 is required. On this workstation, use WSL: bash scripts/deploy.sh' }
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK is required. On this workstation, use WSL: bash scripts/deploy.sh' }
$command = if ($BuildOnly) { 'build' } else { 'deploy' }
& $python.Source (Join-Path $root 'scripts/rhah_pipeline.py') $command --rimworld-dir $RimWorldDir --oxygen-dsl-dir $OxygenDslDir --configuration $Configuration
exit $LASTEXITCODE
