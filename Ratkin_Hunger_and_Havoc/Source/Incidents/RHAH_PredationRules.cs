using RimWorld;


namespace HungerAndHavoc.Incidents
{
    internal enum RHAH_PredationAction
    {
        None = 0,
        Hunt = 1,
        EatCorpse = 2,
        Leave = 3,
        Vanilla = 4,
        Wait = 5
    }

    internal readonly struct RHAH_PredationDecision
    {
        public RHAH_PredationAction Action { get; }
        public int TargetIndex { get; }
        public int NextSearchTick { get; }

        public RHAH_PredationDecision(RHAH_PredationAction action, int targetIndex, int nextSearchTick)
        {
            Action = action;
            TargetIndex = targetIndex;
            NextSearchTick = nextSearchTick;
        }
    }

    internal static class RHAH_PredationRules
    {
        internal const int SearchIntervalTicks = GenDate.TicksPerHour * 2;
        internal const int ArrivalDelayTicks = 120;
        internal const float DefaultChancePercent = 10f;
        internal const string CorePackageId = "ludeon.rimworld";

        internal static readonly string[] VanillaWeapons =
        {
            "MeleeWeapon_Club",
            "MeleeWeapon_Ikwa",
            "MeleeWeapon_Spear",
            "Bow_Short"
        };

        internal static float ClampChance(float percent)
        {
            if (float.IsNaN(percent))
            {
                return DefaultChancePercent;
            }

            if (percent < 0f)
            {
                return 0f;
            }

            return percent > 100f ? 100f : percent;
        }

        internal static bool Rolls(float chancePercent, bool campMap, bool alreadyRolled)
        {
            return campMap && !alreadyRolled && ClampChance(chancePercent) > 0f;
        }

        internal static bool Selected(float chancePercent, float roll)
        {
            float chance = ClampChance(chancePercent);
            if (chance <= 0f)
            {
                return false;
            }

            if (chance >= 100f)
            {
                return true;
            }

            return roll < chance / 100f;
        }

        internal static bool VanillaWeapon(string defName, bool coreMod, bool melee, int tech, bool shortBow, bool wooden)
        {
            if (!coreMod || string.IsNullOrEmpty(defName) || !wooden)
            {
                return false;
            }

            if (shortBow)
            {
                return defName == "Bow_Short";
            }

            return melee && tech >= 2 && tech <= 3 && Listed(defName);
        }

        internal static bool CampPrey(bool ratkin, bool campResident, bool playerMember, bool inHome, bool dead)
        {
            return ratkin && campResident && !playerMember && !(inHome && dead);
        }

        internal static bool Reachable(bool spawned, bool sameMap, bool reachable)
        {
            return spawned && sameMap && reachable;
        }

        internal static bool ShouldScan(int activePredators, int now, int nextTick)
        {
            return activePredators > 0 && now >= nextTick;
        }

        internal static int NextScanTick(int now)
        {
            return now + SearchIntervalTicks;
        }

        internal static RHAH_PredationDecision Decide(
            bool outside,
            bool followDifficulty,
            bool hungry,
            bool eating,
            int now,
            int nextSearchTick,
            bool anyPrey,
            bool anyCorpse,
            int preyIndex,
            int corpseIndex)
        {
            if (eating)
            {
                return new RHAH_PredationDecision(RHAH_PredationAction.Wait, -1, nextSearchTick);
            }

            if (now < nextSearchTick)
            {
                return new RHAH_PredationDecision(RHAH_PredationAction.Wait, -1, nextSearchTick);
            }

            int next = NextScanTick(now);
            if (!hungry)
            {
                return new RHAH_PredationDecision(RHAH_PredationAction.Wait, -1, next);
            }

            if (anyPrey && preyIndex >= 0)
            {
                return new RHAH_PredationDecision(RHAH_PredationAction.Hunt, preyIndex, next);
            }

            if (anyCorpse && corpseIndex >= 0)
            {
                return new RHAH_PredationDecision(RHAH_PredationAction.EatCorpse, corpseIndex, next);
            }

            if (outside && followDifficulty)
            {
                return new RHAH_PredationDecision(RHAH_PredationAction.Vanilla, -1, next);
            }

            if (outside)
            {
                return new RHAH_PredationDecision(RHAH_PredationAction.Leave, -1, next);
            }

            return new RHAH_PredationDecision(RHAH_PredationAction.Wait, -1, next);
        }

        internal static bool FightsBack(bool fightBack, bool violent)
        {
            return fightBack && violent;
        }

        static bool Listed(string defName)
        {
            for (int i = 0; i < VanillaWeapons.Length; i++)
            {
                if (VanillaWeapons[i] == defName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
