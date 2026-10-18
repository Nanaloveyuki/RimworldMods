using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Trade;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_AmbushLetterTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        readonly LetterDef previousThreat;

        public RHAH_AmbushLetterTests()
        {
            FieldInfo binding = typeof(DefOfHelper).GetField("bindingNow", BindingFlags.Static | BindingFlags.NonPublic);
            bool previousBinding = (bool)binding.GetValue(null);
            try
            {
                binding.SetValue(null, true);
                RuntimeHelpers.RunClassConstructor(typeof(LetterDefOf).TypeHandle);
            }
            finally
            {
                binding.SetValue(null, previousBinding);
            }
            previousThreat = LetterDefOf.ThreatBig;
            LetterDefOf.ThreatBig = (LetterDef)FormatterServices.GetUninitializedObject(typeof(LetterDef));
            LetterDefOf.ThreatBig.letterClass = typeof(StandardLetter);
            Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            typeof(Game).GetField("maps", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Current.Game, new List<Map>());
            LanguageDatabase.activeLanguage = LoadLanguage();
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            LanguageDatabase.activeLanguage = previousLanguage;
            LetterDefOf.ThreatBig = previousThreat;
        }

        [Theory]
        [InlineData("I-035", 0)]
        [InlineData("I-035", 1)]
        [InlineData("I-050", 0)]
        [InlineData("I-050", 1)]
        public void AmbushLetterTargetsAttackersOnTheirActualMap(string displayId, int attackMapIndex)
        {
            Find.Maps.Add((Map)FormatterServices.GetUninitializedObject(typeof(Map)));
            Find.Maps.Add((Map)FormatterServices.GetUninitializedObject(typeof(Map)));
            Faction faction = (Faction)FormatterServices.GetUninitializedObject(typeof(Faction));
            List<Verse.Pawn> attackers = new List<Verse.Pawn>
            {
                Attacker(attackMapIndex),
                Attacker(attackMapIndex)
            };

            ChoiceLetter letter = TradeEventRouter.CreateAmbushLetter(RHAH_IncidentCatalog.GetByDisplayId(displayId), attackers, faction);

            Assert.IsType<StandardLetter>(letter);
            Assert.Same(LetterDefOf.ThreatBig, letter.def);
            Assert.Same(faction, letter.relatedFaction);
            Assert.Equal(attackers.Count, letter.lookTargets.targets.Count);
            for (int i = 0; i < attackers.Count; i++)
            {
                Assert.Same(attackers[i], letter.lookTargets.targets[i].Thing);
                Assert.Same(Find.Maps[attackMapIndex], letter.lookTargets.targets[i].Map);
                Assert.Null(letter.lookTargets.targets[i].WorldObject);
            }
        }

        static Verse.Pawn Attacker(int mapIndex)
        {
            Verse.Pawn pawn = (Verse.Pawn)FormatterServices.GetUninitializedObject(typeof(Verse.Pawn));
            typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(pawn, (sbyte)mapIndex);
            return pawn;
        }

        static LoadedLanguage LoadLanguage([CallerFilePath] string testFile = null)
        {
            LoadedLanguage language = (LoadedLanguage)FormatterServices.GetUninitializedObject(typeof(LoadedLanguage));
            typeof(LoadedLanguage).GetField("dataIsLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(language, true);
            language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            XmlDocument document = new XmlDocument();
            document.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile), "../../Languages/English/Keyed/Incidents/RHAH_Incidents.xml")));
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    language.keyedReplacements.Add(node.Name, new LoadedLanguage.KeyedReplacement { key = node.Name, value = node.InnerText });
                }
            }
            return language;
        }
    }
}
