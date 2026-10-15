using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;
using Oxygendsl.Core;
using RimWorld;
using Verse;

namespace Oxygendsl.Mod.Content
{
    public enum NativeFieldKind
    {
        Scalar,
        DefReference,
        NativeType,
        List,
        Object,
        CustomNamedValue
    }

    public sealed class NativeFieldMapping
    {
        public NativeFieldMapping(
            string name,
            Type valueType,
            NativeFieldKind kind,
            Type elementType,
            Type requiredBase,
            bool customNamedValue,
            bool identity)
        {
            Name = name;
            ValueType = valueType;
            Kind = kind;
            ElementType = elementType;
            RequiredBase = requiredBase;
            CustomNamedValue = customNamedValue;
            Identity = identity;
        }

        public string Name { get; }

        public Type ValueType { get; }

        public NativeFieldKind Kind { get; }

        public Type ElementType { get; }

        public Type RequiredBase { get; }

        public bool CustomNamedValue { get; }

        public bool Identity { get; }
    }

    public sealed class NativeContentSchema
    {
        public const string DefaultThoughtWorker = "Oxygendsl.Mod.Runtime.ThoughtWorker_OxygenRule";

        static readonly string[] ProductPackageIds =
        {
            "ludeon.rimworld",
            "ludeon.rimworld.royalty",
            "ludeon.rimworld.ideology",
            "ludeon.rimworld.biotech",
            "ludeon.rimworld.anomaly",
            "ludeon.rimworld.odyssey"
        };

        readonly Dictionary<string, Type> _defTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        readonly Dictionary<string, Dictionary<string, NativeFieldMapping>> _fields =
            new Dictionary<string, Dictionary<string, NativeFieldMapping>>(StringComparer.Ordinal);
        readonly Dictionary<string, List<CatalogDef>> _catalog =
            new Dictionary<string, List<CatalogDef>>(StringComparer.Ordinal);
        readonly HashSet<string> _activePackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _loadedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool _complete;
        string _incompleteReason = "No native Def catalog has been loaded.";

        public static NativeContentSchema Shared { get; } = CreateDefault();

        public bool IsComplete { get { return _complete; } }

        public string IncompleteReason { get { return _complete ? string.Empty : _incompleteReason; } }

        public IReadOnlyCollection<string> ActivePackages { get { return _activePackages; } }

        public static NativeContentSchema CreateDefault()
        {
            NativeContentSchema schema = new NativeContentSchema();
            schema.Register("TraitDef", typeof(TraitDef));
            schema.Register("ThoughtDef", typeof(ThoughtDef));
            schema.RegisterObject(typeof(TraitDegreeData));
            schema.RegisterObject(typeof(ThoughtStage));
            schema.RegisterObject(typeof(StatModifier));
            schema.RegisterObject(typeof(SkillGain));
            schema.RegisterObject(typeof(Aptitude));
            schema.RegisterObject(typeof(TraitRequirement));
            schema.RegisterObject(typeof(IngestibleModifiers));
            schema.RegisterObject(typeof(PossessionThingDefCountClass));
            schema.RegisterObject(typeof(DefHyperlink));
            schema.RegisterObject(typeof(SimpleCurve));
            schema.RegisterObject(typeof(CurvePoint));
            return schema;
        }

        public void Register(string defTypeName, Type clrType)
        {
            if (string.IsNullOrEmpty(defTypeName))
                throw new ArgumentException("A native Def type name is required.", "defTypeName");
            if (clrType == null)
                throw new ArgumentNullException("clrType");
            if (!typeof(Def).IsAssignableFrom(clrType))
                throw new ArgumentException(clrType.FullName + " is not a Verse.Def.", "clrType");
            _defTypes[defTypeName] = clrType;
            RegisterObject(clrType);
        }

