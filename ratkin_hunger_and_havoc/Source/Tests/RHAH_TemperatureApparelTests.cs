using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_TemperatureApparelTests : IDisposable
    {
        readonly bool previousProfiler = DeepProfiler.enabled;

        public RHAH_TemperatureApparelTests()
        {
            DeepProfiler.enabled = false;
        }

        public void Dispose()
        {
            DeepProfiler.enabled = previousProfiler;
        }

        [Fact]
        public void TemperatureClothesKeepBabyPermissionWithoutDuplicatingOrRemovingOtherItems()
        {
            XmlDocument defs = Fixture();
            defs.DocumentElement.InnerXml +=
                "<Toddlers.DefListDef><defName>WearableByBaby</defName><whitelist>" +
                "<li>Apparel_ShieldBelt</li><li>  RHAH_Cold_ThinHemp  </li>" +
                "</whitelist></Toddlers.DefListDef>";

            Apply(defs);
            Apply(defs);

            string[] names = defs.SelectNodes("Defs/Toddlers.DefListDef/whitelist/li").Cast<XmlNode>()
                .Select(node => node.InnerText.Trim()).ToArray();
            Assert.Contains("Apparel_ShieldBelt", names);
            XmlDocument apparel = new XmlDocument();
            apparel.Load(Path.Combine(Root(), "1.6", "Defs", "ThingDefs", "RHAH_TemperatureApparel.xml"));
            foreach (XmlNode name in apparel.SelectNodes("Defs/ThingDef/defName"))
            {
                Assert.Single(names, item => item == name.InnerText);
            }
        }

        [Fact]
        public void WithoutToddlersTemperatureStatPatchesStillApply()
        {
            XmlDocument defs = Fixture();

            Apply(defs);

            foreach (XmlNode stat in defs.SelectNodes("Defs/StatDef"))
            {
                Assert.NotNull(stat.SelectSingleNode("parts/li[@Class='HungerAndHavoc.Pawn.StatPart_RHAH_TemperatureApparel']"));
            }
            Assert.Null(defs.SelectSingleNode("Defs/Toddlers.DefListDef"));
        }

        static XmlDocument Fixture()
        {
            XmlDocument defs = new XmlDocument();
            defs.LoadXml("<Defs><StatDef><defName>Insulation_Cold</defName><parts /></StatDef>" +
                "<StatDef><defName>Insulation_Heat</defName><parts /></StatDef></Defs>");
            return defs;
        }

        static void Apply(XmlDocument defs)
        {
            XmlDocument patch = new XmlDocument();
            patch.Load(Path.Combine(Root(), "1.6", "Patches", "RHAH_TemperatureApparel.xml"));
            foreach (XmlNode operation in patch.DocumentElement.ChildNodes)
            {
                if (operation.NodeType == XmlNodeType.Element)
                {
                    Assert.True(DirectXmlToObject.ObjectFromXml<PatchOperation>(operation, false).Apply(defs));
                }
            }
        }

        static string Root([CallerFilePath] string file = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "..", ".."));
        }
    }
}
