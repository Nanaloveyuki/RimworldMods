// Oxygen module catalog. Script sources are registered by the host; std is reserved.
// Copyright (c) 2026 Nanaloveyuki
using System;
using System.Collections.Generic;

namespace Oxygendsl.Language
{
    public interface IOxygenModuleCatalog
    {
        bool TryGetSource(string packageId, string modulePath, out string source);
    }

    public sealed class OxygenModuleRegistry : IOxygenModuleCatalog
    {
        private readonly Dictionary<string, string> _sources =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public void RegisterSource(string packageId, string modulePath, string source)
        {
            if (packageId == null) throw new ArgumentNullException(nameof(packageId));
            if (modulePath == null) throw new ArgumentNullException(nameof(modulePath));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (packageId.Length == 0 || packageId.IndexOf('\n') >= 0 || packageId.IndexOf('\0') >= 0)
                throw new ArgumentException("A module provider cannot be empty or contain a separator.", nameof(packageId));
            if (string.Equals(packageId, "std", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("std is a reserved module provider.", nameof(packageId));
            if (modulePath.Length == 0 || modulePath.IndexOf('\n') >= 0 || modulePath.IndexOf('\0') >= 0)
                throw new ArgumentException("A module path cannot be empty or contain a separator.", nameof(modulePath));

            string key = Key(packageId, modulePath);
            if (_sources.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    "Module '" + modulePath + "' from '" + packageId + "' is already registered.");
            }

            _sources.Add(key, source);
        }

        public bool TryGetSource(string packageId, string modulePath, out string source)
        {
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(modulePath)
                || string.Equals(packageId, "std", StringComparison.OrdinalIgnoreCase))
            {
                source = null;
                return false;
            }

            return _sources.TryGetValue(Key(packageId, modulePath), out source);
        }

        private static string Key(string packageId, string modulePath)
        {
            return packageId.ToLowerInvariant() + "\n" + modulePath;
        }
    }
}
