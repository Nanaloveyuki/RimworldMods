using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Oxygendsl.Core;
using RimWorld;
using Verse;

namespace Oxygendsl.Mod.Content
{
    public static class NativeContentPipeline
    {
        static readonly string[] IdentityFields = { "defName", "Name" };

        public static IReadOnlyList<NativeContentChange> LastChanges { get; private set; } = Array.Empty<NativeContentChange>();

        public static IReadOnlyList<NativeRegisteredContent> LastLayout { get; private set; } = Array.Empty<NativeRegisteredContent>();

        public static NativeContentCheckResult Check(
            ContentPlan plan,
            XmlDocument document,
            Dictionary<XmlNode, LoadableXmlAsset> assetlookup,
            ModContentPack provider)
        {
            return Execute(plan, document, assetlookup, provider, false);
        }

        public static NativeContentCheckResult Check(
            ContentPlan plan,
            XmlDocument document,
            IReadOnlyList<string> activePackages,
            bool catalogComplete = false,
            string packageId = "offline")
        {
            NativeContentSchema schema = NativeContentSchema.Shared;
            if (activePackages != null)
                schema.SetActivePackages(activePackages);
            if (catalogComplete)
                schema.MarkComplete();
            return Execute(plan, document, null, packageId ?? "offline", false);
        }

        public static void Apply(
            ContentPlan plan,
            XmlDocument document,
            Dictionary<XmlNode, LoadableXmlAsset> assetlookup,
            ModContentPack provider)
        {
            Execute(plan, document, assetlookup, provider, true);
        }

        static NativeContentCheckResult Execute(
            ContentPlan plan,
            XmlDocument document,
            Dictionary<XmlNode, LoadableXmlAsset> assetlookup,
            ModContentPack provider,
            bool commit)
        {
            return Execute(plan, document, assetlookup, Package(provider), commit, provider);
        }

        static NativeContentCheckResult Execute(
            ContentPlan plan,
            XmlDocument document,
            Dictionary<XmlNode, LoadableXmlAsset> assetlookup,
            string packageId,
            bool commit,
            ModContentPack provider = null)
        {
            if (plan == null)
                throw new NativeContentException("A content plan is required.", 1, 1, packageId ?? string.Empty, string.Empty, string.Empty);
            if (document == null || document.DocumentElement == null)
                throw new NativeContentException("The final Def document is required.", 1, 1, packageId ?? string.Empty, string.Empty, string.Empty);
            packageId = packageId ?? string.Empty;
            NativeContentSchema schema = NativeContentSchema.Shared;
            IndexLiveDocument(schema, document, assetlookup);
            List<NativeContentIssue> issues = new List<NativeContentIssue>();
            Candidate candidate = Build(plan, document, schema, packageId, issues);
            bool success = issues.Count == 0;
            if (commit)
            {
                if (!success)
                    throw FirstError(issues);
                Commit(document, assetlookup, provider, candidate, schema, plan);
                LastChanges = candidate.Changes;
                LastLayout = candidate.Layout;
            }
            return new NativeContentCheckResult(
                success && issues.Count == 0,
                success && issues.Count == 0 ? candidate.Working.OuterXml : string.Empty,
                issues,
                success && issues.Count == 0 ? candidate.Changes : Array.Empty<NativeContentChange>(),
                success && issues.Count == 0 ? candidate.Layout : Array.Empty<NativeRegisteredContent>(),
                candidate.RestartRequired);
        }

        static void IndexLiveDocument(
            NativeContentSchema schema,
            XmlDocument document,
            Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
        {
            if (assetlookup == null)
            {
                schema.LoadDocument(document, string.Empty);
                return;
            }
            Dictionary<string, XmlDocument> byPackage = new Dictionary<string, XmlDocument>(StringComparer.OrdinalIgnoreCase);
            List<string> packages = new List<string>();
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element)
                    continue;
                LoadableXmlAsset asset;
                assetlookup.TryGetValue(node, out asset);
                string owner = asset == null || asset.mod == null || asset.mod.PackageId == null
                    ? string.Empty
                    : asset.mod.PackageId;
                XmlDocument owned;
                if (!byPackage.TryGetValue(owner, out owned))
                {
                    owned = new XmlDocument();
                    owned.AppendChild(owned.CreateElement("Defs"));
                    byPackage[owner] = owned;
                    packages.Add(owner);
                }
                owned.DocumentElement.AppendChild(owned.ImportNode(node, true));
            }
            for (int i = 0; i < packages.Count; i++)
            {
                schema.RemovePackage(packages[i]);
                schema.LoadDocument(byPackage[packages[i]], packages[i]);
                if (!string.IsNullOrEmpty(packages[i]))
                    schema.SetActivePackages(MergePackages(schema.ActivePackages, packages[i]));
            }
        }

        static IReadOnlyList<string> MergePackages(IReadOnlyCollection<string> current, string extra)
        {
            List<string> packages = new List<string>();
            if (current != null)
            {
                foreach (string packageId in current)
                    packages.Add(packageId);
            }
            if (!string.IsNullOrEmpty(extra) && !packages.Contains(extra))
                packages.Add(extra);
            return packages;
        }

