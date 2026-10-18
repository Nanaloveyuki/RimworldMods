using System;
using Verse;
using System.Collections.Generic;

namespace HungerAndHavoc.Incidents
{
    public enum RHAH_RequestKind
    {
        None = 0,
        SimpleMeal = 1,
        FineMeal = 2,
        Medicine = 3,
        Silver = 4,
        Baby = 5,
        HerbalMedicine = 6
    }

    public enum RHAH_IntelSiteKind
    {
        None = 0,
        Treasure = 1,
        Structure = 2,
        Settlement = 3
    }

    public enum RHAH_ChoiceKind
    {
        None = 0,
        Aid = 1,
        Intel = 2,
        Refugees = 3,
        Abandoned = 4,
        ChildExchange = 5,
        Kinship = 6,
        Airdrop = 7,
        Visitors = 8
    }

    public enum RHAH_ChoiceAction
    {
        None = 0,
        Deliver = 1,
        Reject = 2,
        Ignore = 3,
        Join = 4,
        Hire = 5,
        Feed = 6,
        Timeout = 7,
        Recruit = 8,
        Enslave = 9,
        Capture = 10,
        Attack = 11,
        Ally = 13
    }

    internal readonly struct RHAH_RequestSpec
    {
        public RHAH_RequestKind Kind { get; }
        public RHAH_IntelSiteKind Site { get; }
        public RHAH_ChoiceKind Choice { get; }

        public RHAH_RequestSpec(RHAH_RequestKind kind, RHAH_IntelSiteKind site, RHAH_ChoiceKind choice)
        {
            Kind = kind;
            Site = site;
            Choice = choice;
        }
    }

    internal static class RHAH_RequestRules
    {
        internal const int MinSimple = 6;
        internal const int MaxSimple = 28;
        internal const int MinFine = 4;
        internal const int MaxFine = 18;
        internal const int MinMedicine = 2;
        internal const int MaxMedicine = 10;
        internal const int MinSilver = 80;
        internal const int MaxSilver = 1200;
        internal const int MinHerbal = 3;
        internal const int MaxHerbal = 15;
        internal const int RequestDays = 1;
        internal const int TicksPerDay = 60000;

        internal static RHAH_RequestSpec SpecFor(string displayId)
        {
            switch (displayId)
            {
                case "I-015": return new RHAH_RequestSpec(RHAH_RequestKind.SimpleMeal, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Aid);
                case "I-016": return new RHAH_RequestSpec(RHAH_RequestKind.FineMeal, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Aid);
                case "I-017": return new RHAH_RequestSpec(RHAH_RequestKind.Medicine, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Aid);
                case "I-018": return new RHAH_RequestSpec(RHAH_RequestKind.Silver, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Aid);
                case "I-019": return new RHAH_RequestSpec(RHAH_RequestKind.Baby, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Aid);
                case "I-020": return new RHAH_RequestSpec(RHAH_RequestKind.SimpleMeal, RHAH_IntelSiteKind.Treasure, RHAH_ChoiceKind.Intel);
                case "I-021": return new RHAH_RequestSpec(RHAH_RequestKind.HerbalMedicine, RHAH_IntelSiteKind.Treasure, RHAH_ChoiceKind.Intel);
                case "I-022": return new RHAH_RequestSpec(RHAH_RequestKind.Silver, RHAH_IntelSiteKind.Treasure, RHAH_ChoiceKind.Intel);
                case "I-023": return new RHAH_RequestSpec(RHAH_RequestKind.SimpleMeal, RHAH_IntelSiteKind.Structure, RHAH_ChoiceKind.Intel);
                case "I-024": return new RHAH_RequestSpec(RHAH_RequestKind.HerbalMedicine, RHAH_IntelSiteKind.Structure, RHAH_ChoiceKind.Intel);
                case "I-025": return new RHAH_RequestSpec(RHAH_RequestKind.Silver, RHAH_IntelSiteKind.Structure, RHAH_ChoiceKind.Intel);
                case "I-026": return new RHAH_RequestSpec(RHAH_RequestKind.SimpleMeal, RHAH_IntelSiteKind.Settlement, RHAH_ChoiceKind.Intel);
                case "I-027": return new RHAH_RequestSpec(RHAH_RequestKind.HerbalMedicine, RHAH_IntelSiteKind.Settlement, RHAH_ChoiceKind.Intel);
                case "I-028": return new RHAH_RequestSpec(RHAH_RequestKind.Silver, RHAH_IntelSiteKind.Settlement, RHAH_ChoiceKind.Intel);
                case "I-040": return new RHAH_RequestSpec(RHAH_RequestKind.None, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Refugees);
                case "I-002":
                case "I-003":
                case "I-037": return new RHAH_RequestSpec(RHAH_RequestKind.None, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Abandoned);
                case "I-011": return new RHAH_RequestSpec(RHAH_RequestKind.None, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Refugees);
                case "I-013": return new RHAH_RequestSpec(RHAH_RequestKind.Baby, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.ChildExchange);
                case "I-032":
                case "I-046": return new RHAH_RequestSpec(RHAH_RequestKind.None, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Airdrop);
                case "I-033":
                case "I-047": return new RHAH_RequestSpec(RHAH_RequestKind.None, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Kinship);
                case "I-052": return new RHAH_RequestSpec(RHAH_RequestKind.None, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Visitors);
                case "I-053": return new RHAH_RequestSpec(RHAH_RequestKind.None, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.Refugees);
                default: return new RHAH_RequestSpec(RHAH_RequestKind.None, RHAH_IntelSiteKind.None, RHAH_ChoiceKind.None);
            }
        }

