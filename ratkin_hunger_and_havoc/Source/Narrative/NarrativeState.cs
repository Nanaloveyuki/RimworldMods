using System.Collections.Generic;
using HungerAndHavoc.Storyteller.Suiyin;
using Verse;

namespace HungerAndHavoc.Narrative
{
    public sealed class NarrativeSnapshot
    {
        public int Trust { get; }

        public NarrativeSnapshot(int trust)
        {
            Trust = trust;
        }
    }

    // 保留已发布组件类型和信任键 普通来客安置记录不属于剧情任务
    public sealed class NarrativeState : GameComponent
    {
        int trust;
        readonly SuiyinBook book = new SuiyinBook();
#if RIMWORLD_1_7
        bool careTrustEnabled;
        internal bool CareTrustEnabled => careTrustEnabled;
#else
        internal bool CareTrustEnabled => true;
#endif

        public NarrativeState(Game game)
        {
        }

        public void RecordTrust(int amount)
        {
            trust = SuiyinNodes.ClampTrust(book.Trust + amount);
            book.Trust = trust;
        }

        internal SuiyinBook Book => book;

        internal T Commit<T>(System.Func<SuiyinBook, T> change)
        {
            book.Trust = trust;
            T result = change(book);
            trust = book.Trust;
            return result;
        }

        public NarrativeSnapshot Snapshot()
        {
            return new NarrativeSnapshot(trust);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref trust, "trust", 0);
#if RIMWORLD_1_7
            // 只继承旧档已开启的安置信任结算 不恢复故事或新增开关
            if (Scribe.mode == LoadSaveMode.LoadingVars && Scribe.loader.curXmlParent?["careTrustEnabled"] == null)
            {
                Scribe_Values.Look(ref careTrustEnabled, "suiyinNarrativeForced", false);
            }
            else
            {
                Scribe_Values.Look(ref careTrustEnabled, "careTrustEnabled", false);
            }
#endif
            Scribe_Values.Look(ref book.NextCaseId, "nextCaseId", 1);
            Scribe_Collections.Look(ref book.N004, "entrustCases", LookMode.Deep);
            Scribe_Collections.Look(ref book.N005, "exchangeCases", LookMode.Deep);
            Scribe_Collections.Look(ref book.Journals, "journalCases", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                book.N004 = book.N004 ?? new List<SuiyinN004Case>();
                book.N005 = book.N005 ?? new List<SuiyinN005Case>();
                book.Journals = book.Journals ?? new List<SuiyinJournalCase>();
                book.Trust = trust;
            }
        }
    }
}
