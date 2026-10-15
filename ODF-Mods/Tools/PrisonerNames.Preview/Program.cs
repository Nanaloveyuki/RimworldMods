using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Odf.PrisonerNames;
using Oxygendsl.Language;

internal static class Program
{
    static int Main(string[] args)
    {
        string source = null;
        string facts = "{}";
        bool chinese = false;
        var mods = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--source" && i + 1 < args.Length) source = File.ReadAllText(args[++i]);
            else if (args[i] == "--facts" && i + 1 < args.Length) facts = args[++i];
            else if (args[i] == "--mod" && i + 1 < args.Length) mods.Add(Path.GetFullPath(args[++i]));
            else if (args[i] == "--chinese") chinese = true;
            else return Fail("Use --source file --facts json [--chinese] [--mod directory].");
        }
        if (source == null) return Fail("A --source file is required.");
        try
        {
            NamingRules.ModuleCatalog = new ModRootCatalog(mods);
            Console.WriteLine(NamingPreview.Evaluate(source, facts, NamePolicy.CreateDefault(), chinese) ?? string.Empty);
            return 0;
        }
        catch (Exception exception)
        {
            return Fail(exception.Message);
        }
    }

    static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    sealed class ModRootCatalog : IOxygenModuleCatalog
    {
        readonly Dictionary<string, string> roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ModRootCatalog(IEnumerable<string> mods)
        {
            foreach (string mod in mods)
            {
                string id = Identity(mod);
                if (roots.ContainsKey(id)) throw new InvalidOperationException("Duplicate module root: " + id);
                roots.Add(id, mod);
            }
        }

        public bool TryGetSource(string packageId, string modulePath, out string source)
        {
            source = null;
            string root;
            if (string.IsNullOrWhiteSpace(packageId) || !roots.TryGetValue(packageId, out root) || !Valid(modulePath)) return false;
            string relative = modulePath.Replace('.', Path.DirectorySeparatorChar) + ".oxy";
            string path = Path.GetFullPath(Path.Combine(root, "Oxygen", relative));
            string oxygen = Path.GetFullPath(Path.Combine(root, "Oxygen")) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(oxygen, StringComparison.Ordinal) || !File.Exists(path)) return false;
            source = File.ReadAllText(path);
            return true;
        }

        static bool Valid(string modulePath)
        {
            if (string.IsNullOrEmpty(modulePath)) return false;
            foreach (string part in modulePath.Split('.'))
            {
                if (part.Length == 0 || !(part[0] == '_' || char.IsLetter(part[0]))) return false;
                if (part.Any(c => c != '_' && !char.IsLetterOrDigit(c))) return false;
            }
            return true;
        }

        static string Identity(string root)
        {
            string about = Path.Combine(root, "About", "About.xml");
            if (!File.Exists(about)) throw new InvalidOperationException("Missing About/About.xml in " + root);
            var document = new XmlDocument();
            document.Load(about);
            XmlNode id = document.SelectSingleNode("/ModMetaData/packageId");
            if (id == null || string.IsNullOrWhiteSpace(id.InnerText)) throw new InvalidOperationException("Missing packageId in " + root);
            return id.InnerText;
        }
    }
}
