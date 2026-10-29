using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Detects colony/environmental events and applies situational mood memories
    /// to toddlers on this map. Rewritten in the "event moods correctness audit"
    /// (see Architecture.md §5.2) — read that section before changing the rules
    /// below; most checks look simpler than they are for a verified reason.
    ///
    /// ── The four mood categories (never mix two for the same ThoughtDef) ──
    ///
    /// • ONGOING (weather, game conditions, raid present, fire, food, power, party…)
    ///   Owned by this component's tick loop. While the condition is active the
    ///   memory is refreshed on every AWAKE toddler (<see cref="RefreshMemory"/>,
    ///   which renews the existing memory instead of allocating a new one). The
    ///   moment the condition ends it is removed from EVERY spawned toddler —
    ///   asleep ones included (the old code only removed from awake toddlers, so a
    ///   toddler who slept through the end of a raid woke up still "scared").
    ///   Sleeping toddlers are never refreshed: if the condition outlasts their
    ///   sleep, the memory may expire naturally and is re-applied when they wake.
    ///
    /// • LINGERING (Burial, and the three "witnessed" moods: Surgery, Butchering,
    ///   Social Fight). Refreshed while the trigger is active, but NEVER
    ///   force-removed — the memory decays over its full durationDays from the
    ///   last refresh. Witnessed moods are per-toddler: only awake toddlers who
    ///   can actually see the event (<see cref="CanWitness"/>) get them.
    ///
    /// • ONE-TIME, edge-detected here: RaidWon (falling edge of "active threat on
    ///   map"), NewColonist (via <see cref="GameComponent_KnownColonists"/>),
    ///   ToddlerInjured, ToddlerRescued, CorpseObserved, ObservedBlood, Homesick.
    ///   Applied once to toddlers who are awake at that moment (user-confirmed:
    ///   sleeping toddlers do not "catch up" on news when they wake). Exception:
    ///   ToddlerRescued is about the toddler itself and is applied even if the
    ///   toddler is unconscious.
    ///
    /// • ONE-TIME, fired from Harmony patches in HarmonyPatches.cs (never polled
    ///   here): ColonistDied (Pawn.Kill), AnimalTamed (DoRecruit), Birthday
    ///   (Pawn_AgeTracker.BirthdayBiological), DiseaseOutbreak + every
    ///   incident-based mood (IncidentWorker.TryExecute). Those patches call the
    ///   shared <see cref="ApplyToAwakeToddlers"/> / <see cref="RefreshMemory"/>
    ///   helpers below so "which toddlers are eligible" has one definition.
    ///
    /// ── Performance ──
    /// Runs every <see cref="CheckInterval"/> ticks per map. Reuses scratch buffers,
    /// makes no pawn-list copies, and scans spawned pawns once for toddler discovery
    /// and once for all job/mental-state checks. Maps without toddlers skip the
    /// second pass. Blood observation scans the smaller of blood Things and nearby cells.
    /// </summary>
    public class MapComponent_ToddlerEventMoods : MapComponent
    {
        // ═════════════════════════════════════════════════════════════════════
        //  TUNING CONSTANTS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tick interval for the main pass (~2.5 s at 1×). ONGOING thoughts last
        /// 0.125 days (7 500 ticks), so they are refreshed ≈50× before they could
        /// expire while their condition holds.
        /// </summary>
        private const int CheckInterval = 150;

        /// <summary>Cadence of the (comparatively expensive) corpse / blood line-of-sight scans.</summary>
        private const int SightScanInterval = 1500;

        /// <summary>
        /// "Witnessed" moods (surgery, butchering, social fight): the toddler must be
        /// in the same indoor room as the event, or within this many cells of it with
        /// line of sight (user-confirmed rule).
        /// </summary>
        private const float WitnessRadius = 10f;

        /// <summary>A hostile active threat this close (with line of sight) triggers "A scary person is close!".</summary>
        private const float ScaryPersonRadius = 8f;

        /// <summary>
        /// Fire scares a toddler if it is anywhere in the home area, or within this
        /// many cells of the toddler (user-confirmed rule — distant wildfires no
        /// longer count).
        /// </summary>
        private const float NearbyFireRadius = 15f;

        /// <summary>Sight radius for the corpse-observed scan.</summary>
        private const float CorpseSightRadius = 8f;

        /// <summary>Sight radius for the observed-blood scan.</summary>
        private const float BloodSightRadius = 5f;

        /// <summary>An injury younger than this (ticks) counts as "just happened" for ToddlerInjured.</summary>
        private const int FreshInjuryMaxAgeTicks = CheckInterval + 50;

        /// <summary>Minimum injury severity for ToddlerInjured (filters trivial scratches).</summary>
        private const float FreshInjuryMinSeverity = 0.1f;

        /// <summary>Homesick roll: chance per check, and cooldown (20 days) after it fires.</summary>
        private const float HomesickChancePerCheck = 0.02f;
        private const int HomesickCooldownTicks = 1200000;

        /// <summary>
        /// Food shortage threshold: stored human-edible nutrition below this × free
        /// colonists (spawned). Vanilla's Alert_LowFood uses 4 for "low"; this mood
        /// is "there's no food!", so it deliberately uses the lower 2.
        /// </summary>
        private const float FoodShortageNutritionPerColonist = 2f;

        /// <summary>Minimum weather rain/snow rate counted as "it's raining/snowing".</summary>
        private const float PrecipitationThreshold = 0.1f;

        /// <summary>
        /// Copy of vanilla's private PowerNet.MinStoredEnergyToTurnOn (5 Wd): with
        /// batteries present, vanilla keeps switched-off parts off until the stored
        /// energy (minus this) plus the gain rate is non-negative.
        /// </summary>
        private const float VanillaMinStoredEnergyToTurnOn = 5f;

        // ═════════════════════════════════════════════════════════════════════
        //  MENTAL-BREAK WHITELIST for "Someone went crazy!"
        // ═════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Exact set of mental-state defNames that cause toddlers to think
        /// "Someone went crazy!" when an adult humanlike colony member (colonist or
        /// slave — "Rebellion" is slave-only) enters them. Built from the user's
        /// explicit list; anything NOT on this list is ignored (e.g. toddler
        /// crying, food binges, catatonia). All names verified to exist in
        /// Core/DLC MentalStateDefs.
        /// </summary>
        private static readonly HashSet<string> ViolentMentalBreakDefNames = new HashSet<string>
        {
            // Aggro / Malicious (all "red" breaks except BerserkWarcall)
            "Berserk",
            "BerserkShort",
            "FireStartingSpree",
            "Jailbreaker",
            "Slaughterer",
            "MurderousRage",
            "Tantrum",
            "TargetedTantrum",
            "BedroomTantrum",
            "SadisticRage",
            "InsultingSpree",
            "TargetedInsultingSpree",
            "EntityKiller",
            "EntityLiberator",
            "Rebellion",
            // Additional user-specified breaks
            "CorpseObsession",
            "Binging_DrugExtreme",
            "Binging_DrugMajor",
            "Wander_Psychotic",
            "Wander_Sad",
            "Wander_OwnRoom",
            "DarkVisions",
            "InsaneRamblings",
            "WildDecree"
        };

        // ═════════════════════════════════════════════════════════════════════
        //  PERSISTENT (scribed) STATE
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Last check's "any active hostile threat" state — detects the raid-ended edge.</summary>
        private bool wasHostile;

        /// <summary>toddler thingIDNumber → tick before which the homesick roll is suppressed.</summary>
        private Dictionary<int, int> homesickCooldownTicks = new Dictionary<int, int>();

        /// <summary>
        /// (toddler, corpse) pairs already seen — see <see cref="PairKey"/>. PER
        /// TODDLER (the old global "seen corpse" set meant only the first toddler
        /// to spot a corpse ever reacted to it). Pruned whenever the corpse is no
        /// longer spawned, so it stays small.
        /// </summary>
        private HashSet<long> seenCorpsePairs = new HashSet<long>();

        /// <summary>
        /// (toddler, blood filth Thing) pairs already seen. Keyed by the filth
        /// THING, not the cell: the old global per-cell set blacklisted a tile for
        /// every toddler forever, even for fresh blood spilled there later.
        /// Pruned whenever the filth is gone.
        /// </summary>
        private HashSet<long> seenBloodPairs = new HashSet<long>();

        /// <summary>
        /// Per-toddler thunderstorm mood choice (true = Fascinating, false = Scary),
        /// rolled ONCE per storm and held steady; the whole dictionary is cleared
        /// when the storm ends so the next storm re-rolls (re-rolling every check
        /// used to stack both the +5 and the −6 thought).
        /// </summary>
        private Dictionary<int, bool> thunderstormMoodChoice = new Dictionary<int, bool>();

        /// <summary>Elapsed-time schedule cursors for the sight scans (−1 = not yet initialised). Elapsed time, not TicksGame % N, so time-skips/loads can't silence them.</summary>
        private int nextCorpseCheckTick = -1;
        private int nextBloodCheckTick = -1;

        // ═════════════════════════════════════════════════════════════════════
        //  RUNTIME-ONLY STATE (deliberately not saved)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Toddlers seen DOWNED and NOT in a bed. When one of them is next seen
        /// downed AND in a bed, someone rescued them → "Someone saved me!". Kept as
        /// Pawn references (not rebuilt each check) so a toddler who is despawned
        /// while being carried to bed is still remembered when set down. Not saved:
        /// after a load the worst case is one missed rescue mood.
        /// </summary>
        private readonly HashSet<Pawn> downedOutsideBed = new HashSet<Pawn>();

        /// <summary>Set once all event moods were cleared after the setting was switched off (so the cleanup runs once, not every check).</summary>
        private bool clearedAfterDisable;

        /// <summary>Lazily cached per-game component (a MapComponent always belongs to the current Game).</summary>
        private GameComponent_KnownColonists? knownColonists;

        // ── Power-outage tracking (see EvaluatePowerOutage) ──
        // Double-buffered: each evaluation fills the *Next sets, then swaps them in,
        // so nothing is allocated per check. Comps are keyed by reference.
        /// <summary>Player consumers seen RUNNING at the previous evaluation. Runtime-only.</summary>
        private HashSet<CompPowerTrader> runningConsumers = new HashSet<CompPowerTrader>();
        private HashSet<CompPowerTrader> runningConsumersNext = new HashSet<CompPowerTrader>();
        /// <summary>Player consumers the grid cut off for lack of power and that still can't be powered. Saved (see ExposeData).</summary>
        private HashSet<CompPowerTrader> cutOffConsumers = new HashSet<CompPowerTrader>();
        private HashSet<CompPowerTrader> cutOffConsumersNext = new HashSet<CompPowerTrader>();
        /// <summary>Save/load bridge for <see cref="cutOffConsumers"/> (comps can't be referenced directly; their Things can). Null outside ExposeData.</summary>
        private List<Thing>? scribedCutOffThings;

        // ═════════════════════════════════════════════════════════════════════
        //  STATIC SCRATCH BUFFERS — cleared at the start of every use, so no
        //  state can leak between maps or between saves (lore: scribe-saving).
        // ═════════════════════════════════════════════════════════════════════
        private static readonly List<Pawn> tmpToddlers = new List<Pawn>();
        private static readonly List<bool> tmpToddlerAwake = new List<bool>();
        private static readonly List<IntVec3> tmpThreatCells = new List<IntVec3>();
        private static readonly List<IntVec3> tmpOperationCells = new List<IntVec3>();
        private static readonly List<IntVec3> tmpButcheringCells = new List<IntVec3>();
        private static readonly List<IntVec3> tmpSocialFightCells = new List<IntVec3>();
        private static readonly List<Pawn> tmpPawnsToForget = new List<Pawn>();
        private static readonly List<Corpse> tmpHumanlikeCorpses = new List<Corpse>();
        private static readonly List<Thing> tmpSparseBlood = new List<Thing>();
        private static readonly HashSet<int> tmpLiveTargetIds = new HashSet<int>();

        /// <summary>
        /// RemoveWhere predicate for the (toddler, target) pair sets: drops pairs whose
        /// target ID is not in <see cref="tmpLiveTargetIds"/>. References only static
        /// state, so the compiler caches ONE delegate (no per-call allocation).
        /// </summary>
        private static readonly Predicate<long> PairTargetGone =
            key => !tmpLiveTargetIds.Contains((int)(key & 0xFFFFFFFFL));

        // ═════════════════════════════════════════════════════════════════════
        //  STATIC DEF CACHE (resolved once per process — defs never change at runtime)
        // ═════════════════════════════════════════════════════════════════════
        private static bool defsCached;
        private static ThingDef? tornadoDef;
        private static ThingDef? blightDef;
        private static WeatherDef? rainyThunderstormDef;
        private static WeatherDef? dryThunderstormDef;
        /// <summary>Every filth ThingDef whose defName contains "blood" (Filth_Blood, Filth_BloodInsect, Filth_BloodSmear, modded blood).</summary>
        private static readonly HashSet<ThingDef> bloodFilthDefs = new HashSet<ThingDef>();
        private static readonly List<ThingDef> bloodFilthDefList = new List<ThingDef>();
        /// <summary>All mood defs this system can apply — used to clear them when the setting is turned off.</summary>
        private static readonly List<ThoughtDef> allEventThoughtDefs = new List<ThoughtDef>();
        /// <summary>LordJob type → "is a (modded) marriage ceremony" (name contains "Marriage"). Avoids a reflection string test per lord per check.</summary>
        private static readonly Dictionary<Type, bool> marriageLordTypeCache = new Dictionary<Type, bool>();

        private static void EnsureCachedDefs()
        {
            if (defsCached) return;
            defsCached = true;

            tornadoDef = DefDatabase<ThingDef>.GetNamed("Tornado", false);
            blightDef = DefDatabase<ThingDef>.GetNamed("Blight", false);
            rainyThunderstormDef = DefDatabase<WeatherDef>.GetNamed("RainyThunderstorm", false);
            dryThunderstormDef = DefDatabase<WeatherDef>.GetNamed("DryThunderstorm", false);

            // Blood filth: same name rule the old per-tick string test used
            // (defName contains "Blood"/"blood"), evaluated ONCE here instead of
            // twice per filth per cell per toddler.
            List<ThingDef> allThings = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < allThings.Count; i++)
            {
                ThingDef d = allThings[i];
                if (d.thingClass == null || !typeof(Filth).IsAssignableFrom(d.thingClass)) continue;
                if (d.defName.IndexOf("blood", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (bloodFilthDefs.Add(d)) bloodFilthDefList.Add(d);
            }

            // Every TPE_Event_* ThoughtDef (+ the homesick mood this component also
            // owns), discovered by reflection so a future event mood is included
            // automatically.
            foreach (FieldInfo f in typeof(TPE_ThoughtDefOf).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType != typeof(ThoughtDef)) continue;
                if (!f.Name.StartsWith("TPE_Event_", StringComparison.Ordinal) &&
                    f.Name != nameof(TPE_ThoughtDefOf.TPE_AdoptedToddlerHomesick)) continue;
                if (f.GetValue(null) is ThoughtDef td) allEventThoughtDefs.Add(td);
            }
        }

        public MapComponent_ToddlerEventMoods(Map map) : base(map)
        {
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MAIN PASS
        // ═════════════════════════════════════════════════════════════════════
        public override void MapComponentTick()
        {
            if (!map.IsHashIntervalTick(CheckInterval)) return;

            TPE_Settings? settings = TPE_Mod.Settings;
            if (settings == null) return;

            EnsureCachedDefs();

            // ── Setting switched OFF: clear every event mood once, then idle. ──
            // Edge state is reset too, so turning the setting back on can't fire a
            // stale "the bad guys went away!" or reuse an old storm roll.
            if (!settings.enableEventTriggeredMoods)
            {
                if (!clearedAfterDisable)
                {
                    RemoveAllEventMoodsOnMap();
                    clearedAfterDisable = true;
                }
                wasHostile = false;
                thunderstormMoodChoice.Clear();
                downedOutsideBed.Clear();
                return;
            }

            if (knownColonists == null)
                knownColonists = Current.Game?.GetComponent<GameComponent_KnownColonists>();

            // Setting just switched back ON: colonists who joined while it was off
            // were never registered. Register them SILENTLY now, otherwise they would
            // all be announced as "a new person came to play!" on this check.
            if (clearedAfterDisable)
            {
                knownColonists?.RegisterColonistsOn(map);
                clearedAfterDisable = false;
            }

            // ── Known-colonist bookkeeping runs even with no toddlers present. ──
            // The old code only registered colonists while an awake toddler existed,
            // so a colonist who joined while every toddler slept (or before any
            // toddler existed) was announced as "new" hours later. Cheap: one pass
            // over the map's spawned player-faction pawns + HashSet.Add each.
            bool newColonistArrived = knownColonists != null && knownColonists.RegisterColonistsOn(map);

            CollectToddlers();
            if (tmpToddlers.Count == 0)
            {
                // Nobody to apply anything to. Reset edge state so a toddler who
                // appears later never receives a stale one-time mood (e.g. RaidWon
                // for a raid that ended long before they were spawned).
                wasHostile = false;
                thunderstormMoodChoice.Clear();
                // downedOutsideBed is NOT cleared: a downed toddler being carried to
                // bed is unspawned right now and must still be recognised when set
                // down. It is pruned on the sight-scan cadence instead.
                if (downedOutsideBed.Count > 0) PruneDownedOutsideBed();
                return;
            }

            Faction ofPlayer = Faction.OfPlayer;
            int now = Find.TickManager.TicksGame;

            UpdateWeatherAndConditionMoods();
            UpdateThreatMoods(ofPlayer);
            UpdatePawnActivityMoods(ofPlayer);
            UpdateColonyStateMoods();
            UpdatePerToddlerMoods();

            // ── NEW COLONIST — ONE-TIME (edge computed above) ──
            if (newColonistArrived)
                ApplyToAwake(TPE_ThoughtDefOf.TPE_Event_NewColonist);

            UpdateSightScans(now);
            UpdateHomesick(now);
        }

        /// <summary>
        /// Fills <see cref="tmpToddlers"/> with every spawned, living toddler on the
        /// map (awake OR asleep — asleep ones still need ONGOING moods removed) and
        /// <see cref="tmpToddlerAwake"/> with each one's awake state, captured once
        /// so every section of this check sees the same answer. Read the maintained
        /// spawned registry directly: AllHumanlikeSpawned rebuilds another list.
        /// </summary>
        private void CollectToddlers()
        {
            tmpToddlers.Clear();
            tmpToddlerAwake.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p.Dead || !p.RaceProps.Humanlike || !ToddlerUtility.IsToddler(p)) continue;
                tmpToddlers.Add(p);
                tmpToddlerAwake.Add(p.Awake());
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  WEATHER + GAME CONDITIONS — ONGOING
        // ═════════════════════════════════════════════════════════════════════
        private void UpdateWeatherAndConditionMoods()
        {
            WeatherDef? curWeather = map.weatherManager.curWeather;
            GameConditionManager conds = map.gameConditionManager;

            // ── BUGFIX (game conditions) ──────────────────────────────────────
            // The old code read Find.World.gameConditionManager.ActiveConditions —
            // the WORLD list only. Toxic fallout, volcanic winter, heat wave, cold
            // snap and flashstorm are MAP-targeted incidents (targetTags
            // Map_PlayerHome) that IncidentWorker_MakeGameCondition registers on
            // the map's own manager, so those five moods could never fire.
            // map.gameConditionManager.ConditionIsActive checks the map's list AND
            // its parent (the world's) — exactly what vanilla's own
            // ThoughtWorker_GameCondition does — so world-level conditions (aurora,
            // eclipse) still work. Allocation-free (no LINQ).
            //
            // The original intent is kept per condition: TYPE-based where the old
            // code was type-based (GetActiveCondition<T> also walks map → world), so
            // same-class map conditions count too — Royalty's Toxic Spewer
            // (GameCondition_ToxicFallout), Giant Smoke Cloud (GameCondition_SmokeSpewer
            // : GameCondition_VolcanicWinter) and Odyssey's Deep Freeze
            // (GameCondition_ColdSnap) — and DEF-based where it was name-based.
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_Aurora, conds.GetActiveCondition<GameCondition_Aurora>() != null);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_ToxicFallout, conds.GetActiveCondition<GameCondition_ToxicFallout>() != null);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_HeatWave, conds.GetActiveCondition<GameCondition_HeatWave>() != null);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_ColdSnap, conds.GetActiveCondition<GameCondition_ColdSnap>() != null);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_VolcanicWinter, conds.GetActiveCondition<GameCondition_VolcanicWinter>() != null);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_Eclipse, conds.ConditionIsActive(GameConditionDefOf.Eclipse));
            // (The separate Solar Flare mood was REMOVED on user request — a flare that
            // really switches the colony's power off is now covered by the Power Outage
            // mood, which also stays silent when a mod keeps the power on through it.
            // See EvaluatePowerOutage.)

            // Thunderstorm / DryThunderstorm are WeatherDefs, not GameConditions
            // (earlier fix — keep). Flashstorm IS a (map-level) GameCondition.
            bool isThunderstorm = curWeather != null && curWeather == rainyThunderstormDef;
            bool isDryThunderstorm = curWeather != null && curWeather == dryThunderstormDef;
            bool isFlashstorm = conds.ConditionIsActive(GameConditionDefOf.Flashstorm);

            // ── BUGFIX (rain/snow after a storm) ─────────────────────────────
            // WeatherManager.RainRate/SnowRate LERP from the previous weather over
            // the transition. When a thunderstorm gave way to clear skies the lerped
            // rain rate stayed > 0.1 for a while after curWeather stopped being a
            // thunderstorm, so the plain "It's raining!" mood popped up AFTER the
            // storm ended. Reading the CURRENT weather's own rates follows the game's
            // decision of what the weather is right now.
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_SnowStarted,
                curWeather != null && curWeather.snowRate > PrecipitationThreshold);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_RainStarted,
                curWeather != null && curWeather.rainRate > PrecipitationThreshold &&
                !isThunderstorm && !isDryThunderstorm && !isFlashstorm);

            // Thunderstorm mood: fascination XOR fear, rolled ONCE per toddler per
            // storm (earlier fix — keep). Only awake toddlers roll/receive; removal
            // and the roll cache reset cover every toddler when the storm ends.
            if (isThunderstorm)
            {
                for (int i = 0; i < tmpToddlers.Count; i++)
                {
                    if (!tmpToddlerAwake[i]) continue;
                    Pawn toddler = tmpToddlers[i];
                    int id = toddler.thingIDNumber;
                    if (!thunderstormMoodChoice.TryGetValue(id, out bool isFascinated))
                    {
                        isFascinated = Rand.Chance(0.5f);
                        thunderstormMoodChoice[id] = isFascinated;
                    }
                    MaintainOrRemoveForPawn(toddler, TPE_ThoughtDefOf.TPE_Event_ThunderstormFascinating, isFascinated, true);
                    MaintainOrRemoveForPawn(toddler, TPE_ThoughtDefOf.TPE_Event_ThunderstormScary, !isFascinated, true);
                }
            }
            else
            {
                MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_ThunderstormFascinating, false);
                MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_ThunderstormScary, false);
                // Clear the WHOLE cache (the old code only cleared awake toddlers'
                // entries, so a toddler asleep at the storm's end reused last
                // storm's coin flip).
                thunderstormMoodChoice.Clear();
            }

            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_Flashstorm, isFlashstorm);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_DryThunderstorm, isDryThunderstorm);

            // Blight / Tornado are spawned Things (earlier fix — keep). ThingsOfDef
            // returns the lister's own list (no allocation); Count > 0 is O(1).
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_Blight,
                blightDef != null && map.listerThings.ThingsOfDef(blightDef).Count > 0);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_Tornado,
                tornadoDef != null && map.listerThings.ThingsOfDef(tornadoDef).Count > 0);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RAID / HOSTILES — RaidActive ONGOING, RaidWon ONE-TIME
        // ═════════════════════════════════════════════════════════════════════
        private void UpdateThreatMoods(Faction ofPlayer)
        {
            // ── BUGFIX ───────────────────────────────────────────────────────
            // The old test was "any spawned pawn of a hostile faction that isn't a
            // prisoner". That also counted DOWNED raiders bleeding out, SLEEPING
            // enemies, DORMANT mech clusters, insects passively guarding a hive,
            // and enemies hidden in fog — a dormant mech cluster or an uncleared
            // hive meant "scary noises outside" forever and "the bad guys went
            // away!" never. Vanilla's own definition of "an active threat" is
            // GenHostility.IsActiveThreatToPlayer (ThreatDisabled covers downed /
            // inactive; IsPotentialThreat covers prisoners, asleep, dormant,
            // deactivated, fleeing; hive defenders and fog are excluded too).
            // Iterating the engine's maintained attack-target cache (only things
            // hostile to the player) is also far cheaper than scanning every pawn.
            // Faction != null keeps manhunter animals / wild men out — they have
            // their own moods, as before.
            tmpThreatCells.Clear();
            foreach (IAttackTarget target in map.attackTargetsCache.TargetsHostileToFaction(ofPlayer))
            {
                if (!(target.Thing is Pawn hostile) || hostile.Faction == null) continue;
                if (!GenHostility.IsActiveThreatToPlayer(target)) continue;
                tmpThreatCells.Add(hostile.Position);
            }
            bool isHostile = tmpThreatCells.Count > 0;

            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_RaidActive, isHostile);

            // One-time: "the bad guys went away!" — only toddlers awake at the
            // moment it ends (wasHostile is reset whenever the map has no
            // toddlers, so this edge can never be stale).
            if (wasHostile && !isHostile)
                ApplyToAwake(TPE_ThoughtDefOf.TPE_Event_RaidWon);
            wasHostile = isHostile;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ONE PASS OVER ALL SPAWNED PAWNS — manhunters, mental breaks, burial,
        //  surgery, butchering, social fights
        // ═════════════════════════════════════════════════════════════════════
        private void UpdatePawnActivityMoods(Faction ofPlayer)
        {
            int manhunterCount = 0;
            bool anyViolentMentalBreak = false;
            bool anyColonyMemberBurial = false;
            tmpOperationCells.Clear();
            tmpButcheringCells.Clear();
            tmpSocialFightCells.Clear();

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned; // engine's own list, no copy
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                RaceProperties race = p.RaceProps;

                // Manhunter animals (pack = 2+, lone = 1) — unchanged rule.
                if (race.Animal)
                {
                    if (p.InMentalState)
                    {
                        MentalStateDef state = p.MentalStateDef;
                        if (state == MentalStateDefOf.Manhunter || state == MentalStateDefOf.ManhunterPermanent)
                            manhunterCount++;
                    }
                    continue; // animals don't operate, butcher, fight socially, bury, or count as "someone went crazy"
                }

                // "Someone went crazy!" — adult humanlike COLONY MEMBER (player faction:
                // colonists AND slaves, matching the old FreeColonists source, which
                // includes slaves) in a whitelisted break.
                if (!anyViolentMentalBreak && race.Humanlike && p.Faction == ofPlayer && p.InMentalState &&
                    p.DevelopmentalStage == DevelopmentalStage.Adult)
                {
                    MentalStateDef state = p.MentalStateDef;
                    if (state != null && ViolentMentalBreakDefNames.Contains(state.defName))
                        anyViolentMentalBreak = true;
                }

                Job? job = p.CurJob;
                if (job == null) continue;
                JobDef jobDef = job.def;

                if (jobDef == JobDefOf.DoBill)
                {
                    CollectBillWitnessCells(p, job);
                }
                else if (jobDef == JobDefOf.SocialFight)
                {
                    // Witness-based now (see UpdatePerToddlerMoods).
                    tmpSocialFightCells.Add(p.Position);
                }
                else if (!anyColonyMemberBurial && jobDef == JobDefOf.HaulToContainer &&
                         job.targetB.Thing is Building_Grave &&
                         job.targetA.Thing is Corpse corpse && IsColonyMemberCorpse(corpse, ofPlayer))
                {
                    // ── USER-REQUESTED CHANGE ──
                    // Burial = hauling a corpse to a grave (earlier fix: there is no
                    // "Bury" JobDef; mirrors JobDriver_HaulToContainer.GetReport).
                    // Now ONLY corpses of colony members (colonists and slaves) — the
                    // old rule counted raiders and animals too, so every raid cleanup
                    // gave toddlers 5 hours of grief. Non-humanlike haulers (e.g.
                    // Biotech hauler mechs) are deliberately still counted.
                    anyColonyMemberBurial = true;
                }
            }

            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_ManhunterPack, manhunterCount >= 2);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_MadAnimal, manhunterCount == 1);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_MentalBreak, anyViolentMentalBreak);

            // LINGERING: refreshed while a colony member is being carried to a
            // grave, never force-removed (5-hour linger after the last refresh).
            if (anyColonyMemberBurial)
                ApplyToAwake(TPE_ThoughtDefOf.TPE_Event_Burial);
        }

        /// <summary>
        /// For a pawn doing a DoBill job: records where a toddler would have to be
        /// to WITNESS a surgery or the butchering of a humanlike corpse.
        ///
        /// ── BUGFIX (surgery — the "almost always on" mood) ──
        /// The old rule fired map-wide for every toddler whenever ANY pawn had a
        /// TakeToBedToOperate job or a DoBill job with a Bill_Medical — i.e. while a
        /// doctor walked across the map to fetch medicine, while carrying the patient,
        /// for drug administration / blood transfusions / hemogen extraction, and for
        /// animal surgery. Now: only while the doctor is in the actual recipe-work
        /// toil (<c>JobDriver_DoBill.ticksSpentDoingRecipeWork</c> only increments
        /// inside Toils_Recipe.DoRecipeWork), only for recipes that anesthetize
        /// (vanilla generates every "Administer X" recipe with anesthetize = false,
        /// and transfusion/hemogen recipes set it false too), and only toddlers who
        /// can see the patient get the mood.
        ///
        /// ── BUGFIX (butchering) ──
        /// The old rule matched ANY bill at a bench whose Position cell held a
        /// humanlike corpse — so cremation counted — and it only looked at the
        /// bench's root cell, while ingredients are placed on any cell the bench
        /// occupies (2/3 of butcher-table butchering was missed). Now: only recipes
        /// whose specialProducts contain Butchery, only during the recipe-work toil,
        /// and the corpse is read from the job's own placed ingredients.
        /// </summary>
        private static void CollectBillWitnessCells(Pawn worker, Job job)
        {
            Bill? bill = job.bill;
            RecipeDef? recipe = bill?.recipe;
            if (recipe == null) return;

            // Actually working right now? (false while fetching ingredients/walking)
            if (!(worker.jobs?.curDriver is JobDriver_DoBill driver) || driver.ticksSpentDoingRecipeWork <= 0) return;

            if (bill is Bill_Medical)
            {
                // For a surgery the bill giver (TargetIndex.A) is the patient.
                if (recipe.anesthetize && job.targetA.Thing is Pawn patient && patient.Spawned)
                    tmpOperationCells.Add(patient.Position);
            }
            else if (IsButcheryRecipe(recipe) && JobHasHumanlikeCorpseIngredient(job))
            {
                tmpButcheringCells.Add(worker.Position);
            }
        }

        private static bool IsButcheryRecipe(RecipeDef recipe)
        {
            List<SpecialProductType>? special = recipe.specialProducts;
            return special != null && special.Contains(SpecialProductType.Butchery);
        }

        /// <summary>
        /// True if the DoBill job's ingredient is a humanlike corpse. During recipe
        /// work the ingredients sit in <c>job.placedThings</c> (filled by
        /// Toils_Haul.PlaceHauledThingInCell); targetB (last hauled ingredient) is
        /// checked as a fallback.
        /// </summary>
        private static bool JobHasHumanlikeCorpseIngredient(Job job)
        {
            List<ThingCountClass>? placed = job.placedThings;
            if (placed != null)
            {
                for (int i = 0; i < placed.Count; i++)
                {
                    if (placed[i]?.thing is Corpse c && IsHumanlikeCorpse(c)) return true;
                }
            }
            return job.targetB.Thing is Corpse fallback && IsHumanlikeCorpse(fallback);
        }

        private static bool IsHumanlikeCorpse(Corpse corpse)
        {
            Pawn? inner = corpse.InnerPawn;
            return inner != null && inner.RaceProps.Humanlike;
        }

        /// <summary>
        /// Colony member = colonist or slave (user-confirmed scope for Burial): a
        /// humanlike whose Faction is the player's (slaves keep the player faction;
        /// prisoners keep their own, so they're excluded). A pawn keeps its Faction
        /// after death — vanilla's Alert_ColonistLeftUnburied relies on exactly this
        /// test. Like that alert, temporary quest lodgers and subhumans (ghouls etc.)
        /// are excluded; unlike it, slaves are deliberately INCLUDED.
        /// </summary>
        private static bool IsColonyMemberCorpse(Corpse corpse, Faction ofPlayer)
        {
            Pawn? inner = corpse.InnerPawn;
            return inner != null && inner.RaceProps.Humanlike && inner.Faction == ofPlayer &&
                   !inner.IsSubhuman && !inner.IsQuestLodger();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  COLONY STATE — ONGOING (lords, food, power)
        // ═════════════════════════════════════════════════════════════════════
        private void UpdateColonyStateMoods()
        {
            // ── Lord-based events: one pass over the map's lords ──
            bool party = false, wedding = false, prisonBreak = false;
            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                LordJob? lordJob = lords[i].LordJob;
                if (lordJob == null) continue; // null-safe (earlier fix — keep)

                if (lordJob is LordJob_Joinable_MarriageCeremony) wedding = true;
                else if (lordJob is LordJob_Joinable_Party) party = true;    // incl. LordJob_Joinable_Concert (subclass)
                else if (lordJob is LordJob_PrisonBreak) prisonBreak = true;
                else if (!wedding && IsModdedMarriageLord(lordJob)) wedding = true; // modded ceremonies (old name fallback)
            }
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_Wedding, wedding);
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_Party, party);
            // ── BUGFIX (double source) ──
            // Prison break used to be applied BOTH here (ongoing, lord present) AND by
            // a Harmony postfix on PrisonBreakUtility.StartPrisonBreak (one-time) —
            // the exact ongoing/one-time mixing Architecture.md forbids. The patch was
            // removed; this ongoing lord check is now the sole source.
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_PrisonBreak, prisonBreak);

            // ── Food shortage ──
            // BUGFIX: home maps only, like vanilla Alert_LowFood. ResourceCounter only
            // counts STORED food, so on a caravan camp / quest map (food carried in
            // inventories, no stockpiles) this used to claim "There's no food!" the
            // whole time. Counts SPAWNED free colonists, also like vanilla.
            MaintainOrRemove(TPE_ThoughtDefOf.TPE_Event_FoodShortage,
                map.IsPlayerHome &&
                map.resourceCounter.TotalHumanEdibleNutrition <
                    map.mapPawns.FreeColonistsSpawnedCount * FoodShortageNutritionPerColonist);

            // ── Power outage — per toddler (tech-level gate), see UpdatePowerOutageMood ──
            UpdatePowerOutageMood();
        }

        private static bool IsModdedMarriageLord(LordJob lordJob)
        {
            Type type = lordJob.GetType();
            if (!marriageLordTypeCache.TryGetValue(type, out bool isMarriage))
            {
                isMarriage = type.Name.Contains("Marriage");
                marriageLordTypeCache[type] = isMarriage;
            }
            return isMarriage;
        }

        /// <summary>
        /// Power outage mood, per toddler.
        ///
        /// ── USER-REQUESTED (tech level) ──
        /// Never shown to toddlers whose faction is tribal/neolithic, medieval (or the
        /// "animal" level below them): they have no concept of electric lights. Judged
        /// by the toddler's OWN faction def's techLevel, so a tribal-start colony
        /// (PlayerTribe = Neolithic) never shows it even after researching electricity
        /// (user-confirmed). Undefined/Industrial+ factions (incl. modded player
        /// factions that leave techLevel unset) are NOT excluded.
        ///
        /// PERF: if no toddler on the map is eligible (a tribal colony) the power scan
        /// is skipped entirely; ineligible toddlers just get the memory removed
        /// (alloc-free pre-checked RemoveMemory).
        /// </summary>
        private void UpdatePowerOutageMood()
        {
            ThoughtDef? outageDef = TPE_ThoughtDefOf.TPE_Event_PowerOutage;
            if (outageDef == null) return;

            bool anyEligible = false;
            for (int i = 0; i < tmpToddlers.Count; i++)
            {
                if (!IsLowTechToddler(tmpToddlers[i])) { anyEligible = true; break; }
            }

            bool outage = anyEligible && EvaluatePowerOutage();

            for (int i = 0; i < tmpToddlers.Count; i++)
            {
                Pawn toddler = tmpToddlers[i];
                bool active = outage && !IsLowTechToddler(toddler);
                MaintainOrRemoveForPawn(toddler, outageDef, active, tmpToddlerAwake[i]);
            }
        }

        /// <summary>
        /// Tribal/neolithic, medieval or animal-level faction (see
        /// <see cref="UpdatePowerOutageMood"/>). A faction-less pawn counts as
        /// Undefined → not excluded. Two field reads, no allocation.
        /// </summary>
        private static bool IsLowTechToddler(Pawn toddler)
        {
            TechLevel tech = toddler.Faction?.def?.techLevel ?? TechLevel.Undefined;
            return tech == TechLevel.Animal || tech == TechLevel.Neolithic || tech == TechLevel.Medieval;
        }

        /// <summary>
        /// True while the power has ACTUALLY gone out (user-confirmed rule): the game
        /// has switched off at least one player-owned power consumer that was
        /// RUNNING, because its grid's generation + stored battery energy can't keep up
        /// with demand (or a solar flare / other condition disabled electricity), and
        /// that consumer still can't be powered.
        ///
        /// Why "was running" matters — what this deliberately does NOT count:
        /// • A deficit that batteries are still covering: nothing is switched off, the
        ///   lights are on → no mood.
        /// • A machine that NEVER had power (e.g. a deep drill bigger than the whole
        ///   grid, or a grid that never had a generator): it was never seen running →
        ///   ignored. (The previous rule kept the mood on forever for these.)
        /// • Anything switched off on purpose (flick switch, schedule such as sun lamps
        ///   at night, auto-power) or broken down — FlickUtility.WantsToBeOn and
        ///   IsBrokenDown, the same tests vanilla PowerNet.PowerNetTick uses for
        ///   "parts wanting power on".
        /// • Consumers the grid could power again right now (PowerNet.CanPowerNow
        ///   inequality): vanilla restarts those within a few hundred ticks, so the
        ///   mood ends the moment power is genuinely back.
        /// • Consumers cut off from every grid (a destroyed conduit): not a
        ///   generation-vs-use problem, and they're on no net, so they're never visited.
        /// What it DOES now catch that the previous rule missed: the colony's only
        /// generator being destroyed (the grid loses its power source but its running
        /// lights still get shut off).
        ///
        /// Solar flares: the separate Solar Flare mood was REMOVED (user request — it
        /// duplicated this one and misfired with mods that keep power on during a
        /// flare). A flare now shows up here only if consumers really get shut off:
        /// vanilla's PowerNetTick shuts consumers off while
        /// GameConditionManager.ElectricityDisabled(map) is true, so during that time
        /// a running-then-cut consumer counts regardless of the energy budget. Mods
        /// that keep (some) power on during a flare keep those consumers RUNNING, so
        /// they never count — no mood unless the lights really went out.
        ///
        /// State: <see cref="runningConsumers"/> (seen ON at the previous evaluation) and
        /// <see cref="cutOffConsumers"/> (cut off by a shortage, still unpowered) are
        /// rebuilt every evaluation into double buffers and swapped — no allocation.
        /// cutOffConsumers is saved (as Thing references) so an outage that is in
        /// progress when the game is saved is still recognised after loading.
        ///
        /// Cost: one pass over the map's power comps (a field read + compare each; a
        /// HashSet add per running player consumer); the expensive net energy sum is
        /// computed at most once per net and only when an off, previously running,
        /// still-wanted consumer is found.
        ///
        /// ── Mod compatibility: Vanilla Furniture Expanded – Power (VERIFIED by decompiling
        /// VanillaPowerExpanded.dll + its framework's VEF.dll / PipeSystem.dll; no code
        /// changes were needed — keep these assumptions true if this method is edited) ──
        /// • VFE Power has NO Harmony patches and NO XML patches touching power; it only
        ///   adds generators and batteries.
        /// • Every VFE generator (advanced wind/solar/watermill/geothermal, tidal, wood/
        ///   chemfuel, vanometric cells, souls generator, nuclear reactor) has a negative
        ///   basePowerConsumption, so its PowerOutput is always >= 0 → skipped by the
        ///   consumer filter. The nuclear reactor is a plain CompPowerTrader (not a
        ///   CompPowerPlant) that rewrites PowerOutput every tick, but only ever to 0 or a
        ///   positive value, so it is never mistaken for a consumer either.
        /// • VFE batteries are vanilla CompPowerBattery → counted by
        ///   PowerNet.CurrentStoredEnergy and net.batteryComps exactly like vanilla's.
        /// • Generator loss is caught naturally: a meltdown/breakdown (the reactor
        ///   deliberately breaks down once per solar flare), advanced solar at night, a
        ///   souls generator running out of fuel → the grid can't keep up → vanilla
        ///   switches running consumers off → mood (unless batteries still cover it).
        /// • Framework: VEF powered floors run through a real floor-wire building
        ///   (CompPowerTraderFloor : CompPowerTrader, negative output = its floors' total
        ///   draw) → an ordinary consumer here; VEF CompScheduleExtended IS a CompSchedule,
        ///   so FlickUtility.WantsToBeOn sees it (GetComp<T> matches subclasses) and
        ///   scheduled-off buildings are excluded; PipeSystem's CompPowerTrader_Overclocked
        ///   only varies a consumer's draw, CompPowerPlantNeedResource is a generator.
        /// • VFE's meltdown weather VPE_RadioactiveFog has no rain/snow rate, so it never
        ///   triggers the rain/snow moods.
        /// </summary>
        private bool EvaluatePowerOutage()
        {
            Faction ofPlayer = Faction.OfPlayer;
            bool electricityDisabled = map.gameConditionManager.ElectricityDisabled(map);

            runningConsumersNext.Clear();
            cutOffConsumersNext.Clear();

            List<PowerNet> nets = map.powerNetManager.AllNetsListForReading;
            for (int n = 0; n < nets.Count; n++)
            {
                PowerNet net = nets[n];
                bool budgetKnown = false;
                float budget = 0f;         // gain rate + stored energy, as in PowerNet.CanPowerNow
                bool netCanRestart = false; // vanilla's precondition for turning ANY part back on
                List<CompPowerTrader> comps = net.powerComps;
                for (int c = 0; c < comps.Count; c++)
                {
                    CompPowerTrader comp = comps[c];
                    // Consumers only: PowerOutput is the DESIRED draw (set by
                    // SetUpPowerVars, unchanged when vanilla switches the comp off),
                    // negative for consumers; generators / zero-draw / EMP-stunned are >= 0.
                    if (comp.PowerOutput >= 0f) continue;
                    ThingWithComps parent = comp.parent;
                    // Only the COLONY's lights going out counts — not an abandoned or
                    // ancient grid on a quest/visited map.
                    if (parent.Faction != ofPlayer) continue;

                    if (comp.PowerOn)
                    {
                        runningConsumersNext.Add(comp); // remember for the next evaluation
                        continue;
                    }

                    // OFF consumer. Only one that WAS running (or was already counted as
                    // cut off) can have been cut off by the grid. Cheap hash lookups first.
                    if (!runningConsumers.Contains(comp) && !cutOffConsumers.Contains(comp)) continue;
                    if (parent.IsBrokenDown()) continue;
                    if (!FlickUtility.WantsToBeOn(parent)) continue; // switched / scheduled off on purpose

                    if (!electricityDisabled)
                    {
                        if (!budgetKnown)
                        {
                            float gain = net.CurrentEnergyGainRate();  // honours DebugSettings.unlimitedPower
                            float stored = net.CurrentStoredEnergy();
                            budget = gain + stored;
                            // Mirrors PowerNet.PowerNetTick exactly: vanilla only turns
                            // wanted parts back on when the net isn't in deficit AND
                            // (stored - MinStoredEnergyToTurnOn(5) when it has batteries
                            // holding >= 0.1, else stored) + gain >= 0. Without this the
                            // mood could end while vanilla is still holding the lights
                            // off waiting for the batteries to reach 5 Wd.
                            float restartStored = (net.batteryComps.Count > 0 && stored >= 0.1f) ? stored - VanillaMinStoredEnergyToTurnOn : stored;
                            netCanRestart = budget >= -1E-07f && restartStored + gain >= 0f;
                            budgetKnown = true;
                        }
                        // Same per-part inequality as PowerNet.CanPowerNow(comp): if the
                        // grid can restart parts AND has room for this one, vanilla will
                        // switch it back on within a few hundred ticks — not an outage.
                        if (netCanRestart && budget >= -(comp.EnergyOutputPerTick + 1E-07f)) continue;
                    }

                    cutOffConsumersNext.Add(comp);
                }
            }

            // Swap the double buffers (the "Next" sets become current; the old
            // current sets are reused as next time's scratch).
            (runningConsumers, runningConsumersNext) = (runningConsumersNext, runningConsumers);
            (cutOffConsumers, cutOffConsumersNext) = (cutOffConsumersNext, cutOffConsumers);
            runningConsumersNext.Clear(); // drop stale references (destroyed comps) right away
            cutOffConsumersNext.Clear();

            return cutOffConsumers.Count > 0;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PER-TODDLER — fire, scary person, witnessed events, injury, rescue
        // ═════════════════════════════════════════════════════════════════════
        private void UpdatePerToddlerMoods()
        {
            // ── USER-REQUESTED CHANGE (fire) ──
            // Was "any Fire anywhere on the map" (distant wildfires scared every
            // toddler for hours). Now: any fire in the home area, OR a fire within
            // NearbyFireRadius of the toddler. Home-area test computed once.
            List<Thing> fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
            bool fireInHomeArea = false;
            if (fires.Count > 0)
            {
                Area_Home? home = map.areaManager?.Home; // null-safe for unusual maps
                if (home != null)
                {
                    for (int f = 0; f < fires.Count; f++)
                    {
                        if (home[fires[f].Position]) { fireInHomeArea = true; break; }
                    }
                }
            }

            ThoughtDef? operationDef = TPE_ThoughtDefOf.TPE_Event_MedicalOperation;
            ThoughtDef? butcheringDef = TPE_ThoughtDefOf.TPE_Event_ButcheringHumanlike;
            ThoughtDef? socialFightDef = TPE_ThoughtDefOf.TPE_Event_SocialFight;
            bool anyWitnessable = tmpOperationCells.Count > 0 || tmpButcheringCells.Count > 0 || tmpSocialFightCells.Count > 0;

            for (int i = 0; i < tmpToddlers.Count; i++)
            {
                Pawn toddler = tmpToddlers[i];
                bool awake = tmpToddlerAwake[i];
                IntVec3 pos = toddler.Position;

                // ── Fire — ONGOING per toddler ──
                bool fireNear = fireInHomeArea || (fires.Count > 0 && AnyThingWithin(fires, pos, NearbyFireRadius));
                MaintainOrRemoveForPawn(toddler, TPE_ThoughtDefOf.TPE_Event_FireOnMap, fireNear, awake);

                // ── Scary person nearby — ONGOING, sight-based ──
                // An active threat within ScaryPersonRadius with line of sight. A
                // sleeping toddler can't see anyone, so the mood is removed while
                // asleep (RaidActive still covers "a raid is happening").
                bool scary = awake && tmpThreatCells.Count > 0 && AnyCellInSight(tmpThreatCells, pos, ScaryPersonRadius);
                MaintainOrRemoveForPawn(toddler, TPE_ThoughtDefOf.TPE_Event_ScaryPersonNearby, scary, awake);

                // ── Witnessed events — LINGERING, user-confirmed ──
                // Only awake toddlers who can see it; refreshed while watching,
                // then decays over its normal duration (never force-removed). Social
                // fights last seconds, so the old ONGOING version was removed on the
                // very next check and was practically invisible.
                if (awake && anyWitnessable)
                {
                    Room? room = toddler.GetRoom();
                    if (operationDef != null && WitnessesAny(toddler, room, tmpOperationCells))
                        RefreshMemory(toddler, operationDef);
                    if (butcheringDef != null && WitnessesAny(toddler, room, tmpButcheringCells))
                        RefreshMemory(toddler, butcheringDef);
                    if (socialFightDef != null && WitnessesAny(toddler, room, tmpSocialFightCells))
                        RefreshMemory(toddler, socialFightDef);
                }

                // ── Injury — ONE-TIME (unchanged rule, allocation-free) ──
                // Hediff.ageTicks advances with the pawn's ticks and resets when an
                // injury merges with a new hit, so a < FreshInjuryMaxAgeTicks injury
                // was gained since the last check.
                if (awake && HasFreshInjury(toddler))
                    RefreshMemory(toddler, TPE_ThoughtDefOf.TPE_Event_ToddlerInjured);

                // ── Rescued — ONE-TIME ──
                // BUGFIX: the old rule fired when a toddler STOPPED being downed —
                // even if they recovered alone on the floor — and never at the moment
                // someone actually carried them to safety. Now: downed outside a bed
                // → later seen downed IN a bed (cribs count) = someone rescued them.
                // Recovering on the floor just forgets the entry. Applied even if the
                // toddler is unconscious (it happened to them; the memory lasts a day).
                if (toddler.Downed)
                {
                    if (!toddler.InBed())
                        downedOutsideBed.Add(toddler);
                    else if (downedOutsideBed.Remove(toddler))
                        RefreshMemory(toddler, TPE_ThoughtDefOf.TPE_Event_ToddlerRescued);
                }
                else
                {
                    downedOutsideBed.Remove(toddler);
                }
            }
        }

        /// <summary>Allocation-free "any thing within radius" (squared-distance compare).</summary>
        private static bool AnyThingWithin(List<Thing> things, IntVec3 pos, float radius)
        {
            float radiusSq = radius * radius;
            for (int i = 0; i < things.Count; i++)
            {
                if ((things[i].Position - pos).LengthHorizontalSquared <= radiusSq) return true;
            }
            return false;
        }

        /// <summary>Any of <paramref name="cells"/> within <paramref name="radius"/> of <paramref name="pos"/> AND in line of sight (distance first — LOS is the expensive part).</summary>
        private bool AnyCellInSight(List<IntVec3> cells, IntVec3 pos, float radius)
        {
            float radiusSq = radius * radius;
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                if ((cell - pos).LengthHorizontalSquared > radiusSq) continue;
                if (GenSight.LineOfSight(pos, cell, map)) return true;
            }
            return false;
        }

        /// <summary>True if the toddler can witness any of the event cells (see <see cref="CanWitness"/>).</summary>
        private bool WitnessesAny(Pawn toddler, Room? toddlerRoom, List<IntVec3> eventCells)
        {
            for (int i = 0; i < eventCells.Count; i++)
            {
                if (CanWitness(toddler.Position, toddlerRoom, eventCells[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// User-confirmed witness rule: the toddler is in the same INDOOR room as the
        /// event (<c>Room.ProperRoom</c> — enclosed, not touching the map edge, so
        /// "the outdoors" never counts as one big room), OR within
        /// <see cref="WitnessRadius"/> cells with line of sight (closed doors and
        /// walls block sight). Room lookups are O(1) region-grid reads.
        /// </summary>
        private bool CanWitness(IntVec3 toddlerPos, Room? toddlerRoom, IntVec3 eventCell)
        {
            if (toddlerRoom != null && toddlerRoom.ProperRoom && eventCell.GetRoom(map) == toddlerRoom)
                return true;
            if ((eventCell - toddlerPos).LengthHorizontalSquared > WitnessRadius * WitnessRadius)
                return false;
            return GenSight.LineOfSight(toddlerPos, eventCell, map, skipFirstCell: true);
        }

        private static bool HasFreshInjury(Pawn toddler)
        {
            List<Hediff>? hediffs = toddler.health?.hediffSet?.hediffs;
            if (hediffs == null) return false;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (h is Hediff_Injury && h.Severity > FreshInjuryMinSeverity && h.ageTicks < FreshInjuryMaxAgeTicks)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Drops rescue-tracking entries for toddlers who died, were destroyed, or are
        /// no longer held on this map (e.g. carried off-map by a kidnapper). Manual
        /// loop + static scratch list: no lambda/closure allocation.
        /// </summary>
        private void PruneDownedOutsideBed()
        {
            tmpPawnsToForget.Clear();
            foreach (Pawn p in downedOutsideBed)
            {
                if (p == null || p.Dead || p.Destroyed || p.MapHeld != map)
                    tmpPawnsToForget.Add(p!);
            }
            for (int i = 0; i < tmpPawnsToForget.Count; i++)
                downedOutsideBed.Remove(tmpPawnsToForget[i]);
            tmpPawnsToForget.Clear();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SIGHT SCANS — corpse & blood, ONE-TIME per (toddler, thing)
        // ═════════════════════════════════════════════════════════════════════
        private void UpdateSightScans(int now)
        {
            // Elapsed-time scheduling (earlier fix — keep): first scan right away,
            // blood offset by half an interval so the two scans never share a tick.
            if (nextCorpseCheckTick < 0) nextCorpseCheckTick = now;
            if (nextBloodCheckTick < 0) nextBloodCheckTick = now + SightScanInterval / 2;

            if (now >= nextCorpseCheckTick)
            {
                nextCorpseCheckTick = now + SightScanInterval;
                ScanForCorpses();
                // Piggy-back the rare rescue-set prune on this cadence.
                if (downedOutsideBed.Count > 0) PruneDownedOutsideBed();
            }

            if (now >= nextBloodCheckTick)
            {
                nextBloodCheckTick = now + SightScanInterval;
                ScanForBlood();
            }
        }

        /// <summary>
        /// "There's a dead person!" — an awake toddler sees a HUMANLIKE corpse
        /// (BUGFIX: animal corpses — hunting kills, meat in the freezer — used to
        /// count) within <see cref="CorpseSightRadius"/> with line of sight, that THIS
        /// toddler hasn't seen before (BUGFIX: the seen-set used to be global). Each
        /// newly seen corpse renews the memory. Corpses in graves are unspawned and
        /// never listed.
        /// </summary>
        private void ScanForCorpses()
        {
            List<Thing> corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);

            // Forget pairs whose corpse is gone (buried, destroyed, carried off).
            if (seenCorpsePairs.Count > 0)
            {
                tmpLiveTargetIds.Clear();
                for (int j = 0; j < corpses.Count; j++) tmpLiveTargetIds.Add(corpses[j].thingIDNumber);
                seenCorpsePairs.RemoveWhere(PairTargetGone);
                tmpLiveTargetIds.Clear();
            }
            if (corpses.Count == 0) return;

            ThoughtDef? corpseDef = TPE_ThoughtDefOf.TPE_Event_CorpseObserved;
            if (corpseDef == null) return;

            // Humanlike filter done ONCE (a freezer full of animal corpses would
            // otherwise be re-tested for every toddler).
            tmpHumanlikeCorpses.Clear();
            for (int j = 0; j < corpses.Count; j++)
            {
                if (corpses[j] is Corpse c && IsHumanlikeCorpse(c)) tmpHumanlikeCorpses.Add(c);
            }
            if (tmpHumanlikeCorpses.Count == 0) return;
            float radiusSq = CorpseSightRadius * CorpseSightRadius;

            for (int i = 0; i < tmpToddlers.Count; i++)
            {
                if (!tmpToddlerAwake[i]) continue;
                Pawn toddler = tmpToddlers[i];
                IntVec3 pos = toddler.Position;
                bool sawNew = false;

                for (int j = 0; j < tmpHumanlikeCorpses.Count; j++)
                {
                    Corpse corpse = tmpHumanlikeCorpses[j];
                    IntVec3 corpsePos = corpse.Position;
                    if ((corpsePos - pos).LengthHorizontalSquared > radiusSq) continue;
                    long key = PairKey(toddler, corpse);
                    if (seenCorpsePairs.Contains(key)) continue;
                    if (!GenSight.LineOfSight(pos, corpsePos, map, skipFirstCell: true)) continue;
                    seenCorpsePairs.Add(key);
                    sawNew = true;
                }

                if (sawNew) RefreshMemory(toddler, corpseDef);
            }
            tmpHumanlikeCorpses.Clear(); // drop references so destroyed corpses aren't kept alive
        }

        /// <summary>
        /// "There's blood everywhere!" — an awake toddler sees blood filth within
        /// <see cref="BloodSightRadius"/> that THIS toddler hasn't seen before.
        /// BUGFIXES: (1) the seen-set was a global, never-pruned set of CELLS, so
        /// once any toddler noticed blood on a tile, no toddler ever reacted to that
        /// tile again — even to fresh blood; (2) blood behind walls counted (no line
        /// of sight); (3) the scan allocated an iterator + LINQ per toddler per cell.
        /// </summary>
        private void ScanForBlood()
        {
            int numCells = GenRadial.NumCellsInRadius(BloodSightRadius);
            tmpSparseBlood.Clear();
            int bloodOnMap = 0;
            bool prune = seenBloodPairs.Count > 0;
            if (prune) tmpLiveTargetIds.Clear();
            for (int d = 0; d < bloodFilthDefList.Count; d++)
            {
                List<Thing> things = map.listerThings.ThingsOfDef(bloodFilthDefList[d]);
                bloodOnMap += things.Count;
                // Sparse maps are cheaper to scan by blood Thing than by every nearby cell.
                if (bloodOnMap < numCells) tmpSparseBlood.AddRange(things);
                else tmpSparseBlood.Clear();
                if (!prune) continue;
                for (int j = 0; j < things.Count; j++) tmpLiveTargetIds.Add(things[j].thingIDNumber);
            }
            if (prune)
            {
                seenBloodPairs.RemoveWhere(PairTargetGone);
                tmpLiveTargetIds.Clear();
            }
            if (bloodOnMap == 0) return; // nothing to see — skip all radial scans

            ThoughtDef? bloodDef = TPE_ThoughtDefOf.TPE_Event_ObservedBlood;
            if (bloodDef == null)
            {
                tmpSparseBlood.Clear();
                return;
            }
            if (bloodOnMap < numCells)
            {
                ScanSparseBlood(bloodDef);
                tmpSparseBlood.Clear();
                return;
            }

            for (int i = 0; i < tmpToddlers.Count; i++)
            {
                if (!tmpToddlerAwake[i]) continue;
                Pawn toddler = tmpToddlers[i];
                IntVec3 pos = toddler.Position;
                bool sawNew = false;

                // k = 1: skip the toddler's own cell (same as the old useCenter: false).
                for (int k = 1; k < numCells; k++)
                {
                    IntVec3 cell = pos + GenRadial.RadialPattern[k];
                    if (!cell.InBounds(map)) continue;
                    List<Thing> thingsHere = map.thingGrid.ThingsListAtFast(cell);
                    bool? cellVisible = null; // LOS computed lazily, at most once per cell
                    for (int m = 0; m < thingsHere.Count; m++)
                    {
                        Thing t = thingsHere[m];
                        if (!bloodFilthDefs.Contains(t.def)) continue;
                        long key = PairKey(toddler, t);
                        if (seenBloodPairs.Contains(key)) continue;
                        if (cellVisible == null)
                            cellVisible = GenSight.LineOfSight(pos, cell, map, skipFirstCell: true);
                        if (cellVisible == false) break; // nothing in this cell is visible
                        seenBloodPairs.Add(key);
                        sawNew = true;
                    }
                }

                if (sawNew) RefreshMemory(toddler, bloodDef);
            }
        }

        private void ScanSparseBlood(ThoughtDef bloodDef)
        {
            float radiusSq = BloodSightRadius * BloodSightRadius;
            for (int i = 0; i < tmpToddlers.Count; i++)
            {
                if (!tmpToddlerAwake[i]) continue;
                Pawn toddler = tmpToddlers[i];
                IntVec3 pos = toddler.Position;
                bool sawNew = false;
                for (int j = 0; j < tmpSparseBlood.Count; j++)
                {
                    Thing blood = tmpSparseBlood[j];
                    IntVec3 cell = blood.Position;
                    int distanceSq = (cell - pos).LengthHorizontalSquared;
                    if (distanceSq == 0 || distanceSq > radiusSq) continue;
                    long key = PairKey(toddler, blood);
                    if (seenBloodPairs.Contains(key)) continue;
                    if (!GenSight.LineOfSight(pos, cell, map, skipFirstCell: true)) continue;
                    seenBloodPairs.Add(key);
                    sawNew = true;
                }
                if (sawNew) RefreshMemory(toddler, bloodDef);
            }
        }

        /// <summary>
        /// Packs (toddler ID, target ID) into one long: high 32 bits = toddler,
        /// low 32 bits = target. thingIDNumbers are non-negative ints, so the
        /// low half round-trips through <c>(int)(key &amp; 0xFFFFFFFF)</c>.
        /// </summary>
        private static long PairKey(Pawn toddler, Thing target)
        {
            return ((long)toddler.thingIDNumber << 32) | (uint)target.thingIDNumber;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  HOMESICKNESS — ONE-TIME random roll (time-gated)
        // ═════════════════════════════════════════════════════════════════════
        private void UpdateHomesick(int now)
        {
            ThoughtDef? homesickDef = TPE_ThoughtDefOf.TPE_AdoptedToddlerHomesick;
            if (homesickDef == null) return;

            for (int i = 0; i < tmpToddlers.Count; i++)
            {
                if (!tmpToddlerAwake[i]) continue;
                Pawn toddler = tmpToddlers[i];
                int id = toddler.thingIDNumber;

                if (homesickCooldownTicks.TryGetValue(id, out int cooldownUntil))
                {
                    if (now < cooldownUntil) continue;
                    homesickCooldownTicks.Remove(id); // expired — drop it so the dictionary stays small
                }
                if (AlreadyHasThought(toddler, homesickDef)) continue;
                // Cheap 2% roll first; the relation scan only runs when it succeeds.
                if (!Rand.Chance(HomesickChancePerCheck)) continue;
                if (!IsAdoptedToddler(toddler)) continue;

                RefreshMemory(toddler, homesickDef);
                homesickCooldownTicks[id] = now + HomesickCooldownTicks;
            }
        }

        /// <summary>
        /// "Adopted" = no parent who is (or died as) a member of the player's faction.
        /// BUGFIX: the old test only accepted LIVING parents, so a toddler born in the
        /// colony whose parents later died counted as "adopted" and kept getting
        /// "I miss my old home…" about a home it never had. A pawn keeps its Faction
        /// after death, so <c>parent.Faction == player</c> covers both cases. (The old
        /// extra check for the toddler's own CHILDREN relation was meaningless for a
        /// toddler and is dropped.)
        /// </summary>
        private static bool IsAdoptedToddler(Pawn toddler)
        {
            if (toddler.relations == null) return true;
            Faction ofPlayer = Faction.OfPlayer;
            List<DirectPawnRelation> relations = toddler.relations.DirectRelations;
            for (int i = 0; i < relations.Count; i++)
            {
                DirectPawnRelation rel = relations[i];
                if (rel.def != PawnRelationDefOf.Parent) continue;
                if (rel.otherPawn != null && rel.otherPawn.Faction == ofPlayer) return false;
            }
            return true;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SETTING TURNED OFF — one-time cleanup
        // ═════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Removes every event mood from every toddler held on this map (spawned or
        /// carried). Runs once at the on→off edge of the setting (BUGFIX: turning
        /// the setting off used to leave existing moods until they expired).
        /// </summary>
        private void RemoveAllEventMoodsOnMap()
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawns; // spawned + held (carried etc.)
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p.Dead || !ToddlerUtility.IsToddler(p)) continue;
                for (int d = 0; d < allEventThoughtDefs.Count; d++)
                    RemoveMemory(p, allEventThoughtDefs[d]);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SHARED HELPERS (also used by the Harmony patches in HarmonyPatches.cs)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ONGOING map-wide condition: active → refresh on every AWAKE toddler;
        /// inactive → remove from EVERY spawned toddler (asleep ones too).
        /// </summary>
        private static void MaintainOrRemove(ThoughtDef? thought, bool conditionActive)
        {
            if (thought == null) return;
            for (int i = 0; i < tmpToddlers.Count; i++)
                MaintainOrRemoveForPawn(tmpToddlers[i], thought, conditionActive, tmpToddlerAwake[i]);
        }

        /// <summary>
        /// Per-pawn ONGOING rule: active + awake → refresh; active + asleep → leave
        /// alone (may expire, re-applied on waking); inactive → remove.
        /// </summary>
        private static void MaintainOrRemoveForPawn(Pawn pawn, ThoughtDef? thought, bool conditionActive, bool awake)
        {
            if (thought == null) return;
            if (conditionActive)
            {
                if (awake) RefreshMemory(pawn, thought);
            }
            else
            {
                RemoveMemory(pawn, thought);
            }
        }

        /// <summary>ONE-TIME / LINGERING: apply (or renew) on every awake toddler of this check.</summary>
        private static void ApplyToAwake(ThoughtDef? thought)
        {
            if (thought == null) return;
            for (int i = 0; i < tmpToddlers.Count; i++)
            {
                if (tmpToddlerAwake[i]) RefreshMemory(tmpToddlers[i], thought);
            }
        }

        /// <summary>
        /// Applies a one-time event mood to every awake, living, spawned toddler on
        /// <paramref name="map"/>. The single definition of "which toddlers react to a
        /// one-shot event", used by the Harmony event patches (colonist death,
        /// incidents, animal tamed). Allocation-free; does NOT touch the static
        /// scratch buffers (safe to call from inside any game code path).
        /// </summary>
        internal static void ApplyToAwakeToddlers(Map? map, ThoughtDef? thought)
        {
            if (map == null || thought == null) return;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p.Dead || !p.RaceProps.Humanlike || !ToddlerUtility.IsToddler(p) || !p.Awake()) continue;
                RefreshMemory(p, thought);
            }
        }

        /// <summary>
        /// Gains <paramref name="thought"/>, or — if the pawn already has it — just
        /// renews it (<c>Thought_Memory.Renew</c>, age = 0). For these stackLimit-1,
        /// non-social memories that is exactly what TryGainMemory's own merge path
        /// does, minus the throw-away Thought_Memory allocation TryGainMemory makes
        /// on EVERY call (this runs every 150 ticks per toddler per active mood).
        /// The first gain goes through TryGainMemory, so ThoughtUtility.CanGetThought
        /// (developmental-stage filter etc.) is still honoured.
        /// </summary>
        internal static void RefreshMemory(Pawn pawn, ThoughtDef? thought)
        {
            if (thought == null) return;
            MemoryThoughtHandler? memories = pawn.needs?.mood?.thoughts?.memories;
            if (memories == null) return;
            Thought_Memory? existing = memories.GetFirstMemoryOfDef(thought);
            if (existing != null)
                existing.Renew();
            else
                memories.TryGainMemory(thought);
        }

        /// <summary>Remove matching memories without RemoveMemoriesOfDef's predicate allocation.</summary>
        private static void RemoveMemory(Pawn pawn, ThoughtDef thought)
        {
            MemoryThoughtHandler? memories = pawn.needs?.mood?.thoughts?.memories;
            if (memories == null) return;
            List<Thought_Memory> list = memories.Memories;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Thought_Memory memory = list[i];
                if (memory.def == thought) memories.RemoveMemory(memory);
            }
        }

        private static bool AlreadyHasThought(Pawn pawn, ThoughtDef def)
        {
            return pawn.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(def) != null;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SAVE / LOAD
        // ═════════════════════════════════════════════════════════════════════
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref wasHostile, "wasHostile", false);
            Scribe_Values.Look(ref nextCorpseCheckTick, "nextCorpseCheckTick", -1);
            Scribe_Values.Look(ref nextBloodCheckTick, "nextBloodCheckTick", -1);

            Scribe_Collections.Look(ref homesickCooldownTicks, "homesickCooldownTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref seenCorpsePairs, "seenCorpsePairs", LookMode.Value);
            Scribe_Collections.Look(ref seenBloodPairs, "seenBloodPairs", LookMode.Value);
            Scribe_Collections.Look(ref thunderstormMoodChoice, "thunderstormMoodChoice", LookMode.Value, LookMode.Value);

            // Null-safety after load (missing nodes in older saves load as null).
            if (homesickCooldownTicks == null) homesickCooldownTicks = new Dictionary<int, int>();
            if (seenCorpsePairs == null) seenCorpsePairs = new HashSet<long>();
            if (seenBloodPairs == null) seenBloodPairs = new HashSet<long>();
            if (thunderstormMoodChoice == null) thunderstormMoodChoice = new Dictionary<int, bool>();

            // Power outage in progress: save the cut-off consumers as Thing references
            // (only live, spawned ones — a reference to a destroyed thing can't resolve on
            // load) and turn them back into comps after loading. Without this, loading
            // during an outage would drop the mood for the rest of it, because the
            // "was running" history isn't known yet on the first check after a load.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                scribedCutOffThings = new List<Thing>(cutOffConsumers.Count); // save-time only
                foreach (CompPowerTrader comp in cutOffConsumers)
                {
                    ThingWithComps? parent = comp?.parent;
                    if (parent != null && parent.Spawned && !parent.Destroyed && parent.Map == map)
                        scribedCutOffThings.Add(parent);
                }
            }
            Scribe_Collections.Look(ref scribedCutOffThings, "powerCutOffThings", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                cutOffConsumers.Clear();
                if (scribedCutOffThings != null)
                {
                    for (int i = 0; i < scribedCutOffThings.Count; i++)
                    {
                        CompPowerTrader? comp = (scribedCutOffThings[i] as ThingWithComps)?.GetComp<CompPowerTrader>();
                        if (comp != null) cutOffConsumers.Add(comp);
                    }
                }
            }
            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.PostLoadInit)
                scribedCutOffThings = null; // bridge no longer needed

            // Save compatibility: nodes written by older versions of this component —
            // <lastBiologicalAge> (birthday is now a Harmony hook), <diseasedPawnIds>
            // (disease is now incident-based), <seenCorpseIds>/<seenBloodCells> (global
            // seen-sets, replaced by per-toddler pair sets) and <lastColonistCount> —
            // are simply never read, which the XML loader ignores silently. Their old
            // "seen" data is intentionally not migrated (it was wrong by design).
        }
    }
}
