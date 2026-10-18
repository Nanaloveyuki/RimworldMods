using RimWorld;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    public class ThoughtWorker_MouseDisasterTraitNearbyDisease : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p?.Map?.mapPawns == null || p.Dead || p.Suspended)
            {
                return ThoughtState.Inactive;
            }

            foreach (Pawn other in p.Map.mapPawns.AllPawnsSpawned)
            {
                if (other == null || other == p || other.Dead)
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

    public class ThoughtWorker_MouseDisasterTraitYoungInNeed : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p?.Map?.mapPawns == null || p.Dead || p.Suspended)
            {
                return ThoughtState.Inactive;
            }

            bool hungry = false;
            Lord lord = p.GetLord();
            foreach (Pawn other in p.Map.mapPawns.AllPawnsSpawned)
            {
                if (other == null || other == p || other.Dead || other.DevelopmentalStage.Adult())
                {
                    continue;
                }

                if (!Related(p, other, lord))
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

        private static bool Related(Pawn pawn, Pawn other, Lord lord)
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
