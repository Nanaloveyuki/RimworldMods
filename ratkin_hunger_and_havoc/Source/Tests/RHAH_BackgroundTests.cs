using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Trade;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_BackgroundTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.background.tests");
        readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        readonly LoadedLanguage previousDefaultLanguage = LanguageDatabase.defaultLanguage;
        readonly HashSet<string> activeMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly List<RHAH_BackgroundStory> stories = new List<RHAH_BackgroundStory>();
        readonly List<string> takeovers = new List<string>();
        readonly ITrader previousTrader = TradeSession.trader;
        readonly TradeDeal previousDeal = TradeSession.deal;
        readonly bool previousGiftMode = TradeSession.giftMode;
        bool tradeSucceeds;
        EventChainRuntime runtime;
        static RHAH_BackgroundTests active;

        public RHAH_BackgroundTests()
        {
            active = this;
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Current.Game.letterStack = new LetterStack();
            Find.Maps.Clear();
            LoadedLanguage language = (LoadedLanguage)RuntimeHelpers.GetUninitializedObject(typeof(LoadedLanguage));
            AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
            AccessTools.Field(typeof(LoadedLanguage), "workerInt").SetValue(language, new LanguageWorker_English());
            language.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
            language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            LanguageDatabase.activeLanguage = language;
            LanguageDatabase.defaultLanguage = language;
            Patch(AccessTools.PropertyGetter(typeof(OxygenEventChainHost), nameof(OxygenEventChainHost.Runtime)), nameof(ReadRuntime));
            Patch(AccessTools.Method(typeof(ModsConfig), nameof(ModsConfig.IsActive), new[] { typeof(string) }), nameof(IsActive));
            Patch(AccessTools.Method(typeof(RHAH_BackgroundStories), nameof(RHAH_BackgroundStories.SendLetter)), nameof(SendStory));
            Patch(AccessTools.Method(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Add)), nameof(AddWorldObject));
            Patch(AccessTools.Method(typeof(WorldObject), nameof(WorldObject.Destroy)), nameof(DestroyWorldObject));
            foreach (MethodInfo method in typeof(LetterStack).GetMethods())
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == nameof(LetterStack.ReceiveLetter) && parameters.Length >= 3 &&
                    parameters[0].ParameterType == typeof(TaggedString) && parameters[1].ParameterType == typeof(TaggedString))
                    Patch(method, nameof(Receive));
            }
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            TradeSession.trader = previousTrader;
            TradeSession.deal = previousDeal;
            TradeSession.giftMode = previousGiftMode;
            LanguageDatabase.activeLanguage = previousLanguage;
            LanguageDatabase.defaultLanguage = previousDefaultLanguage;
            active = null;
            fixture.Dispose();
        }

        [Fact]
        public void RandomPoolContainsTenAllAgesStoriesAndNeverDrawsTheFollowup()
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings.background;
            Assert.Equal(RHAH_ContentRating.AllAges, RHAH_BackgroundStories.ContentRating);
            Assert.Equal(RHAH_BackgroundStory.Rebellion, RHAH_BackgroundStories.Select(options, 0f));
            RHAH_BackgroundStory[] expected = { RHAH_BackgroundStory.Rebellion, RHAH_BackgroundStory.Ruins,
                RHAH_BackgroundStory.Unrest, RHAH_BackgroundStory.Mutiny, RHAH_BackgroundStory.TwoTaxes,
                RHAH_BackgroundStory.SameCallsign, RHAH_BackgroundStory.HarvestFestival, RHAH_BackgroundStory.Memorial,
                RHAH_BackgroundStory.AidManifest, RHAH_BackgroundStory.CancelledBanquet };
            for (int i = 0; i < expected.Length; i++)
                Assert.Equal(expected[i], RHAH_BackgroundStories.Select(options, (i + 0.1f) / expected.Length));
            options.twoTaxesEnabled = options.sameCallsignEnabled = options.harvestFestivalEnabled =
                options.memorialEnabled = options.aidManifestEnabled = options.cancelledBanquetEnabled = false;
            options.rebellionEnabled = options.ruinsEnabled = options.mutinyEnabled = false;
            Assert.Equal(RHAH_BackgroundStory.Unrest, RHAH_BackgroundStories.Select(options, 0.99f));
            options.unrestWeight = 0f;
            Assert.Null(RHAH_BackgroundStories.Select(options, 0.5f));
            options.enabled = false;
            Assert.Null(RHAH_BackgroundStories.Select(options, 0f));
        }

        [Fact]
        public void UnrestHasAnExactThreeDayDeadlineAndRespectsItsSwitch()
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings.background;
            int due = RHAH_BackgroundStories.DueTick(100, options.unrestDays);
            Assert.Equal(180100, due);
            Assert.Equal(1.5f, RHAH_BackgroundStories.UnrestMultiplier(options, due, due - 1));
            Assert.Equal(1f, RHAH_BackgroundStories.UnrestMultiplier(options, due, due));
            options.enabled = false;
            Assert.Equal(1f, RHAH_BackgroundStories.UnrestMultiplier(options, due, 100));
            options.enabled = true;
            options.unrestEnabled = false;
            Assert.Equal(1f, RHAH_BackgroundStories.UnrestMultiplier(options, due, 100));
            Assert.Equal(int.MaxValue, RHAH_BackgroundStories.DueTick(int.MaxValue - 2, 60f));
        }

        [Theory]
        [InlineData(3, true, true, 0)]
        [InlineData(4, true, true, 1)]
        [InlineData(5, true, true, 2)]
        [InlineData(5, true, false, 1)]
        [InlineData(5, false, true, 1)]
        [InlineData(5, false, false, 0)]
        public void ReloadedCivilWarWaitsOneFullYearAndPreservesThreeSettlements(int count, bool underground, bool warlord, int expected)
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings.background;
            options.civilWarChancePercent = 100f;
            options.takeoverChancePercent = 100f;
            Faction kingdom = Faction(RHAH_BackgroundStories.KingdomDef, 80);
            Faction(RHAH_WarlordCompat.UndergroundFaction, 81);
            Faction(RHAH_BackgroundStories.WarlordDef, 82);
            if (underground) activeMods.Add(RHAH_WarlordCompat.UndergroundPackage);
            if (warlord) activeMods.Add(RHAH_WarlordCompat.WarlordPackage);
            for (int i = 0; i < count; i++) AddSettlement(kingdom, i);
            string[] names = Find.WorldObjects.Settlements.Select(item => item.Name).OrderBy(name => name).ToArray();
            Settlement[] original = Find.WorldObjects.Settlements.ToArray();
            List<EventChainProviderState> states = new List<EventChainProviderState>();
            Restore(states, 100);
            RHAH_BackgroundFollow.StartCivilWar(kingdom, 100, options.civilWarDelayDays);
            int civilWarTick = 100 + 3 * GenDate.TicksPerDay;
            runtime.Tick(civilWarTick - 1);
            Assert.Empty(stories);
            Restore(states, civilWarTick - 1);
            runtime.Tick(civilWarTick);
            Assert.Equal(new[] { RHAH_BackgroundStory.CivilWar }, stories);
            int due = civilWarTick + GenDate.TicksPerYear;
            runtime.Tick(due - 1);
            Assert.Empty(takeovers);
            Restore(states, due - 1);
            runtime.Tick(due);
            Assert.True(expected == takeovers.Count, runtime.Describe());
            Assert.Equal(count - expected, Find.WorldObjects.Settlements.Count(item => item.Faction == kingdom));
            Assert.Equal(names, Find.WorldObjects.Settlements.Select(item => item.Name).OrderBy(name => name));
            Assert.Equal(expected, original.Count(item => item.Destroyed));
            Restore(states, due + 1);
            runtime.Tick(due + GenDate.TicksPerYear);
            Assert.True(expected == takeovers.Count, runtime.Describe());
            Assert.Single(stories);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FailedOrDisabledCivilWarDoesNotOpenTakeovers(bool disabled)
        {
            RHAH_Mod.Settings.background.civilWarChancePercent = 0f;
            Faction kingdom = Faction(RHAH_BackgroundStories.KingdomDef, 80);
            List<EventChainProviderState> states = new List<EventChainProviderState>();
            Restore(states, 100);
            RHAH_BackgroundFollow.StartCivilWar(kingdom, 100, 3f);
            if (disabled) RHAH_Mod.Settings.background.civilWarEnabled = false;
            runtime.Tick(180100);
            Assert.Empty(stories);
            Assert.Single(states[0].Instances);
            Assert.Equal(disabled ? EventChainEnd.Cancelled : EventChainEnd.Completed, states[0].Instances[0].End);
        }

        [Fact]
        public void ADisappearingKingdomDoesNotRedirectTheFollowup()
        {
            RHAH_Mod.Settings.background.civilWarChancePercent = 100f;
            Faction kingdom = Faction(RHAH_BackgroundStories.KingdomDef, 80);
            List<EventChainProviderState> states = new List<EventChainProviderState>();
            Restore(states, 100);
            RHAH_BackgroundFollow.StartCivilWar(kingdom, 100, 3f);
            Find.FactionManager.AllFactionsListForReading.Remove(kingdom);
            Faction(RHAH_BackgroundStories.KingdomDef, 90);
            runtime.Tick(180100);
            Assert.Empty(stories);
            Assert.Equal(EventChainEnd.Completed, states[0].Instances[0].End);
        }

        [Fact]
        public void SettingsRoundTripKeepsZeroWeightsAndOldSettingsGainDefaults()
        {
            RHAH_Mod.Settings.background.averageDays = 0f;
            RHAH_Mod.Settings.background.ruinsEnabled = false;
            RHAH_Mod.Settings.background.unrestWeight = 0f;
            RHAH_Mod.Settings.background.silver = new IntRange(123, 456);
            RHAH_Settings loaded = Reload(RHAH_Mod.Settings, () => new RHAH_Settings());
            Assert.Equal(0f, loaded.background.averageDays);
            Assert.False(loaded.background.ruinsEnabled);
            Assert.Equal(0f, loaded.background.unrestWeight);
            Assert.Equal(new IntRange(123, 456), loaded.background.silver);
            loaded = Reload<RHAH_Settings>(null, () => new RHAH_Settings());
            Assert.True(loaded.background.enabled);
            Assert.Equal(5f, loaded.background.civilWarChancePercent);
            Assert.Equal(60f, loaded.background.takeoverDelayDays);
            Assert.Equal(new IntRange(8, 20), loaded.background.ruinsDistance);
            Assert.Equal(new IntRange(3, 8), loaded.background.storyDistance);
            Assert.Equal(25f, loaded.background.tradeDiscountPercent);
            Assert.Equal(40, loaded.background.memorialSteel);
            Assert.Equal(80, loaded.background.memorialWood);
        }

        [Fact]
        public void RuinsPersistTheirOriginalRewardAcrossSettingsChanges()
        {
            List<WorldObjectDef> previous = new List<WorldObjectDef>(DefDatabase<WorldObjectDef>.AllDefsListForReading);
            WorldObjectDef def = new WorldObjectDef { defName = "RHAH_Ruins", comps = new List<WorldObjectCompProperties>() };
            try
            {
                DefDatabase<WorldObjectDef>.Add(def);
                WorldObject_RHAH_Ruins ruins = new WorldObject_RHAH_Ruins { def = def };
                ruins.Configure(17, 234, 3);
                RHAH_Mod.Settings.background.hardtack = new IntRange(0, 0);
                WorldObject_RHAH_Ruins loaded = Reload(ruins, () => new WorldObject_RHAH_Ruins());
                Assert.Equal(17, AccessTools.Field(typeof(WorldObject_RHAH_Ruins), "hardtackRemaining").GetValue(loaded));
                Assert.Equal(234, AccessTools.Field(typeof(WorldObject_RHAH_Ruins), "silverRemaining").GetValue(loaded));
                Assert.Equal(3, AccessTools.Field(typeof(WorldObject_RHAH_Ruins), "componentsRemaining").GetValue(loaded));
            }
            finally
            {
                DefDatabase<WorldObjectDef>.Clear();
                foreach (WorldObjectDef previousDef in previous) DefDatabase<WorldObjectDef>.Add(previousDef);
            }
        }

        [Fact]
        public void ExploringDeliversLegalStacksToTheCaravanAndCannotBeClaimedTwice()
        {
            List<ThingDef> previous = new List<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading);
            try
            {
                DefDatabase<ThingDef>.Clear();
                foreach (string name in new[] { WorldObject_RHAH_Ruins.HardtackDef, "Silver", "ComponentIndustrial" })
                    DefDatabase<ThingDef>.Add(new ThingDef
                    {
                        defName = name, label = name, category = ThingCategory.Item, thingClass = typeof(Thing),
                        stackLimit = name == "Silver" ? 500 : 75, useHitPoints = false
                    });
                Verse.Pawn carrier = new Verse.Pawn
                {
                    thingIDNumber = 101, def = new ThingDef { category = ThingCategory.Pawn, race = new RaceProperties() }
                };
                carrier.inventory = new Pawn_InventoryTracker(carrier);
                RimWorld.Planet.Caravan caravan = new RimWorld.Planet.Caravan
                {
                    ID = 101, def = new WorldObjectDef { canHaveFaction = true }
                };
                caravan.SetFaction(RimWorld.Faction.OfPlayer);
                caravan.PawnsListForReading.Add(carrier);
                WorldObject_RHAH_Ruins ruins = new WorldObject_RHAH_Ruins { ID = 102 };
                ruins.Configure(17, 1000, 3);
                HashSet<WorldObject> objects = (HashSet<WorldObject>)AccessTools.Field(typeof(WorldObjectsHolder), "worldObjectsHashSet").GetValue(Find.WorldObjects);
                objects.Add(caravan);
                objects.Add(ruins);

                Assert.True(ruins.Explore(caravan));
                List<Thing> inventory = carrier.inventory.innerContainer.InnerListForReading;
                Assert.Equal(17, inventory.Where(item => item.def.defName == WorldObject_RHAH_Ruins.HardtackDef).Sum(item => item.stackCount));
                Assert.Equal(1000, inventory.Where(item => item.def.defName == "Silver").Sum(item => item.stackCount));
                Assert.Equal(3, inventory.Where(item => item.def.defName == "ComponentIndustrial").Sum(item => item.stackCount));
                Assert.All(inventory, item => Assert.InRange(item.stackCount, 1, item.def.stackLimit));
                Assert.True(ruins.Destroyed);
                Assert.False(ruins.Explore(caravan));
                Assert.Equal(1020, inventory.Sum(item => item.stackCount));
            }
            finally
            {
                DefDatabase<ThingDef>.Clear();
                foreach (ThingDef def in previous) DefDatabase<ThingDef>.Add(def);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TradeDiscountSurvivesCancellationAndReloadAndIsConsumedOnlyBySuccessfulPurchases(bool weapons)
        {
            Faction kingdom = Faction(RHAH_BackgroundStories.KingdomDef, 80);
            List<EventChainProviderState> states = new List<EventChainProviderState>();
            Restore(states, 100);
            RHAH_BackgroundStory story = weapons ? RHAH_BackgroundStory.AidManifest : RHAH_BackgroundStory.HarvestFestival;
            Assert.True(RHAH_BackgroundTradeFollow.Start(story, kingdom, 100));
            Assert.False(RHAH_BackgroundTradeFollow.Start(story, kingdom, 100));
            Restore(states, 100);
            AccessTools.Field(typeof(TickManager), "ticksGameInt").SetValue(Find.TickManager, 100);
            TradeSession.trader = new TestTrader(kingdom);
            TradeSession.giftMode = false;
            ThingDef def = weapons ? Weapon("bbb.ratkinweapon.morefailure") : Food();
            Tradeable item = new Tradeable();
            item.AddThing(new Thing { def = def, stackCount = 5 }, Transactor.Trader);
            item.ForceTo(1);
            Patch(AccessTools.Method(typeof(Tradeable), nameof(Tradeable.GetPriceFor)), nameof(BasePrice));
            harmony.CreateClassProcessor(typeof(RHAH_BackgroundTrade).GetNestedType("PricePatch", BindingFlags.NonPublic)).Patch();
            Assert.Equal(75f, item.GetPriceFor(TradeAction.PlayerBuys));
            Assert.Equal(100f, item.GetPriceFor(TradeAction.PlayerSells));
            TradeSession.giftMode = true;
            Assert.Equal(100f, item.GetPriceFor(TradeAction.PlayerBuys));
            TradeSession.giftMode = false;
            TradeSession.trader = new TestTrader(Faction("Other", 81));
            Assert.Equal(100f, item.GetPriceFor(TradeAction.PlayerBuys));
            TradeSession.trader = new TestTrader(kingdom);
            Assert.NotNull(RHAH_BackgroundTradeFollow.Pending(80, weapons));

            TradeDeal deal = (TradeDeal)RuntimeHelpers.GetUninitializedObject(typeof(TradeDeal));
            AccessTools.Field(typeof(TradeDeal), "tradeables").SetValue(deal, new List<Tradeable> { item });
            MethodInfo execute = AccessTools.Method(typeof(TradeDeal), nameof(TradeDeal.TryExecute));
            harmony.Patch(execute, prefix: new HarmonyMethod(typeof(RHAH_BackgroundTests), nameof(ExecuteTrade)) { priority = Priority.Last });
            harmony.CreateClassProcessor(typeof(RHAH_BackgroundTrade).GetNestedType("ExecutePatch", BindingFlags.NonPublic)).Patch();
            tradeSucceeds = false;
            Assert.False(deal.TryExecute(out bool traded));
            Assert.False(traded);
            Assert.NotNull(RHAH_BackgroundTradeFollow.Pending(80, weapons));
            tradeSucceeds = true;
            item.ForceTo(-1);
            Assert.True(deal.TryExecute(out traded));
            Assert.NotNull(RHAH_BackgroundTradeFollow.Pending(80, weapons));
            deal.AllTradeables.Add(item);
            item.ForceTo(1);
            Assert.True(deal.TryExecute(out traded));
            Assert.True(traded);
            Assert.Null(RHAH_BackgroundTradeFollow.Pending(80, weapons));
            Assert.Equal(100f, item.GetPriceFor(TradeAction.PlayerBuys));
            Restore(states, 101);
            runtime.Tick(101);
            Assert.Single(takeovers);
            Restore(states, 102);
            runtime.Tick(1000);
            Assert.Single(takeovers);
        }

        [Theory]
        [InlineData("Solaris.RatkinRaceMod", true)]
        [InlineData("bbb.ratkinweapon.morefailure", true)]
        [InlineData("fxz.ratkinfaction", true)]
        [InlineData("RKU.RatkinUnderground", true)]
        [InlineData("Other.Mod", false)]
        public void AidWeaponsUseTheirOwningModInsteadOfTheirName(string package, bool expected)
        {
            ThingDef weapon = Weapon(package);
            Assert.Equal(expected, RHAH_BackgroundTrade.RatkinWeapon(weapon));
            weapon.tools.Clear();
            Assert.False(RHAH_BackgroundTrade.RatkinWeapon(weapon));
        }

        [Fact]
        public void CompetingFoodStoriesKeepSeparateDiscountsInsteadOfStacking()
        {
            Faction kingdom = Faction(RHAH_BackgroundStories.KingdomDef, 80);
            List<EventChainProviderState> states = new List<EventChainProviderState>();
            Restore(states, 100);
            Assert.True(RHAH_BackgroundTradeFollow.Start(RHAH_BackgroundStory.HarvestFestival, kingdom, 100));
            Assert.True(RHAH_BackgroundTradeFollow.Start(RHAH_BackgroundStory.CancelledBanquet, kingdom, 100));
            EventChainInstance first = RHAH_BackgroundTradeFollow.Pending(80, false);
            Assert.Equal(RHAH_BackgroundStory.HarvestFestival, first.PreparedState);
            Assert.True(RHAH_BackgroundTradeFollow.Consume(first.Id, 100));
            Assert.False(RHAH_BackgroundTradeFollow.Consume(first.Id, 100));
            Assert.Equal(RHAH_BackgroundStory.CancelledBanquet, RHAH_BackgroundTradeFollow.Pending(80, false).PreparedState);
        }

        [Theory]
        [InlineData(false, false, 40, 5)]
        [InlineData(false, true, 80, 5)]
        [InlineData(true, false, 20, 8)]
        [InlineData(true, true, 40, 8)]
        public void MemorialConsumesActualMaterialsAndSettlesItsGoodwillOnce(bool restore, bool wood, int cost, int goodwill)
        {
            Faction underground = FriendlyFaction(RHAH_WarlordCompat.UndergroundFaction, 80);
            Faction warlord = FriendlyFaction(RHAH_BackgroundStories.WarlordDef, 81);
            RimWorld.Planet.Caravan caravan = Caravan();
            WorldObject_RHAH_BackgroundSite site = Site(RHAH_BackgroundStory.Memorial, underground, warlord, 102);
            ThingDef material = new ThingDef { defName = "Material", stackLimit = 75, category = ThingCategory.Item, thingClass = typeof(Thing), useHitPoints = false };
            ThingDef previous = wood ? ThingDefOf.WoodLog : ThingDefOf.Steel;
            try
            {
                if (wood) ThingDefOf.WoodLog = material; else ThingDefOf.Steel = material;
                Thing item = new Thing { def = material, stackCount = cost - 1 };
                Assert.True(caravan.PawnsListForReading[0].inventory.innerContainer.TryAdd(item));
                Assert.False(site.Supply(caravan, restore, wood));
                Assert.Equal(cost - 1, item.stackCount);
                Assert.Equal(0, underground.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
                item.stackCount = cost + 2;
                Assert.True(site.Supply(caravan, restore, wood));
                Assert.Equal(2, caravan.PawnsListForReading[0].inventory.innerContainer.TotalStackCountOfDef(material));
                Assert.Equal(goodwill, underground.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
                Assert.Equal(goodwill, warlord.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
                Assert.False(site.Supply(caravan, restore, wood));
                Assert.Equal(goodwill, underground.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
            }
            finally
            {
                if (wood) ThingDefOf.WoodLog = previous; else ThingDefOf.Steel = previous;
            }
        }

        [Fact]
        public void EvacuationGrantsBothFactionsGoodwillEvenWhenJoiningIsDeclined()
        {
            Faction underground = FriendlyFaction(RHAH_WarlordCompat.UndergroundFaction, 80);
            Faction warlord = FriendlyFaction(RHAH_BackgroundStories.WarlordDef, 81);
            RimWorld.Planet.Caravan caravan = Caravan();
            WorldObject_RHAH_BackgroundSite site = Site(RHAH_BackgroundStory.SameCallsign, underground, warlord, 102);
            AccessTools.Field(site.GetType(), "joinOffered").SetValue(site, true);
            Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.WindowStack)), nameof(ReadWindows));
            Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add)), nameof(IgnoreWindow));
            site.Visit(caravan);
            Assert.True(site.JoinPending);
            Assert.Equal(5, underground.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
            Assert.Equal(5, warlord.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
            site.Visit(caravan);
            Assert.Equal(5, underground.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
            Assert.True(site.ResolveJoin(caravan, false));
            Assert.True(site.Destroyed);
            Assert.False(site.ResolveJoin(caravan, false));
            Assert.Single(caravan.PawnsListForReading);
        }

        [Fact]
        public void NamesFinishOnlyTheirOriginalMemorialWithTheirOwnGoodwill()
        {
            Faction underground = FriendlyFaction(RHAH_WarlordCompat.UndergroundFaction, 80);
            Faction warlord = FriendlyFaction(RHAH_BackgroundStories.WarlordDef, 81);
            RimWorld.Planet.Caravan caravan = Caravan();
            WorldObject_RHAH_BackgroundSite memorial = Site(RHAH_BackgroundStory.Memorial, underground, warlord, 102);
            WorldObject_RHAH_BackgroundSite names = Site(RHAH_BackgroundStory.Memorial, underground, warlord, 103);
            AccessTools.Field(names.GetType(), "memorialId").SetValue(names, memorial.ID);
            AccessTools.Field(memorial.GetType(), "nameListId").SetValue(memorial, names.ID);
            Assert.False(memorial.Supply(caravan, true, false));
            names.Visit(caravan);
            Assert.True(names.Destroyed);
            Assert.True(memorial.Destroyed);
            Assert.Equal(10, underground.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
            Assert.Equal(3, warlord.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
            names.Visit(caravan);
            Assert.Equal(10, underground.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
        }

        [Fact]
        public void ReloadKeepsAnEvacuatedJoinOfferWithoutGrantingGoodwillAgain()
        {
            List<WorldObjectDef> previous = new List<WorldObjectDef>(DefDatabase<WorldObjectDef>.AllDefsListForReading);
            WorldObjectDef def = new WorldObjectDef { defName = "RHAH_BackgroundSite", comps = new List<WorldObjectCompProperties>() };
            try
            {
                DefDatabase<WorldObjectDef>.Add(def);
                Faction underground = FriendlyFaction(RHAH_WarlordCompat.UndergroundFaction, 80);
                Faction warlord = FriendlyFaction(RHAH_BackgroundStories.WarlordDef, 81);
                RimWorld.Planet.Caravan caravan = Caravan();
                WorldObject_RHAH_BackgroundSite original = Site(RHAH_BackgroundStory.SameCallsign, underground, warlord, 102);
                original.def = def;
                AccessTools.Field(original.GetType(), "evacuated").SetValue(original, true);
                AccessTools.Field(original.GetType(), "joinOffered").SetValue(original, true);
                RHAH_Mod.Settings.background.woundedJoinChancePercent = 0f;
                WorldObject_RHAH_BackgroundSite loaded = Reload(original, () => new WorldObject_RHAH_BackgroundSite());
                AddWorldObject(loaded);
                Assert.True(loaded.JoinPending);
                Assert.True(loaded.ResolveJoin(caravan, false));
                Assert.True(loaded.Destroyed);
                Assert.Equal(0, underground.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
                Assert.Equal(0, warlord.BaseGoodwillWith(RimWorld.Faction.OfPlayer));
            }
            finally
            {
                DefDatabase<WorldObjectDef>.Clear();
                foreach (WorldObjectDef previousDef in previous) DefDatabase<WorldObjectDef>.Add(previousDef);
            }
        }

        static ThingDef Food()
        {
            ThingDef def = new ThingDef { defName = "Food", ingestible = new IngestibleProperties() };
            AccessTools.Field(typeof(IngestibleProperties), "cachedNutrition").SetValue(def.ingestible, 0.9f);
            return def;
        }

        static ThingDef Weapon(string package)
        {
            ModContentPack mod = (ModContentPack)RuntimeHelpers.GetUninitializedObject(typeof(ModContentPack));
            AccessTools.Field(typeof(ModContentPack), "packageIdPlayerFacingInt").SetValue(mod, package);
            return new ThingDef { defName = "ArbitraryName", category = ThingCategory.Item, modContentPack = mod, tools = new List<Tool> { new Tool() } };
        }

        static Faction FriendlyFaction(string defName, int id)
        {
            Faction faction = Faction(defName, id);
            faction.SetRelation(new FactionRelation { other = RimWorld.Faction.OfPlayer, baseGoodwill = 0, kind = FactionRelationKind.Neutral });
            RimWorld.Faction.OfPlayer.RelationWith(faction).baseGoodwill = 0;
            return faction;
        }

        static RimWorld.Planet.Caravan Caravan()
        {
            Verse.Pawn pawn = new Verse.Pawn { def = new ThingDef { category = ThingCategory.Pawn, race = new RaceProperties() } };
            pawn.inventory = new Pawn_InventoryTracker(pawn);
            RimWorld.Planet.Caravan caravan = new RimWorld.Planet.Caravan { ID = 101, def = new WorldObjectDef { canHaveFaction = true } };
            caravan.SetFaction(RimWorld.Faction.OfPlayer);
            caravan.PawnsListForReading.Add(pawn);
            AddWorldObject(caravan);
            return caravan;
        }

        static WorldObject_RHAH_BackgroundSite Site(RHAH_BackgroundStory story, Faction underground, Faction warlord, int id)
        {
            WorldObject_RHAH_BackgroundSite site = new WorldObject_RHAH_BackgroundSite { ID = id };
            AccessTools.Field(site.GetType(), "story").SetValue(site, story);
            AccessTools.Field(site.GetType(), "undergroundId").SetValue(site, underground.loadID);
            AccessTools.Field(site.GetType(), "warlordId").SetValue(site, warlord.loadID);
            AddWorldObject(site);
            return site;
        }

        static bool ReadWindows(ref WindowStack __result) { __result = new WindowStack(); return false; }
        static bool IgnoreWindow() { return false; }
        static bool BasePrice(ref float __result) { __result = 100f; return false; }
        static bool ExecuteTrade(TradeDeal __instance, out bool actuallyTraded, ref bool __result)
        {
            actuallyTraded = __result = active.tradeSucceeds;
            if (__result) __instance.AllTradeables.Clear();
            return false;
        }

        sealed class TestTrader : ITrader
        {
            public TestTrader(Faction faction) { Faction = faction; }
            public TraderKindDef TraderKind => null;
            public IEnumerable<Thing> Goods => Array.Empty<Thing>();
            public int RandomPriceFactorSeed => 0;
            public string TraderName => "Test";
            public bool CanTradeNow => true;
            public float TradePriceImprovementOffsetForPlayer => 0f;
            public Faction Faction { get; }
            public TradeCurrency TradeCurrency => TradeCurrency.Silver;
            public IEnumerable<Thing> ColonyThingsWillingToBuy(Verse.Pawn playerNegotiator) => Array.Empty<Thing>();
            public void GiveSoldThingToTrader(Thing toGive, int countToGive, Verse.Pawn playerNegotiator) { }
            public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Verse.Pawn playerNegotiator) { }
        }

        static T Reload<T>(T source, Func<T> create) where T : class, IExposable
        {
            ScribeSaver previousSaver = Scribe.saver;
            ScribeLoader previousLoader = Scribe.loader;
            LoadSaveMode previousMode = Scribe.mode;
            bool previousProfiling = DeepProfiler.enabled;
            FieldInfo prefs = AccessTools.Field(typeof(Prefs), "data");
            object previousPrefs = prefs.GetValue(null);
            string path = Path.GetTempFileName();
            try
            {
                Scribe.saver = new ScribeSaver();
                Scribe.loader = new ScribeLoader();
                Scribe.mode = LoadSaveMode.Inactive;
                DeepProfiler.enabled = false;
                prefs.SetValue(null, new PrefsData { devMode = false });
                if (source == null) File.WriteAllText(path, "<data />");
                else
                {
                    Scribe.saver.InitSaving(path, "data");
                    source.ExposeData();
                    Scribe.saver.FinalizeSaving();
                }
                Scribe.loader.InitLoading(path);
                T loaded = create();
                Scribe.loader.curParent = loaded;
                loaded.ExposeData();
                Scribe.loader.crossRefs.RegisterForCrossRefResolve(loaded);
                Scribe.loader.initer.RegisterForPostLoadInit(loaded);
                Scribe.loader.FinalizeLoading();
                return loaded;
            }
            finally
            {
                Scribe.ForceStop();
                Scribe.saver = previousSaver;
                Scribe.loader = previousLoader;
                Scribe.mode = previousMode;
                DeepProfiler.enabled = previousProfiling;
                prefs.SetValue(null, previousPrefs);
                File.Delete(path);
            }
        }

        void Restore(List<EventChainProviderState> states, int tick)
        {
            runtime = new EventChainRuntime(states);
            runtime.Register(new RHAH_EventChainProvider());
            runtime.Activate(tick);
        }

        static Faction Faction(string defName, int id)
        {
            Faction faction = new Faction { def = new FactionDef { defName = defName }, loadID = id, Name = defName };
            Find.FactionManager.AllFactionsListForReading.Add(faction);
            return faction;
        }

        static void AddSettlement(Faction kingdom, int id)
        {
            Settlement settlement = new Settlement
            {
                ID = id, Name = "Town " + id,
                def = new WorldObjectDef { canHaveFaction = true, worldObjectClass = typeof(Settlement), comps = new List<WorldObjectCompProperties>() }
            };
            settlement.SetFaction(kingdom);
            Find.WorldObjects.Settlements.Add(settlement);
            ((HashSet<WorldObject>)AccessTools.Field(typeof(WorldObjectsHolder), "worldObjectsHashSet").GetValue(Find.WorldObjects)).Add(settlement);
        }

        void Patch(MethodInfo target, string prefix)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(RHAH_BackgroundTests), prefix));
        }

        static bool ReadRuntime(ref EventChainRuntime __result) { __result = active.runtime; return false; }
        static bool IsActive(string __0, ref bool __result) { __result = active.activeMods.Contains(__0); return false; }
        static bool SendStory(RHAH_BackgroundStory __0) { active.stories.Add(__0); return false; }
        static bool Receive(TaggedString __1) { active.takeovers.Add(__1.ToString()); return false; }
        static bool AddWorldObject(WorldObject __0)
        {
            Find.WorldObjects.AllWorldObjects.Add(__0);
            ((HashSet<WorldObject>)AccessTools.Field(typeof(WorldObjectsHolder), "worldObjectsHashSet").GetValue(Find.WorldObjects)).Add(__0);
            if (__0 is Settlement settlement) Find.WorldObjects.Settlements.Add(settlement);
            return false;
        }

        static bool DestroyWorldObject(WorldObject __instance)
        {
            if (!(__instance is WorldObject_RHAH_Ruins) && !(__instance is WorldObject_RHAH_BackgroundSite) && !(__instance is Settlement)) return true;
            Find.WorldObjects.AllWorldObjects.Remove(__instance);
            ((HashSet<WorldObject>)AccessTools.Field(typeof(WorldObjectsHolder), "worldObjectsHashSet").GetValue(Find.WorldObjects)).Remove(__instance);
            if (__instance is Settlement settlement) Find.WorldObjects.Settlements.Remove(settlement);
            AccessTools.Field(typeof(WorldObject), "destroyed").SetValue(__instance, true);
            return false;
        }
    }
}
