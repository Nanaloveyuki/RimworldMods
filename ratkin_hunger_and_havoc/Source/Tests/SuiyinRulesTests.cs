using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class SuiyinRulesTests
    {
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

            SuiyinBook missing = new SuiyinBook();
            missing.AcceptExchange(5, 1, 0, new[] { 9 });
            missing.N005[0].Children[0].Presence = SuiyinPresence.Unknown;
            Assert.Equal(SuiyinN005Outcome.Accepted, missing.AdvanceCare(missing.N005[0], 100));
            Assert.Equal(SuiyinN005Outcome.Missing, missing.AdvanceCare(missing.N005[0], 100 + 60000));
            Assert.NotEqual(SuiyinN005Outcome.Dead, missing.N005[0].Outcome);
        }

        [Fact]
        public void JournalCountsAidOnceAfterSafeLeave()
        {
            SuiyinBook book = new SuiyinBook();
            Assert.Equal(14, SuiyinBook.JournalFor("I-015"));
            Assert.Equal(5, SuiyinBook.JournalFor("I-006"));
            Assert.True(book.OpenJournal(14, 1, 9, 0, new[] { 41 }, true));
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
    }
}
