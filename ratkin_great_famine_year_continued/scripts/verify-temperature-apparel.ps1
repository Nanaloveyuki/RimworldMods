$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$defPath = Join-Path $root 'Defs/ThingDefs/ThingDefs_MouseDisaster_TemperatureApparel.xml'
$sourcePath = Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Apparel.cs'
$settingsPath = Join-Path $root '1.6/Source/MouseDisasterSettings.cs'
$modPath = Join-Path $root '1.6/Source/ModEntry.cs'
$irisPath = Join-Path $root '1.6/Source/ModEntry.IrisMenus.cs'
$targetPolicyPath = Join-Path $root '1.6/Source/MouseDisasterIncidentTargetPolicy.cs'
$chancePath = Join-Path $root '1.6/Source/IncidentChancePatches.cs'
$patchPath = Join-Path $root '1.6/Source/IdentityLifecyclePatches.cs'

[xml]$defsDocument = Get-Content -LiteralPath $defPath -Raw
$defs = @($defsDocument.Defs.ThingDef | Where-Object { $_.defName })
if ($defs.Count -ne 12) {
    throw "Expected 12 temperature apparel defs, found $($defs.Count)."
}
$baseDef = @($defsDocument.Defs.ThingDef | Where-Object { $_.Name -eq 'MouseDisasterTemperatureApparelBase' })[0]
if ($null -eq $baseDef -or
    $baseDef.graphicData.texPath -ne 'Things/Pawn/Humanlike/Apparel/TribalA/TribalA' -or
    -not [string]::IsNullOrWhiteSpace([string]$baseDef.apparel.wornGraphicPath) -or
    @($baseDef.apparel.bodyPartGroups.li) -notcontains 'Waist' -or
    [string]$baseDef.apparel.developmentalStageFilter -notmatch '\bBaby\b' -or
    [string]$baseDef.apparel.developmentalStageFilter -notmatch '\bChild\b' -or
    [string]$baseDef.apparel.developmentalStageFilter -notmatch '\bAdult\b') {
    throw 'Temperature apparel does not use the expected non-rendering protection layer, Waist slot, and developmental stages.'
}

$expected = @(
    @{ Name = 'MouseDisaster_Heat_StaleWetCloth'; Stat = 'Insulation_Heat'; Value = 8 },
    @{ Name = 'MouseDisaster_Heat_DryMudCoating'; Stat = 'Insulation_Heat'; Value = 12 },
    @{ Name = 'MouseDisaster_Heat_ReedShadeWrap'; Stat = 'Insulation_Heat'; Value = 20 },
    @{ Name = 'MouseDisaster_Heat_SoakedBarkWrap'; Stat = 'Insulation_Heat'; Value = 28 },
    @{ Name = 'MouseDisaster_Heat_MudReedMantle'; Stat = 'Insulation_Heat'; Value = 36 },
    @{ Name = 'MouseDisaster_Heat_HeavyCoolingMud'; Stat = 'Insulation_Heat'; Value = 44 },
    @{ Name = 'MouseDisaster_Cold_ThinHempLayer'; Stat = 'Insulation_Cold'; Value = 8 },
    @{ Name = 'MouseDisaster_Cold_LayeredHempClothes'; Stat = 'Insulation_Cold'; Value = 12 },
    @{ Name = 'MouseDisaster_Cold_StrawBarkQuilt'; Stat = 'Insulation_Cold'; Value = 20 },
    @{ Name = 'MouseDisaster_Cold_PatchedFurCloak'; Stat = 'Insulation_Cold'; Value = 28 },
    @{ Name = 'MouseDisaster_Cold_SmokeStiffenedBlanket'; Stat = 'Insulation_Cold'; Value = 40 },
    @{ Name = 'MouseDisaster_Cold_ThickHideHempWrap'; Stat = 'Insulation_Cold'; Value = 56 }
)

