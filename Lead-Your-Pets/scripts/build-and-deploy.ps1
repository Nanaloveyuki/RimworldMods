[CmdletBinding()]
param(
    [string]$RimWorldDir = $(if ($env:RIMWORLD_DIR) { $env:RIMWORLD_DIR } else { 'E:\Apps\Steam\steamapps\common\RimWorld' }),
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($project in @('Source/LeadYourPet.csproj', 'Guard/Source/LeadYourPetContinuedGuard.csproj')) {
    & dotnet build (Join-Path $root $project) -c $Configuration "-p:RimWorldDir=$RimWorldDir" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
if ($BuildOnly) { return }
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Exit RimWorld before deploying.' }
$target = Join-Path $RimWorldDir 'Mods/LeadYourPetsContinued'
if (Test-Path $target) {
    if ((Get-Item $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Target is a reparse point.' }
    $about = Join-Path $target 'About/About.xml'
    if (!(Test-Path $about)) { throw 'Existing target has no mod identity.' }
    if (([xml](Get-Content $about -Raw)).ModMetaData.packageId -ne 'nanaloveyuki.leadyourpet.continued') { throw 'Target belongs to another mod.' }
}
$files = @(
    foreach ($folder in @('About','Defs','Languages','Guard/Languages')) {
        $path = Join-Path $root $folder
        if (Test-Path $path) { Get-ChildItem $path -File -Recurse }
    }
    foreach ($file in @('Assemblies/LeadYourPet.dll','Guard/Assemblies/LeadYourPetContinuedGuard.dll','LoadFolders.xml','NOTICE','README.md','LICENSE')) {
        $path = Join-Path $root $file
        if (Test-Path $path) { Get-Item $path }
    }
)
$prefix = $root.TrimEnd('\','/')
foreach ($file in $files) {
    $full = $file.FullName
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "File is outside root: $full" }
    $relative = $full.Substring($prefix.Length).TrimStart('\','/')
    $destination = Join-Path $target $relative
    New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
    Copy-Item -LiteralPath $full -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $full).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Hash mismatch: $relative" }
}
foreach ($folder in @('About','Defs','Languages','Guard/Languages')) {
    $live = Join-Path $target $folder
    if (!(Test-Path -LiteralPath $live)) { continue }
    foreach ($deployed in (Get-ChildItem -LiteralPath $live -File -Recurse)) {
        $relative = $deployed.FullName.Substring($target.TrimEnd('\','/').Length).TrimStart('\','/')
        if (!(Test-Path -LiteralPath (Join-Path $root $relative))) { Remove-Item -LiteralPath $deployed.FullName -Force }
    }
}
Write-Host "Deployed and SHA-256 verified $($files.Count) files: $target"