        public void RegisterField(string ownerTypeName, NativeFieldMapping mapping)
        {
            if (string.IsNullOrEmpty(ownerTypeName))
                throw new ArgumentException("An owner type name is required.", "ownerTypeName");
            if (mapping == null)
                throw new ArgumentNullException("mapping");
            Dictionary<string, NativeFieldMapping> fields;
            if (!_fields.TryGetValue(ownerTypeName, out fields))
            {
                fields = new Dictionary<string, NativeFieldMapping>(StringComparer.Ordinal);
                _fields[ownerTypeName] = fields;
            }
            fields[mapping.Name] = mapping;
        }

        public bool TryGetDefType(string defTypeName, out Type clrType)
        {
            clrType = null;
            return !string.IsNullOrEmpty(defTypeName) && _defTypes.TryGetValue(defTypeName, out clrType);
        }

        public bool TryGetFields(string typeName, out IReadOnlyDictionary<string, NativeFieldMapping> fields)
        {
            Dictionary<string, NativeFieldMapping> found;
            if (!string.IsNullOrEmpty(typeName) && _fields.TryGetValue(typeName, out found))
            {
                fields = found;
                return true;
            }
            fields = null;
            return false;
        }

        public Type RequiredBaseFor(string typeName, string fieldName)
        {
            NativeFieldMapping mapping;
            if (!TryGetMapping(typeName, fieldName, out mapping))
                return null;
            return mapping.RequiredBase;
        }

        public void CheckFields(string typeName, IDictionary<string, object> fields, int line, int column)
        {
            CheckFields(typeName, fields, line, column, string.Empty, string.Empty, string.Empty);
        }

        public void CheckFields(
            string typeName,
            IDictionary<string, object> fields,
            int line,
            int column,
            string provider,
            string target,
            string path)
        {
            if (fields == null)
                return;
            Dictionary<string, NativeFieldMapping> known;
            if (!_fields.TryGetValue(typeName, out known))
            {
                throw Located(line, column, provider, target, path,
                    "No native field mapping is registered for " + typeName + ".");
            }
            foreach (KeyValuePair<string, object> pair in fields)
            {
                string fieldPath = Join(path, pair.Key);
                NativeFieldMapping mapping;
                if (!known.TryGetValue(pair.Key, out mapping))
                {
                    throw Located(line, column, provider, target, fieldPath,
                        "Unknown field '" + pair.Key + "' on " + typeName + ".");
                }
                CheckValue(typeName, mapping, pair.Value, line, column, provider, target, fieldPath);
            }
        }

        internal NativeFieldMapping PathMapping(string typeName, string path, int line, int column, string provider, string target)
        {
            string[] parts = path.Split('.');
            NativeFieldMapping mapping = null;
            for (int i = 0; i < parts.Length; i++)
            {
                int index;
                if (int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out index))
                {
                    if (mapping == null || mapping.Kind != NativeFieldKind.List)
                        throw Located(line, column, provider, target, path, "Index '" + parts[i] + "' does not address a list.");
                    Type item = mapping.ElementType;
                    mapping = new NativeFieldMapping(parts[i], item, KindOf(item), null, mapping.RequiredBase, mapping.CustomNamedValue, false);
                }
                else
                {
                    if (i > 0)
                    {
                        if (mapping == null || (mapping.Kind != NativeFieldKind.Object && mapping.Kind != NativeFieldKind.CustomNamedValue))
                            throw Located(line, column, provider, target, path, "Field path cannot descend through '" + parts[i - 1] + "'.");
                        typeName = mapping.ValueType.Name;
                    }
                    if (!TryGetMapping(typeName, parts[i], out mapping))
                        throw Located(line, column, provider, target, path, "Unknown field '" + parts[i] + "' on " + typeName + ".");
                }
            }
            return mapping;
        }

        internal void CheckPath(string typeName, string path, object value, int line, int column, string provider, string target, bool collection)
        {
            NativeFieldMapping mapping = PathMapping(typeName, path, line, column, provider, target);
            if (collection && mapping.Kind != NativeFieldKind.List)
                throw Located(line, column, provider, target, path, "append/remove requires a list field.");
            CheckValue(typeName, mapping, value, line, column, provider, target, path);
        }

