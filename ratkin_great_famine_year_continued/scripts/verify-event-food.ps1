$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$policy = Get-Content (Join-Path $root '1.6/Source/MouseDisasterEventFoodPolicy.cs') -Raw
$relief = Get-Content (Join-Path $root '1.6/Source/MouseDisasterReliefAreaPolicy.cs') -Raw
$methods = ''
foreach ($name in @('AllowsOutsideReliefSearch','AppliesEventFoodRestrictions','IsFoodAllowed','ResolveWeight','ScoreFood','NormalizeWeight','TryGetWeight')) {
    $methods += Get-CSharpMethod $policy $name
}
foreach ($name in @('ShouldUseReliefAreaFoodFirst','ShouldFallbackToDefaultFoodSearch','CanUseReliefFood')) {
    $methods += Get-CSharpMethod $relief $name
}
$stub = @'
using System;
using System.Collections.Generic;
namespace MouseDisaster {
public static class EventFoodHarness {
    const float DefaultWeight = 0.1f;
    const float MinWeight = 0f;
    const float MaxWeight = 1f;
    const float WeightScoreScale = 10000f;
    const float MarketValueScoreScale = 100f;
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    public static int Run() {
        Check(!AllowsOutsideReliefSearch(false, false), "disabled outside search allowed");
        Check(!AllowsOutsideReliefSearch(false, true), "disabled outside search with relief allowed");
        Check(!AllowsOutsideReliefSearch(true, true), "outside search used while relief food exists");
        Check(AllowsOutsideReliefSearch(true, false), "outside search blocked without relief food");
        Check(!AppliesEventFoodRestrictions(false, false), "unrestricted pawn still event-restricted");
        Check(!AppliesEventFoodRestrictions(true, true), "prisoner or colonist still event-restricted");
        Check(AppliesEventFoodRestrictions(true, false), "visitor food restriction skipped");
        Check(ShouldUseReliefAreaFoodFirst(true, true, true), "relief food not forced first");
        Check(!ShouldUseReliefAreaFoodFirst(true, false, true), "missing relief food still forced");
        Check(ShouldFallbackToDefaultFoodSearch(true, false), "no fallback without relief food");
        Check(!ShouldFallbackToDefaultFoodSearch(true, true), "fallback used while relief food exists");
        Check(CanUseReliefFood(true, false), "ordinary visitor blocked from relief food");
        Check(!CanUseReliefFood(true, true), "colonist still uses event relief food");
        Check(!CanUseReliefFood(true, false, true), "abandoned delivery still seeks relief food");
        Check(IsFoodAllowed("MealSimple", null), "null disabled list rejected food");
        Check(IsFoodAllowed("MealSimple", new List<string>()), "empty disabled list rejected food");
        Check(IsFoodAllowed("ModdedStew", new List<string> { "MealLavish" }), "mod food not default-enabled");
        Check(!IsFoodAllowed("MealLavish", new List<string> { "MealLavish" }), "disabled food still allowed");
        Check(!IsFoodAllowed("meallavish", new List<string> { "MealLavish" }), "disabled food check is case-sensitive");
        Check(!IsFoodAllowed(null, null), "null def allowed");
        Check(Math.Abs(ResolveWeight("Rice", null) - 0.1f) < 0.0001f, "missing weight not default 10%");
        Check(Math.Abs(ResolveWeight("Rice", new Dictionary<string, float>()) - 0.1f) < 0.0001f, "empty weight map not default 10%");
        Check(Math.Abs(ResolveWeight("Rice", new Dictionary<string, float> { { "Rice", 0.4f } }) - 0.4f) < 0.0001f, "stored weight ignored");
        Check(Math.Abs(ResolveWeight("rice", new Dictionary<string, float> { { "Rice", 0.4f } }) - 0.4f) < 0.0001f, "weight lookup ignored case");
        Check(Math.Abs(NormalizeWeight(float.NaN) - 0.1f) < 0.0001f, "NaN weight not defaulted");
        Check(NormalizeWeight(-1f) == 0f, "negative weight not clamped");
        Check(NormalizeWeight(2f) == 1f, "oversize weight not clamped");
        float rice = ScoreFood(0.1f, 40f, 0.4f, true);
        float simple = ScoreFood(0.1f, 10f, 15f, true);
        float lavish = ScoreFood(0.1f, 5f, 40f, true);
        Check(rice > simple && simple > lavish, "equal weights did not prefer lowest value outside relief");
        float weightedLavish = ScoreFood(0.8f, 5f, 40f, true);
        Check(weightedLavish > rice, "higher player weight could not outrank cheaper food");
        float reliefRice = ScoreFood(0.1f, 40f, 0.4f, false);
        float reliefSimple = ScoreFood(0.1f, 10f, 15f, false);
        Check(reliefSimple > reliefRice, "relief ranking used market value");
        Check(Math.Abs(reliefSimple - (0.1f * 10000f - 10f)) < 0.001f, "relief score formula changed");
        return checks;
    }
__METHODS__
}
}
'@
Add-Type -TypeDefinition $stub.Replace('__METHODS__', $methods)
"PASS: $([MouseDisaster.EventFoodHarness]::Run()) event-food policy assertions."
$foodUtil = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Food.cs') -Raw
$patches = Get-Content (Join-Path $root '1.6/Source/ReliefAreaFoodPatches.cs') -Raw
$beggar = Get-Content (Join-Path $root '1.6/Source/JobGiver_MouseDisasterBeggar.cs') -Raw
$delivery = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.AbandonedDelivery.cs') -Raw
$settings = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
if ($foodUtil -notmatch 'AppliesEventFoodRestrictions\(') { throw 'event food restrictions are not applied to affiliated pawns' }
if ($foodUtil -notmatch 'IsAbandonedDeliveryPawn\(pawn\)') { throw 'abandoned delivery pawns still seek relief-area food' }
if ($beggar -notmatch 'IsAbandonedDeliveryPawn\(pawn\)') { throw 'abandoned delivery mother still uses beggar relief jobs' }
if ($delivery -notmatch 'JobGiver_GetFood') { throw 'abandoned delivery still uses vanilla GetFood' }
if ($foodUtil -notmatch 'IsPlayerAffiliatedRatkin\(pawn\)') { throw 'prisoners and colonists remain in the event food search' }
if ($foodUtil -notmatch 'BestFoodInInventory') { throw 'carried food is ignored when relief-only search is enabled' }
if ($patches -notmatch 'AllowsOutsideReliefSearch\(') { throw 'outside relief search policy is unused' }
if ($settings -notmatch 'allowEventPawnsEatOutsideReliefArea') { throw 'outside relief setting field is missing' }
if ($settings -notmatch 'disabledEventFoodDefNames') { throw 'disabled food field is missing' }
if ($settings -notmatch 'eventFoodWeights') { throw 'food weight field is missing' }
foreach ($language in @('ChineseSimplified', 'English')) {
    [xml]$visits = Get-Content (Join-Path $root "Languages/$language/Keyed/MouseDisasterVisits.xml") -Raw
    foreach ($key in @(
        'MouseDisaster_Settings_AllowEventPawnsEatOutsideReliefArea',
        'MouseDisaster_Settings_AllowEventPawnsEatOutsideReliefArea_Tooltip',
        'MouseDisaster_Settings_EventFoodWeights_Hint',
        'MouseDisaster_Settings_EventFood_EnableAll',
        'MouseDisaster_Settings_EventFood_DisableAll',
        'MouseDisaster_Settings_EventFood_ResetWeights',
        'MouseDisaster_Settings_EventFood_NotLoaded',
        'MouseDisaster_Settings_EventFood_Item_Tooltip',
        'MouseDisaster_Settings_EventFood_Weight',
        'MouseDisaster_Settings_EventFood_Weight_Tooltip'
    )) {
        if (!$visits.LanguageData.($key)) { throw "Missing $language key: $key" }
    }
    [xml]$iris = Get-Content (Join-Path $root "Languages/$language/Keyed/MouseDisasterIrisMenus.xml") -Raw
    if (!$iris.LanguageData.MouseDisaster_IrisMenus_FoodWeights) { throw "Missing $language IrisMenus title: FoodWeights" }
}
'PASS: event-food source wiring, save fields and language keys.'
