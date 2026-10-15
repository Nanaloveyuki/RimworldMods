[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$GameModPath,
    [string]$RimWorldManagedDir,
    [switch]$SkipGpu,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repoRoot 'Source/IrisMenus.csproj'
if (-not $RimWorldManagedDir) {
    $RimWorldManagedDir = if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        'E:\Apps\Steam\steamapps\common\RimWorld'
    } else {
        '/mnt/e/Apps/Steam/steamapps/common/RimWorld'
    }
}
if (-not $SkipBuild) {
    & dotnet build $project --configuration $Configuration -p:RimWorldVersion=1.7 "-p:RimWorldManagedDir=$RimWorldManagedDir" --nologo
    if ($LASTEXITCODE -ne 0) { throw "RimWorld 1.7 build failed: $LASTEXITCODE" }
}
if (-not $SkipGpu) { Write-Host 'Native RimWorld 1.7 GPU/UI checks require the actual game; no Unity/1.6 artifacts are rebuilt.' }

$files = @(
    'About/About.xml', 'LoadFolders.xml', 'About/Preview.png', 'About/ModIcon.png',
    'README.md', 'LICENSE', 'guide.md', 'guide_agents.md',
    '1.7/Assemblies/IrisMenus.dll', '1.7/Rendering/Frost17.frag.glsl'
)
foreach ($directory in @('Common/Defs', 'Common/Languages', '1.7/Rendering', 'docs')) {
    $sourceDirectory = Join-Path $repoRoot $directory
    if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) {
        throw "Source directory not found: $sourceDirectory"
    }
    $files += Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File |
        ForEach-Object { $_.FullName.Substring($repoRoot.Length + 1).Replace('\', '/') }
}
$files = @($files | Sort-Object -Unique)
foreach ($relative in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relative) -PathType Leaf)) {
        throw "Source file not found: $relative"
    }
}
if (-not $GameModPath) {
    Write-Host 'Built RimWorld 1.7: 1.7/Assemblies/IrisMenus.dll; existing 1.6 files preserved.'
    return
}

if (Get-Process -Name RimWorldWin64,RimWorldWin64Steam,RimWorldWin,RimWorld,RimWorldLinux -ErrorAction SilentlyContinue) {
    throw 'Exit RimWorld before deploying.'
}
$target = [IO.Path]::GetFullPath($GameModPath)
if ($target.TrimEnd('\', '/') -eq $repoRoot.TrimEnd('\', '/')) {
    throw 'Deployment target must not be the source repository.'
}
[xml]$sourceMetadata = Get-Content -LiteralPath (Join-Path $repoRoot 'About/About.xml') -Raw
if ($sourceMetadata.ModMetaData.packageId -ne 'Nanaloveyuki.IrisMenus') { throw 'Unexpected source package identity.' }
$targetAbout = Join-Path $target 'About/About.xml'
if (Test-Path -LiteralPath $target) {
    if (-not (Test-Path -LiteralPath $target -PathType Container)) { throw "Target is not a directory: $target" }
    if (Test-Path -LiteralPath $targetAbout) {
        [xml]$metadata = Get-Content -LiteralPath $targetAbout -Raw
        if ($metadata.ModMetaData.packageId -ne 'Nanaloveyuki.IrisMenus') { throw "Target is not IrisMenus: $target" }
    } elseif (Get-ChildItem -LiteralPath $target -Force) {
        throw "Non-empty target has no About.xml: $target"
    }
}
# Inspect all existing destination ancestors before copying.
foreach ($relative in $files) {
    $destination = Join-Path $target $relative
    while ($destination) {
        if (Test-Path -LiteralPath $destination) {
            $item = Get-Item -LiteralPath $destination -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing a symlink/reparse destination: $destination" }
        }
        $destination = Split-Path -Parent $destination
    }
}
foreach ($relative in $files) {
    $source = Join-Path $repoRoot $relative
    $destination = Join-Path $target $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
        throw "Deployment verification failed: $destination"
    }
}
Write-Host "Deployed and verified $($files.Count) files: $target"
