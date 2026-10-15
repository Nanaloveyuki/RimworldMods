[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$GameModsRoot = "D:\Appdata\Steam\steamapps\common\RimWorld\Mods",

    [string]$GameModPath,

    [string]$RimWorldManagedDir = "D:\Appdata\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed",

    [string]$HarmonyAssembliesDir = "D:\Appdata\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$expectedPackageId = "nanaloveyuki.mouse.disaster.famine.continued"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishedFileIdPath = Join-Path $repoRoot "About\PublishedFileId.txt"
if (-not (Test-Path -LiteralPath $publishedFileIdPath -PathType Leaf) -or
    [string]::IsNullOrWhiteSpace((Get-Content -LiteralPath $publishedFileIdPath -Raw))) {
    throw "Required Workshop ID file is missing or empty: $publishedFileIdPath"
}
$projectPaths = @(
    (Join-Path $repoRoot "1.6\Source\MouseDisasterYear.csproj"),
    (Join-Path $repoRoot "Guard\Source\MouseDisasterContinuedGuard.csproj"),
    (Join-Path $repoRoot "Compat\Optimized\AL_MouseDisaster.csproj")
)
$sourceAssemblies = @(
    (Join-Path $repoRoot "1.6\Assemblies\MouseDisaster.dll"),
    (Join-Path $repoRoot "Guard\Assemblies\MouseDisasterContinuedGuard.dll"),
    (Join-Path $repoRoot "1.6\Assemblies\AL_MouseDisaster.dll")
)
$modsRootFull = [System.IO.Path]::GetFullPath($GameModsRoot).TrimEnd([char[]]"\/")
if ([string]::IsNullOrWhiteSpace($GameModPath)) {
    $GameModPath = Join-Path $modsRootFull "RatkinGreatFamineYearContinued"
}
$targetFull = [System.IO.Path]::GetFullPath($GameModPath).TrimEnd([char[]]"\/")
$targetParent = [System.IO.DirectoryInfo]::new($targetFull).Parent.FullName.TrimEnd([char[]]"\/")

if (-not [string]::Equals($targetParent, $modsRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Deployment target must be a direct child of the RimWorld Mods directory: $modsRootFull"
}

if (Get-Process -Name "RimWorldWin64" -ErrorAction SilentlyContinue) {
    throw "RimWorld is running. Exit the game before deploying."
}

foreach ($projectPath in $projectPaths) {
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Project file not found: $projectPath"
    }
}
if (-not (Test-Path -LiteralPath $RimWorldManagedDir -PathType Container)) {
    throw "RimWorld Managed directory not found: $RimWorldManagedDir"
}
if (-not (Test-Path -LiteralPath (Join-Path $HarmonyAssembliesDir "0Harmony.dll") -PathType Leaf)) {
    throw "Harmony assembly not found under: $HarmonyAssembliesDir"
}

if (Test-Path -LiteralPath $targetFull) {
    $targetItem = Get-Item -LiteralPath $targetFull -Force
    if (($targetItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to deploy through a filesystem reparse point: $targetFull"
    }

    $targetAbout = Join-Path $targetFull "About\About.xml"
    $targetEntries = @(Get-ChildItem -LiteralPath $targetFull -Force)
    if ($targetEntries.Count -gt 0 -and -not (Test-Path -LiteralPath $targetAbout -PathType Leaf)) {
        throw "Non-empty deployment target has no About.xml: $targetFull"
    }
    if (Test-Path -LiteralPath $targetAbout -PathType Leaf) {
        [xml]$targetMetadata = Get-Content -LiteralPath $targetAbout -Raw
        if ($targetMetadata.ModMetaData.packageId -ne $expectedPackageId) {
            throw "Target directory belongs to another Mod: $targetFull"
        }
    }
}

if (-not $SkipBuild) {
    Write-Host "Building $Configuration..."
    foreach ($projectPath in $projectPaths) {
        $buildArguments = @(
            "build",
            $projectPath,
            "--configuration", $Configuration,
            "--nologo",
            "-p:RimWorldManagedDir=$RimWorldManagedDir",
            "-p:HarmonyAssembliesDir=$HarmonyAssembliesDir"
        )
        & dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for $projectPath with exit code $LASTEXITCODE."
        }
    }
}

foreach ($sourceAssembly in $sourceAssemblies) {
    if (-not (Test-Path -LiteralPath $sourceAssembly -PathType Leaf)) {
        throw "Build output not found: $sourceAssembly"
    }
}

New-Item -ItemType Directory -Force -Path $modsRootFull, $targetFull | Out-Null

$managedDirectories = @("About", "Defs", "Languages", "Patches", "Guard", "1.6")
foreach ($relativePath in $managedDirectories) {
    $path = Join-Path $targetFull $relativePath
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
foreach ($relativePath in @("LoadFolders.xml", "NOTICE", "README.md")) {
    $path = Join-Path $targetFull $relativePath
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

$sourceFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
foreach ($relativeDirectory in @("About", "Defs", "Languages", "Patches", "Textures")) {
    $sourceDirectory = Join-Path $repoRoot $relativeDirectory
    if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) {
        throw "Required content directory not found: $sourceDirectory"
    }
    foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -File -Recurse) {
        $sourceFiles.Add($file)
    }
}
foreach ($relativeDirectory in @("Guard\Assemblies", "Guard\Languages")) {
    $sourceDirectory = Join-Path $repoRoot $relativeDirectory
    if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) {
        throw "Required content directory not found: $sourceDirectory"
    }
    foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -File -Recurse) {
        $sourceFiles.Add($file)
    }
}
foreach ($relativeFile in @("LoadFolders.xml", "NOTICE", "README.md", "1.6\Assemblies\MouseDisaster.dll", "1.6\Assemblies\AL_MouseDisaster.dll")) {
    $filePath = Join-Path $repoRoot $relativeFile
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Required deployment file not found: $filePath"
    }
    $sourceFiles.Add((Get-Item -LiteralPath $filePath))
}
$sourcePdb = Join-Path $repoRoot "1.6\Assemblies\MouseDisaster.pdb"
if (Test-Path -LiteralPath $sourcePdb -PathType Leaf) {
    $sourceFiles.Add((Get-Item -LiteralPath $sourcePdb))
}

foreach ($file in $sourceFiles) {
    $relativePath = $file.FullName.Substring($repoRoot.Length).TrimStart([char[]]"\/")
    $destination = Join-Path $targetFull $relativePath
    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}

[xml]$deployedMetadata = Get-Content -LiteralPath (Join-Path $targetFull "About\About.xml") -Raw
if ($deployedMetadata.ModMetaData.packageId -ne $expectedPackageId) {
    throw "Deployed About.xml has an unexpected packageId."
}

foreach ($file in $sourceFiles) {
    $relativePath = $file.FullName.Substring($repoRoot.Length).TrimStart([char[]]"\/")
    $destination = Join-Path $targetFull $relativePath
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
        throw "Deployment verification failed, file missing: $destination"
    }

    $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($sourceHash -ne $targetHash) {
        throw "Deployment verification failed, hash mismatch: $destination"
    }
}

Write-Host "Deployed MouseDisaster $Configuration build to: $targetFull"
Write-Host "Verified $($sourceFiles.Count) file(s) with SHA-256."
Write-Host "Workshop ID verified: $((Get-Content -LiteralPath $publishedFileIdPath -Raw).Trim())"
