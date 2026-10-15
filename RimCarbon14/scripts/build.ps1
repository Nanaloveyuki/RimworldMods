param(
    [Parameter(Mandatory=$true)][string]$RimWorldDir,
    [string]$Dotnet = 'dotnet',
    [string]$DeployTo,
    [switch]$SkipAgent
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts/RimCarbon14'
& $Dotnet build (Join-Path $root 'src/RimCarbon14.Mod/RimCarbon14.Mod.csproj') -c Release "-p:RimWorldDir=$RimWorldDir"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed' }
$assemblies = Join-Path $out '1.6/Assemblies'
New-Item -ItemType Directory -Path $assemblies -Force | Out-Null
foreach ($name in @('RimCarbon14.dll','RimCarbon14.Core.dll','Newtonsoft.Json.dll')) {
    Copy-Item -LiteralPath (Join-Path $root "src/RimCarbon14.Mod/bin/Release/net472/$name") -Destination $assemblies -Force
}
Copy-Item -LiteralPath (Join-Path $root 'About') -Destination $out -Recurse -Force
foreach ($name in @('LoadFolders.xml','LICENSE','README.md','THIRD_PARTY_NOTICES.md')) {
    if (Test-Path (Join-Path $root $name)) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $out -Force }
}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $out -Recurse -Force
if (!$SkipAgent) {
    & $Dotnet publish (Join-Path $root 'src/RimCarbon14.Agent/RimCarbon14.Agent.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $out 'Tools/win-x64')
    if ($LASTEXITCODE -ne 0) { throw 'Agent publish failed' }
    $helper = Join-Path $out 'Tools/win-x64/desktop-input.ps1'
    if (!(Test-Path -LiteralPath $helper) -or (Get-FileHash $helper).Hash -ne (Get-FileHash (Join-Path $root 'scripts/desktop-input.ps1')).Hash) { throw 'Published desktop helper is missing or stale.' }
    $assets = Get-Content (Join-Path $root 'src/RimCarbon14.Agent/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $licenses = Join-Path $out 'Tools/win-x64/licenses'
    New-Item -ItemType Directory -Path $licenses -Force | Out-Null
    $manifest = @()
    $packages = @{}
    foreach ($entry in $assets.libraries.GetEnumerator() | Where-Object { $_.Value.type -eq 'package' }) { $packages[$entry.Key] = $entry.Value }
    $deps = Get-Content (Join-Path $root 'src/RimCarbon14.Agent/bin/Release/net10.0/win-x64/RimCarbon14.Agent.deps.json') -Raw | ConvertFrom-Json -AsHashtable
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
if ($DeployTo) {
    $target = [IO.Path]::GetFullPath($DeployTo)
    if ($target -eq [IO.Path]::GetPathRoot($target) -or (Split-Path $target -Leaf) -ne 'RimCarbon14') { throw 'DeployTo must name a dedicated RimCarbon14 mod directory.' }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath $out | Copy-Item -Destination $target -Recurse -Force
    foreach ($name in @('RimCarbon14.dll','RimCarbon14.Core.dll','Newtonsoft.Json.dll')) {
        if ((Get-FileHash (Join-Path $assemblies $name)).Hash -ne (Get-FileHash (Join-Path $target "1.6/Assemblies/$name")).Hash) { throw "Deploy hash mismatch: $name" }
    }
}
if (!$SkipAgent) { Compress-Archive -LiteralPath $out -DestinationPath (Join-Path $root 'artifacts/RimCarbon14-win-x64.zip') -Force }
Write-Output "Package: $out"
