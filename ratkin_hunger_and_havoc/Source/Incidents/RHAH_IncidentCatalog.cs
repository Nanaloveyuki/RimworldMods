using System;
using System.Collections.Generic;
using HungerAndHavoc.Api;

namespace HungerAndHavoc.Incidents
{
    internal enum RHAH_IncidentFamily
    {
        Beggar = 0,
        Thief = 1,
        Wild = 2,
        Aid = 3,
        Intel = 4,
        Siege = 5,
        Trade = 6,
        Special = 7
    }

    internal enum RHAH_IncidentOrigin
    {
        Original = 0,
        Sequel = 1
    }

    internal enum RHAH_IncidentCategory
    {
        Hunger = 0,
        Plague = 1
    }

    internal enum RHAH_IncidentTarget
    {
        Map = 0,
        Caravan = 1
    }

    internal enum RHAH_AttitudePool
    {
        Positive = 0,
        Negative = 1
    }

    internal sealed class RHAH_IncidentEntry
    {
        public string DisplayId { get; }
        public string DefName { get; }
        public string LabelKey { get; }
        public RHAH_IncidentFamily Family { get; }
        public RHAH_IncidentOrigin Origin { get; }
        public RHAH_IncidentCategory Category { get; }
        public RHAH_IncidentTarget Target { get; }
        public bool BroadcastEligible { get; }
        public RHAH_AttitudePool DefaultAttitudePool { get; }
        public RHAH_Attitude DefaultAttitude { get; }
        public float DebugPoints { get; }

        public RHAH_IncidentEntry(
            string displayId,
            string defName,
            RHAH_IncidentFamily family,
            RHAH_IncidentOrigin origin,
            RHAH_IncidentCategory category,
            RHAH_IncidentTarget target,
            bool broadcastEligible,
            RHAH_AttitudePool defaultAttitudePool,
            RHAH_Attitude defaultAttitude,
            float debugPoints,
            string labelKey = null)
        {
            DisplayId = displayId;
            DefName = defName;
            LabelKey = labelKey ?? "RHAH_Incident_" + defName.Substring("RHAH_".Length) + "_Label";
            Family = family;
            Origin = origin;
            Category = category;
            Target = target;
            BroadcastEligible = broadcastEligible;
            DefaultAttitudePool = defaultAttitudePool;
            DefaultAttitude = defaultAttitude;
            DebugPoints = debugPoints;
        }
    }

