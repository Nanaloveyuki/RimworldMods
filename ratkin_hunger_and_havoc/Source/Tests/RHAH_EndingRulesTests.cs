using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Xml;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_EndingRulesTests : IDisposable
    {
        readonly ScribeSaver previousSaver = Scribe.saver;
        readonly ScribeLoader previousLoader = Scribe.loader;
        readonly LoadSaveMode previousMode = Scribe.mode;
        readonly bool previousProfiling = DeepProfiler.enabled;
        readonly object previousPrefs = PrefsField.GetValue(null);
        readonly IDictionary previousTypeCache = (IDictionary)TypeCacheField.GetValue(null);
        readonly string path = Path.GetTempFileName();
        static FieldInfo PrefsField => typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
        static FieldInfo TypeCacheField => typeof(GenTypes).GetField("typeCache", BindingFlags.Static | BindingFlags.NonPublic);

        public RHAH_EndingRulesTests()
        {
            Scribe.saver = new ScribeSaver();
            Scribe.loader = new ScribeLoader();
            Scribe.mode = LoadSaveMode.Inactive;
            DeepProfiler.enabled = false;
            PrefsField.SetValue(null, new PrefsData { devMode = false });
            // 独立测试没有活动模组程序集；全名解析不查询 AllTypes，仅登记实际深存档图
            Type[] serializedTypes =
            {
                typeof(SuiyinN004Case), typeof(SuiyinN005Case),
                typeof(SuiyinJournalCase), typeof(SuiyinMember)
            };
            IDictionary typeCache = (IDictionary)Activator.CreateInstance(previousTypeCache.GetType());
            foreach (DictionaryEntry entry in previousTypeCache) typeCache.Add(entry.Key, entry.Value);
            Type keyType = typeof(GenTypes).GetNestedType("TypeCacheKey", BindingFlags.NonPublic);
            foreach (Type type in serializedTypes)
            {
                object key = Activator.CreateInstance(keyType, new object[] { type.FullName, null });
                typeCache[key] = type;
            }
            TypeCacheField.SetValue(null, typeCache);
        }

        public void Dispose()
        {
            Scribe.ForceStop();
            Scribe.saver = previousSaver;
            Scribe.loader = previousLoader;
            Scribe.mode = previousMode;
            DeepProfiler.enabled = previousProfiling;
            PrefsField.SetValue(null, previousPrefs);
            TypeCacheField.SetValue(null, previousTypeCache);
            File.Delete(path);
        }

        [Fact]
        public void ThreatFactor_MatchesTrustAnchors()
        {
            Assert.Equal(1.25f, RHAH_EndingRules.ThreatFactor(-100));
            Assert.Equal(1f, RHAH_EndingRules.ThreatFactor(0));
            Assert.Equal(0.75f, RHAH_EndingRules.ThreatFactor(100));
        }

        [Theory]
        [InlineData(-80)]
        [InlineData(80)]
        public void PublishedSaveKeepsTrustAndDiscardsStoryProgress(int trust)
        {
            File.WriteAllText(path, "<record><trust>" + trust + "</trust>" +
                "<suiyinNarrativeForced>True</suiyinNarrativeForced><kingdomArc><invitation>Accepted</invitation></kingdomArc>" +
                "<relicCase><outcome>Pending</outcome></relicCase><rewardDue>300</rewardDue><pendingNotices><li><letter>N003</letter></li></pendingNotices></record>");
            NarrativeState loaded = Load();
            Assert.Equal(trust, loaded.Snapshot().Trust);
            Assert.True(loaded.CareTrustEnabled);
            loaded.RecordTrust(10);
            Scribe.saver.InitSaving(path, "record");
            loaded.ExposeData();
            Scribe.saver.FinalizeSaving();
            XmlDocument saved = new XmlDocument();
            saved.Load(path);
            Assert.Null(saved.SelectSingleNode("/record/kingdomArc"));
            Assert.Null(saved.SelectSingleNode("/record/relicCase"));
            Assert.Null(saved.SelectSingleNode("/record/rewardDue"));
            Assert.Null(saved.SelectSingleNode("/record/pendingNotices"));
            Assert.Null(saved.SelectSingleNode("/record/suiyinNarrativeForced"));
            Assert.True(Load().CareTrustEnabled);
            Assert.Equal(trust + 10, Load().Snapshot().Trust);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("<suiyinNarrativeForced>False</suiyinNarrativeForced>", false)]
        [InlineData("<suiyinNarrativeForced>True</suiyinNarrativeForced>", true)]
        [InlineData("<careTrustEnabled>False</careTrustEnabled><suiyinNarrativeForced>True</suiyinNarrativeForced>", false)]
        [InlineData("<careTrustEnabled>True</careTrustEnabled><suiyinNarrativeForced>False</suiyinNarrativeForced>", true)]
        public void TrustPolicyKeepsThePublishedEnablementAndNewKeyTakesPrecedence(string keys, bool enabled)
        {
            File.WriteAllText(path, "<record><trust>64</trust>" + keys + "</record>");
            NarrativeState loaded = Load();
#if RIMWORLD_1_7
            Assert.Equal(enabled, loaded.CareTrustEnabled);
#else
            Assert.True(loaded.CareTrustEnabled);
#endif
            Assert.Equal(64, loaded.Snapshot().Trust);
            Scribe.saver.InitSaving(path, "record");
            loaded.ExposeData();
            Scribe.saver.FinalizeSaving();
            Assert.Equal(loaded.CareTrustEnabled, Load().CareTrustEnabled);
        }

        [Theory]
        [InlineData(false, -1, false)]
        [InlineData(true, -1, true)]
        [InlineData(true, 99, true)]
        public void DisabledCareNodesKeepTheirPublishedBehavior(bool started, int deadline, bool allowed)
        {
            File.WriteAllText(path, "<record><trust>64</trust><suiyinNarrativeForced>True</suiyinNarrativeForced>" +
                "<suiyinEnabled><li>True</li><li>True</li><li>True</li><li>False</li><li>False</li></suiyinEnabled>" +
                "<suiyinStarted><li>False</li><li>False</li><li>False</li><li>" + started + "</li><li>" + started + "</li></suiyinStarted>" +
                "<suiyinDeadlineTick><li>-1</li><li>-1</li><li>-1</li><li>" + deadline + "</li><li>" + deadline + "</li></suiyinDeadlineTick></record>");
            NarrativeState loaded = Load();
            Assert.Equal(allowed, loaded.Book.OpenEntrust(7, 1, 100, new[] { 8 }));
            Assert.Equal(allowed, loaded.Book.AcceptExchange(4, 1, 100, new[] { 9 }));
            Assert.Equal(64, loaded.Snapshot().Trust);
            Scribe.saver.InitSaving(path, "record");
            loaded.ExposeData();
            Scribe.saver.FinalizeSaving();
            NarrativeState again = Load();
            Assert.Equal(allowed, again.Book.OpenEntrust(17, 1, 100, new[] { 18 }));
            Assert.Equal(allowed, again.Book.AcceptExchange(14, 1, 100, new[] { 19 }));
        }

        [Fact]
        public void NewCarePolicyTakesPrecedenceWithoutReopeningCompletedCases()
        {
            File.WriteAllText(path, "<record><trust>64</trust><careEntrustEnabled>False</careEntrustEnabled><careExchangeEnabled>False</careExchangeEnabled>" +
                "<entrustCases><li><id>7</id><motherId>8</motherId><outcome>FamilyHere</outcome><effectsApplied>True</effectsApplied></li></entrustCases>" +
                "<exchangeCases><li><id>9</id><outcome>Cared</outcome><careClosed>True</careClosed></li></exchangeCases></record>");
            NarrativeState loaded = Load();
            Assert.False(loaded.Book.OpenEntrust(17, 1, 100, new[] { 18 }));
            Assert.False(loaded.Book.AcceptExchange(19, 1, 100, new[] { 20 }));
            Assert.Equal(SuiyinN004Outcome.FamilyHere, loaded.Commit(book => book.ResolveEntrust(book.N004[0], 100, SuiyinPresence.Dead)));
            Assert.Equal(SuiyinN005Outcome.Cared, loaded.Commit(book => book.AdvanceCare(book.N005[0], 100)));
            Assert.Equal(64, loaded.Snapshot().Trust);
        }

        [Fact]
        public void ConfirmedDepartureFactsSurviveSaveLoadWithoutRetainingPawnReferences()
        {
            NarrativeState state = new NarrativeState(null);
            state.Book.Journals.Add(new SuiyinJournalCase
            {
                Id = 14, MapId = 0, Delivered = true,
                People = { new SuiyinMember { LoadId = 21 }, new SuiyinMember { LoadId = 22 } }
            });
            state.Book.N004.Add(new SuiyinN004Case
            {
                MotherId = 21, MapId = 0,
                Children = { new SuiyinMember { LoadId = 21, Child = true } }
            });
            state.Book.N005.Add(new SuiyinN005Case
            {
                MapId = 0, Outcome = SuiyinN005Outcome.Accepted,
                Children = { new SuiyinMember { LoadId = 21 } }
            });
            Assert.True(state.Book.NoteExit(21, 0));
            Scribe.saver.InitSaving(path, "record");
            state.ExposeData();
            Scribe.saver.FinalizeSaving();

            NarrativeState loaded = Load();
            SuiyinMember left = loaded.Book.Journals[0].People[0];
            SuiyinMember unavailable = loaded.Book.Journals[0].People[1];
            left.ObservePresence(null, 0, 60000);
            unavailable.ObservePresence(null, 0, 60000);
            Assert.Equal(SuiyinPresence.Left, left.Presence);
            Assert.Equal(-1, left.MissingSince);
            Assert.Equal(SuiyinPresence.Unknown, unavailable.Presence);
            Assert.Equal(SuiyinPresence.Left, loaded.Book.N004[0].Mother);
            Assert.Equal(SuiyinPresence.Left, loaded.Book.N004[0].Children[0].Presence);
            SuiyinMember child = loaded.Book.N005[0].Children[0];
            child.ObservePresence(null, 0, 60000);
            Assert.Equal(SuiyinPresence.Left, child.Presence);
            Assert.Equal(SuiyinN005Outcome.Left, loaded.Book.AdvanceCare(loaded.Book.N005[0], 60000));
            Assert.Equal(0, loaded.Snapshot().Trust);
        }

        NarrativeState Load()
        {
            Scribe.loader.InitLoading(path);
            NarrativeState loaded = new NarrativeState(null);
            Scribe.loader.curParent = loaded;
            loaded.ExposeData();
            Scribe.loader.initer.RegisterForPostLoadInit(loaded);
            Scribe.loader.FinalizeLoading();
            return loaded;
        }
    }
}