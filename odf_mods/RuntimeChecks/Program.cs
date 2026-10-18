using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using Odf.PrisonerNames;
using RimWorld;
using Verse;

internal static class Program
{
    static readonly List<Pawn> colony = new();
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static GameComponent_PrisonerNames runtime;
    static int passed;
    static T Raw<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
    static void Check(bool condition, string scenario)
    {
        if (!condition) throw new Exception(scenario);
        passed++;
        Console.WriteLine("PASS " + scenario);
    }
    static bool ColonyPrefix(ref List<Pawn> __result) { __result = colony; return false; }

    static void Main()
    {
        // Native graphics are not involved; only the map/caravan enumeration boundary is replaced.
        string game = Environment.GetEnvironmentVariable("RIMWORLD_DIR") ?? "/mnt/e/Apps/Steam/steamapps/common/RimWorld";
        AssemblyLoadContext.Default.Resolving += (context, name) => {
            foreach (string folder in new[] { game, Path.Combine(game, "Mods/Oxygendsl/1.7/Assemblies"), Path.Combine(game, "Mods/IrisMenus/1.7/Assemblies") })
            {
                string path = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            return null;
        };
        Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        AccessTools.Field(typeof(Prefs), "data").SetValue(null, Raw<PrefsData>());
        var game = Raw<Game>();
        game.components = new List<GameComponent>();
        runtime = new GameComponent_PrisonerNames(game);
        game.components.Add(runtime);
        Current.Game = game;
        Current.ProgramState = ProgramState.Playing;
        Set(runtime, "restored", true);
        var boundary = new Harmony("odf.prisonernames.runtimechecks");
        boundary.Patch(AccessTools.Method(typeof(GameComponent_PrisonerNames), "ColonyPrisoners"), prefix: new HarmonyMethod(typeof(Program), nameof(ColonyPrefix)));
        NamePatches.Install();
        Pawn pawn = NewPawn(1, new NameTriple("First", "Original", "Last"));
        Apply("return pawn.original_name + \"-001\"");
        Check(Nick(pawn) == "Original-001" && pawn.LabelShort == "Original-001", "automatic codename is the real nickname");
        Check(((NameTriple)pawn.Name).First == "First" && ((NameTriple)pawn.Name).Last == "Last", "automatic naming preserves first and last names");
        for (int i = 0; i < 3; i++) runtime.RefreshNow();
        Check(Nick(pawn) == "Original-001" && PrisonerFacts.OriginalShort(pawn) == "Original", "refresh never recursively formats its own nickname");
        pawn.Name = new NameTriple("EditedFirst", "PlayerChoice", "EditedLast");
        Check(runtime.Prisoners.Single().ManualAlias == "PlayerChoice", "external rename is captured immediately");
        Apply("return \"ChangedRule\"");
        runtime.RefreshNow();
        Check(Nick(pawn) == "PlayerChoice", "external nickname beats refreshed and replaced rules");
        runtime.SetEnabled(false);
        Check(pawn.Name.ToStringFull == "EditedFirst 'PlayerChoice' EditedLast", "disabling never undoes an external full-name edit");
        runtime.SetEnabled(true);
        Check(runtime.SetManual(pawn, "RosterChoice") && Nick(pawn) == "RosterChoice", "roster alias writes the real nickname");
        RoundtripRecords();
        Check(Nick(pawn) == "RosterChoice", "record restore keeps a roster override");
        runtime.SetEnabled(false);
        Check(Nick(pawn) == "PlayerChoice", "disabling a roster override restores the latest player name");
        runtime.SetEnabled(true);
        runtime.SetManual(pawn, "");
        Check(Nick(pawn) == "ChangedRule", "clearing override resumes automatic naming");
        Apply("return pawn.original_name + \"-again\"");
        Check(Nick(pawn) == "PlayerChoice-again", "clearing override does not lose the player baseline");
        RoundtripRecords();
        runtime.RefreshNow();
        Check(Nick(pawn) == "PlayerChoice-again", "saved original name prevents recursive naming after reload");
        runtime.FinalizeInit();
        runtime.LoadedGame();
        Check(Nick(pawn) == "PlayerChoice-again", "repeated game initialization keeps name ownership");
        runtime.SetSettingsScope(false);
        runtime.SetSettingsScope(true);
        Check(Nick(pawn) == "PlayerChoice-again", "settings scope changes preserve live original-name records");
        Apply("return null");
        Check(Nick(pawn) == "PlayerChoice", "null rule restores original nickname");
        Apply("return \"ReleaseAlias\"");
        colony.Clear();
        Set(pawn.guest, "guestStatusInt", GuestStatus.Guest);
        runtime.RefreshNow();
        Check(Nick(pawn) == "PlayerChoice", "release restores only the mod-owned name");
        Set(pawn.guest, "guestStatusInt", GuestStatus.Prisoner);
        colony.Add(pawn);
        runtime.RefreshNow();
        Check(Nick(pawn) == "ReleaseAlias", "recapture resumes naming");
        Set(pawn.health, "healthState", PawnHealthState.Dead);
        colony.Clear();
        runtime.RefreshNow();
        Check(Nick(pawn) == "PlayerChoice", "death restores original name");
        Set(pawn.health, "healthState", PawnHealthState.Mobile);
        colony.Add(pawn);
        runtime.RefreshNow();
        pawn.Name = new NameTriple("New", "AfterRelease", "Person");
        colony.Clear();
        runtime.RefreshNow();
        Check(Nick(pawn) == "AfterRelease", "retirement preserves the last external edit");
        Pawn single = NewPawn(2, new NameSingle("SingleOriginal", true));
        Apply("return \"SingleAlias\"");
        Check(single.Name is NameSingle && Nick(single) == "SingleAlias", "single names remain editable single names");
        runtime.SetEnabled(false);
        Check(single.Name.Numerical && Nick(single) == "SingleOriginal", "single-name restoration retains numerical flag");
        runtime.SetEnabled(true);
        single.Name = new NameSingle("SinglePlayer");
        RoundtripRecords();
        Check(Nick(single) == "SinglePlayer", "single external rename survives record restore");
        Pawn early = NewPawn(4, new NameTriple("Early", "Initial", "Edit"));
        early.Name = new NameTriple("Early", "BeforeRefresh", "Edit");
        runtime.RefreshNow();
        Check(Nick(early) == "BeforeRefresh", "player edit before first refresh wins");
        Set(single, "nameInt", new NameSingle("DirectWrite"));
        runtime.RefreshNow();
        Check(Nick(single) == "DirectWrite", "refresh detects external writes bypassing the property setter");
        runtime.SetEnabled(false);
        single.Name = new NameSingle("EditedWhileDisabled");
        runtime.SetEnabled(true);
        Check(Nick(single) == "EditedWhileDisabled", "disabled-state external edits remain authoritative");
        Pawn automatic = NewPawn(5, new NameTriple("Error", "Baseline", "Case"));
        runtime.RefreshNow();
        string automaticBefore = Nick(automatic);
        string kept = Nick(single);
        Check(!runtime.Apply("fn codename(pawn): string? { return pawn.missing }"), "invalid rule is rejected");
        Check(Nick(single) == kept, "failed rule application retains the actual nickname");
        Check(Nick(automatic) == automaticBefore, "rule failure leaves automatic prisoner nickname unchanged");
        Pawn old = NewPawn(3, new NameTriple("Old", "LegacyOriginal", "Save"));
        var legacy = new GameComponent_PrisonerNames.SavedEntry { Pawn = old, Number = 42, Manual = "LegacyManual" };
        Set(runtime, "restored", false);
        Set(runtime, "saved", new List<GameComponent_PrisonerNames.SavedEntry> { legacy });
        AccessTools.Method(typeof(GameComponent_PrisonerNames), "Restore").Invoke(runtime, null);
        Check(Nick(old) == "LegacyManual" && PrisonerFacts.OriginalShort(old) == "LegacyOriginal", "old display-only saves migrate without losing original names");
        runtime.SetEnabled(false);
        Check(Nick(old) == "LegacyOriginal", "migrated manual override can restore original");
        Console.WriteLine($"Real-assembly nickname checks passed: {passed}");
    }

    static void Apply(string body)
    {
        if (!runtime.Apply("fn codename(pawn): string? { " + body + " }")) throw new Exception(runtime.LastError);
    }
    static string Nick(Pawn pawn) => pawn.Name.ToStringShort;
    static Pawn NewPawn(int id, Name name)
    {
        var pawn = Raw<Pawn>();
        pawn.thingIDNumber = id;
        pawn.def = new ThingDef { defName = "Human", race = new RaceProperties { intelligence = Intelligence.Humanlike } };
        pawn.health = new Pawn_HealthTracker(pawn);
        Set(pawn.health, "healthState", PawnHealthState.Mobile);
        pawn.guest = new Pawn_GuestTracker(pawn);
        var faction = Raw<Faction>();
        faction.def = new FactionDef { isPlayer = true };
        Set(pawn.guest, "hostFactionInt", faction);
        Set(pawn.guest, "guestStatusInt", GuestStatus.Prisoner);
        // Name assignment precedes colony membership and tests the pre-first-refresh rename case separately.
        Set(pawn, "nameInt", name);
        colony.Add(pawn);
        return pawn;
    }

    static void RoundtripRecords()
    {
        var entries = (List<GameComponent_PrisonerNames.SavedEntry>)AccessTools.Method(typeof(GameComponent_PrisonerNames), "Capture", Type.EmptyTypes).Invoke(runtime, null);
        foreach (var entry in entries)
        {
            string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xml");
            try
            {
                PrisonerNameState names = entry.Names;
                Scribe.saver.InitSaving(file, "root");
                Scribe_Deep.Look(ref names, "names");
                Scribe.saver.FinalizeSaving();
                names = null;
                Scribe.loader.InitLoading(file);
                Scribe_Deep.Look(ref names, "names");
                Scribe.loader.FinalizeLoading();
                entry.Names = names;
            }
            finally { Scribe.ForceStop(); File.Delete(file); }
        }
        Set(runtime, "restored", false);
        Set(runtime, "saved", entries);
        AccessTools.Method(typeof(GameComponent_PrisonerNames), "Restore").Invoke(runtime, null);
    }
}
