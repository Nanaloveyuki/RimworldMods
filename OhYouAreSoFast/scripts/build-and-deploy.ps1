[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$RimWorldDir = "E:\Apps\Steam\steamapps\common\RimWorld",
    [string]$HarmonyAssemblyPath = "E:\Apps\Steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll",
    [string]$GameModPath,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
if (-not $GameModPath) {
    $GameModPath = Join-Path $RimWorldDir "Mods\OhYouAreSoFast"
}
$repoRoot = (Get-Item -LiteralPath (Join-Path $PSScriptRoot "..")).FullName.TrimEnd([char[]]"\/")
$projectPath = Join-Path $repoRoot "Source\OhYouAreSoFast.csproj"
$sourceAssembly = Join-Path $repoRoot "1.7\Assemblies\OhYouAreSoFast.dll"

if (-not $SkipBuild) {
    & dotnet build $projectPath --configuration $Configuration --nologo "-p:RimWorldDir=$RimWorldDir" "-p:HarmonyAssemblyPath=$HarmonyAssemblyPath"
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}
if (-not (Test-Path -LiteralPath $sourceAssembly)) {
    throw "Build output not found: $sourceAssembly"
}
if (-not (Test-Path -LiteralPath $GameModPath -PathType Container)) {
    throw "Game Mod directory not found: $GameModPath"
}
$targetAbout = Join-Path $GameModPath "About\About.xml"
if (-not (Test-Path -LiteralPath $targetAbout)) {
    throw "Target About.xml not found: $targetAbout"
}
if ((Get-Content -LiteralPath $targetAbout -Raw) -notmatch "<packageId>\s*Nanaloveyuki\.OhYouAreSoFast\s*</packageId>") {
    throw "Target directory is not the OhYouAreSoFast Mod: $GameModPath"
}

# Explicit allowlist: never copy or delete installed 1.6 content or Workshop IDs.
$files = @(
    Get-Item -LiteralPath (Join-Path $repoRoot "About\About.xml")
    Get-Item -LiteralPath (Join-Path $repoRoot "LoadFolders.xml")
    Get-Item -LiteralPath (Join-Path $repoRoot "LICENSE")
    Get-Item -LiteralPath $sourceAssembly
    Get-ChildItem -LiteralPath (Join-Path $repoRoot "Common") -File -Recurse
)
$sourcePdb = Join-Path $repoRoot "1.7\Assemblies\OhYouAreSoFast.pdb"
if (Test-Path -LiteralPath $sourcePdb) {
    $files += Get-Item -LiteralPath $sourcePdb
}
foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($repoRoot.Length).TrimStart([char[]]"\/")
    $destination = Join-Path $GameModPath $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) {
        throw "Deployment verification failed: $destination"
    }
}
Write-Host "Deployed OhYouAreSoFast 1.7 ($Configuration) to: $GameModPath"
Write-Host "Installed 1.6 content and PublishedFileId were left untouched."
