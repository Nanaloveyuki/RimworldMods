#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using Xunit;

namespace LeadYourPet.Tests
{
    [Collection("Game state")]
    public class ThoughtVoiceTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        private readonly List<ModContentPack> activeMods = LoadedModManager.RunningModsListForReading;
        private readonly List<ModContentPack> previousMods;
        private readonly ModContentPack ratkin = Pack("Solaris.RatkinRaceMod");
        private readonly ModContentPack kiiro = Pack("Ancot.KiiroRace");
        private readonly LoadedLanguage language = Raw<LoadedLanguage>();
        private readonly ThoughtDef def = new ThoughtDef { defName = "LeadYourPet_MouseEggFearMood" };

        public ThoughtVoiceTests()
        {
            previousMods = new List<ModContentPack>(activeMods);
            activeMods.Clear();
            activeMods.Add(ratkin);
            activeMods.Add(kiiro);
            language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            typeof(LoadedLanguage).GetField("dataIsLoaded", Fields).SetValue(language, true);
            LanguageDatabase.activeLanguage = language;
            Add("LeadYourPet_ThoughtRace_RatkinYoung", "young-ratkin");
            Add("LeadYourPet_ThoughtRace_RatkinAdult", "adult-ratkin");
            Add("LeadYourPet_ThoughtRace_KiiroYoung", "young-kiiro");
            Add("LeadYourPet_ThoughtRace_KiiroAdult", "adult-kiiro");
            Add("LeadYourPet_ThoughtRace_Unknown", "unknown");
            foreach (string voice in new[] { "Generic", "RatkinYoung", "RatkinAdult", "KiiroYoung", "KiiroAdult" })
                Add(def.defName + "_" + voice + "_Description", voice + ":{Race}");
        }

        [Theory]
        [InlineData("Ratkin", 0, "Kiiro", 30, "RatkinYoung:adult-kiiro")]
        [InlineData("Ratkin", 13.999, "Kiiro", 5, "RatkinYoung:young-kiiro")]
        [InlineData("Ratkin", 14, "Ratkin", 13, "RatkinAdult:young-ratkin")]
        [InlineData("Kiiro", 13, "Ratkin", 30, "KiiroYoung:adult-ratkin")]
        [InlineData("Kiiro", 14, "Ratkin", 2, "KiiroAdult:young-ratkin")]
        [InlineData("Human", 8, "Ratkin", 4, "Generic:young-ratkin")]
        public void SpeakerVoiceAndCounterpartAgeAreIndependent(string race, double years, string otherRace, double otherYears, string expected)
        {
            Assert.True(ThoughtVoice.TryText(def, Pawn(race, years), Pawn(otherRace, otherYears), true, out string text));
            Assert.Equal(expected, text);
        }

        [Fact]
        public void DisabledRaceModAndUnrelatedRaceDoNotAcquireItsVoice()
        {
            activeMods.Remove(ratkin);
            Assert.True(ThoughtVoice.TryText(def, Pawn("Ratkin", 3), Pawn("Kiiro", 20), true, out string disabled));
            Assert.Equal("Generic:adult-kiiro", disabled);
            Assert.True(ThoughtVoice.TryText(def, Pawn("Human", 20), Pawn("Kiiro", 3), true, out string ordinary));
            Assert.Equal("Generic:young-kiiro", ordinary);
        }

        [Fact]
        public void MissingVoiceUsesCurrentLanguageNeutralTextAndThenOriginal()
        {
            language.keyedReplacements.Remove(def.defName + "_RatkinYoung_Description");
            Pawn speaker = Pawn("Ratkin", 2);
            Assert.True(ThoughtVoice.TryText(def, speaker, null, true, out string text));
            Assert.Equal("Generic:unknown", text);
            language.keyedReplacements.Remove(def.defName + "_Generic_Description");
            Assert.False(ThoughtVoice.TryText(def, speaker, null, true, out _));
            Thought_Memory thought = Raw<Thought_Memory>();
            thought.def = def;
            thought.pawn = speaker;
            Assert.Equal("original", ThoughtVoice.Format(thought, "original", true));
        }

        [Fact]
        public void MemoryKeepsRecordedCounterpartAndPreservesVanillaNotes()
        {
            Thought_Memory thought = Raw<Thought_Memory>();
            thought.def = def;
            thought.pawn = Pawn("Ratkin", 3);
            thought.otherPawn = Pawn("Kiiro", 20);
            Assert.Equal("RatkinYoung:adult-kiiro\n\nvanilla-note", ThoughtVoice.Format(thought, "original\n\nvanilla-note", true));
            SetAge(thought.pawn, 14);
            Assert.Equal("RatkinAdult:adult-kiiro", ThoughtVoice.Format(thought, "original", true));
        }

        [Fact]
        public void LanguageSwitchDoesNotReusePreviousLanguageVoice()
        {
            Pawn speaker = Pawn("Ratkin", 4);
            Assert.True(ThoughtVoice.TryText(def, speaker, null, true, out string chinese));
            Assert.Equal("RatkinYoung:unknown", chinese);
            LoadedLanguage english = Raw<LoadedLanguage>();
            typeof(LoadedLanguage).GetField("dataIsLoaded", Fields).SetValue(english, true);
            english.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            LanguageDatabase.activeLanguage = english;
            Assert.False(ThoughtVoice.TryText(def, speaker, null, true, out _));
        }

        [Fact]
        public void UnrelatedThoughtIsUnchanged()
        {
            Thought_Memory thought = Raw<Thought_Memory>();
            thought.def = new ThoughtDef { defName = "Unrelated" };
            thought.pawn = Pawn("Ratkin", 3);
            Assert.Equal("original", ThoughtVoice.Format(thought, "original", true));
        }

        public void Dispose()
        {
            LanguageDatabase.activeLanguage = previousLanguage;
            activeMods.Clear();
            activeMods.AddRange(previousMods);
        }

        private void Add(string key, string value) => language.keyedReplacements.Add(key,
            new LoadedLanguage.KeyedReplacement { key = key, value = value });

        private Pawn Pawn(string race, double years)
        {
            Pawn pawn = Raw<Pawn>();
            pawn.def = Raw<ThingDef>();
            pawn.def.defName = race;
            pawn.def.label = race;
            pawn.def.race = new RaceProperties { intelligence = Intelligence.Humanlike };
            pawn.def.modContentPack = race == "Ratkin" ? ratkin : race == "Kiiro" ? kiiro : null;
            pawn.ageTracker = Raw<Pawn_AgeTracker>();
            SetAge(pawn, years);
            return pawn;
        }

        private static ModContentPack Pack(string package)
        {
            ModContentPack pack = Raw<ModContentPack>();
            typeof(ModContentPack).GetField("packageIdPlayerFacingInt", Fields).SetValue(pack, package);
            return pack;
        }

        private static void SetAge(Pawn pawn, double years) => typeof(Pawn_AgeTracker)
            .GetField("ageBiologicalTicksInt", Fields).SetValue(pawn.ageTracker, (long)(years * 3600000));
        private static T Raw<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
}
#endif
