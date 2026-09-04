[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OuterrealmAssemblyPath =
        "D:\Appdata\Steam\steamapps\workshop\content\294100\3712146789\Assemblies\FullyAutomaticOmniCrafter.dll",

    [string]$RimWorldManagedPath =
        "D:\Appdata\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed",

    [switch]$SkipOuterrealmContract
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "Source\SaveYourStorageAndTickEveryMinute.csproj"
$testProjectPath = Join-Path $repoRoot "Tests\SchedulerHarness\SchedulerHarness.csproj"
$assemblyPath = Join-Path $repoRoot "1.6\Assemblies\SaveYourStorageAndTickEveryMinute.dll"

& dotnet build $projectPath --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

& dotnet run --project $testProjectPath --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Scheduler harness failed with exit code $LASTEXITCODE."
}

$xmlFiles = @(
    (Join-Path $repoRoot "About\About.xml"),
    (Join-Path $repoRoot "Analyzer.xml"))
$xmlFiles += @(Get-ChildItem `
    -LiteralPath (Join-Path $repoRoot "1.6\Languages") `
    -Filter "*.xml" `
    -File `
    -Recurse | ForEach-Object { $_.FullName })
foreach ($xmlFile in $xmlFiles) {
    [xml](Get-Content -LiteralPath $xmlFile -Raw) | Out-Null
}
Write-Host "XML verification passed ($($xmlFiles.Count) files)."

$assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
$referenceNames = @($assembly.GetReferencedAssemblies() | ForEach-Object { $_.Name })
if ($referenceNames -contains "FullyAutomaticOmniCrafter") {
    throw "The build unexpectedly has a hard reference to FullyAutomaticOmniCrafter."
}
Write-Host "Optional-dependency verification passed."

if (-not $SkipOuterrealmContract) {
    if (Test-Path -LiteralPath $OuterrealmAssemblyPath -PathType Leaf) {
        & (Join-Path $PSScriptRoot "verify-outerrealm-contract.ps1") `
            -OuterrealmAssemblyPath $OuterrealmAssemblyPath `
            -RimWorldManagedPath $RimWorldManagedPath
    }
    else {
        Write-Warning "Outerrealm Tech is not installed; its runtime contract was not checked."
    }
}

if (Test-Path -LiteralPath $RimWorldManagedPath -PathType Container) {
    & (Join-Path $PSScriptRoot "verify-rimworld-contract.ps1") `
        -RimWorldManagedPath $RimWorldManagedPath
}
else {
    Write-Warning "RimWorld is not installed; its dense-Thing runtime contract was not checked."
}

Write-Host "All available verification passed."
