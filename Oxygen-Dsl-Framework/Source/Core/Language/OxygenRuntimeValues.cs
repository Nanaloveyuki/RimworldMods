// Shared runtime values used by the executor and the module partial.
// Copyright (c) 2026 Nanaloveyuki
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Oxygendsl.Core;
namespace Oxygendsl.Language
{
    internal sealed class RuntimeScope
    {
        private readonly HashSet<string> _constantNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly IDictionary<string, Binding> _bindings =
            new Dictionary<string, Binding>(StringComparer.Ordinal);
        private readonly HashSet<string> _declared = new HashSet<string>(StringComparer.Ordinal);
        private readonly bool transparent;
        private static int _nextId;

        public RuntimeScope(RuntimeScope parent, bool transparent = false)
        {
            Id = System.Threading.Interlocked.Increment(ref _nextId);
            // Only function calls own a new binding table; braces share their owner's table.
            this.transparent = transparent;
            Parent = transparent ? parent.Parent : parent;
            if (transparent)
            {
                _bindings = parent._bindings;
                _constantNames = parent._constantNames;
                Id = parent.Id;
            }
        }

        public int Id { get; }

        public string ModulePackageId { get; set; }

        public string ModulePath { get; set; }


        public RuntimeScope Parent { get; }

        public void MarkConstant(string name) { _constantNames.Add(name); }

        public IEnumerable<string> GetDefinitionNames(bool functions)
        {
            foreach (var binding in _bindings)
                if (functions ? binding.Value.Declaration is ArtiFunctionDeclarationStatement
                    : _constantNames.Contains(binding.Key)) yield return binding.Key;
        }

        public bool IsConstant(string name)
        {
            Binding binding;
            if (_bindings.TryGetValue(name, out binding))
                return _constantNames.Contains(name);
            return false;
        }

        public bool TryDeclare(string name, object value, bool isConst)
        {
            return TryDeclare(name, value, isConst, false);
        }

        public bool TryDeclare(string name, object value, bool isConst, bool allowReplace, object declaration = null)
        {
            if (!CanDeclare(name, isConst, allowReplace, declaration)) return false;
            _bindings[name] = new Binding(value, isConst, declaration, transparent);
            _constantNames.Remove(name);
            _declared.Add(name);
            return true;
        }

        public bool CanDeclare(string name, bool isConst, bool allowReplace, object declaration)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (_bindings.TryGetValue(name, out Binding existing))
            {
                // A loop may execute the same declaration again, including a constant initializer.
                bool sameDeclaration = declaration != null && ReferenceEquals(existing.Declaration, declaration);
                bool blockVariable = !_declared.Contains(name) && !isConst && !existing.IsConst
                    && (transparent || existing.FromBlock);
                if (!allowReplace && !sameDeclaration && !blockVariable)
                {
                    return false;
                }

                return true;
            }

            return true;
        }

        public bool TryGet(string name, out object value)
        {
            Binding binding;
            if (_bindings.TryGetValue(name, out binding))
            {
                value = binding.Value;
                return true;
            }

            value = null;
            return Parent != null && Parent.TryGet(name, out value);
        }

        public bool TryGetBinding(string name, out Binding binding)
        {
            if (_bindings.TryGetValue(name, out binding))
            {
                return true;
            }

            binding = null;
            return Parent != null && Parent.TryGetBinding(name, out binding);
        }

        public bool CanAssign(string name, out string error)
        {
            if (_bindings.TryGetValue(name, out Binding binding))
            {
                error = binding.IsConst ? "The name '" + name + "' cannot be assigned." : null;
                return !binding.IsConst;
            }
            if (Parent != null) return Parent.CanAssign(name, out error);
            error = "The name '" + name + "' is not declared.";
            return false;
        }

