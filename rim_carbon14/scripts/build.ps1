param(
    [Parameter(Mandatory=$true)][string]$RimWorldDir,
    [ValidateSet('1.6','1.7')][string]$GameVersion = '1.7',
    [string]$HarmonyPath,
    [string]$Dotnet = 'dotnet',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$SkipAgent
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path (Split-Path $root -Parent) 'artifacts/rim_carbon14/package'
$legacy = Join-Path $root '1.6/Assemblies'
$runtime = Join-Path $root '1.7/Assemblies'
$names = @('RimCarbon14.dll','RimCarbon14.Core.dll','Newtonsoft.Json.dll')
foreach ($name in $names) {
    if (!(Test-Path -LiteralPath (Join-Path $legacy $name))) { throw "Preserved 1.6 runtime missing: $name. Restore the existing release; do not rebuild it." }
}
if ($GameVersion -eq '1.7') {
    $installed = (Get-Content -LiteralPath (Join-Path $RimWorldDir 'Version.txt') -Raw).Trim().Split(' ')[0]
    if (!$installed.StartsWith('1.7.')) { throw "RimWorldDir is not a 1.7 installation: $installed" }
    $properties = @("-p:RimWorldDir=$RimWorldDir", "-p:GameVersion=$GameVersion")
    if ($HarmonyPath) { $properties += "-p:HarmonyPath=$HarmonyPath" }
    & $Dotnet build (Join-Path $root 'src/RimCarbon14.Mod/RimCarbon14.Mod.csproj') -c $Configuration @properties
    if ($LASTEXITCODE -ne 0) { throw 'Mod build failed' }
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    foreach ($name in $names) {
        Copy-Item -LiteralPath (Join-Path $root "src/RimCarbon14.Mod/bin/$Configuration/net10.0/$name") -Destination $runtime -Force
    }
}
foreach ($version in @('1.6','1.7')) {
    $assemblies = Join-Path $out "$version/Assemblies"
    New-Item -ItemType Directory -Path $assemblies -Force | Out-Null
    foreach ($name in $names) {
        $source = Join-Path $root "$version/Assemblies/$name"
        if (!(Test-Path -LiteralPath $source)) { throw "Runtime missing: $source. Build 1.7 before packaging the legacy release." }
        Copy-Item -LiteralPath $source -Destination $assemblies -Force
    }
}
New-Item -ItemType Directory -Path (Join-Path $out 'About') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'About') | Copy-Item -Destination (Join-Path $out 'About') -Recurse -Force
foreach ($name in @('LoadFolders.xml','LICENSE','README.md','THIRD_PARTY_NOTICES.md')) {
    if (Test-Path (Join-Path $root $name)) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $out -Force }
}
New-Item -ItemType Directory -Path (Join-Path $out 'docs') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'docs') | Copy-Item -Destination (Join-Path $out 'docs') -Recurse -Force
if (!$SkipAgent) {
    & $Dotnet publish (Join-Path $root 'src/RimCarbon14.Agent/RimCarbon14.Agent.csproj') -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $out 'Tools/win-x64')
    if ($LASTEXITCODE -ne 0) { throw 'Agent publish failed' }
    $helper = Join-Path $out 'Tools/win-x64/desktop-input.ps1'
    if (!(Test-Path -LiteralPath $helper) -or (Get-FileHash $helper).Hash -ne (Get-FileHash (Join-Path (Split-Path $root -Parent) 'scripts/carbon/desktop-input.ps1')).Hash) { throw 'Published desktop helper is missing or stale.' }
    $assets = Get-Content (Join-Path $root 'src/RimCarbon14.Agent/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $licenses = Join-Path $out 'Tools/win-x64/licenses'
    New-Item -ItemType Directory -Path $licenses -Force | Out-Null
    $manifest = @()
    $packages = @{}
    foreach ($entry in $assets.libraries.GetEnumerator() | Where-Object { $_.Value.type -eq 'package' }) { $packages[$entry.Key] = $entry.Value }
    $deps = Get-Content (Join-Path $root "src/RimCarbon14.Agent/bin/$Configuration/net10.0/win-x64/RimCarbon14.Agent.deps.json") -Raw | ConvertFrom-Json -AsHashtable
    foreach ($entry in $deps.libraries.GetEnumerator() | Where-Object { $_.Key.StartsWith('runtimepack.') }) {
        $name = $entry.Key.Substring('runtimepack.'.Length)
        $packages[$name] = @{ path=$name.ToLowerInvariant() }
    }
    foreach ($entry in $packages.GetEnumerator()) {
        $package = $null
        foreach ($folder in $assets.packageFolders.Keys) {
            $candidate = Join-Path $folder $entry.Value.path
            if (Test-Path -LiteralPath $candidate) { $package = $candidate; break }
        }
        if (!$package) { throw "Restored package missing: $($entry.Key)" }
        $nuspec = Get-ChildItem -LiteralPath $package -Filter '*.nuspec' | Select-Object -First 1
        $metadata = ([xml](Get-Content -LiteralPath $nuspec.FullName -Raw)).package.metadata
        $destination = Join-Path $licenses ($entry.Key.Replace('/', '-'))
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Get-ChildItem -LiteralPath $package -File | Where-Object { $_.Name -match '^(LICENSE|NOTICE|THIRD-PARTY-NOTICES)(\.|$)' } | Copy-Item -Destination $destination -Force
        if ($metadata.id -like 'ModelContextProtocol*') {
            $license = Join-Path $destination 'LICENSE'
            if (!(Test-Path -LiteralPath $license)) { Invoke-WebRequest "https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/$($metadata.repository.commit)/LICENSE" -OutFile $license }
        }
        if (!(Get-ChildItem -LiteralPath $destination -File | Where-Object Name -like 'LICENSE*') -and $metadata.repository.url -in @('https://github.com/dotnet/dotnet','https://github.com/dotnet/extensions','https://github.com/dotnet/runtime')) {
            $license = Join-Path $destination 'LICENSE.TXT'
            $cache = Join-Path $licenses ('upstream-' + $metadata.repository.commit + '-LICENSE.TXT')
            $licenseName = if ($metadata.repository.url -eq 'https://github.com/dotnet/extensions') { 'LICENSE' } else { 'LICENSE.TXT' }
            if (!(Test-Path -LiteralPath $cache)) { Invoke-WebRequest ($metadata.repository.url.Replace('https://github.com/', 'https://raw.githubusercontent.com/') + '/' + $metadata.repository.commit + '/' + $licenseName) -OutFile $cache }
            Copy-Item -LiteralPath $cache -Destination $license -Force
        }
        if ($metadata.repository.url -eq 'https://github.com/dotnet/extensions') {
            $notice = Join-Path $destination 'THIRD-PARTY-NOTICES.TXT'
            if (!(Test-Path -LiteralPath $notice)) { Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/extensions/$($metadata.repository.commit)/THIRD-PARTY-NOTICES.TXT" -OutFile $notice }
        }
        if (!(Get-ChildItem -LiteralPath $destination -File | Where-Object Name -like 'LICENSE*')) { throw "License text missing: $($entry.Key)" }
        $manifest += @{ id=$metadata.id; version=$metadata.version; authors=$metadata.authors; copyright=$metadata.copyright; license=$metadata.license.InnerText; repository=$metadata.repository.url }
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $licenses 'dependencies.json') -Encoding utf8
}
if (!$SkipAgent) { Compress-Archive -LiteralPath $out -DestinationPath (Join-Path (Split-Path $root -Parent) 'artifacts/rim_carbon14/RimCarbon14-win-x64.zip') -Force }
Write-Output "Package: $out"
