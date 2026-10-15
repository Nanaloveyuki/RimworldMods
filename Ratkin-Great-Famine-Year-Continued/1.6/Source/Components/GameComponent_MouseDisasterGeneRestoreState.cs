using System.Collections.Generic;
using Verse;

namespace MouseDisaster
{
    public class GameComponent_MouseDisasterGeneRestoreState : GameComponent
    {
        private List<Pawn> manualRemovalPawns = new List<Pawn>();
        private HashSet<int> internalRemovalDepthByPawnId = new HashSet<int>();

        public GameComponent_MouseDisasterGeneRestoreState(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref manualRemovalPawns, "mouseDisaster_manualGeneRemovalPawns", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                manualRemovalPawns ??= new List<Pawn>();
                manualRemovalPawns.RemoveAll(pawn => pawn == null || pawn.Destroyed);
                internalRemovalDepthByPawnId = new HashSet<int>();
            }
        }

        public bool HasManualRemovalMarker(Pawn pawn)
        {
            return pawn != null && manualRemovalPawns != null && manualRemovalPawns.Contains(pawn);
        }

        public void MarkManualRemoval(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            manualRemovalPawns ??= new List<Pawn>();
            if (!manualRemovalPawns.Contains(pawn))
            {
                manualRemovalPawns.Add(pawn);
            }
        }

        public void ClearManualRemoval(Pawn pawn)
        {
            if (pawn == null || manualRemovalPawns == null)
            {
                return;
            }

            manualRemovalPawns.Remove(pawn);
        }

        internal void CleanupExpiredMarkers()
        {
            manualRemovalPawns?.RemoveAll(pawn => pawn == null || pawn.Dead || pawn.Destroyed);
        }

        public void BeginInternalGeneRemoval(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            internalRemovalDepthByPawnId ??= new HashSet<int>();
            internalRemovalDepthByPawnId.Add(pawn.thingIDNumber);
        }

        public void EndInternalGeneRemoval(Pawn pawn)
        {
            if (pawn == null || internalRemovalDepthByPawnId == null)
            {
                return;
            }

            internalRemovalDepthByPawnId.Remove(pawn.thingIDNumber);
        }

        public bool IsInternalGeneRemovalInProgress(Pawn pawn)
        {
            return pawn != null && internalRemovalDepthByPawnId != null && internalRemovalDepthByPawnId.Contains(pawn.thingIDNumber);
        }
    }
}