        public bool TryAssign(string name, object value, out string error)
        {
            Binding binding;
            if (_bindings.TryGetValue(name, out binding))
            {
                if (binding.IsConst)
                {
                    error = "The name '" + name + "' cannot be assigned.";
                    return false;
                }

                binding.Value = value;
                error = null;
                return true;
            }

            if (Parent != null)
            {
                return Parent.TryAssign(name, value, out error);
            }

            error = "The name '" + name + "' is not declared.";
            return false;
        }
    }

    internal sealed class Binding
    {
        public Binding(object value, bool isConst, object declaration = null, bool fromBlock = false)
        {
            Value = value;
            IsConst = isConst;
            Declaration = declaration;
            FromBlock = fromBlock;
            Type = OxygenTypes.Any;
        }

        public object Value { get; set; }
        public bool IsConst { get; }
        public object Declaration { get; }
        public bool FromBlock { get; }
        public OxygenType Type { get; set; }
        internal OxygenType AnalysisType { get; set; }
    }

    internal sealed class RuntimeObject : IOxygenHostValue
    {
        private readonly IDictionary<string, object> _members;
        private readonly bool _sealed;

        public RuntimeObject()
            : this(null, false)
        {
        }

        public RuntimeObject(IDictionary<string, object> members)
            : this(members, true)
        {
        }

        internal RuntimeObject(bool sealMembers)
            : this(null, sealMembers)
        {
        }

        private RuntimeObject(IDictionary<string, object> members, bool sealedObject)
        {
            _members = members ?? new Dictionary<string, object>(StringComparer.Ordinal);
            _sealed = sealedObject;
        }

        public int Count { get { return _members.Count; } }

        public bool IsSealed { get { return _sealed; } }

        public IEnumerable<string> Keys { get { return _members.Keys; } }

        public void Set(string name, object value)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("A readonly context cannot be modified.");
            }

            _members[name] = value;
        }

        internal void SetUnchecked(string name, object value)
        {
            _members[name] = value;
        }

        public bool TryGet(string name, out object value)
        {
            return _members.TryGetValue(name, out value);
        }

        public bool TryGetMember(string name, out object value)
        {
            return TryGet(name, out value);
        }


        public bool Contains(string name)
        {
            return _members.ContainsKey(name);
        }
    }

    internal sealed class RuntimeCallable : IArtiCallable, IOxygenTypedCallable
    {
        private readonly Func<RuntimeArguments, object> _invoke;
        private readonly Func<int, string, OxygenType> _parameterType;

        public RuntimeCallable(Func<RuntimeArguments, object> invoke, string name = null)
            : this(invoke, name, null)
        {
        }

        public RuntimeCallable(Func<RuntimeArguments, object> invoke, string name, Func<int, string, OxygenType> parameterType)
        {
            _invoke = invoke;
            Name = name;
            _parameterType = parameterType;
        }

        public string Name { get; }
        internal ArtiFunctionDeclarationStatement Function { get; set; }
        internal RuntimeScope Closure { get; set; }

        public OxygenType GetParameterType(int position, string name)
        {
            return _parameterType == null ? null : _parameterType(position, name);
        }

        public object InvokeInternal(RuntimeArguments arguments)
        {
            return _invoke(arguments);
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

            return InvokeInternal(arguments);
        }
    }

    internal sealed class RuntimeArguments
    {
        private readonly List<RuntimeArgument> _items = new List<RuntimeArgument>();

        public IList<RuntimeArgument> Items { get { return _items; } }
        public IList<object> Positional
        {
            get
            {
                List<object> values = new List<object>();
                foreach (RuntimeArgument item in _items)
                {
                    if (item.Name == null)
                    {
                        values.Add(item.Value);
                    }
                }

                return values;
            }
        }

        public void Add(RuntimeArgument argument)
        {
            _items.Add(argument);
        }

        public object Get(int position, string name, bool required)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (RuntimeArgument item in _items)
                {
                    if (string.Equals(item.Name, name, StringComparison.Ordinal))
                    {
                        return item.Value;
                    }
                }
            }

            int current = 0;
            foreach (RuntimeArgument item in _items)
            {
                if (item.Name == null)
                {
                    if (current == position)
                    {
                        return item.Value;
                    }

                    current++;
                }
            }

            if (required)
            {
                throw new RuntimeFault("Required argument '" + (name ?? position.ToString(CultureInfo.InvariantCulture)) + "' is missing.");
            }

            return null;
        }
    }

    internal sealed class RuntimeArgument
    {
        public RuntimeArgument(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public object Value { get; }
    }

    internal sealed class HostArgumentList : IList<object>
    {
        private readonly List<object> _items = new List<object>();

        public void Clear() { _items.Clear(); }
        public void Add(object value) { _items.Add(value); }
        public int Count { get { return _items.Count; } }
        public bool IsReadOnly { get { return true; } }
        public object this[int index]
        {
            get { return _items[index]; }
            set { throw new NotSupportedException(); }
        }
        public IEnumerator<object> GetEnumerator() { return _items.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return _items.GetEnumerator(); }
        public int IndexOf(object item) { return _items.IndexOf(item); }
        public bool Contains(object item) { return _items.Contains(item); }
        public void CopyTo(object[] array, int arrayIndex) { _items.CopyTo(array, arrayIndex); }
        public void Insert(int index, object item) { throw new NotSupportedException(); }
        public void RemoveAt(int index) { throw new NotSupportedException(); }
        public bool Remove(object item) { throw new NotSupportedException(); }
        int IList<object>.IndexOf(object item) { return IndexOf(item); }
    }

    internal sealed class RuntimeFault : Exception
    {
        public RuntimeFault(string message)
            : base(message)
        {
        }
    }

    internal interface IOxygenTypedCallable : IArtiCallable
    {
        OxygenType GetParameterType(int position, string name);
    }
}
