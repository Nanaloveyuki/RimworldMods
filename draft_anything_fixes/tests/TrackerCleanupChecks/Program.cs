using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using MiliraFlightJobFix;
using RimWorld;
using RimWorld.Planet;
using Verse;

internal static class Program
{
    private static MethodInfo assign;
    private static MethodInfo restore;
    private static Type controlType;
    private static IDictionary controls;
    private static Faction player;
    private static Faction enemy;
    private static int nextId;

    private static void Main(string[] args)
    {
        string game = Environment.GetEnvironmentVariable("RIMWORLD_DIR") ?? "/mnt/e/Apps/Steam/steamapps/common/RimWorld";
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(game, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        string original = args.Length == 0
            ? Path.Combine(game, "Mods/3636082176/1.6/Assemblies/DraftAnything.dll")
            : Path.GetFullPath(args[0]);
        string temporary = Path.Combine(Path.GetTempPath(), "draft-anything-checks-" + Guid.NewGuid());
        try
        {
            Run(original, temporary);
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string original, string temporary)
    {
        InitializeGame(temporary);
        Assembly upstream = AssemblyLoadContext.Default.LoadFromAssemblyPath(original);
        Type utils = upstream.GetType("DraftAnything.Utils", true);
        assign = utils.GetMethod("AssignComponents");
        restore = utils.GetMethod("RestoreOriginalState");
        controlType = upstream.GetType("DraftAnything.CompControlPawn", true);
        controls = (IDictionary)controlType.GetField("instances").GetValue(null);

        Pawn broken = Fixture(player, existingTrackers: true);
        Restore(broken);
        Require(broken.playerSettings == null && broken.drafter == null,
            "unpatched upstream reproduces destruction of colonist trackers");

        Type patch = typeof(MiliraFlightJobFixMod).Assembly.GetType("MiliraFlightJobFix.DraftAnythingTrackerCleanup", true);
        AccessTools.Method(patch, "Install").Invoke(null, null);
        Require(Harmony.GetPatchInfo(restore)?.Transpilers.Count == 1,
            "production transpiler installs against the real upstream DLL");

        Pawn colonist = Fixture(player, existingTrackers: true);
        Pawn_PlayerSettings settings = colonist.playerSettings;
        Pawn_DraftController drafter = colonist.drafter;
        for (int cycle = 0; cycle < 3; cycle++)
        {
            SetControlled(colonist);
            Assign(colonist);
            Assign(colonist);
            Restore(colonist);
        }
        Require(ReferenceEquals(colonist.playerSettings, settings) && ReferenceEquals(colonist.drafter, drafter)
            && settings.displayOrder == 7 && settings.medCare == MedicalCareCategory.Best,
            "repeated draft cycles preserve colonist tracker identities and settings");

        Pawn temporaryPawn = Fixture(enemy, existingTrackers: false);
        Assign(temporaryPawn);
        Assign(temporaryPawn);
        Require(temporaryPawn.playerSettings != null && temporaryPawn.drafter != null,
            "upstream can still create trackers for a controlled enemy");
        Restore(temporaryPawn);
        Require(temporaryPawn.playerSettings == null && temporaryPawn.drafter == null && !Controlled(temporaryPawn),
            "release removes temporary enemy trackers and clears the control flag");
        SetControlled(temporaryPawn);
        Assign(temporaryPawn);
        Restore(temporaryPawn);
        Require(temporaryPawn.playerSettings == null && temporaryPawn.drafter == null,
            "a second enemy control cycle still cleans its temporary trackers");

        Pawn mixed = Fixture(enemy, existingTrackers: false);
        mixed.playerSettings = new Pawn_PlayerSettings(mixed) { displayOrder = 7 };
        Pawn_PlayerSettings existing = mixed.playerSettings;
        Assign(mixed);
        Restore(mixed);
        Require(ReferenceEquals(mixed.playerSettings, existing) && mixed.drafter == null,
            "mixed ownership preserves existing settings and removes only the added controller");

        Pawn replaced = Fixture(enemy, existingTrackers: false);
        Assign(replaced);
        replaced.playerSettings = new Pawn_PlayerSettings(replaced);
        replaced.drafter = new Pawn_DraftController(replaced);
        Pawn_PlayerSettings replacementSettings = replaced.playerSettings;
        Pawn_DraftController replacementDrafter = replaced.drafter;
        Restore(replaced);
        Require(ReferenceEquals(replaced.playerSettings, replacementSettings)
            && ReferenceEquals(replaced.drafter, replacementDrafter),
            "trackers replaced by another system are preserved");

        Pawn loaded = Fixture(enemy, existingTrackers: true);
        Pawn_PlayerSettings loadedSettings = loaded.playerSettings;
        Pawn_DraftController loadedDrafter = loaded.drafter;
        Restore(loaded);
        Require(ReferenceEquals(loaded.playerSettings, loadedSettings) && ReferenceEquals(loaded.drafter, loadedDrafter),
            "loaded trackers without runtime ownership evidence are preserved");

        Pawn colonyPawn = Fixture(player, existingTrackers: false);
        Assign(colonyPawn);
        Restore(colonyPawn);
        Require(colonyPawn.playerSettings != null && colonyPawn.drafter != null,
            "trackers now required by vanilla remain on a player colonist");
    }

    private static void InitializeGame(string temporary)
    {
        string mods = Path.Combine(temporary, "Mods");
        Directory.CreateDirectory(Path.Combine(mods, "Core/About"));
        File.WriteAllText(Path.Combine(mods, "Core/About/About.xml"),
            "<ModMetaData><name>Core</name><packageId>ludeon.rimworld</packageId><author>Ludeon</author><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>");
        AccessTools.Field(typeof(GenFilePaths), "saveDataPath").SetValue(null, temporary);
        foreach (string name in new[] { "modsFolderPath", "officialModsFolderPath" })
            AccessTools.Field(typeof(GenFilePaths), name).SetValue(null, mods);
        AccessTools.Field(typeof(Prefs), "data").SetValue(null, new PrefsData());
        DeepProfiler.enabled = false;
        _ = ModsConfig.BiotechActive;
        Current.Game = Empty<Game>();
        Current.Game.playSettings = new PlaySettings();
        Current.Game.World = Empty<World>();
        Current.Game.World.factionManager = new FactionManager();
        player = new Faction { def = new FactionDef { isPlayer = true }, loadID = 1 };
        enemy = new Faction { def = new FactionDef(), loadID = 2 };
        AccessTools.Field(typeof(Faction), "relations").SetValue(enemy,
            new List<FactionRelation> { new FactionRelation { other = player, kind = FactionRelationKind.Hostile } });
        AccessTools.Field(typeof(FactionManager), "ofPlayer").SetValue(Find.FactionManager, player);
    }

    private static Pawn Fixture(Faction faction, bool existingTrackers)
    {
        var pawn = new Pawn
        {
            thingIDNumber = ++nextId,
            def = new ThingDef
            {
                defName = "TrackerCheckHuman",
                category = ThingCategory.Pawn,
                race = new RaceProperties { intelligence = Intelligence.Humanlike }
            },
            equipment = Empty<Pawn_EquipmentTracker>(),
            skills = Empty<Pawn_SkillTracker>(),
            story = Empty<Pawn_StoryTracker>(),
            royalty = Empty<Pawn_RoyaltyTracker>()
        };
        pawn.SetFactionDirect(faction);
        if (existingTrackers)
        {
            pawn.playerSettings = new Pawn_PlayerSettings(pawn) { displayOrder = 7, medCare = MedicalCareCategory.Best };
            pawn.drafter = new Pawn_DraftController(pawn);
        }
        object comp = Activator.CreateInstance(controlType);
        controlType.GetField("originalFaction").SetValue(comp, faction);
        controls.Add(pawn, comp);
        SetControlled(pawn);
        return pawn;
    }

    private static T Empty<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Assign(Pawn pawn) => assign.Invoke(null, new object[] { pawn });
    private static void Restore(Pawn pawn) => restore.Invoke(null, new object[] { pawn });
    private static void SetControlled(Pawn pawn) => controlType.GetField("isControled").SetValue(controls[pawn], true);
    private static bool Controlled(Pawn pawn) => (bool)controlType.GetField("isControled").GetValue(controls[pawn]);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
