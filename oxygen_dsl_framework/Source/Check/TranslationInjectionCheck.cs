using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Oxygendsl.Mod.Content;
using Verse;

namespace Oxygendsl.Tool
{
    sealed class TranslationInjectionIssue
    {
        public TranslationInjectionIssue(NativeContentIssueKind kind, string message, string target, string field, int line, int column)
        {
            Kind = kind;
            Message = message;
            Target = target ?? string.Empty;
            Field = field ?? string.Empty;
            Line = line;
            Column = column;
        }

        public NativeContentIssueKind Kind { get; }
        public string Message { get; }
        public string Target { get; }
        public string Field { get; }
        public int Line { get; }
        public int Column { get; }
    }

    // Mirrors LoadedLanguage DefInjected folder resolution and DefInjectionPackage's
    // "Found no <type> named <def>" identity check. Field paths and completeness are out of scope.
    sealed class TranslationInjectionCheck
    {
        static readonly string[] IgnoredNamespaces =
        {
            "RimWorld", "Verse", "LudeonTK", "Verse.AI", "Verse.AI.Group", "Verse.Sound", "Verse.Grammar",
            "RimWorld.Planet", "RimWorld.BaseGen", "RimWorld.QuestGen", "RimWorld.SketchGen", "System"
        };

        readonly Dictionary<string, TypeResolution> resolvedTypes = new Dictionary<string, TypeResolution>(StringComparer.Ordinal);
        Dictionary<string, Type> ignoredTypes;
        public static IReadOnlyList<TranslationInjectionIssue> Check(string modRoot, string candidateXml, bool catalogComplete)
        {
            List<TranslationInjectionIssue> issues = new List<TranslationInjectionIssue>();
            string languages = Path.Combine(modRoot, "Languages");
            if (!Directory.Exists(languages))
                return issues;

            var checker = new TranslationInjectionCheck();
            Dictionary<string, HashSet<string>> defs = checker.IndexDefs(candidateXml);
            foreach (string language in Directory.GetDirectories(languages).OrderBy(path => path, StringComparer.Ordinal))
            {
                string injected = Path.Combine(language, "DefInjected");
                if (!Directory.Exists(injected))
                    continue;
                foreach (string folder in Directory.GetDirectories(injected).OrderBy(path => path, StringComparer.Ordinal))
                    checker.CheckFolder(Path.GetFileName(folder), folder, defs, catalogComplete, issues);
            }
            return issues;
        }

        void CheckFolder(string folderName, string folder, Dictionary<string, HashSet<string>> defs, bool catalogComplete, List<TranslationInjectionIssue> issues)
        {
            TypeResolution resolved = ResolveFolder(folderName);
            if (resolved.Kind == ResolutionKind.Unresolved)
            {
                issues.Add(new TranslationInjectionIssue(
                    NativeContentIssueKind.Unknown,
                    "DefInjected folder '" + folderName + "' does not correspond to any def type in the loaded assemblies.",
                    folderName, string.Empty, 1, 1));
            }
            if (resolved.Kind == ResolutionKind.NotDef)
            {
                issues.Add(new TranslationInjectionIssue(
                    NativeContentIssueKind.Error,
                    "DefInjected folder '" + folderName + "' resolves to " + resolved.Type.FullName + ", which is not a Verse.Def.",
                    folderName, string.Empty, 1, 1));
                return;
            }

            string typeName = resolved.Type == null ? folderName : resolved.Type.FullName;
            HashSet<string> names = null;
            bool typeKnown = resolved.Type != null && defs.TryGetValue(typeName, out names);
            foreach (string file in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                XmlDocument document;
                try
                {
                    document = new XmlDocument();
                    document.Load(file);
                }
                catch (XmlException exception)
                {
                    issues.Add(new TranslationInjectionIssue(
                        NativeContentIssueKind.Error,
                        "DefInjected XML could not be loaded (" + Path.GetFileName(file) + "): " + exception.Message,
                        folderName, string.Empty, exception.LineNumber, exception.LinePosition));
                    continue;
                }
                if (document.DocumentElement == null)
                    continue;
                foreach (XmlNode node in document.DocumentElement.ChildNodes)
                {
                    XmlElement element = node as XmlElement;
                    if (element == null)
                        continue;
                    string key = element.Name == "rep" ? ChildText(element, "path") : element.Name;
                    key = key.Replace("]", string.Empty).Replace('[', '.');
                    string defName = FirstSegment(key);
                    if (defName.Length == 0)
                        continue;
                    if (!typeKnown || !names.Contains(defName))
                    {
                        string expectedType = OtherKnownType(defs, typeName, defName);
                        NativeContentIssueKind kind = expectedType != null || catalogComplete && resolved.Type != null
                            ? NativeContentIssueKind.Error
                            : NativeContentIssueKind.Unknown;
                        string reason = expectedType != null
                            ? "belongs to " + expectedType + "; use DefInjected/" + expectedType
                            : "Def catalog is " + (catalogComplete ? "complete and has no such Def" : "incomplete; supply the defining Defs");
                        issues.Add(new TranslationInjectionIssue(
                            kind,
                            "Found no " + (resolved.Type == null ? folderName : resolved.Type.FullName) + " named " + defName + " to match " + key
                                + " (" + Path.GetFileName(file) + "); " + reason + ".",
                            defName, string.Empty, LineOf(element), 1));
                    }
                }
            }
        }

