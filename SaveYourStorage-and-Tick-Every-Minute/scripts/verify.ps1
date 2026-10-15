[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$RimWorldDir = "E:\Apps\Steam\steamapps\common\RimWorld",
    [string]$HarmonyAssemblyPath = "E:\Apps\Steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll",
    [string]$DotNet = "dotnet"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item -LiteralPath (Join-Path $PSScriptRoot "..")).FullName.TrimEnd([char[]]"\/")
& $DotNet build (Join-Path $repoRoot "Source\SaveYourStorageAndTickEveryMinute.csproj") `
    --configuration $Configuration --nologo "-p:RimWorldDir=$RimWorldDir" "-p:HarmonyAssemblyPath=$HarmonyAssemblyPath"
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
& $DotNet run --project (Join-Path $repoRoot "Tests\SchedulerHarness\SchedulerHarness.csproj") --configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "Scheduler harness failed with exit code $LASTEXITCODE." }
$xmlFiles = @("About\About.xml", "Analyzer.xml", "LoadFolders.xml") |
    ForEach-Object { Join-Path $repoRoot $_ }
$xmlFiles += @(Get-ChildItem -LiteralPath (Join-Path $repoRoot "1.6\Languages"), (Join-Path $repoRoot "1.7\Languages") `
    -Filter "*.xml" -File -Recurse | ForEach-Object { $_.FullName })
foreach ($xmlFile in $xmlFiles) { [xml](Get-Content -LiteralPath $xmlFile -Raw) | Out-Null }
Write-Host "Build, scheduler semantics and XML checks passed."
Write-Warning "Harmony runtime verification requires a separate game CLR process; see docs/TESTING.md for the migration results and in-game checks."
