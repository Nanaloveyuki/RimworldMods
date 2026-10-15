using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Xml;
using Oxygendsl.Core;
using Oxygendsl.Language;
using Oxygendsl.Mod.Content;
using Oxygendsl.Mod.Runtime;

namespace Oxygendsl.Tool
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length < 2 || (args[0] != "check" && args[0] != "preview"))
                {
                    Console.Error.WriteLine("Usage: Oxygendsl.Tool check|preview MOD_ROOT [--catalog DEFS_ROOT] [--catalog-complete] [--mod MOD_ROOT] [--facts FILE.json]");
                    return 1;
                }
                string root = Path.GetFullPath(args[1]);
                List<string> catalogs = new List<string>();
                List<string> mods = new List<string> { root };
                string factsPath = null;
                bool complete = false;
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--catalog-complete") { complete = true; continue; }
                    if (i + 1 == args.Length) throw new ArgumentException("Missing value for " + args[i]);
                    string option = args[i++];
                    if (option == "--catalog") catalogs.Add(Path.GetFullPath(args[i]));
                    else if (option == "--mod") mods.Add(Path.GetFullPath(args[i]));
                    else if (option == "--facts") factsPath = args[i];
                    else throw new ArgumentException("Unknown option " + option);
                }
                FileCatalog modules = new FileCatalog(mods);
                string package = modules.Identity(root);
                ContentCompilation content = Compile(root, modules);
                ContentCompilation rules = content;
                XmlDocument document = new XmlDocument();
                document.LoadXml("<Defs/>");
                NativeContentSchema schema = NativeContentSchema.Shared;
                schema.LoadFromFolders(catalogs, modules.Packages);
                foreach (string folder in catalogs)
                    foreach (string path in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                    {
                        XmlDocument file = new XmlDocument();
                        file.Load(path);
                        if (file.DocumentElement == null || file.DocumentElement.Name != "Defs") continue;
                        foreach (XmlNode node in file.DocumentElement.ChildNodes)
                            if (node is XmlElement) document.DocumentElement.AppendChild(document.ImportNode(node, true));
                    }
                foreach (GateRuleDefinition gate in rules.Plan.Gates)
                    if (!NativeRuleRuntime.IsGateName(gate.Gate)) throw new InvalidOperationException(gate.Line + ":" + gate.Column + " Unknown RHAH gate '" + gate.Gate + "'.");
                ContentPlan combined = new ContentPlan(content.Plan.Definitions, content.Plan.Patches, rules.Plan.Thoughts, rules.Plan.Gates);
                NativeContentCheckResult native = NativeContentPipeline.Check(combined, document, modules.Packages, complete, package);
                foreach (NativeContentIssue issue in native.Issues)
                    Console.WriteLine(issue.Kind + " " + issue.Line + ":" + issue.Column + " " + issue.Target + "." + issue.Field + ": " + issue.Message);
                foreach (NativeContentChange change in native.Changes)
                    Console.WriteLine(change.Order + " " + change.Kind + " " + change.Type + "/" + change.Id + "." + change.Field + " " + change.Before + " -> " + change.After);
                Console.WriteLine("Native catalog " + (schema.IsComplete ? "declared complete" : "INCOMPLETE: " + schema.IncompleteReason));
                IReadOnlyList<TranslationInjectionIssue> translationIssues = TranslationInjectionCheck.Check(
                    root, native.Success ? native.CandidateXml : document.OuterXml, complete);
                foreach (TranslationInjectionIssue issue in translationIssues)
                    Console.WriteLine(issue.Kind + " " + issue.Line + ":" + issue.Column + " " + issue.Target + "." + issue.Field + ": " + issue.Message);
                foreach (string folder in catalogs) Console.WriteLine("Catalog " + folder + " SHA256 " + CatalogHash(folder));
                foreach (ContentImport import in content.Imports)
                    Console.WriteLine("Import " + import.PackageId + "/" + import.ModulePath + " SHA256 " + import.SourceHash);
                Console.WriteLine("Content changes: " + (native.RestartRequired ? "restart required" : "none") + "; rule evaluation is read-only.");
                Dictionary<string, int> stages = new Dictionary<string, int>(StringComparer.Ordinal);
                if (native.Success) document.LoadXml(native.CandidateXml);
                foreach (XmlNode node in document.DocumentElement.ChildNodes)
                    if (node.Name == "ThoughtDef" && node["defName"] != null && node["stages"] != null)
                        stages[node["defName"].InnerText] = node["stages"].ChildNodes.OfType<XmlElement>().Count();
                if (args[0] == "preview")
                {
                    if (factsPath == null) throw new ArgumentException("preview requires --facts JSON facts; it does not invent game state.");
                    JavaScriptSerializer json = new JavaScriptSerializer();
                    IDictionary<string, object> values = (IDictionary<string, object>)json.DeserializeObject(File.ReadAllText(factsPath));
                    PawnContext pawn = new PawnContext
                    {
                        Id = Integer(values, "id"), Name = Text(values, "name"),
                        Mood = Number(values, "mood"), Food = Number(values, "food"), Rest = Number(values, "rest"),
                        Colonist = Flag(values, "colonist"), Drafted = Flag(values, "drafted"), Downed = Flag(values, "downed"),
                        Facts = new PreviewFacts(values)
                    };
                    GameContext game = new GameContext { Tick = Integer(values, "tick"), Hour = Number(values, "hour") ?? 0d };
                    foreach (ThoughtRuleDefinition rule in rules.Plan.Thoughts)
                    {
                        int stage = rule.Evaluator.Evaluate(pawn, game);
                        int count;
                        if (stages.TryGetValue(rule.Thought.Id, out count) && stage >= count)
                            throw new ScriptEvaluationException("Stage " + stage + " outside native stage count " + count, rule.Line, rule.Column);
                        Console.WriteLine(rule.Id + ": " + (stage < 0 ? "inactive" : "stage " + stage));
                    }
                    foreach (GateRuleDefinition rule in rules.Plan.Gates)
                    {
                        bool? decision = rule.Evaluator.Evaluate(pawn, game);
                        Console.WriteLine(rule.Id + " " + rule.Gate + ": " + (decision.HasValue ? (decision.Value ? "allow" : "deny") : "abstain"));
                    }
                }
                bool unknown = native.Issues.Any(issue => issue.Kind == NativeContentIssueKind.Unknown)
                    || translationIssues.Any(issue => issue.Kind == NativeContentIssueKind.Unknown)
                    || !schema.IsComplete;
                if (native.Issues.Any(issue => issue.Kind == NativeContentIssueKind.Error)
                    || translationIssues.Any(issue => issue.Kind == NativeContentIssueKind.Error)) return 1;
                if (!native.Success) return 2;
                return unknown ? 2 : 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        static ContentCompilation Compile(string root, IOxygenModuleCatalog modules)
        {
            var definitions = new List<ContentDefinition>();
            var patches = new List<ContentPatch>();
            var thoughts = new List<ThoughtRuleDefinition>();
            var gates = new List<GateRuleDefinition>();
            var imports = new List<ContentImport>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (OxygenSourceFile file in OxygenSourceFiles.Discover(new[] { root }))
            {
                OxygenFileCompilation result = OxygenFileCompiler.Compile(File.ReadAllText(file.Path), false, modules);
                if (!result.Success) throw new InvalidOperationException(file.Path + ": " + string.Join("; ", result.Diagnostics));
                foreach (ContentDefinition definition in result.Content.Definitions)
                {
                    if (!ids.Add("def/" + definition.Type + "/" + definition.Id)) throw new InvalidOperationException(file.Path + ": Duplicate definition " + definition.Id);
                    definitions.Add(definition);
                }
                patches.AddRange(result.Content.Patches);
                foreach (ThoughtRuleDefinition thought in result.Content.Thoughts)
                {
                    if (!ids.Add("thought/" + thought.Id)) throw new InvalidOperationException(file.Path + ": Duplicate thought rule " + thought.Id);
                    thoughts.Add(thought);
                }
                foreach (GateRuleDefinition gate in result.Content.Gates)
                {
                    if (!ids.Add("gate/" + gate.Id)) throw new InvalidOperationException(file.Path + ": Duplicate gate rule " + gate.Id);
                    gates.Add(gate);
                }
                imports.AddRange(result.Imports);
            }
            return new ContentCompilation(new ContentPlan(definitions, patches, thoughts, gates), Array.Empty<ScriptDiagnostic>(), imports);
        }

        static string CatalogHash(string root)
        {
            using (SHA256 sha = SHA256.Create())
            {
                foreach (string path in Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                {
                    byte[] name = System.Text.Encoding.UTF8.GetBytes(path.Substring(root.Length));
                    sha.TransformBlock(name, 0, name.Length, name, 0);
                    byte[] bytes = File.ReadAllBytes(path);
                    sha.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", string.Empty);
            }
        }

        static bool Flag(IDictionary<string, object> facts, string key) { object value; return facts.TryGetValue(key, out value) && value is bool flag && flag; }
        static int Integer(IDictionary<string, object> facts, string key) { object value; return facts.TryGetValue(key, out value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0; }
        static double? Number(IDictionary<string, object> facts, string key) { object value; return facts.TryGetValue(key, out value) && value != null ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : (double?)null; }
        static string Text(IDictionary<string, object> facts, string key) { object value; return facts.TryGetValue(key, out value) ? Convert.ToString(value) : string.Empty; }

        sealed class FileCatalog : IOxygenModuleCatalog
        {
            readonly Dictionary<string, string> roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public FileCatalog(IEnumerable<string> mods)
            {
                foreach (string mod in mods) roots.Add(Identity(mod), mod);
            }
            public List<string> Packages { get { return roots.Keys.ToList(); } }
            public string Identity(string root)
            {
                XmlDocument about = new XmlDocument();
                about.Load(Path.Combine(root, "About", "About.xml"));
                XmlNode id = about.SelectSingleNode("/ModMetaData/packageId");
                if (id == null) throw new InvalidOperationException("Missing packageId in " + root);
                return id.InnerText.ToLowerInvariant();
            }
            public bool TryGetSource(string packageId, string modulePath, out string source)
            {
                source = null;
                string root;
                if (!roots.TryGetValue(packageId, out root)) return false;
                foreach (string part in modulePath.Split('.'))
                    if (part.Length == 0 || !(part[0] == '_' || char.IsLetter(part[0])) || part.Any(c => c != '_' && !char.IsLetterOrDigit(c))) return false;
                string path = Path.Combine(root, "Oxygen", modulePath.Replace('.', Path.DirectorySeparatorChar) + ".oxy");
                if (!File.Exists(path)) return false;
                source = File.ReadAllText(path);
                return true;
            }
        }

        sealed class PreviewFacts : IOxygenHostValue
        {
            readonly IDictionary<string, object> values;
            public PreviewFacts(IDictionary<string, object> values) { this.values = values; }
            public bool TryGetMember(string name, out object value)
            {
                value = null;
                if (name == "has_trait" || name == "trait_degree" || name == "trait_suppressed")
                {
                    value = new TraitQuery(values, name);
                    return true;
                }
                if (values.TryGetValue(name, out value)) return true;
                throw new ScriptEvaluationException("Preview input does not supply pawn." + name, 1, 1);
            }
        }

        sealed class TraitQuery : IOxygenHostCallable
        {
            readonly IDictionary<string, object> values;
            readonly string kind;
            public TraitQuery(IDictionary<string, object> values, string kind) { this.values = values; this.kind = kind; }
            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                object name;
                if (!named.TryGetValue("name", out name)) name = positional.Count > 0 ? positional[0] : null;
                if (!(name is string)) throw new InvalidOperationException("Trait query requires a string name.");
                object traits;
                if (!values.TryGetValue("traits", out traits)) throw new InvalidOperationException("Preview input does not supply traits.");
                IDictionary<string, object> map = traits as IDictionary<string, object>;
                object degree;
                bool found = map != null && map.TryGetValue((string)name, out degree);
                if (kind == "trait_degree") return found ? Convert.ToInt32(map[(string)name]) : 0;
                if (kind == "trait_suppressed")
                {
                    object suppressed;
                    if (!values.TryGetValue("suppressed_traits", out suppressed)) throw new InvalidOperationException("Preview input does not supply suppressed_traits.");
                    return ((IEnumerable)suppressed).Cast<object>().Contains(name);
                }
                object requested;
                if (!named.TryGetValue("degree", out requested)) requested = positional.Count > 1 ? positional[1] : null;
                return found && (requested == null || Convert.ToInt32(map[(string)name]) == Convert.ToInt32(requested));
            }
        }
    }
}
