using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ArmedFoodPaymentTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.armed.food.payment.tests");
        readonly ThingDef previousMeal = ThingDefOf.MealSimple;
        readonly Map map;
        readonly List<Verse.Pawn> colonists = new List<Verse.Pawn>();
        readonly HashSet<int> unreachable = new HashSet<int>();
        readonly Dictionary<Thing, IHaulDestination> storage = new Dictionary<Thing, IHaulDestination>();
        static RHAH_ArmedFoodPaymentTests active;

        static RHAH_ArmedFoodPaymentTests()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "rhah-food-" + Guid.NewGuid());
            object previousSave = Field(typeof(GenFilePaths), "saveDataPath").GetValue(null);
            object previousMods = Field(typeof(GenFilePaths), "modsFolderPath").GetValue(null);
            object previousOfficial = Field(typeof(GenFilePaths), "officialModsFolderPath").GetValue(null);
            object previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
            bool previousProfiler = DeepProfiler.enabled;
            try
            {
                Directory.CreateDirectory(Path.Combine(temporary, "Config"));
                Directory.CreateDirectory(Path.Combine(temporary, "Mods", "Core", "About"));
                File.WriteAllText(Path.Combine(temporary, "Mods", "Core", "About", "About.xml"),
                    "<ModMetaData><name>Core</name><packageId>ludeon.rimworld</packageId><author>Ludeon</author><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>");
                File.WriteAllText(Path.Combine(temporary, "Config", "ModsConfig.xml"),
                    "<ModsConfigData><activeMods /><knownExpansions /></ModsConfigData>");
                Field(typeof(GenFilePaths), "saveDataPath").SetValue(null, temporary);
                Field(typeof(GenFilePaths), "modsFolderPath").SetValue(null, Path.Combine(temporary, "Mods"));
                Field(typeof(GenFilePaths), "officialModsFolderPath").SetValue(null, Path.Combine(temporary, "Mods"));
                Field(typeof(Prefs), "data").SetValue(null, new PrefsData());
                DeepProfiler.enabled = false;
                RuntimeHelpers.RunClassConstructor(typeof(ModsConfig).TypeHandle);
            }
            finally
            {
                Field(typeof(GenFilePaths), "saveDataPath").SetValue(null, previousSave);
                Field(typeof(GenFilePaths), "modsFolderPath").SetValue(null, previousMods);
                Field(typeof(GenFilePaths), "officialModsFolderPath").SetValue(null, previousOfficial);
                Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
                DeepProfiler.enabled = previousProfiler;
                Directory.Delete(temporary, true);
            }
        }

        public RHAH_ArmedFoodPaymentTests()
        {
            active = this;
            map = (Map)Field(typeof(RHAH_AttackChoiceTests), "map").GetValue(fixture);
            map.mapPawns = Uninitialized<MapPawns>();
            Field(typeof(MapPawns), "pawnsSpawned").SetValue(map.mapPawns, new List<Verse.Pawn>());
            map.listerThings = new ListerThings(ListerThingsUse.Global, null);
            map.designationManager = Uninitialized<DesignationManager>();
            Field(typeof(DesignationManager), "thingDesignations").SetValue(map.designationManager, new Dictionary<Thing, List<Designation>>());
            ThingDef meal = Uninitialized<ThingDef>();
            meal.defName = "MealSimple";
            meal.category = ThingCategory.Item;
            meal.stackLimit = 10;
            meal.ingestible = new IngestibleProperties
            {
                foodType = FoodTypeFlags.Meal,
                preferability = FoodPreferability.MealSimple
            };
            Field(typeof(IngestibleProperties), "cachedNutrition").SetValue(meal.ingestible, 0.9f);
            meal.modContentPack = Uninitialized<ModContentPack>();
            Field(typeof(ModContentPack), "packageIdInt").SetValue(meal.modContentPack, "ludeon.rimworld");
            ThingDefOf.MealSimple = meal;
            colonists.Add(Visitor(990));
            Patch(AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.FreeColonistsSpawned)), nameof(Colonists));
            Patch(AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.CurrentHaulDestinationOf)), nameof(Storage));
            Patch(AccessTools.Method(typeof(ReachabilityUtility), nameof(ReachabilityUtility.CanReach), new[]
            {
                typeof(Verse.Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger),
                typeof(bool), typeof(bool), typeof(TraverseMode)
            }), nameof(Reach));
            Patch(AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue), new[]
            {
                typeof(Thing), typeof(StatDef), typeof(bool), typeof(int)
            }), nameof(Nutrition));
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            ThingDefOf.MealSimple = previousMeal;
            active = null;
            fixture.Dispose();
        }

        [Theory]
        [InlineData("unreachable")]
        [InlineData("inventoryRefuses")]
        public void LaterEligibleReceiverCanAcceptMealPayment(string firstProblem)
        {
            Verse.Pawn first = Visitor(901);
            Verse.Pawn second = Visitor(902);
            RHAH_ChoiceRecord record = Request(first, second);
            if (firstProblem == "unreachable") unreachable.Add(first.thingIDNumber);
            else first.inventory.innerContainer = new ThingOwner<Thing>(first.inventory, true);
            if (firstProblem == "inventoryRefuses")
                first.inventory.innerContainer.InnerListForReading.Add(new Thing { def = ThingDefOf.MealSimple, stackCount = 10 });
            PaymentFood food = Food(10);

            Assert.True(RHAH_ArmedDemand.CanPay(record));
            Assert.True(RHAH_ArmedDemand.TryPay(record));
            Assert.Same(second.inventory.innerContainer, food.holdingOwner);
            Assert.False(food.Spawned);
            Assert.Equal(10, second.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.MealSimple));
            Assert.DoesNotContain(food, map.listerThings.ThingsOfDef(ThingDefOf.MealSimple));
        }

        [Theory]
        [InlineData("insufficient")]
        [InlineData("unreachableFood")]
        [InlineData("unreachableVisitors")]
        [InlineData("forbidden")]
        [InlineData("invalidStorage")]
        [InlineData("nonPlayerStorage")]
        [InlineData("cannotEat")]
        public void InvalidMealPaymentRemainsRejectedWithoutConsumingStock(string reason)
        {
            Verse.Pawn first = Visitor(901);
            Verse.Pawn second = Visitor(902);
            RHAH_ChoiceRecord record = Request(first, second);
            PaymentFood food = Food(reason == "insufficient" ? 9 : 10);
            if (reason == "unreachableFood") unreachable.Add(food.thingIDNumber);
            if (reason == "unreachableVisitors")
            {
                unreachable.Add(first.thingIDNumber);
                unreachable.Add(second.thingIDNumber);
            }
            if (reason == "forbidden")
                Field(typeof(CompForbiddable), "forbiddenInt").SetValue(food.compForbiddable, true);
            if (reason == "invalidStorage") ((PaymentStorage)storage[food]).Allowed = false;
            if (reason == "nonPlayerStorage")
                ((PaymentStorage)storage[food]).SetFactionDirect(first.Faction);
            if (reason == "cannotEat") second.RaceProps.foodType = FoodTypeFlags.Plant;
            int original = food.stackCount;

            Assert.False(RHAH_ArmedDemand.CanPay(record));
            Assert.False(RHAH_ArmedDemand.TryPay(record));
            Assert.Equal(original, food.stackCount);
            Assert.True(food.Spawned);
            Assert.Null(food.holdingOwner);
            Assert.Equal(0, first.inventory.innerContainer.TotalStackCount);
            Assert.Equal(0, second.inventory.innerContainer.TotalStackCount);
            Assert.True(record.Open);
        }

        [Fact]
        public void WholeMealStacksTransferExactlyOnceToOrdinaryInventory()
        {
            Verse.Pawn receiver = Visitor(901);
            RHAH_ChoiceRecord record = Request(receiver);
            PaymentFood first = Food(4);
            PaymentFood second = Food(6);
            Assert.True(RHAH_ArmedDemand.CanPay(record));

            Assert.True(RHAH_ArmedDemand.TryPay(record));

            Assert.Equal(10, receiver.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.MealSimple));
            Assert.Same(receiver.inventory.innerContainer, first.holdingOwner);
            Assert.Same(receiver.inventory.innerContainer, second.holdingOwner);
            Assert.False(first.Spawned);
            Assert.False(second.Spawned);
            Assert.Empty(map.listerThings.ThingsOfDef(ThingDefOf.MealSimple));
        }

        [Fact]
        public void MixedPaymentUsesNutritionAndRoundsOnlyFinalStack()
        {
            Verse.Pawn receiver = Visitor(901);
            RHAH_ChoiceRecord record = Request(receiver);
            Food(4);
            ThingDef biscuit = Meal("HardBiscuit", 0.2f, "Solaris.RatkinRaceMod");
            PaymentFood biscuits = Food(30, biscuit);

            Assert.True(RHAH_ArmedDemand.TryPay(record));

            Assert.Equal(4, receiver.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.MealSimple));
            Assert.Equal(27, receiver.inventory.innerContainer.TotalStackCountOfDef(biscuit));
            Assert.Equal(3, biscuits.stackCount);
            Assert.True(biscuits.Spawned);
        }

        [Fact]
        public void OtherModStockRequiresOptInAndRespectsOptOut()
        {
            Verse.Pawn receiver = Visitor(901);
            RHAH_ChoiceRecord record = Request(receiver);
            ThingDef modFood = Meal("ModRation", 1.8f, "example.foods");
            Food(5, modFood);
            Assert.False(RHAH_ArmedDemand.CanPay(record));
            RHAH_Mod.Settings.SetDemandFoodEnabled(modFood.defName, true);
            Assert.True(RHAH_ArmedDemand.CanPay(record));
            RHAH_Mod.Settings.SetDemandFoodEnabled(modFood.defName, false);
            Assert.False(RHAH_ArmedDemand.TryPay(record));
            Assert.Equal(0, receiver.inventory.innerContainer.TotalStackCount);
        }

        [Fact]
        public void PartialMixedDeliveryAndLegacyCreditShareOneBatchLedger()
        {
            Verse.Pawn first = Visitor(901);
            Verse.Pawn second = Visitor(902);
            RHAH_ChoiceRecord record = Request(first, second);
            record.ArmedFoodDelivered = 4;
            Thing food = new Thing { def = Meal("Biscuit", 0.2f, "Solaris.RatkinRaceMod") };
            RHAH_ArmedDemand.NoteFood(first, food, 10);
            RHAH_ArmedDemand.NoteFood(second, food, 10);
            Assert.Equal(1.4, RHAH_DemandFood.Remaining(record), 5);
            Assert.Equal(7, RHAH_DemandFood.Units(RHAH_DemandFood.Remaining(record), 0.2f));
            Assert.True(record.Open);
            RHAH_ArmedDemand.NoteFood(Visitor(903), food, 10);
            Assert.Equal(1.4, RHAH_DemandFood.Remaining(record), 5);
            RHAH_Mod.Settings.SetDemandFoodEnabled(food.def.defName, false);
            RHAH_ArmedDemand.NoteFood(first, food, 10);
            Assert.Equal(1.4, RHAH_DemandFood.Remaining(record), 5);
            Assert.Equal(4, record.ArmedFoodDelivered);
        }

        [Fact]
        public void HaulCountConvertsFoodUnitsAndSubtractsOtherBatchCarriers()
        {
            Verse.Pawn first = Visitor(901);
            Verse.Pawn second = Visitor(902);
            RHAH_ChoiceRecord record = Request(first, second);
            record.ArmedFoodDelivered = 4;
            ThingDef biscuit = Meal("Biscuit", 0.2f, "Solaris.RatkinRaceMod");
            Verse.Pawn giver = colonists[0];
            GiveJob(giver, first, new Thing { def = biscuit });
            Verse.Pawn otherGiver = Visitor(991);
            GiveJob(otherGiver, second, new Thing { def = biscuit });
            otherGiver.carryTracker.innerContainer.TryAdd(new Thing { def = biscuit, stackCount = 10 }, false);
            harmony.CreateClassProcessor(typeof(RHAH_DemandHaulCountPatch)).Patch();

            Toil determine = (Toil)Method(typeof(JobDriver_GiveToPawn), "DetermineNumToHaul").Invoke(giver.jobs.curDriver, null);
            determine.actor = giver;
            determine.initAction();

            Assert.Equal(17, giver.CurJob.count);
            Assert.Equal(5.4, RHAH_DemandFood.Remaining(record), 5);
        }

        [Fact]
        public void SuccessfulContainerTransferCreditsOnlyActuallyDeliveredUnits()
        {
            Verse.Pawn receiver = Visitor(901);
            RHAH_ChoiceRecord record = Request(receiver);
            ThingDef biscuit = Meal("Biscuit", 0.2f, "Solaris.RatkinRaceMod");
            Verse.Pawn giver = colonists[0];
            Thing carried = new Thing { def = biscuit, stackCount = 3 };
            GiveJob(giver, receiver, carried);
            giver.carryTracker.innerContainer.TryAdd(carried, false);
            harmony.CreateClassProcessor(typeof(RHAH_FoodDeliveryPatch)).Patch();

            int transferred = giver.carryTracker.innerContainer.TryTransferToContainer(carried, receiver.inventory.innerContainer, 3, false);

            Assert.Equal(3, transferred);
            Assert.Equal(3, receiver.inventory.innerContainer.TotalStackCountOfDef(biscuit));
            Assert.Equal(8.4, RHAH_DemandFood.Remaining(record), 5);
            Assert.True(record.Open);
        }

        [Fact]
        public void SiegeInfantFoodDeliveryGoesToInventoryAndCreditsTheDemand()
        {
            Verse.Pawn receiver = Infant(901);
            RHAH_ChoiceRecord record = Request(receiver);
            HungerAndHavoc.Identity.CompRHAH_Pawn.TryGet(receiver).State.lifecycle = RHAH_Lifecycle.Fed;
            Verse.AI.Group.Lord lord = receiver.GetLord();
            LordJob_RHAH_Visitor visitor = (LordJob_RHAH_Visitor)lord.LordJob;
            Field(typeof(Verse.AI.Group.Lord), "curLordToil").SetValue(lord, new LordToil_RHAH_VisitorSeek(visitor));
            PaymentFood food = Food(10);
            food.def.label = "simple meal";
            food.def.ingestible.babiesCanIngest = false;
            Verse.Pawn giver = colonists[0];
            giver.SetFactionDirect(Faction.OfPlayer);
            giver.jobs = new Pawn_JobTracker(giver);
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            giver.carryTracker = new Pawn_CarryTracker(giver);
            JobDef previousGive = JobDefOf.GiveToPawn;
            JobDefOf.GiveToPawn = new JobDef { defName = "GiveToPawn" };
            Patch(AccessTools.Method(typeof(ReservationUtility), nameof(ReservationUtility.CanReserve)), nameof(Reservable));
            Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob)), nameof(QueueDelivery));
            harmony.CreateClassProcessor(typeof(RHAH_FoodDeliveryPatch)).Patch();
            try
            {
                List<Thing> foods = new List<Thing>();
                RHAH_FoodHandoff.CollectFood(giver, foods, receiver, true);
                Assert.Contains(food, foods);

                Method(typeof(RHAH_VisitorExpelMenu), "GiveFood").Invoke(null, new object[] { giver, receiver, food, 1 });

                Assert.Equal(10, food.stackCount);
                Assert.Equal(0.3f, receiver.needs.food.CurLevel);
                Assert.Equal(9d, RHAH_DemandFood.Remaining(record), 5);
                Assert.True(visitor.ListedForFood(receiver));
                Assert.Same(JobDefOf.GiveToPawn, giver.CurJob.def);
                Assert.Equal(10, giver.CurJob.count);
                giver.carryTracker.innerContainer.TryAdd(food, false);
                int transferred = giver.carryTracker.innerContainer.TryTransferToContainer(food, receiver.inventory.innerContainer, 3, false);

                Assert.Equal(3, transferred);
                Assert.Equal(3, receiver.inventory.innerContainer.TotalStackCountOfDef(food.def));
                Assert.Equal(6.3, RHAH_DemandFood.Remaining(record), 5);
                Assert.Equal(0.3f, receiver.needs.food.CurLevel);
                Assert.True(record.Open);
            }
            finally
            {
                JobDefOf.GiveToPawn = previousGive;
            }
        }

        [Theory]
        [InlineData(true, false, FoodPreferability.MealFine, true)]
        [InlineData(false, false, FoodPreferability.MealSimple, false)]
        [InlineData(true, true, FoodPreferability.MealFine, false)]
        [InlineData(true, false, FoodPreferability.DesperateOnly, false)]
        public void OrdinaryInfantFoodRespectsSuitabilityAndMenuRestrictions(
            bool babiesCanIngest, bool disabled, FoodPreferability preferability, bool offered)
        {
            Verse.Pawn receiver = Infant(901);
            PaymentFood food = Food(10);
            food.def.ingestible.babiesCanIngest = babiesCanIngest;
            food.def.ingestible.preferability = preferability;
            RHAH_Mod.Settings.SetGiveFoodEnabled(food.def.defName, !disabled);
            Patch(AccessTools.Method(typeof(ReservationUtility), nameof(ReservationUtility.CanReserve)), nameof(Reservable));
            colonists[0].SetFactionDirect(Faction.OfPlayer);
            map.lordManager = new LordManager(map);
            List<Thing> foods = new List<Thing>();

            RHAH_FoodHandoff.CollectFood(colonists[0], foods, receiver);

            Assert.Equal(offered, foods.Contains(food));
        }

        [Fact]
        public void OrdinaryInfantConsumesFoodWithoutStartingInventoryDelivery()
        {
            Verse.Pawn receiver = Infant(901);
            Verse.Pawn giver = colonists[0];
            giver.jobs = new Pawn_JobTracker(giver);
            PaymentFood food = Food(10);
            food.def.ingestible.babiesCanIngest = true;

            Method(typeof(RHAH_VisitorExpelMenu), "GiveFood").Invoke(null, new object[] { giver, receiver, food, 1 });

            Assert.Equal(9, food.stackCount);
            Assert.Equal(1f, receiver.needs.food.CurLevel);
            Assert.Equal(0, receiver.inventory.innerContainer.TotalStackCountOfDef(food.def));
            Assert.Null(giver.CurJob);
        }

        Verse.Pawn Infant(int id)
        {
            Verse.Pawn receiver = Visitor(id);
            receiver.Name = new NameSingle("Infant");
            receiver.kindDef = new PawnKindDef();
            receiver.def.race.lifeStageAges = new List<LifeStageAge>
            {
                new LifeStageAge { def = new LifeStageDef { developmentalStage = DevelopmentalStage.Baby } }
            };
            receiver.ageTracker = new Pawn_AgeTracker(receiver);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(receiver.ageTracker, 0);
            receiver.needs = new Pawn_NeedsTracker();
            receiver.needs.food = Uninitialized<InfantFoodNeed>();
            Field(typeof(Need), "pawn").SetValue(receiver.needs.food, receiver);
            Field(typeof(Need), "curLevelInt").SetValue(receiver.needs.food, 0.3f);
            HungerAndHavoc.Identity.CompRHAH_Pawn.TryGet(receiver).State.lifecycle = RHAH_Lifecycle.Fed;
            return receiver;
        }

        sealed class InfantFoodNeed : Need_Food
        {
            public InfantFoodNeed(Verse.Pawn pawn) : base(pawn) { }
            public override float MaxLevel => 1f;
        }

        static bool Reservable(ref bool __result)
        {
            __result = true;
            return false;
        }

        static bool QueueDelivery(Pawn_JobTracker __instance, Job job, ref bool __result)
        {
            __instance.curJob = job;
            __result = true;
            return false;
        }


        void GiveJob(Verse.Pawn giver, Verse.Pawn receiver, Thing food)
        {
            giver.SetFactionDirect(Faction.OfPlayer);
            giver.jobs = Uninitialized<Pawn_JobTracker>();
            giver.jobs.curJob = new Job { def = JobDefOf.GiveToPawn, targetA = food, targetB = receiver };
            giver.jobs.curDriver = new JobDriver_GiveToPawn { pawn = giver, job = giver.jobs.curJob };
            giver.carryTracker = new Pawn_CarryTracker(giver);
            ((List<Verse.Pawn>)Field(typeof(MapPawns), "pawnsSpawned").GetValue(map.mapPawns)).Add(giver);
        }

        internal static ThingDef Meal(string name, float nutrition, string packageId)
        {
            ThingDef def = Uninitialized<ThingDef>();
            def.defName = name;
            def.category = ThingCategory.Item;
            def.stackLimit = 75;
            def.ingestible = new IngestibleProperties { foodType = FoodTypeFlags.Meal, preferability = FoodPreferability.MealSimple };
            Field(typeof(IngestibleProperties), "cachedNutrition").SetValue(def.ingestible, nutrition);
            def.modContentPack = Uninitialized<ModContentPack>();
            Field(typeof(ModContentPack), "packageIdInt").SetValue(def.modContentPack, packageId);
            return def;
        }

        RHAH_ChoiceRecord Request(params Verse.Pawn[] visitors)
        {
            RHAH_ChoiceRecord record = (RHAH_ChoiceRecord)Method(typeof(RHAH_AttackChoiceTests), "ArmedRequest")
                .Invoke(fixture, new object[] { visitors[0] });
            for (int i = 1; i < visitors.Length; i++)
            {
                HungerAndHavoc.Identity.CompRHAH_Pawn.TryGet(visitors[i]).ApplySeed(
                    new RHAH_PawnSeed("I-014", 7, 0, RHAH_PawnRole.Refugee, RHAH_Lifecycle.SeekingFood));
                record.PawnLoadIds.Add(visitors[i].thingIDNumber);
                visitors[i].lord = visitors[0].lord;
                visitors[0].lord.ownedPawns.Add(visitors[i]);
            }
            return record;
        }

        Verse.Pawn Visitor(int id)
        {
            Verse.Pawn pawn = (Verse.Pawn)Method(typeof(RHAH_AttackChoiceTests), "Visitor").Invoke(fixture, new object[] { id });
            pawn.def.race.foodType = FoodTypeFlags.Meal;
            pawn.inventory = new Pawn_InventoryTracker(pawn);
            return pawn;
        }

        PaymentFood Food(int count, ThingDef def = null)
        {
            PaymentFood food = new PaymentFood { def = def ?? ThingDefOf.MealSimple, stackCount = count, thingIDNumber = 1000 + storage.Count };
            food.compForbiddable = new CompForbiddable { parent = food };
            Field(typeof(Thing), "mapIndexOrState").SetValue(food, (sbyte)0);
            PaymentStorage destination = new PaymentStorage { def = Uninitialized<ThingDef>() };
            destination.def.category = ThingCategory.Building;
            destination.SetFactionDirect(Faction.OfPlayer);
            storage.Add(food, destination);
            Dictionary<ThingDef, List<Thing>> lists = (Dictionary<ThingDef, List<Thing>>)Field(typeof(ListerThings), "listsByDef").GetValue(map.listerThings);
            if (!lists.TryGetValue(food.def, out List<Thing> items)) lists[food.def] = items = new List<Thing>();
            items.Add(food);
            List<Thing>[] groups = (List<Thing>[])Field(typeof(ListerThings), "listsByGroup").GetValue(map.listerThings);
            int group = (int)ThingRequestGroup.FoodSourceNotPlantOrTree;
            if (groups[group] == null) groups[group] = new List<Thing>();
            groups[group].Add(food);
            return food;
        }

        void Patch(MethodInfo target, string prefix)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(Method(typeof(RHAH_ArmedFoodPaymentTests), prefix)));
        }

        static bool Nutrition(Thing __0, StatDef __1, ref float __result)
        {
            if (__1 != StatDefOf.Nutrition || __0?.def?.ingestible == null) return true;
            __result = __0.def.ingestible.CachedNutrition;
            return false;
        }

        static bool Colonists(ref List<Verse.Pawn> __result)
        {
            __result = active.colonists;
            return false;
        }

        static bool Storage(Thing __0, ref IHaulDestination __result)
        {
            active.storage.TryGetValue(__0, out __result);
            return false;
        }

        static bool Reach(Verse.Pawn __0, LocalTargetInfo __1, ref bool __result)
        {
            __result = __1.HasThing && !active.unreachable.Contains(__1.Thing.thingIDNumber);
            return false;
        }

        sealed class PaymentFood : ThingWithComps
        {
            public override bool IngestibleNow => true;

            public override Thing SplitOff(int count)
            {
                if (count >= stackCount) return base.SplitOff(count);
                stackCount -= count;
                return new PaymentFood { def = def, stackCount = count, thingIDNumber = thingIDNumber + 10000 };
            }

            public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
            {
                Map.listerThings.ThingsOfDef(def).Remove(this);
                Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Remove(this);
                ForceSetStateToUnspawned();
            }
        }

        sealed class PaymentStorage : Thing, IHaulDestination
        {
            internal bool Allowed = true;
            public bool StorageTabVisible => false;
            public bool HaulDestinationEnabled => true;
            public bool Accepts(Thing thing) => Allowed;
            public StorageSettings GetStoreSettings() => null;
            public StorageSettings GetParentStoreSettings() => null;
            public void Notify_SettingsChanged() { }
        }

        static T Uninitialized<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
        static FieldInfo Field(Type type, string name) => AccessTools.Field(type, name) ?? throw new InvalidOperationException(type.Name + "." + name);
        static MethodInfo Method(Type type, string name) => AccessTools.Method(type, name) ?? throw new InvalidOperationException(type.Name + "." + name);
    }
}
