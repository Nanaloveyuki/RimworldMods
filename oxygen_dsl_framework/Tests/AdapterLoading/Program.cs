using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml;

internal static class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 4)
            throw new ArgumentException("Usage: AdapterLoading FRAMEWORK_ROOT RHAH_API_DLL GAME_ROOT HARMONY_DLL");
        string framework = Path.GetFullPath(args[0]);
        string api = Path.GetFullPath(args[1]);
        string gameRoot = Path.GetFullPath(args[2]);
        string harmony = Path.GetFullPath(args[3]);
        var folders = new XmlDocument();
        folders.Load(Path.Combine(framework, "LoadFolders.xml"));
        XmlNode version = folders.SelectSingleNode("/loadFolders/v1.7");
        if (version == null) throw new InvalidOperationException("LoadFolders.xml does not declare RimWorld 1.7.");
        var directories = new List<string>();
        foreach (XmlElement folder in version.SelectNodes("li"))
        {
            string optional = folder.GetAttribute("IfModActive");
            if (optional.Length != 0 && !string.Equals(optional, "nanaloveyuki.ratkin.hungerandhavoc", StringComparison.OrdinalIgnoreCase)) continue;
            string directory = Path.Combine(framework, folder.InnerText == "/" ? "" : folder.InnerText, "Assemblies");
            if (Directory.Exists(directory)) directories.Add(directory);
        }
        if (directories.Count == 0) throw new InvalidOperationException("No framework assembly folders were found for RimWorld 1.7.");
        var dependencies = new List<string>(directories) { Path.GetDirectoryName(harmony), gameRoot };
        AssemblyLoadContext context = AssemblyLoadContext.Default;
        context.Resolving += (loader, name) =>
        {
            foreach (string directory in dependencies)
            {
                string path = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(path)) return loader.LoadFromAssemblyPath(path);
            }
            return null;
        };
        context.LoadFromAssemblyPath(Path.Combine(gameRoot, "UnityStubs.dll"));
        context.LoadFromAssemblyPath(Path.Combine(gameRoot, "Assembly-CSharp.dll"));
        context.LoadFromAssemblyPath(harmony);
        foreach (string directory in directories)
        {
            string[] files = Directory.GetFiles(directory, "*.dll");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files) context.LoadFromAssemblyPath(file).GetTypes();
        }
        string apiName = AssemblyName.GetAssemblyName(api).Name;
        foreach (Assembly loaded in context.Assemblies)
            if (loaded.GetName().Name == apiName) throw new InvalidOperationException("The RHAH API was loaded before framework type scanning completed.");
        dependencies.Add(Path.GetDirectoryName(api));
        Type behavior = context.LoadFromAssemblyPath(api).GetType("HungerAndHavoc.Api.IRHAH_PawnBehavior", true);
        Assembly adapter = context.LoadFromAssemblyPath(Path.Combine(framework, "1.7", "Rhah", "Assemblies", "Oxygendsl.Rhah.dll"));
        adapter.GetTypes();
        Type implementation = adapter.GetType("Oxygendsl.Rhah.RhahBehaviorAdapter", true);
        if (!behavior.IsAssignableFrom(implementation)) throw new Exception("Adapter does not implement the loaded RHAH API.");
        implementation.GetMethod("Activate").Invoke(null, null);
        implementation.GetMethod("Activate").Invoke(null, null);
        Type registry = behavior.Assembly.GetType("HungerAndHavoc.Api.RHAH_PawnBehaviors", true);
        var handlers = (System.Collections.IList)registry.GetField("handlers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        if (handlers.Count != 1 || handlers[0].GetType() != implementation)
            throw new Exception("Adapter activation must register exactly one behavior.");
        Console.WriteLine("ODF-first type scanning and deferred adapter registration passed.");
    }
}
