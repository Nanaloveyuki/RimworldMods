[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$RimWorldDir,
    [string]$HarmonyAssemblyPath,
    [Alias("build-only", "-build-only")]
    [switch]$BuildOnly
)

$ErrorActionPreference = "Stop"
$expectedPackageId = "nanaloveyuki.washingmachine"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$isWindowsHost = [System.IO.Path]::DirectorySeparatorChar -eq '\'
if ([string]::IsNullOrWhiteSpace($RimWorldDir)) {
    if ($isWindowsHost) { $RimWorldDir = "E:\Apps\Steam\steamapps\common\RimWorld" }
    else { $RimWorldDir = "/mnt/e/Apps/Steam/steamapps/common/RimWorld" }
}
$gameRoot = [System.IO.Path]::GetFullPath($RimWorldDir)
$target = Join-Path $gameRoot "Mods/WashingMachine"
$project = Join-Path $repoRoot "WashingMachine.csproj"
$assembly = Join-Path $repoRoot "tmp/build/WashingMachine.dll"
$comparison = [System.StringComparison]::Ordinal
if ($isWindowsHost) { $comparison = [System.StringComparison]::OrdinalIgnoreCase }

function Assert-NoLinks([string]$Path) {
    $current = [System.IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrEmpty($current)) {
        # Get-Item, rather than Resolve-Path, preserves symlink/reparse identity.
        $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw "Refusing filesystem link/reparse point: $current"
        }
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ($parent -eq $current) { break }
        $current = $parent
    }
}

function Assert-Identity([string]$About) {
    Assert-NoLinks $About
    try { [xml]$metadata = Get-Content -LiteralPath $About -Raw -Encoding UTF8 }
    catch { throw "Cannot read mod identity: $About ($_)" }
    if ($null -eq $metadata.ModMetaData -or $metadata.ModMetaData.packageId.Trim().ToLowerInvariant() -ne $expectedPackageId) {
        throw "Unexpected mod identity: $About"
    }
}

function Get-SafeFiles([string]$Folder) {
    Assert-NoLinks $Folder
    if (-not (Test-Path -LiteralPath $Folder -PathType Container)) {
        throw "Required directory not found: $Folder"
    }
    foreach ($item in @(Get-ChildItem -LiteralPath $Folder -Force)) {
        Assert-NoLinks $item.FullName
        if ($item.PSIsContainer) { Get-SafeFiles $item.FullName }
        else { $item }
    }
}

function Assert-GameStopped {
    if (Get-Process -Name @("RimWorldWin64", "RimWorldWin64Steam", "RimWorldWin", "RimWorld", "RimWorldLinux") -ErrorAction SilentlyContinue) {
        throw "Exit RimWorld before deploying."
    }
    if (-not $isWindowsHost -and ($env:WSL_INTEROP -or $env:WSL_DISTRO_NAME)) {
        $hostPowerShell = Get-Command powershell.exe -ErrorAction SilentlyContinue
        if ($null -eq $hostPowerShell) {
            throw "Cannot check Windows RimWorld processes: powershell.exe is unavailable in WSL."
        }
        $hostCount = & $hostPowerShell.Source -NoProfile -NonInteractive -Command '$ErrorActionPreference = "Stop"; @(Get-Process -Name RimWorldWin64,RimWorldWin64Steam,RimWorldWin,RimWorld -ErrorAction SilentlyContinue).Count'
        if ($LASTEXITCODE -ne 0 -or ($hostCount -join "").Trim() -ne "0") {
            throw "Exit RimWorld before deploying, or repair the Windows process query."
        }
    }
}

