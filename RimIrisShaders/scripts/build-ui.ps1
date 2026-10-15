[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RimWorldManagedDir,
    [Parameter(Mandatory = $true)][string]$RimIrisAssemblyPath
)
$ErrorActionPreference = 'Stop'
foreach ($file in @((Join-Path $RimWorldManagedDir 'Assembly-CSharp.dll'), $RimIrisAssemblyPath)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing assembly: $file" }
}
dotnet build (Join-Path $PSScriptRoot '../Source/Moonlight.UI.csproj') -c Release "-p:RimWorldManagedDir=$RimWorldManagedDir" "-p:RimIrisAssemblyPath=$RimIrisAssemblyPath"
if ($LASTEXITCODE -ne 0) { throw 'UI build failed.' }
