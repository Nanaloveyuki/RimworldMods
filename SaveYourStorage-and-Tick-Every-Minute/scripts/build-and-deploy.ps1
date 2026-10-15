[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$RimWorldDir = "E:\Apps\Steam\steamapps\common\RimWorld",
    [string]$HarmonyAssemblyPath = "E:\Apps\Steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll",
    [string]$GameModPath,
    [string]$DotNet = "dotnet",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item -LiteralPath (Join-Path $PSScriptRoot "..")).FullName.TrimEnd([char[]]"\/")
if (-not $GameModPath) {
    $GameModPath = Join-Path $RimWorldDir "Mods\SaveYourStorageAndTickEveryMinute"
}
$sourceAssembly = Join-Path $repoRoot "1.7\Assemblies\SaveYourStorageAndTickEveryMinute.dll"
if (-not $SkipBuild) {
    & $DotNet build (Join-Path $repoRoot "Source\SaveYourStorageAndTickEveryMinute.csproj") `
        --configuration $Configuration --nologo "-p:RimWorldDir=$RimWorldDir" "-p:HarmonyAssemblyPath=$HarmonyAssemblyPath"
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}
if (-not (Test-Path -LiteralPath $sourceAssembly -PathType Leaf)) {
    throw "Build output not found: $sourceAssembly"
}
$targetAbout = Join-Path $GameModPath "About\About.xml"
if (Test-Path -LiteralPath $targetAbout) {
    [xml]$about = Get-Content -LiteralPath $targetAbout -Raw
    if ($about.ModMetaData.packageId -ne "Nanaloveyuki.SaveYourStorageAndTickEveryMinute") {
        throw "Target directory is not the expected Mod: $GameModPath"
    }
}
elseif ((Test-Path -LiteralPath $GameModPath) -and @(Get-ChildItem -LiteralPath $GameModPath -Force).Count -gt 0) {
    throw "Refusing to deploy into a non-empty directory without About.xml: $GameModPath"
}
elseif (-not (Test-Path -LiteralPath (Split-Path -Parent $GameModPath))) {
    throw "RimWorld Mods directory not found: $(Split-Path -Parent $GameModPath)"
}

# Copy only 1.7 and shared metadata. Never traverse installed 1.6 or PublishedFileId.txt.
$files = @("About\About.xml", "Analyzer.xml", "LoadFolders.xml", "LICENSE")
$preview = Join-Path $repoRoot "About\Preview.png"
if (Test-Path -LiteralPath $preview) { $files += "About\Preview.png" }
$files += @(Get-ChildItem -LiteralPath (Join-Path $repoRoot "1.7") -File -Recurse |
    Where-Object { $_.Extension -in @(".dll", ".pdb", ".xml") } |
    ForEach-Object { $_.FullName.Substring($repoRoot.Length).TrimStart([char[]]"\/") })
foreach ($relativePath in $files) {
    $source = Join-Path $repoRoot $relativePath
    $target = Join-Path $GameModPath $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
        throw "Deployment verification failed: $target"
    }
}
Write-Host "Deployed RimWorld 1.7 to: $GameModPath (existing 1.6 and workshop metadata preserved)"
