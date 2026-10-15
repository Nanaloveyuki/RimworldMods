// Oxygen runtime types, collections, and struct values.
// Copyright (c) 2026 Nanaloveyuki
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Oxygendsl.Language
{
    public enum OxygenTypeKind
    {
        Any,
        Int,
        Float,
        Bool,
        String,
        List,
        Array,
        Object,
        Void,
        Struct,
        NativeInt32,
        TypeParameter
    }

    public sealed class OxygenType
    {
        internal OxygenType(OxygenTypeKind kind, string name, OxygenType element, bool nullable, string identity)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Element = element;
            Nullable = nullable;
            Identity = identity;
        }

        internal OxygenTypeKind Kind { get; }

        internal string Name { get; }

        internal OxygenType Element { get; }

        internal bool Nullable { get; }

        internal string Identity { get; }

        internal bool IsAny { get { return Kind == OxygenTypeKind.Any; } }

        internal bool IsStruct { get { return Kind == OxygenTypeKind.Struct; } }


        internal OxygenTypeKind ValueKind { get { return Kind; } }

        internal bool AcceptsNull { get { return Nullable || Kind == OxygenTypeKind.Any; } }

        public override string ToString()
        {
            if (Kind == OxygenTypeKind.List || Kind == OxygenTypeKind.Array)
            {
                string text = Name + "<" + (Element == null ? "any" : Element.ToString()) + ">";
                return Nullable ? text + "?" : text;
            }

            return Nullable && Kind != OxygenTypeKind.Any && Kind != OxygenTypeKind.Void ? Name + "?" : Name;
        }
    }

    internal sealed class OxygenNativeInt32
    {
        internal OxygenNativeInt32(int value, OxygenType type)
        {
            Value = value;
            Type = type;
        }

        public int Value { get; }

        public OxygenType Type { get; }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class OxygenCollection : IList<object>
    {
        private readonly List<object> _items;

        internal OxygenCollection(bool isArray, OxygenType elementType, int capacity)
        {
            IsArray = isArray;
            ElementType = elementType ?? OxygenTypes.Any;
            _items = new List<object>(capacity);
        }

        internal OxygenCollection(bool isArray, OxygenType elementType, IList<object> items)
        {
            IsArray = isArray;
            ElementType = elementType ?? OxygenTypes.Any;
            _items = items as List<object> ?? new List<object>(items);
        }

        internal bool IsArray { get; }

        internal OxygenType ElementType { get; }

        public int Count { get { return _items.Count; } }

        public bool IsReadOnly { get { return false; } }

        public object this[int index]
        {
            get { return _items[index]; }
            set { _items[index] = value; }
        }

        public void Add(object item)
        {
            _items.Add(item);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public bool Contains(object item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(object[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<object> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public int IndexOf(object item)
        {
            return _items.IndexOf(item);
        }

        public void Insert(int index, object item)
        {
            _items.Insert(index, item);
        }

        public bool Remove(object item)
        {
            return _items.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
        }
    }

    internal sealed class OxygenStruct
    {
        internal OxygenStruct(OxygenStructShape shape)
        {
            Shape = shape;
            Fields = new object[shape.FieldCount];
        }

        internal OxygenStructShape Shape { get; }

        internal object[] Fields { get; }
    }

    internal sealed class OxygenStructShape
    {
        internal OxygenStructShape(OxygenType type, string declarationName)
        {
            Type = type;
            DeclarationName = declarationName ?? string.Empty;
            Fields = new List<OxygenField>();
            Methods = new List<OxygenMethod>();
            Promoted = new Dictionary<string, OxygenPromotion>(StringComparer.Ordinal);
            StaticMethods = new Dictionary<string, object>(StringComparer.Ordinal);
            ImplementedTraits = new HashSet<string>(StringComparer.Ordinal);
        }

        internal object DeclarationScope { get; set; }
        internal RuntimeScope FieldScope { get; set; }

        internal OxygenType Type { get; }

        internal string DeclarationName { get; }

        internal List<OxygenField> Fields { get; }

        internal List<OxygenMethod> Methods { get; }

        internal Dictionary<string, OxygenPromotion> Promoted { get; }
        internal Dictionary<string, object> StaticMethods { get; }

        internal HashSet<string> ImplementedTraits { get; }

        internal int FieldCount { get { return Fields.Count; } }

        internal bool Defined { get; set; }
    }

    internal sealed class OxygenField
    {
        internal OxygenField(string name, OxygenType type, ArtiExpression defaultValue, bool embedded, int slot)
        {
            Name = name ?? string.Empty;
            Type = type ?? OxygenTypes.Any;
            DefaultValue = defaultValue;
            Embedded = embedded;
            Slot = slot;
        }

        internal string Name { get; }

        internal OxygenType Type { get; }

        internal ArtiExpression DefaultValue { get; }

        internal bool Embedded { get; }

        internal int Slot { get; }

        internal OxygenStructShape EmbeddedShape { get; set; }
    }

    internal sealed class OxygenMethod
    {
        internal OxygenMethod(string name, ArtiFunctionDeclarationStatement function, object closure)
        {
            Name = name ?? string.Empty;
            Function = function;
            Closure = closure;
        }

        internal string Name { get; }

        internal ArtiFunctionDeclarationStatement Function { get; }

        internal object Closure { get; }
    }

    internal struct OxygenPromotion
    {
        internal OxygenPromotion(int slot, string member, bool method)
        {
            Slot = slot;
            Member = member;
            Method = method;
        }

        internal int Slot { get; }

        internal string Member { get; }

        internal bool Method { get; }
    }

    internal sealed class OxygenBoundMethod : IOxygenTypedCallable
    {
        private readonly Func<OxygenMethod, OxygenStruct, RuntimeArguments, object> _invoke;
        private readonly Func<ArtiFunctionDeclarationStatement, int, string, OxygenType> _parameterType;

        internal OxygenBoundMethod(OxygenStruct receiver, OxygenMethod method,
            Func<OxygenMethod, OxygenStruct, RuntimeArguments, object> invoke,
            Func<ArtiFunctionDeclarationStatement, int, string, OxygenType> parameterType)
        {
            Receiver = receiver;
            Method = method;
            _invoke = invoke;
            _parameterType = parameterType;
        }

        internal OxygenStruct Receiver { get; }

        internal OxygenMethod Method { get; }

        public OxygenType GetParameterType(int position, string name)
        {
            return _parameterType(Method.Function, position, name);
        }

        internal object InvokeInternal(RuntimeArguments arguments)
        {
            return _invoke(Method, Receiver, arguments);
        }

        public object Invoke(IList<object> positionalArguments, IDictionary<string, object> namedArguments)
        {
            RuntimeArguments arguments = new RuntimeArguments();
            if (positionalArguments != null)
            {
                foreach (object argument in positionalArguments)
                {
                    arguments.Add(new RuntimeArgument(null, argument));
                }
            }

            if (namedArguments != null)
            {
                foreach (KeyValuePair<string, object> argument in namedArguments)
                {
                    arguments.Add(new RuntimeArgument(argument.Key, argument.Value));
                }
            }

            return _invoke(Method, Receiver, arguments);
        }
    }

    internal static class OxygenTypes
    {
        internal static readonly OxygenType Any = new OxygenType(OxygenTypeKind.Any, "any", null, true, "any");
        internal static readonly OxygenType Int = new OxygenType(OxygenTypeKind.Int, "int", null, false, "int");
        internal static readonly OxygenType Float = new OxygenType(OxygenTypeKind.Float, "float", null, false, "float");
        internal static readonly OxygenType Bool = new OxygenType(OxygenTypeKind.Bool, "bool", null, false, "bool");
        internal static readonly OxygenType String = new OxygenType(OxygenTypeKind.String, "string", null, false, "string");
        internal static readonly OxygenType Object = new OxygenType(OxygenTypeKind.Object, "object", null, false, "object");
        internal static readonly OxygenType Void = new OxygenType(OxygenTypeKind.Void, "void", null, false, "void");
        internal static readonly OxygenType List = Collection(OxygenTypeKind.List, Any, false);
        internal static readonly OxygenType Array = Collection(OxygenTypeKind.Array, Any, false);

        internal static OxygenType Collection(OxygenTypeKind kind, OxygenType element, bool nullable)
        {
            string name = kind == OxygenTypeKind.Array ? "array" : "list";
            return new OxygenType(kind, name, element ?? Any, nullable, name);
        }

        internal static OxygenType Nullable(OxygenType type)
        {
            if (type == null || type.IsAny || type.Kind == OxygenTypeKind.Void || type.Nullable)
            {
                return type;
            }

            return new OxygenType(type.Kind, type.Name, type.Element, true, type.Identity);
        }

        internal static OxygenType Builtin(string name)
        {
            switch (name)
            {
                case "any": return Any;
                case "int": return Int;
                case "float": return Float;
                case "bool": return Bool;
                case "string": return String;
                case "object": return Object;
                case "void": return Void;
                case "list": return List;
                case "array": return Array;
                default: return null;
            }
        }
    }
}
