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
                typeof(KingdomArcState), typeof(KingdomArcProgress), typeof(KingdomMaterialRecord),
                typeof(SuiyinN004Case), typeof(SuiyinN005Case), typeof(SuiyinN006Case),
                typeof(SuiyinN008Case), typeof(SuiyinN009Case), typeof(SuiyinJournalCase), typeof(SuiyinNotice)
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

        [Fact]
        public void IdentityDue_RequiresRelicAndConsumesTierWithoutChangingTrust()
        {
            NarrativeState state = new NarrativeState(null);
            state.RecordTrust(75);
            Assert.False(state.IdentityDue(true, true, 50, 75));

            state.NoteRelicDone(100);
            Assert.True(state.IdentityDue(true, true, 50, 75));
            state.MarkIdentity(RHAH_IdentityTier.Full, false);

            Assert.False(state.IdentityDue(true, true, 50, 75));
            Assert.Equal(75, state.Snapshot().Trust);
        }

        [Theory]
        [InlineData(49, 0)]
        [InlineData(50, 1)]
        [InlineData(75, 2)]
        public void IdentityOffer_UsesConfiguredTrustTiers(int trust, int expected)
        {
            Assert.Equal(expected, (int)RHAH_EndingRules.IdentityOffer(trust, 50, 75));
        }

        [Theory]
        [InlineData(80, 0, false, true)]
        [InlineData(-80, 0, false, false)]
        [InlineData(80, 2, false, false)]
        [InlineData(80, 0, true, false)]
        public void PublishedSave_PreservesRetiredInputsAndIdentityConsumption(int trust, int tier, bool refused, bool identityDue)
        {
            File.WriteAllText(path, "<record><trust>" + trust + "</trust>" +
                "<endingE01>True</endingE01><endingE02>False</endingE02><endingE03>True</endingE03>" +
                "<endingE04>False</endingE04><endingE05>True</endingE05><adultCount>123</adultCount>" +
                "<firstFactTick>700</firstFactTick><nextAdultCheckTick>900</nextAdultCheckTick>" +
                "<relicDone>True</relicDone><identityTier>" + tier + "</identityTier><identityRefused>" + refused + "</identityRefused></record>");
            NarrativeState loaded = Load();
            Assert.Equal(trust, loaded.Snapshot().Trust);
            Assert.Equal(trust, loaded.Book.Trust);
            Assert.Equal(identityDue, loaded.IdentityDue(true, true, 50, 75));
            Assert.False(loaded.IdentityDue(false, true, 50, 75));
            Assert.False(loaded.IdentityDue(true, false, 50, 75));

            Scribe.saver.InitSaving(path, "record");
            loaded.ExposeData();
            Scribe.saver.FinalizeSaving();
            XmlDocument saved = new XmlDocument();
            saved.Load(path);
            Assert.True(bool.Parse(saved.SelectSingleNode("/record/endingE01").InnerText));
            Assert.True(bool.Parse(saved.SelectSingleNode("/record/endingE03").InnerText));
            Assert.True(bool.Parse(saved.SelectSingleNode("/record/endingE05").InnerText));
            Assert.Equal("123", saved.SelectSingleNode("/record/adultCount").InnerText);
            Assert.Equal("700", saved.SelectSingleNode("/record/firstFactTick").InnerText);
            Assert.Equal("900", saved.SelectSingleNode("/record/nextAdultCheckTick").InnerText);

            NarrativeState reloaded = Load();
            Assert.Equal(trust, reloaded.Snapshot().Trust);
            Assert.Equal(trust, reloaded.Book.Trust);
            Assert.Equal(identityDue, reloaded.IdentityDue(true, true, 50, 75));
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