        internal static bool UsesRequest(string displayId)
        {
            RHAH_RequestSpec spec = SpecFor(displayId);
            return spec.Kind != RHAH_RequestKind.None;
        }

        internal static bool UsesChoice(string displayId)
        {
            return SpecFor(displayId).Choice != RHAH_ChoiceKind.None || OffersVisitorControl(displayId);
        }

        internal static bool OffersVisitorControl(string displayId)
        {
            switch (displayId)
            {
                case "I-004":
                case "I-005":
                case "I-006":
                case "I-007":
                case "I-008":
                case "I-009":
                case "I-010":
                case "I-014":
                case "I-029":
                case "I-030":
                case "I-031":
                case "I-036":
                case "I-039":
                case "I-040":
                case "I-041":
                case "I-042":
                case "I-043":
                case "I-044":
                case "I-045":
                case "I-052":
                case "I-053":
                    return true;
                default:
                    return false;
            }
        }

        internal static int Amount(RHAH_RequestKind kind, float wealth, int roll)
        {
            return Amount(kind, wealth, roll, RHAH_IncidentScale.ReferencePoints);
        }

        internal static int Amount(RHAH_RequestKind kind, float wealth, int roll, float points)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            int amount;
            int min;
            int max;
            switch (kind)
            {
                case RHAH_RequestKind.SimpleMeal:
                    amount = 6 + Round(wealth / 12000f) + ClampRoll(roll, 0, 4);
                    min = settings == null ? MinSimple : settings.requestMinSimple;
                    max = settings == null ? MaxSimple : settings.requestMaxSimple;
                    break;
                case RHAH_RequestKind.FineMeal:
                    amount = 4 + Round(wealth / 18000f) + ClampRoll(roll, 0, 3);
                    min = settings == null ? MinFine : settings.requestMinFine;
                    max = settings == null ? MaxFine : settings.requestMaxFine;
                    break;
                case RHAH_RequestKind.Medicine:
                    amount = 2 + Round(wealth / 25000f) + ClampRoll(roll, 0, 2);
                    min = settings == null ? MinMedicine : settings.requestMinMedicine;
                    max = settings == null ? MaxMedicine : settings.requestMaxMedicine;
                    break;
                case RHAH_RequestKind.Silver:
                    amount = 80 + Round(wealth / 40f) + ClampRoll(roll, 0, 120);
                    min = settings == null ? MinSilver : settings.requestMinSilver;
                    max = settings == null ? MaxSilver : settings.requestMaxSilver;
                    break;
                case RHAH_RequestKind.HerbalMedicine:
                    amount = 3 + Round(wealth / 22000f) + ClampRoll(roll, 0, 3);
                    min = settings == null ? MinHerbal : settings.requestMinHerbal;
                    max = settings == null ? MaxHerbal : settings.requestMaxHerbal;
                    break;
                case RHAH_RequestKind.Baby:
                    return 1;
                default:
                    return 0;
            }

            return RHAH_IncidentScale.ScaleAmount(amount, points, min, max);
        }

        internal static string ThingDefName(RHAH_RequestKind kind)
        {
            switch (kind)
            {
                case RHAH_RequestKind.SimpleMeal: return "MealSimple";
                case RHAH_RequestKind.FineMeal: return "MealFine";
                case RHAH_RequestKind.Medicine: return "MedicineIndustrial";
                case RHAH_RequestKind.HerbalMedicine: return "MedicineHerbal";
                case RHAH_RequestKind.Silver: return "Silver";
                default: return null;
            }
        }

