$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Require-Text([string]$text, [string]$needle, [string]$message) {
    if (!$text.Contains($needle)) { throw $message }
}

$settings = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
$visitor = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterVisitorControl.cs') -Raw
$mod = Get-Content (Join-Path $root '1.6/Source/ModEntry.cs') -Raw
$iris = Get-Content (Join-Path $root '1.6/Source/ModEntry.IrisMenus.cs') -Raw
$timer = Get-Content (Join-Path $root '1.6/Source/HediffComp_MouseDisasterEmploymentTimer.cs') -Raw
$defs = Get-Content (Join-Path $root 'Defs/HediffDefs/Hediffs_MouseDisaster.xml') -Raw
$guanyin = Get-Content (Join-Path $root 'Defs/HediffDefs/Hediffs_MouseDisaster_GuanyinTu.xml') -Raw

Require-Text $settings 'DefaultTemporaryRecruitDurationDays = 5' 'Short-term default is not 5 days.'
Require-Text $settings 'DefaultHiredWorkerDurationDays = GenDate.DaysPerYear' 'Long-term default is not one RimWorld year.'
Require-Text $settings 'MaxTemporaryRecruitDurationDays = GenDate.DaysPerYear' 'Short-term maximum is not one RimWorld year.'
Require-Text $settings 'MaxHiredWorkerDurationDays = GenDate.DaysPerYear * 10' 'Long-term maximum is not ten RimWorld years.'
foreach ($field in 'temporaryRecruitDurationDays','hiredWorkerDurationDays') {
    Require-Text $settings ('Scribe_Values.Look(ref ' + $field) "Setting is not persisted: $field"
    Require-Text $settings ($field + ' = Mathf.Clamp(') "Setting is not clamped: $field"
    Require-Text $settings ($field + ' = ' + ('Default' + $(if ($field -eq 'temporaryRecruitDurationDays') { 'TemporaryRecruitDurationDays' } else { 'HiredWorkerDurationDays' }))) "Setting reset is missing: $field"
}

Require-Text $visitor 'record.temporaryUntilTick = (Find.TickManager?.TicksGame ?? 0) +' 'Long-term employment has no timer.'
Require-Text $visitor 'record.employmentTimerPaused = true;' 'Identity changes do not pause employment timers.'
Require-Text $visitor 'record.temporaryUntilTick = -1;' 'Paused employment does not clear the active deadline.'
Require-Text $visitor 'NotifyNarrativeVisitorIdentityChanged' 'Identity changes are not passed to narrative tracking.'
Require-Text $visitor 'SyncEmploymentMarkers(record.pawn, record.status, GenDate.TicksPerDay, true);' 'Loaded paused employment does not restore a disabled health timer.'
Require-Text $visitor 'HiredWorkerDurationTicks' 'Long-term duration is not sourced from settings.'
Require-Text $visitor 'TicksToPeriod(out years, out quadrums, out days, out hours)' 'Employment duration formatter does not preserve years and remaining days.'
Require-Text $timer 'FormatDurationLabel(ticksToDisappear)' 'Employment timer does not use the shared largest-unit display.'
Require-Text $timer 'HediffComp_DisappearsDisableable' 'Employment timer is not pauseable.'
Require-Text $mod 'MouseDisaster_Settings_TemporaryRecruitDuration' 'Short-term duration is missing from settings UI.'
Require-Text $mod 'MouseDisaster_Settings_HiredWorkerDuration' 'Long-term duration is missing from settings UI.'
Require-Text $iris 'PawnBehavior' 'IrisMenus does not expose the pawn behavior settings page.'

[xml]$defsXml = $defs
foreach ($name in 'MouseDisaster_TemporaryShelterMark','MouseDisaster_HiredWorkerMark') {
    $def = @($defsXml.Defs.HediffDef | Where-Object defName -eq $name)
    if ($def.Count -ne 1 -or $def.tendable -ne 'false' -or $def.everCurableByItem -ne 'false' -or !$def.comps.li.Class) {
        throw "Employment marker is not a non-treatable countdown: $name"
    }
}
[xml]$guanyinXml = $guanyin
if ($guanyinXml.Defs.HediffDef.tendable -ne 'false' -or $guanyinXml.Defs.HediffDef.everCurableByItem -ne 'false') {
    throw 'Guanyin clay satiety remains treatable.'
}

Write-Host 'PASS: employment settings, identity pause, narrative handoff, and non-treatable timers are wired.'
