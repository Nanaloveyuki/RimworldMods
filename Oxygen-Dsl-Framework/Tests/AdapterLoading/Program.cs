using System;
using System.IO;
using System.Reflection;
using System.Xml;

internal static class Program
{
    static void Main(string[] args)
    {
        string framework = Path.GetFullPath(args[0]);
        string api = Path.GetFullPath(args[1]);
        string managed = Path.GetFullPath(args[2]);
        string harmony = Path.GetFullPath(args[3]);
        Assembly game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        Assembly.LoadFrom(harmony);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, request) =>
        {
            string path = Path.Combine(managed, new AssemblyName(request.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : game;
        };
        var folders = new XmlDocument();
        folders.Load(Path.Combine(framework, "LoadFolders.xml"));
        foreach (XmlElement folder in folders.SelectNodes("/loadFolders/v1.6/li"))
        {
            string optional = folder.GetAttribute("IfModActive");
            if (optional.Length != 0 && optional != "nanaloveyuki.ratkin.hungerandhavoc") continue;
            string directory = Path.Combine(framework, folder.InnerText == "/" ? "" : folder.InnerText, "Assemblies");
            if (!Directory.Exists(directory)) continue;
            string[] files = Directory.GetFiles(directory, "*.dll");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files) Assembly.LoadFrom(file).GetTypes();
        }
        Type behavior = Assembly.LoadFrom(api).GetType("HungerAndHavoc.Api.IRHAH_PawnBehavior", true);
        Assembly adapter = Assembly.LoadFrom(Path.Combine(framework, "Rhah", "Assemblies", "Oxygendsl.Rhah.dll"));
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
