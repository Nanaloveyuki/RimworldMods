using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace LeadYourPet.Tests
{
    public class DuplicateLoadingTests
    {
        [Theory]
        [InlineData("", true)]
        [InlineData("lezhizhong.leadyourpet", false)]
        [InlineData("codex.leadyourpet", false)]
        [InlineData("unrelated.mod", true)]
        public void DuplicateSessionLoadsOnlyGuard(string activeOriginal, bool expectedMain)
        {
            DirectoryInfo root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !File.Exists(Path.Combine(root.FullName, "LoadFolders.xml")))
                root = root.Parent;
            Assert.NotNull(root);
            XDocument document = XDocument.Load(Path.Combine(root.FullName, "LoadFolders.xml"));
            XElement version = document.Root.Element("v1.6");
            XElement guard = version.Elements("li").Single(li => li.Value == "Guard");
            Assert.Empty(guard.Attributes());
            XElement main = version.Elements("li").Single(li => li.Value == "/");
            string[] disallowed = ((string)main.Attribute("IfModNotActive")).Split(',');
            Assert.Equal(expectedMain, !disallowed.Contains(activeOriginal));
            XDocument about = XDocument.Load(Path.Combine(root.FullName, "About", "About.xml"));
            foreach (string id in disallowed)
                Assert.Contains(about.Root.Element("incompatibleWith").Elements("li"), li => li.Value == id);
        }
    }
}
