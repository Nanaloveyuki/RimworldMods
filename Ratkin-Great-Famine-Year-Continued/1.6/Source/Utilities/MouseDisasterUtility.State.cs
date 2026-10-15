using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {
        private const string RatkinRaceDefName = "Ratkin";
        private const string RatkinXenotypeDefName = "Ratkin";
        private static readonly string[] PreferredRatkinXenotypeDefNames =
        {
            "RK_XenoType_Ratkin",
            "Ratkin_HouseMouse",
            "Ratkin_Mole",
            "Ratkin_LabRat",
            "Ratkin_Hamster",
            "Ratkin_Squirrel",
            "Ratkin_Vole",
            "Ratkin_VolePrototype",
            "Ratkin_Waster",
            "OAGene_RatkinBase",
            "OAGene_PrimordialRatkin",
            "OAGene_WhiteRatkin",
            "OAGene_TravelRatkin",
            "OAGene_RockRatkin",
            "OAGene_SnowRatkin",
            "OAGene_LowlandRatkin",
            "OAGene_BiochemicalRatkinI",
            "OAGene_BiochemicalRatkinII",
            "OAGene_BiochemicalRatkinIII",
            "OAGene_BiochemicalRatkinIV",
            "Ratkin"
        };
        private const string ClothStuffDefName = "Cloth";
        private const string HumanleatherStuffDefName = "Humanleather";
        private const string RatTailPartDefName = "RK_RatTail";
        private static readonly float RatEggMinAgeYears = 1f / GenDate.DaysPerYear;
        private const float RatEggMaxAgeYears = 2.9f;
        private const float RatkinYoungChildMinAgeYears = 3f;
        private const float RatkinYoungChildMaxAgeYears = 6.9f;
        private const float RatkinAdultMinAgeYears = 16f;
        private const float RatkinAdultMaxAgeYears = 50f;
        private const string EarPartDefName = "Ear";
        private const string FingerPartDefName = "Finger";
        private const string ToePartDefName = "Toe";
        private const float SleepPoopSpawnChance = 0.0004f;
        private const int DefaultChaosPregnancyCheckInterval = 3000;
        private const float DefaultChaosPregnancyChancePerCheck = 0.02f;
        private const float MinimumMouseDisasterPregnancyAgeYears = 1f;
        private const float MouseDisasterFoodMinPercent = 0.30f;
        private const float MouseDisasterFoodMaxPercent = 0.70f;
        private const float MouseDisasterFoodMinAbsolute = 30f;
        private const float MouseDisasterFoodMaxAbsolute = 70f;
        private const int PrisonerScavengeBurstMinCount = 3;
        private const int PrisonerScavengeBurstMaxCount = 5;
        private const int PrisonerScavengeBurstCooldownTicks = GenDate.TicksPerHour;
        private const float NegativeTraitTargetRatio = 0.80f;
        private const float RandomShootingAdultKeepChance = 0.08f;
        private const float RandomShootingChildKeepChance = 0.04f;
        private const float RandomShootingBabyKeepChance = 0.02f;
        private const float DisasterApparelMinDurability = 0.10f;
        private const float DisasterApparelMaxDurability = 0.60f;
        private const int PrisonerScavengeLogIntervalTicks = 600;
        private const int PrisonerScavengeCareDelayTicks = GenDate.TicksPerHour * 3;
        private const int MissingFactionRelationRepairIntervalTicks = 1800;
        private const int TailBiteAttemptCooldownTicks = 2400;
        private const int TailBiteVictimCooldownTicks = 6000;
        private const int TailBiteStartTextCooldownTicks = 900;
        private const int TailBiteResultTextCooldownTicks = 240;
        private const int TailBiteNotifyCooldownTicks = 600;
        private const string VanillaNewbornSpawnCategory = "Newborn";
        private const int SiegeBeggarLeaveFoodUnits = 5;
        private const int HiddenFactionExplicitHostilityDurationTicks = GenDate.TicksPerDay;
        private static string HiddenFactionChineseName => MouseDisasterDefOf.MouseDisaster_HiddenFaction.label;
        private const string BabyExpansionPackageId = "cj.rimtalk.toddlers";
        private const string HospitalityPackageId = "Orion.Hospitality";
        private const string VanillaNutrientPasteExpandedPackageId = "VanillaExpanded.VNutrientE";
        private const string BabyExpansionVisitorMoodThoughtDefName = "RimTalk_VisitorBabyNewEnvironment";
        public const int ChildExchangeModeColonist = 0;
        public const int ChildExchangeModeSlave = 1;
        public const int ChildExchangeModePrisoner = 2;
        private static readonly Dictionary<int, int> BegAttempts = new Dictionary<int, int>();
        private static readonly Dictionary<int, HashSet<int>> BeggedColonists = new Dictionary<int, HashSet<int>>();
        private static readonly HashSet<int> BegSuccess = new HashSet<int>();
        private static readonly HashSet<int> SiegeBeggarPawnIds = new HashSet<int>();
        private static readonly HashSet<int> SiegeBeggarStoleFoodSuccess = new HashSet<int>();
        private static readonly HashSet<int> StrongSiegePawnIds = new HashSet<int>();
        private static readonly Dictionary<int, int> AirDropStayUntilTickByPawnId = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> WallGnawCounts = new Dictionary<int, int>();
        private static readonly HashSet<int> ForcePrisonerOnPurchasePawnIds = new HashSet<int>();
        private static readonly HashSet<int> TradableChattelPawnIds = new HashSet<int>();
        private static readonly HashSet<int> ChildExchangeMoodPawnIds = new HashSet<int>();
        private static Dictionary<int, ChildExchangeState> ActiveChildExchangeByTraderId = new Dictionary<int, ChildExchangeState>();
        private static Dictionary<int, AbandonedDeliveryState> ActiveAbandonedDeliveryByAdultId = new Dictionary<int, AbandonedDeliveryState>();
        private static readonly Dictionary<int, MapPawnClassificationCache> MapPawnCaches = new Dictionary<int, MapPawnClassificationCache>();
        private static readonly Dictionary<int, int> PrisonerScavengeDelayStateByPawnId = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> PrisonerScavengeBurstRemainingByPawnId = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> PrisonerScavengeBurstCooldownByPawnId = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> PrisonerScavengeLastLogTickByPawnId = new Dictionary<int, int>();
        private static readonly Dictionary<int, string> PrisonerScavengeLastLogMessageByPawnId = new Dictionary<int, string>();
        private static readonly Dictionary<int, int> TailBiteLastAttemptTickByPawnId = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> TailBiteLastVictimTickByPawnId = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> TailBiteLastNotifyTickByPawnId = new Dictionary<int, int>();
        private static readonly Dictionary<string, int> FloatingTextLastTickByKey = new Dictionary<string, int>();
        private static readonly Dictionary<ThingDef, List<ThingDef>> ApparelPreferredStuffCache = new Dictionary<ThingDef, List<ThingDef>>();
        private static readonly Dictionary<ThingDef, bool> IsRatkinRaceDefCache = new Dictionary<ThingDef, bool>();
        private static readonly Dictionary<XenotypeDef, bool> IsRatkinXenotypeDefCache = new Dictionary<XenotypeDef, bool>();
        private static readonly string[] RatkinRaceKeywords =
        {
            "ratkin",
            "鼠族"
        };
        private static readonly string[] RatkinXenotypeKeywords =
        {
            "ratkin",
            "鼠族"
        };
        private static readonly string[] RandomShootingTraitKeywords =
        {
            "trigger",
            "shoot",
            "gun",
            "\u67aa",
            "\u5f00\u706b",
            "\u5f00\u67aa",
            "乱射"
        };
        private static readonly string[] RandomMissingPartPool =
        {
            "Ear",
            "Ear",
            "Ear",
            "RK_RatTail",
            "RK_RatTail",
            "Finger",
            "Toe"
        };
        private static readonly string[] BabyRestrictedApparelDefNames =
        {
            "Apparel_BabyOnesie",
            "Apparel_WarmerHat",
            "Apparel_SunHat"
        };
        private static readonly string[] ChildRestrictedApparelDefNames =
        {
            "Apparel_WarmerHat",
            "Apparel_SunHat",
            "Apparel_KidTribal"
        };
        private static readonly string[] SlaveTraderKindDefNames =
        {
            "MouseDisaster_RatkinTrader",
            "Caravan_Neolithic_Slaver",
            "Caravan_Outlander_Slaver",
            "Caravan_Pirate_Slaver"
        };
        private static readonly string[] PrisonerScavengeFloorDustKeywords =
        {
            "dirt",
            "dust",
            "ash",
            "sand",
            "\u7070",
            "\u5c18",
            "\u571f"
        };
        private static readonly string[] PrisonerScavengeVomitKeywords =
        {
            "vomit",
            "puke",
            "bile",
            "呕吐"
        };
        private static readonly string[] PrisonerScavengeAmnioticKeywords =
        {
            "amniotic",
            "amni",
            "羊水",
            "胎水"
        };
        private static readonly string[] PrisonerScavengeBloodKeywords =
        {
            "blood",
            "hemorr",
            "血"
        };
        private static readonly string[] PrisonerScavengeFloorDustTextKeys =
        {
            "MouseDisaster_PrisonerScavenge_Text_FloorDust_1",
            "MouseDisaster_PrisonerScavenge_Text_FloorDust_2",
            "MouseDisaster_PrisonerScavenge_Text_FloorDust_3"
        };
        private static readonly string[] PrisonerScavengeDirtyTextKeys =
        {
            "MouseDisaster_PrisonerScavenge_Text_Dirty_1",
            "MouseDisaster_PrisonerScavenge_Text_Dirty_2",
            "MouseDisaster_PrisonerScavenge_Text_Dirty_3"
        };
        private static readonly string[] PrisonerScavengeVomitTextKeys =
        {
            "MouseDisaster_PrisonerScavenge_Text_Vomit_1",
            "MouseDisaster_PrisonerScavenge_Text_Vomit_2",
            "MouseDisaster_PrisonerScavenge_Text_Vomit_3"
        };
        private static readonly string[] PrisonerScavengeAmnioticTextKeys =
        {
            "MouseDisaster_PrisonerScavenge_Text_Amniotic_1",
            "MouseDisaster_PrisonerScavenge_Text_Amniotic_2",
            "MouseDisaster_PrisonerScavenge_Text_Amniotic_3"
        };
        private static readonly string[] PrisonerScavengeBloodTextKeys =
        {
            "MouseDisaster_PrisonerScavenge_Text_Blood_1",
            "MouseDisaster_PrisonerScavenge_Text_Blood_2",
            "MouseDisaster_PrisonerScavenge_Text_Blood_3"
        };
        private static readonly string[] PrisonerScavengePoisonedTextKeys =
        {
            "MouseDisaster_PrisonerScavenge_Text_Poisoned_1",
            "MouseDisaster_PrisonerScavenge_Text_Poisoned_2"
        };
        private static readonly string[] ScavengeInterruptBlockedJobKeywords =
        {
            "Carried",
            "Carry"
        };
        private static readonly string[] ExtremeCompatModKeywords =
        {
            "RimTalk",
            "BabyAngel",
            "TrainingFacility"
        };
        private const string BabyOnesieApparelDefName = "Apparel_BabyOnesie";
        private const string KidTribalApparelDefName = "Apparel_KidTribal";
        private const string SunHatApparelDefName = "Apparel_SunHat";
        private const string WarmerHatApparelDefName = "Apparel_WarmerHat";
        private static ThingDef ratEggTailThingDef;
        private static ThingDef ratkinRaceDef;
        private static XenotypeDef ratkinXenotypeDef;
        private static ThingDef clothStuffDef;
        private static ThingDef humanleatherStuffDef;
        private static PawnKindDef playerAffiliatedRatkinKindDef;
        private static bool playerAffiliatedRatkinKindResolved;
        private static HediffDef toddlersLearningToWalkDef;
        private static HediffDef toddlersLearningManipulationDef;
        private static HediffDef toddlersLonelyDef;
        private static HediffDef rimTalkBabyBabblingDef;
        private static HediffDef rimTalkToddlerLanguageLearningDef;
        private static bool toddlerCompatDefsResolved;
        private static bool syncingToddlerCompat;
        private static List<TraitDef> ratEggTraitPool;
        private static List<TraitDef> negativeTraitPool;
        private static List<GeneDef> cachedMouseDisasterGenePool;
        private static bool cachedMouseDisasterGenePoolResolved;
        private static bool ratEggTraitPoolResolved;
        private static bool negativeTraitPoolResolved;
        private static bool ratkinRaceResolved;
        private static bool ratkinXenotypeResolved;
        private static bool apparelStuffResolved;
        private static bool slaveTraderKindResolved;
        private static Faction mouseDisasterHiddenFaction;
        private static TraderKindDef slaveTraderKind;
        private static bool ratEggTailResolved;
        private static AnimationDef toddlerCrawlAnimationDef;
        private static bool toddlerCrawlAnimationResolved;
        private static List<ThingDef> adultApparelDefsCache;
        private static bool adultApparelDefsResolved;
        private static List<BackstoryDef> vanillaNewbornBackstoryPool;
        private static bool vanillaNewbornBackstoryPoolResolved;
        private static int lastFactionRelationRepairTick = int.MinValue;
        private static int lastFactionRelationRepairFactionCount = -1;
        private static int hiddenFactionExplicitHostilityUntilTick = -1;
        private static bool cachedHostilePawnExists;
        private static int cachedHostilePawnCheckTick = -1;
        private static int lastHiddenFactionRelationSyncFactionCount = -1;
        private static bool suppressReliefAreaPostfix;
        private static readonly Dictionary<int, Pawn> ReusablePawnLookup = new Dictionary<int, Pawn>();
        private static readonly List<int> ReusableIntList = new List<int>();
        private static readonly List<Pawn> ReusablePawnList = new List<Pawn>();
        private static bool? extremeCompatJobModeCached;
        private static bool? babyExpansionEnabledCached;
        private static bool? leadYourPetEnabledCached;
        private static bool? hospitalityEnabledCached;
        private static bool? vanillaNutrientPasteExpandedEnabledCached;
        private static ThoughtDef babyExpansionVisitorMoodThoughtDef;
        private static bool babyExpansionVisitorMoodThoughtResolved;
        private static Type leadYourPetComponentType;
        private static MethodInfo leadYourPetTryStartRatkinMotherLeashMethod;
        private static MethodInfo leadYourPetStartLeashMethod;
        private static MethodInfo leadYourPetTryAssignTravelMouseEggsMethod;
        private static MethodInfo leadYourPetGetLinksForMasterMethod;
        private static MethodInfo leadYourPetAnchorLeashedPetsToCellMethod;
        private static MethodInfo leadYourPetEndLeashForPetMethod;
        private static MethodInfo leadYourPetClearMouseEggPetStateMethod;
        private static MethodInfo leadYourPetClearTravelStockMethod;
        private static MethodInfo gameGetComponentMethod;
        private sealed class ChildExchangeState : IExposable
        {
            public Pawn trader;
            public List<Pawn> escortPawns = new List<Pawn>();
            public List<Pawn> childPawns = new List<Pawn>();
            public int mapId;
            public int expireTick;
            public List<int> escortPawnIds;
            public List<int> childPawnIds;

            public void ExposeData()
            {
                Scribe_References.Look(ref trader, "trader");
                Scribe_Collections.Look(ref escortPawns, "escortPawns", LookMode.Reference);
                Scribe_Collections.Look(ref childPawns, "childPawns", LookMode.Reference);
                Scribe_Values.Look(ref mapId, "mapId", -1);
                Scribe_Values.Look(ref expireTick, "expireTick", 0);
                Scribe_Collections.Look(ref escortPawnIds, "escortPawnIds", LookMode.Value);
                Scribe_Collections.Look(ref childPawnIds, "childPawnIds", LookMode.Value);

                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    escortPawns ??= new List<Pawn>();
                    childPawns ??= new List<Pawn>();
                    escortPawnIds ??= new List<int>();
                    childPawnIds ??= new List<int>();
                    escortPawns.RemoveAll(pawn => pawn == null);
                    childPawns.RemoveAll(pawn => pawn == null);
                    escortPawnIds = escortPawnIds
                        .Concat(escortPawns.Select(pawn => pawn.thingIDNumber))
                        .Distinct()
                        .ToList();
                    childPawnIds = childPawnIds
                        .Concat(childPawns.Select(pawn => pawn.thingIDNumber))
                        .Distinct()
                        .ToList();
                }
            }
        }

        private sealed class AbandonedDeliveryState : IExposable
        {
            public Pawn adult;
            public List<Pawn> childPawns = new List<Pawn>();
            public int mapId;
            public IntVec3 foodCell;
            public List<int> childPawnIds;
            public List<int> deliveredChildIds = new List<int>();
            public bool adultHasLeft;
            public int adultArrivedAtDropoffTick = -1;
            public int startedTick = -1;
            public bool loggedWaitStall;

            public void ExposeData()
            {
                Scribe_References.Look(ref adult, "adult");
                Scribe_Collections.Look(ref childPawns, "childPawns", LookMode.Reference);
                Scribe_Values.Look(ref mapId, "mapId", -1);
                Scribe_Values.Look(ref foodCell, "foodCell", IntVec3.Invalid);
                Scribe_Collections.Look(ref childPawnIds, "childPawnIds", LookMode.Value);
                Scribe_Collections.Look(ref deliveredChildIds, "deliveredChildIds", LookMode.Value);
                Scribe_Values.Look(ref adultHasLeft, "adultHasLeft", false);
                Scribe_Values.Look(ref adultArrivedAtDropoffTick, "adultArrivedAtDropoffTick", -1);
                Scribe_Values.Look(ref startedTick, "startedTick", -1);

                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    childPawns ??= new List<Pawn>();
                    deliveredChildIds ??= new List<int>();
                    childPawnIds ??= new List<int>();
                    childPawns.RemoveAll(pawn => pawn == null);
                    childPawnIds = childPawnIds
                        .Concat(childPawns.Select(pawn => pawn.thingIDNumber))
                        .Distinct()
                        .ToList();
                }
            }
        }

        internal static void ExposePendingStateData()
        {
            Scribe_Collections.Look(ref ActiveChildExchangeByTraderId, "mouseDisaster_activeChildExchange", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref ActiveAbandonedDeliveryByAdultId, "mouseDisaster_activeAbandonedDelivery", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ActiveChildExchangeByTraderId ??= new Dictionary<int, ChildExchangeState>();
                ActiveAbandonedDeliveryByAdultId ??= new Dictionary<int, AbandonedDeliveryState>();

                List<int> invalidChildExchangeIds = ActiveChildExchangeByTraderId
                    .Where(pair => pair.Value == null || pair.Value.childPawnIds.NullOrEmpty() || pair.Value.mapId < 0)
                    .Select(pair => pair.Key)
                    .ToList();
                for (int i = 0; i < invalidChildExchangeIds.Count; i++)
                {
                    ActiveChildExchangeByTraderId.Remove(invalidChildExchangeIds[i]);
                }

                List<int> invalidAbandonedDeliveryIds = ActiveAbandonedDeliveryByAdultId
                    .Where(pair => pair.Value == null || pair.Value.childPawnIds.NullOrEmpty())
                    .Select(pair => pair.Key)
                    .ToList();
                for (int i = 0; i < invalidAbandonedDeliveryIds.Count; i++)
                {
                    ActiveAbandonedDeliveryByAdultId.Remove(invalidAbandonedDeliveryIds[i]);
                }
            }
        }

        internal static void ResetLoadedGameTransientState()
        {
            // These values are runtime-only. They are keyed by map/Pawn IDs and must not
            // survive loading a different save in the same process.
            MouseDisasterPrisonTransferUtility.ResetTransientState();
            BegAttempts.Clear();
            BeggedColonists.Clear();
            BegSuccess.Clear();
            MapPawnCaches.Clear();
            PrisonerScavengeDelayStateByPawnId.Clear();
            PrisonerScavengeBurstRemainingByPawnId.Clear();
            PrisonerScavengeBurstCooldownByPawnId.Clear();
            PrisonerScavengeLastLogTickByPawnId.Clear();
            PrisonerScavengeLastLogMessageByPawnId.Clear();
            TailBiteLastAttemptTickByPawnId.Clear();
            TailBiteLastVictimTickByPawnId.Clear();
            TailBiteLastNotifyTickByPawnId.Clear();
            FloatingTextLastTickByKey.Clear();
            ReusablePawnLookup.Clear();
            ReusableIntList.Clear();
            ReusablePawnList.Clear();

            lastFactionRelationRepairTick = int.MinValue;
            lastFactionRelationRepairFactionCount = -1;
            hiddenFactionExplicitHostilityUntilTick = -1;
            cachedHostilePawnExists = false;
            cachedHostilePawnCheckTick = -1;
            lastHiddenFactionRelationSyncFactionCount = -1;
            suppressReliefAreaPostfix = false;
        }

        internal static void ResetPendingState()
        {
            ResetLoadedGameTransientState();
            NextBegTickByPawnId.Clear();
            SiegeBeggarPawnIds.Clear();
            SiegeBeggarStoleFoodSuccess.Clear();
            StrongSiegePawnIds.Clear();
            AirDropStayUntilTickByPawnId.Clear();
            WallGnawCounts.Clear();
            ForcePrisonerOnPurchasePawnIds.Clear();
            TradableChattelPawnIds.Clear();
            ChildExchangeMoodPawnIds.Clear();
            ActiveChildExchangeByTraderId.Clear();
            ActiveAbandonedDeliveryByAdultId.Clear();
        }

        internal static List<int> CopyForcePrisonerOnPurchasePawnIds()
        {
            return ForcePrisonerOnPurchasePawnIds.ToList();
        }

        internal static List<int> CopyTradableChattelPawnIds()
        {
            return TradableChattelPawnIds.ToList();
        }

        internal static List<int> CopyChildExchangeMoodPawnIds()
        {
            return ChildExchangeMoodPawnIds.ToList();
        }

        internal static List<int> CopySiegeBeggarPawnIds()
        {
            return SiegeBeggarPawnIds.ToList();
        }

        internal static List<int> CopySiegeBeggarStoleFoodSuccessPawnIds()
        {
            return SiegeBeggarStoleFoodSuccess.ToList();
        }

        internal static List<int> CopyStrongSiegePawnIds()
        {
            return StrongSiegePawnIds.ToList();
        }

        internal static Dictionary<int, int> CopyAirDropStayUntilTickByPawnId()
        {
            return new Dictionary<int, int>(AirDropStayUntilTickByPawnId);
        }

        internal static Dictionary<int, int> CopyWallGnawCounts()
        {
            return new Dictionary<int, int>(WallGnawCounts);
        }

        internal static void RestorePendingMarkerState(
            IEnumerable<int> forcePrisonerOnPurchasePawnIds,
            IEnumerable<int> tradableChattelPawnIds,
            IEnumerable<int> childExchangeMoodPawnIds,
            IEnumerable<int> siegeBeggarPawnIds,
            IEnumerable<int> siegeBeggarStoleFoodSuccessPawnIds,
            IEnumerable<int> strongSiegePawnIds,
            IDictionary<int, int> airDropStayUntilTickByPawnId,
            IDictionary<int, int> wallGnawCounts)
        {
            ForcePrisonerOnPurchasePawnIds.Clear();
            TradableChattelPawnIds.Clear();
            ChildExchangeMoodPawnIds.Clear();
            SiegeBeggarPawnIds.Clear();
            SiegeBeggarStoleFoodSuccess.Clear();
            StrongSiegePawnIds.Clear();
            AirDropStayUntilTickByPawnId.Clear();
            WallGnawCounts.Clear();

            if (forcePrisonerOnPurchasePawnIds != null)
            {
                ForcePrisonerOnPurchasePawnIds.UnionWith(forcePrisonerOnPurchasePawnIds);
            }

            if (tradableChattelPawnIds != null)
            {
                TradableChattelPawnIds.UnionWith(tradableChattelPawnIds);
            }

            if (childExchangeMoodPawnIds != null)
            {
                ChildExchangeMoodPawnIds.UnionWith(childExchangeMoodPawnIds);
            }

            if (siegeBeggarPawnIds != null)
            {
                SiegeBeggarPawnIds.UnionWith(siegeBeggarPawnIds);
            }

            if (siegeBeggarStoleFoodSuccessPawnIds != null)
            {
                SiegeBeggarStoleFoodSuccess.UnionWith(siegeBeggarStoleFoodSuccessPawnIds);
            }

            if (strongSiegePawnIds != null)
            {
                StrongSiegePawnIds.UnionWith(strongSiegePawnIds);
            }

            if (airDropStayUntilTickByPawnId != null)
            {
                foreach (KeyValuePair<int, int> pair in airDropStayUntilTickByPawnId)
                {
                    AirDropStayUntilTickByPawnId[pair.Key] = pair.Value;
                }
            }

            if (wallGnawCounts != null)
            {
                foreach (KeyValuePair<int, int> pair in wallGnawCounts)
                {
                    WallGnawCounts[pair.Key] = pair.Value;
                }
            }
        }

        internal static void CleanupLoadedPendingState()
        {
            if (ActiveChildExchangeByTraderId != null)
            {
                List<int> invalidChildExchangeIds = ActiveChildExchangeByTraderId
                    .Where(pair => pair.Value == null || pair.Value.childPawnIds.NullOrEmpty() || pair.Value.mapId < 0)
                    .Select(pair => pair.Key)
                    .ToList();
                for (int i = 0; i < invalidChildExchangeIds.Count; i++)
                {
                    ActiveChildExchangeByTraderId.Remove(invalidChildExchangeIds[i]);
                }
            }

            if (ActiveAbandonedDeliveryByAdultId != null)
            {
                List<int> invalidAbandonedDeliveryIds = ActiveAbandonedDeliveryByAdultId
                    .Where(pair => pair.Value == null || pair.Value.childPawnIds.NullOrEmpty())
                    .Select(pair => pair.Key)
                    .ToList();
                for (int i = 0; i < invalidAbandonedDeliveryIds.Count; i++)
                {
                    ActiveAbandonedDeliveryByAdultId.Remove(invalidAbandonedDeliveryIds[i]);
                }
            }
        }

        internal static void CleanupExpiredPendingState()
        {
            CleanupLoadedPendingState();

            HashSet<int> activeMapIds = new HashSet<int>(
                Find.Maps?.Where(map => map != null && !map.Disposed).Select(map => map.uniqueID) ??
                Enumerable.Empty<int>());

            if (ActiveChildExchangeByTraderId != null)
            {
                List<int> staleChildExchangeIds = ActiveChildExchangeByTraderId
                    .Where(pair => pair.Value == null || !activeMapIds.Contains(pair.Value.mapId))
                    .Select(pair => pair.Key)
                    .ToList();
                for (int i = 0; i < staleChildExchangeIds.Count; i++)
                {
                    if (ActiveChildExchangeByTraderId.TryGetValue(staleChildExchangeIds[i], out ChildExchangeState state))
                    {
                        state.trader = null;
                        state.escortPawns?.Clear();
                        state.childPawns?.Clear();
                        state.escortPawnIds?.Clear();
                        state.childPawnIds?.Clear();
                    }

                    ActiveChildExchangeByTraderId.Remove(staleChildExchangeIds[i]);
                }
            }

            if (ActiveAbandonedDeliveryByAdultId != null)
            {
                List<int> staleAbandonedDeliveryIds = ActiveAbandonedDeliveryByAdultId
                    .Where(pair => pair.Value == null || !activeMapIds.Contains(pair.Value.mapId))
                    .Select(pair => pair.Key)
                    .ToList();
                for (int i = 0; i < staleAbandonedDeliveryIds.Count; i++)
                {
                    if (ActiveAbandonedDeliveryByAdultId.TryGetValue(staleAbandonedDeliveryIds[i], out AbandonedDeliveryState state))
                    {
                        state.adult = null;
                        state.childPawns?.Clear();
                        state.childPawnIds?.Clear();
                        state.deliveredChildIds?.Clear();
                    }

                    ActiveAbandonedDeliveryByAdultId.Remove(staleAbandonedDeliveryIds[i]);
                }
            }

            List<int> staleCacheIds = MapPawnCaches
                .Where(pair => !activeMapIds.Contains(pair.Key))
                .Select(pair => pair.Key)
                .ToList();
            for (int i = 0; i < staleCacheIds.Count; i++)
            {
                if (MapPawnCaches.TryGetValue(staleCacheIds[i], out MapPawnClassificationCache cache))
                {
                    cache.ratkinPawns.Clear();
                    cache.mouseDisasterPawns.Clear();
                    cache.incidentMoodChildren.Clear();
                    cache.doorStuckCandidates.Clear();
                    cache.spawnedPawnById.Clear();
                }

                MapPawnCaches.Remove(staleCacheIds[i]);
            }
        }

        internal static bool HasActivePendingPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            int pawnId = pawn.thingIDNumber;
            return (ActiveChildExchangeByTraderId?.Values.Any(state => state != null &&
                        (state.trader == pawn || state.trader?.thingIDNumber == pawnId ||
                         state.escortPawnIds?.Contains(pawnId) == true || state.childPawnIds?.Contains(pawnId) == true)) == true) ||
                   (ActiveAbandonedDeliveryByAdultId?.Values.Any(state => state != null &&
                        (state.adult == pawn || state.adult?.thingIDNumber == pawnId ||
                         state.childPawnIds?.Contains(pawnId) == true)) == true);
        }

        internal static void RestoreAbandonedDeliveryDuties()
        {
            foreach (AbandonedDeliveryState state in ActiveAbandonedDeliveryByAdultId.Values)
                foreach (Pawn child in state.childPawns.Where(p => p != null && p.Spawned && !IsPlayerAffiliatedRatkin(p)))
                    MouseDisasterPawnGroupUtility.HoldForDropoff(child, state.foodCell);
        }

        private sealed class MapPawnClassificationCache
        {
            public bool dirty = true;
            public readonly List<Pawn> ratkinPawns = new List<Pawn>();
            public readonly List<Pawn> mouseDisasterPawns = new List<Pawn>();
            public readonly List<Pawn> incidentMoodChildren = new List<Pawn>();
            public readonly List<Pawn> doorStuckCandidates = new List<Pawn>();
            public readonly Dictionary<int, Pawn> spawnedPawnById = new Dictionary<int, Pawn>();
        }

        public sealed class PrisonerScavengeProfile
        {
            public readonly ThoughtDef thoughtDef;
            public readonly string[] textKeys;
            public readonly float nutritionGain;

            public PrisonerScavengeProfile(ThoughtDef thoughtDef, string[] textKeys, float nutritionGain)
            {
                this.thoughtDef = thoughtDef;
                this.textKeys = textKeys;
                this.nutritionGain = nutritionGain;
            }
        }

        private static readonly HediffDef[] IncidentVisitorHediffs =
        {
            MouseDisasterDefOf.MouseDisaster_AbandonedEgg,
            MouseDisasterDefOf.MouseDisaster_AbandoningMother,
            MouseDisasterDefOf.MouseDisaster_ShatteredMother_Dying,
            MouseDisasterDefOf.MouseDisaster_ShatteredMother_Children,
            MouseDisasterDefOf.MouseDisaster_BeggarMother,
            MouseDisasterDefOf.MouseDisaster_BeggarChild,
            MouseDisasterDefOf.MouseDisaster_BeggarGroupAdult,
            MouseDisasterDefOf.MouseDisaster_BeggarGroupChild,
            MouseDisasterDefOf.MouseDisaster_ThiefAdult,
            MouseDisasterDefOf.MouseDisaster_ThiefChild,
            MouseDisasterDefOf.MouseDisaster_ThiefChildOnly,
            MouseDisasterDefOf.MouseDisaster_LargeRefugeeAdult,
            MouseDisasterDefOf.MouseDisaster_LargeRefugeeChild,
            MouseDisasterDefOf.MouseDisaster_FamineRefugee,
            MouseDisasterDefOf.MouseDisaster_WildWanderer,
            MouseDisasterDefOf.MouseDisaster_WildChildWanderer,
            MouseDisasterDefOf.MouseDisaster_WildGroupWanderer,
            MouseDisasterDefOf.MouseDisaster_TraderCaravanTrader,
            MouseDisasterDefOf.MouseDisaster_TraderCaravanEscort,
            MouseDisasterDefOf.MouseDisaster_TraderCaravanChild,
            MouseDisasterDefOf.MouseDisaster_ChildExchangeTrader,
            MouseDisasterDefOf.MouseDisaster_ChildExchangeChild,
            MouseDisasterDefOf.MouseDisaster_SiegeBeggar,
            MouseDisasterDefOf.MouseDisaster_GreatFamineAdult,
            MouseDisasterDefOf.MouseDisaster_GreatFamineChild,
        };

        private static Dictionary<int, Pawn> BuildSpawnedPawnLookup(IReadOnlyList<Pawn> pawns)
        {
            return MouseDisasterPawnLookup.Populate(pawns, ReusablePawnLookup);
        }

        private static Pawn ResolvePawnById(Dictionary<int, Pawn> pawnLookup, int pawnId)
        {
            if (pawnLookup != null && pawnLookup.TryGetValue(pawnId, out Pawn pawn))
            {
                return pawn;
            }

            return null;
        }
    }
}
