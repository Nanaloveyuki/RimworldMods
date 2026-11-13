param(
    [string]$ProcDumpPath = (Join-Path $PSScriptRoot 'procdump64.exe'),
    [string]$DumpDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Dumps')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ProcDumpPath -PathType Leaf)) {
    throw "ProcDump executable not found: $ProcDumpPath"
}
if ((Get-AuthenticodeSignature -LiteralPath $ProcDumpPath).Status -ne 'Valid') {
    throw 'ProcDump signature validation failed.'
}

New-Item -ItemType Directory -Path $DumpDirectory -Force | Out-Null
Write-Host "Waiting for RimWorld.exe. One full stack-overflow dump will be saved in $DumpDirectory"
& $ProcDumpPath -accepteula -ma -e 1 -g -f C00000FD -n 1 -w RimWorld.exe $DumpDirectory
exit $LASTEXITCODE
