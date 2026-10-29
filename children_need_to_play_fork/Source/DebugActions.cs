using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using LudeonTK;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Dev-mode debug actions exposed under the "General" debug menu.
    /// All methods are annotated with <see cref="DebugActionAttribute"/>.
    /// </summary>
    public static class TPE_DebugActions
    {
        // God-mode toggles: when true, continuously max out the respective need for all children.
        public static bool GodModeMaxLearning = false;
        public static bool GodModeMaxPlay = false;

        // Child play need system debug actions
        [DebugAction("General", "Spawn 3 children (age 4)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Spawn3ChildrenAged4()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            for (int i = 0; i < 3; i++)
            {
                PawnGenerationRequest request = new PawnGenerationRequest(
                    PawnKindDefOf.Colonist,
                    Faction.OfPlayer,
                    PawnGenerationContext.NonPlayer,
                    map.Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: true,
                    mustBeCapableOfViolence: false,
                    colonistRelationChanceFactor: 1f,
                    forceAddFreeWarmLayerIfNeeded: false,
                    allowGay: true,
                    allowPregnant: false,
                    allowFood: true,
                    allowAddictions: true,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    biocodeApparelChance: 0f,
                    extraPawnForExtraRelationChance: null,
                    relationWithExtraPawnChanceFactor: 1f,
                    validatorPreGear: null,
                    validatorPostGear: null,
                    forcedTraits: null,
                    prohibitedTraits: null,
                    minChanceToRedressWorldPawn: null,
                    fixedBiologicalAge: 4f,
                    fixedChronologicalAge: 4f,
                    fixedGender: null,
                    fixedLastName: null,
                    fixedBirthName: null,
                    fixedTitle: null,
                    fixedIdeo: null,
                    forceNoIdeo: false,
                    forceNoBackstory: false,
                    forbidAnyTitle: false,
                    forceDead: false,
                    forcedXenogenes: null,
                    forcedEndogenes: null,
                    forcedXenotype: null,
                    forcedCustomXenotype: null,
                    allowedXenotypes: null,
                    forceBaselinerChance: 0f,
                    developmentalStages: DevelopmentalStage.Child
                );

                Pawn pawn = PawnGenerator.GeneratePawn(request);
                IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 10,
                    c => GenGrid.Standable(c, map) && !c.Fogged(map));
                GenSpawn.Spawn(pawn, spawnCell, map);

                if (map.areaManager.Home != null && pawn.playerSettings != null)
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = map.areaManager.Home;
                }
            }
        }

        [DebugAction("General", "Spawn 7 children (3-13)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Spawn7Children()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            for (int i = 0; i < 7; i++)
            {
                float age = Rand.Range(3f, 13f);
                PawnGenerationRequest request = new PawnGenerationRequest(
                    PawnKindDefOf.Colonist,
                    Faction.OfPlayer,
                    PawnGenerationContext.NonPlayer,
                    map.Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: true,
                    mustBeCapableOfViolence: false,
                    colonistRelationChanceFactor: 1f,
                    forceAddFreeWarmLayerIfNeeded: false,
                    allowGay: true,
                    allowPregnant: false,
                    allowFood: true,
                    allowAddictions: true,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    biocodeApparelChance: 0f,
                    extraPawnForExtraRelationChance: null,
                    relationWithExtraPawnChanceFactor: 1f,
                    validatorPreGear: null,
                    validatorPostGear: null,
                    forcedTraits: null,
                    prohibitedTraits: null,
                    minChanceToRedressWorldPawn: null,
                    fixedBiologicalAge: age,
                    fixedChronologicalAge: age,
                    fixedGender: null,
                    fixedLastName: null,
                    fixedBirthName: null,
                    fixedTitle: null,
                    fixedIdeo: null,
                    forceNoIdeo: false,
                    forceNoBackstory: false,
                    forbidAnyTitle: false,
                    forceDead: false,
                    forcedXenogenes: null,
                    forcedEndogenes: null,
                    forcedXenotype: null,
                    forcedCustomXenotype: null,
                    allowedXenotypes: null,
                    forceBaselinerChance: 0f,
                    developmentalStages: DevelopmentalStage.Child
                );

                Pawn pawn = PawnGenerator.GeneratePawn(request);
                IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 10,
                    c => GenGrid.Standable(c, map) && !c.Fogged(map));
                GenSpawn.Spawn(pawn, spawnCell, map);

                if (map.areaManager.Home != null && pawn.playerSettings != null)
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = map.areaManager.Home;
                }
            }
        }

        [DebugAction("General", "Toggle God Mode: Max Learning", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleGodModeMaxLearning()
        {
            GodModeMaxLearning = !GodModeMaxLearning;
            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message($"[TPE] God Mode Max Learning: {(GodModeMaxLearning ? "ON" : "OFF")}");
        }

        [DebugAction("General", "Toggle God Mode: Max Play", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleGodModeMaxPlay()
        {
            GodModeMaxPlay = !GodModeMaxPlay;
            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message($"[TPE] God Mode Max Play: {(GodModeMaxPlay ? "ON" : "OFF")}");
        }
        [DebugAction("General", "Spawn 7 toddlers", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Spawn7Toddlers()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            for (int i = 0; i < 7; i++)
            {
                PawnGenerationRequest request = new PawnGenerationRequest(
                    PawnKindDefOf.Colonist,
                    Faction.OfPlayer,
                    PawnGenerationContext.NonPlayer,
                    map.Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: true,
                    mustBeCapableOfViolence: false,
                    colonistRelationChanceFactor: 1f,
                    forceAddFreeWarmLayerIfNeeded: false,
                    allowGay: true,
                    allowPregnant: false,
                    allowFood: true,
                    allowAddictions: true,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    biocodeApparelChance: 0f,
                    extraPawnForExtraRelationChance: null,
                    relationWithExtraPawnChanceFactor: 1f,
                    validatorPreGear: null,
                    validatorPostGear: null,
                    forcedTraits: null,
                    prohibitedTraits: null,
                    minChanceToRedressWorldPawn: null,
                    fixedBiologicalAge: Rand.Range(1f, 2.5f),
                    fixedChronologicalAge: Rand.Range(1f, 2.5f),
                    fixedGender: null,
                    fixedLastName: null,
                    fixedBirthName: null,
                    fixedTitle: null,
                    fixedIdeo: null,
                    forceNoIdeo: false,
                    forceNoBackstory: false,
                    forbidAnyTitle: false,
                    forceDead: false,
                    forcedXenogenes: null,
                    forcedEndogenes: null,
                    forcedXenotype: null,
                    forcedCustomXenotype: null,
                    allowedXenotypes: null,
                    forceBaselinerChance: 0f,
                    developmentalStages: DevelopmentalStage.Child
                );

                Pawn pawn = PawnGenerator.GeneratePawn(request);
                IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 10,
                    c => GenGrid.Standable(c, map) && !c.Fogged(map));
                GenSpawn.Spawn(pawn, spawnCell, map);

                // Set allowed area to Home
                if (map.areaManager.Home != null && pawn.playerSettings != null)
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = map.areaManager.Home;
                }
            }
        }

        /// <summary>
        /// BUGFIX: previously hardcoded to always use the TODDLER thought pair
        /// (<c>TPE_SleptWithPlushToy</c>/<c>TPE_NoSoftCuddleBuddy</c>, which are
        /// <c>developmentalStageFilter: Baby</c>-only) regardless of the target
        /// pawn's actual age — so running this on a child or adult silently did
        /// nothing useful (vanilla's own thought-grant check rejects a memory
        /// whose developmentalStageFilter doesn't match the pawn's stage). It also
        /// ignored the current selection entirely, always grabbing "the first
        /// colonist in mapPawns" instead. Now prefers the selected pawn and
        /// reuses the same <see cref="Patch_Toils_LayDown_ApplyBedThoughts.TryResolvePlushMoodBracket"/>
        /// dispatch the real sleep-thought patch uses, so this debug tool can
        /// never drift out of sync with production behavior and correctly
        /// exercises all four brackets (toddler / child 3–6 / child 7–12 / adult).
        /// </summary>
        [DebugAction("General", "Test plush thought", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestPlushThought()
        {
            Pawn pawn = Find.Selector.SelectedPawns.FirstOrDefault(p => p.IsColonist)
                ?? Find.CurrentMap?.mapPawns?.AllPawnsSpawned?.FirstOrDefault(p => p.IsColonist);
            if (pawn == null) return;

            var memories = pawn.needs?.mood?.thoughts?.memories;
            if (memories == null)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Warning("[TPE] Selected pawn has no mood memories");
                return;
            }

            if (!Patch_Toils_LayDown_ApplyBedThoughts.TryResolvePlushMoodBracket(pawn,
                    out _, out ThoughtDef plushThoughtDef, out ThoughtDef noPlushThoughtDef,
                    out bool allowPositive, out bool allowNegative))
            {
                Messages.Message($"[TPE] {pawn.NameShortColored} is not currently eligible for any plush-sleep mood " +
                    "(wrong age/race, or the relevant setting is off).", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            bool hasPlush = TPE_ToyUtility.HasPlushEquippedOrCarried(pawn);

            memories.RemoveMemoriesOfDef(plushThoughtDef);
            memories.RemoveMemoriesOfDef(noPlushThoughtDef);

            if (hasPlush)
            {
                if (allowPositive)
                {
                    memories.TryGainMemory(plushThoughtDef);
                    if (TPE_Mod.Settings?.debugLogging ?? false)
                        Log.Message($"[TPE] Manually applied '{plushThoughtDef.defName}' to {pawn.NameShortColored} (has plush)");
                }
                else
                {
                    Messages.Message($"[TPE] {pawn.NameShortColored} has a plush, but the positive thought is currently suppressed by settings.", MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            else
            {
                if (allowNegative)
                {
                    memories.TryGainMemory(noPlushThoughtDef);
                    if (TPE_Mod.Settings?.debugLogging ?? false)
                        Log.Message($"[TPE] Manually applied '{noPlushThoughtDef.defName}' to {pawn.NameShortColored} (no plush)");
                }
                else
                {
                    Messages.Message($"[TPE] {pawn.NameShortColored} has no plush, but the negative thought is currently suppressed by settings.", MessageTypeDefOf.RejectInput, historical: false);
                }
            }
        }

        [DebugAction("General", "Spawn toy set", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnToySet()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            /// <summary>
            /// Array of all toy items to spawn. One of each toy def will be spawned
            /// near the player's cursor or the map center.
            /// Includes: plush toys (Teddy, Muffalo, Sheep, Dino, Monster, Fox),
            /// Toy Sword (for monster hunting), and Magnifying Glass (for bug watching).
            /// </summary>
            ThingDef[] toyDefs = new ThingDef[]
            {
                TPE_ThingDefOf.TPE_PlushTeddy,
                TPE_ThingDefOf.TPE_PlushMuffalo,
                TPE_ThingDefOf.TPE_PlushSheep,
                TPE_ThingDefOf.TPE_PlushDino,
                TPE_ThingDefOf.TPE_PlushMonster,
                TPE_ThingDefOf.TPE_PlushFox,
                TPE_ThingDefOf.TPE_ToySword,
                TPE_ThingDefOf.TPE_MagnifyingGlass
            };

            IntVec3 center = UI.MouseCell().IsValid ? UI.MouseCell() : map.Center;
            for (int i = 0; i < toyDefs.Length; i++)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 3,
                    c => GenGrid.Standable(c, map) && !c.Fogged(map));
                Thing toy = ThingMaker.MakeThing(toyDefs[i]);
                GenSpawn.Spawn(toy, cell, map);
            }
        }

        [DebugAction("General", "Spawn thing/babyfood", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnBabyFood()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            IntVec3 cell = UI.MouseCell().IsValid ? UI.MouseCell() : map.Center;
            if (GenGrid.Standable(cell, map) && !cell.Fogged(map))
            {
                Thing food = ThingMaker.MakeThing(ThingDefOf.BabyFood);
                GenSpawn.Spawn(food, cell, map);
            }
        }

        [DebugAction("General", "Spawn thing/muffalo plush", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnMuffaloPlush()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            IntVec3 cell = UI.MouseCell().IsValid ? UI.MouseCell() : map.Center;
            if (GenGrid.Standable(cell, map) && !cell.Fogged(map))
            {
                Thing plush = ThingMaker.MakeThing(TPE_ThingDefOf.TPE_PlushMuffalo);
                GenSpawn.Spawn(plush, cell, map);
            }
        }

        [DebugAction("General", "Test dinosaur roar", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestDinoRoar()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            IntVec3 cell = UI.MouseCell().IsValid ? UI.MouseCell() : map.Center;
            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message("[TPE] Playing TPE_DinoRoar at " + cell);
            TPE_SoundDefOf.TPE_DinoRoar.PlayOneShot(new TargetInfo(cell, map));
        }

        /// <summary>
        /// Debug action: force a specific toddler play activity via a
        /// cursor tool.  Presents a submenu of every <see cref="ToddlerPlayDef"/>
        /// (base mod + this mod).  Once selected, left-click on a pawn to force
        /// the activity; right-click cancels the tool.
        ///
        /// Eligibility checks:
        ///   • Pawn must be a humanlike toddler (<see cref="ToddlerUtility.IsToddler"/>).
        ///   • The play giver's <c>CanDo</c> (or <c>CanDoFromCrib</c> if in crib)
        ///     must return true.
        ///
        /// Failure messages mirror vanilla "RejectInput" style.
        /// </summary>
        [DebugAction("General", "Force play activity on toddler", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> ForcePlayActivityOnToddler()
        {
            List<DebugActionNode> nodes = new List<DebugActionNode>();

            foreach (ToddlerPlayDef playDef in DefDatabase<ToddlerPlayDef>.AllDefsListForReading)
            {
                // Capture def in local so the closure is correct.
                ToddlerPlayDef localDef = playDef;
                string label = localDef.label?.CapitalizeFirst() ?? localDef.defName;

                nodes.Add(new DebugActionNode(
                    label,
                    DebugActionType.ToolMapForPawns,
                    pawnAction: pawn => ForcePlayActivity(pawn, localDef)));
            }

            return nodes;
        }

        /// <summary>
        /// Attempts to start <paramref name="playDef"/> on <paramref name="pawn"/>.
        /// Checks toddler status and play-giver eligibility, then creates and
        /// starts the job.  Prefers <c>TryGiveJobFromCrib</c> (via reflection) when
        /// the toddler is currently in a crib and the worker exposes that method.
        /// </summary>
        static void ForcePlayActivity(Pawn pawn, ToddlerPlayDef playDef)
        {
            // ---------- 1. Toddler check ----------
            if (pawn == null || !pawn.Spawned) return;
            if (pawn.RaceProps?.Humanlike != true || !ToddlerUtility.IsToddler(pawn))
            {
                string name = pawn.NameShortColored.Resolve();
                Messages.Message(
                    $"{name} can not gain this play activity because {name} is not a toddler!",
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return;
            }

            // ---------- 2. Eligibility check ----------
            // If the toddler is in a crib, accept CanDoFromCrib as well.
            bool canDo = playDef.Worker.CanDo(pawn);
            if (!canDo && CribUtility.InCrib(pawn))
            {
                canDo = playDef.Worker.CanDoFromCrib(pawn);
            }

            if (!canDo)
            {
                string name = pawn.NameShortColored.Resolve();
                Messages.Message(
                    $"{name} is ineligible for this play activity (CanDo returned false).",
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return;
            }

            // ---------- 3. Create job ----------
            Job job = null;

            // Some base-mod workers (e.g. WatchTelevision, Skydreaming) expose
            // TryGiveJobFromCrib for in-crib usage.  Detect it via reflection
            // so we don't need a hard reference to the subclass.
            if (CribUtility.InCrib(pawn))
            {
                System.Reflection.MethodInfo fromCribMethod = playDef.Worker.GetType().GetMethod(
                    "TryGiveJobFromCrib",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (fromCribMethod != null)
                {
                    try
                    {
                        job = (Job)fromCribMethod.Invoke(playDef.Worker, new object[] { pawn });
                    }
                    catch (System.Exception ex)
                    {
                        if (TPE_Mod.Settings?.debugLogging ?? false)
                            Log.Warning($"[TPE] TryGiveJobFromCrib failed for {playDef.defName}: {ex}");
                    }
                }
            }

            // Fallback to standard TryGiveJob.
            if (job == null)
            {
                job = playDef.Worker.TryGiveJob(pawn);
            }

            // ---------- 4. Magnifying-glass enhancement for bug watching ----------
            // When the base mod's ToddlerBugwatching job is forced via the debug
            // tool, the normal Harmony postfix (Patch_JobGiver_ToddlerPlay_TryGiveJob_
            // BugWatchingMagnifyingGlass) does NOT fire — that patch only intercepts
            // the base mod's JobGiver_ToddlerPlay.TryGiveJob, not the play def worker's
            // TryGiveJob that we call above. We replicate the same glass-search logic
            // here so forced bug-watching jobs also pick up a magnifying glass when
            // one is available.
            if (job != null && job.def?.defName == "ToddlerBugwatching")
            {
                Thing? glass = FindMagnifyingGlassForForcedBugwatch(pawn);
                if (glass != null)
                {
                    IntVec3 originalTarget = job.targetA.Cell;
                    Job enhancedJob = JobMaker.MakeJob(TPE_JobDefOf.TPE_BugwatchingWithGlass, glass, originalTarget);
                    job = enhancedJob;
                }
            }

            // ---------- 5. Start job ----------
            if (job != null)
            {
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE] Forced play activity '{playDef.defName}' on {pawn.NameShortColored.Resolve()}.");
            }
            else
            {
                string name = pawn.NameShortColored.Resolve();
                Messages.Message(
                    $"{name}: TryGiveJob returned null (job could not be created).",
                    MessageTypeDefOf.RejectInput,
                    historical: false);
            }
        }

        /// <summary>
        /// Finds the nearest reachable magnifying glass within 30 tiles.
        /// Returns null if none are available or if the toddler already carries one.
        /// Duplicated from <see cref="Patch_JobGiver_ToddlerPlay_TryGiveJob_BugWatchingMagnifyingGlass"/>
        /// so the debug force tool can offer the same enhancement.
        /// </summary>
        static Thing? FindMagnifyingGlassForForcedBugwatch(Pawn pawn)
        {
            if (pawn.carryTracker?.CarriedThing?.def == TPE_ThingDefOf.TPE_MagnifyingGlass)
                return null;

            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(TPE_ThingDefOf.TPE_MagnifyingGlass),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                30f,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }

        /// <summary>
        /// Debug action: inspect and fix duplicate play needs on a selected child.
        /// Shows how many play needs the child currently has and offers to reset
        /// to exactly one. Uses a cursor tool for pawn selection.
        /// </summary>
        [DebugAction("General", "Fix child play need duplicates", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FixChildPlayNeedDuplicates()
        {
            DebugTools.curTool = new DebugTool(
                "Click child to inspect/fix play needs",
                () =>
                {
                    IntVec3 mouseCell = UI.MouseCell();
                    Map map = Find.CurrentMap;
                    if (map == null) return;

                    // Find a child pawn at the clicked cell
                    foreach (Thing t in map.thingGrid.ThingsAt(mouseCell))
                    {
                        if (t is Pawn pawn
                            && pawn.RaceProps.Humanlike
                            && pawn.DevelopmentalStage.Child())
                        {
                            InspectAndFixPlayNeeds(pawn);
                            return; // Applied to first child found
                        }
                    }

                    if (TPE_Mod.Settings?.debugLogging ?? false)
                        Log.Message($"[TPE] No child at {mouseCell}. Click on a child pawn.");
                });
        }

        /// <summary>
        /// Inspects a child's play needs and removes duplicates, keeping exactly one.
        /// This function:
        /// 1. Counts how many play needs currently exist in the pawn's needs list
        /// 2. Logs the current count
        /// 3. Removes all duplicate needs beyond the first one
        /// 4. Ensures exactly one play need remains (or adds one if none exist)
        /// 5. Resets the play need to 50% level (neutral state)
        /// 
        /// Why this is needed: When a child is resurrected via revival/ressurection,
        /// the Patch_Pawn_NeedsTracker_AddOrRemoveNeedsAsAppropriate postfix may be
        /// called multiple times before the needs list is properly synchronized.
        /// If Pawn_NeedsTracker.AddNeed() doesn't deduplicate internally, multiple
        /// identical need instances can accumulate in the list. This causes the needs
        /// tab UI to draw multiple identical bars stacked on top of each other.
        /// </summary>
        private static void InspectAndFixPlayNeeds(Pawn pawn)
        {
            if (pawn?.needs == null)
            {
                Messages.Message("[TPE] Pawn has no needs tracker!", MessageTypeDefOf.RejectInput);
                return;
            }

            NeedDef needDef = TPE_ChildPlayDefOf.TPE_ChildPlay;
            if (needDef == null)
            {
                Messages.Message("[TPE] TPE_ChildPlay NeedDef not found!", MessageTypeDefOf.RejectInput);
                return;
            }

            // Access the needs list directly via Traverse.
            // Pawn_NeedsTracker.needs is a private List<Need> field.
            List<Need>? needsList = Traverse.Create(pawn.needs).Field("needs").GetValue<List<Need>>();
            if (needsList == null)
            {
                Messages.Message("[TPE] Could not access pawn's needs list!", MessageTypeDefOf.RejectInput);
                return;
            }

            // Count how many play needs exist (should be exactly 1, but we're fixing duplicates)
            int playNeedCount = 0;
            int playNeedIndex = -1;
            for (int i = 0; i < needsList.Count; i++)
            {
                if (needsList[i]?.def == needDef)
                {
                    playNeedCount++;
                    if (playNeedIndex == -1)
                        playNeedIndex = i; // Remember first occurrence
                }
            }

            // Log the inspection result
            string pawnName = pawn.NameShortColored.Resolve();
            if (TPE_Mod.Settings?.debugLogging ?? false)
            {
                Log.Message($"[TPE] {pawnName} has {playNeedCount} play need(s).");
            }

            // If no play needs exist and should exist, add one
            if (playNeedCount == 0 && ShouldHavePlayNeed(pawn))
            {
                Traverse.Create(pawn.needs).Method("AddNeed", needDef).GetValue();
                playNeedCount = 1;
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE] Added missing play need to {pawnName}.");
                Messages.Message($"{pawnName}: Added 1 new play need.", MessageTypeDefOf.NeutralEvent);
                return;
            }

            // If exactly 1 play need exists, no fix needed
            if (playNeedCount == 1)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE] {pawnName} has exactly 1 play need. No duplicates to remove.");
                Messages.Message($"{pawnName}: Play need is healthy (1 need found).", MessageTypeDefOf.NeutralEvent);
                return;
            }

            // If multiple play needs exist, remove all but the first one
            if (playNeedCount > 1)
            {
                // Remove in reverse order to avoid index shifting issues
                for (int i = needsList.Count - 1; i >= 0; i--)
                {
                    if (i != playNeedIndex && needsList[i]?.def == needDef)
                    {
                        needsList.RemoveAt(i);
                    }
                }

                // Reset the remaining play need to 50% (neutral satisfaction level)
                Need_ChildPlay? playNeed = pawn.needs?.TryGetNeed(needDef) as Need_ChildPlay;
                if (playNeed != null)
                {
                    playNeed.CurLevelPercentage = 0.5f;
                }

                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE] Removed {playNeedCount - 1} duplicate play need(s) from {pawnName}. Reset remaining need to 50%.");
                Messages.Message($"{pawnName}: Removed {playNeedCount - 1} duplicate play need(s). Reset to 50%.", MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// Returns true if this pawn should have a child play need.
        /// Mirrors the logic in Patch_Pawn_NeedsTracker_AddOrRemoveNeedsAsAppropriate.
        /// </summary>
        private static bool ShouldHavePlayNeed(Pawn pawn)
        {
            return TPE_Mod.Settings?.enableChildrenPlayNeed == true
                && pawn.DevelopmentalStage.Child()
                && pawn.Faction != null
                && pawn.Faction.IsPlayer;
        }

        /// <summary>
        /// Debug action: force a specific play desire onto children via a
        /// cursor tool. First pick the desire from the menu, then click on
        /// any child to apply it. Right-click to cancel the tool.
        /// Mirrors the vanilla "Destroy" debug action flow.
        ///
        /// DELIBERATELY bypasses <see cref="PlayDesireGiver.CanGiveDesire"/>
        /// (the colony-must-have-built-the-required-structure / research gate
        /// used at normal roll time — e.g. Radiotalking's comms-console check),
        /// age gating, AND the Play↔Learning mutual-exclusion check
        /// (<see cref="PlayUtility.ConflictsWithActiveLearningDesire(PlayDesireDef,Pawn)"/>)
        /// — this writes straight into <see cref="Pawn_PlayTracker.ActivePlayDesires"/>
        /// rather than going through <c>AddNewPlayDesire</c> at all, so none of
        /// that method's filters ever run. This is a debug/QA tool for forcing
        /// ANY desire onto ANY child for testing purposes, including verifying a
        /// desire's <see cref="PlayDesireGiver.CanDo"/>/<see cref="PlayDesireGiver.TryGiveJob"/>
        /// behaviour even before its normal prerequisites are met — or, just as
        /// usefully, testing what SHOULD be an excluded combination on purpose
        /// (e.g. forcing "floor drawing" as a Play desire while it's already an
        /// active Learning desire, to visually confirm the child doesn't somehow
        /// end up doing it twice).
        /// </summary>
        [DebugAction("General", "Force play desire on child", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForcePlayDesireOnChild()
        {
            List<DebugMenuOption> desireOptions = new List<DebugMenuOption>();

            foreach (PlayDesireDef desireDef in DefDatabase<PlayDesireDef>.AllDefsListForReading)
            {
                desireOptions.Add(new DebugMenuOption(
                    $"{desireDef.label} (age {desireDef.minAge}-{desireDef.maxAge})",
                    DebugMenuOptionMode.Action,
                    () => ActivateDesireTool(desireDef)));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(desireOptions));
        }

        /// <summary>
        /// Activates a <see cref="DebugTool"/> that applies the given play
        /// desire to any child clicked on the map. Left-click applies,
        /// right-click cancels the tool.
        /// </summary>
        static void ActivateDesireTool(PlayDesireDef desireDef)
        {
            DebugTools.curTool = new DebugTool(
                $"Force {desireDef.label} on child",
                () =>
                {
                    IntVec3 mouseCell = UI.MouseCell();
                    Map map = Find.CurrentMap;
                    if (map == null) return;

                    // Find a child pawn at the clicked cell
                    foreach (Thing t in map.thingGrid.ThingsAt(mouseCell))
                    {
                        if (t is Pawn pawn
                            && pawn.RaceProps.Humanlike
                            && pawn.DevelopmentalStage.Child()
                            && pawn.ageTracker.AgeBiologicalYears >= 3
                            && pawn.ageTracker.AgeBiologicalYears <= 13)
                        {
                            var gameComp = Current.Game.GetComponent<GameComponent_PlayTracker>();
                            Pawn_PlayTracker? tracker = gameComp?.GetTracker(pawn);
                            if (tracker != null)
                            {
                                tracker.ActivePlayDesires.Clear();
                                tracker.ActivePlayDesires.Add(desireDef);
                                if (TPE_Mod.Settings?.debugLogging ?? false)
                                    Log.Message($"[TPE] Forced play desire '{desireDef.label}' on {pawn.Name.ToStringShort}.");
                            }
                            else
                            {
                                if (TPE_Mod.Settings?.debugLogging ?? false)
                                    Log.Warning($"[TPE] {pawn.Name.ToStringShort} has no play tracker.");
                            }
                            return; // applied to first child found
                        }
                    }

                    if (TPE_Mod.Settings?.debugLogging ?? false)
                        Log.Message($"[TPE] No child at {mouseCell}. Click on a child pawn.");
                });
        }
    }
}
