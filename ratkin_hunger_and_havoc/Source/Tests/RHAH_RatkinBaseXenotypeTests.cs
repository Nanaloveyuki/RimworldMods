using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    // 用原版 XML 补丁检查白名单补回与候选保留
    public class RHAH_RatkinBaseXenotypeTests : IDisposable
    {
        const string RaceXPath =
            "Defs/AlienRace.ThingDef_AlienRace[defName=\"Ratkin\"]/alienRace/raceRestriction/whiteXenotypeList";

        readonly bool previousProfilerEnabled = DeepProfiler.enabled;

        public RHAH_RatkinBaseXenotypeTests()
        {
            // 独立测试宿主未加载 Prefs 不启用依赖它的深度计时
            DeepProfiler.enabled = false;
        }

        public void Dispose()
        {
            DeepProfiler.enabled = previousProfilerEnabled;
        }

        [Fact]
        public void OfficialBranchRestoresBaseXenotypeWithoutDroppingOthers()
        {
            List<string> original = RgeOfficialWhiteList();
            List<string> list = ApplyPatch(original);
            original.Add("RK_XenoType_Ratkin");
            Assert.Equal(original, list);
        }

        [Fact]
        public void UnofficialBranchRestoresBaseXenotypeWithoutDroppingOthers()
        {
            List<string> original = RgeUnofficialWhiteList();
            List<string> list = ApplyPatch(original);
            original.Add("RK_XenoType_Ratkin");
            Assert.Equal(original, list);
        }

        [Fact]
        public void RepeatedApplyIsIdempotent()
        {
            List<string> once = ApplyPatch(RgeOfficialWhiteList());
            List<string> twice = ApplyPatch(once);
            Assert.Equal(once, twice);
        }

        [Fact]
        public void PlainNewRatkinPlusKeepsExistingWhitelist()
        {
            List<string> list = ApplyPatch(new List<string> { "RK_XenoType_Ratkin", "Baseliner", "Sanguophage" });
            Assert.Equal(new[] { "RK_XenoType_Ratkin", "Baseliner", "Sanguophage" }, list);
        }

        [Fact]
        public void ExistingEntryWithWhitespaceIsNotDuplicated()
        {
            XmlDocument race = RaceDocument(new List<string>());
            XmlNode node = race.SelectSingleNode(RaceXPath);
            XmlElement spaced = race.CreateElement("li");
            spaced.InnerText = "  RK_XenoType_Ratkin  ";
            node.AppendChild(spaced);
            ApplyPatch(race);
            List<string> list = ReadRace(race);
            Assert.Equal(new[] { "  RK_XenoType_Ratkin  " }, list);
        }

        [Fact]
        public void ThirdPartyCandidatesBeforeAndAfterRepairArePreserved()
        {
            List<string> original = RgeOfficialWhiteList();
            original.Insert(2, "ThirdParty_Ratkin");
            List<string> once = ApplyPatch(original);
            original.Add("RK_XenoType_Ratkin");
            Assert.Equal(original, once);
            once.Add("Ratkin_OA");
            Assert.Equal(once, ApplyPatch(once));
        }

        static List<string> RgeOfficialWhiteList()
        {
            return new List<string>
            {
                "Ratkin_HouseMouse", "Ratkin_Mole", "Ratkin_LabRat", "Ratkin_Hamster",
                "Ratkin_Squirrel", "Ratkin_Vole", "Ratkin_VolePrototype", "Ratkin_Waster",
                "Ratkin_GuineaPig", "Ratkin_Beaver"
            };
        }

        static List<string> RgeUnofficialWhiteList()
        {
            return new List<string>
            {
                "Ratkin_HouseMouse", "Ratkin_Mole", "Ratkin_LabRat", "Ratkin_Hamster",
                "Ratkin_Squirrel", "Ratkin_Vole", "Ratkin_VolePrototype", "Ratkin_Waster"
            };
        }

        // 返回真实补丁执行后的候选顺序
        static List<string> ApplyPatch(List<string> whiteXenotypeList)
        {
            XmlDocument race = RaceDocument(whiteXenotypeList);
            ApplyPatch(race);
            return ReadRace(race);
        }

        static void ApplyPatch(XmlDocument race)
        {
            XmlDocument patch = PatchDocument();
            foreach (XmlNode operation in patch.DocumentElement.ChildNodes)
            {
                if (operation.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                PatchOperation parsed = DirectXmlToObject.ObjectFromXml<PatchOperation>(operation, false);
                Assert.NotNull(parsed);
                Assert.True(parsed.Apply(race));
            }
        }

        static List<string> ReadRace(XmlDocument race)
        {
            List<string> result = new List<string>();
            foreach (XmlNode item in race.SelectSingleNode(RaceXPath).SelectNodes("li"))
            {
                result.Add(item.InnerText);
            }

            return result;
        }

        static XmlDocument RaceDocument(List<string> whiteXenotypeList)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(
                "<Defs><AlienRace.ThingDef_AlienRace><defName>Ratkin</defName><alienRace><raceRestriction>" +
                "<whiteXenotypeList /></raceRestriction></alienRace></AlienRace.ThingDef_AlienRace></Defs>");
            XmlNode node = document.SelectSingleNode(RaceXPath);
            foreach (string defName in whiteXenotypeList)
            {
                XmlElement item = document.CreateElement("li");
                item.InnerText = defName;
                node.AppendChild(item);
            }

            return document;
        }

        static XmlDocument PatchDocument()
        {
            XmlDocument document = new XmlDocument();
            document.Load(Path.Combine(Root(), "1.6", "Patches", "RHAH_RatkinBaseXenotype.xml"));
            return document;
        }

        static string Root([CallerFilePath] string testFile = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile), "..", ".."));
        }
    }
}
