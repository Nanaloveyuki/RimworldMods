using System.Collections.Generic;
using HungerAndHavoc.Storyteller.Suiyin;
using Verse;
using HungerAndHavoc.Core;

namespace HungerAndHavoc.Narrative
{
    public enum NarrativeOutcome
    {
        Unresolved = 0,
        Rescue = 1,
        Loss = 2,
        Exposure = 3,
        Failure = 4,
        Massacre = 5
    }

    public sealed class NarrativeSnapshot
    {
        public int RevealedCount { get; }
        public int Trust { get; }
        public int Rescued { get; }
        public int Lost { get; }
        public bool Failed { get; }

        public NarrativeSnapshot(int revealedCount, int trust, int rescued, int lost, bool failed)
        {
            RevealedCount = revealedCount;
            Trust = trust;
            Rescued = rescued;
            Lost = lost;
            Failed = failed;
        }
    }

    public sealed class NarrativeState : GameComponent
    {
        int revealedCount;
        int trust;
        int rescued;
        int lost;
        bool failed;
        List<bool> suiyinEnabled = new List<bool>();
        List<bool> suiyinStarted = new List<bool>();
        List<int> suiyinDeadlineTick = new List<int>();
        readonly SuiyinLedger suiyin = new SuiyinLedger();
        readonly SuiyinBook book = new SuiyinBook();
        KingdomArcState kingdom = new KingdomArcState();
        List<string> seenKinds = new List<string>();
        List<int> theftMaps = new List<int>();
        List<int> theftCounts = new List<int>();
        List<int> journalNoted = new List<int>();
        List<int> asidesSent = new List<int>();
        bool openingSent;
        bool progressSent;
        bool rewardClaimed;
        int rewardPaid;
        int rewardDue;
        bool envoyClue;
        bool relicClue;
        int lastAsideTick = -1;
        int nextCaseId = 1;
        int aidCount;
        int broadcastCount;
        int expulsionCount;
        // 退休结局的已发布输入继续存读 不再参与判定
        int adultCount;
        int completedKindCount;
        List<int> completedJournals = new List<int>();
        int firstFactTick = -1;
        int nextAdultCheckTick = -1;
        bool relicDone;
        bool endingE01;
        bool endingE02;
        bool endingE03;
        bool endingE04;
        bool endingE05;
        int identityTier;
        bool identityRefused;

        public NarrativeState(Game game)
        {
        }

        public void RecordRevelation()
        {
            revealedCount++;
        }

        public void RecordTrust(int amount)
        {
            trust = SuiyinNodes.ClampTrust(book.Trust + amount);
            book.Trust = trust;
        }

        public void RecordRescue()
        {
            rescued++;
        }

        public void RecordLoss()
        {
            lost++;
        }

        public void MarkFailure()
        {
            failed = true;
        }
        internal bool SuiyinAllows(SuiyinNode node, int tick)
        {
            return suiyin.Allows(node, tick);
        }

        internal bool SuiyinEnabled(SuiyinNode node)
        {
            return suiyin.Enabled(node);
        }

        internal void SetSuiyinEnabled(SuiyinNode node, bool enabled)
        {
            suiyin.SetEnabled(node, enabled);
        }

        internal void StartSuiyin(SuiyinNode node, int deadlineTick)
        {
            suiyin.Start(node, deadlineTick);
        }

        internal void NoteIncident(SuiyinIncidentFact fact)
        {
            if (fact.DisplayId == null)
            {
                return;
            }

            PullBook();
            book.Note(fact.DisplayId, fact.MapId, fact.Tick, true);
            PushBook();
            if (!string.IsNullOrEmpty(fact.DisplayId))
            {
                revealedCount = book.Distinct;
            }

            NoteFirstFact(fact.Tick);
        }


        internal void PullBook()
        {
            book.Gates = suiyin;
            book.Trust = trust;
        }

        internal void PushBook()
        {
            trust = book.Trust;
            rewardClaimed = book.RewardClaimed;
            if (book.RewardPaid > rewardPaid)
            {
                rewardDue += book.RewardPaid - rewardPaid;
                rewardPaid = book.RewardPaid;
            }
        }
        internal T Commit<T>(System.Func<SuiyinBook, T> change)
        {
            PullBook();
            T result = change(book);
            PushBook();
            return result;
        }
        internal void RestoreReward(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            rewardDue += amount;
        }


        internal int CompletedKindCount => completedKindCount;
        internal void NoteAid(int tick)
        {
            aidCount++;
            NoteFirstFact(tick);
        }

