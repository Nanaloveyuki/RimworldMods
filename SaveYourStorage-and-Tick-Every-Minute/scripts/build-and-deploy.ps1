[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$GameModPath = "D:\Appdata\Steam\steamapps\common\RimWorld\Mods\SaveYourStorageAndTickEveryMinute",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "Source\SaveYourStorageAndTickEveryMinute.csproj"
$sourceAssembly = Join-Path $repoRoot "1.6\Assemblies\SaveYourStorageAndTickEveryMinute.dll"
$sourceAbout = Join-Path $repoRoot "About\About.xml"
$sourceAnalyzer = Join-Path $repoRoot "Analyzer.xml"
$sourceLanguages = Join-Path $repoRoot "1.6\Languages"
$targetAssemblyDirectory = Join-Path $GameModPath "1.6\Assemblies"
$targetLanguageDirectory = Join-Path $GameModPath "1.6\Languages"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not $SkipBuild) {
    Write-Host "Building $Configuration..."
    & dotnet build $projectPath --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $sourceAssembly)) {
    throw "Build output not found: $sourceAssembly"
}
if (-not (Test-Path -LiteralPath $sourceAbout)) {
    throw "About.xml not found: $sourceAbout"
}

$targetAbout = Join-Path $GameModPath "About\About.xml"
if (Test-Path -LiteralPath $GameModPath) {
    if (-not (Test-Path -LiteralPath $targetAbout)) {
        $existingEntries = @(Get-ChildItem -LiteralPath $GameModPath -Force)
        if ($existingEntries.Count -gt 0) {
            throw "Refusing to deploy into a non-empty directory without About.xml: $GameModPath"
        }
    }
    else {
        $aboutText = Get-Content -LiteralPath $targetAbout -Raw
        if ($aboutText -notmatch "<packageId>\s*Nanaloveyuki\.SaveYourStorageAndTickEveryMinute\s*</packageId>") {
            throw "Target directory is not the expected Mod: $GameModPath"
        }
    }
}
else {
    $modsDirectory = Split-Path -Parent $GameModPath
    if (-not (Test-Path -LiteralPath $modsDirectory)) {
        throw "RimWorld Mods directory not found: $modsDirectory"
    }
}

New-Item -ItemType Directory -Force -Path `
    (Split-Path -Parent $targetAbout), `
    $targetAssemblyDirectory, `
    $targetLanguageDirectory | Out-Null
Copy-Item -LiteralPath $sourceAbout -Destination $targetAbout -Force
Copy-Item -LiteralPath $sourceAnalyzer -Destination (Join-Path $GameModPath "Analyzer.xml") -Force
$targetAssembly = Join-Path $targetAssemblyDirectory "SaveYourStorageAndTickEveryMinute.dll"
Copy-Item -LiteralPath $sourceAssembly -Destination $targetAssembly -Force

$sourcePdb = Join-Path $repoRoot "1.6\Assemblies\SaveYourStorageAndTickEveryMinute.pdb"
if (Test-Path -LiteralPath $sourcePdb) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $targetAssemblyDirectory "SaveYourStorageAndTickEveryMinute.pdb") -Force
}

$sourceHash = (Get-FileHash -LiteralPath $sourceAssembly -Algorithm SHA256).Hash
$targetHash = (Get-FileHash -LiteralPath $targetAssembly -Algorithm SHA256).Hash
if ($sourceHash -ne $targetHash) {
    throw "Assembly verification failed: $targetAssembly"
}

$languageFiles = @(Get-ChildItem -LiteralPath $sourceLanguages -File -Recurse)
foreach ($file in $languageFiles) {
    $relativePath = $file.FullName.Substring($sourceLanguages.Length).TrimStart([char[]]"\/")
    $destination = Join-Path $targetLanguageDirectory $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force

    $sourceLanguageHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $targetLanguageHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($sourceLanguageHash -ne $targetLanguageHash) {
        throw "Language file verification failed: $destination"
    }
}

Write-Host "Deployed $Configuration build to: $GameModPath"
