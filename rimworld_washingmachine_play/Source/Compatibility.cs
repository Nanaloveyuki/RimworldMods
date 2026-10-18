using RimWorld;
using Verse;

namespace WashingMachine
{
    public static class Compatibility
    {
        public static void ApplyAfterWash(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.needs == null)
            {
                return;
            }

            MemoryThoughtHandler memories = pawn.needs.mood?.thoughts?.memories;
            if (memories != null)
            {
                int stage = IsRatkin(pawn) ? Rand.RangeInclusive(4, 6) : Rand.RangeInclusive(0, 3);
                memories.TryGainMemory(ThoughtMaker.MakeThought(WashingMachineDefOf.PWM_Washed, stage));
            }

            // Match the optional mod's existing need; never create hygiene, bladder or thirst needs.
            var needs = pawn.needs.AllNeeds;
            for (int index = 0; index < needs.Count; index++)
            {
                Need need = needs[index];
                if (need.def?.defName == "Hygiene" && need.GetType().FullName == "DubsBadHygiene.Need_Hygiene")
                {
                    need.CurLevelPercentage = 1f;
                }
            }
        }

        private static bool IsRatkin(Pawn pawn)
        {
            // Preserve the normal and inherited subject race identifiers across versions.
            string race = pawn.def?.defName;
            return race == "Ratkin" || race == "Ratkin_Su";
        }
    }
}
