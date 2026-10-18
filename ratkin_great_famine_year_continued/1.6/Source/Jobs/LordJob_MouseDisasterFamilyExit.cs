using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public class LordJob_MouseDisasterFamilyExit : LordJob
    {
        public Pawn carrier;
        public List<Pawn> children = new List<Pawn>();
        public LordJob_MouseDisasterFamilyExit() { }
        public LordJob_MouseDisasterFamilyExit(Pawn carrier, IEnumerable<Pawn> children)
        {
            this.carrier = carrier;
            this.children = children.ToList();
        }
        public override void ExposeData()
        {
            Scribe_References.Look(ref carrier, "carrier");
            Scribe_Collections.Look(ref children, "children", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) children ??= new List<Pawn>();
        }
        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_MouseDisasterFamilyExit());
            return graph;
        }
    }

    public class LordToil_MouseDisasterFamilyExit : LordToil
    {
        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in lord.ownedPawns)
                pawn.mindState.duty = new PawnDuty(MouseDisasterDefOf.MouseDisaster_FamilyExit);
        }
    }

    public class JobGiver_MouseDisasterFamilyExit : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var family = pawn.GetLord()?.LordJob as LordJob_MouseDisasterFamilyExit;
            if (family == null) return null;
            if (family.carrier == null || family.carrier.Dead || family.carrier.Downed ||
                MouseDisasterUtility.IsPlayerAffiliatedRatkin(family.carrier))
                family.carrier = pawn.GetLord().ownedPawns.FirstOrDefault(p => !p.Dead && !p.Downed &&
                    !MouseDisasterUtility.IsPlayerAffiliatedRatkin(p) &&
                    p.DevelopmentalStage == DevelopmentalStage.Adult && p.inventory != null);
            if (family.carrier == null || pawn.DevelopmentalStage != DevelopmentalStage.Adult)
                return JobMaker.MakeJob(JobDefOf.Wait, 120);
            if (pawn != family.carrier)
                return family.carrier.Spawned ? JobMaker.MakeJob(JobDefOf.Wait, 120) : MouseDisasterUtility.ExitMapJob(pawn, force: true);

            // Collect actual children at their positions; native inventory ownership survives saving.
            var waiting = family.children.Where(p => p != null && !p.Dead && p.MapHeld == pawn.Map &&
                !MouseDisasterUtility.IsPlayerAffiliatedRatkin(p) && p.ParentHolder != pawn.inventory &&
                !(p.ParentHolder is Pawn_InventoryTracker held && MouseDisasterUtility.IsPlayerAffiliatedRatkin(held.pawn))).ToList();
            if (waiting.Count == 0) return MouseDisasterUtility.ExitMapJob(pawn, force: true);
            foreach (Pawn heldChild in waiting.Where(p => p.ParentHolder is Pawn_InventoryTracker))
            {
                var inventory = (Pawn_InventoryTracker)heldChild.ParentHolder;
                if (inventory.pawn.Dead && inventory.pawn.Corpse?.Spawned == true)
                    inventory.innerContainer.TryDrop(heldChild, inventory.pawn.Corpse.Position, pawn.Map, ThingPlaceMode.Near, out _);
                else if (inventory.pawn.Spawned && pawn.CanReach(inventory.pawn, PathEndMode.Touch, Danger.Deadly))
                    return JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, heldChild, inventory.pawn);
            }
            Pawn child = waiting.Where(p => p.Spawned && pawn.CanReserveAndReach(p, PathEndMode.ClosestTouch, Danger.Deadly))
                .OrderBy(p => pawn.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (child == null) return JobMaker.MakeJob(JobDefOf.Wait, 180);
            Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, child);
            job.count = 1;
            job.checkEncumbrance = false;
            return job;
        }
    }
}
