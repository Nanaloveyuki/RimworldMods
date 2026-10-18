using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    public class ThoughtWorker_RHAH_YoungInNeed : ThoughtWorker
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
                return Need(pawn, lord.ownedPawns, other => Related(pawn, other, lord));
            }

            if (pawn.Faction == Faction.OfPlayer && pawn.Map.mapPawns.FreeColonistsSpawned != null)
            {
                return Need(pawn, pawn.Map.mapPawns.FreeColonistsSpawned, other => Related(pawn, other, null));
            }

            return ThoughtState.Inactive;
        }

        static ThoughtState Need(Verse.Pawn pawn, List<Verse.Pawn> others, System.Func<Verse.Pawn, bool> related)
        {
            bool hungry = false;
            for (int i = 0; i < others.Count; i++)
            {
                Verse.Pawn other = others[i];
                if (other == null || other == pawn || other.Dead || other.DevelopmentalStage.Adult())
                {
                    continue;
                }

                if (!related(other))
                {
                    continue;
                }

                if (other.Downed || other.health?.hediffSet?.AnyHediffMakesSickThought == true)
                {
                    return ThoughtState.ActiveAtStage(1);
                }

                if (other.needs?.food != null && other.needs.food.CurCategory >= HungerCategory.Hungry)
                {
                    hungry = true;
                }
            }

            return hungry ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
        static bool Related(Verse.Pawn pawn, Verse.Pawn other, Lord lord)
        {
            if (pawn.Faction != null && pawn.Faction == other.Faction)
            {
                return true;
            }

            if (lord != null && other.GetLord() == lord)
            {
                return true;
            }

            return pawn.Faction == Faction.OfPlayer &&
                (other.IsColonist || other.IsPrisonerOfColony || other.IsSlaveOfColony);
        }
    }
}