        static string OtherKnownType(Dictionary<string, HashSet<string>> defs, string typeName, string defName)
        {
            foreach (KeyValuePair<string, HashSet<string>> pair in defs)
                if (!string.Equals(pair.Key, typeName, StringComparison.Ordinal) && pair.Value.Contains(defName))
                    return pair.Key;
            return null;
        }

        TypeResolution ResolveFolder(string folderName)
        {
            TypeResolution exact = ResolveTypeName(folderName);
            return exact.Kind != ResolutionKind.Unresolved || folderName.Length <= 3
                ? exact : ResolveTypeName(folderName.Substring(0, folderName.Length - 1));
        }

        TypeResolution ResolveTypeName(string typeName)
        {
            if (resolvedTypes.TryGetValue(typeName, out TypeResolution cached)) return cached;
            Type type = TypeInIgnoredNamespace(typeName) ?? TypeInLoadedAssemblies(typeName);
            for (int i = 0; type == null && i < IgnoredNamespaces.Length; i++)
                type = TypeInLoadedAssemblies(IgnoredNamespaces[i] + "." + typeName);
            TypeResolution result = type == null ? TypeResolution.Unresolved() : Classify(type);
            resolvedTypes[typeName] = result;
            return result;
        }

        static TypeResolution Classify(Type type)
        {
            return typeof(Def).IsAssignableFrom(type)
                ? TypeResolution.Def(type)
                : TypeResolution.NotDef(type);
        }

        // GenTypes.TryGetTypeInIgnoredNamespace is case-sensitive and last-write-wins.
        Type TypeInIgnoredNamespace(string typeName)
        {
            if (ignoredTypes == null)
            {
                ignoredTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException exception) { types = exception.Types; }
                    foreach (Type type in types)
                        if (type != null && (type.Namespace == null || Ignored(type.Namespace)))
                            ignoredTypes[type.Name] = type;
                }
            }
            ignoredTypes.TryGetValue(typeName, out Type found);
            return found;
        }

        static bool Ignored(string namespaceName)
        {
            for (int i = 0; i < IgnoredNamespaces.Length; i++)
                if (IgnoredNamespaces[i] == namespaceName)
                    return true;
            return false;
        }

        // GenTypes.GetTypeInAnyAssemblyRaw uses Assembly.GetType(..., ignoreCase: true).
        static Type TypeInLoadedAssemblies(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = assemblies[i].GetType(typeName, false, true);
                if (found != null)
                    return found;
            }
            return Type.GetType(typeName, false, true);
        }

        Dictionary<string, HashSet<string>> IndexDefs(string xml)
        {
            Dictionary<string, HashSet<string>> defs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(xml))
                return defs;
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            if (document.DocumentElement == null)
                return defs;
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null || element["defName"] == null)
                    continue;
                if (element.Attributes["Abstract"] != null
                    && string.Equals(element.Attributes["Abstract"].Value, "true", StringComparison.OrdinalIgnoreCase))
                    continue;
                string name = element["defName"].InnerText.Trim();
                TypeResolution resolved = ResolveTypeName(element.Name);
                string typeName = resolved.Kind == ResolutionKind.Def ? resolved.Type.FullName : element.Name;
                if (name.Length == 0)
                    continue;
                HashSet<string> names;
                if (!defs.TryGetValue(typeName, out names))
                {
                    names = new HashSet<string>(StringComparer.Ordinal);
                    defs[typeName] = names;
                }
                names.Add(name);
            }
            return defs;
        }

        static string ChildText(XmlElement element, string name)
        {
            XmlElement child = element[name];
            return child == null ? string.Empty : child.InnerText.Trim();
        }

        static string FirstSegment(string key)
        {
            int dot = key.IndexOf('.');
            string head = (dot < 0 ? key : key.Substring(0, dot)).Trim();
            return head;
        }

        static int LineOf(XmlElement element)
        {
            IXmlLineInfo info = element as IXmlLineInfo;
            return info != null && info.HasLineInfo() ? info.LineNumber : 1;
        }

        enum ResolutionKind { Unresolved, Def, NotDef }

        struct TypeResolution
        {
            public ResolutionKind Kind;
            public Type Type;
            public static TypeResolution Unresolved() { return new TypeResolution { Kind = ResolutionKind.Unresolved }; }
            public static TypeResolution Def(Type type) { return new TypeResolution { Kind = ResolutionKind.Def, Type = type }; }
            public static TypeResolution NotDef(Type type) { return new TypeResolution { Kind = ResolutionKind.NotDef, Type = type }; }
        }
    }
}