        internal void NoteBroadcast(int tick)
        {
            broadcastCount++;
            NoteFirstFact(tick);
        }

        internal void NoteExpulsion(int tick)
        {
            expulsionCount++;
            NoteFirstFact(tick);
        }

        internal bool NoteCompletedKind(int tick, int journal)
        {
            if (journal < 1 || journal > 14 || completedJournals.Contains(journal))
            {
                return false;
            }

            completedJournals.Add(journal);
            completedKindCount = completedJournals.Count;
            NoteFirstFact(tick);
            return true;
        }

        internal int TakeRewardDue()
        {
            int due = rewardDue;
            rewardDue = 0;
            return due;
        }

        internal void NoteRelicDone(int tick)
        {
            relicDone = true;
            NoteFirstFact(tick);
        }


        internal bool IdentityDue(bool narrator, bool enabled, int trustFloor, int hopeTrust)
        {
            if (!narrator || !enabled || !relicDone || identityRefused)
            {
                return false;
            }

            RHAH_IdentityTier offered = RHAH_EndingRules.IdentityOffer(trust, trustFloor, hopeTrust);
            return offered != RHAH_IdentityTier.None && identityTier < (int)offered;
        }

        internal void MarkIdentity(RHAH_IdentityTier tier, bool refused)
        {
            if ((int)tier > identityTier)
            {
                identityTier = (int)tier;
            }

            if (refused)
            {
                identityRefused = true;
            }
        }

        void NoteFirstFact(int tick)
        {
            if (firstFactTick < 0 || (tick >= 0 && tick < firstFactTick))
            {
                firstFactTick = tick;
            }
        }


        internal SuiyinBook Book => book;
        internal KingdomArcState Kingdom => kingdom;


        public NarrativeSnapshot Snapshot()
        {
            return new NarrativeSnapshot(revealedCount, trust, rescued, lost, failed);
        }

