$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$python = Get-Command python3 -ErrorAction SilentlyContinue
if (-not $python) { $python = Get-Command python -ErrorAction SilentlyContinue }
if (-not $python) { throw 'python3 is required for scripts/verify-scaffold.py' }
& $python.Source (Join-Path $root 'scripts/verify-scaffold.py')
if ($LASTEXITCODE -ne 0) { throw 'verify-scaffold.py failed' }
