param([Parameter(Mandatory=$true)][string]$RimWorldDir, [Parameter(Mandatory=$true)][string]$Profile, [string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& $Dotnet build (Join-Path $root 'tests/RimCarbon14.Fixture/RimCarbon14.Fixture.csproj') -c Release "-p:RimWorldDir=$RimWorldDir"
if ($LASTEXITCODE -ne 0) { throw 'Fixture build failed' }
$target=Join-Path $RimWorldDir 'Mods/RimCarbon14.Fixture'
New-Item -ItemType Directory -Path (Join-Path $target 'Assemblies') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'tests/RimCarbon14.Fixture/About') -Destination $target -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'tests/RimCarbon14.Fixture/bin/Release/net472/RimCarbon14.Fixture.dll') -Destination (Join-Path $target 'Assemblies') -Force
$config=Join-Path $Profile 'Config/ModsConfig.xml'
$xml=[xml](Get-Content -LiteralPath $config -Raw)
if ('nanaloveyuki.rimcarbon14.fixture' -notin $xml.ModsConfigData.activeMods.li) {
    $entry=$xml.CreateElement('li'); $entry.InnerText='nanaloveyuki.rimcarbon14.fixture'
    $xml.ModsConfigData.activeMods.AppendChild($entry) | Out-Null
    $xml.Save($config)
}
