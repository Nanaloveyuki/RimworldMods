[CmdletBinding()]
param(
    [string]$RimWorldDir = $(if ($env:RIMWORLD_DIR) { $env:RIMWORLD_DIR } elseif ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { 'E:\Apps\Steam\steamapps\common\RimWorld' } else { '/mnt/e/Apps/Steam/steamapps/common/RimWorld' }),
    [ValidateSet('1.6', '1.7')][string]$RimWorldVersion = $(if ($env:RIMWORLD_VERSION) { $env:RIMWORLD_VERSION } else { '1.7' }),
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = if ($RimWorldVersion -eq '1.7') { '1.7/' } else { '' }
function Assert-GameStopped {
    if (Get-Process RimWorld, RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Exit RimWorld (RimWorld.exe or RimWorldWin64.exe) before deploying.' }
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -and (Test-Path -LiteralPath '/proc/sys/kernel/osrelease') -and (Get-Content -LiteralPath '/proc/sys/kernel/osrelease' -Raw) -match 'microsoft') {
        if (!(Get-Command powershell.exe -ErrorAction SilentlyContinue)) { throw 'powershell.exe is required to check Windows game processes from WSL before deployment.' }
        $count = & powershell.exe -NoProfile -Command '@(Get-Process RimWorld, RimWorldWin64 -ErrorAction SilentlyContinue).Count'
        if ($LASTEXITCODE -ne 0 -or ($count -join '').Trim() -ne '0') { throw 'Exit RimWorld (RimWorld.exe or RimWorldWin64.exe) before deploying.' }
    }
}
if (!$BuildOnly) { Assert-GameStopped }
foreach ($project in @('Source/LeadYourPet.csproj', 'Guard/Source/LeadYourPetContinuedGuard.csproj')) {
    & dotnet build (Join-Path $root $project) -c $Configuration "-p:RimWorldDir=$RimWorldDir" "-p:RimWorldVersion=$RimWorldVersion" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project (RimWorld $RimWorldVersion)" }
}
if ($BuildOnly) { return }
Assert-GameStopped
$target = Join-Path $RimWorldDir 'Mods/LeadYourPetsContinued'
if (Test-Path -LiteralPath $target) {
    if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Target is a reparse point.' }
    $about = Join-Path $target 'About/About.xml'
    if (!(Test-Path -LiteralPath $about)) { throw 'Existing target has no mod identity.' }
    if (([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData.packageId -ne 'nanaloveyuki.leadyourpet.continued') { throw 'Target belongs to another mod.' }
}
# A 1.7 deployment upgrades only its runtime and shared metadata. Legacy files stay untouched.
$folders = @('About', "${runtime}Defs", "${runtime}Languages", "${runtime}Guard/Languages")
$assemblies = @("${runtime}Assemblies/LeadYourPet.dll", "${runtime}Guard/Assemblies/LeadYourPetContinuedGuard.dll")
foreach ($assembly in $assemblies) {
    if (!(Test-Path -LiteralPath (Join-Path $root $assembly) -PathType Leaf)) { throw "Missing runtime assembly: $assembly" }
}
$files = @(
    foreach ($folder in $folders) {
        $path = Join-Path $root $folder
        if (Test-Path -LiteralPath $path) { Get-ChildItem -LiteralPath $path -File -Recurse }
    }
    foreach ($file in ($assemblies + @('LoadFolders.xml','NOTICE','README.md','LICENSE'))) {
        $path = Join-Path $root $file
        if (Test-Path -LiteralPath $path) { Get-Item -LiteralPath $path }
    }
)
$prefix = $root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
function Assert-DestinationPath([string]$path) {
    for ($parent = $path; $parent; $parent = Split-Path $parent -Parent) {
        if ((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Destination is a reparse point: $parent" }
        if ($parent -eq $target) { return }
    }
    throw "Destination is outside target: $path"
}
foreach ($file in $files) {
    $full = $file.FullName
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "File is outside root: $full" }
    $relative = $full.Substring($prefix.Length)
    $destination = Join-Path $target $relative
    Assert-DestinationPath $destination
    New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
    Copy-Item -LiteralPath $full -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) { throw "Hash mismatch: $relative" }
}
foreach ($folder in $folders) {
    $live = Join-Path $target $folder
    if (!(Test-Path -LiteralPath $live)) { continue }
    Assert-DestinationPath $live
    foreach ($deployed in (Get-ChildItem -LiteralPath $live -File -Recurse)) {
        Assert-DestinationPath $deployed.FullName
        $relative = $deployed.FullName.Substring($target.TrimEnd('\','/').Length).TrimStart('\','/')
        if (!(Test-Path -LiteralPath (Join-Path $root $relative))) { Remove-Item -LiteralPath $deployed.FullName -Force }
    }
}
Write-Host "Deployed RimWorld $RimWorldVersion runtime and SHA-256 verified $($files.Count) files: $target"