Assert-NoLinks $repoRoot
Assert-NoLinks $gameRoot
Assert-NoLinks $project
Assert-Identity (Join-Path $repoRoot "About/About.xml")
$managed = Join-Path $gameRoot "RimWorldWin64_Data/Managed"
if (-not (Test-Path -LiteralPath (Join-Path $managed "Assembly-CSharp.dll") -PathType Leaf)) {
    $managed = Join-Path $gameRoot "RimWorldLinux_Data/Managed"
}
if (-not (Test-Path -LiteralPath (Join-Path $managed "Assembly-CSharp.dll") -PathType Leaf)) {
    throw "RimWorld managed assemblies not found: $gameRoot"
}
$buildArguments = @("build", $project, "--configuration", $Configuration, "-p:RimWorldDir=$gameRoot", "-p:RimWorldManagedDir=$managed", "--nologo")
if (-not [string]::IsNullOrWhiteSpace($HarmonyAssemblyPath)) {
    $buildArguments += "-p:HarmonyAssemblyPath=$HarmonyAssemblyPath"
}
if (-not $BuildOnly) { Assert-GameStopped }

& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
Assert-NoLinks $assembly
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Build output not found: $assembly" }
if ($BuildOnly) { return }

$modsRoot = Join-Path $gameRoot "Mods"
Assert-NoLinks $target
if (-not (Test-Path -LiteralPath $modsRoot -PathType Container)) { throw "RimWorld Mods directory not found: $modsRoot" }
$sourcePrefix = $repoRoot.TrimEnd([char[]]"\/") + [System.IO.Path]::DirectorySeparatorChar
$targetPrefix = $target.TrimEnd([char[]]"\/") + [System.IO.Path]::DirectorySeparatorChar
if ([string]::Equals($repoRoot, $target, $comparison) -or $target.StartsWith($sourcePrefix, $comparison) -or $repoRoot.StartsWith($targetPrefix, $comparison)) {
    throw "Deployment target and source must not contain each other."
}
if (Test-Path -LiteralPath $target) {
    if (-not (Test-Path -LiteralPath $target -PathType Container)) { throw "Deployment target is not a directory: $target" }
    # Check untouched descendants too, without traversing links.
    $null = @(Get-SafeFiles $target)
    $targetAbout = Join-Path $target "About/About.xml"
    if (Test-Path -LiteralPath $targetAbout -PathType Leaf) { Assert-Identity $targetAbout }
    elseif (@(Get-ChildItem -LiteralPath $target -Force).Count -gt 0) {
        throw "Non-empty target has no matching About.xml: $target"
    }
}

$manifest = [System.Collections.Generic.List[object]]::new()
$manifest.Add([pscustomobject]@{ Source = $assembly; Relative = "Assemblies/WashingMachine.dll" })
foreach ($folder in @("About", "Defs", "Languages", "Textures")) {
    foreach ($file in @(Get-SafeFiles (Join-Path $repoRoot $folder))) {
        $manifest.Add([pscustomobject]@{
            Source = $file.FullName
            Relative = $file.FullName.Substring($sourcePrefix.Length)
        })
    }
}
foreach ($name in @("README.md", "LICENSE")) {
    $manifest.Add([pscustomobject]@{ Source = (Join-Path $repoRoot $name); Relative = $name })
}

# Validate the entire release manifest and hash sources before the first write.
foreach ($entry in $manifest) {
    Assert-NoLinks $entry.Source
    if (-not (Test-Path -LiteralPath $entry.Source -PathType Leaf)) { throw "Required source file not found: $($entry.Source)" }
    $destination = Join-Path $target $entry.Relative
    Assert-NoLinks $destination
    if ((Test-Path -LiteralPath $destination) -and -not (Test-Path -LiteralPath $destination -PathType Leaf)) {
        throw "Destination is not a file: $destination"
    }
    $parent = [System.IO.Path]::GetDirectoryName($destination)
    while (-not [string]::IsNullOrEmpty($parent)) {
        if ((Test-Path -LiteralPath $parent) -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
            throw "Destination ancestor is not a directory: $parent"
        }
        $parent = [System.IO.Path]::GetDirectoryName($parent)
    }
    $entry | Add-Member -NotePropertyName Hash -NotePropertyValue (Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash
}
Assert-GameStopped
foreach ($entry in $manifest) {
    $destination = Join-Path $target $entry.Relative
    Assert-NoLinks $destination
    New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $entry.Source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $entry.Hash) {
        throw "SHA-256 mismatch: $($entry.Relative)"
    }
}
Write-Host "Deployed and SHA-256 verified $($manifest.Count) files: $target"
Write-Host "Unrelated files and ModsConfig.xml were not changed."
