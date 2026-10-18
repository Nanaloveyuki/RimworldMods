using System;
using System.IO;
using Oxygendsl.Language;
using Verse;

namespace Oxygendsl.Mod
{
    internal sealed class OxygenModModuleCatalog : IOxygenModuleCatalog
    {
        internal static readonly OxygenModModuleCatalog Instance = new OxygenModModuleCatalog();

        public bool TryGetSource(string packageId, string modulePath, out string source)
        {
            source = null;
            if (string.IsNullOrWhiteSpace(packageId) || !ValidModulePath(modulePath)) return false;
            ModContentPack provider = null;
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (!string.Equals(mod.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(mod.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase)) continue;
                if (provider != null) throw new InvalidOperationException("More than one active Mod provides '" + packageId + "'.");
                provider = mod;
            }
            if (provider == null) return false;
            string relative = modulePath.Replace('.', Path.DirectorySeparatorChar) + ".oxy";
            for (int i = 0; i < provider.foldersToLoadDescendingOrder.Count; i++)
            {
                string path = Path.Combine(provider.foldersToLoadDescendingOrder[i], "Oxygen", relative);
                if (!File.Exists(path)) continue;
                source = File.ReadAllText(path);
                return true;
            }
            return false;
        }

        private static bool ValidModulePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            bool first = true;
            for (int i = 0; i < path.Length; i++)
            {
                char character = path[i];
                if (character == '.')
                {
                    if (first) return false;
                    first = true;
                }
                else
                {
                    if (first ? character != '_' && !char.IsLetter(character)
                        : character != '_' && !char.IsLetterOrDigit(character)) return false;
                    first = false;
                }
            }
            return !first;
        }
    }
}
