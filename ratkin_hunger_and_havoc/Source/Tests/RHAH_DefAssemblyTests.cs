#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_DefAssemblyTests
    {
        [Theory]
        [InlineData("1.6")]
        [InlineData("1.7")]
        public void EachRuntimeContainsEveryTypeReferencedByItsXml(string version)
        {
            DirectoryInfo root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Source", "HungerAndHavoc.csproj")))
            {
                root = root.Parent;
            }
            Assert.NotNull(root);
            HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
            foreach (string assembly in new[] { "HungerAndHavoc.dll", "HungerAndHavoc.Api.dll" })
            {
                using FileStream stream = File.OpenRead(Path.Combine(root.FullName, version, "Assemblies", assembly));
                using PEReader reader = new PEReader(stream);
                MetadataReader metadata = reader.GetMetadataReader();
                foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
                {
                    TypeDefinition type = metadata.GetTypeDefinition(handle);
                    types.Add(metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name));
                }
            }

            List<string> missing = new List<string>();
            foreach (string folder in new[] { version + "/Defs", version + "/Patches", "Biotech" })
            {
                foreach (string file in Directory.GetFiles(Path.Combine(root.FullName, folder), "*.xml", SearchOption.AllDirectories))
                {
                    foreach (XElement element in XDocument.Load(file).Descendants())
                    {
                        string name = element.HasElements ? null : element.Value.Trim();
                        CheckType(name, file, types, missing);
                        CheckType(element.Attribute("Class")?.Value, file, types, missing);
                    }
                }
            }
            Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
        }

        static void CheckType(string name, string file, HashSet<string> types, List<string> missing)
        {
            if (name != null && name.StartsWith("HungerAndHavoc.", StringComparison.Ordinal) && !types.Contains(name))
            {
                missing.Add(file + ": " + name);
            }
        }
    }
}
#endif
