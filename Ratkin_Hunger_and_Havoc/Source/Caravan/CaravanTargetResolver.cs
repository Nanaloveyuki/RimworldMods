using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Caravan
{
    internal static class CaravanTargetResolver
    {
        internal static RimWorld.Planet.Caravan ResolvePlayerCaravan()
        {
            List<RimWorld.Planet.Caravan> caravans = new List<RimWorld.Planet.Caravan>();
            List<WorldObject> objects = Find.WorldObjects.AllWorldObjects;
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] is RimWorld.Planet.Caravan caravan && caravan.IsPlayerControlled && caravan.PawnsListForReading.Count > 0)
                {
                    caravans.Add(caravan);
                }
            }

            return caravans.Count == 0 ? null : caravans.RandomElement();
        }

        internal static RimWorld.Planet.Caravan Resolve(RimWorld.Planet.Caravan selected, bool allowFallback)
        {
            if (IsPlayerCaravan(selected))
            {
                return selected;
            }

            return allowFallback ? ResolvePlayerCaravan() : null;
        }

        internal static RimWorld.Planet.Caravan CaravanById(int caravanId)
        {
            if (caravanId <= 0 || Find.WorldObjects == null)
            {
                return null;
            }

            List<WorldObject> objects = Find.WorldObjects.AllWorldObjects;
            for (int i = 0; i < objects.Count; i++)
            {
                RimWorld.Planet.Caravan caravan = objects[i] as RimWorld.Planet.Caravan;
                if (caravan != null && caravan.ID == caravanId)
                {
                    return caravan;
                }
            }

            return null;
        }

        internal static bool IsPlayerCaravan(RimWorld.Planet.Caravan caravan)
        {
            return caravan != null && caravan.IsPlayerControlled && caravan.PawnsListForReading.Count > 0;
        }
    }
}
