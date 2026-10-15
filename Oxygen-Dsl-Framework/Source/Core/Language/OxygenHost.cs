using System;
using System.Collections.Generic;

namespace Oxygendsl.Core
{
    public interface IOxygenHostValue
    {
        bool TryGetMember(string name, out object value);
    }

    public interface IOxygenHostCallable
    {
        object Invoke(IList<object> positional, IDictionary<string, object> named);
    }
    public interface IOxygenStandardModuleProvider
    {
        bool TryGetModule(string modulePath, out IOxygenHostValue module);
    }

    public interface IOxygenModuleProvider
    {
        bool TryGetModule(string packageId, string modulePath, out IOxygenHostValue module);
    }

    public sealed class OxygenHostModuleRegistry : IOxygenModuleProvider, IOxygenStandardModuleProvider
    {
        private readonly Dictionary<string, IOxygenHostValue> _modules =
            new Dictionary<string, IOxygenHostValue>(StringComparer.Ordinal);

        public void Register(string packageId, string modulePath, IOxygenHostValue module)
        {
            if (packageId == null) throw new ArgumentNullException(nameof(packageId));
            if (modulePath == null) throw new ArgumentNullException(nameof(modulePath));
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (packageId.Length == 0 || packageId.IndexOf('\n') >= 0 || packageId.IndexOf('\0') >= 0)
                throw new ArgumentException("A module provider cannot be empty or contain a separator.", nameof(packageId));
            if (modulePath.Length == 0 || modulePath.IndexOf('\n') >= 0 || modulePath.IndexOf('\0') >= 0)
                throw new ArgumentException("A module path cannot be empty or contain a separator.", nameof(modulePath));

            string key = Key(packageId, modulePath);
            if (_modules.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    "Module '" + modulePath + "' from '" + packageId + "' is already registered.");
            }

            _modules.Add(key, module);
        }

        public bool TryGetModule(string packageId, string modulePath, out IOxygenHostValue module)
        {
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(modulePath))
            {
                module = null;
                return false;
            }

            return _modules.TryGetValue(Key(packageId, modulePath), out module);
        }

        public bool TryGetModule(string modulePath, out IOxygenHostValue module)
        {
            return TryGetModule("std", modulePath, out module);
        }

        private static string Key(string packageId, string modulePath)
        {
            return packageId.ToLowerInvariant() + "\n" + modulePath;
        }
    }

    public sealed class DictionaryOxygenHostValue : IOxygenHostValue
    {
        private readonly Dictionary<string, object> _members;

        public DictionaryOxygenHostValue(IDictionary<string, object> members)
        {
            _members = new Dictionary<string, object>(StringComparer.Ordinal);
            if (members == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> member in members)
            {
                if (member.Key != null)
                {
                    _members[member.Key] = member.Value;
                }
            }
        }

        public bool TryGetMember(string name, out object value)
        {
            if (name == null)
            {
                value = null;
                return false;
            }

            return _members.TryGetValue(name, out value);
        }
    }
}