        public NarrativeOutcome CalculateOutcome()
        {
            return NarrativeOutcomeCalculator.Calculate(Snapshot());
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref revealedCount, "revealedCount", 0);
            Scribe_Values.Look(ref trust, "trust", 0);
            Scribe_Values.Look(ref rescued, "rescued", 0);
            Scribe_Values.Look(ref lost, "lost", 0);
            Scribe_Values.Look(ref failed, "failed", false);
            Scribe_Deep.Look(ref kingdom, "kingdomArc");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                kingdom = kingdom ?? new KingdomArcState();
            }
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                PullBook();
                PushBook();
                book.ExportSave(seenKinds, theftMaps, theftCounts, journalNoted, asidesSent);
                openingSent = book.OpeningSent;
                progressSent = book.ProgressSent;
                rewardClaimed = book.RewardClaimed;
                envoyClue = book.EnvoyClue;
                relicClue = book.RelicClue;
                lastAsideTick = book.LastAsideTick;
                nextCaseId = book.NextCaseId;
                suiyin.Export(suiyinEnabled, suiyinStarted, suiyinDeadlineTick);
            }

            Scribe_Collections.Look(ref seenKinds, "seenKinds", LookMode.Value);
            Scribe_Collections.Look(ref theftMaps, "theftMaps", LookMode.Value);
            Scribe_Collections.Look(ref theftCounts, "theftCounts", LookMode.Value);
            Scribe_Collections.Look(ref journalNoted, "journalNoted", LookMode.Value);
            Scribe_Collections.Look(ref asidesSent, "asidesSent", LookMode.Value);
            Scribe_Values.Look(ref openingSent, "openingSent", false);
            Scribe_Values.Look(ref progressSent, "progressSent", false);
            Scribe_Values.Look(ref rewardClaimed, "rewardClaimed", false);
            Scribe_Values.Look(ref rewardPaid, "rewardPaid", 0);
            Scribe_Values.Look(ref rewardDue, "rewardDue", 0);
            Scribe_Values.Look(ref envoyClue, "envoyClue", false);
            Scribe_Values.Look(ref relicClue, "relicClue", false);
            Scribe_Values.Look(ref lastAsideTick, "lastAsideTick", -1);
            Scribe_Values.Look(ref nextCaseId, "nextCaseId", 1);
            Scribe_Values.Look(ref aidCount, "aidCount", 0);
            Scribe_Values.Look(ref broadcastCount, "broadcastCount", 0);
            Scribe_Values.Look(ref expulsionCount, "expulsionCount", 0);
            Scribe_Values.Look(ref adultCount, "adultCount", 0);
            Scribe_Values.Look(ref completedKindCount, "completedKindCount", 0);
            Scribe_Collections.Look(ref completedJournals, "completedJournals", LookMode.Value);
            Scribe_Values.Look(ref firstFactTick, "firstFactTick", -1);
            Scribe_Values.Look(ref nextAdultCheckTick, "nextAdultCheckTick", -1);
            Scribe_Values.Look(ref relicDone, "relicDone", false);
            Scribe_Values.Look(ref endingE01, "endingE01", false);
            Scribe_Values.Look(ref endingE02, "endingE02", false);
            Scribe_Values.Look(ref endingE03, "endingE03", false);
            Scribe_Values.Look(ref endingE04, "endingE04", false);
            Scribe_Values.Look(ref endingE05, "endingE05", false);
            Scribe_Values.Look(ref identityTier, "identityTier", 0);
            Scribe_Values.Look(ref identityRefused, "identityRefused", false);

            Scribe_Collections.Look(ref suiyinEnabled, "suiyinEnabled", LookMode.Value);
            Scribe_Collections.Look(ref suiyinStarted, "suiyinStarted", LookMode.Value);
            Scribe_Collections.Look(ref suiyinDeadlineTick, "suiyinDeadlineTick", LookMode.Value);
            Scribe_Collections.Look(ref book.N004, "entrustCases", LookMode.Deep);
            Scribe_Collections.Look(ref book.N005, "exchangeCases", LookMode.Deep);
            Scribe_Collections.Look(ref book.N006, "holeCases", LookMode.Deep);
            Scribe_Collections.Look(ref book.N008, "envoyCases", LookMode.Deep);
            Scribe_Deep.Look(ref book.N009, "relicCase");
            Scribe_Collections.Look(ref book.Journals, "journalCases", LookMode.Deep);
            Scribe_Collections.Look(ref book.Pending, "pendingNotices", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                suiyinEnabled = suiyinEnabled ?? new List<bool>();
                theftMaps = theftMaps ?? new List<int>();
                theftCounts = theftCounts ?? new List<int>();
                journalNoted = journalNoted ?? new List<int>();
                asidesSent = asidesSent ?? new List<int>();
                completedJournals = completedJournals ?? new List<int>();
                suiyinStarted = suiyinStarted ?? new List<bool>();
                suiyinDeadlineTick = suiyinDeadlineTick ?? new List<int>();
                completedKindCount = completedJournals.Count;
                if (rewardDue < 0)
                {
                    rewardDue = 0;
                }

                suiyin.Import(suiyinEnabled, suiyinStarted, suiyinDeadlineTick);
                book.Gates = suiyin;
                book.Trust = trust;
                book.Narrator = true;
                book.OpeningSent = openingSent;
                book.ProgressSent = progressSent;
                book.RewardClaimed = rewardClaimed;
                book.RewardPaid = rewardPaid;
                book.EnvoyClue = envoyClue;
                book.RelicClue = relicClue;
                book.LastAsideTick = lastAsideTick;
                book.NextCaseId = nextCaseId < 1 ? 1 : nextCaseId;
                book.N004 = book.N004 ?? new System.Collections.Generic.List<SuiyinN004Case>();
                book.N005 = book.N005 ?? new System.Collections.Generic.List<SuiyinN005Case>();
                book.N006 = book.N006 ?? new System.Collections.Generic.List<SuiyinN006Case>();
                book.N008 = book.N008 ?? new System.Collections.Generic.List<SuiyinN008Case>();
                book.Journals = book.Journals ?? new System.Collections.Generic.List<SuiyinJournalCase>();
                book.Pending = book.Pending ?? new System.Collections.Generic.List<SuiyinNotice>();
                book.ImportSave(seenKinds, theftMaps, theftCounts, journalNoted, asidesSent);
            }
        }
    }

    internal static class NarrativeOutcomeCalculator
    {
        internal static NarrativeOutcome Calculate(NarrativeSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Failed)
            {
                return NarrativeOutcome.Failure;
            }

            if (snapshot.Lost > snapshot.Rescued && snapshot.Lost > 0)
            {
                return NarrativeOutcome.Massacre;
            }

            if (snapshot.RevealedCount >= 10 && snapshot.Trust >= 5)
            {
                return NarrativeOutcome.Exposure;
            }

            if (snapshot.Rescued > 0 && snapshot.Trust >= 0)
            {
                return NarrativeOutcome.Rescue;
            }

            return NarrativeOutcome.Unresolved;
        }
    }
}