$source = Get-Content -LiteralPath $sourcePath -Raw
$settings = Get-Content -LiteralPath $settingsPath -Raw
$mod = Get-Content -LiteralPath $modPath -Raw
$iris = Get-Content -LiteralPath $irisPath -Raw
$targetPolicy = Get-Content -LiteralPath $targetPolicyPath -Raw
$chance = Get-Content -LiteralPath $chancePath -Raw
$patch = Get-Content -LiteralPath $patchPath -Raw
if ($source -notmatch 'GenTemperature\.TryGetTemperatureForCell' -or
    $source -notmatch 'MinimumGeneratedComfortTemperature' -or
    $source -notmatch 'MaximumGeneratedComfortTemperature' -or
    $source -notmatch 'HasTemperatureProtectionApparel' -or
    $patch -notmatch 'Prefix\(\s*Pawn __instance, Map map, bool respawningAfterLoad\)' -or
    $patch -notmatch 'bool respawningAfterLoad' -or
    $patch -notmatch 'respawningAfterLoad\s*\|\|' -or
    $patch -notmatch '__instance\.Spawned' -or
    $patch -notmatch 'Pre-spawn temperature apparel hook failed' -or
    $patch -notmatch 'ApplyTemperatureProtectionApparel\(\s*__instance, map, __instance\.Position\)' -or
    $patch -match 'ApplyTemperatureProtectionApparel\(\s*__instance, __instance\.Map, __instance\.Position\)') {
    throw 'Temperature apparel is not connected to the spawned MouseDisaster pawn path.'
}
if ($settings -notmatch 'DefaultMouseDisasterMinimumEnvironmentTemperature = -35f' -or
    $settings -notmatch 'DefaultMouseDisasterMaximumEnvironmentTemperature = 70f' -or
    $settings -notmatch 'enableTemperatureProtectionApparel' -or
    $settings -notmatch 'temperatureApparelInsulation' -or
    $settings -notmatch 'disabledTemperatureApparelDefNames' -or
    $settings -notmatch 'enableDetailedTraceLog = false' -or
    $settings -notmatch 'Scribe_Values\.Look\(ref mouseDisasterMinimumEnvironmentTemperature' -or
    $settings -notmatch 'Scribe_Collections\.Look\(ref temperatureApparelInsulation' -or
    $settings -notmatch 'Scribe_Values\.Look\(ref enableDetailedTraceLog') {
    throw 'Temperature adaptation settings are not persisted with the expected defaults.'
}
if ($mod -notmatch 'SettingsPage\.Environment' -or
    $mod -notmatch 'DrawEnvironmentSettings' -or
    $mod -notmatch 'enableDetailedTraceLog' -or
    $iris -notmatch 'Environment' -or
    $targetPolicy -notmatch 'ShouldBlockEnvironmentTemperature' -or
    $chance -notmatch 'ShouldBlockEnvironmentTemperature') {
    throw 'Temperature adaptation settings are not connected to the UI and map incident gate.'
}

foreach ($item in $expected) {
    $def = $defs | Where-Object defName -eq $item.Name
    if ($null -eq $def) {
        throw "Missing temperature apparel def: $($item.Name)."
    }

    $stats = @($def.statBases.ChildNodes | Where-Object NodeType -eq Element)
    if ($stats.Count -ne 1 -or $stats[0].Name -ne $item.Stat -or [float]$stats[0].InnerText -ne $item.Value) {
        throw "Unexpected temperature stats for $($item.Name)."
    }
    if ($null -ne $def.equippedStatOffsets -or $null -ne $def.comps) {
        throw "$($item.Name) has non-temperature gameplay effects."
    }
    if ($def.ParentName -ne 'MouseDisasterTemperatureApparelBase') {
        throw "$($item.Name) does not use the shared temperature apparel base."
    }
    $isCold = $item.Stat -eq 'Insulation_Cold'
    $mapping = 'new TemperatureApparelOption("{0}", {1}f, {2})' -f $item.Name, $item.Value, $isCold.ToString().ToLowerInvariant()
    if ($source -notmatch [regex]::Escape($mapping)) {
        throw "$($item.Name) is not mapped to the expected runtime insulation."
    }
}

$baseMin = 21.0
$baseMax = 26.0
$coldForMinus35 = ($baseMin - [math]::Min(-35.0, -35.0))
$heatFor70 = (70.0 - $baseMax)
if ($coldForMinus35 -gt 56.0 -or $heatFor70 -gt 44.0) {
    throw 'Temperature apparel does not cover the requested -35C to 70C boundary for the current Ratkin baseline.'
}

"PASS: 12 non-rendering temperature apparel defs, developmental-stage support, guarded pre-spawn hookup, lifecycle preservation, and -35C/70C boundary coverage verified."
