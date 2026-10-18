$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Require-Text([string]$text, [string]$needle, [string]$message) {
    if (!$text.Contains($needle)) {
        throw $message
    }
}

$settings = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
$modEntry = Get-Content (Join-Path $root '1.6/Source/ModEntry.cs') -Raw
$travel = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Travel.cs') -Raw
$factions = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Factions.cs') -Raw
$generation = Get-Content (Join-Path $root '1.6/Source/MouseDisasterPawnGenerationPatches.cs') -Raw
$generationUtility = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Generation.cs') -Raw
$trade = Get-Content (Join-Path $root '1.6/Source/TradePatches.cs') -Raw
$tradeUtility = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Trade.cs') -Raw
$childExchangeIncident = Get-Content (Join-Path $root '1.6/Source/IncidentWorker_ChildExchange.cs') -Raw
$childExchange = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.ChildExchange.cs') -Raw
$pendingState = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterPendingState.cs') -Raw
$eventBehavior = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterEventBehavior.cs') -Raw
$lifecycle = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Lifecycle.cs') -Raw
$visitorControl = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterVisitorControl.cs') -Raw
$familyExit = Get-Content (Join-Path $root '1.6/Source/LordJob_MouseDisasterFamilyExit.cs') -Raw
$english = Get-Content (Join-Path $root 'Languages/English/Keyed/MouseDisasterUI.xml') -Raw
$chinese = Get-Content (Join-Path $root 'Languages/ChineseSimplified/Keyed/MouseDisasterUI.xml') -Raw

Require-Text $settings 'public bool allowMouseDisasterFactionToLeaveWhenIdle = false;' 'Idle departure setting is not default-off.'
Require-Text $settings 'public bool preventUnnecessaryNeutralPawnRelations = true;' 'Unnecessary relation blocking is not default-on.'
Require-Text $settings 'Scribe_Values.Look(ref allowMouseDisasterFactionToLeaveWhenIdle, "allowMouseDisasterFactionToLeaveWhenIdle", false);' 'Idle departure setting is not serialized.'
Require-Text $settings 'Scribe_Values.Look(ref preventUnnecessaryNeutralPawnRelations, "preventUnnecessaryNeutralPawnRelations", true);' 'Relation blocking setting is not serialized.'
Require-Text $modEntry 'MouseDisaster_Settings_AllowMouseDisasterFactionToLeaveWhenIdle' 'Idle departure setting is missing from the settings UI.'
Require-Text $modEntry 'MouseDisaster_Settings_PreventUnnecessaryNeutralPawnRelations' 'Relation blocking setting is missing from the settings UI.'
Require-Text $travel 'if (!force && ShouldBlockIdleDeparture(pawn))' 'Ordinary departure is not guarded by the setting.'
Require-Text $travel 'IsMouseDisasterNeutralFaction(pawn.Faction)' 'Departure guard does not target the neutral Mouse Disaster faction.'
Require-Text $factions 'public static bool IsMouseDisasterNeutralFaction(Faction faction)' 'Neutral Mouse Disaster faction helper is missing.'
Require-Text $visitorControl 'RequestDeparture(pawn)' 'Explicit visitor departure is not persisted for retries.'
Require-Text $lifecycle 'ExitMapJob(pawn, force: true)' 'Explicit lifecycle departure was not preserved.'
Require-Text $familyExit 'ExitMapJob(pawn, force: true)' 'Explicit family departure was not preserved.'

