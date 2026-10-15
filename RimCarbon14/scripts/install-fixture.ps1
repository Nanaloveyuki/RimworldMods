param([Parameter(Mandatory=$true)][string]$RimWorldDir, [Parameter(Mandatory=$true)][string]$Profile, [ValidateSet('1.7')][string]$GameVersion='1.7', [string]$HarmonyPath, [string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$installed=(Get-Content -LiteralPath (Join-Path $RimWorldDir 'Version.txt') -Raw).Trim().Split(' ')[0]
if (!$installed.StartsWith("$GameVersion.")) { throw "Fixture requires RimWorld $GameVersion, found $installed." }
$properties=@("-p:RimWorldDir=$RimWorldDir", "-p:GameVersion=$GameVersion")
if ($HarmonyPath) { $properties += "-p:HarmonyPath=$HarmonyPath" }
& $Dotnet build (Join-Path $root 'tests/RimCarbon14.Fixture/RimCarbon14.Fixture.csproj') -c Release @properties
if ($LASTEXITCODE -ne 0) { throw 'Fixture build failed' }
$target=Join-Path $RimWorldDir 'Mods/RimCarbon14.Fixture'
$assemblies=Join-Path $target "$GameVersion/Assemblies"
New-Item -ItemType Directory -Path $assemblies -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'tests/RimCarbon14.Fixture/About') -Destination $target -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'tests/RimCarbon14.Fixture/LoadFolders.xml') -Destination $target -Force
Copy-Item -LiteralPath (Join-Path $root 'tests/RimCarbon14.Fixture/bin/Release/net10.0/RimCarbon14.Fixture.dll') -Destination $assemblies -Force
$config=Join-Path $Profile 'Config/ModsConfig.xml'
$xml=[xml](Get-Content -LiteralPath $config -Raw)
if ('nanaloveyuki.rimcarbon14.fixture' -notin $xml.ModsConfigData.activeMods.li) {
    $entry=$xml.CreateElement('li'); $entry.InnerText='nanaloveyuki.rimcarbon14.fixture'
    $xml.ModsConfigData.activeMods.AppendChild($entry) | Out-Null
    $xml.Save($config)
}
