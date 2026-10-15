using System;
using System.IO;
using Oxygendsl.Language;
using Verse;

namespace Odf.PrisonerNames
{
    public sealed class GameModuleCatalog : IOxygenModuleCatalog
    {
        public bool TryGetSource(string packageId, string modulePath, out string source)
        {
            source = null;
            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrEmpty(modulePath)) return false;
            foreach (string segment in modulePath.Split('.'))
            {
                if (segment.Length == 0 || (segment[0] != '_' && !char.IsLetter(segment[0]))) return false;
                for (int i = 1; i < segment.Length; i++)
                    if (segment[i] != '_' && !char.IsLetterOrDigit(segment[i])) return false;
            }
            ModContentPack provider = null;
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
                if (string.Equals(mod.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mod.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    if (provider != null) throw new InvalidOperationException("Multiple active mods provide " + packageId);
                    provider = mod;
                }
            if (provider == null) return false;
            string relative = modulePath.Replace('.', Path.DirectorySeparatorChar) + ".oxy";
            foreach (string folder in provider.foldersToLoadDescendingOrder)
            {
                string path = Path.Combine(folder, "Oxygen", relative);
                if (!File.Exists(path)) continue;
                source = File.ReadAllText(path);
                return true;
            }
            return false;
        }
    }
}