        public void MarkComplete()
        {
            _complete = true;
            _incompleteReason = string.Empty;
        }

        public void MarkIncomplete(string reason)
        {
            _complete = false;
            _incompleteReason = string.IsNullOrEmpty(reason)
                ? "The native Def catalog is incomplete."
                : reason;
        }

        public void SetActivePackages(IEnumerable<string> packageIds)
        {
            _activePackages.Clear();
            if (packageIds == null)
                return;
            foreach (string packageId in packageIds)
            {
                if (!string.IsNullOrEmpty(packageId))
                    _activePackages.Add(packageId);
            }
        }

        public void LoadDocument(XmlDocument document, string packageId)
        {
            if (document == null || document.DocumentElement == null)
                return;
            if (!string.IsNullOrEmpty(packageId))
                _activePackages.Add(packageId);
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element)
                    continue;
                AddCatalogNode(node, packageId);
            }
        }

        public void LoadFromFolders(IEnumerable<string> defRoots)
        {
            LoadFromFolders(defRoots, null);
        }

        public void LoadFromFolders(IEnumerable<string> defRoots, IEnumerable<string> activePackageIds)
        {
            _catalog.Clear();
            _loadedRoots.Clear();
            _activePackages.Clear();
            bool sawRoot = false;
            bool missingRoot = false;
            if (defRoots != null)
            {
                foreach (string root in defRoots)
                {
                    if (string.IsNullOrEmpty(root))
                        continue;
                    sawRoot = true;
                    if (!Directory.Exists(root))
                    {
                        missingRoot = true;
                        continue;
                    }
                    string full = Path.GetFullPath(root);
                    _loadedRoots.Add(full);
                    string packageId = PackageIdForRoot(full);
                    if (!string.IsNullOrEmpty(packageId))
                        _activePackages.Add(packageId);
                    foreach (string file in Directory.GetFiles(full, "*.xml", SearchOption.AllDirectories))
                    {
                        if (file.IndexOf(Path.DirectorySeparatorChar + "Patches" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;
                        XmlDocument document = new XmlDocument();
                        document.Load(file);
                        LoadDocument(document, packageId);
                    }
                }
            }
            if (activePackageIds != null)
            {
                foreach (string packageId in activePackageIds)
                {
                    if (!string.IsNullOrEmpty(packageId))
                        _activePackages.Add(packageId);
                }
            }
            if (!sawRoot || missingRoot)
                MarkIncomplete(missingRoot
                    ? "A requested Def catalog folder does not exist."
                    : "No native Def catalog folder was provided.");
            else
                MarkIncomplete("XML Def folders do not include Defs created dynamically by assemblies. Call MarkComplete only for a closed catalog.");
        }

        public void IndexDocumentDefs(XmlDocument document, string packageId, bool replacePackage)
        {
            if (replacePackage)
                RemovePackage(packageId);
            LoadDocument(document, packageId);
        }

        public NativeDefResolution ResolveDef(string type, string id, string packageId)
        {
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id))
                return _complete ? NativeDefResolution.Missing : NativeDefResolution.Unknown;
            if (!IsKnownDefTypeName(type) && !LooksLikeDefType(type))
                return NativeDefResolution.Missing;
            List<CatalogDef> matches;
            if (!_catalog.TryGetValue(Key(type, id), out matches) || matches.Count == 0)
                return _complete ? NativeDefResolution.Missing : NativeDefResolution.Unknown;
            if (string.IsNullOrEmpty(packageId))
                return NativeDefResolution.Found;
            for (int i = 0; i < matches.Count; i++)
            {
                if (string.Equals(matches[i].PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                    return NativeDefResolution.Found;
            }
            return _complete ? NativeDefResolution.Missing : NativeDefResolution.Unknown;
        }

        public bool PackageActive(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return false;
            if (_activePackages.Contains(packageId))
                return true;
            string bare = StripVersionSuffix(packageId);
            foreach (string active in _activePackages)
            {
                if (string.Equals(StripVersionSuffix(active), bare, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public bool IsKnownDefTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            if (_defTypes.ContainsKey(typeName))
                return true;
            return typeName.EndsWith("Def", StringComparison.Ordinal) && typeName.IndexOf('.') < 0;
        }

        public void NoteDefined(string type, string id, string packageId)
        {
            AddCatalog(type, id, packageId, false);
        }

        public void RemovePackage(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return;
            List<string> empty = new List<string>();
            foreach (KeyValuePair<string, List<CatalogDef>> pair in _catalog)
            {
                pair.Value.RemoveAll(item => string.Equals(item.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
                if (pair.Value.Count == 0)
                    empty.Add(pair.Key);
            }
            for (int i = 0; i < empty.Count; i++)
                _catalog.Remove(empty[i]);
        }

        void RegisterObject(Type type)
        {
            if (type == null)
                return;
            Dictionary<string, NativeFieldMapping> fields = new Dictionary<string, NativeFieldMapping>(StringComparer.Ordinal);
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                FieldInfo[] declared = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < declared.Length; i++)
                {
                    FieldInfo field = declared[i];
                    if (field.IsStatic || field.IsLiteral || IsIgnored(field) || fields.ContainsKey(field.Name))
                        continue;
                    if (!field.IsPublic && !IsSerializedPrivate(field))
                        continue;
                    fields[field.Name] = MapField(field);
                }
            }
            if (type == typeof(SimpleCurve))
            {
                fields["points"] = new NativeFieldMapping(
                    "points", typeof(List<CurvePoint>), NativeFieldKind.List, typeof(CurvePoint), null, false, false);
            }
            _fields[type.Name] = fields;
        }

        static bool IsIgnored(FieldInfo field)
        {
            if (field.IsDefined(typeof(UnsavedAttribute), false))
                return true;
            if (field.Name == "shortHash" || field.Name == "index" || field.Name == "defNameHash"
                || field.Name == "modContentPack" || field.Name == "fileName" || field.Name == "generated"
                || field.Name == "debugRandomId")
                return true;
            return false;
        }

        static bool IsSerializedPrivate(FieldInfo field)
        {
            if (field.IsPublic)
                return false;
            if (field.Name == "commonality" || field.Name == "commonalityFemale" || field.Name == "icon")
                return true;
            if (field.FieldType == typeof(List<PawnRenderNodeProperties>))
                return true;
            if (field.FieldType == typeof(List<CurvePoint>))
                return true;
            return false;
        }

        static NativeFieldMapping MapField(FieldInfo field)
        {
            Type fieldType = field.FieldType;
            Type required = RequiredBase(field);
            if (HasCustomLoader(fieldType) && IsCustomNamedValue(fieldType))
            {
                return new NativeFieldMapping(field.Name, fieldType, NativeFieldKind.CustomNamedValue, ItemType(fieldType), required, true, false);
            }
            if (IsList(fieldType))
            {
                Type item = ItemType(fieldType);
                return new NativeFieldMapping(field.Name, fieldType, NativeFieldKind.List, item, required, HasCustomLoader(item) && IsCustomNamedValue(item), false);
            }
            if (typeof(Def).IsAssignableFrom(fieldType))
                return new NativeFieldMapping(field.Name, fieldType, NativeFieldKind.DefReference, fieldType, null, false, false);
            if (fieldType == typeof(Type))
                return new NativeFieldMapping(field.Name, fieldType, NativeFieldKind.NativeType, null, required, false, false);
            if (IsScalar(fieldType))
                return new NativeFieldMapping(field.Name, fieldType, NativeFieldKind.Scalar, null, null, false, field.Name == "defName");
            return new NativeFieldMapping(field.Name, fieldType, NativeFieldKind.Object, null, required, false, false);
        }

        static bool IsCustomNamedValue(Type type)
        {
            return type == typeof(StatModifier)
                || type == typeof(SkillGain)
                || type == typeof(Aptitude)
                || type == typeof(PossessionThingDefCountClass)
                || type == typeof(TraitRequirement)
                || type == typeof(DefHyperlink);
        }

        static bool HasCustomLoader(Type type)
        {
            return type != null && type.GetMethod("LoadDataFromXmlCustom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        }

        static Type RequiredBase(FieldInfo field)
        {
            if (field.Name == "workerClass")
                return typeof(ThoughtWorker);
            if (field.Name == "thoughtClass")
                return typeof(Thought);
            if (field.Name == "mentalStateGiverClass")
                return typeof(TraitMentalStateGiver);
            return null;
        }

        static NativeFieldKind KindOf(Type type)
        {
            if (type == typeof(Type))
                return NativeFieldKind.NativeType;
            if (typeof(Def).IsAssignableFrom(type))
                return NativeFieldKind.DefReference;
            if (HasCustomLoader(type) && IsCustomNamedValue(type))
                return NativeFieldKind.CustomNamedValue;
            if (IsScalar(type))
                return NativeFieldKind.Scalar;
            return NativeFieldKind.Object;
        }

        static bool IsList(Type type)
        {
            return type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        static Type ItemType(Type type)
        {
            if (IsList(type))
                return type.GetGenericArguments()[0];
            return type;
        }

        static bool IsScalar(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return true;
            if (type == typeof(IntRange) || type == typeof(CurvePoint))
                return true;
            return false;
        }

        void CheckValue(
            string owner,
            NativeFieldMapping mapping,
            object value,
            int line,
            int column,
            string provider,
            string target,
            string path)
        {
            if (value == null)
                return;
            if (mapping.Identity)
            {
                throw Located(line, column, provider, target, path,
                    "Field '" + mapping.Name + "' changes content identity and cannot be set here.");
            }
            switch (mapping.Kind)
            {
                case NativeFieldKind.Scalar:
                    CheckScalar(mapping.ValueType, value, line, column, provider, target, path);
                    return;
                case NativeFieldKind.DefReference:
                    CheckDefReference(mapping.ValueType, value, line, column, provider, target, path);
                    return;
                case NativeFieldKind.NativeType:
                    CheckNativeType(mapping.RequiredBase, value, line, column, provider, target, path);
                    return;
                case NativeFieldKind.List:
                    CheckList(mapping, value, line, column, provider, target, path);
                    return;
                case NativeFieldKind.CustomNamedValue:
                    CheckCustom(mapping.ValueType, value, line, column, provider, target, path);
                    return;
                default:
                    CheckObject(mapping.ValueType, value, line, column, provider, target, path);
                    return;
            }
        }

        void CheckList(
            NativeFieldMapping mapping,
            object value,
            int line,
            int column,
            string provider,
            string target,
            string path)
        {
            IList list = AsList(value);
            if (list == null)
            {
                throw Located(line, column, provider, target, path,
                    "Field '" + path + "' must be a list.");
            }
            for (int i = 0; i < list.Count; i++)
            {
                string itemPath = path + "." + i.ToString(CultureInfo.InvariantCulture);
                object item = list[i];
                if (item == null)
                    continue;
                NativeFieldKind kind = KindOf(mapping.ElementType);
                if (kind == NativeFieldKind.DefReference)
                    CheckDefReference(mapping.ElementType, item, line, column, provider, target, itemPath);
                else if (kind == NativeFieldKind.Scalar)
                    CheckScalar(mapping.ElementType, item, line, column, provider, target, itemPath);
                else if (kind == NativeFieldKind.NativeType)
                    CheckNativeType(mapping.RequiredBase, item, line, column, provider, target, itemPath);
                else if (kind == NativeFieldKind.CustomNamedValue)
                    CheckCustom(mapping.ElementType, item, line, column, provider, target, itemPath);
                else
                    CheckObject(mapping.ElementType, item, line, column, provider, target, itemPath);
            }
        }

        void CheckObject(
            Type type,
            object value,
            int line,
            int column,
            string provider,
            string target,
            string path)
        {
            IDictionary<string, object> fields = AsDictionary(value);
            if (fields == null)
            {
                throw Located(line, column, provider, target, path,
                    "Field '" + path + "' must be an object matching " + type.Name + ".");
            }
            CheckFields(type.Name, fields, line, column, provider, target, path);
        }

        void CheckCustom(
            Type type,
            object value,
            int line,
            int column,
            string provider,
            string target,
            string path)
        {
            if (type == typeof(DefHyperlink))
            {
                CheckDefReference(typeof(Def), value, line, column, provider, target, path);
                return;
            }
            IDictionary<string, object> fields = AsDictionary(value);
            if (fields == null)
            {
                throw Located(line, column, provider, target, path,
                    "Field '" + path + "' must be an object matching " + type.Name + ".");
            }
            if (type == typeof(StatModifier) || type == typeof(SkillGain) || type == typeof(Aptitude))
            {
                RequireCustomParts(fields, line, column, provider, target, path, "stat", "value", "amount", "level", "skill");
                object stat = First(fields, "stat", "skill");
                CheckDefReference(type == typeof(StatModifier) ? typeof(StatDef) : typeof(SkillDef), stat, line, column, provider, target, path);
                object number = First(fields, "value", "amount", "level");
                CheckScalar(type == typeof(StatModifier) ? typeof(float) : typeof(int), number, line, column, provider, target, path);
                return;
            }
            if (type == typeof(TraitRequirement))
            {
                object def = First(fields, "def", "trait");
                CheckDefReference(typeof(TraitDef), def, line, column, provider, target, path);
                if (fields.ContainsKey("degree") && fields["degree"] != null)
                    CheckScalar(typeof(int), fields["degree"], line, column, provider, target, path);
                return;
            }
            if (type == typeof(PossessionThingDefCountClass))
            {
                object key = First(fields, "key", "thing");
                CheckDefReference(typeof(ThingDef), key, line, column, provider, target, path);
                if (fields.ContainsKey("value") && fields["value"] != null)
                    CheckScalar(typeof(IntRange), fields["value"], line, column, provider, target, path);
            }
        }

        static void RequireCustomParts(
            IDictionary<string, object> fields,
            int line,
            int column,
            string provider,
            string target,
            string path,
            params string[] names)
        {
            bool hasName = fields.ContainsKey("stat") || fields.ContainsKey("skill");
            bool hasValue = fields.ContainsKey("value") || fields.ContainsKey("amount") || fields.ContainsKey("level");
            if (!hasName || !hasValue)
            {
                throw Located(line, column, provider, target, path,
                    "Field '" + path + "' needs a typed Def reference and a numeric value.");
            }
            foreach (string key in fields.Keys)
            {
                bool known = false;
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(key, names[i], StringComparison.Ordinal))
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                {
                    throw Located(line, column, provider, target, Join(path, key),
                        "Unknown field '" + key + "' on a custom native value.");
                }
            }
        }

        void CheckDefReference(
            Type expected,
            object value,
            int line,
            int column,
            string provider,
            string target,
            string path)
        {
            DefReference reference = value as DefReference;
            if (reference == null || string.IsNullOrEmpty(reference.Type) || string.IsNullOrEmpty(reference.Id))
            {
                throw Located(line, column, provider, target, path,
                    "Field '" + path + "' must be a typed Def reference, not a raw string.");
            }
            if (expected != null && expected != typeof(Def) && !string.Equals(reference.Type, expected.Name, StringComparison.Ordinal))
            {
                throw Located(line, column, provider, target, path,
                    "Field '" + path + "' requires " + expected.Name + ", not " + reference.Type + ".");
            }
            if (!IsKnownDefTypeName(reference.Type))
            {
                throw Located(line, column, provider, target, path,
                    "Def type '" + reference.Type + "' is not a native Def type.");
            }
        }

        void CheckNativeType(
            Type requiredBase,
            object value,
            int line,
            int column,
            string provider,
            string target,
            string path)
        {
            NativeTypeReference reference = value as NativeTypeReference;
            if (reference == null || string.IsNullOrEmpty(reference.Name))
            {
                throw Located(line, column, provider, target, path,
                    "Field '" + path + "' must be a native_type reference, not a raw string.");
            }
            Type resolved = ResolveNativeType(reference.Name);
            if (resolved == null)
            {
                throw Located(line, column, provider, target, path,
                    "Native type '" + reference.Name + "' was not found in a loaded assembly.");
            }
            if (requiredBase != null && !requiredBase.IsAssignableFrom(resolved))
            {
                throw Located(line, column, provider, target, path,
                    "Native type '" + reference.Name + "' is not assignable to " + requiredBase.FullName + ".");
            }
        }

        public static Type ResolveNativeType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            Type direct = Type.GetType(name, false);
            if (direct != null)
                return direct;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = assemblies[i].GetType(name, false);
                if (found != null)
                    return found;
            }
            return null;
        }

        static void CheckScalar(
            Type type,
            object value,
            int line,
            int column,
            string provider,
            string target,
            string path)
        {
            if (value == null)
                return;
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(string))
            {
                if (!(value is string))
                    throw Located(line, column, provider, target, path, "Field '" + path + "' must be text.");
                return;
            }
            if (type == typeof(bool))
            {
                if (!(value is bool))
                    throw Located(line, column, provider, target, path, "Field '" + path + "' must be a boolean.");
                return;
            }
            if (type.IsEnum)
            {
                if (value is string)
                {
                    string text = (string)value;
                    if (!Enum.IsDefined(type, text) && !TryParseFlags(type, text))
                    {
                        throw Located(line, column, provider, target, path,
                            "Field '" + path + "' has unknown " + type.Name + " value '" + text + "'.");
                    }
                    return;
                }
                IList flags = value as IList;
                if (flags != null && type.IsDefined(typeof(FlagsAttribute), false))
                {
                    foreach (object flag in flags)
                        if (!(flag is string) || !Enum.IsDefined(type, (string)flag))
                            throw Located(line, column, provider, target, path, "Field '" + path + "' contains an unknown flag.");
                    return;
                }
                throw Located(line, column, provider, target, path,
                    "Field '" + path + "' must be a " + type.Name + " name.");
            }
            if (type == typeof(IntRange) || type == typeof(CurvePoint))
            {
                if (!(value is string) && !(value is int) && !IsNumber(value))
                {
                    throw Located(line, column, provider, target, path,
                        "Field '" + path + "' must use the native text form.");
                }
                return;
            }
            if (IsNumber(value) || value is int)
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw Located(line, column, provider, target, path, "Field '" + path + "' must be finite.");
                if (type == typeof(int) && (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue))
                    throw Located(line, column, provider, target, path, "Field '" + path + "' must be an Int32.");
                if (type == typeof(float) && Math.Abs(number) > float.MaxValue)
                    throw Located(line, column, provider, target, path, "Field '" + path + "' exceeds Single range.");
                return;
            }
            throw Located(line, column, provider, target, path,
                "Field '" + path + "' must be a number.");
        }

        static bool TryParseFlags(Type type, string text)
        {
            string[] parts = text.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0 || !Enum.IsDefined(type, part))
                    return false;
            }
            return parts.Length > 0;
        }

        static bool IsNumber(object value)
        {
            return value is float || value is double || value is decimal || value is long || value is short || value is byte;
        }

        bool TryGetMapping(string typeName, string fieldName, out NativeFieldMapping mapping)
        {
            mapping = null;
            Dictionary<string, NativeFieldMapping> fields;
            return _fields.TryGetValue(typeName, out fields) && fields.TryGetValue(fieldName, out mapping);
        }

        void AddCatalogNode(XmlNode node, string packageId)
        {
            if (node.Attributes != null && node.Attributes["Abstract"] != null
                && string.Equals(node.Attributes["Abstract"].Value, "true", StringComparison.OrdinalIgnoreCase))
                return;
            XmlNode nameNode = node["defName"];
            if (nameNode == null || string.IsNullOrEmpty(nameNode.InnerText))
                return;
            AddCatalog(node.Name, nameNode.InnerText.Trim(), packageId, false);
        }

        void AddCatalog(string type, string id, string packageId, bool allowDuplicate)
        {
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id))
                return;
            string key = Key(type, id);
            List<CatalogDef> list;
            if (!_catalog.TryGetValue(key, out list))
            {
                list = new List<CatalogDef>();
                _catalog[key] = list;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].PackageId, packageId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    if (!allowDuplicate)
                        return;
                }
            }
            list.Add(new CatalogDef(type, id, packageId ?? string.Empty));
        }

        static string PackageIdForRoot(string fullPath)
        {
            DirectoryInfo directory = new DirectoryInfo(fullPath);
            for (int depth = 0; directory != null && depth < 6; depth++)
            {
                string about = Path.Combine(directory.FullName, "About", "About.xml");
                if (File.Exists(about))
                {
                    XmlDocument document = new XmlDocument();
                    document.Load(about);
                    XmlNode node = document.SelectSingleNode("//packageId");
                    if (node != null && !string.IsNullOrEmpty(node.InnerText))
                        return node.InnerText.Trim();
                }
                string name = directory.Name;
                if (string.Equals(name, "Core", StringComparison.OrdinalIgnoreCase))
                    return "ludeon.rimworld";
                for (int i = 0; i < ProductPackageIds.Length; i++)
                {
                    if (ProductPackageIds[i].EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
                        return ProductPackageIds[i];
                }
                directory = directory.Parent;
            }
            return string.Empty;
        }

        static string StripVersionSuffix(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return string.Empty;
            int slash = packageId.LastIndexOf('_');
            if (slash > 0 && slash < packageId.Length - 1)
            {
                string tail = packageId.Substring(slash + 1);
                bool numeric = true;
                for (int i = 0; i < tail.Length; i++)
                {
                    if (!char.IsDigit(tail[i]) && tail[i] != '.')
                    {
                        numeric = false;
                        break;
                    }
                }
                if (numeric)
                    return packageId.Substring(0, slash);
            }
            return packageId;
        }

        public static bool LooksLikeDefType(string typeName)
        {
            return !string.IsNullOrEmpty(typeName)
                && typeName.EndsWith("Def", StringComparison.Ordinal)
                && typeName.IndexOf('.') < 0;
        }

        static string Key(string type, string id)
        {
            return type + "\n" + id;
        }

        static string Join(string path, string name)
        {
            return string.IsNullOrEmpty(path) ? name : path + "." + name;
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

        static IList AsList(object value)
        {
            IList list = value as IList;
            if (list != null && !(value is string))
                return list;
            return null;
        }

        static IDictionary<string, object> AsDictionary(object value)
        {
            return value as IDictionary<string, object>;
        }

        static NativeContentException Located(
            int line,
            int column,
            string provider,
            string target,
            string field,
            string message)
        {
            return new NativeContentException(message, line, column, provider, target, field);
        }

        sealed class CatalogDef
        {
            public CatalogDef(string type, string id, string packageId)
            {
                Type = type;
                Id = id;
                PackageId = packageId ?? string.Empty;
            }

            public string Type { get; }

            public string Id { get; }

            public string PackageId { get; }
        }
    }
}
