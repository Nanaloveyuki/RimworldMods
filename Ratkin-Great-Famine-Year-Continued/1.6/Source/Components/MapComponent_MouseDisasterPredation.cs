using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public sealed class MouseDisasterPredator : IExposable
    {
        public Pawn pawn;
        public bool outside;
        public bool firstHunt = true;
        public bool mealJobActive;
        public float mealJobStartFoodLevel = -1f;
        public bool fedAfterMeal;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref outside, "outside");
            Scribe_Values.Look(ref firstHunt, "firstHunt", true);
            Scribe_Values.Look(ref mealJobActive, "mealJobActive");
            Scribe_Values.Look(ref mealJobStartFoodLevel, "mealJobStartFoodLevel", -1f);
            Scribe_Values.Look(ref fedAfterMeal, "fedAfterMeal");
        }
    }

    public sealed class MapComponent_MouseDisasterPredation : MapComponent
    {
        private const float MealDetectionEpsilon = 0.001f;
        private List<Pawn> prey = new List<Pawn>();
        private List<int> selectedGroups = new List<int>();
        private List<MouseDisasterPredator> predators = new List<MouseDisasterPredator>();
        private readonly Dictionary<Pawn, MouseDisasterPredator> byPawn = new Dictionary<Pawn, MouseDisasterPredator>();
        private readonly Dictionary<Pawn, int> nextSearch = new Dictionary<Pawn, int>();
        private readonly HashSet<Pawn> vanillaFallback = new HashSet<Pawn>();
        private readonly List<Pawn> cachedWildPrey = new List<Pawn>();
        private readonly List<Corpse> cachedRatkinCorpses = new List<Corpse>();
        private int pendingTick = -1;
        private int nextTick;
        private int candidateCacheTick = -1;

        private static int SearchIntervalTicks
        {
            get
            {
                int value = MouseDisasterMod.Settings?.wildPredatorSearchIntervalTicks ??
                    MouseDisasterSettings.DefaultWildPredatorSearchIntervalTicks;
                return System.Math.Max(MouseDisasterSettings.MinWildPredatorSearchIntervalTicks,
                    System.Math.Min(MouseDisasterSettings.MaxWildPredatorSearchIntervalTicks, value));
            }
        }

        public MapComponent_MouseDisasterPredation(Map map) : base(map) { }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref prey, "predationPrey", LookMode.Reference);
            Scribe_Collections.Look(ref selectedGroups, "predationGroups", LookMode.Value);
            Scribe_Collections.Look(ref predators, "predationPredators", LookMode.Deep);
            Scribe_Values.Look(ref pendingTick, "predationPendingTick", -1);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            prey ??= new List<Pawn>();
            selectedGroups ??= new List<int>();
            predators ??= new List<MouseDisasterPredator>();
            byPawn.Clear();
            foreach (var predator in predators)
                if (predator?.pawn != null) byPawn[predator.pawn] = predator;
            candidateCacheTick = -1;
        }

        public void Register(MouseDisasterEventGroup group, IEnumerable<Pawn> pawns)
        {
            if (!MouseDisasterRuntime.AllowsNewContent) return;
            if (!group.predationRolledMaps.Contains(map.uniqueID))
            {
                group.predationRolledMaps.Add(map.uniqueID);
                if (Rand.Chance((MouseDisasterMod.Settings?.refugeePredationChancePercent ?? 10f) / 100f))
                {
                    selectedGroups.Add(group.id);
                    if (pendingTick < 0)
                    {
                        pendingTick = Find.TickManager.TicksGame + 120;
                        nextTick = System.Math.Min(nextTick, pendingTick);
                    }
                }
            }
            if (!selectedGroups.Contains(group.id)) return;
            foreach (Pawn pawn in pawns)
                if (!prey.Contains(pawn)) prey.Add(pawn);
        }

        private static bool WildPredator(Pawn pawn) => pawn != null && pawn.Spawned && !pawn.Dead &&
            !pawn.Downed && pawn.Faction == null && pawn.RaceProps.Animal && pawn.RaceProps.predator &&
            !pawn.InMentalState && pawn.meleeVerbs?.TryGetMeleeVerb(null) != null;

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextTick || (predators.Count == 0 && pendingTick < 0)) return;
            nextTick = pendingTick >= 0 ? System.Math.Min(now + SearchIntervalTicks, pendingTick) : now + SearchIntervalTicks;
            candidateCacheTick = -1;
            if (pendingTick >= 0 && now >= pendingTick)
            {
                pendingTick = -1;
                // The delayed arrival may outlive the visitors or their recruitment.
                if (MouseDisasterRuntime.AllowsNewContent && prey.Any(p => p != null && p.Spawned &&
                    !p.Dead && p.Map == map && !MouseDisasterUtility.IsPlayerAffiliatedRatkin(p)))
                {
                    var wild = map.mapPawns.AllPawnsSpawned.Where(WildPredator).ToList();
                    if (wild.Count > 0)
                    {
                        AddPredator(wild.RandomElement(), false);
                        Find.LetterStack.ReceiveLetter("MouseDisaster_Predation_Label".Translate(),
                            "MouseDisaster_Predation_Text".Translate(), LetterDefOf.ThreatSmall, new LookTargets(prey));
                    }
                    else SpawnOutside();
                }
            }
            for (int i = predators.Count - 1; i >= 0; i--)
            {
                var record = predators[i];
                Pawn pawn = record.pawn;
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map || pawn.Faction != null)
                {
                    if (pawn != null) { byPawn.Remove(pawn); nextSearch.Remove(pawn); vanillaFallback.Remove(pawn); }
                    predators.RemoveAt(i);
                    continue;
                }
                if (!WildPredator(pawn)) continue;
                ObserveMeal(record, pawn);
                if (pawn.CurJob?.exitMapOnArrival == true) continue;
                if (record.fedAfterMeal && MouseDisasterMod.Settings?.wildPredatorsLeaveAfterFed == true)
                {
                    TryStartExitJob(pawn);
                    continue;
                }
                if (IsPredatorFoodJob(pawn)) continue;
                if (record.fedAfterMeal) record.fedAfterMeal = false;
                if (MouseDisasterMod.Settings?.wildPredatorsAvoidRatkinWhenFed != false && !Hungry(pawn)) continue;
                if (TryFoodJob(pawn, out Job job) && job != null)
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
            prey.RemoveAll(p => p == null || (!p.Dead && p.MapHeld != map) ||
                (p.Dead && (p.Corpse == null || p.Corpse.Destroyed)));
        }

        private void AddPredator(Pawn pawn, bool outside)
        {
            if (byPawn.TryGetValue(pawn, out var existing))
            {
                existing.firstHunt = true;
                existing.mealJobActive = false;
                existing.mealJobStartFoodLevel = -1f;
                existing.fedAfterMeal = false;
                return;
            }
            var record = new MouseDisasterPredator { pawn = pawn, outside = outside };
            predators.Add(record);
            byPawn[pawn] = record;
        }

        private static bool IsPredatorFoodJob(Pawn pawn) => pawn?.CurJobDef == JobDefOf.PredatorHunt ||
            pawn?.CurJobDef == JobDefOf.Ingest;

        private static void BeginMealJob(MouseDisasterPredator record, Pawn pawn)
        {
            record.mealJobActive = true;
            record.mealJobStartFoodLevel = pawn.needs?.food?.CurLevelPercentage ?? -1f;
            record.firstHunt = false;
        }

        private static void ObserveMeal(MouseDisasterPredator record, Pawn pawn)
        {
            if (!record.mealJobActive) return;
            float current = pawn.needs?.food?.CurLevelPercentage ?? -1f;
            if (record.mealJobStartFoodLevel >= 0f && current > record.mealJobStartFoodLevel + MealDetectionEpsilon && !Hungry(pawn))
                record.fedAfterMeal = true;
            if (!IsPredatorFoodJob(pawn))
            {
                record.mealJobActive = false;
                record.mealJobStartFoodLevel = -1f;
            }
        }

        private static bool TryMakeExitJob(Pawn pawn, out Job job)
        {
            job = null;
            if (pawn == null || !RCellFinder.TryFindRandomExitSpot(pawn, out IntVec3 exit)) return false;
            job = JobMaker.MakeJob(JobDefOf.Goto, exit);
            job.exitMapOnArrival = true;
            return true;
        }

        private static bool TryStartExitJob(Pawn pawn)
        {
            if (pawn?.jobs == null || !TryMakeExitJob(pawn, out Job job)) return false;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            return true;
        }

        internal bool IsTracked(Pawn pawn) => pawn != null && byPawn.ContainsKey(pawn);

        private bool IsHomeCell(IntVec3 cell) => map.areaManager != null && map.areaManager.Home != null &&
            map.areaManager.Home[cell];

        internal bool ForbiddenTarget(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                if (!pawn.Spawned || pawn.Map != map || !MouseDisasterUtility.IsRatkin(pawn)) return false;
                return MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn) ||
                    MouseDisasterMod.Settings?.wildPredatorsHuntHomeAreaRatkin != true && IsHomeCell(pawn.Position);
            }

            Corpse corpse = thing as Corpse;
            if (corpse == null || corpse.Destroyed || corpse.MapHeld != map || !MouseDisasterUtility.IsRatkin(corpse.InnerPawn)) return false;
            return MouseDisasterUtility.IsPlayerAffiliatedRatkin(corpse.InnerPawn) ||
                MouseDisasterMod.Settings?.wildPredatorsHuntHomeAreaRatkin != true && IsHomeCell(corpse.PositionHeld);
        }

        internal bool ForbiddenCorpse(Thing thing)
        {
            if (thing is Pawn pawn && pawn.Corpse != null) return ForbiddenTarget(pawn.Corpse);
            return ForbiddenTarget(thing);
        }

        private void RefreshCandidateCache(int now)
        {
            if (candidateCacheTick == now) return;
            candidateCacheTick = now;
            cachedWildPrey.Clear();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn != null && pawn.Spawned && !pawn.Dead && MouseDisasterUtility.IsRatkin(pawn) &&
                    !MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn) && pawn.RaceProps.canBePredatorPrey &&
                    pawn.RaceProps.IsFlesh && !ForbiddenTarget(pawn))
                    cachedWildPrey.Add(pawn);

            cachedRatkinCorpses.Clear();
            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                if (thing is Corpse corpse && !corpse.Destroyed && corpse.MapHeld == map && corpse.IngestibleNow &&
                    MouseDisasterUtility.IsRatkin(corpse.InnerPawn) &&
                    !MouseDisasterUtility.IsPlayerAffiliatedRatkin(corpse.InnerPawn) && !ForbiddenTarget(corpse))
                    cachedRatkinCorpses.Add(corpse);
        }

        private void SpawnOutside()
        {
            var kinds = map.Biome.AllWildAnimals.Where(k => k.race.race.predator).ToList();
            if (kinds.Count == 0) kinds.Add(DefDatabase<PawnKindDef>.GetNamed("Wolf_Timber"));
            if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 cell, map, 0f, true))
            {
                Log.Warning("MouseDisaster: no entry cell for outside predators on map " + map.uniqueID);
                return;
            }
            Pawn pawn = PawnGenerator.GeneratePawn(kinds.RandomElement());
            GenSpawn.Spawn(pawn, cell, map);
            AddPredator(pawn, true);
            Find.LetterStack.ReceiveLetter("MouseDisaster_OutsidePredators_Label".Translate(),
                "MouseDisaster_OutsidePredators_Text".Translate(), LetterDefOf.ThreatSmall, pawn);
        }

        private static bool Hungry(Pawn pawn) => pawn?.needs?.food != null &&
            pawn.needs.food.CurLevelPercentage < pawn.RaceProps.FoodLevelPercentageWantEat;

        internal bool IsOutside(Pawn pawn) => pawn != null && byPawn.TryGetValue(pawn, out var record) && record.outside;

        internal static bool ShouldFlee(Pawn predator, Pawn victim) =>
            MouseDisasterMod.Settings?.refugeePredationFightBack == false &&
            predator?.CurJobDef == JobDefOf.PredatorHunt && predator.CurJob.targetA.Pawn == victim &&
            GameComponent_MouseDisasterEventBehavior.Component?.TryGetGroup(victim, out _) == true;

        private static bool CanTargetRatkin(Pawn predator, Pawn target)
        {
            return target != null && target.Spawned && target.Map == predator.Map && !target.Dead &&
                MouseDisasterUtility.IsRatkin(target) && !MouseDisasterUtility.IsPlayerAffiliatedRatkin(target) &&
                target.RaceProps.canBePredatorPrey && target.RaceProps.IsFlesh &&
                target.BodySize <= predator.RaceProps.maxPreyBodySize;
        }

        private Pawn FindVictim(Pawn predator, MouseDisasterPredator record, int now)
        {
            IEnumerable<Pawn> targets;
            if (record.outside)
            {
                RefreshCandidateCache(now);
                targets = cachedWildPrey;
            }
            else
            {
                targets = prey;
            }

            Pawn best = null;
            int bestPriority = int.MaxValue;
            int bestDistance = int.MaxValue;
            foreach (Pawn target in targets)
            {
                if (!CanTargetRatkin(predator, target) || ForbiddenTarget(target) ||
                    !predator.CanReach(target, PathEndMode.Touch, Danger.Deadly)) continue;
                int priority = target.ageTracker != null && target.ageTracker.AgeBiologicalYearsFloat <= 8f ? 0 : 1;
                int distance = target.Position.DistanceToSquared(predator.Position);
                if (priority < bestPriority || (priority == bestPriority && distance < bestDistance))
                {
                    best = target;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private Corpse FindCorpse(Pawn predator, MouseDisasterPredator record, int now)
        {
            IEnumerable<Corpse> candidates;
            if (record.outside)
            {
                RefreshCandidateCache(now);
                candidates = cachedRatkinCorpses;
            }
            else
            {
                candidates = prey.Select(p => p?.Corpse).Where(c => c != null);
            }
            Corpse best = null;
            int bestDistance = int.MaxValue;
            foreach (Corpse corpse in candidates)
            {
                if (corpse == null || !corpse.Spawned || corpse.Destroyed || corpse.MapHeld != map || !corpse.IngestibleNow ||
                    !MouseDisasterUtility.IsRatkin(corpse.InnerPawn) ||
                    MouseDisasterUtility.IsPlayerAffiliatedRatkin(corpse.InnerPawn) || ForbiddenTarget(corpse) ||
                    !predator.RaceProps.CanEverEat(corpse) || !predator.CanReach(corpse, PathEndMode.Touch, Danger.Deadly)) continue;
                int distance = corpse.Position.DistanceToSquared(predator.Position);
                if (distance < bestDistance)
                {
                    best = corpse;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool HasEventFoodOnMap(Pawn predator)
        {
            foreach (Pawn target in prey)
            {
                if (target == null || target.MapHeld != map || !MouseDisasterUtility.IsRatkin(target)) continue;
                if (target.Spawned && !target.Dead &&
                    (ForbiddenTarget(target) || CanTargetRatkin(predator, target))) return true;
                Corpse corpse = target.Corpse;
                if (corpse != null && corpse.Spawned && !corpse.Destroyed && corpse.MapHeld == map &&
                    (ForbiddenTarget(corpse) || (corpse.IngestibleNow && !MouseDisasterUtility.IsPlayerAffiliatedRatkin(corpse.InnerPawn) &&
                        predator.RaceProps.CanEverEat(corpse)))) return true;
            }
            return false;
        }

        // Return true only while this component owns the predator's food choice.
        internal bool TryFoodJob(Pawn pawn, out Job job)
        {
            job = null;
            if (!byPawn.TryGetValue(pawn, out var record) || !WildPredator(pawn)) return false;
            ObserveMeal(record, pawn);
            if (record.fedAfterMeal && MouseDisasterMod.Settings?.wildPredatorsLeaveAfterFed == true &&
                TryMakeExitJob(pawn, out job)) return true;
            bool follow = record.outside && MouseDisasterMod.Settings?.outsidePredatorsFollowDifficulty == true;
            if (MouseDisasterMod.Settings?.wildPredatorsAvoidRatkinWhenFed != false && !Hungry(pawn)) return !follow;
            int now = Find.TickManager.TicksGame;
            if (nextSearch.TryGetValue(pawn, out int until) && now < until) return !(follow && vanillaFallback.Contains(pawn));
            nextSearch[pawn] = now + SearchIntervalTicks;
            vanillaFallback.Remove(pawn);
            Pawn victim = FindVictim(pawn, record, now);
            if (victim != null)
            {
                BeginMealJob(record, pawn);
                job = JobMaker.MakeJob(JobDefOf.PredatorHunt, victim);
                job.killIncappedTarget = true;
                return true;
            }
            Corpse corpse = FindCorpse(pawn, record, now);
            if (corpse != null)
            {
                BeginMealJob(record, pawn);
                job = JobMaker.MakeJob(JobDefOf.Ingest, corpse);
                job.count = 1;
                return true;
            }
            if (record.outside)
            {
                record.firstHunt = false;
                if (follow) { vanillaFallback.Add(pawn); return false; }
                if (Hungry(pawn)) TryMakeExitJob(pawn, out job);
                return true;
            }
            if (!record.outside && HasEventFoodOnMap(pawn)) return true;
            byPawn.Remove(pawn);
            predators.Remove(record);
            nextSearch.Remove(pawn);
            vanillaFallback.Remove(pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    public static class MouseDisasterPredatorFoodPatch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn == null || !pawn.RaceProps.Animal || !pawn.RaceProps.predator || pawn.Faction != null) return true;
            var component = pawn.Map?.GetComponent<MapComponent_MouseDisasterPredation>();
            if (component == null || !component.TryFoodJob(pawn, out Job job)) return true;
            __result = job;
            return false;
        }

        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result == null || pawn == null || !pawn.RaceProps.Animal || !pawn.RaceProps.predator || pawn.Faction != null) return;
            var component = pawn.Map?.GetComponent<MapComponent_MouseDisasterPredation>();
            if (component?.IsTracked(pawn) == true && component.ForbiddenTarget(__result.targetA.Thing))
                __result = null;
        }
    }

    [HarmonyPatch(typeof(JobDriver_PredatorHunt), "MakeNewToils")]
    public static class MouseDisasterPredatorCorpsePatch
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> __result, JobDriver_PredatorHunt __instance)
        {
            var component = __instance.pawn.Map?.GetComponent<MapComponent_MouseDisasterPredation>();
            foreach (Toil toil in __result)
            {
                if (component?.IsTracked(__instance.pawn) == true)
                    toil.FailOn(() => component.ForbiddenTarget(__instance.job.targetA.Thing));
                yield return toil;
            }
        }
    }

    [HarmonyPatch(typeof(JobDriver_Ingest), "MakeNewToils")]
    public static class MouseDisasterPredatorIngestPatch
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> __result, JobDriver_Ingest __instance)
        {
            var component = __instance.pawn.RaceProps.predator ?
                __instance.pawn.Map?.GetComponent<MapComponent_MouseDisasterPredation>() : null;
            foreach (Toil toil in __result)
            {
                if (component?.IsTracked(__instance.pawn) == true)
                    toil.FailOn(() => component.ForbiddenTarget(__instance.job.targetA.Thing));
                yield return toil;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
    public static class MouseDisasterPredationResponsePatch
    {
        public static void Postfix(Pawn ___pawn, DamageInfo dinfo)
        {
            Pawn predator = dinfo.Instigator as Pawn;
            if (___pawn == null || !___pawn.Spawned || ___pawn.Dead || ___pawn.Downed ||
                predator?.CurJobDef != JobDefOf.PredatorHunt || predator.CurJob.targetA.Pawn != ___pawn ||
                GameComponent_MouseDisasterEventBehavior.Component?.TryGetGroup(___pawn, out _) != true) return;
            Job response;
            if (MouseDisasterMod.Settings?.refugeePredationFightBack != false && !___pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                response = JobMaker.MakeJob(JobDefOf.AttackMelee, predator);
                response.expiryInterval = 250;
                response.maxNumMeleeAttacks = 1;
            }
            else response = FleeUtility.FleeJob(___pawn, predator, 16);
            if (response != null) ___pawn.jobs.StartJob(response, JobCondition.InterruptForced);
        }
    }

    [HarmonyPatch(typeof(JobGiver_ReactToCloseMeleeThreat), "TryGiveJob")]
    public static class MouseDisasterPredationNoRetaliationPatch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            Pawn predator = pawn.mindState.meleeThreat;
            if (MouseDisasterMod.Settings?.refugeePredationFightBack != false ||
                predator?.CurJobDef != JobDefOf.PredatorHunt || predator.CurJob.targetA.Pawn != pawn ||
                GameComponent_MouseDisasterEventBehavior.Component?.TryGetGroup(pawn, out _) != true) return true;
            __result = FleeUtility.FleeJob(pawn, predator, 16);
            return false;
        }
    }
}
