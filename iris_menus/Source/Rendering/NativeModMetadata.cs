using System;
using System.IO;
using System.Xml;
using Verse;

namespace IrisMenus
{
    internal sealed class NativeModMetadata
    {
        internal readonly string Name, Author, Version;

        internal NativeModMetadata(ModContentPack content)
        {
            if (content == null) return;
            string path = Path.Combine(content.RootDir, "About", "About.xml");
            if (!File.Exists(path)) return;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (XmlReader reader = XmlReader.Create(path, settings))
                {
                    var document = new XmlDocument { XmlResolver = null };
                    document.Load(reader);
                    XmlNode root = document.SelectSingleNode("/ModMetaData");
                    Name = root?.SelectSingleNode("name")?.InnerText?.Trim();
                    Author = root?.SelectSingleNode("author")?.InnerText?.Trim();
                    Version = root?.SelectSingleNode("modVersion")?.InnerText?.Trim();
                }
            }
            catch (Exception exception) when (exception is XmlException || exception is IOException || exception is UnauthorizedAccessException)
            {
                Log.Warning("[IrisMenus] Cannot read footer metadata for " + content.PackageIdPlayerFacing + ": " + exception.Message);
            }
        }
    }
}
