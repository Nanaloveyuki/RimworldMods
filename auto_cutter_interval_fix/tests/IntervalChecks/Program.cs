using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml;
using AutoCutterIntervalFix;
using HarmonyLib;
using RimWorld;
using Verse;

internal static class Program
{
    private static Type cutter;
    private static FieldInfo interval;
    private static MethodInfo tick;
    private static MethodInfo exposeData;
    private static int workCalls;

    private static void Main(string[] args)
    {
        string game = Environment.GetEnvironmentVariable("RIMWORLD_DIR")
            ?? "/mnt/e/Apps/Steam/steamapps/common/RimWorld";
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(game, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        Run(args.Length == 0
            ? Path.Combine(game, "Mods/3492083520/1.7/Assemblies/NetLingItemCuter.dll")
            : Path.GetFullPath(args[0]));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string original)
    {
        Current.Game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
        Current.Game.tickManager = (TickManager)RuntimeHelpers.GetUninitializedObject(typeof(TickManager));
        DeepProfiler.enabled = false;
        cutter = AssemblyLoadContext.Default.LoadFromAssemblyPath(original)
            .GetType("LingItemCuter.Building_ItemCuter", true);
        interval = cutter.GetField("WorkTick");
        tick = AccessTools.DeclaredMethod(cutter, "Tick");
        exposeData = AccessTools.DeclaredMethod(cutter, "ExposeData");

        var fixtures = new Harmony("autocutterintervalfix.checks.fixtures");
        fixtures.Patch(AccessTools.DeclaredMethod(typeof(Building_Storage), "ExposeData"),
            prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        fixtures.Patch(AccessTools.DeclaredMethod(typeof(ThingWithComps), "Tick"),
            prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        fixtures.Patch(AccessTools.DeclaredMethod(cutter, "GoWork"),
            prefix: new HarmonyMethod(typeof(Program), nameof(CountWork)));

        object broken = Load("<thing><CutCorpse>True</CutCorpse></thing>");
        Require(Read(broken) == 0, "original ExposeData loads a missing interval as zero");
        try
        {
            tick.Invoke(broken, null);
            throw new InvalidOperationException("Expected upstream division by zero.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is DivideByZeroException)
        {
            Console.WriteLine("PASS original Tick reproduces the logged DivideByZeroException");
        }

        Type patch = typeof(AutoCutterIntervalFixMod).Assembly.GetType("AutoCutterIntervalFix.IntervalPatch", true);
        AccessTools.Method(patch, "Install").Invoke(null, null);
        const string owner = "nanaloveyuki.autocutterintervalfix";
        Require(Harmony.GetPatchInfo(tick)?.Prefixes.Any(p => p.owner == owner) == true
            && Harmony.GetPatchInfo(exposeData)?.Postfixes.Any(p => p.owner == owner) == true,
            "production patches install against the actual upstream methods");

        foreach (string value in new[] { "", "<WorkTick>0</WorkTick>", "<WorkTick>-30</WorkTick>" })
        {
            object loaded = Load("<thing><CutCorpse>True</CutCorpse>" + value + "</thing>");
            Require(Read(loaded) == 60 && (bool)cutter.GetField("CutCorpse").GetValue(loaded),
                "missing or nonpositive interval recovers on load and preserves cutting settings: " + value);
        }
        foreach (int value in new[] { 1, 37, 60, 200, 250 })
        {
            object loaded = Load($"<thing><WorkTick>{value}</WorkTick></thing>");
            Require(Read(loaded) == value, "positive saved interval remains " + value);
            CheckCadence(loaded, value);
        }
        foreach (int value in new[] { 0, -1, int.MinValue })
        {
            interval.SetValue(broken, value);
            CheckCadence(broken, 60);
            Require(Read(broken) == 60, "Tick repairs a runtime interval of " + value);
        }

        var production = new Harmony(owner);
        production.Unpatch(tick, HarmonyPatchType.All, owner);
        production.Unpatch(exposeData, HarmonyPatchType.All, owner);
        Require(Read(Load("<thing/>")) == 0, "removing production patches restores the upstream load defect");
    }

    private static object Load(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        object machine = Activator.CreateInstance(cutter);
        ((Thing)machine).thingIDNumber = 50841;
        Scribe.loader.curXmlParent = document.DocumentElement;
        Scribe.mode = LoadSaveMode.LoadingVars;
        try
        {
            exposeData.Invoke(machine, null);
        }
        finally
        {
            Scribe.mode = LoadSaveMode.Inactive;
            Scribe.loader.curXmlParent = null;
        }
        return machine;
    }

    private static void CheckCadence(object machine, int expectedInterval)
    {
        workCalls = 0;
        for (int gameTick = 0; gameTick < expectedInterval * 3; gameTick++)
        {
            Find.TickManager.DebugSetTicksGame(gameTick);
            tick.Invoke(machine, null);
        }
        Require(workCalls == 3, "original Tick schedules exactly three work calls in three periods of " + expectedInterval);
    }

    private static int Read(object machine) => (int)interval.GetValue(machine);
    private static bool Skip() => false;
    private static bool CountWork()
    {
        workCalls++;
        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
