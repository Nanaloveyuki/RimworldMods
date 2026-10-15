using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static class MouseDisasterPawnGroupUtility
    {
        public static void HoldForDropoff(Pawn child, IntVec3 anchor)
        {
            if (child == null) return;
            child.GetLord()?.RemovePawn(child);
            child.mindState?.mentalStateHandler?.Reset();
            child.jobs?.StopAll();
            if (child.mindState != null)
            {
                child.mindState.canFleeIndividual = false;
                child.mindState.exitMapAfterTick = -1;
                child.mindState.duty = new PawnDuty(DutyDefOf.Defend, anchor, MouseDisasterAbandonedDeliveryPolicy.ChildDropoffRadius);
            }
        }

        public static Lord SendFamilyAway(Map map, Pawn carrier, IEnumerable<Pawn> pawns, IEnumerable<Pawn> children)
        {
            if (map == null || carrier == null || !carrier.Spawned || carrier.Map != map || carrier.Dead || MouseDisasterUtility.IsPlayerAffiliatedRatkin(carrier)) return null;
            var group = (pawns ?? Enumerable.Empty<Pawn>()).Append(carrier)
                .Where(p => p != null && p.Spawned && !p.Dead && p.Map == map && !MouseDisasterUtility.IsPlayerAffiliatedRatkin(p)).Distinct().ToList();
            foreach (Pawn pawn in group)
            {
                pawn.GetLord()?.RemovePawn(pawn);
                MouseDisasterUtility.TryEndLeadYourPetLeashForPet(pawn);
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.StopAll();
                if (pawn.mindState != null) pawn.mindState.exitMapAfterTick = -1;
            }
            return LordMaker.MakeNewLord(carrier.Faction,
                new LordJob_MouseDisasterFamilyExit(carrier, children ?? Enumerable.Empty<Pawn>()), map, group);
        }
    }
}
