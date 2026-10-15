using RimWorld;
using HungerAndHavoc.Api;
using HungerAndHavoc.Incidents;

namespace HungerAndHavoc.EventMgr
{
    internal enum RHAH_FollowMood
    {
        None = 0,
        Temporary = 1,
        Permanent = 2
    }

    internal enum RHAH_FollowTrait
    {
        None = 0,
        Kind = 1,
        Cannibal = 2
    }

    internal readonly struct RHAH_FollowOutcome
    {
        internal string Thought { get; }
        internal int Mood { get; }
        internal RHAH_FollowMood Duration { get; }
        internal RHAH_FollowTrait Trait { get; }
        internal bool Applies { get; }

        internal RHAH_FollowOutcome(string thought, int mood, RHAH_FollowMood duration, RHAH_FollowTrait trait)
        {
            Thought = thought ?? "";
            Mood = mood;
            Duration = duration;
            Trait = trait;
            Applies = !string.IsNullOrEmpty(Thought) && Duration != RHAH_FollowMood.None;
        }

        internal static RHAH_FollowOutcome None => new RHAH_FollowOutcome("", 0, RHAH_FollowMood.None, RHAH_FollowTrait.None);
    }

    internal readonly struct RHAH_FollowSubject
    {
        internal string DisplayId { get; }
        internal RHAH_PawnRole Role { get; }
        internal int LoadId { get; }
        internal int ParentLoadId { get; }
        internal float Age { get; }
        internal bool Alive { get; }
        internal bool OnMap { get; }
        internal bool Joined { get; }
        internal bool InjuredByPlayer { get; }
        internal bool BitFood { get; }
        internal bool BitPerson { get; }
        internal bool Kind { get; }
        internal bool Twisted { get; }
        internal bool InjuredOrSick { get; }
        internal bool RecoveredPlague { get; }
        internal bool Bought { get; }
        internal bool GaveChild { get; }
        internal bool FoodSubstitute { get; }
        internal bool SpreadPlague { get; }
        internal bool ColonistRecovered { get; }
        internal bool StarvedWhileBegging { get; }

        internal RHAH_FollowSubject(
            string displayId,
            RHAH_PawnRole role,
            int loadId,
            int parentLoadId,
            float age,
            bool alive,
            bool onMap,
            bool joined,
            bool injuredByPlayer,
            bool bitFood,
            bool bitPerson,
            bool kind,
            bool twisted,
            bool injuredOrSick,
            bool recoveredPlague,
            bool bought,
            bool gaveChild,
            bool foodSubstitute,
            bool spreadPlague,
            bool colonistRecovered,
            bool starvedWhileBegging)
        {
            DisplayId = displayId ?? "";
            Role = role;
            LoadId = loadId;
            ParentLoadId = parentLoadId;
            Age = age;
            Alive = alive;
            OnMap = onMap;
            Joined = joined;
            InjuredByPlayer = injuredByPlayer;
            BitFood = bitFood;
            BitPerson = bitPerson;
            Kind = kind;
            Twisted = twisted;
            InjuredOrSick = injuredOrSick;
            RecoveredPlague = recoveredPlague;
            Bought = bought;
            GaveChild = gaveChild;
            FoodSubstitute = foodSubstitute;
            SpreadPlague = spreadPlague;
            ColonistRecovered = colonistRecovered;
            StarvedWhileBegging = starvedWhileBegging;
        }
    }