    internal static class RHAH_IncidentCatalog
    {
        static readonly List<RHAH_IncidentEntry> entries = new List<RHAH_IncidentEntry>
        {
            Original("I-001", "RHAH_LargeRefugeeWave", RHAH_IncidentFamily.Beggar, true, RHAH_AttitudePool.Negative, 700f),
            Original("I-002", "RHAH_AbandonedRatkinChildren", RHAH_IncidentFamily.Special, true, RHAH_AttitudePool.Negative, 400f),
            Original("I-003", "RHAH_ShatteredMother", RHAH_IncidentFamily.Special, true, RHAH_AttitudePool.Negative, 400f),
            Original("I-004", "RHAH_BeggarFamily", RHAH_IncidentFamily.Beggar, true, RHAH_AttitudePool.Negative, 200f),
            Original("I-005", "RHAH_BeggarGroup", RHAH_IncidentFamily.Beggar, true, RHAH_AttitudePool.Negative, 300f),
            Original("I-006", "RHAH_ThiefRatkinGroup", RHAH_IncidentFamily.Thief, true, RHAH_AttitudePool.Negative, 300f),
            Original("I-007", "RHAH_ThiefRatkinChildGroup", RHAH_IncidentFamily.Thief, true, RHAH_AttitudePool.Negative, 300f),
            Original("I-008", "RHAH_WildRatkinWandersIn", RHAH_IncidentFamily.Wild, true, RHAH_AttitudePool.Negative, 150f),
            Original("I-009", "RHAH_WildRatkinChildWandersIn", RHAH_IncidentFamily.Wild, true, RHAH_AttitudePool.Negative, 150f),
            Original("I-010", "RHAH_WildRatkinGroupWandersIn", RHAH_IncidentFamily.Wild, true, RHAH_AttitudePool.Negative, 400f),
            Original("I-011", "RHAH_FamineRefugees", RHAH_IncidentFamily.Beggar, true, RHAH_AttitudePool.Positive, 350f),
            Original("I-012", "RHAH_RatkinTraderCaravan", RHAH_IncidentFamily.Trade, true, RHAH_AttitudePool.Positive, 350f),
            Original("I-013", "RHAH_ChildExchange", RHAH_IncidentFamily.Special, true, RHAH_AttitudePool.Negative, 200f),
            Original("I-014", "RHAH_BeggarSiege", RHAH_IncidentFamily.Siege, false, RHAH_AttitudePool.Negative, 300f),
            Sequel("I-015", "RHAH_AidSimpleMeal", RHAH_IncidentFamily.Aid, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-016", "RHAH_AidFineMeal", RHAH_IncidentFamily.Aid, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-017", "RHAH_AidMedicine", RHAH_IncidentFamily.Aid, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-018", "RHAH_AidSilver", RHAH_IncidentFamily.Aid, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-019", "RHAH_AidBaby", RHAH_IncidentFamily.Aid, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-020", "RHAH_IntelTreasureSimpleMeal", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-021", "RHAH_IntelTreasureHerbal", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-022", "RHAH_IntelTreasureSilver", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-023", "RHAH_IntelStructureSimpleMeal", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-024", "RHAH_IntelStructureHerbal", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-025", "RHAH_IntelStructureSilver", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-026", "RHAH_IntelSettlementSimpleMeal", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-027", "RHAH_IntelSettlementHerbal", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-028", "RHAH_IntelSettlementSilver", RHAH_IncidentFamily.Intel, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Positive, 300f),
            Sequel("I-029", "RHAH_LaboringRefugees", RHAH_IncidentFamily.Beggar, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 260f),
            Sequel("I-030", "RHAH_StrongSiege", RHAH_IncidentFamily.Siege, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 320f),
            Sequel("I-031", "RHAH_Passersby", RHAH_IncidentFamily.Beggar, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 300f),
            Sequel("I-032", "RHAH_AirdropMistake", RHAH_IncidentFamily.Special, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 300f),
            Sequel("I-033", "RHAH_MisguidedKinship", RHAH_IncidentFamily.Special, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 200f),
            Sequel("I-034", "RHAH_GreatFamine", RHAH_IncidentFamily.Thief, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 600f),
            Sequel("I-035", "RHAH_CaravanMuggers", RHAH_IncidentFamily.Thief, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Caravan, RHAH_AttitudePool.Negative, 320f),
            Sequel("I-036", "RHAH_PlagueWanderers", RHAH_IncidentFamily.Wild, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 300f),
            Sequel("I-037", "RHAH_PlagueAbandonedBabies", RHAH_IncidentFamily.Special, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 300f),
            Sequel("I-038", "RHAH_PlagueTraderCaravan", RHAH_IncidentFamily.Trade, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 350f),
            Sequel("I-039", "RHAH_PlaguePassersby", RHAH_IncidentFamily.Beggar, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 350f),
            Sequel("I-040", "RHAH_PlagueRefugees", RHAH_IncidentFamily.Beggar, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 350f),
            Sequel("I-041", "RHAH_PlagueOrphan", RHAH_IncidentFamily.Wild, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 200f),
            Sequel("I-042", "RHAH_PlagueBeggarGroup", RHAH_IncidentFamily.Beggar, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 300f),
            Sequel("I-043", "RHAH_PlagueThiefGroup", RHAH_IncidentFamily.Thief, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 300f),
            Sequel("I-044", "RHAH_PlagueLaboringRefugees", RHAH_IncidentFamily.Beggar, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 260f),
            Sequel("I-045", "RHAH_PlagueStrongSiege", RHAH_IncidentFamily.Siege, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 320f),
            Sequel("I-046", "RHAH_PlagueAirdropMistake", RHAH_IncidentFamily.Special, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 300f),
            Sequel("I-047", "RHAH_PlagueMisguidedKinship", RHAH_IncidentFamily.Special, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 200f),
            Sequel("I-048", "RHAH_PlagueGreatFamine", RHAH_IncidentFamily.Thief, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 600f),
            Sequel("I-049", "RHAH_PlagueRevenge", RHAH_IncidentFamily.Special, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 100f),
            Sequel("I-050", "RHAH_PlagueCaravanMuggers", RHAH_IncidentFamily.Thief, RHAH_IncidentCategory.Plague, RHAH_IncidentTarget.Caravan, RHAH_AttitudePool.Negative, 320f),
            Sequel("I-051", "RHAH_RefugeeMassacre", RHAH_IncidentFamily.Special, RHAH_IncidentCategory.Hunger, RHAH_IncidentTarget.Map, RHAH_AttitudePool.Negative, 300f)
        };

        public static IReadOnlyList<RHAH_IncidentEntry> All => entries;

        static RHAH_IncidentCatalog()
        {
            entries.Add(SubEvent("I-008-01", "RHAH_FeralRatkinWandersIn", "I-008"));
            entries.Add(SubEvent("I-009-01", "RHAH_FeralRatkinChildWandersIn", "I-009"));
            entries.Add(SubEvent("I-010-01", "RHAH_FeralRatkinGroupWandersIn", "I-010"));
            entries.Add(Original("I-052", "RHAH_MouseSwarm", RHAH_IncidentFamily.Thief, true, RHAH_AttitudePool.Negative, 700f));
            entries.Add(Original("I-053", "RHAH_PleadingJoin", RHAH_IncidentFamily.Beggar, true, RHAH_AttitudePool.Positive, 200f));
            entries.Add(Original("I-054", "RHAH_BanditAttack", RHAH_IncidentFamily.Thief, false, RHAH_AttitudePool.Negative, 400f));
        }

        public static RHAH_IncidentEntry GetByDisplayId(string displayId)
        {
            if (string.IsNullOrEmpty(displayId))
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].DisplayId, displayId, StringComparison.Ordinal))
                {
                    return entries[i];
                }
            }

            return null;
        }

        public static RHAH_IncidentEntry GetByDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].DefName, defName, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[i];
                }
            }

            return null;
        }

        static RHAH_IncidentEntry SubEvent(string displayId, string defName, string parentId)
        {
            RHAH_IncidentEntry parent = GetByDisplayId(parentId);
            return new RHAH_IncidentEntry(displayId, defName, parent.Family, parent.Origin,
                parent.Category, parent.Target, parent.BroadcastEligible, parent.DefaultAttitudePool,
                parent.DefaultAttitude, parent.DebugPoints, parent.LabelKey);
        }

        static RHAH_IncidentEntry Original(
            string displayId,
            string defName,
            RHAH_IncidentFamily family,
            bool broadcastEligible,
            RHAH_AttitudePool pool,
            float debugPoints)
        {
            return new RHAH_IncidentEntry(
                displayId,
                defName,
                family,
                RHAH_IncidentOrigin.Original,
                RHAH_IncidentCategory.Hunger,
                RHAH_IncidentTarget.Map,
                broadcastEligible,
                pool,
                OriginalAttitude(displayId),
                debugPoints);
        }


        static RHAH_Attitude OriginalAttitude(string displayId)
        {
            switch (displayId)
            {
                case "I-006":
                case "I-007":
                case "I-014":
                case "I-054":
                    return RHAH_Attitude.Hostile;
                case "I-008":
                case "I-009":
                case "I-010":
                case "I-012":
                case "I-052":
                    return RHAH_Attitude.Neutral;
                default:
                    return RHAH_Attitude.LeaningFriendly;
            }
        }

        static RHAH_Attitude SequelAttitude(string displayId)
        {
            switch (displayId)
            {
                case "I-015":
                case "I-016":
                case "I-017":
                case "I-018":
                case "I-019":
                case "I-020":
                case "I-021":
                case "I-022":
                case "I-023":
                case "I-024":
                case "I-025":
                case "I-026":
                case "I-027":
                case "I-028":
                    return RHAH_Attitude.LeaningFriendly;
                case "I-030":
                case "I-043":
                case "I-045":
                    return RHAH_Attitude.Hostile;
                case "I-034":
                case "I-048":
                    return RHAH_Attitude.LeaningHostile;
                default:
                    return RHAH_Attitude.Neutral;
            }
        }
        static RHAH_IncidentEntry Sequel(
            string displayId,
            string defName,
            RHAH_IncidentFamily family,
            RHAH_IncidentCategory category,
            RHAH_IncidentTarget target,
            RHAH_AttitudePool pool,
            float debugPoints)
        {
            return new RHAH_IncidentEntry(
                displayId,
                defName,
                family,
                RHAH_IncidentOrigin.Sequel,
                category,
                target,
                broadcastEligible: false,
                pool,
                SequelAttitude(displayId),
                debugPoints);
        }
    }
}
