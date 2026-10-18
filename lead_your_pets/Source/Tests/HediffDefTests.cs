using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace LeadYourPet.Tests
{
    public class HediffDefTests
    {
        [Fact]
        public void DragBruiseDisappearsAfterThreeInGameHours()
        {
            XDocument document = XDocument.Load(FindHediffDefsPath());
            XElement dragBruise = document.Descendants("HediffDef")
                .First(def => (string)def.Element("defName") == "LeadYourPet_DragBruise");

            XElement disappearsComp = dragBruise.Element("comps")?
                .Elements("li")
                .FirstOrDefault(li => (string)li.Attribute("Class") == "HediffCompProperties_Disappears");

            Assert.NotNull(disappearsComp);
            Assert.Equal("7500~7500", (string)disappearsComp.Element("disappearsAfterTicks"));
        }

        [Fact]
        public void MouseEggCryingNearbyMoodIsPositiveAndStacksForSixHours()
        {
            XDocument document = XDocument.Load(FindThoughtDefsPath());
            XElement thought = document.Descendants("ThoughtDef")
                .First(def => (string)def.Element("defName") == "LeadYourPet_MouseEggCryingNearbyMood");

            Assert.Equal("0.25", (string)thought.Element("durationDays"));
            Assert.Equal("10", (string)thought.Element("stackLimit"));
            Assert.Equal("3", (string)thought.Descendants("baseMoodEffect").Single());
        }

        private static string FindHediffDefsPath()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                string path = Path.Combine(directory.FullName, "Defs", "HediffDefs", "LeadYourPet_Hediffs.xml");
                if (File.Exists(path))
                {
                    return path;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("未找到 LeadYourPet_Hediffs.xml。");
        }

        private static string FindThoughtDefsPath()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                string path = Path.Combine(directory.FullName, "Defs", "ThoughtDefs", "LeadYourPet_Thoughts.xml");
                if (File.Exists(path))
                {
                    return path;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("未找到 LeadYourPet_Thoughts.xml。");
        }
    }
}
