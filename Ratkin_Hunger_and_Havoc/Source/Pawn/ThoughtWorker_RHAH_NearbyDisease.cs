using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;
namespace HungerAndHavoc.Pawn
{
    public class ThoughtWorker_RHAH_NearbyDisease : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Verse.Pawn pawn)
        {
            if (pawn?.Map?.mapPawns == null || pawn.Dead || pawn.Suspended)
            {
                return ThoughtState.Inactive;
            }

            Lord lord = pawn.GetLord();
            if (lord?.ownedPawns != null)
            {
                return Sick(pawn, lord.ownedPawns);
            }

            if (pawn.IsColonist && pawn.Map.mapPawns.FreeColonistsSpawned != null)
            {
                return Sick(pawn, pawn.Map.mapPawns.FreeColonistsSpawned);
            }

            return ThoughtState.Inactive;
        }

        static ThoughtState Sick(Verse.Pawn pawn, List<Verse.Pawn> others)
        {
            for (int i = 0; i < others.Count; i++)
            {
                Verse.Pawn other = others[i];
                if (other == null || other == pawn || other.Dead)
                {
                    continue;
                }

                if (other.health?.hediffSet?.AnyHediffMakesSickThought == true)
                {
                    return ThoughtState.ActiveAtStage(0);
                }
            }

            return ThoughtState.Inactive;
        }
    }
}
