using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Incidents
{
    public class RHAH_PredatorRecord : IExposable
    {
        public Verse.Pawn pawn;
        public bool outside;
        public int nextSearchTick = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref outside, "outside", false);
            Scribe_Values.Look(ref nextSearchTick, "nextSearchTick", -1);
        }
    }

    internal static class RHAH_CampPredation
    {
        internal static void Roll(Map map, IList<Verse.Pawn> residents)
        {
            if (map == null || map.Parent is not WorldObject_RHAH_RefugeeCamp || !RHAH_Runtime.AllowsNewContent)
            {
                return;
            }

            MapComponent_RHAH_Map component = map.GetComponent<MapComponent_RHAH_Map>();
            RHAH_Settings settings = RHAH_Mod.Settings;
            float chance = settings == null ? RHAH_PredationRules.DefaultChancePercent : settings.refugeePredationChancePercent;
            if (component == null || !RHAH_PredationRules.Rolls(chance, true, component.PredationRolled))
            {
                return;
            }

            component.PredationRolled = true;
            if (!RHAH_PredationRules.Selected(chance, Rand.Value))
            {
                return;
            }

            component.PredationSelected = true;
            Remember(component, residents);
            HungerAndHavoc.EventMgr.RHAH_EventChainEntry.QueueArrival(map,
                Verse.Find.TickManager.TicksGame + RHAH_PredationRules.ArrivalDelayTicks);
        }

        internal static void Tick(MapComponent_RHAH_Map component, Map map)
        {
            TickSite(component, map, map != null && map.Parent is WorldObject_RHAH_RefugeeCamp);
        }

        internal static void TickSite(MapComponent_RHAH_Map component, Map map, bool allowed)
        {
            if (component == null || map == null || !allowed)
            {
                return;
            }

            int now = Verse.Find.TickManager.TicksGame;

            List<RHAH_PredatorRecord> predators = component.Predators;
            if (!RHAH_PredationRules.ShouldScan(CountActive(predators, map), now, component.PredationNextTick))
            {
                return;
            }

            component.PredationNextTick = RHAH_PredationRules.NextScanTick(now);
            for (int i = predators.Count - 1; i >= 0; i--)
            {
                RHAH_PredatorRecord record = predators[i];
                if (!Active(record?.pawn, map))
                {
                    predators.RemoveAt(i);
                    continue;
                }

                Apply(component, record, now);
            }
        }

        internal static bool TryFoodJob(Verse.Pawn pawn, out Job job)
        {
            job = null;
            Map map = pawn?.Map;
            if (!Active(pawn, map) || map.Parent is not WorldObject_RHAH_RefugeeCamp)
            {
                return false;
            }

            MapComponent_RHAH_Map component = map.GetComponent<MapComponent_RHAH_Map>();
            RHAH_PredatorRecord record = Find(component, pawn);
            if (record == null)
            {
                return false;
            }

            return Apply(component, record, Verse.Find.TickManager.TicksGame, out job);
        }

        internal static bool Forbidden(Thing thing)
        {
            Verse.Pawn pawn = thing as Verse.Pawn;
            Corpse corpse = thing as Corpse;
            if (pawn == null && corpse != null)
            {
                pawn = corpse.InnerPawn;
            }

            Map map = thing?.MapHeld;
            if (pawn == null || map == null || map.Parent is not WorldObject_RHAH_RefugeeCamp)
            {
                return false;
            }

            MapComponent_RHAH_Map component = map.GetComponent<MapComponent_RHAH_Map>();
            if (component == null || !component.IsPredationPrey(pawn))
            {
                return false;
            }

            bool dead = corpse != null || pawn.Dead;
            IntVec3 cell = corpse != null ? corpse.PositionHeld : pawn.Position;
            return !RHAH_PredationRules.CampPrey(true, true, PlayerMember(pawn), Home(map, cell), dead);
        }

        internal static bool ShouldFlee(Verse.Pawn predator, Verse.Pawn victim)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            bool fight = settings == null || settings.refugeePredationFightBack;
            if (fight || predator?.CurJobDef != JobDefOf.PredatorHunt || predator.CurJob?.targetA.Pawn != victim)
            {
                return false;
            }

            Map map = victim?.Map;
            MapComponent_RHAH_Map component = map?.GetComponent<MapComponent_RHAH_Map>();
            return map?.Parent is WorldObject_RHAH_RefugeeCamp && component != null && component.IsPredationPrey(victim);
        }

        internal static void TryArrive(MapComponent_RHAH_Map component, Map map)
        {
            if (!component.PredationSelected || !HasLivingPrey(component, map))
            {
                return;
            }

            Verse.Pawn local = FindLocal(map);
            if (local != null)
            {
                component.TrackPredator(local, false, Verse.Find.TickManager.TicksGame);
                Send("RHAH_Predation_Label", "RHAH_Predation_Text", new LookTargets(local));
                return;
            }

            SpawnOutside(component, map);
        }

        static void SpawnOutside(MapComponent_RHAH_Map component, Map map)
        {
            PawnKindDef kind = PredatorKind(map);
            if (kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 cell, map, CellFinder.EdgeRoadChance_Animal, false, null))
            {
                return;
            }

            Verse.Pawn pawn = PawnGenerator.GeneratePawn(kind);
            GenSpawn.Spawn(pawn, cell, map);
            component.TrackPredator(pawn, true, Verse.Find.TickManager.TicksGame);
            Send("RHAH_OutsidePredators_Label", "RHAH_OutsidePredators_Text", new LookTargets(pawn));
        }

        static bool Apply(MapComponent_RHAH_Map component, RHAH_PredatorRecord record, int now)
        {
            return Apply(component, record, now, out Job job) && Start(record.pawn, job);
        }

        static bool Apply(MapComponent_RHAH_Map component, RHAH_PredatorRecord record, int now, out Job job)
        {
            job = null;
            Verse.Pawn predator = record.pawn;
            bool eating = predator.CurJobDef == JobDefOf.PredatorHunt || predator.CurJobDef == JobDefOf.Ingest;
            int preyIndex = -1;
            int corpseIndex = -1;
            Verse.Pawn prey = FindPrey(predator, component, out preyIndex);
            Corpse corpse = prey == null ? FindCorpse(predator, component, out corpseIndex) : null;
            RHAH_Settings settings = RHAH_Mod.Settings;
            bool follow = settings != null && settings.outsidePredatorsFollowDifficulty;
            RHAH_PredationDecision decision = RHAH_PredationRules.Decide(
                record.outside,
                follow,
                Hungry(predator),
                eating,
                now,
                record.nextSearchTick,
                prey != null,
                corpse != null,
                preyIndex,
                corpseIndex);
            record.nextSearchTick = decision.NextSearchTick;
            if (decision.Action == RHAH_PredationAction.Hunt)
            {
                job = JobMaker.MakeJob(JobDefOf.PredatorHunt, prey);
                job.killIncappedTarget = true;
                return true;
            }

            if (decision.Action == RHAH_PredationAction.EatCorpse)
            {
                job = JobMaker.MakeJob(JobDefOf.Ingest, corpse);
                job.count = 1;
                return true;
            }

            if (decision.Action == RHAH_PredationAction.Leave && TryExit(predator, out job))
            {
                return true;
            }

            return decision.Action == RHAH_PredationAction.Wait && eating;
        }

        static Verse.Pawn FindPrey(Verse.Pawn predator, MapComponent_RHAH_Map component, out int index)
        {
            index = -1;
            List<Verse.Pawn> prey = component.PredationPrey;
            Verse.Pawn best = null;
            int bestPriority = int.MaxValue;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < prey.Count; i++)
            {
                Verse.Pawn target = prey[i];
                if (!PreyNow(predator, target))
                {
                    continue;
                }

                int priority = target.ageTracker != null && target.ageTracker.AgeBiologicalYearsFloat <= 8f ? 0 : 1;
                int distance = (target.Position - predator.Position).LengthHorizontalSquared;
                if (priority < bestPriority || (priority == bestPriority && distance < bestDistance))
                {
                    best = target;
                    index = i;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }

            return best;
        }

        static Corpse FindCorpse(Verse.Pawn predator, MapComponent_RHAH_Map component, out int index)
        {
            index = -1;
            List<Verse.Pawn> prey = component.PredationPrey;
            Corpse best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < prey.Count; i++)
            {
                Verse.Pawn target = prey[i];
                Corpse corpse = target?.Corpse;
                if (corpse == null || corpse.Destroyed || !corpse.Spawned || corpse.MapHeld != predator.Map || !corpse.IngestibleNow)
                {
                    continue;
                }

                if (Forbidden(corpse) || !predator.RaceProps.CanEverEat(corpse) || !predator.CanReach(corpse, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                int distance = (corpse.Position - predator.Position).LengthHorizontalSquared;
                if (distance < bestDistance)
                {
                    best = corpse;
                    index = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        static bool PreyNow(Verse.Pawn predator, Verse.Pawn target)
        {
            if (target == null || target.Dead || !target.Spawned || target.Map != predator.Map)
            {
                return false;
            }

            return !Forbidden(target) &&
                target.RaceProps.canBePredatorPrey &&
                target.RaceProps.IsFlesh &&
                target.BodySize <= predator.RaceProps.maxPreyBodySize &&
                predator.CanReach(target, PathEndMode.Touch, Danger.Deadly);
        }

        static bool HasLivingPrey(MapComponent_RHAH_Map component, Map map)
        {
            List<Verse.Pawn> prey = component.PredationPrey;
            for (int i = 0; i < prey.Count; i++)
            {
                Verse.Pawn pawn = prey[i];
                if (pawn != null && pawn.Spawned && !pawn.Dead && pawn.Map == map && !PlayerMember(pawn))
                {
                    return true;
                }
            }

            return false;
        }

        static Verse.Pawn FindLocal(Map map)
        {
            IReadOnlyList<Verse.Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                if (Wild(spawned[i]))
                {
                    return spawned[i];
                }
            }

            return null;
        }

        static PawnKindDef PredatorKind(Map map)
        {
            List<PawnKindDef> kinds = new List<PawnKindDef>();
            if (map.Biome != null)
            {
                foreach (PawnKindDef kind in map.Biome.AllWildAnimals)
                {
                    if (kind?.RaceProps != null && kind.RaceProps.predator)
                    {
                        kinds.Add(kind);
                    }
                }
            }

            if (kinds.Count == 0)
            {
                return DefDatabase<PawnKindDef>.GetNamedSilentFail("Wolf_Timber");
            }

            return kinds[Rand.Range(0, kinds.Count)];
        }

        static bool Wild(Verse.Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.Faction == null &&
                pawn.RaceProps.Animal && pawn.RaceProps.predator && !pawn.InMentalState;
        }

        static bool Active(Verse.Pawn pawn, Map map)
        {
            return Wild(pawn) && pawn.Map == map;
        }

        static bool Hungry(Verse.Pawn pawn)
        {
            return pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < pawn.RaceProps.FoodLevelPercentageWantEat;
        }

        static bool PlayerMember(Verse.Pawn pawn)
        {
            if (pawn.Faction == Faction.OfPlayer)
            {
                return true;
            }

            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            return comp != null && comp.State.IsReleased;
        }

        static bool Home(Map map, IntVec3 cell)
        {
            return map.areaManager?.Home != null && cell.InBounds(map) && map.areaManager.Home[cell];
        }

        static bool TryExit(Verse.Pawn pawn, out Job job)
        {
            job = null;
            if (!RCellFinder.TryFindRandomExitSpot(pawn, out IntVec3 exit, TraverseMode.ByPawn))
            {
                return false;
            }

            job = JobMaker.MakeJob(JobDefOf.Goto, exit);
            job.exitMapOnArrival = true;
            return true;
        }

        static bool Start(Verse.Pawn pawn, Job job)
        {
            if (pawn?.jobs == null || job == null)
            {
                return false;
            }

            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            return true;
        }

        static RHAH_PredatorRecord Find(MapComponent_RHAH_Map component, Verse.Pawn pawn)
        {
            if (component == null)
            {
                return null;
            }

            List<RHAH_PredatorRecord> predators = component.Predators;
            for (int i = 0; i < predators.Count; i++)
            {
                if (predators[i]?.pawn == pawn)
                {
                    return predators[i];
                }
            }

            return null;
        }

        static int CountActive(List<RHAH_PredatorRecord> predators, Map map)
        {
            int count = 0;
            for (int i = 0; i < predators.Count; i++)
            {
                if (Active(predators[i]?.pawn, map))
                {
                    count++;
                }
            }

            return count;
        }

        static void Remember(MapComponent_RHAH_Map component, IList<Verse.Pawn> residents)
        {
            if (residents == null)
            {
                return;
            }

            for (int i = 0; i < residents.Count; i++)
            {
                Verse.Pawn pawn = residents[i];
                if (RHAH_Race.IsRatkin(pawn?.def))
                {
                    component.RememberPredationPrey(pawn);
                }
            }
        }

        static void Send(string label, string text, LookTargets targets)
        {
            if (Verse.Find.LetterStack == null)
            {
                return;
            }

            Verse.Find.LetterStack.ReceiveLetter(label.Translate(), text.Translate(), LetterDefOf.ThreatSmall, targets);
        }
    }
}