        static bool HasErrors(List<NativeContentIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Kind == NativeContentIssueKind.Error)
                    return true;
            }
            return false;
        }

        static NativeContentException FirstError(List<NativeContentIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                NativeContentIssue issue = issues[i];
                if (issue.Kind == NativeContentIssueKind.Error)
                    return new NativeContentException(issue.Message, issue.Line, issue.Column, issue.Provider, issue.Target, issue.Field);
            }
            NativeContentIssue first = issues[0];
            return new NativeContentException(first.Message, first.Line, first.Column, first.Provider, first.Target, first.Field);
        }

        static Candidate Build(
            ContentPlan plan,
            XmlDocument document,
            NativeContentSchema schema,
            string packageId,
            List<NativeContentIssue> issues)
        {
            XmlDocument fragment = new XmlDocument();
            fragment.AppendChild(fragment.CreateElement("Defs"));
            List<NativeContentChange> changes = new List<NativeContentChange>();
            List<PendingNode> pending = new List<PendingNode>();
            HashSet<string> defined = new HashSet<string>(StringComparer.Ordinal);
            int order = 1;
            bool restart = false;

            for (int i = 0; i < plan.Definitions.Count; i++)
            {
                ContentDefinition definition = plan.Definitions[i];
                if (definition == null)
                    throw new NativeContentException("A content definition is missing.", 1, 1, packageId, string.Empty, string.Empty);
                ValidateDefinition(definition, schema, packageId, document, defined);
                XmlElement node = WriteDefinition(fragment, definition, schema, packageId);
                pending.Add(new PendingNode(node, definition.Type, definition.Id, definition.BaseDef));
                defined.Add(Key(definition.Type, definition.Id));
                changes.Add(new NativeContentChange(
                    order++,
                    NativeContentChangeKind.Define,
                    definition.Type,
                    definition.Id,
                    string.Empty,
                    string.Empty,
                    DescribeFields(definition.Fields),
                    packageId));
                restart = true;
            }

            XmlDocument working = CloneDocument(document);
            ImportPending(working, pending);
            MaterializeBases(working, pending, packageId);
            Dictionary<string, List<XmlElement>> index = Index(working);

            for (int i = 0; i < plan.Patches.Count; i++)
            {
                ContentPatch patch = plan.Patches[i];
                if (patch == null)
                    throw new NativeContentException("A content patch is missing.", 1, 1, packageId, string.Empty, string.Empty);
                if (!RequiresActive(schema, patch.Requires))
                    continue;
                ValidatePatchShape(patch, schema, packageId);
                List<XmlElement> targets = FindTargets(index, patch, packageId);
                if (targets.Count != 1)
                {
                    throw Located(patch, packageId, patch.Id, string.Empty,
                        targets.Count == 0
                            ? "Patch target " + patch.Type + " '" + patch.Id + "' was not found in the final XML."
                            : "Patch target " + patch.Type + " '" + patch.Id + "' matched " + targets.Count + " nodes.");
                }
                XmlElement target = targets[0];
                ApplyPatch(working, target, patch, schema, packageId, changes, ref order);
                restart = true;
            }

            List<NativeRegisteredContent> layout = DescribeLayout(working, pending, plan.Patches);
            ValidateReferences(working, schema, packageId, plan, defined, issues);
            foreach (NativeRegisteredContent item in layout)
            {
                if (item.Type == "TraitDef" && item.Degrees.Count == 0)
                    throw new NativeContentException("TraitDef requires at least one degreeDatas item.", 1, 1, packageId, item.Id, "degreeDatas");
                if (item.Type == "TraitDef" && new HashSet<int>(item.Degrees).Count != item.Degrees.Count)
                    throw new NativeContentException("TraitDef degree values must be unique.", 1, 1, packageId, item.Id, "degreeDatas");
                if (item.Type == "ThoughtDef" && item.StageLabels.Count == 0)
                    throw new NativeContentException("ThoughtDef requires at least one stages item.", 1, 1, packageId, item.Id, "stages");
            }
            return new Candidate(fragment, working, changes, layout, restart);
        }

        static void MaterializeBases(XmlDocument working, List<PendingNode> pending, string provider)
        {
            Dictionary<string, PendingNode> candidates = new Dictionary<string, PendingNode>(StringComparer.Ordinal);
            foreach (PendingNode item in pending) candidates.Add(Key(item.Type, item.Id), item);
            Dictionary<string, List<XmlElement>> index = Index(working);
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> complete = new HashSet<string>(StringComparer.Ordinal);
            foreach (PendingNode item in pending) MaterializeBase(working, item, candidates, index, visiting, complete, provider);
        }

        static void MaterializeBase(XmlDocument working, PendingNode item, Dictionary<string, PendingNode> candidates,
            Dictionary<string, List<XmlElement>> index, HashSet<string> visiting, HashSet<string> complete, string provider)
        {
            string key = Key(item.Type, item.Id);
            if (complete.Contains(key)) return;
            if (!visiting.Add(key)) throw new NativeContentException("Cyclic Def inheritance on '" + item.Id + "'.", 1, 1, provider, item.Id, "extends");
            XmlElement target = index[key][0];
            if (item.BaseDef != null)
            {
                PendingNode baseCandidate;
                string baseKey = Key(item.BaseDef.Type, item.BaseDef.Id);
                if (candidates.TryGetValue(baseKey, out baseCandidate))
                    MaterializeBase(working, baseCandidate, candidates, index, visiting, complete, provider);
                List<XmlElement> matches;
                if (!index.TryGetValue(baseKey, out matches) || matches.Count != 1)
                    throw new NativeContentException("Native base '" + item.BaseDef.Id + "' must resolve to exactly one Def by defName.", 1, 1, provider, item.Id, "extends");
                XmlElement clone = (XmlElement)working.ImportNode(ResolveInheritance(working, matches[0]), true);
                clone.Attributes.RemoveAll();
                // DSL fields override base fields; lists replace rather than concatenate.
                foreach (XmlNode child in target.ChildNodes)
                {
                    XmlElement field = child as XmlElement;
                    if (field == null) continue;
                    XmlElement inherited = clone[field.Name];
                    if (inherited != null) clone.RemoveChild(inherited);
                    clone.AppendChild(working.ImportNode(field, true));
                }
                target.ParentNode.ReplaceChild(clone, target);
                target = clone;
                index[key][0] = clone;
            }
            double duration;
            bool memory = target["durationDays"] != null && double.TryParse(target["durationDays"].InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out duration) && duration > 0;
            if (item.Type == "ThoughtDef" && target["workerClass"] == null && target["thoughtClass"] == null && !memory)
            {
                XmlElement worker = working.CreateElement("workerClass");
                worker.InnerText = NativeContentSchema.DefaultThoughtWorker;
                target.AppendChild(worker);
            }
            visiting.Remove(key);
            complete.Add(key);
        }

        static void ValidateDefinition(
            ContentDefinition definition,
            NativeContentSchema schema,
            string packageId,
            XmlDocument document,
            HashSet<string> defined)
        {
            if (string.IsNullOrEmpty(definition.Type) || string.IsNullOrEmpty(definition.Id))
            {
                throw Located(definition.Line, definition.Column, packageId, definition.Id, string.Empty,
                    "A content definition needs a type and defName.");
            }
            Type clr;
            if (!schema.TryGetDefType(definition.Type, out clr))
            {
                throw Located(definition.Line, definition.Column, packageId, definition.Id, "type",
                    "Content type '" + definition.Type + "' is not registered.");
            }
            if (!defined.Add(Key(definition.Type, definition.Id)) || CountNodes(document, definition.Type, definition.Id) > 0)
            {
                defined.Remove(Key(definition.Type, definition.Id));
                throw Located(definition.Line, definition.Column, packageId, definition.Id, "defName",
                    "Duplicate " + definition.Type + " '" + definition.Id + "'.");
            }
            schema.CheckFields(definition.Type, definition.Fields, definition.Line, definition.Column, packageId, definition.Id, string.Empty);
            if (definition.BaseDef != null)
            {
                if (!string.Equals(definition.BaseDef.Type, definition.Type, StringComparison.Ordinal))
                {
                    throw Located(definition.Line, definition.Column, packageId, definition.Id, "extends",
                        "Native base '" + definition.BaseDef.Id + "' is " + definition.BaseDef.Type + ", not " + definition.Type + ".");
                }
                // Def references resolve by defName after all candidate definitions are present.
            }
            if (string.Equals(definition.Type, "ThoughtDef", StringComparison.Ordinal))
                EnsureThoughtWorker(definition, schema, packageId);
        }

        static bool NeedsDefaultWorker(ContentDefinition definition)
        {
            if (definition == null || !string.Equals(definition.Type, "ThoughtDef", StringComparison.Ordinal))
                return false;
            object thoughtClass;
            object workerClass;
            bool hasThought = TryField(definition.Fields, "thoughtClass", out thoughtClass) && thoughtClass != null;
            bool hasWorker = TryField(definition.Fields, "workerClass", out workerClass) && workerClass != null;
            return !hasThought && !hasWorker && !IsMemory(definition.Fields);
        }

        static void EnsureThoughtWorker(ContentDefinition definition, NativeContentSchema schema, string packageId)
        {
            if (!NeedsDefaultWorker(definition))
                return;
            NativeTypeReference worker = new NativeTypeReference();
            worker.Name = NativeContentSchema.DefaultThoughtWorker;
            schema.CheckFields(
                "ThoughtDef",
                new Dictionary<string, object> { { "workerClass", worker } },
                definition.Line,
                definition.Column,
                packageId,
                definition.Id,
                string.Empty);
        }

        static bool IsMemory(IDictionary<string, object> fields)
        {
            object duration;
            if (fields != null && fields.TryGetValue("durationDays", out duration) && IsPositive(duration))
                return true;
            object thoughtClass;
            if (fields != null && fields.TryGetValue("thoughtClass", out thoughtClass))
            {
                NativeTypeReference reference = thoughtClass as NativeTypeReference;
                if (reference != null)
                {
                    Type resolved = NativeContentSchema.ResolveNativeType(reference.Name);
                    return resolved != null && typeof(Thought_Memory).IsAssignableFrom(resolved);
                }
            }
            return false;
        }

        static XmlElement WriteDefinition(XmlDocument fragment, ContentDefinition definition, NativeContentSchema schema, string packageId)
        {
            XmlElement node = fragment.CreateElement(definition.Type);
            // Native Def inheritance is flattened from the referenced defName, never ParentName.
            XmlElement defName = fragment.CreateElement("defName");
            defName.InnerText = definition.Id;
            node.AppendChild(defName);
            WriteFields(fragment, node, definition.Type, definition.Fields, schema);
            if (definition.BaseDef == null && NeedsDefaultWorker(definition))
            {
                XmlElement worker = fragment.CreateElement("workerClass");
                worker.InnerText = NativeContentSchema.DefaultThoughtWorker;
                node.AppendChild(worker);
            }
            fragment.DocumentElement.AppendChild(node);
            return node;
        }

        static void WriteFields(
            XmlDocument document,
            XmlElement parent,
            string typeName,
            IDictionary<string, object> fields,
            NativeContentSchema schema)
        {
            if (fields == null)
                return;
            IReadOnlyDictionary<string, NativeFieldMapping> mappings;
            schema.TryGetFields(typeName, out mappings);
            foreach (KeyValuePair<string, object> pair in fields)
            {
                if (string.Equals(pair.Key, "defName", StringComparison.Ordinal) || pair.Value == null)
                    continue;
                NativeFieldMapping mapping;
                if (mappings == null || !mappings.TryGetValue(pair.Key, out mapping))
                    continue;
                XmlElement child = document.CreateElement(pair.Key);
                WriteValue(document, child, mapping, pair.Value, schema);
                parent.AppendChild(child);
            }
        }

        static void WriteValue(
            XmlDocument document,
            XmlElement parent,
            NativeFieldMapping mapping,
            object value,
            NativeContentSchema schema)
        {
            if (mapping.Kind == NativeFieldKind.List)
            {
                IList list = value as IList;
                if (list == null)
                    return;
                bool custom = mapping.CustomNamedValue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == null)
                        continue;
                    if (custom)
                    {
                        XmlElement named = WriteCustom(document, mapping.ElementType, list[i]);
                        if (named != null)
                            parent.AppendChild(named);
                    }
                    else
                    {
                        XmlElement item = document.CreateElement("li");
                        WriteItem(document, item, mapping.ElementType, list[i], schema);
                        parent.AppendChild(item);
                    }
                }
                return;
            }
            if (mapping.Kind == NativeFieldKind.CustomNamedValue)
            {
                XmlElement named = WriteCustom(document, mapping.ValueType, value);
                if (named != null)
                {
                    while (named.FirstChild != null)
                        parent.AppendChild(named.FirstChild);
                }
                return;
            }
            WriteItem(document, parent, mapping.ValueType, value, schema);
        }

        static void WriteItem(XmlDocument document, XmlElement parent, Type type, object value, NativeContentSchema schema)
        {
            DefReference reference = value as DefReference;
            if (reference != null)
            {
                parent.InnerText = reference.Id;
                if (!string.IsNullOrEmpty(reference.PackageId))
                {
                    XmlAttribute mayRequire = document.CreateAttribute("MayRequire");
                    mayRequire.Value = reference.PackageId;
                    parent.Attributes.Append(mayRequire);
                }
                return;
            }
            NativeTypeReference nativeType = value as NativeTypeReference;
            if (nativeType != null)
            {
                parent.InnerText = nativeType.Name;
                return;
            }
            IDictionary<string, object> fields = value as IDictionary<string, object>;
            if (fields != null)
            {
                WriteFields(document, parent, type == null ? string.Empty : type.Name, fields, schema);
                return;
            }
            IList list = value as IList;
            if (list != null && !(value is string))
            {
                if (type != null && type.IsEnum)
                {
                    parent.InnerText = string.Join(", ", System.Linq.Enumerable.Cast<string>(list));
                    return;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    XmlElement item = document.CreateElement("li");
                    item.InnerText = FormatScalar(list[i]);
                    parent.AppendChild(item);
                }
                return;
            }
            parent.InnerText = FormatScalar(value);
        }

        static XmlElement WriteCustom(XmlDocument document, Type type, object value)
        {
            IDictionary<string, object> fields = value as IDictionary<string, object>;
            DefReference reference = value as DefReference;
            if (type != null && type.Name == "DefHyperlink")
            {
                if (reference == null && fields != null)
                    reference = FirstReference(fields);
                if (reference == null)
                    return null;
                XmlElement link = document.CreateElement(reference.Type);
                link.InnerText = reference.Id;
                return link;
            }
            if (fields == null)
                return null;
            DefReference name = FirstReference(fields, "stat", "skill", "def", "trait", "key", "thing");
            object number = First(fields, "value", "amount", "level");
            if (name == null)
                return null;
            XmlElement element = document.CreateElement(name.Id);
            if (number != null)
                element.InnerText = FormatScalar(number);
            if (!string.IsNullOrEmpty(name.PackageId))
            {
                XmlAttribute mayRequire = document.CreateAttribute("MayRequire");
                mayRequire.Value = name.PackageId;
                element.Attributes.Append(mayRequire);
            }
            object degree = First(fields, "degree");
            if (degree != null && type != null && type.Name == "TraitRequirement")
            {
                element.InnerText = FormatScalar(degree);
            }
            return element;
        }

        static void ValidatePatchShape(ContentPatch patch, NativeContentSchema schema, string packageId)
        {
            if (string.IsNullOrEmpty(patch.Type) || string.IsNullOrEmpty(patch.Id))
            {
                throw Located(patch, packageId, patch.Id, string.Empty, "A patch needs a type and target defName.");
            }
            Type clr;
            if (!schema.TryGetDefType(patch.Type, out clr))
            {
                throw Located(patch, packageId, patch.Id, "type", "Patch type '" + patch.Type + "' is not registered.");
            }
            RejectIdentity(patch.Set, patch, packageId);
            RejectIdentity(patch.Append, patch, packageId);
            RejectIdentity(patch.Remove, patch, packageId);
            if (patch.Set != null)
                foreach (KeyValuePair<string, object> pair in patch.Set)
                    schema.CheckPath(patch.Type, pair.Key, pair.Value, patch.Line, patch.Column, packageId, patch.Id, false);
            CheckPatchCollections(patch.Append, patch, schema, packageId, true);
            CheckPatchCollections(patch.Remove, patch, schema, packageId, false);
        }

        static void CheckPatchCollections(
            IDictionary<string, object> operations,
            ContentPatch patch,
            NativeContentSchema schema,
            string packageId,
            bool append)
        {
            if (operations == null)
                return;
            foreach (KeyValuePair<string, object> pair in operations)
            {
                if (!(pair.Value is IList) && !(pair.Value is IDictionary))
                {
                    throw Located(patch, packageId, patch.Id, pair.Key,
                        (append ? "append" : "remove") + " of '" + pair.Key + "' must be a list or nested field path.");
                }
                schema.CheckPath(patch.Type, pair.Key, pair.Value, patch.Line, patch.Column, packageId, patch.Id, true);
            }
        }

        static void RejectIdentity(IDictionary<string, object> fields, ContentPatch patch, string packageId)
        {
            if (fields == null)
                return;
            for (int i = 0; i < IdentityFields.Length; i++)
            {
                if (fields.ContainsKey(IdentityFields[i]))
                {
                    throw Located(patch, packageId, patch.Id, IdentityFields[i],
                        "Field '" + IdentityFields[i] + "' changes content identity and cannot be patched.");
                }
            }
        }

        static void ApplyPatch(
            XmlDocument working,
            XmlElement target,
            ContentPatch patch,
            NativeContentSchema schema,
            string packageId,
            List<NativeContentChange> changes,
            ref int order)
        {
            if (patch.Set != null)
            {
                foreach (KeyValuePair<string, object> pair in patch.Set)
                {
                    XmlElement resolved = ResolveInheritance(working, target);
                    string before = ReadPath(resolved, pair.Key);
                    Materialize(working, target, pair.Key, resolved);
                    SetPath(working, target, pair.Key, pair.Value, schema, patch.Type);
                    changes.Add(new NativeContentChange(order++, NativeContentChangeKind.Set, patch.Type, patch.Id, pair.Key, before, FormatChange(pair.Value), packageId));
                }
            }
            if (patch.Append != null)
            {
                foreach (KeyValuePair<string, object> pair in patch.Append)
                {
                    XmlElement resolved = ResolveInheritance(working, target);
                    string before = ReadPath(resolved, pair.Key);
                    Materialize(working, target, pair.Key, resolved);
                    AppendPath(working, target, pair.Key, pair.Value, schema, patch.Type);
                    changes.Add(new NativeContentChange(order++, NativeContentChangeKind.Append, patch.Type, patch.Id, pair.Key, before, FormatChange(pair.Value), packageId));
                }
            }
            if (patch.Remove != null)
            {
                foreach (KeyValuePair<string, object> pair in patch.Remove)
                {
                    XmlElement resolved = ResolveInheritance(working, target);
                    string before = ReadPath(resolved, pair.Key);
                    Materialize(working, target, pair.Key, resolved);
                    int removed = RemovePath(target, pair.Key, pair.Value);
                    if (removed == 0)
                    {
                        throw Located(patch, packageId, patch.Id, pair.Key,
                            "remove of '" + pair.Key + "' matched no existing value.");
                    }
                    changes.Add(new NativeContentChange(order++, NativeContentChangeKind.Remove, patch.Type, patch.Id, pair.Key, before, FormatChange(pair.Value), packageId));
                }
            }
        }

        static void Materialize(XmlDocument document, XmlElement target, string path, XmlElement resolved)
        {
            if (target.Attributes["ParentName"] == null)
                return;
            string[] parts = Split(path);
            if (parts.Length == 0)
                return;
            XmlElement inherited = Child(resolved, parts[0]);
            if (inherited == null)
                return;
            XmlElement own = Child(target, parts[0]);
            if (own != null) target.RemoveChild(own);
            XmlElement effective = (XmlElement)document.ImportNode(inherited, true);
            effective.SetAttribute("Inherit", "False");
            target.AppendChild(effective);
        }


        static void SetPath(XmlDocument document, XmlElement root, string path, object value, NativeContentSchema schema, string typeName)
        {
            string[] parts = Split(path);
            XmlElement parent = EnsureParent(document, root, parts, parts.Length - 1);
            string leaf = parts[parts.Length - 1];
            int index;
            if (int.TryParse(leaf, NumberStyles.None, CultureInfo.InvariantCulture, out index))
            {
                XmlElement list = parent;
                XmlElement item = ListItem(list, index, false);
                if (item == null)
                    throw new NativeContentException("List index " + leaf + " does not exist.", 1, 1, string.Empty, root["defName"] == null ? string.Empty : root["defName"].InnerText, path);
                NativeFieldMapping itemMapping = Mapping(schema, typeName, parts);
                if (itemMapping.CustomNamedValue)
                {
                    XmlElement replacement = WriteCustom(document, itemMapping.ValueType, value);
                    item.ParentNode.ReplaceChild(replacement, item);
                }
                else ReplaceElement(document, item, value, schema, itemMapping.ValueType);
                return;
            }
            if (parts.Length > 1)
            {
                NativeFieldMapping parentMapping = Mapping(schema, typeName, path.Substring(0, path.LastIndexOf('.')).Split('.'));
                if (parentMapping.Kind == NativeFieldKind.CustomNamedValue)
                {
                    if (value is DefReference)
                    {
                        XmlElement replacement = document.CreateElement(((DefReference)value).Id);
                        replacement.InnerText = parent.InnerText;
                        parent.ParentNode.ReplaceChild(replacement, parent);
                    }
                    else parent.InnerText = FormatScalar(value);
                    return;
                }
            }
            XmlElement existing = Child(parent, leaf);
            if (existing != null)
                parent.RemoveChild(existing);
            XmlElement created = document.CreateElement(leaf);
            NativeFieldMapping mapping = Mapping(schema, typeName, parts);
            if (mapping != null)
                WriteValue(document, created, mapping, value, schema);
            else
                created.InnerText = FormatScalar(value);
            parent.AppendChild(created);
        }

        static void AppendPath(XmlDocument document, XmlElement root, string path, object value, NativeContentSchema schema, string typeName)
        {
            string[] parts = Split(path);
            XmlElement list = EnsureParent(document, root, parts, parts.Length);
            IList items = value as IList;
            if (items == null)
                items = new object[] { value };
            NativeFieldMapping mapping = Mapping(schema, typeName, parts);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null || ContainsValue(list, items[i]))
                    continue;
                if (mapping != null && mapping.CustomNamedValue)
                {
                    XmlElement named = WriteCustom(document, mapping.ElementType, items[i]);
                    if (named != null)
                        list.AppendChild(named);
                }
                else
                {
                    XmlElement item = document.CreateElement("li");
                    WriteItem(document, item, mapping == null ? null : mapping.ElementType, items[i], schema);
                    list.AppendChild(item);
                }
            }
        }

        static int RemovePath(XmlElement root, string path, object value)
        {
            string[] parts = Split(path);
            XmlElement list = Walk(root, parts, parts.Length);
            if (list == null)
                return 0;
            IList items = value as IList;
            if (items == null)
                items = new object[] { value };
            int removed = 0;
            for (int i = 0; i < items.Count; i++)
            {
                XmlElement match = FindChildValue(list, items[i]);
                if (match == null)
                    continue;
                list.RemoveChild(match);
                removed++;
            }
            return removed;
        }

        static void ReplaceElement(XmlDocument document, XmlElement item, object value, NativeContentSchema schema, Type type)
        {
            while (item.FirstChild != null)
                item.RemoveChild(item.FirstChild);
            item.InnerText = string.Empty;
            WriteItem(document, item, type, value, schema);
        }

        static XmlElement EnsureParent(XmlDocument document, XmlElement root, string[] parts, int count)
        {
            XmlElement current = root;
            for (int i = 0; i < count; i++)
            {
                int index;
                if (int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out index))
                {
                    XmlElement item = ListItem(current, index, false);
                    if (item == null)
                        throw new NativeContentException("List index " + parts[i] + " does not exist.", 1, 1, string.Empty, string.Empty, string.Join(".", parts));
                    current = item;
                    continue;
                }
                XmlElement child = Child(current, parts[i]);
                if (child == null)
                {
                    child = document.CreateElement(parts[i]);
                    current.AppendChild(child);
                }
                current = child;
            }
            return current;
        }

        static XmlElement Walk(XmlElement root, string[] parts, int count)
        {
            XmlElement current = root;
            for (int i = 0; i < count && current != null; i++)
            {
                int index;
                if (int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out index))
                    current = ListItem(current, index, false);
                else
                    current = Child(current, parts[i]);
            }
            return current;
        }

        static string ReadPath(XmlElement root, string path)
        {
            string[] parts = Split(path);
            XmlElement node = Walk(root, parts, parts.Length);
            if (node == null && parts.Length > 1)
            {
                XmlElement parent = Walk(root, parts, parts.Length - 1);
                if (parent != null && parent.ChildNodes.Count == 1 && parent.FirstChild is XmlText)
                    return parent.InnerText;
            }
            return node == null ? string.Empty : node.InnerXml;
        }

        static void ValidateReferences(
            XmlDocument working,
            NativeContentSchema schema,
            string packageId,
            ContentPlan plan,
            HashSet<string> defined,
            List<NativeContentIssue> issues)
        {
            for (int i = 0; i < plan.Definitions.Count; i++)
            {
                ContentDefinition definition = plan.Definitions[i];
                if (definition == null)
                    continue;
                ValidateValueReferences(definition.Fields, definition.Line, definition.Column, packageId, definition.Id, schema, working, defined, issues);
                if (definition.BaseDef != null)
                    ResolveReference(definition.BaseDef, definition.Line, definition.Column, packageId, definition.Id, schema, working, defined, issues);
            }
            for (int i = 0; i < plan.Patches.Count; i++)
            {
                ContentPatch patch = plan.Patches[i];
                if (patch == null || !RequiresActive(schema, patch.Requires))
                    continue;
                ValidateValueReferences(patch.Set, patch.Line, patch.Column, packageId, patch.Id, schema, working, defined, issues);
                ValidateValueReferences(patch.Append, patch.Line, patch.Column, packageId, patch.Id, schema, working, defined, issues);
            }
            foreach (ThoughtRuleDefinition rule in plan.Thoughts)
                ResolveReference(rule.Thought, rule.Line, rule.Column, packageId, rule.Id, schema, working, defined, issues);
        }

        static void ValidateValueReferences(
            object value,
            int line,
            int column,
            string provider,
            string target,
            NativeContentSchema schema,
            XmlDocument working,
            HashSet<string> defined,
            List<NativeContentIssue> issues)
        {
            DefReference reference = value as DefReference;
            if (reference != null)
            {
                ResolveReference(reference, line, column, provider, target, schema, working, defined, issues);
                return;
            }
            IDictionary<string, object> fields = value as IDictionary<string, object>;
            if (fields != null)
            {
                foreach (object child in fields.Values)
                    ValidateValueReferences(child, line, column, provider, target, schema, working, defined, issues);
                return;
            }
            IList list = value as IList;
            if (list != null && !(value is string))
            {
                for (int i = 0; i < list.Count; i++)
                    ValidateValueReferences(list[i], line, column, provider, target, schema, working, defined, issues);
            }
        }

        static void ResolveReference(
            DefReference reference,
            int line,
            int column,
            string provider,
            string target,
            NativeContentSchema schema,
            XmlDocument working,
            HashSet<string> defined,
            List<NativeContentIssue> issues)
        {
            if (string.IsNullOrEmpty(reference.Type) || string.IsNullOrEmpty(reference.Id))
                return;
            if (defined.Contains(Key(reference.Type, reference.Id)) && string.Equals(reference.PackageId, provider, StringComparison.OrdinalIgnoreCase))
                return;
            if (string.IsNullOrEmpty(reference.PackageId)
                && (defined.Contains(Key(reference.Type, reference.Id)) || CountNodes(working, reference.Type, reference.Id) > 0))
                return;
            NativeDefResolution resolution = schema.ResolveDef(reference.Type, reference.Id, reference.PackageId);
            if (resolution == NativeDefResolution.Found)
                return;
            if (!schema.IsComplete || resolution == NativeDefResolution.Unknown)
            {
                issues.Add(new NativeContentIssue(
                    "UNKNOWN: " + reference.Type + " '" + reference.Id + "' cannot be confirmed. " + schema.IncompleteReason,
                    line, column, provider, target, reference.Type, NativeContentIssueKind.Unknown));
                return;
            }
            issues.Add(new NativeContentIssue(
                "Missing " + reference.Type + " '" + reference.Id + "'.",
                line, column, provider, target, reference.Type, NativeContentIssueKind.Error));
        }

        static List<NativeRegisteredContent> DescribeLayout(
            XmlDocument working,
            List<PendingNode> pending,
            IReadOnlyList<ContentPatch> patches)
        {
            List<NativeRegisteredContent> layout = new List<NativeRegisteredContent>();
            HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < pending.Count; i++)
                wanted.Add(Key(pending[i].Type, pending[i].Id));
            if (patches != null)
            {
                for (int i = 0; i < patches.Count; i++)
                {
                    if (patches[i] != null)
                        wanted.Add(Key(patches[i].Type, patches[i].Id));
                }
            }
            foreach (XmlNode node in working.DocumentElement.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null || element["defName"] == null)
                    continue;
                string id = element["defName"].InnerText.Trim();
                if (!wanted.Contains(Key(element.Name, id)))
                    continue;
                XmlElement resolved = ResolveInheritance(working, element);
                layout.Add(Describe(resolved, element.Name, id));
            }
            return layout;
        }

        static NativeRegisteredContent Describe(XmlElement resolved, string type, string id)
        {
            List<int> degrees = new List<int>();
            XmlElement degreeDatas = resolved["degreeDatas"];
            if (degreeDatas != null)
            {
                foreach (XmlNode child in degreeDatas.ChildNodes)
                {
                    XmlElement item = child as XmlElement;
                    if (item == null) continue;
                    int degree = 0;
                    if (item["degree"] != null && !int.TryParse(item["degree"].InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out degree))
                        throw new NativeContentException("Invalid TraitDef degree.", 1, 1, string.Empty, id, "degreeDatas.degree");
                    degrees.Add(degree);
                }
            }
            List<string> stages = new List<string>();
            XmlElement stageNode = resolved["stages"];
            if (stageNode != null)
            {
                foreach (XmlNode child in stageNode.ChildNodes)
                {
                    XmlElement item = child as XmlElement;
                    if (item == null)
                        continue;
                    stages.Add(item["label"] == null ? string.Empty : item["label"].InnerText);
                }
            }
            bool isAbstract = resolved.Attributes["Abstract"] != null
                && string.Equals(resolved.Attributes["Abstract"].Value, "true", StringComparison.OrdinalIgnoreCase);
            string parent = resolved.Attributes["ParentName"] == null ? string.Empty : resolved.Attributes["ParentName"].Value;
            return new NativeRegisteredContent(
                type,
                id,
                degrees,
                stages,
                Text(resolved, "workerClass"),
                Text(resolved, "thoughtClass"),
                isAbstract,
                parent);
        }

        static XmlElement ResolveInheritance(XmlDocument document, XmlElement node)
        {
            return ResolveInheritance(document, node, new HashSet<XmlElement>());
        }

        static XmlElement ResolveInheritance(XmlDocument document, XmlElement node, HashSet<XmlElement> stack)
        {
            XmlAttribute parentName = node.Attributes["ParentName"];
            if (parentName == null || string.IsNullOrEmpty(parentName.Value))
                return (XmlElement)node.CloneNode(true);
            if (!stack.Add(node))
            {
                throw new NativeContentException(
                    "Cyclic XML parent '" + parentName.Value + "' on " + node.Name + ".",
                    1, 1, string.Empty, Text(node, "defName"), "ParentName");
            }
            XmlElement parent = FindParent(document, node.Name, parentName.Value);
            if (parent == null)
            {
                throw new NativeContentException(
                    "Required XML parent '" + parentName.Value + "' was not found for " + node.Name + ".",
                    1, 1, string.Empty, Text(node, "defName"), "ParentName");
            }
            XmlElement clone = ResolveInheritance(document, parent, stack);
            stack.Remove(node);
            CopyAttributes(node, clone);
            Merge(node, clone);
            return clone;
        }

        static void CopyAttributes(XmlElement child, XmlElement current)
        {
            current.Attributes.RemoveAll();
            foreach (XmlAttribute attribute in child.Attributes)
            {
                XmlAttribute copy = current.OwnerDocument.CreateAttribute(attribute.Name);
                copy.Value = attribute.Value;
                current.Attributes.Append(copy);
            }
        }

        static void Merge(XmlElement child, XmlElement current)
        {
            XmlAttribute inherit = child.Attributes["Inherit"];
            if (inherit != null && string.Equals(inherit.Value, "false", StringComparison.OrdinalIgnoreCase))
            {
                while (current.FirstChild != null)
                    current.RemoveChild(current.FirstChild);
                foreach (XmlNode item in child.ChildNodes)
                    current.AppendChild(current.OwnerDocument.ImportNode(item, true));
                return;
            }
            bool scalar = true;
            foreach (XmlNode item in child.ChildNodes) if (item is XmlElement) { scalar = false; break; }
            if (scalar)
            {
                current.InnerText = child.InnerText;
                return;
            }
            foreach (XmlNode item in child.ChildNodes)
            {
                XmlElement element = item as XmlElement;
                if (element == null)
                    continue;
                if (element.Name == "li" || IsDuplicateAllowed(element))
                {
                    current.AppendChild(current.OwnerDocument.ImportNode(element, true));
                    continue;
                }
                XmlElement existing = current[element.Name];
                if (existing == null)
                    current.AppendChild(current.OwnerDocument.ImportNode(element, true));
                else
                    Merge(element, existing);
            }
        }

        static bool IsDuplicateAllowed(XmlElement element)
        {
            return element.ParentNode != null
                && (element.ParentNode.Name == "descriptionHyperlinks" || element.ParentNode.Name == "comps");
        }

        static XmlElement FindParent(XmlDocument document, string type, string name)
        {
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null || element.Name != type || element.Attributes["Name"] == null)
                    continue;
                if (string.Equals(element.Attributes["Name"].Value, name, StringComparison.Ordinal))
                    return element;
            }
            return null;
        }

        static bool ParentExists(XmlDocument document, string type, string name)
        {
            return FindParent(document, type, name) != null;
        }

        static void Commit(
            XmlDocument document,
            Dictionary<XmlNode, LoadableXmlAsset> assetlookup,
            ModContentPack provider,
            Candidate candidate,
            NativeContentSchema schema,
            ContentPlan plan)
        {
            if (assetlookup == null)
                throw new NativeContentException("The Def asset lookup is required.", 1, 1, Package(provider), string.Empty, string.Empty);
            LoadableXmlAsset asset = null;
            if (plan.Definitions.Count != 0)
            {
                string directory = Path.Combine(Path.GetTempPath(), "Oxygendsl", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Oxygen_content.oxy.xml");
                try
                {
                    File.WriteAllText(path, candidate.Fragment.OuterXml);
                    asset = new LoadableXmlAsset(new FileInfo(path), provider);
                    if (asset.xmlDoc == null) throw new NativeContentException("Generated XML could not be loaded.", 1, 1, Package(provider), string.Empty, string.Empty);
                }
                finally { Directory.Delete(directory, true); }
            }
            HashSet<string> defined = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Definitions.Count; i++)
            {
                if (plan.Definitions[i] != null)
                    defined.Add(Key(plan.Definitions[i].Type, plan.Definitions[i].Id));
            }
            List<XmlNode> originals = new List<XmlNode>();
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
                originals.Add(node);
            for (int i = 0; i < originals.Count; i++)
                document.DocumentElement.RemoveChild(originals[i]);
            int originalIndex = 0;
            foreach (XmlNode node in candidate.Working.DocumentElement.ChildNodes)
            {
                XmlNode imported = document.ImportNode(node, true);
                document.DocumentElement.AppendChild(imported);
                XmlElement element = imported as XmlElement;
                if (element != null && element["defName"] != null && defined.Contains(Key(element.Name, element["defName"].InnerText.Trim())))
                    assetlookup[imported] = asset;
                else if (originalIndex < originals.Count)
                {
                    LoadableXmlAsset originalAsset;
                    if (assetlookup.TryGetValue(originals[originalIndex], out originalAsset)) assetlookup[imported] = originalAsset;
                }
                originalIndex++;
            }
            foreach (XmlNode original in originals) assetlookup.Remove(original);
        }

        static void ImportPending(XmlDocument working, List<PendingNode> pending)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                XmlNode imported = working.ImportNode(pending[i].Node, true);
                working.DocumentElement.AppendChild(imported);
            }
        }

        static XmlDocument CloneDocument(XmlDocument document)
        {
            XmlDocument clone = new XmlDocument();
            clone.LoadXml(document.OuterXml);
            return clone;
        }

        static Dictionary<string, List<XmlElement>> Index(XmlDocument document)
        {
            Dictionary<string, List<XmlElement>> index = new Dictionary<string, List<XmlElement>>(StringComparer.Ordinal);
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null || element["defName"] == null)
                    continue;
                string key = Key(element.Name, element["defName"].InnerText.Trim());
                List<XmlElement> list;
                if (!index.TryGetValue(key, out list))
                {
                    list = new List<XmlElement>();
                    index[key] = list;
                }
                list.Add(element);
            }
            return index;
        }

        static List<XmlElement> FindTargets(Dictionary<string, List<XmlElement>> index, ContentPatch patch, string packageId)
        {
            List<XmlElement> found;
            if (!index.TryGetValue(Key(patch.Type, patch.Id), out found))
                return new List<XmlElement>();
            return found;
        }

        static int CountNodes(XmlDocument document, string type, string id)
        {
            if (document == null || document.DocumentElement == null)
                return 0;
            int count = 0;
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null || element.Name != type || element["defName"] == null)
                    continue;
                if (string.Equals(element["defName"].InnerText.Trim(), id, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        static bool RequiresActive(NativeContentSchema schema, IReadOnlyList<string> requires)
        {
            if (requires == null)
                return true;
            for (int i = 0; i < requires.Count; i++)
            {
                if (!schema.PackageActive(requires[i]))
                    return false;
            }
            return true;
        }

        static bool ContainsValue(XmlElement list, object value)
        {
            return FindChildValue(list, value) != null;
        }

        static XmlElement FindChildValue(XmlElement list, object value)
        {
            string expected = ValueText(value);
            foreach (XmlNode child in list.ChildNodes)
            {
                XmlElement element = child as XmlElement;
                if (element == null)
                    continue;
                if (string.Equals(element.InnerText.Trim(), expected, StringComparison.Ordinal)
                    || string.Equals(element.Name, expected, StringComparison.Ordinal))
                    return element;
            }
            return null;
        }

        static string ValueText(object value)
        {
            DefReference reference = value as DefReference;
            if (reference != null)
                return reference.Id;
            IDictionary<string, object> fields = value as IDictionary<string, object>;
            if (fields != null)
            {
                DefReference name = FirstReference(fields, "stat", "skill", "def", "trait", "key", "thing");
                if (name != null)
                    return name.Id;
            }
            return FormatScalar(value);
        }

        static XmlElement ListItem(XmlElement list, int index, bool create)
        {
            int seen = 0;
            foreach (XmlNode child in list.ChildNodes)
            {
                XmlElement element = child as XmlElement;
                if (element == null)
                    continue;
                if (seen == index)
                    return element;
                seen++;
            }
            return null;
        }

        static XmlElement Child(XmlElement parent, string name)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                XmlElement element = child as XmlElement;
                if (element != null && element.Name == name)
                    return element;
            }
            return null;
        }

        static NativeFieldMapping Mapping(NativeContentSchema schema, string typeName, string[] parts)
        {
            return schema.PathMapping(typeName, string.Join(".", parts), 1, 1, string.Empty, string.Empty);
        }


        static string[] Split(string path)
        {
            return (path ?? string.Empty).Split('.');
        }

        static bool TryField(IDictionary<string, object> fields, string name, out object value)
        {
            value = null;
            return fields != null && fields.TryGetValue(name, out value);
        }

        static bool IsPositive(object value)
        {
            if (value is int)
                return (int)value > 0;
            if (value is float)
                return (float)value > 0f;
            if (value is double)
                return (double)value > 0d;
            return false;
        }

        static string FormatScalar(object value)
        {
            if (value == null)
                return string.Empty;
            if (value is bool)
                return (bool)value ? "true" : "false";
            if (value is float)
                return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            if (value is double)
                return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            if (value is IFormattable)
                return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
            return value.ToString();
        }
        static string FormatChange(object value)
        {
            DefReference reference = value as DefReference;
            if (reference != null)
                return reference.Type + ":" + reference.Id;
            NativeTypeReference nativeType = value as NativeTypeReference;
            if (nativeType != null)
                return nativeType.Name;
            IList list = value as IList;
            if (list != null && !(value is string))
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < list.Count; i++)
                {
                    if (builder.Length > 0)
                        builder.Append(", ");
                    builder.Append(FormatChange(list[i]));
                }
                return builder.ToString();
            }
            IDictionary<string, object> fields = value as IDictionary<string, object>;
            if (fields != null)
                return DescribeFields(fields);
            return FormatScalar(value);
        }


        static string DescribeFields(IDictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0)
                return string.Empty;
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, object> pair in fields)
            {
                if (builder.Length > 0)
                    builder.Append("; ");
                builder.Append(pair.Key);
                builder.Append('=');
                builder.Append(FormatChange(pair.Value));
            }
            return builder.ToString();
        }

        static string Text(XmlElement element, string child)
        {
            return element[child] == null ? string.Empty : element[child].InnerText.Trim();
        }

        static object First(IDictionary<string, object> fields, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                object value;
                if (fields.TryGetValue(names[i], out value))
                    return value;
            }
            return null;
        }

        static DefReference FirstReference(IDictionary<string, object> fields, params string[] names)
        {
            if (names.Length == 0)
            {
                foreach (object value in fields.Values)
                {
                    DefReference reference = value as DefReference;
                    if (reference != null)
                        return reference;
                }
                return null;
            }
            return First(fields, names) as DefReference;
        }

        static string Package(ModContentPack provider)
        {
            return provider == null || provider.PackageId == null ? string.Empty : provider.PackageId;
        }

        static string Key(string type, string id)
        {
            return (type ?? string.Empty) + "\n" + (id ?? string.Empty);
        }

        static NativeContentException Located(ContentPatch patch, string provider, string target, string field, string message)
        {
            return Located(patch == null ? 1 : patch.Line, patch == null ? 1 : patch.Column, provider, target, field, message);
        }

        static NativeContentException Located(int line, int column, string provider, string target, string field, string message)
        {
            return new NativeContentException(message, line, column, provider, target, field);
        }

        sealed class PendingNode
        {
            public PendingNode(XmlElement node, string type, string id, DefReference baseDef)
            {
                Node = node;
                Type = type;
                Id = id;
                BaseDef = baseDef;
            }

            public XmlElement Node { get; }

            public string Type { get; }

            public string Id { get; }

            public DefReference BaseDef { get; }
        }

        sealed class Candidate
        {
            public Candidate(
                XmlDocument fragment,
                XmlDocument working,
                IReadOnlyList<NativeContentChange> changes,
                IReadOnlyList<NativeRegisteredContent> layout,
                bool restartRequired)
            {
                Fragment = fragment;
                Working = working;
                Changes = changes;
                Layout = layout;
                RestartRequired = restartRequired;
            }

            public XmlDocument Fragment { get; }

            public XmlDocument Working { get; }

            public IReadOnlyList<NativeContentChange> Changes { get; }

            public IReadOnlyList<NativeRegisteredContent> Layout { get; }

            public bool RestartRequired { get; }
        }
    }
}
