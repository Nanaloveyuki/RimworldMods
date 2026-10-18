using HungerAndHavoc.Identity;
using HungerAndHavoc.Narrative;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    public class WorldObject_RHAH_Interception : MapParent
    {
        string displayId = "";
        int spawnBatchId;
        bool settled;

        public override bool CanReformFoggedEnemies => true;
        public override AcceptanceReport CanBeSettled => false;

        internal bool CanReuse
        {
            get
            {
                if (HasActivePawns())
                {
                    return false;
                }

                Finish();
                return true;
            }
        }

        internal void Configure(string incidentId, int batchId)
        {
            displayId = incidentId;
            spawnBatchId = batchId;
            settled = false;
        }

        internal bool HasActivePawns()
        {
            Map map = Map;
            if (map == null || spawnBatchId <= 0)
            {
                return false;
            }

            System.Collections.Generic.IReadOnlyList<Verse.Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
                if (comp != null && comp.State.spawnBatchId == spawnBatchId &&
                    !pawn.Dead && !pawn.IsPrisoner && !pawn.IsSlave && pawn.Faction != Faction.OfPlayer)
                {
                    return true;
                }
            }

            return false;
        }

        internal void Finish()
        {
            if (settled || spawnBatchId <= 0)
            {
                return;
            }

            Current.Game?.GetComponent<NarrativeState>()?.Commit(book => book.FailInterception(spawnBatchId));
            settled = true;
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!settled && spawnBatchId > 0 && !HasActivePawns())
            {
                Finish();
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            alsoRemoveWorldObject = false;
            Map map = Map;
            if (map == null || map.mapPawns.AnyPawnBlockingMapRemoval ||
                TransporterUtility.IncomingTransporterPreventingMapRemoval(map))
            {
                return false;
            }

            Finish();
            alsoRemoveWorldObject = true;
            return true;
        }

        public override void Notify_MyMapAboutToBeRemoved()
        {
            Finish();
            base.Notify_MyMapAboutToBeRemoved();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref displayId, "displayId", "");
            Scribe_Values.Look(ref spawnBatchId, "spawnBatchId", 0);
            Scribe_Values.Look(ref settled, "settled", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                displayId = displayId ?? "";
            }
        }
    }
}
