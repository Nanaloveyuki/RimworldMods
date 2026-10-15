$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Require-Text([string]$text, [string]$needle, [string]$message) {
    if (!$text.Contains($needle)) { throw $message }
}

$settings = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
$patch = Get-Content (Join-Path $root '1.6/Source/ChildcarePatches.cs') -Raw
$mod = Get-Content (Join-Path $root '1.6/Source/ModEntry.cs') -Raw
$iris = Get-Content (Join-Path $root '1.6/Source/ModEntry.IrisMenus.cs') -Raw
$english = Get-Content (Join-Path $root 'Languages/English/Keyed/MouseDisasterUI.xml') -Raw
$chinese = Get-Content (Join-Path $root 'Languages/ChineseSimplified/Keyed/MouseDisasterUI.xml') -Raw

$key = 'allowNonColonistChildcareForMouseDisasterEggs'
Require-Text $settings ('public bool ' + $key + ' = false;') 'Non-colonist childcare must default to disabled.'
Require-Text $settings ('Scribe_Values.Look(ref ' + $key) 'Non-colonist childcare setting is not persisted.'
Require-Text $settings ($key + ' = false;') 'Non-colonist childcare setting is not reset to default.'
Require-Text $mod ('ref Settings.' + $key) 'Non-colonist childcare setting is missing from the legacy settings page.'
Require-Text $iris 'PawnBehavior' 'IrisMenus does not expose the page containing childcare settings.'

Require-Text $patch 'pawn.IsColonist' 'Childcare policy no longer distinguishes colonists from non-colonists.'
Require-Text $patch $key 'Childcare policy does not read the non-colonist setting.'
Require-Text $patch 'IsMouseDisasterBabyTarget' 'Childcare policy is not restricted to Mouse Disaster neutral babies.'
Require-Text $patch 'FilterAndAppendAllowedBabies' 'Play targets are not filtered by the carer setting.'
Require-Text $patch 'WorkGiver_PlayWithBaby.HasJobOnThing' 'Direct baby-play jobs are not gated.'
Require-Text $patch 'typeof(Pawn), typeof(Pawn)' 'Pawn-level faction compatibility is not patched.'
Require-Text $patch 'FindAutofeedBaby' 'Autofeed fallback is not gated.'
Require-Text $patch 'FindUnsafeBaby' 'Unsafe-baby fallback is not gated.'
if ($patch.Contains('IsColonistCarer')) { throw 'Obsolete colonist-only childcare predicate remains.' }

foreach ($text in $english, $chinese) {
    Require-Text $text 'MouseDisaster_Settings_AllowNonColonistChildcareForMouseDisasterEggs' 'Non-colonist childcare localization is missing.'
    Require-Text $text 'MouseDisaster_Settings_AllowNonColonistChildcareForMouseDisasterEggs_Tooltip' 'Non-colonist childcare tooltip localization is missing.'
}

Write-Host 'PASS: non-colonist Mouse Disaster childcare setting, gating, IrisMenus and localization are wired.'
