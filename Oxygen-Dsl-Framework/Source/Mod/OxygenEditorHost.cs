using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Oxygendsl.Mod.Editor;
using Oxygendsl.Language;
using UnityEngine;
using Verse;

namespace Oxygendsl.Mod
{
    public static class OxygenEditorHost
    {
        public const string PackageId = "nanaloveyuki.oxygendsl";
        const int MaxSourceLength = 1024 * 1024;
        static readonly Dictionary<string, Func<IEnumerable<OxygenEditorDocument>>> documentProviders =
            new Dictionary<string, Func<IEnumerable<OxygenEditorDocument>>>(StringComparer.OrdinalIgnoreCase);
        static readonly List<ModContentPack> providers = new List<ModContentPack>();
        static volatile OxygenEditorServer server;
        static string assetRoot;
        static bool installed;
        public static bool Running
        {
            get { OxygenEditorServer current = server; return current != null && current.Running; }
        }
        public static IReadOnlyList<ModContentPack> Providers { get { return providers; } }

        internal static void Install(ModContentPack framework)
        {
            if (installed) return;
            installed = true;
            assetRoot = Path.Combine(framework.RootDir, "WebEditor");
            AppDomain.CurrentDomain.ProcessExit += delegate { Stop(); };
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                providers.Clear();
                foreach (ModContentPack provider in LoadedModManager.RunningModsListForReading)
                    if (HasOxygenFiles(provider) || documentProviders.ContainsKey(provider.PackageIdPlayerFacing)) providers.Add(provider);
                var gameObject = new GameObject("Oxygen local editor");
                gameObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                gameObject.AddComponent<OxygenEditorPump>();
                Application.quitting += Stop;
            });
        }

        public static void RegisterDocuments(string packageId, Func<IEnumerable<OxygenEditorDocument>> provider)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentException("An editor package is required.", nameof(packageId));
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (documentProviders.ContainsKey(packageId)) throw new ArgumentException("Editor documents already registered for " + packageId, nameof(packageId));
            documentProviders.Add(packageId, provider);
        }

        public static void Open(string packageId, string documentId = null)
        {
            if (FindProvider(packageId) == null) throw new ArgumentException("Only active Mods with Oxygen files can be edited: " + packageId, nameof(packageId));
            if (!Running)
            {
                if (!File.Exists(Path.Combine(assetRoot, "index.html")))
                    throw new InvalidOperationException("The bundled Oxygen web editor is missing. Reinstall the complete Oxygendsl Mod.");
                var candidate = new OxygenEditorServer(assetRoot, Documents, CreateDocument);
                try { candidate.Start(); }
                catch { candidate.Dispose(); throw; }
                server = candidate;
            }
            string url = server.Url + "&package=" + Uri.EscapeDataString(packageId);
            if (!string.IsNullOrEmpty(documentId)) url += "&document=" + Uri.EscapeDataString(documentId);
            Application.OpenURL(url);
        }

        public static void Stop()
        {
            OxygenEditorServer previous = server;
            server = null;
            previous?.Dispose();
        }

        internal static void Update()
        {
            if (!LongEventHandler.ShouldWaitForEvent) server?.Pump();
        }

        static bool HasOxygenFiles(ModContentPack provider)
        {
            foreach (string folder in provider.foldersToLoadDescendingOrder)
                if (Directory.Exists(Path.Combine(folder, "Oxygen"))) return true;
            return false;
        }

        static ModContentPack FindProvider(string packageId)
        {
            for (int index = 0; index < providers.Count; index++)
                if (string.Equals(providers[index].PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase)) return providers[index];
            return null;
        }

        static IReadOnlyList<OxygenEditorDocument> Documents()
        {
            var documents = new List<OxygenEditorDocument>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModContentPack provider in providers)
            {
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string folder in provider.foldersToLoadDescendingOrder)
                {
                    string directory = Path.Combine(folder, "Oxygen");
                    if (!Directory.Exists(directory)) continue;
                    CheckPath(provider.RootDir, directory);
                    var directories = new Stack<string>();
                    directories.Push(directory);
                    while (directories.Count != 0)
                    {
                        string current = directories.Pop();
                        foreach (string child in Directory.GetDirectories(current))
                        {
                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                            directories.Push(child);
                        }
                        foreach (string file in Directory.GetFiles(current))
                        {
                            if (!AllowedExtension(file) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                            string relative = file.Substring(directory.Length + 1).Replace('\\', '/');
                            if (!paths.Add(relative)) continue;
                            Add(documents, ids, FileDocument(provider, file, relative));
                        }
                    }
                }
                if (documentProviders.TryGetValue(provider.PackageIdPlayerFacing, out Func<IEnumerable<OxygenEditorDocument>> factory))
                {
                    IEnumerable<OxygenEditorDocument> extra = factory();
                    if (extra == null) continue;
                    foreach (OxygenEditorDocument document in extra)
                    {
                        if (document == null) throw new InvalidOperationException("An editor provider returned a missing document.");
                        document.PackageId = provider.PackageIdPlayerFacing;
                        Add(documents, ids, document);
                    }
                }
            }
            documents.Sort((left, right) => string.Compare(left.PackageId + "/" + left.Path, right.PackageId + "/" + right.Path, StringComparison.Ordinal));
            return documents;
        }

        static void Add(List<OxygenEditorDocument> documents, HashSet<string> ids, OxygenEditorDocument document)
        {
            if (string.IsNullOrWhiteSpace(document.Id) || !ids.Add(document.Id)) throw new InvalidOperationException("Duplicate or missing editor document ID: " + document.Id);
            documents.Add(document);
        }

        static OxygenEditorDocument FileDocument(ModContentPack provider, string file, string relative)
        {
            string full = Path.GetFullPath(file);
            string packageId = provider.PackageIdPlayerFacing;
            string root = provider.RootDir;
            bool blocks = full.EndsWith(".oxyblocks", StringComparison.OrdinalIgnoreCase);
            return new OxygenEditorDocument
            {
                Id = packageId + ":file:" + full,
                PackageId = packageId,
                Title = provider.Name + " / " + relative,
                Path = "Oxygen/" + relative,
                Kind = blocks ? "blocks" : "text",
                Writable = (File.GetAttributes(full) & FileAttributes.ReadOnly) == 0,
                IsModule = false,
                Read = () => ReadFile(root, full),
                Write = source => WriteFile(root, full, source),
                Apply = blocks ? null : (Func<OxygenEditorActionResult>)(() => ApplyRules(packageId))
            };
        }


        static OxygenEditorActionResult ApplyRules(string packageId)
        {
            bool success = OxygenContentHost.ReloadRulesFor(packageId, null);
            return new OxygenEditorActionResult { Success = success, Message = success ? "Oxygen runtime rules and settings applied. Native content changes still require a restart."
                : "Oxygen files were not applied. Check content inspection; native content changes require a restart." };
        }

        static OxygenEditorDocument CreateDocument(string packageId, string relative, string source)
        {
            ModContentPack provider = FindProvider(packageId) ?? throw new ArgumentException("The selected Mod is not active or has no Oxygen files.");
            if (string.IsNullOrWhiteSpace(relative) || relative.IndexOf('\\') >= 0 || relative.IndexOf(':') >= 0
                || !relative.StartsWith("Oxygen/", StringComparison.Ordinal) || !AllowedExtension(relative))
                throw new ArgumentException("New files must be .oxy or .oxyblocks files under Oxygen/.");
            string[] segments = relative.Split('/');
            foreach (string segment in segments)
                if (segment.Length == 0 || segment == "." || segment == ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new ArgumentException("The file path has an invalid segment.");
            CheckSource(source);
            foreach (OxygenEditorDocument document in Documents())
                if (string.Equals(document.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(document.Path, relative, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("This file already exists in an active loading folder.");
            string file = Path.Combine(provider.RootDir, relative.Replace('/', Path.DirectorySeparatorChar));
            CheckPath(provider.RootDir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            CheckPath(provider.RootDir, file);
            using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(source);
            return FileDocument(provider, file, relative.Substring("Oxygen/".Length));
        }

        static bool AllowedExtension(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".oxy", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".oxyblocks", StringComparison.OrdinalIgnoreCase);
        }

        static string ReadFile(string root, string file)
        {
            CheckPath(root, file);
            if (new FileInfo(file).Length > MaxSourceLength * 4L) throw new IOException("The editor file exceeds the source limit.");
            string source = File.ReadAllText(file);
            CheckSource(source);
            return source;
        }

        static void WriteFile(string root, string file, string source)
        {
            CheckPath(root, file);
            CheckSource(source);
            string temporary = file + ".oxygen-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(source);
                CheckPath(root, file);
                File.Replace(temporary, file, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        static void CheckSource(string source)
        {
            if (source == null || source.Length > MaxSourceLength) throw new ArgumentException("Source must be at most 1 MiB of UTF-16 text.");
        }

        static void CheckPath(string root, string file)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string full = Path.GetFullPath(file);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison)) throw new IOException("The editor file escapes its Mod directory.");
            string path = fullRoot;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Editing a redirected Mod root is not allowed.");
            foreach (string segment in full.Substring(fullRoot.Length + 1).Split(Path.DirectorySeparatorChar))
            {
                path = Path.Combine(path, segment);
                if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Editing through filesystem redirects is not allowed.");
            }
        }
    }

    public sealed class OxygenEditorPump : MonoBehaviour
    {
        void Update() { OxygenEditorHost.Update(); }
        void OnApplicationQuit() { OxygenEditorHost.Stop(); }
    }
}