Require-Text $trade '[HarmonyPatch(typeof(Pawn_TraderTracker), nameof(Pawn_TraderTracker.CanTradeNow), MethodType.Getter)]' 'Trader CanTradeNow patch is missing.'
Require-Text $trade '[HarmonyPatch(typeof(Tradeable), nameof(Tradeable.TraderWillTrade), MethodType.Getter)]' 'Pawn Tradeable eligibility patch is missing.'
Require-Text $trade 'IsMouseDisasterTradePawn(tradePawn)' 'Mouse egg Pawn is not included in trade eligibility.'
Require-Text $trade 'ref bool __state' 'Trade identification is not captured before vanilla clears pawn status.'
Require-Text $trade '(!__state && !markedChattel && !forcePrisoner)' 'Read-loaded mouse egg purchase state is not restored after vanilla PreTraded.'
Require-Text $trade 'private static bool IsMouseDisasterTraderIdleDeparture' 'Trader idle departure guard is missing.'
Require-Text $trade 'transition.sources[0]?.GetType().Name != "LordToil_DefendTraderCaravan"' 'Trader timed departure guard does not identify the vanilla idle toil.'
Require-Text $trade 'transition.triggers[0] is Trigger_TicksPassed' 'Trader timed departure guard is not limited to the idle timer.'
Require-Text $trade 'public static void Prefix(Pawn_TraderTracker __instance)' 'Loaded trader tracker repair hook is missing.'
Require-Text $trade 'MouseDisasterUtility.IsMouseDisasterRatkinTradeThingDef(td)' 'Ratkin trade goods are not covered by trader eligibility.'
Require-Text $tradeUtility 'public static void RepairLoadedTradeLeaders()' 'Loaded trader repair routine is missing.'
Require-Text $tradeUtility 'HasActiveChildExchangeForTrader(pawn)' 'Loaded trader repair is not scoped to active exchange state.'
Require-Text $childExchangeIncident 'EnsureTradeLeader(trader, MouseDisasterUtility.ResolveSlaveTraderKind())' 'Child exchange traders are not initialized for trade.'
Require-Text $pendingState 'MouseDisasterUtility.RepairLoadedTradeLeaders();' 'Loaded trader repair is not run during save recovery.'
Require-Text $eventBehavior 'private void RebuildRuntimeIndexes()' 'Event runtime index rebuild is missing.'
Require-Text $eventBehavior 'private static bool IsValidLoadedEventPawn(Pawn pawn)' 'Invalid event Pawn references are not filtered during load.'
Require-Text $eventBehavior 'if (byPawn.TryGetValue(pawn, out var owner) && owner == group)' 'Event registration still assumes a valid Pawn index.'
Require-Text $visitorControl 'var seenPawns = new HashSet<Pawn>();' 'Duplicate visitor records are not removed during load.'
Require-Text $visitorControl 'Removed an invalid visitor record after load' 'Malformed visitor records are not dropped during load.'
Require-Text $childExchange 'private static bool IsPersistedMouseDisasterTradePawn(Pawn pawn)' 'Read-loaded mouse egg recognition is missing.'
Require-Text $childExchange 'pawn.guest.IsPrisoner' 'Read-loaded mouse egg recognition does not require prisoner state.'
Require-Text $childExchange 'lord?.LordJob is LordJob_TradeWithColony' 'Read-loaded mouse egg recognition does not require a trade lord.'

Require-Text $generation '[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]' 'Pawn generation relation patch is missing.'
Require-Text $generation 'request.ExtraPawnForExtraRelationChance != null' 'Explicit relation requests are not exempted.'
Require-Text $generation 'request.AllowedDevelopmentalStages.Newborn()' 'Newborn generation is not exempted.'
Require-Text $generation 'request.CanGeneratePawnRelations = false;' 'Unnecessary relation generation is not blocked.'
Require-Text $generationUtility 'MouseDisasterPawnGenerationPolicy.AllowRelationsForDirectMouseDisasterGeneration()' 'Direct event generation does not use the relation setting.'

foreach ($key in @(
    'MouseDisaster_Settings_AllowMouseDisasterFactionToLeaveWhenIdle',
    'MouseDisaster_Settings_AllowMouseDisasterFactionToLeaveWhenIdle_Tooltip',
    'MouseDisaster_Settings_PreventUnnecessaryNeutralPawnRelations',
    'MouseDisaster_Settings_PreventUnnecessaryNeutralPawnRelations_Tooltip'
)) {
    Require-Text $english $key "English localization is missing: $key"
    Require-Text $chinese $key "Chinese localization is missing: $key"
}

'PASS: neutral departure, trader eligibility, save recovery, and nonessential relation guards are wired.'
