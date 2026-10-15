using System;
using System.Collections.Generic;
using System.Globalization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Api;
using HungerAndHavoc.EventMgr;
using Oxygendsl.Core;
using Oxygendsl.Core.Events;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_EventFollowTests : IDisposable
    {
        readonly Game previousGame;
        readonly RHAH_Settings previousSettings;

        static RHAH_EventFollowTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        public RHAH_EventFollowTests()
        {
            previousGame = Current.Game;
            previousSettings = RHAH_Mod.Settings;
            Current.Game = null;
            RHAH_Mod.Settings = null;
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            RHAH_Mod.Settings = previousSettings;
        }

        [Fact]
        public void PredatorSelectionRespectsProbabilityBoundary()
        {
            Assert.True(RHAH_EventFollowRules.PredatorSelected(10, 0.09f));
            Assert.False(RHAH_EventFollowRules.PredatorSelected(10, 0.1f));
            Assert.Equal(0, RHAH_EventFollowRules.Clamp(-4));
            Assert.Equal(100, RHAH_EventFollowRules.Clamp(140));
        }

        [Fact]
        public void EventClosesWhenTheLastYoungOutcomeFinishes()
        {
            Assert.False(RHAH_EventFollowRules.BatchOutcomeDone(1, 1));
            Assert.True(RHAH_EventFollowRules.BatchOutcomeDone(0, 2));
            Assert.False(RHAH_EventFollowRules.BatchOutcomeDone(0, 0));
        }

        [Fact]
        public void ProviderOwnsStableIncidentsInternalArrivalAndManualKingdomDefinitions()
        {
            RHAH_EventChainProvider provider = new RHAH_EventChainProvider();
            Assert.Equal("nanaloveyuki.ratkin.hungerandhavoc", provider.ProviderId);
            Assert.Equal(RHAH_EventChainProvider.ProviderKey, provider.ProviderId);
            HashSet<string> expected = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i <= 54; i++)
            {
                expected.Add("I-" + i.ToString("D3", CultureInfo.InvariantCulture));
            }

            expected.Add("RHAH_PredationArrival");
            expected.Add("RHAH_KingdomInvitation");
            expected.Add("RHAH_KingdomCollaboration");
            Assert.Equal("RHAH_PredationArrival", RHAH_EventChainProvider.ArrivalId);
            Assert.Equal(expected.Count, provider.Definitions.Count);
            for (int i = 0; i < provider.Definitions.Count; i++)
            {
                Assert.True(expected.Remove(provider.Definitions[i].Id));
            }

            Assert.Empty(expected);
        }

        [Fact]
        public void DocumentedLongParentsAndConcreteVariantsUseTheLongFollowRule()
        {
            HashSet<string> expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "I-002", "I-012", "I-013", "I-019", "I-032", "I-033", "I-037", "I-041", "I-038", "I-046", "I-047"
            };
            for (int i = 1; i <= 54; i++)
            {
                string id = "I-" + i.ToString("D3", CultureInfo.InvariantCulture);
                bool hasLong = expected.Contains(id);
                Assert.Equal(hasLong, RHAH_EventFollowRules.HasLongFollow(id));
                Assert.Equal(hasLong, RHAH_EventFollowRules.OpensLong(id, true));
                Assert.False(RHAH_EventFollowRules.OpensLong(id, false));
            }

            Assert.False(RHAH_EventFollowRules.HasLongFollow(RHAH_EventChainProvider.ArrivalId));
            Assert.False(RHAH_EventFollowRules.HasLongFollow("I-052"));
        }

        [Fact]
        public void ProviderPreparesATypedPayloadOnceAndReusesItForDecisions()
        {
            IEventChainDefinition definition = Definition("I-019");
            EventChainInstance instance = Follow("I-019", 7, 1, 5007);

            definition.Prepare(instance);
            object prepared = instance.PreparedState;
            IOxygenHostValue facts = Assert.IsAssignableFrom<IOxygenHostValue>(prepared);
            Assert.True(facts.TryGetMember("definition_id", out object displayId));
            Assert.Equal("I-019", displayId);
            Assert.True(facts.TryGetMember("batch_id", out object batchId));
            Assert.Equal(9, Convert.ToInt32(batchId, CultureInfo.InvariantCulture));
            Assert.True(facts.TryGetMember("site_id", out object siteId));
            Assert.Equal(4, Convert.ToInt32(siteId, CultureInfo.InvariantCulture));
            Assert.False(facts.TryGetMember("map", out object map));

            definition.Prepare(instance);
            Assert.Same(prepared, instance.PreparedState);
            definition.Evaluate(instance, 5007);
            definition.Evaluate(instance, 7507);
            Assert.Same(prepared, instance.PreparedState);
            Assert.True(facts.TryGetMember("stage", out object stage));
            Assert.Equal(1, Convert.ToInt32(stage, CultureInfo.InvariantCulture));
            Assert.True(facts.TryGetMember("tick", out object tick));
            Assert.Equal(7507, Convert.ToInt32(tick, CultureInfo.InvariantCulture));
            Assert.True(facts.TryGetMember("next_long_tick", out object nextLongTick));
            Assert.Equal(10007, Convert.ToInt32(nextLongTick, CultureInfo.InvariantCulture));
            Assert.True(facts.TryGetMember("long_ready", out object longReady));
            Assert.Equal(false, Assert.IsType<bool>(longReady));
            Assert.Equal("I-019|9|4", instance.Payload);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("I-002|0|4")]
        [InlineData("I-002|9|4|extra")]
        [InlineData("I-003|9|4")]
        [InlineData("I-002|9|5")]
        public void ProviderRejectsMalformedPayloadBeforeInstallingPreparedState(string payload)
        {
            EventChainInstance instance = Follow("I-002", 4, 0, 11);
            instance.Payload = payload;

            Assert.Throws<FormatException>(() => Definition("I-002").Prepare(instance));

            Assert.Null(instance.PreparedState);
            Assert.Equal(payload, instance.Payload);
        }

        [Fact]
        public void EmbeddedFollowDecisionSelectsTheShortActionThenLeavesDoneDormant()
        {
            IEventChainDefinition definition = Definition("I-002");
            EventChainInstance instance = Follow("I-002", 4, 0, 11);
            definition.Prepare(instance);

            EventChainDecision shortDecision = definition.Evaluate(instance, 11);
            Assert.Equal("roll_predator", shortDecision.ActionId);
            Assert.Equal(2, shortDecision.Stage);
            Assert.Equal(-1, shortDecision.NextTick);
            Assert.Equal(EventChainEnd.None, shortDecision.End);

            instance.Stage = shortDecision.Stage;
            instance.NextTick = shortDecision.NextTick;
            EventChainDecision done = definition.Evaluate(instance, 12);
            Assert.Null(done.ActionId);
            Assert.Equal(-1, done.NextTick);
            Assert.Equal(EventChainEnd.None, done.End);
        }

        [Fact]
        public void RealProviderShortActionIsReceiptedOnceByOxygenAndDoesNotReroll()
        {
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());
            runtime.Activate(10);
            int id = runtime.Start(RHAH_EventChainProvider.ProviderKey, "I-002", 4, 0, 10, -1, "I-002|9|4", 0, 11);
            Assert.True(id > 0);

            runtime.Tick(11);
            EventChainInstance instance = runtime.Find(RHAH_EventChainProvider.ProviderKey, id);
            Assert.Equal(2, instance.Stage);
            Assert.Equal(-1, instance.NextTick);
            Assert.Equal(EventChainEnd.None, instance.End);
            EventChainReceipt receipt = Assert.Single(instance.Receipts);
            Assert.Equal("roll_predator", receipt.ActionId);
            Assert.Equal("applied", receipt.Status);
            long callbacks = runtime.CallbackCount;
            Assert.True(callbacks > 0);

            runtime.Tick(12);
            runtime.Tick(2501);
            runtime.Tick(5001);

            Assert.Same(receipt, Assert.Single(instance.Receipts));
            Assert.Equal(callbacks, runtime.CallbackCount);
            Assert.Equal(2, instance.Stage);
        }

        [Fact]
        public void LongProviderWithoutGameFactsWaitsOnTheOriginalPhaseAcrossHours()
        {
            IEventChainDefinition definition = Definition("I-019");
            EventChainInstance instance = Follow("I-019", 7, 1, 5007);
            definition.Prepare(instance);

            EventChainDecision first = definition.Evaluate(instance, 5007);
            EventChainDecision later = definition.Evaluate(instance, 7507);

            Assert.Null(first.ActionId);
            Assert.Equal(7507, first.NextTick);
            Assert.Equal(EventChainEnd.None, first.End);
            Assert.Null(later.ActionId);
            Assert.Equal(10007, later.NextTick);
            Assert.Equal(EventChainEnd.None, later.End);
            Assert.Equal(1, instance.Stage);
            Assert.Equal(10, instance.StartedTick);
            Assert.Equal(100000, instance.DeadlineTick);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PublishedLongNotificationRedirectsIntoInteractiveStageWithoutCompleting(bool ready)
        {
            EventChainScript script = EventChainScript.Compile(RHAH_EventChainProvider.ReadScript("RHAH_EventFollow"));
            IOxygenHostValue snapshot = new DictionaryOxygenHostValue(new Dictionary<string, object>
            {
                { "stage", 1 },
                { "tick", 5007 },
                { "next_tick", 5007 },
                { "next_long_tick", 7507 },
                { "long_ready", ready },
                { "definition_id", "I-019" },
                { "batch_id", 9 },
                { "site_id", 4 }
            });

            EventChainDecision decision = script.Evaluate(snapshot);

            Assert.Equal(ready ? "notify_long" : null, decision.ActionId);
            Assert.Equal(7507, decision.NextTick);
            Assert.Equal(ready ? 3 : -1, decision.Stage);
            Assert.Equal(EventChainEnd.None, decision.End);
        }

        [Fact]
        public void EmbeddedArrivalDecisionWaitsUntilDueThenChoosesOnlyTheArrivalAction()
        {
            IEventChainDefinition definition = Definition(RHAH_EventChainProvider.ArrivalId);
            EventChainInstance instance = new EventChainInstance
            {
                Id = 12,
                DefinitionId = RHAH_EventChainProvider.ArrivalId,
                SiteId = 4,
                StartedTick = 10,
                NextTick = 130,
                Payload = "4"
            };
            definition.Prepare(instance);
            object prepared = instance.PreparedState;

            EventChainDecision before = definition.Evaluate(instance, 129);
            Assert.Null(before.ActionId);
            Assert.Equal(130, before.NextTick);
            Assert.Equal(EventChainEnd.None, before.End);
            EventChainDecision due = definition.Evaluate(instance, 130);
            Assert.Equal("arrive_predator", due.ActionId);
            Assert.Equal(2, due.Stage);
            Assert.Equal(-1, due.NextTick);
            Assert.Equal(EventChainEnd.None, due.End);
            Assert.Same(prepared, instance.PreparedState);
            Assert.Equal("4", instance.Payload);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("-4")]
        [InlineData("5")]
        [InlineData("I-002|9|4")]
        public void ArrivalProviderRejectsPayloadThatCannotIdentifyItsMap(string payload)
        {
            EventChainInstance instance = new EventChainInstance
            {
                Id = 12,
                DefinitionId = RHAH_EventChainProvider.ArrivalId,
                SiteId = 4,
                Payload = payload
            };

            Assert.Throws<FormatException>(() => Definition(RHAH_EventChainProvider.ArrivalId).Prepare(instance));

            Assert.Null(instance.PreparedState);
            Assert.Equal(payload, instance.Payload);
        }

        [Fact]
        public void ProviderRejectsActionsOutsideTheTypedStageWhitelist()
        {
            IEventChainDefinition definition = Definition("I-002");
            EventChainInstance instance = Follow("I-002", 4, 0, 11);
            definition.Prepare(instance);

            Assert.Equal("failed", definition.Apply(instance, 11, "arbitrary_method").Status);
            Assert.Equal("failed", definition.Apply(instance, 11, "notify_long").Status);
            Assert.Equal("failed", definition.Apply(instance, 11, "arrive_predator").Status);
            Assert.Equal(0, instance.Stage);
            Assert.Equal("I-002|9|4", instance.Payload);
            Assert.Empty(instance.Receipts);
        }

        [Fact]
        public void OnlyDocumentedIncidentsOpenALongFollow()
        {
            Assert.True(RHAH_EventFollowRules.OpensLong("I-002", true));
            Assert.True(RHAH_EventFollowRules.OpensLong("I-041", true));
            Assert.False(RHAH_EventFollowRules.OpensLong("I-002", false));
            Assert.False(RHAH_EventFollowRules.OpensLong("I-001", true));
            Assert.False(RHAH_EventFollowRules.OpensLong("I-051", true));
            Assert.Equal(10 + 15 * RHAH_EventFollowRules.TicksPerDay, RHAH_EventFollowRules.LongDeadline("I-037", 10));
            Assert.Equal(10 + 120 * RHAH_EventFollowRules.TicksPerDay, RHAH_EventFollowRules.LongDeadline("I-002", 10));
            Assert.Equal(-1, RHAH_EventFollowRules.LongDeadline("I-001", 10));
        }

        [Fact]
        public void PayloadRoundTripsBatchAndMap()
        {
            Assert.True(RHAH_EventFollowRules.ReadPayload(RHAH_EventFollowRules.Payload("I-012", 9, 4), out string id, out int batch, out int map));
            Assert.Equal("I-012", id);
            Assert.Equal(9, batch);
            Assert.Equal(4, map);
            Assert.False(RHAH_EventFollowRules.ReadPayload("I-012|0|4", out id, out batch, out map));
            Assert.False(RHAH_EventFollowRules.ReadPayload("", out id, out batch, out map));
        }

        [Fact]
        public void FourteenUsesTraitsBeforeTheCoinAndSkipsTheWrongPeople()
        {
            RHAH_FollowOutcome kind = RHAH_EventFollowRules.AtFourteen(Subject("I-002", 14f, true, kind: true), false, false, 0.9f);
            Assert.Equal(RHAH_EventFollowRules.MotherGoneBad, kind.Thought);
            Assert.Equal(RHAH_FollowMood.Permanent, kind.Duration);
            Assert.Equal(RHAH_FollowTrait.Kind, kind.Trait);

            RHAH_FollowOutcome twisted = RHAH_EventFollowRules.AtFourteen(Subject("I-003", 14f, true, twisted: true), false, false, 0.1f);
            Assert.Equal(RHAH_EventFollowRules.AteMotherFine, twisted.Thought);
            Assert.Equal(RHAH_FollowTrait.Cannibal, twisted.Trait);

            RHAH_FollowOutcome coin = RHAH_EventFollowRules.AtFourteen(Subject("I-002", 14f, true), false, false, 0.8f);
            Assert.Equal(RHAH_EventFollowRules.MotherGoneGood, coin.Thought);
            Assert.Equal(RHAH_FollowMood.Temporary, coin.Duration);
            Assert.False(RHAH_EventFollowRules.AtFourteen(Subject("I-002", 13.9f, true), false, false, 0f).Applies);
            Assert.False(RHAH_EventFollowRules.AtFourteen(Subject("I-002", 14f, true), true, true, 0f).Applies);
            Assert.False(RHAH_EventFollowRules.AtFourteen(Subject("I-013", 14f, true, gaveChild: true, food: true), false, false, 0f).Applies);
            Assert.False(RHAH_EventFollowRules.AtFourteen(Subject("I-007", 14f, true), false, false, 0f).Applies);
        }

        [Fact]
        public void ReliefBirthAndHarmKeepTheirGates()
        {
            Assert.Equal(RHAH_EventFollowRules.AidAgain, RHAH_EventFollowRules.OnRelief("I-015", true, true, false, false, 0.1f).Thought);
            Assert.False(RHAH_EventFollowRules.OnRelief("I-015", false, true, false, false, 0.1f).Applies);
            Assert.False(RHAH_EventFollowRules.OnRelief("I-017", true, true, false, false, 0.1f).Applies);
            Assert.False(RHAH_EventFollowRules.OnRelief("I-018", true, true, false, false, 0.1f).Applies);
            Assert.Equal(RHAH_EventFollowRules.SilverHard, RHAH_EventFollowRules.OnRelief("I-018", true, true, false, true, 0.9f).Thought);
            Assert.Equal(RHAH_EventFollowRules.BornAlive, RHAH_EventFollowRules.OnBirth("I-029", true, true, false, 3, true, false, 0.1f).Thought);
            Assert.False(RHAH_EventFollowRules.OnBirth("I-029", true, true, false, 2, false, false, 0.1f).Applies);
            Assert.Equal(RHAH_EventFollowRules.BornSick, RHAH_EventFollowRules.OnBirth("I-044", true, true, true, 1, false, false, 0.9f).Thought);
            Assert.Equal(RHAH_EventFollowRules.TraderSilent, RHAH_EventFollowRules.OnTraderPlague(true, true).Thought);
            Assert.False(RHAH_EventFollowRules.OnTraderPlague(true, false).Applies);
            Assert.False(RHAH_EventFollowRules.OnJoinedAfterHarm(Subject("I-006", 20f, true), 0f).Applies);
            Assert.True(RHAH_EventFollowRules.OnJoinedAfterHarm(Subject("I-006", 20f, true, joined: true, hurt: true), 0.2f).Applies);
        }

        [Fact]
        public void FamilyLinksOnlyTheMotherEvents()
        {
            Assert.True(RHAH_EventFollowRules.LinksFamily("I-004", 0, RHAH_PawnRole.BeggarMother));
            Assert.True(RHAH_EventFollowRules.LinksFamily("I-004", 2, RHAH_PawnRole.BeggarChild));
            Assert.False(RHAH_EventFollowRules.LinksFamily("I-003", 0, RHAH_PawnRole.RatkinYoung));
            Assert.False(RHAH_EventFollowRules.LinksFamily("I-002", 1, RHAH_PawnRole.BeggarChild));
        }

        static IEventChainDefinition Definition(string id)
        {
            RHAH_EventChainProvider provider = new RHAH_EventChainProvider();
            for (int i = 0; i < provider.Definitions.Count; i++)
            {
                if (provider.Definitions[i].Id == id)
                {
                    return provider.Definitions[i];
                }
            }

            throw new InvalidOperationException("Missing event definition: " + id);
        }

        static EventChainInstance Follow(string definitionId, int id, int stage, int nextTick)
        {
            return new EventChainInstance
            {
                Id = id,
                DefinitionId = definitionId,
                SiteId = 4,
                Stage = stage,
                StartedTick = 10,
                DeadlineTick = 100000,
                NextTick = nextTick,
                Payload = RHAH_EventFollowRules.Payload(definitionId, 9, 4)
            };
        }

        static RHAH_FollowSubject Subject(
            string id,
            float age,
            bool alive,
            bool kind = false,
            bool twisted = false,
            bool joined = false,
            bool hurt = false,
            bool gaveChild = false,
            bool food = false)
        {
            return new RHAH_FollowSubject(id, RHAH_PawnRole.BeggarChild, 1, 2, age, alive, true, joined, hurt, false, false, kind, twisted, false, true, true, gaveChild, food, false, false, false);
        }
    }
}