    // 短链捕食、长链和触发心情 不碰地图
    internal static class RHAH_EventFollowRules
    {
        internal const float AdultAge = 14f;
        internal const int ShortPredatorPercent = 10;
        internal const int BirthWatchDays = 3;
        internal const int PlagueBirthWatchDays = 5;
        internal const int TicksPerDay = 60000;
        internal const int MotherReturnDays = 15;
        internal const int LongReturnYears = 2;
        internal const string MotherGoneBad = "RHAH_Thought_MotherGoneBad";
        internal const string MotherGoneGood = "RHAH_Thought_MotherGoneGood";
        internal const string AteMotherGuilt = "RHAH_Thought_AteMotherGuilt";
        internal const string AteMotherFine = "RHAH_Thought_AteMotherFine";
        internal const string MotherSorry = "RHAH_Thought_MotherSorry";
        internal const string MotherFine = "RHAH_Thought_MotherFine";
        internal const string ChildDeadGlad = "RHAH_Thought_ChildDeadGlad";
        internal const string ChildDeadSad = "RHAH_Thought_ChildDeadSad";
        internal const string ChildStarvedSad = "RHAH_Thought_ChildStarvedSad";
        internal const string ChildStarvedGlad = "RHAH_Thought_ChildStarvedGlad";
        internal const string StealHurtBad = "RHAH_Thought_StealHurtBad";
        internal const string StealHurtGood = "RHAH_Thought_StealHurtGood";
        internal const string StealMine = "RHAH_Thought_StealMine";
        internal const string StealGot = "RHAH_Thought_StealGot";
        internal const string SoldAway = "RHAH_Thought_SoldAway";
        internal const string SoldFed = "RHAH_Thought_SoldFed";
        internal const string TradedAway = "RHAH_Thought_TradedAway";
        internal const string TradedFed = "RHAH_Thought_TradedFed";
        internal const string AidAgain = "RHAH_Thought_AidAgain";
        internal const string AidEmpty = "RHAH_Thought_AidEmpty";
        internal const string FineGood = "RHAH_Thought_FineGood";
        internal const string FineKeep = "RHAH_Thought_FineKeep";
        internal const string MedicineEnough = "RHAH_Thought_MedicineEnough";
        internal const string MedicineNext = "RHAH_Thought_MedicineNext";
        internal const string SilverLight = "RHAH_Thought_SilverLight";
        internal const string SilverHard = "RHAH_Thought_SilverHard";
        internal const string ExtraMouth = "RHAH_Thought_ExtraMouth";
        internal const string BornAlive = "RHAH_Thought_BornAlive";
        internal const string FellBad = "RHAH_Thought_FellBad";
        internal const string FellGood = "RHAH_Thought_FellGood";
        internal const string WrongKinBad = "RHAH_Thought_WrongKinBad";
        internal const string WrongKinGood = "RHAH_Thought_WrongKinGood";
        internal const string PlagueMotherDeadBad = "RHAH_Thought_PlagueMotherDeadBad";
        internal const string PlagueMotherDeadGood = "RHAH_Thought_PlagueMotherDeadGood";
        internal const string PlagueLeft = "RHAH_Thought_PlagueLeft";
        internal const string PlagueLived = "RHAH_Thought_PlagueLived";
        internal const string TraderSilent = "RHAH_Thought_TraderSilent";
        internal const string OrphanBad = "RHAH_Thought_OrphanBad";
        internal const string OrphanGood = "RHAH_Thought_OrphanGood";
        internal const string CleanBirth = "RHAH_Thought_CleanBirth";
        internal const string NextBirth = "RHAH_Thought_NextBirth";
        internal const string BornSick = "RHAH_Thought_BornSick";
        internal const string DropSick = "RHAH_Thought_DropSick";
        internal const string DropLived = "RHAH_Thought_DropLived";
        internal const string WrongSick = "RHAH_Thought_WrongSick";
        internal const string WrongFed = "RHAH_Thought_WrongFed";
        internal const string TakenBad = "RHAH_Thought_TakenBad";
        internal const string TakenFed = "RHAH_Thought_TakenFed";


        internal static bool PredatorSelected(int chancePercent, float roll)
        {
            float chance = Clamp(chancePercent) / 100f;
            return roll >= 0f && roll < chance;
        }

        internal static bool HasLongFollow(string displayId)
        {
            switch (displayId)
            {
                case "I-002":
                case "I-012":
                case "I-013":
                case "I-019":
                case "I-032":
                case "I-033":
                case "I-037":
                case "I-041":
                case "I-038":
                case "I-046":
                case "I-047":
                    return true;
                default:
                    return false;
            }
        }

        internal static bool OpensLong(string displayId, bool longChain)
        {
            return longChain && HasLongFollow(displayId);
        }