        internal static string SitePartDefName(RHAH_IntelSiteKind kind)
        {
            switch (kind)
            {
                case RHAH_IntelSiteKind.Treasure: return "ItemStash";
                case RHAH_IntelSiteKind.Structure: return "Outpost";
                case RHAH_IntelSiteKind.Settlement: return "BanditCamp";
                default: return null;
            }
        }

        internal static bool CanDeliver(RHAH_RequestKind kind, int stock, int amount, bool hasCaptiveBaby)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (kind == RHAH_RequestKind.Baby)
            {
                return hasCaptiveBaby;
            }

            return stock >= amount;
        }
        internal static int FoodForChildren(int childCount)
        {
            int each = Core.RHAH_Mod.Settings == null
                ? HungerAndHavoc.Trade.RHAH_CaravanStay.FoodPerChild
                : Core.RHAH_Mod.Settings.foodPerChild;
            return childCount <= 0 ? 0 : childCount * each;
        }

        internal static bool CanSubstituteFood(bool foodSubstitutionEnabled, RHAH_ChoiceKind choice, int foodStock, int childCount)
        {
            return HungerAndHavoc.Trade.RHAH_CaravanStay.AllowsFoodForChild(
                foodSubstitutionEnabled, choice, true, true, foodStock, childCount);
        }

        internal static RHAH_ChoiceAction Settle(
            RHAH_ChoiceAction requested,
            bool enabled,
            bool alreadySettled,
            bool canDeliver)
        {
            if (alreadySettled || requested == RHAH_ChoiceAction.None)
            {
                return RHAH_ChoiceAction.None;
            }

            if (requested == RHAH_ChoiceAction.Timeout)
            {
                return requested;
            }

            if (!enabled)
            {
                return RHAH_ChoiceAction.None;
            }

            if (requested == RHAH_ChoiceAction.Deliver && !canDeliver)
            {
                return RHAH_ChoiceAction.None;
            }

            return requested;
        }

        internal static bool RemovesStock(RHAH_ChoiceAction action, RHAH_RequestKind kind)
        {
            return action == RHAH_ChoiceAction.Deliver && kind != RHAH_RequestKind.None && kind != RHAH_RequestKind.Baby;
        }

        internal static bool CreatesSite(RHAH_ChoiceAction action, RHAH_IntelSiteKind site)
        {
            return action == RHAH_ChoiceAction.Deliver && site != RHAH_IntelSiteKind.None;
        }

        internal static bool PromisesAid(RHAH_ChoiceKind choice, RHAH_ChoiceAction action)
        {
            if (action != RHAH_ChoiceAction.Deliver && action != RHAH_ChoiceAction.Join)
            {
                return false;
            }

            return choice == RHAH_ChoiceKind.Aid ||
                choice == RHAH_ChoiceKind.ChildExchange ||
                choice == RHAH_ChoiceKind.Refugees ||
                choice == RHAH_ChoiceKind.Abandoned ||
                choice == RHAH_ChoiceKind.Kinship ||
                choice == RHAH_ChoiceKind.Airdrop;
        }

        internal static bool Joins(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Join;
        }

        internal static bool Hires(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Hire;
        }

        internal static bool Recruits(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Recruit;
        }

        internal static bool Enslaves(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Enslave;
        }

        internal static bool Captures(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Capture;
        }

        internal static bool Attacks(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Attack;
        }

        internal static bool WaitsForFood(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Feed;
        }

        internal static bool Leaves(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Reject || action == RHAH_ChoiceAction.Timeout;
        }

        internal static bool SendsToAlly(RHAH_ChoiceAction action)
        {
            return action == RHAH_ChoiceAction.Ally;
        }

        internal const int FoodPerVisitor = 1;
        internal const int TransactionVersion = 1;
        internal const int MaxTransactionFood = 100;
        internal const float NutritionPerPerson = 2.7f;

        internal static bool IsFoodTransaction(string displayId)
        {
            return displayId == "I-020" || displayId == "I-021" || displayId == "I-022" ||
                displayId == "I-023" || displayId == "I-024" || displayId == "I-025" ||
                displayId == "I-026" || displayId == "I-027" || displayId == "I-028";
        }

        internal static int RequiredFoodUnits(int people, float nutrition)
        {
            if (people <= 0 || nutrition <= 0f || float.IsNaN(nutrition) || float.IsInfinity(nutrition))
            {
                return 0;
            }

            decimal units = Math.Ceiling(people * (decimal)NutritionPerPerson / (decimal)nutrition);
            return units >= MaxTransactionFood ? MaxTransactionFood : (int)units;
        }

        internal static ThingDef LargestFood(IReadOnlyDictionary<ThingDef, int> counts)
        {
            ThingDef chosen = null;
            int largest = 0;
            foreach (KeyValuePair<ThingDef, int> entry in counts)
            {
                if (entry.Value > largest || (entry.Value == largest && chosen != null &&
                    string.CompareOrdinal(entry.Key.defName, chosen.defName) < 0))
                {
                    chosen = entry.Key;
                    largest = entry.Value;
                }
            }
            return chosen;
        }

        internal static int RewardUnits(int quotedCount, float foodUnitValue, float rewardUnitValue, float multiplier)
        {
            return quotedCount <= 0 || foodUnitValue <= 0f || rewardUnitValue <= 0f || multiplier <= 0f ? 0 :
                (int)Math.Floor(quotedCount * (double)foodUnitValue * multiplier / rewardUnitValue);
        }
        internal const int MaxFoodRequest = 12;

        internal static int FoodRequestCount(int visitorCount)
        {
            if (visitorCount < 1)
            {
                return 0;
            }

            int cap = Core.RHAH_Mod.Settings == null ? MaxFoodRequest : Core.RHAH_Mod.Settings.maxFoodRequest;
            int each = Core.RHAH_Mod.Settings == null ? FoodPerVisitor : Core.RHAH_Mod.Settings.foodPerVisitor;
            int count = visitorCount * each;
            return count > cap ? cap : count;
        }

        internal static bool OffersBatchControl(string displayId)
        {
            switch (displayId)
            {
                case "I-004":
                case "I-005":
                case "I-006":
                case "I-007":
                case "I-008":
                case "I-009":
                case "I-010":
                case "I-011":
                case "I-014":
                case "I-029":
                case "I-030":
                case "I-031":
                case "I-036":
                case "I-039":
                case "I-040":
                case "I-041":
                case "I-042":
                case "I-043":
                case "I-044":
                case "I-045":
                case "I-052":
                case "I-053":
                    return true;
                default:
                    return false;
            }
        }
        internal static bool ShowsHire(string displayId, bool recruitable)
        {
            return recruitable && OffersBatchControl(displayId);
        }

        internal static bool ShowsRecruit(string displayId, bool recruitable)
        {
            return recruitable && OffersBatchControl(displayId);
        }

        internal static bool ShowsJoin(string displayId, bool present)
        {
            return present && (OffersBatchControl(displayId) || displayId == "I-032" || displayId == "I-046");
        }

        internal static bool ShowsEnslave(bool ideologyActive, bool recruitable)
        {
            return ideologyActive && recruitable;
        }

        internal static bool ShowsCapture(bool recruitable)
        {
            return recruitable;
        }

        internal static bool ShowsAttack(bool recruitable)
        {
            return recruitable;
        }

        internal static bool ShowsAlly(bool present, bool hasAlly)
        {
            return present && hasAlly;
        }

        internal static bool IsAllyDestination(bool player, bool hidden, bool defeated, bool temporary, bool humanlike, bool hostile, bool ally, bool hasSettlement, bool attitudeFaction)
        {
            return !player && !hidden && !defeated && !temporary && humanlike && !hostile && ally && hasSettlement && !attitudeFaction;
        }

        internal static bool ShowsFoodGive(string displayId, RHAH_ChoiceKind choice)
        {
            return displayId != "I-053" && ShowsFoodGive(choice);
        }

        internal static bool ShowsFoodGive(RHAH_ChoiceKind choice)
        {
            return choice == RHAH_ChoiceKind.Visitors ||
                choice == RHAH_ChoiceKind.Refugees ||
                choice == RHAH_ChoiceKind.Airdrop;
        }

        internal static bool ShowsFoodHint(bool dismissed, bool firstClick)
        {
            return !dismissed && firstClick;
        }

        internal static bool FoodWaiting(bool open, bool alive, bool spawned, int received, int requested)
        {
            return open && alive && spawned && requested > 0 && received < requested;
        }

        static int Round(float value)
        {
            if (value <= 0f)
            {
                return 0;
            }

            return (int)(value + 0.5f);
        }

        static int ClampRoll(int roll, int min, int max)
        {
            if (roll < min)
            {
                return min;
            }

            return roll > max ? max : roll;
        }

        static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    public sealed class RHAH_ChoiceRecord : IExposable
    {
        internal int Id;
        internal string DisplayId = string.Empty;
        internal int MapId;
        internal int BatchId;
        internal RHAH_RequestKind Kind;
        internal RHAH_IntelSiteKind Site;
        internal RHAH_ChoiceKind Choice;
        internal int Amount;
        internal int ExpireTick = -1;
        internal RHAH_ChoiceAction Settled;
        internal List<int> PawnLoadIds = new List<int>();
        internal int AllyFactionId;
        internal List<int> AllyPawnIds = new List<int>();
        internal float AllyTransferredValue;
        internal int AllyGoodwillGranted;
        internal bool AllyGiftChecked;
        internal int AllyGiftDueTick = -1;
        internal int TransactionVersion;
        internal string QuotedFoodDefName = string.Empty;
        internal int QuotedFoodCount;
        internal List<string> RewardDefNames = new List<string>();
        internal List<int> RewardCounts = new List<int>();
        internal List<int> RewardThingIds = new List<int>();
        internal int OutgoingChildThingId;
        internal int ResponsibleAdultThingId;
        internal int ChildExchangeVersion;
        internal bool ChildFoodAlternative;

        internal bool Open => Settled == RHAH_ChoiceAction.None;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", 0);
            Scribe_Values.Look(ref DisplayId, "displayId");
            Scribe_Values.Look(ref MapId, "mapId", 0);
            Scribe_Values.Look(ref BatchId, "batchId", 0);
            Scribe_Values.Look(ref Kind, "kind", RHAH_RequestKind.None);
            Scribe_Values.Look(ref Site, "site", RHAH_IntelSiteKind.None);
            Scribe_Values.Look(ref Choice, "choice", RHAH_ChoiceKind.None);
            Scribe_Values.Look(ref Amount, "amount", 0);
            Scribe_Values.Look(ref ExpireTick, "expireTick", -1);
            bool legacyPrison = false;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                string savedAction = null;
                Scribe_Values.Look(ref savedAction, "settled", "None");
                legacyPrison = savedAction == "Prison" || savedAction == "12";
            }
            if (legacyPrison)
            {
                Settled = RHAH_ChoiceAction.Capture;
            }
            else
            {
                Scribe_Values.Look(ref Settled, "settled", RHAH_ChoiceAction.None);
            }
            Scribe_Collections.Look(ref PawnLoadIds, "pawnLoadIds", LookMode.Value);
            Scribe_Values.Look(ref AllyFactionId, "allyFactionId", 0);
            Scribe_Collections.Look(ref AllyPawnIds, "allyPawnIds", LookMode.Value);
            Scribe_Values.Look(ref AllyTransferredValue, "allyTransferredValue", 0f);
            Scribe_Values.Look(ref AllyGoodwillGranted, "allyGoodwillGranted", 0);
            Scribe_Values.Look(ref AllyGiftChecked, "allyGiftChecked", false);
            Scribe_Values.Look(ref AllyGiftDueTick, "allyGiftDueTick", -1);
            Scribe_Values.Look(ref TransactionVersion, "transactionVersion", 0);
            Scribe_Values.Look(ref QuotedFoodDefName, "quotedFoodDefName", string.Empty);
            Scribe_Values.Look(ref QuotedFoodCount, "quotedFoodCount", 0);
            Scribe_Collections.Look(ref RewardDefNames, "rewardDefNames", LookMode.Value);
            Scribe_Collections.Look(ref RewardCounts, "rewardCounts", LookMode.Value);
            Scribe_Collections.Look(ref RewardThingIds, "rewardThingIds", LookMode.Value);
            Scribe_Values.Look(ref OutgoingChildThingId, "outgoingChildThingId", 0);
            Scribe_Values.Look(ref ResponsibleAdultThingId, "responsibleAdultThingId", 0);
            Scribe_Values.Look(ref ChildExchangeVersion, "childExchangeVersion", 0);
            Scribe_Values.Look(ref ChildFoodAlternative, "childFoodAlternative", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                DisplayId = DisplayId ?? string.Empty;
                PawnLoadIds = PawnLoadIds ?? new List<int>();
                AllyPawnIds = AllyPawnIds ?? new List<int>();
                QuotedFoodDefName = QuotedFoodDefName ?? string.Empty;
                RewardDefNames = RewardDefNames ?? new List<string>();
                RewardCounts = RewardCounts ?? new List<int>();
                RewardThingIds = RewardThingIds ?? new List<int>();
            }
        }
    }
}
