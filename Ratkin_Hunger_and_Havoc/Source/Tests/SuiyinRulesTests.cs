using System;
using System.Reflection;
using System.Collections.Generic;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class SuiyinRulesTests
    {

        [Fact]
        public void DistinctKindsOpenLettersOnceAndPaySilverOnce()
        {
            SuiyinBook book = new SuiyinBook();
            book.Note("I-001", 1, 0, true);
            book.Note("I-001", 1, 10, true);
            Assert.Equal(1, book.Distinct);
            Assert.True(book.OpeningSent);
            Assert.False(book.ProgressSent);
            Assert.Equal(1, Count(book, SuiyinLetter.N001));

            book.TakeNotices(new List<SuiyinNotice>());
            book.Note("I-006", 1, 20, true);
            book.Note("I-013", 1, 30, true);
            Assert.True(book.ProgressSent);
            Assert.Equal(3, Arg(book, SuiyinLetter.N002));
            book.Note("I-013", 1, 40, true);
            Assert.Equal(1, Count(book, SuiyinLetter.N002));

            book.TakeNotices(new List<SuiyinNotice>());
            for (int i = 8; i <= 12; i++)
            {
                book.Note("I-" + i.ToString("000"), 1, i, true);
            }

            Assert.True(book.RewardClaimed);
            Assert.Equal(300, book.RewardPaid);
            Assert.Equal(300, Arg(book, SuiyinLetter.N003));
            book.TakeNotices(new List<SuiyinNotice>());
            book.Note("I-015", 1, 90, true);
            Assert.Equal(0, Count(book, SuiyinLetter.N003));
            Assert.Equal(300, book.RewardPaid);
        }

        [Fact]
        public void ChoiceTrustReturnsToTheNarrativeState()
        {
            NarrativeState state = new NarrativeState(null);
            state.RecordTrust(4);
            SuiyinN004Case record = new SuiyinN004Case();
            state.Book.N004.Add(record);
            record.Children.Add(new SuiyinMember { LoadId = 8, Child = false, Presence = SuiyinPresence.Here });
            Assert.Equal(SuiyinN004Outcome.FamilyHere, state.Commit(book => book.ResolveEntrust(record, 10, SuiyinPresence.Here)));
            Assert.Equal(9, state.Snapshot().Trust);
            Assert.Equal(9, state.Book.Trust);
        }


        [Fact]
        public void RewardDueMatchesTheLedgerOnce()
        {
            NarrativeState state = new NarrativeState(null);
            for (int i = 1; i <= 8; i++)
            {
                state.NoteIncident(new SuiyinIncidentFact("I-" + i.ToString("000"), 1, i * 10, i, 1));
            }

            Assert.Equal(300, state.Book.RewardPaid);
            Assert.Equal(300, state.TakeRewardDue());
            Assert.Equal(0, state.TakeRewardDue());
            state.NoteIncident(new SuiyinIncidentFact("I-009", 1, 90, 9, 1));
            Assert.Equal(0, state.TakeRewardDue());
        }

        [Fact]
        public void CompletedJournalKindsCountOnce()
        {
            NarrativeState state = new NarrativeState(null);
            Assert.True(state.NoteCompletedKind(10, 14));
            Assert.False(state.NoteCompletedKind(20, 14));
            Assert.True(state.NoteCompletedKind(30, 5));
            Assert.False(state.NoteCompletedKind(40, 0));
            Assert.Equal(2, state.CompletedKindCount);
        }


        [Fact]
        public void DisabledNodesDoNotSendPrivateLetters()
        {
            SuiyinBook book = new SuiyinBook();
            book.Gates = new SuiyinLedger();
            book.Gates.SetEnabled(SuiyinNode.N001, false);
            book.Gates.SetEnabled(SuiyinNode.N002, false);
            book.Note("I-001", 1, 0, true);
            book.Note("I-006", 1, 1, true);
            book.Note("I-008", 1, 2, true);
            Assert.False(book.OpeningSent);
            Assert.False(book.ProgressSent);
            Assert.Equal(0, Count(book, SuiyinLetter.N001));
        }

        [Fact]
        public void OtherNarratorsSkipPrivateLettersButKeepObjectiveOnes()
        {
            SuiyinBook book = new SuiyinBook();
            book.Narrator = false;
            book.Note("I-001", 1, 0, true);
            Assert.True(book.OpeningSent);
            Assert.Equal(0, Count(book, SuiyinLetter.N001));
            book.EnvoyClue = true;
            Assert.True(book.ArriveEnvoy(1, 41, 1));
            Assert.True(book.ChooseEnvoy(book.N008[0], SuiyinN008Action.Refuse, 2));
            Assert.Equal(1, Count(book, SuiyinLetter.N008Refuse));
        }

        [Fact]
        public void EntrustOutcomesStayExclusive()
        {
            SuiyinBook book = new SuiyinBook();
            Assert.True(book.OpenEntrust(7, 1, 0, new[] { 8 }));
            Assert.False(book.OpenEntrust(7, 1, 1, new[] { 8 }));
            SuiyinN004Case record = book.N004[0];
            record.Children[0].Child = false;
            Assert.Equal(SuiyinN004Outcome.FamilyHere, book.ResolveEntrust(record, 10, SuiyinPresence.Here));
            record.Children[0].Presence = SuiyinPresence.Left;
            Assert.Equal(SuiyinN004Outcome.FamilyHere, book.ResolveEntrust(record, 20, SuiyinPresence.Left));
            Assert.Equal(5, book.Trust);
            Assert.Equal(1, Count(book, SuiyinLetter.N004FamilyHere));

            SuiyinBook dead = new SuiyinBook();
            dead.OpenEntrust(1, 1, 0, new[] { 2 });
            dead.N004[0].Children[0].Presence = SuiyinPresence.Dead;
            Assert.Equal(SuiyinN004Outcome.Story, dead.ResolveEntrust(dead.N004[0], 10, SuiyinPresence.Dead));
            Assert.Equal(-2, dead.Trust);

            SuiyinBook captive = new SuiyinBook();
            captive.OpenEntrust(1, 1, 0, new[] { 2 });
            captive.N004[0].Children[0].Child = false;
            captive.N004[0].Children[0].Care = SuiyinCare.Captive;
            Assert.Equal(SuiyinN004Outcome.Captive, captive.ResolveEntrust(captive.N004[0], 10, SuiyinPresence.Dead));

            SuiyinBook alone = new SuiyinBook();
            alone.OpenEntrust(1, 1, 0, new[] { 2 });
            alone.N004[0].Children[0].Child = false;
            Assert.Equal(SuiyinN004Outcome.ChildAlone, alone.ResolveEntrust(alone.N004[0], 10, SuiyinPresence.Dead));

            SuiyinBook regret = new SuiyinBook();
            regret.OpenEntrust(1, 1, 0, new[] { 2 });
            regret.N004[0].Children[0].Presence = SuiyinPresence.Dead;
            Assert.Equal(SuiyinN004Outcome.Regret, regret.ResolveEntrust(regret.N004[0], 10, SuiyinPresence.Here));
        }

        [Fact]
        public void BanishedRescueCanBeAnsweredAfterMeetingDeadlineAndPaysOnce()
        {
            SuiyinBook book = new SuiyinBook();
            book.OpenEntrust(1, 1, 0, new[] { 2 });
            SuiyinN004Case record = book.N004[0];
            record.Children[0].Presence = SuiyinPresence.Left;
            Assert.Equal(SuiyinN004Outcome.Banished, book.ResolveEntrust(record, 100, SuiyinPresence.Left));
            record.RevisitSeen = true;
            int answerTick = record.RevisitDeadline + 1;
            Assert.False(book.ChooseRevisit(record, SuiyinN004Revisit.Rescue, answerTick, false));
            Assert.True(book.ChooseRevisit(record, SuiyinN004Revisit.Rescue, answerTick, true));
            Assert.False(book.ChooseRevisit(record, SuiyinN004Revisit.Kill, answerTick + 100, true));
            Assert.Equal(0, book.ClaimRescue(record, record.RescueDueTick - 1));
            Assert.Equal(2500, book.ClaimRescue(record, record.RescueDueTick));
            Assert.Equal(0, book.ClaimRescue(record, record.RescueDueTick + 10));
        }

        [Fact]
        public void CarePausesForMalnutritionResumesAndMissingIsNotDeath()
        {
            SuiyinBook book = new SuiyinBook();
            Assert.True(book.AcceptExchange(4, 1, 0, new[] { 9 }));
            Assert.False(book.AcceptExchange(4, 1, 1, new[] { 9 }));
            SuiyinN005Case record = book.N005[0];
            SuiyinMember child = record.Children[0];
            child.ObserveSettlement(60000, false);
            child.ObserveSettlement(120000, true);
            child.ObserveSettlement(180000, true);
            Assert.Equal(SuiyinN005Outcome.Accepted, book.AdvanceCare(record, 180000));
            Assert.Equal(60000, child.CareTicks);

            child.ObserveSettlement(240000, false);
            child.ObserveSettlement(360000, false);
            Assert.Equal(SuiyinN005Outcome.Accepted, book.AdvanceCare(record, 360000));
            Assert.Equal(180000, child.CareTicks);
            child.ObserveSettlement(360001, false);
            Assert.Equal(SuiyinN005Outcome.Cared, book.AdvanceCare(record, 360001));
            Assert.Equal(3, book.Trust);
            book.AdvanceCare(record, 360002);
            Assert.Equal(1, Count(book, SuiyinLetter.N005Cared));

            SuiyinBook missing = new SuiyinBook();
            missing.AcceptExchange(5, 1, 0, new[] { 9 });
            missing.N005[0].Children[0].Presence = SuiyinPresence.Unknown;
            Assert.Equal(SuiyinN005Outcome.Accepted, missing.AdvanceCare(missing.N005[0], 100));
            Assert.Equal(SuiyinN005Outcome.Missing, missing.AdvanceCare(missing.N005[0], 100 + 60000));
            Assert.NotEqual(SuiyinN005Outcome.Dead, missing.N005[0].Outcome);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(20)]
        public void SecondTheftOpensOneHoleAndSealUsesConfiguredCost(int cost)
        {
            HungerAndHavoc.Core.RHAH_Settings previous = HungerAndHavoc.Core.RHAH_Mod.Settings;
            HungerAndHavoc.Core.RHAH_Mod.Settings = new HungerAndHavoc.Core.RHAH_Settings { holeWoodCost = cost };
            try
            {
                SuiyinBook book = new SuiyinBook();
                Assert.False(book.NoteTheft(3, 0));
                Assert.True(book.NoteTheft(3, 10));
                Assert.False(book.NoteTheft(3, 20));
                SuiyinN006Case hole = book.N006[0];
                Assert.False(book.PlaceHole(hole));
                hole.FoodPresent = true;
                Assert.True(book.PlaceHole(hole));
                if (cost > 0)
                {
                    hole.Wood = cost - 1;
                    Assert.False(book.ChooseHole(hole, SuiyinN006Action.Seal, 30));
                }
                hole.Wood = cost;
                Assert.True(book.ChooseHole(hole, SuiyinN006Action.Seal, 30));
                Assert.Equal(0, hole.Wood);
                Assert.False(hole.Hole);
                Assert.False(book.ChooseHole(hole, SuiyinN006Action.Clean, 40));
            }
            finally
            {
                HungerAndHavoc.Core.RHAH_Mod.Settings = previous;
            }
        }

        [Fact]
        public void HoleExpiresWithoutAnEntityAndDestroyedBaitDoesNotStayOpen()
        {
            SuiyinBook book = new SuiyinBook();
            SuiyinN006Case hole = new SuiyinN006Case { Outcome = SuiyinN006Outcome.Pending, Hole = false, IgnoreUntil = 100 };
            Assert.False(book.ChooseHole(hole, SuiyinN006Action.Ignore, 99));
            Assert.True(book.ChooseHole(hole, SuiyinN006Action.Ignore, 100));
            Assert.Equal(SuiyinN006Outcome.Gone, hole.Outcome);
            SuiyinN006Case bait = new SuiyinN006Case { Outcome = SuiyinN006Outcome.BaitSet, Hole = true, BaitUntil = 200 };
            Assert.True(book.LoseHole(bait));
            Assert.Equal(SuiyinN006Outcome.Gone, bait.Outcome);
            Assert.False(bait.Hole);
            Assert.False(book.FinishBait(bait, 200, true));
        }



        [Fact]
        public void PositiveTrustRaisesNarrativeSilverByAtMostAQuarter()
        {
            SuiyinBook book = new SuiyinBook();
            book.Trust = 100;
            Assert.Equal(375, book.Scaled(300));
            book.Trust = 0;
            Assert.Equal(300, book.Scaled(300));
            book.Trust = -40;
            Assert.Equal(300, book.Scaled(300));
            book.OpenRelic(0);
            book.Trust = 100;
            Assert.Equal(250, book.ChooseRelic(SuiyinN009Action.Take, 10));
            Assert.Equal(0, book.ChooseRelic(SuiyinN009Action.Take, 20));
        }


        [Fact]
        public void AsidesFireOnceAndStopWhenTrustIsClosed()
        {
            SuiyinBook book = new SuiyinBook();
            book.OpenEntrust(1, 1, 0, new[] { 2 });
            book.N004[0].Children[0].Child = false;
            book.ResolveEntrust(book.N004[0], 10, SuiyinPresence.Here);
            book.AcceptExchange(3, 1, 20, new[] { 4 });
            Assert.Equal(1, Count(book, SuiyinLetter.Aside));
            book.TakeNotices(new List<SuiyinNotice>());
            book.Trust = -75;
            book.TryAsideForTest(20 + 60000 * 4);
            Assert.Equal(0, Count(book, SuiyinLetter.Aside));
        }

        [Fact]
        public void OldArrivalNoticesAreDiscardedWithoutLosingChoicesOrRewards()
        {
            SuiyinBook book = new SuiyinBook();
            book.Pending.Add(new SuiyinNotice(SuiyinLetter.Journal, 14, true));
            book.Pending.Add(new SuiyinNotice(SuiyinLetter.N004Entry, 7, true));
            book.Pending.Add(new SuiyinNotice(SuiyinLetter.N006Entry, 0, false));
            book.Pending.Add(new SuiyinNotice(SuiyinLetter.N008Arrive, 41, false));
            book.Pending.Add(new SuiyinNotice(SuiyinLetter.N008Refuse, 41, false));
            book.Pending.Add(new SuiyinNotice(SuiyinLetter.N003, 300, true));
            List<SuiyinNotice> notices = new List<SuiyinNotice>();

            Assert.Equal(2, book.TakeNotices(notices));
            Assert.Equal(SuiyinLetter.N008Refuse, notices[0].Letter);
            Assert.Equal(SuiyinLetter.N003, notices[1].Letter);
            Assert.Equal(300, notices[1].Arg);
            Assert.Empty(book.Pending);
            Assert.Equal(0, book.TakeNotices(new List<SuiyinNotice>()));
        }

        [Fact]
        public void JournalCountsAidOnceAfterSafeLeave()
        {
            SuiyinBook book = new SuiyinBook();
            Assert.Equal(14, SuiyinBook.JournalFor("I-015"));
            Assert.Equal(5, SuiyinBook.JournalFor("I-006"));
            Assert.True(book.OpenJournal(14, 1, 9, 0, new[] { 41 }, true));
            Assert.Empty(book.Pending);
            Assert.False(book.OpenJournal(14, 1, 9, 1, new[] { 41 }, true));
            SuiyinJournalCase record = book.Journals[0];
            Assert.False(book.CloseJournal(record, 1000));
            record.People[0].Presence = SuiyinPresence.Left;
            Assert.True(book.CloseJournal(record, 2000));
            Assert.True(record.Counted);
            Assert.False(book.CloseJournal(record, 3000));
        }

        [Fact]
        public void JournalIgnoresNaturalDeathAndFailsOnPlayerKill()
        {
            SuiyinBook book = new SuiyinBook();
            Assert.True(book.OpenJournal(14, 1, 9, 0, new[] { 41, 42 }, false));
            SuiyinJournalCase record = book.Journals[0];
            record.People[0].Presence = SuiyinPresence.Dead;
            record.People[1].Presence = SuiyinPresence.Left;
            Assert.False(book.CloseJournal(record, 1000));
            Assert.True(record.Closed);
            Assert.False(record.Counted);

            SuiyinBook promised = new SuiyinBook();
            Assert.True(promised.OpenJournal(14, 1, 12, 0, new[] { 46, 47 }, false));
            Assert.True(promised.MarkJournalDelivered(12));
            promised.Journals[0].People[0].Presence = SuiyinPresence.Dead;
            promised.Journals[0].People[1].Presence = SuiyinPresence.Left;
            Assert.True(promised.CloseJournal(promised.Journals[0], 2000));
            Assert.True(promised.Journals[0].Counted);

            SuiyinBook empty = new SuiyinBook();
            Assert.True(empty.OpenJournal(14, 1, 10, 0, new[] { 43 }, true));
            empty.Journals[0].People[0].Presence = SuiyinPresence.Dead;
            Assert.False(empty.CloseJournal(empty.Journals[0], 1000));
            Assert.True(empty.Journals[0].Closed);
            Assert.True(empty.Journals[0].Empty);
            Assert.False(empty.Journals[0].Counted);

            SuiyinBook killed = new SuiyinBook();
            Assert.True(killed.OpenJournal(14, 1, 11, 0, new[] { 44, 45 }, true));
            Assert.True(killed.MarkJournalFailed(11));
            Assert.False(killed.CloseJournal(killed.Journals[0], 1000));
            killed.Journals[0].People[1].Presence = SuiyinPresence.Left;
            Assert.False(killed.CloseJournal(killed.Journals[0], 2000));
            Assert.False(killed.Journals[0].Counted);
            Assert.False(killed.MarkJournalFailed(11));
        }

        [Fact]
        public void InterceptionFailsAPromisedJournalOnce()
        {
            SuiyinBook book = new SuiyinBook();
            book.Trust = 10;
            Assert.True(book.OpenJournal(14, 1, 9, 0, new[] { 41, 42 }, true));
            book.Journals[0].People[0].Presence = SuiyinPresence.Left;
            book.Journals[0].People[1].Presence = SuiyinPresence.Left;
            Assert.False(book.OpenInterception(7, 9, 100, new[] { 41, 42 }));
            Assert.False(book.FailInterception(9));
            Assert.Equal(10, book.Trust);
            Assert.Equal(14, book.Journals[0].Id);
            Assert.False(book.Journals[0].Failed);
            Assert.False(book.Journals[0].Closed);
            Assert.True(book.CloseJournal(book.Journals[0], 2000));
            Assert.True(book.Journals[0].Counted);
            Assert.Equal(10, book.Trust);

            SuiyinBook intercepted = new SuiyinBook();
            intercepted.Trust = 10;
            Assert.True(intercepted.OpenInterception(7, 21, 100, new[] { 51, 52 }));
            Assert.Equal(0, intercepted.Journals[0].Id);
            Assert.True(intercepted.Journals[0].Failed);
            Assert.False(intercepted.Journals[0].Closed);
            Assert.False(intercepted.Journals[0].Counted);
            Assert.Equal(SuiyinPresence.Left, intercepted.Journals[0].People[0].Presence);
            Assert.Equal(10, intercepted.Trust);
            Assert.Empty(intercepted.Pending);
            Assert.Empty(intercepted.JournalNoted);
            Assert.False(intercepted.CloseJournal(intercepted.Journals[0], 2000));
            Assert.False(intercepted.Journals[0].Closed);
            Assert.False(intercepted.Journals[0].Counted);
            Assert.Equal(10, intercepted.Trust);
            Assert.True(intercepted.FailInterception(21));
            Assert.Equal(8, intercepted.Trust);
            Assert.True(intercepted.Journals[0].Closed);
            Assert.False(intercepted.Journals[0].Counted);
            Assert.False(intercepted.FailInterception(21));
            Assert.Equal(8, intercepted.Trust);
            Assert.False(intercepted.OpenInterception(7, 21, 300, new[] { 51 }));
            Assert.False(intercepted.OpenInterception(7, 0, 300, new[] { 51 }));
            Assert.False(intercepted.OpenInterception(7, 22, 300, new int[0]));
            Assert.Single(intercepted.Journals);

            SuiyinBook missing = new SuiyinBook();
            missing.Trust = 10;
            Assert.False(missing.FailInterception(0));
            Assert.False(missing.FailInterception(9));
            Assert.Equal(10, missing.Trust);
            Assert.Empty(missing.Journals);
            Assert.Empty(missing.Pending);
        }



        [Fact]
        public void SavedClaimsDoNotReset()
        {
            SuiyinBook book = new SuiyinBook();
            book.Note("I-001", 1, 0, true);
            book.RewardClaimed = true;
            book.RewardPaid = 300;
            book.N009 = new SuiyinN009Case { StartedTick = 10, Deadline = 900000, Outcome = SuiyinN009Outcome.Taken };
            SuiyinBook loaded = Copy(book);
            loaded.Note("I-002", 1, 50, true);
            Assert.True(loaded.RewardClaimed);
            Assert.Equal(300, loaded.RewardPaid);
            Assert.Equal(900000, loaded.N009.Deadline);
            Assert.Equal(SuiyinN009Outcome.Taken, loaded.N009.Outcome);
        }

        [Fact]
        public void ProofStillAllowsOneTrade()
        {
            SuiyinBook book = new SuiyinBook();
            for (int i = 1; i <= 5; i++)
            {
                book.Note("I-" + i.ToString("000"), 1, i, true);
            }

            Assert.True(book.ArriveEnvoy(4, 9, 10));
            SuiyinN008Case envoy = book.N008[0];
            envoy.ProofAvailable = true;
            Assert.True(book.ChooseEnvoy(envoy, SuiyinN008Action.Proof, 20));
            Assert.Equal(SuiyinN008Outcome.Checking, envoy.Outcome);
            envoy.MealsReady = false;
            Assert.False(book.ChooseEnvoy(envoy, SuiyinN008Action.Trade, 30));
            envoy.MealsReady = true;
            envoy.Deadline = 30;
            Assert.True(book.ChooseEnvoy(envoy, SuiyinN008Action.Trade, envoy.CheckUntil - 1));
            Assert.Equal(SuiyinN008Outcome.Traded, envoy.Outcome);
            Assert.True(book.RelicClue);
        }

        [Fact]
        public void EnvoyCheckDeadlineAndPresenceRejectUnfulfillableChoices()
        {
            SuiyinBook book = new SuiyinBook();
            SuiyinN008Case envoy = new SuiyinN008Case
            {
                Outcome = SuiyinN008Outcome.Checking,
                CheckUntil = 100,
                Deadline = 50,
                MealsReady = true,
                Presence = SuiyinPresence.Here
            };
            Assert.False(book.ChooseEnvoy(envoy, SuiyinN008Action.Proof, 60));
            envoy.Presence = SuiyinPresence.Left;
            Assert.False(book.ChooseEnvoy(envoy, SuiyinN008Action.Trade, 60));
            envoy.Presence = SuiyinPresence.Here;
            Assert.False(book.ChooseEnvoy(envoy, SuiyinN008Action.Trade, 100));
            Assert.True(book.FinishEnvoy(envoy, 100));
            Assert.Equal(SuiyinN008Outcome.TimedOut, envoy.Outcome);
        }

        [Fact]
        public void NarrativeScansUseSeparateSlots()
        {
            Assert.True(RHAH_NarrativePace.Due(250, 2500, 250));
            Assert.False(RHAH_NarrativePace.Due(250, 2500, 500));
            Assert.True(RHAH_NarrativePace.Due(500, 2500, 500));
            Assert.False(RHAH_NarrativePace.Due(0, 2500, 250));
        }

        [Fact]
        public void JournalKeepsRealPawnIds()
        {
            SuiyinBook book = new SuiyinBook();
            Assert.True(book.OpenJournal(14, 1, 9, 0, new[] { 41, 42 }, true));
            Assert.Equal(41, book.Journals[0].People[0].LoadId);
            Assert.Equal(42, book.Journals[0].People[1].LoadId);
            Assert.False(book.OpenJournal(14, 1, 9, 1, new[] { 41 }, true));
            Assert.False(book.OpenJournal(14, 1, 10, 1, new int[0], true));
        }
        static SuiyinBook Copy(SuiyinBook source)
        {
            SuiyinBook copy = new SuiyinBook();
            copy.Seen.AddRange(source.Seen);
            copy.OpeningSent = source.OpeningSent;
            copy.ProgressSent = source.ProgressSent;
            copy.RewardClaimed = source.RewardClaimed;
            copy.RewardPaid = source.RewardPaid;
            copy.Trust = source.Trust;
            if (source.N009 != null)
            {
                copy.N009 = new SuiyinN009Case
                {
                    StartedTick = source.N009.StartedTick,
                    Deadline = source.N009.Deadline,
                    Outcome = source.N009.Outcome
                };
            }

            return copy;
        }

        static int Count(SuiyinBook book, SuiyinLetter letter)
        {
            int count = 0;
            for (int i = 0; i < book.Pending.Count; i++)
            {
                if (book.Pending[i].Letter == letter)
                {
                    count++;
                }
            }

            return count;
        }

        static int Arg(SuiyinBook book, SuiyinLetter letter)
        {
            for (int i = 0; i < book.Pending.Count; i++)
            {
                if (book.Pending[i].Letter == letter)
                {
                    return book.Pending[i].Arg;
                }
            }

            return -1;
        }
    }
}
