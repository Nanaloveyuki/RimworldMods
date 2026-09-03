[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$GameModPath = "D:\Appdata\Steam\steamapps\common\RimWorld\Mods\OhYouAreSoFast",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "Source\OhYouAreSoFast.csproj"
$sourceAssembly = Join-Path $repoRoot "1.6\Assemblies\OhYouAreSoFast.dll"
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

if (-not (Test-Path -LiteralPath $GameModPath)) {
    throw "Game Mod directory not found: $GameModPath"
}

$targetAbout = Join-Path $GameModPath "About\About.xml"
if (-not (Test-Path -LiteralPath $targetAbout)) {
    throw "Target About.xml not found: $targetAbout"
}

$aboutText = Get-Content -LiteralPath $targetAbout -Raw
if ($aboutText -notmatch "<packageId>\s*Nanaloveyuki\.OhYouAreSoFast\s*</packageId>") {
    throw "Target directory is not the OhYouAreSoFast Mod: $GameModPath"
}

if (-not (Test-Path -LiteralPath $sourceLanguages)) {
    throw "Language directory not found: $sourceLanguages"
}

New-Item -ItemType Directory -Force -Path $targetAssemblyDirectory, $targetLanguageDirectory | Out-Null

Copy-Item -LiteralPath $sourceAssembly -Destination (Join-Path $targetAssemblyDirectory "OhYouAreSoFast.dll") -Force

$sourcePdb = Join-Path $repoRoot "1.6\Assemblies\OhYouAreSoFast.pdb"
if (Test-Path -LiteralPath $sourcePdb) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $targetAssemblyDirectory "OhYouAreSoFast.pdb") -Force
}

$languageFiles = @(Get-ChildItem -LiteralPath $sourceLanguages -File -Recurse)
if ($languageFiles.Count -eq 0) {
    throw "No language files found under: $sourceLanguages"
}

foreach ($file in $languageFiles) {
    $relativePath = $file.FullName.Substring($sourceLanguages.Length).TrimStart([char[]]"\/")
    $destination = Join-Path $targetLanguageDirectory $relativePath
    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}

$sourceHash = (Get-FileHash -LiteralPath $sourceAssembly -Algorithm SHA256).Hash
$targetAssembly = Join-Path $targetAssemblyDirectory "OhYouAreSoFast.dll"
$targetHash = (Get-FileHash -LiteralPath $targetAssembly -Algorithm SHA256).Hash
if ($sourceHash -ne $targetHash) {
    throw "Assembly verification failed: $targetAssembly"
}

foreach ($file in $languageFiles) {
    $relativePath = $file.FullName.Substring($sourceLanguages.Length).TrimStart([char[]]"\/")
    $destination = Join-Path $targetLanguageDirectory $relativePath
    if (-not (Test-Path -LiteralPath $destination)) {
        throw "Language file verification failed: $destination"
    }

    $sourceLanguageHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $targetLanguageHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($sourceLanguageHash -ne $targetLanguageHash) {
        throw "Language file verification failed: $destination"
    }
}

Write-Host "Deployed OhYouAreSoFast $Configuration build to: $GameModPath"
Write-Host "Synchronized $($languageFiles.Count) language file(s)."
