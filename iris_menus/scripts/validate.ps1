[CmdletBinding()]
param([string]$RimWorldManagedDir)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$consumer = Join-Path $root 'tests/ApiConsumer/ApiConsumer.csproj'
$version17 = @('-p:RimWorldVersion=1.7')
if ($RimWorldManagedDir) { $version17 += "-p:RimWorldManagedDir=$RimWorldManagedDir" }
& dotnet build $consumer -c Release @version17 --nologo
if ($LASTEXITCODE -ne 0) { throw 'RimWorld 1.7 API consumer build failed.' }
& dotnet run --project (Join-Path $root 'tests/RuntimeChecks/RuntimeChecks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Runtime checks failed.' }
foreach ($directory in @('About', 'Common/Defs', 'Common/Languages')) {
    foreach ($file in Get-ChildItem (Join-Path $root $directory) -Recurse -Filter *.xml) {
        [xml]$document = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    }
}
[xml]$loadFolders = Get-Content (Join-Path $root 'LoadFolders.xml') -Raw -Encoding UTF8
[xml]$about = Get-Content (Join-Path $root 'About/About.xml') -Raw -Encoding UTF8
foreach ($version in @('1.6', '1.7')) {
    if ($version -notin @($about.ModMetaData.supportedVersions.li)) { throw "Missing supported version: $version" }
    $folders = @($loadFolders.SelectNodes("/loadFolders/v$version/li") | ForEach-Object InnerText)
    $expectedFolders = if ($version -eq '1.6') { '1.6' } else { '1.7,Common' }
    if (($folders -join ',') -ne $expectedFolders) { throw "Unsafe load folders for $version" }
}
[xml]$en = Get-Content (Join-Path $root 'Common/Languages/English/Keyed/IrisMenus.xml') -Raw -Encoding UTF8
[xml]$zh = Get-Content (Join-Path $root 'Common/Languages/ChineseSimplified/Keyed/IrisMenus.xml') -Raw -Encoding UTF8
$english = @($en.LanguageData.ChildNodes | ForEach-Object Name)
$chinese = @($zh.LanguageData.ChildNodes | ForEach-Object Name)
if (Compare-Object $english $chinese) { throw 'Localization key mismatch.' }
if (@($english | Select-Object -Unique).Count -ne $english.Count) { throw 'Duplicate English key.' }
if (@($chinese | Select-Object -Unique).Count -ne $chinese.Count) { throw 'Duplicate Chinese key.' }
$used = @(Get-ChildItem (Join-Path $root 'Source') -Filter *.cs |
    Select-String -Pattern '"(IM_[A-Za-z]+)"' -AllMatches | ForEach-Object { $_.Matches } |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
foreach ($key in $used) { if ($key -notin $english) { throw "Missing translation: $key" } }
Write-Host "XML/localization/load-folder validation passed: $($english.Count) keys."
# Native 1.7 GPU/UI acceptance runs in the actual game; 1.6 outputs stay archived.
$monorepo = Split-Path $root -Parent
$python = if (Get-Command python3 -ErrorAction SilentlyContinue) { 'python3' } else { 'python' }
$deployArgs = @((Join-Path $monorepo 'scripts/python/deploy.py'), 'iris_menus', '--build-only', '--skip-build')
if ($RimWorldManagedDir) { $deployArgs += @('--rimworld-dir', $RimWorldManagedDir) }
& $python @deployArgs
if ($LASTEXITCODE -ne 0) { throw 'Package staging failed.' }
$staging = Join-Path $monorepo 'artifacts/iris_menus/release'
if (Test-Path -LiteralPath (Join-Path $staging '1.6')) { throw 'The current 1.7 staging package must not contain archived 1.6 files.' }
[xml]$stagedFolders = Get-Content (Join-Path $staging 'LoadFolders.xml') -Raw -Encoding UTF8
foreach ($version in @('1.7')) {
    $folders = @($stagedFolders.SelectNodes("/loadFolders/v$version/li") | ForEach-Object InnerText)
    $assemblies = @($folders | ForEach-Object {
        $path = Join-Path $staging "$_/Assemblies"
        if (Test-Path -LiteralPath $path) { Get-ChildItem -LiteralPath $path -Recurse -Filter *.dll }
    })
    $expected = [IO.Path]::GetFullPath((Join-Path $staging "$version/Assemblies/IrisMenus.dll"))
    if ($assemblies.Count -ne 1 -or $assemblies[0].FullName -ne $expected) {
        throw "Unexpected shipped assemblies for RimWorld $version"
    }
    foreach ($resource in @('Defs/MainButtonDefs.xml', 'Languages/English/Keyed/IrisMenus.xml', 'Languages/ChineseSimplified/Keyed/IrisMenus.xml')) {
        $matches = @($folders | Where-Object { Test-Path -LiteralPath (Join-Path $staging "$_/$resource") -PathType Leaf })
        if ($matches.Count -ne 1) { throw "Missing/duplicate $resource for RimWorld $version" }
    }
    if ($version -eq '1.7') {
        foreach ($folder in $folders) {
            if (Test-Path -LiteralPath (Join-Path $staging "$folder/AssetBundles")) {
                throw 'RimWorld 1.7 must not load the Unity shader bundle.'
            }
        }
    }
}
Write-Host "RimWorld 1.7 package staged with isolated legacy runtime: $staging"
