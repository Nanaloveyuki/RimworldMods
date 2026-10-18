using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Pawn;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_RequestRulesTests
    {
        [Theory]
        [InlineData(1, 0.9f, 3)]
        [InlineData(3, 0.9f, 9)]
        [InlineData(1, 0.05f, 54)]
        [InlineData(3, 0.05f, 100)]
        [InlineData(1, 0.8f, 4)]
        [InlineData(0, 0.9f, 0)]
        [InlineData(3, 0f, 0)]
        public void TransactionQuoteRoundsNutritionWithoutBoundaryOvershoot(int people, float nutrition, int expected)
        {
            Assert.Equal(expected, RHAH_RequestRules.RequiredFoodUnits(people, nutrition));
        }

        [Fact]
        public void LargestFoodUsesTotalStockAndStableTieBreak()
        {
            ThingDef meals = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            meals.defName = "MealSimple";
            ThingDef potatoes = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            potatoes.defName = "RawPotatoes";
            Dictionary<ThingDef, int> counts = new Dictionary<ThingDef, int> { [meals] = 30, [potatoes] = 60 };
            Assert.Same(potatoes, RHAH_RequestRules.LargestFood(counts));
            counts[meals] = 60;
            Assert.Same(meals, RHAH_RequestRules.LargestFood(counts));
            counts.Clear();
            Assert.Null(RHAH_RequestRules.LargestFood(counts));
        }

        [Fact]
        public void RewardValueUsesCappedActualFoodNotVisitorCount()
        {
            int count = RHAH_RequestRules.RequiredFoodUnits(200, 0.05f);
            Assert.Equal(100, count);
            Assert.Equal(200, RHAH_RequestRules.RewardUnits(count, 2f, 1f, 1f));
            Assert.Equal(0, RHAH_RequestRules.RewardUnits(0, 2f, 1f, 1f));
        }

        [Fact]
        public void ChildOutcomeKeepsLegacyAndExplicitFoodAlternativesDistinct()
        {
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord { DisplayId = "I-013", Choice = RHAH_ChoiceKind.ChildExchange,
                Kind = RHAH_RequestKind.SimpleMeal, Settled = RHAH_ChoiceAction.Deliver };
            Assert.True(RHAH_ChildExchange.FoodAlternative(record));
            record.Kind = RHAH_RequestKind.Baby;
            Assert.False(RHAH_ChildExchange.FoodAlternative(record));
            record.ChildExchangeVersion = 1;
            record.ChildFoodAlternative = true;
            Assert.True(RHAH_ChildExchange.FoodAlternative(record));
            record.ChildFoodAlternative = false;
            record.OutgoingChildThingId = 17;
            Assert.False(RHAH_ChildExchange.FoodAlternative(record));
        }

        [Fact]
        public void BabyRequestFoodPaymentDoesNotCountAsHandingOverAChild()
        {
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord { DisplayId = "I-019", Choice = RHAH_ChoiceKind.Aid,
                Kind = RHAH_RequestKind.Baby, Settled = RHAH_ChoiceAction.Deliver, ChildExchangeVersion = 1,
                ChildFoodAlternative = true };
            Assert.True(RHAH_ChildExchange.FoodAlternative(record));
            record.ChildFoodAlternative = false;
            Assert.False(RHAH_ChildExchange.FoodAlternative(record));
        }


        [Fact]
        public void AidAndIntelKeepOldAmountsAndSites()
        {
            Assert.Equal(RHAH_RequestKind.SimpleMeal, RHAH_RequestRules.SpecFor("I-015").Kind);
            Assert.Equal(RHAH_IntelSiteKind.Treasure, RHAH_RequestRules.SpecFor("I-020").Site);
            Assert.Equal(RHAH_IntelSiteKind.Structure, RHAH_RequestRules.SpecFor("I-024").Site);
            Assert.Equal(RHAH_IntelSiteKind.Settlement, RHAH_RequestRules.SpecFor("I-028").Site);
            Assert.Equal(6, RHAH_RequestRules.Amount(RHAH_RequestKind.SimpleMeal, 0f, 0));
            Assert.Equal(28, RHAH_RequestRules.Amount(RHAH_RequestKind.SimpleMeal, 1000000f, 4));
            Assert.Equal(80, RHAH_RequestRules.Amount(RHAH_RequestKind.Silver, 0f, 0));
            Assert.Equal(1, RHAH_RequestRules.Amount(RHAH_RequestKind.Baby, 0f, 0));
            Assert.Equal("ItemStash", RHAH_RequestRules.SitePartDefName(RHAH_IntelSiteKind.Treasure));
        }

        [Fact]
        public void ClosedOrRepeatedChoicesDoNotSettleAgain()
        {
            Assert.Equal(RHAH_ChoiceAction.None, RHAH_RequestRules.Settle(RHAH_ChoiceAction.Deliver, false, false, true));
            Assert.Equal(RHAH_ChoiceAction.None, RHAH_RequestRules.Settle(RHAH_ChoiceAction.Deliver, true, true, true));
            Assert.Equal(RHAH_ChoiceAction.None, RHAH_RequestRules.Settle(RHAH_ChoiceAction.Deliver, true, false, false));
            Assert.Equal(RHAH_ChoiceAction.Deliver, RHAH_RequestRules.Settle(RHAH_ChoiceAction.Deliver, true, false, true));
            Assert.True(RHAH_RequestRules.CreatesSite(RHAH_ChoiceAction.Deliver, RHAH_IntelSiteKind.Treasure));
            Assert.False(RHAH_RequestRules.CreatesSite(RHAH_ChoiceAction.Reject, RHAH_IntelSiteKind.Treasure));
        }

        [Theory]
        [InlineData("Prison", RHAH_ChoiceAction.Capture)]
        [InlineData("12", RHAH_ChoiceAction.Capture)]
        [InlineData(null, RHAH_ChoiceAction.None)]
        [InlineData("None", RHAH_ChoiceAction.None)]
        [InlineData("Capture", RHAH_ChoiceAction.Capture)]
        [InlineData("10", RHAH_ChoiceAction.Capture)]
        [InlineData("Ally", RHAH_ChoiceAction.Ally)]
        [InlineData("13", RHAH_ChoiceAction.Ally)]
        [InlineData("37", (RHAH_ChoiceAction)37)]
        public void SettledActionsLoadAndSaveWithoutReopeningChoices(string savedAction, RHAH_ChoiceAction expected)
        {
            ScribeSaver previousSaver = Scribe.saver;
            ScribeLoader previousLoader = Scribe.loader;
            LoadSaveMode previousMode = Scribe.mode;
            bool previousProfiling = DeepProfiler.enabled;
            FieldInfo prefsField = typeof(Prefs).GetField("data", BindingFlags.NonPublic | BindingFlags.Static);
            object previousPrefs = prefsField.GetValue(null);
            string path = Path.GetTempFileName();
            try
            {
                Scribe.saver = new ScribeSaver();
                Scribe.loader = new ScribeLoader();
                Scribe.mode = LoadSaveMode.Inactive;
                DeepProfiler.enabled = false;
                prefsField.SetValue(null, new PrefsData { devMode = false });

                XmlDocument fixture = new XmlDocument();
                fixture.LoadXml("<record />");
                if (savedAction != null)
                {
                    XmlElement settled = fixture.CreateElement("settled");
                    settled.InnerText = savedAction;
                    fixture.DocumentElement.AppendChild(settled);
                }
                fixture.Save(path);

                Scribe.loader.InitLoading(path);
                RHAH_ChoiceRecord loaded = new RHAH_ChoiceRecord();
                Scribe.loader.curParent = loaded;
                loaded.ExposeData();
                Scribe.loader.initer.RegisterForPostLoadInit(loaded);
                Scribe.loader.FinalizeLoading();
                Assert.Equal(expected, loaded.Settled);
                Assert.Equal(expected == RHAH_ChoiceAction.None, loaded.Open);
                Assert.Equal(0, loaded.TransactionVersion);
                Assert.Equal(0, loaded.ChildExchangeVersion);
                loaded.TransactionVersion = 1;
                loaded.QuotedFoodDefName = "RawPotatoes";
                loaded.QuotedFoodCount = 100;
                loaded.RewardDefNames.Add("Silver");
                loaded.RewardCounts.Add(200);
                loaded.RewardThingIds.Add(41);
                loaded.OutgoingChildThingId = 17;
                loaded.ResponsibleAdultThingId = 19;
                loaded.ChildExchangeVersion = 1;
                loaded.ChildFoodAlternative = false;

                Scribe.saver.InitSaving(path, "record");
                loaded.ExposeData();
                Scribe.saver.FinalizeSaving();
                XmlDocument written = new XmlDocument();
                written.Load(path);
                string expectedText = expected == RHAH_ChoiceAction.None ? null : expected.ToString();
                Assert.Equal(expectedText, written.SelectSingleNode("/record/settled")?.InnerText);

                Scribe.loader.InitLoading(path);
                RHAH_ChoiceRecord reloaded = new RHAH_ChoiceRecord();
                Scribe.loader.curParent = reloaded;
                reloaded.ExposeData();
                Scribe.loader.initer.RegisterForPostLoadInit(reloaded);
                Scribe.loader.FinalizeLoading();
                Assert.Equal(expected, reloaded.Settled);
                Assert.Equal(expected == RHAH_ChoiceAction.None, reloaded.Open);
                Assert.Equal(1, reloaded.TransactionVersion);
                Assert.Equal("RawPotatoes", reloaded.QuotedFoodDefName);
                Assert.Equal(100, reloaded.QuotedFoodCount);
                Assert.Equal(new[] { "Silver" }, reloaded.RewardDefNames);
                Assert.Equal(new[] { 200 }, reloaded.RewardCounts);
                Assert.Equal(new[] { 41 }, reloaded.RewardThingIds);
                Assert.Equal(17, reloaded.OutgoingChildThingId);
                Assert.Equal(19, reloaded.ResponsibleAdultThingId);
                Assert.Equal(1, reloaded.ChildExchangeVersion);
                Assert.False(reloaded.ChildFoodAlternative);
            }
            finally
            {
                Scribe.ForceStop();
                Scribe.saver = previousSaver;
                Scribe.loader = previousLoader;
                Scribe.mode = previousMode;
                DeepProfiler.enabled = previousProfiling;
                prefsField.SetValue(null, previousPrefs);
                File.Delete(path);
            }
        }

        [Fact]
        public void CaptureDoesNotAcceptTheRemovedPrisonActionAtRuntime()
        {
            Assert.True(RHAH_RequestRules.Captures(RHAH_ChoiceAction.Capture));
            Assert.False(RHAH_RequestRules.Captures((RHAH_ChoiceAction)12));
            Assert.False(RHAH_RequestRules.Captures(RHAH_ChoiceAction.Enslave));
            Assert.False(RHAH_RequestRules.Captures(RHAH_ChoiceAction.Attack));
        }

        [Fact]
        public void DisabledChoicesStillExpireButDoNotDeliver()
        {
            Assert.Equal(RHAH_ChoiceAction.Timeout, RHAH_RequestRules.Settle(RHAH_ChoiceAction.Timeout, false, false, false));
            Assert.Equal(RHAH_ChoiceAction.None, RHAH_RequestRules.Settle(RHAH_ChoiceAction.Deliver, false, false, true));
            Assert.Equal(RHAH_ChoiceAction.None, RHAH_RequestRules.Settle(RHAH_ChoiceAction.Reject, false, false, true));
            Assert.Equal(RHAH_ChoiceAction.None, RHAH_RequestRules.Settle(RHAH_ChoiceAction.Timeout, false, true, false));
            Assert.False(RHAH_RequestRules.RemovesStock(RHAH_ChoiceAction.Reject, RHAH_RequestKind.SimpleMeal));
            Assert.False(RHAH_RequestRules.RemovesStock(RHAH_ChoiceAction.Ignore, RHAH_RequestKind.SimpleMeal));
            Assert.False(RHAH_RequestRules.RemovesStock(RHAH_ChoiceAction.Timeout, RHAH_RequestKind.SimpleMeal));
            Assert.False(RHAH_RequestRules.RemovesStock(RHAH_ChoiceAction.Deliver, RHAH_RequestKind.Baby));
            Assert.True(RHAH_RequestRules.RemovesStock(RHAH_ChoiceAction.Deliver, RHAH_RequestKind.SimpleMeal));
        }

        [Fact]
        public void DisabledOpenChoicesExpireOnceAndStayClosed()
        {
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(null);
            RHAH_ChoiceRecord opened = RHAH_ChoiceRuntime.Open(game, new RHAH_ChoiceRecord
            {
                DisplayId = "I-008",
                Choice = RHAH_ChoiceKind.Visitors,
                ExpireTick = 10
            });
            Assert.NotNull(opened);

            RHAH_ChoiceRuntime.Tick(game, 9);
            Assert.True(opened.Open);

            RHAH_ChoiceRuntime.Tick(game, 10);
            Assert.Equal(RHAH_ChoiceAction.Timeout, opened.Settled);
            Assert.False(opened.Open);

            RHAH_ChoiceRuntime.Tick(game, 11);
            Assert.Equal(RHAH_ChoiceAction.Timeout, opened.Settled);

            Assert.Equal(
                RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(game, opened.Id, RHAH_ChoiceAction.Deliver, 12, false, true));
            Assert.Equal(RHAH_ChoiceAction.Timeout, opened.Settled);
        }

        [Fact]
        public void SpecialIncidentsOfferAcceptRejectAndIgnore()
        {
            Assert.Equal(RHAH_ChoiceKind.Abandoned, RHAH_RequestRules.SpecFor("I-003").Choice);
            Assert.Equal(RHAH_ChoiceKind.Refugees, RHAH_RequestRules.SpecFor("I-011").Choice);
            Assert.Equal(RHAH_ChoiceKind.ChildExchange, RHAH_RequestRules.SpecFor("I-013").Choice);
            Assert.Equal(RHAH_ChoiceKind.Airdrop, RHAH_RequestRules.SpecFor("I-032").Choice);
            Assert.Equal(RHAH_ChoiceKind.Kinship, RHAH_RequestRules.SpecFor("I-033").Choice);
            Assert.True(RHAH_RequestRules.OffersVisitorControl("I-008"));
            Assert.False(RHAH_RequestRules.OffersVisitorControl("I-051"));
        }

        [Theory]
        [InlineData("I-032")]
        [InlineData("I-046")]
        public void AirdropOffersPermanentJoinOnlyWhileVisitorsArePresent(string displayId)
        {
            Assert.True(RHAH_RequestRules.ShowsJoin(displayId, true));
            Assert.False(RHAH_RequestRules.ShowsJoin(displayId, false));
        }

        [Fact]
        public void VisitorLettersRestoreOptionsTheOldPolicyDropped()
        {
            Assert.True(RHAH_RequestRules.ShowsRecruit("I-008", true));
            Assert.True(RHAH_RequestRules.ShowsHire("I-008", true));
            Assert.False(RHAH_RequestRules.ShowsHire("I-008", false));
            Assert.False(RHAH_RequestRules.ShowsHire("I-002", true));
            Assert.False(RHAH_RequestRules.ShowsHire("I-051", true));
            Assert.False(RHAH_RequestRules.ShowsRecruit("I-008", false));
            Assert.False(RHAH_RequestRules.ShowsJoin("I-002", true));
            Assert.True(RHAH_RequestRules.ShowsJoin("I-011", true));
            Assert.False(RHAH_RequestRules.ShowsEnslave(false, true));
            Assert.True(RHAH_RequestRules.ShowsEnslave(true, true));
            Assert.False(RHAH_RequestRules.ShowsCapture(false));
            Assert.True(RHAH_RequestRules.ShowsAttack(true));
            Assert.True(RHAH_RequestRules.ShowsAlly(true, true));
            Assert.False(RHAH_RequestRules.ShowsAlly(true, false));
            Assert.False(RHAH_RequestRules.ShowsAlly(false, true));
            Assert.True(RHAH_RequestRules.SendsToAlly(RHAH_ChoiceAction.Ally));
            Assert.False(RHAH_RequestRules.Leaves(RHAH_ChoiceAction.Ally));
            Assert.False(RHAH_RequestRules.PromisesAid(RHAH_ChoiceKind.Visitors, RHAH_ChoiceAction.Ally));
            Assert.True(RHAH_RequestRules.IsAllyDestination(false, false, false, false, true, false, true, true, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(true, false, false, false, true, false, true, true, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(false, true, false, false, true, false, true, true, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(false, false, true, false, true, false, true, true, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(false, false, false, true, true, false, true, true, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(false, false, false, false, false, false, true, true, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(false, false, false, false, true, true, true, true, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(false, false, false, false, true, false, false, true, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(false, false, false, false, true, false, true, false, false));
            Assert.False(RHAH_RequestRules.IsAllyDestination(false, false, false, false, true, false, true, true, true));
            Assert.False(RHAH_RequestRules.ShowsFoodGive(RHAH_ChoiceKind.Kinship));
            Assert.Equal(3, RHAH_RequestRules.FoodRequestCount(3));
            Assert.Equal(12, RHAH_RequestRules.FoodRequestCount(40));
            Assert.True(RHAH_RequestRules.ShowsFoodHint(false, true));
            Assert.False(RHAH_RequestRules.ShowsFoodHint(true, true));
            Assert.False(RHAH_RequestRules.ShowsFoodHint(false, false));
            Assert.True(RHAH_RequestRules.FoodWaiting(true, true, true, 0, 2));
            Assert.False(RHAH_RequestRules.FoodWaiting(true, true, true, 2, 2));
            Assert.False(RHAH_RequestRules.FoodWaiting(false, true, true, 0, 2));
        }
        [Fact]
        public void TraderCaravansStayAndFoodReplacesChildren()
        {
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.BlocksEnvironmentLeave(true, true));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.BlocksEnvironmentLeave(false, true));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.BlocksEnvironmentLeave(true, false));
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.BlocksEnclosedLeave(true, true));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.BlocksEnclosedLeave(false, true));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.BlocksEnclosedLeave(true, false));
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.CellIsHarsh(-30f, 0f, 20f));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.CellIsHarsh(15f, 0f, 20f));
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.RoomIsEnclosed(true, false, false));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.RoomIsEnclosed(true, true, false));
            Assert.Equal(20, RHAH_RequestRules.FoodForChildren(2));
            Assert.True(RHAH_RequestRules.CanSubstituteFood(true, RHAH_ChoiceKind.ChildExchange, 20, 2));
            Assert.False(RHAH_RequestRules.CanSubstituteFood(false, RHAH_ChoiceKind.ChildExchange, 20, 2));
            Assert.False(RHAH_RequestRules.CanSubstituteFood(true, RHAH_ChoiceKind.ChildExchange, 19, 2));
            Assert.False(RHAH_RequestRules.CanSubstituteFood(true, RHAH_ChoiceKind.Aid, 20, 2));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.NpcSellsFood(true, true, "MealSimple"));
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.NpcSellsFood(true, true, "Meal_RatEggMeatStewed"));
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.NpcSellsFood(true, false, "MealSimple"));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.ShouldLeave(false, false, false, true, true, true, true));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.ShouldLeave(true, true, true, true, true, true, true));
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.ShouldLeave(true, false, true, true, false, false, false));
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.ShouldLeave(true, true, false, false, false, false, true));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.ShouldLeave(true, false, false, false, false, false, false));
            Assert.True(HungerAndHavoc.Trade.RHAH_CaravanStay.TemperatureSevere(0.16f, 0f));
            Assert.False(HungerAndHavoc.Trade.RHAH_CaravanStay.TemperatureSevere(0.15f, 0.15f));
        }

        [Fact]
        public void FamilyDropRecordsEachChildOnce()
        {
            List<int> dropped = new List<int>();
            Assert.True(RHAH_FamilyRules.CanDrop(RHAH_PawnRole.Mother, true, true));
            Assert.False(RHAH_FamilyRules.CanDrop(RHAH_PawnRole.Beggar, true, true));
            Assert.True(RHAH_FamilyRules.MarkDropped(dropped, 4));
            Assert.False(RHAH_FamilyRules.MarkDropped(dropped, 4));
            Assert.False(RHAH_FamilyRules.AllDropped(new[] { 4, 5 }, dropped));
            Assert.True(RHAH_FamilyRules.MarkDropped(dropped, 5));
            Assert.True(RHAH_FamilyRules.AllDropped(new[] { 4, 5 }, dropped));
            Assert.True(RHAH_FamilyRules.CanMotherFeed(RHAH_PawnRole.BeggarMother, true, true));
            Assert.False(RHAH_FamilyRules.CanScavenge(false, true, true));
            Assert.True(RHAH_FamilyRules.CanTailBite(true, true, true, true, 2.5f));
            Assert.False(RHAH_FamilyRules.CanTailBite(true, true, true, true, 3f));
            Assert.True(RHAH_FamilyRules.CanGiveFood(true, true));
            Assert.False(RHAH_FamilyRules.CanGiveFood(false, true));
            Assert.True(RHAH_FamilyRules.NaturalTail(true, false, false));
            Assert.False(RHAH_FamilyRules.NaturalTail(true, false, true));
            Assert.False(RHAH_FamilyRules.ScavengeFilth(true, false, true));
        }

        [Fact]
        public void BroadcastUsesOnlyEnabledEligibleIncidents()
        {
            HashSet<string> disabled = new HashSet<string> { "I-001" };
            List<string> candidates = RHAH_BroadcastRules.Candidates(RHAH_IncidentCatalog.All, disabled);
            Assert.DoesNotContain("I-001", candidates);
            Assert.DoesNotContain("I-014", candidates);
            Assert.Contains("I-011", candidates);
            Assert.NotEmpty(candidates);
            Assert.Equal(3, RHAH_BroadcastRules.ClampDays(3));
            Assert.Equal(10, RHAH_BroadcastRules.ClampDays(40));
            Assert.Contains("I-011", candidates);
            Assert.Equal("I-005", RHAH_BroadcastRules.Pick(candidates, 0f));
            Assert.Null(RHAH_BroadcastRules.Pick(candidates, RHAH_BroadcastRules.GroupCount(candidates)));
        }
    }
}