        internal static int LongDeadline(string displayId, int startedTick)
        {
            if (!HasLongFollow(displayId))
            {
                return -1;
            }

            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            int motherDays = settings == null ? MotherReturnDays : settings.followMotherReturnDays;
            int years = settings == null ? LongReturnYears : settings.followLongReturnYears;
            int days = displayId == "I-037" ? motherDays : years * 60;
            long due = (long)startedTick + (long)days * TicksPerDay;
            return due >= int.MaxValue ? int.MaxValue : (int)due;
        }

        internal static string Payload(string displayId, int batchId, int mapId)
        {
            return (displayId ?? "") + "|" + batchId + "|" + mapId;
        }

        internal static bool ReadPayload(string payload, out string displayId, out int batchId, out int mapId)
        {
            displayId = "";
            batchId = 0;
            mapId = 0;
            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }

            string[] parts = payload.Split('|');
            if (parts.Length != 3 || string.IsNullOrEmpty(parts[0]))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out batchId) || !int.TryParse(parts[2], out mapId) || batchId <= 0)
            {
                return false;
            }

            displayId = parts[0];
            return true;
        }

        internal static RHAH_FollowOutcome AtFourteen(RHAH_FollowSubject subject, bool motherAlive, bool motherOnMap, float roll)
        {
            float adult = Core.RHAH_Mod.Settings == null ? AdultAge : Core.RHAH_Mod.Settings.followAdultAge;
            if (!subject.Alive || subject.Age < adult)
            {
                return RHAH_FollowOutcome.None;
            }

            switch (subject.DisplayId)
            {
                case "I-002":
                    return !motherOnMap ? Pair(subject, roll, MotherGoneBad, -4, MotherGoneGood, 6, true) : RHAH_FollowOutcome.None;
                case "I-003":
                    return motherAlive
                        ? Pair(subject, roll, MotherSorry, -3, MotherFine, 24, true)
                        : Pair(subject, roll, AteMotherGuilt, -6, AteMotherFine, 12, true);
                case "I-007":
                    if (!subject.BitFood && !subject.BitPerson)
                    {
                        return RHAH_FollowOutcome.None;
                    }

                    if (subject.Kind)
                    {
                        return Mood(StealMine, -2, false);
                    }

                    if (subject.Twisted && subject.BitPerson)
                    {
                        return new RHAH_FollowOutcome(StealGot, 6, RHAH_FollowMood.Permanent, RHAH_FollowTrait.Cannibal);
                    }

                    return roll < 0.5f ? Mood(StealMine, -2, false) : Mood(StealGot, 6, false);
                case "I-012":
                    return subject.Bought ? Pair(subject, roll, SoldAway, -5, SoldFed, 4, true) : RHAH_FollowOutcome.None;
                case "I-013":
                    return subject.GaveChild && !subject.FoodSubstitute
                        ? Pair(subject, roll, TradedAway, -8, TradedFed, 6, true)
                        : RHAH_FollowOutcome.None;
                case "I-019":
                    return subject.GaveChild ? Pair(subject, roll, TakenBad, -8, TakenFed, 4, true) : RHAH_FollowOutcome.None;
                case "I-032":
                    return subject.Joined ? Pair(subject, roll, FellBad, -3, FellGood, 4, true) : RHAH_FollowOutcome.None;
                case "I-033":
                    return subject.Joined ? Pair(subject, roll, WrongKinBad, -5, WrongKinGood, 3, true) : RHAH_FollowOutcome.None;
                case "I-037":
                    if (!subject.RecoveredPlague)
                    {
                        return RHAH_FollowOutcome.None;
                    }

                    return motherAlive
                        ? Pair(subject, roll, PlagueLeft, -4, PlagueLived, 3, false)
                        : Pair(subject, roll, PlagueMotherDeadBad, -6, PlagueMotherDeadGood, 4, true);
                case "I-041":
                    return subject.RecoveredPlague ? Pair(subject, roll, OrphanBad, -5, OrphanGood, 3, true) : RHAH_FollowOutcome.None;
                case "I-046":
                    return subject.Joined && subject.RecoveredPlague
                        ? Pair(subject, roll, DropSick, -6, DropLived, 2, true)
                        : RHAH_FollowOutcome.None;
                case "I-047":
                    return subject.Joined && subject.RecoveredPlague
                        ? Pair(subject, roll, WrongSick, -6, WrongFed, 3, true)
                        : RHAH_FollowOutcome.None;
                default:
                    return RHAH_FollowOutcome.None;
            }
        }

        internal static RHAH_FollowOutcome OnChildDeath(RHAH_FollowSubject child, bool motherAlive, float roll)
        {
            if (child.DisplayId != "I-003" || child.Alive || child.Age >= AdultAge || !motherAlive)
            {
                return RHAH_FollowOutcome.None;
            }

            return roll < 0.5f ? Mood(ChildDeadGlad, 3, true) : Mood(ChildDeadSad, -3, false);
        }

        internal static RHAH_FollowOutcome OnStarvedChild(RHAH_FollowSubject mother, float roll)
        {
            if (mother.DisplayId != "I-004" || !mother.Alive || mother.Role != RHAH_PawnRole.BeggarMother)
            {
                return RHAH_FollowOutcome.None;
            }

            if (mother.Kind)
            {
                return Mood(ChildStarvedSad, -6, true);
            }

            if (mother.Twisted)
            {
                return Mood(ChildStarvedGlad, 3, true);
            }

            return roll < 0.5f ? Mood(ChildStarvedGlad, 3, false) : Mood(ChildStarvedSad, -6, false);
        }

        internal static RHAH_FollowOutcome OnJoinedAfterHarm(RHAH_FollowSubject subject, float roll)
        {
            if (subject.DisplayId != "I-006" || !subject.Alive || !subject.Joined || !subject.InjuredByPlayer)
            {
                return RHAH_FollowOutcome.None;
            }

            if (subject.Kind)
            {
                return Mood(StealHurtBad, -3, false);
            }

            if (subject.Twisted)
            {
                return Mood(StealHurtGood, 4, true);
            }

            return roll < 0.5f ? Mood(StealHurtGood, 4, false) : Mood(StealHurtBad, -3, false);
        }

        internal static RHAH_FollowOutcome OnRelief(string displayId, bool counted, bool alive, bool injuredOrSick, bool oldest, float roll)
        {
            if (!counted || !alive)
            {
                return RHAH_FollowOutcome.None;
            }

            switch (displayId)
            {
                case "I-015":
                    return roll < 0.5f ? Mood(AidAgain, 2, false) : Mood(AidEmpty, -2, false);
                case "I-016":
                    return roll < 0.6f ? Mood(FineGood, 4, false) : Mood(FineKeep, -2, false);
                case "I-017":
                    if (!injuredOrSick)
                    {
                        return RHAH_FollowOutcome.None;
                    }

                    return roll < 0.5f ? Mood(MedicineEnough, 3, false) : Mood(MedicineNext, -4, false);
                case "I-018":
                    if (!oldest)
                    {
                        return RHAH_FollowOutcome.None;
                    }

                    return roll < 0.5f ? Mood(SilverLight, 3, false) : Mood(SilverHard, -3, false);
                default:
                    return RHAH_FollowOutcome.None;
            }
        }

        internal static RHAH_FollowOutcome OnBirth(string displayId, bool motherAlive, bool childAlive, bool childSick, int days, bool motherKind, bool motherTwisted, float roll)
        {
            int birthDays = Core.RHAH_Mod.Settings == null ? BirthWatchDays : Core.RHAH_Mod.Settings.followBirthWatchDays;
            if (displayId == "I-029" && motherAlive && childAlive && days >= birthDays)
            {
                return BirthMood(motherKind, motherTwisted, roll, ExtraMouth, -4, BornAlive, 2, true);
            }

            if (displayId != "I-044" || !motherAlive)
            {
                return RHAH_FollowOutcome.None;
            }

            if (childSick)
            {
                return Mood(BornSick, -6, false);
            }

            int plagueDays = Core.RHAH_Mod.Settings == null ? PlagueBirthWatchDays : Core.RHAH_Mod.Settings.followPlagueBirthDays;
            if (childAlive && days >= plagueDays)
            {
                return BirthMood(motherKind, motherTwisted, roll, CleanBirth, 3, NextBirth, -3, true);
            }

            return RHAH_FollowOutcome.None;
        }

        internal static RHAH_FollowOutcome OnTraderPlague(bool spread, bool colonistRecovered)
        {
            return spread && colonistRecovered ? Mood(TraderSilent, -4, false) : RHAH_FollowOutcome.None;
        }

        internal static bool LinksFamily(string displayId, int index, RHAH_PawnRole role)
        {
            if (index < 0)
            {
                return false;
            }

            if (displayId == "I-003" || displayId == "I-004")
            {
                return index == 0 ? role == RHAH_PawnRole.Mother || role == RHAH_PawnRole.BeggarMother : IsYoung(role);
            }

            return false;
        }

        internal static bool IsYoung(RHAH_PawnRole role)
        {
            return role == RHAH_PawnRole.BeggarChild ||
                role == RHAH_PawnRole.ThiefChild ||
                role == RHAH_PawnRole.WildChild ||
                role == RHAH_PawnRole.RatkinYoung;
        }

        internal static bool BatchOutcomeDone(int pending, int finished)
        {
            return pending == 0 && finished > 0;
        }

        internal static int Clamp(int percent)
        {
            if (percent < 0)
            {
                return 0;
            }

            return percent > 100 ? 100 : percent;
        }

        static int Scaled(int mood)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            int percent = settings == null ? 100 : settings.followMoodScalePercent;
            if (percent == 100 || mood == 0)
            {
                return mood;
            }

            int scaled = mood * percent / 100;
            if (scaled == 0)
            {
                return mood < 0 ? -1 : 1;
            }

            return scaled;
        }

        static RHAH_FollowOutcome Pair(RHAH_FollowSubject subject, float roll, string dark, int darkMood, string light, int lightMood, bool grantTrait)
        {
            darkMood = Scaled(darkMood);
            lightMood = Scaled(lightMood);
            if (subject.Kind)
            {
                RHAH_FollowTrait trait = grantTrait ? RHAH_FollowTrait.Kind : RHAH_FollowTrait.None;
                return new RHAH_FollowOutcome(dark, darkMood, RHAH_FollowMood.Permanent, trait);
            }

            if (subject.Twisted)
            {
                RHAH_FollowTrait trait = grantTrait && lightMood > 0 && subject.DisplayId == "I-003"
                    ? RHAH_FollowTrait.Cannibal
                    : RHAH_FollowTrait.None;
                return new RHAH_FollowOutcome(light, lightMood, RHAH_FollowMood.Permanent, trait);
            }

            if (roll < 0.5f)
            {
                return new RHAH_FollowOutcome(dark, darkMood, DarkPermanent(subject.DisplayId) ? RHAH_FollowMood.Permanent : RHAH_FollowMood.Temporary, grantTrait ? RHAH_FollowTrait.Kind : RHAH_FollowTrait.None);
            }

            return new RHAH_FollowOutcome(light, lightMood, RHAH_FollowMood.Temporary, grantTrait && subject.DisplayId == "I-003" ? RHAH_FollowTrait.Cannibal : RHAH_FollowTrait.None);
        }

        static bool DarkPermanent(string displayId)
        {
            return displayId == "I-003" || displayId == "I-013" || displayId == "I-019" || displayId == "I-037" || displayId == "I-041" || displayId == "I-046" || displayId == "I-047";
        }

        static RHAH_FollowOutcome BirthMood(bool kind, bool twisted, float roll, string first, int firstMood, string second, int secondMood, bool kindTakesSecond)
        {
            firstMood = Scaled(firstMood);
            secondMood = Scaled(secondMood);
            if (kind)
            {
                return Mood(kindTakesSecond ? second : first, kindTakesSecond ? secondMood : firstMood, false);
            }

            if (twisted)
            {
                return Mood(kindTakesSecond ? first : second, kindTakesSecond ? firstMood : secondMood, false);
            }

            return roll < 0.5f ? Mood(first, firstMood, false) : Mood(second, secondMood, false);
        }

        static RHAH_FollowOutcome Mood(string thought, int mood, bool permanent)
        {
            return new RHAH_FollowOutcome(thought, mood, permanent ? RHAH_FollowMood.Permanent : RHAH_FollowMood.Temporary, RHAH_FollowTrait.None);
        }
    }
}
