using System;
using System.Collections.Generic;
using System.IO;

namespace Oxygendsl.Language
{
    public sealed class OxygenSourceFile
    {
        internal OxygenSourceFile(string path, string relativePath) { Path = path; RelativePath = relativePath; }
        public string Path { get; }
        public string RelativePath { get; }
    }

    public static class OxygenSourceFiles
    {
        // Folders are highest priority first; only identical relative paths override each other.
        public static IReadOnlyList<OxygenSourceFile> Discover(IEnumerable<string> folders)
        {
            var files = new SortedDictionary<string, OxygenSourceFile>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                string root = System.IO.Path.Combine(folder, "Oxygen");
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!string.Equals(System.IO.Path.GetExtension(path), ".oxy", StringComparison.OrdinalIgnoreCase)) continue;
                    string relative = path.Substring(root.Length + 1).Replace('\\', '/');
                    if (!files.ContainsKey(relative)) files.Add(relative, new OxygenSourceFile(path, relative));
                }
            }
            return new List<OxygenSourceFile>(files.Values);
        }
    }
}
