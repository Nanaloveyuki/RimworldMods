using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using KeepYourIdentity;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

internal static class Program
{
    private static readonly Queue<Pawn> newborns = new();
    private static readonly List<Pawn> delivered = new();
    private static int litterSize;
    private static int nextId = 100;
    private static string temporary = null!;

    private static void Main()
    {
        string game = Environment.GetEnvironmentVariable("RIMWORLD_DIR") ?? "/mnt/e/Apps/Steam/steamapps/common/RimWorld";
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(game, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        try { Run(); }
        finally { if (temporary != null) Directory.Delete(temporary, true); }
    }

    private static T Empty<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static FieldInfo Field(Type type, string name) => AccessTools.Field(type, name);
    private static MethodInfo Method(string name) => AccessTools.Method(typeof(Program), name);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        temporary = Path.Combine(Path.GetTempPath(), "kyi-birth-" + Guid.NewGuid());
        string mods = Path.Combine(temporary, "Mods");
        Directory.CreateDirectory(Path.Combine(mods, "Core", "About"));
        File.WriteAllText(Path.Combine(mods, "Core", "About", "About.xml"), "<ModMetaData><name>Core</name><packageId>ludeon.rimworld</packageId><author>Ludeon</author><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>");
        Field(typeof(GenFilePaths), "saveDataPath").SetValue(null, temporary);
        foreach (string name in new[] { "modsFolderPath", "officialModsFolderPath" }) Field(typeof(GenFilePaths), name).SetValue(null, mods);
        Field(typeof(Prefs), "data").SetValue(null, new PrefsData());
        DeepProfiler.enabled = false;
        _ = ModsConfig.BiotechActive;
        Current.Game = Empty<Game>();
        Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
        Current.Game.tickManager = new TickManager();
        Current.Game.letterStack = new LetterStack();
        Current.Game.uniqueIDsManager = new UniqueIDsManager();
        Current.Game.playSettings = new PlaySettings();
        Current.Game.questManager = new QuestManager();
        Current.Game.history = Empty<History>();
        Current.Game.history.archive = new Archive();
        Current.Game.World = Empty<World>();
        Current.Game.World.factionManager = new FactionManager();
        var player = new Faction { def = new FactionDef { isPlayer = true }, loadID = 1 };
        Field(typeof(FactionManager), "ofPlayer").SetValue(Find.FactionManager, player);
        Find.FactionManager.AllFactionsListForReading.Add(player);
        DefDatabase<WorkTypeDef>.Add(new WorkTypeDef { defName = "HarnessWork" });
        DefDatabase<LetterDef>.Add(new LetterDef { defName = "KYI_BirthIdentity", letterClass = typeof(ChoiceLetter_BirthIdentity) });
        var language = Empty<LoadedLanguage>();
        language.info = new LanguageInfo();
        Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
        language.keyedReplacements = new();
        foreach (var pair in new Dictionary<string, string> {
            ["KYI_BirthIdentityLabel"]="Newborn identity", ["KYI_BirthIdentityText"]="Choose an identity.",
            ["KYI_Prisoner"]="prisoner", ["KYI_Slave"]="slave", ["KYI_Colonist"]="colonist" })
            language.keyedReplacements.Add(pair.Key, new LoadedLanguage.KeyedReplacement { key = pair.Key, value = pair.Value });
        LanguageDatabase.activeLanguage = language;

        var engineBoundary = new Harmony("kyi.harness.engine");
        engineBoundary.Patch(AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) }), prefix: new HarmonyMethod(Method(nameof(GenerateFixture))));
        var birth = AccessTools.Method(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome));
        engineBoundary.Patch(birth, prefix: new HarmonyMethod(Method(nameof(DeliverFirst))) { priority = Priority.Last }, postfix: new HarmonyMethod(Method(nameof(DeliverLitter))));
        new Harmony("nanaloveyuki.keepyouridentity").PatchAll(typeof(KeepYourIdentityMod).Assembly);
        foreach (int priority in new[] { Priority.First, Priority.Last })
        {
            engineBoundary.Unpatch(birth, HarmonyPatchType.Postfix, engineBoundary.Id);
            engineBoundary.Patch(birth, postfix: new HarmonyMethod(Method(nameof(DeliverLitter))) { priority = priority });
            foreach (int count in new[] { 12, 5 }) CheckLitter(count, GuestStatus.Prisoner);
            CheckLitter(5, GuestStatus.Slave);
            CheckLitter(5, GuestStatus.Guest);
        }
        CheckLitter(1, GuestStatus.Prisoner);
        int before = Find.LetterStack.LettersListForReading.Count;
        var unrelated = Fixture(GuestStatus.Guest);
        newborns.Enqueue(unrelated);
        Generate(unrelated);
        Require(unrelated.guest.GuestStatus == GuestStatus.Guest && Find.LetterStack.LettersListForReading.Count == before,
            "newborn generation outside delivery does not inherit the previous mother's identity");
    }

    private static Pawn Fixture(GuestStatus status)
    {
        var pawn = new Pawn { thingIDNumber = nextId++, gender = Gender.Female, def = new ThingDef { defName = "HarnessHuman", category = ThingCategory.Pawn, race = new RaceProperties { intelligence = Intelligence.Humanlike } }, Name = new NameSingle("Harness") };
        Field(typeof(RaceProperties), "fleshType").SetValue(pawn.RaceProps, new FleshTypeDef { isOrganic = true });
        pawn.SetFactionDirect(Faction.OfPlayer);
        pawn.kindDef = new PawnKindDef { initialResistanceRange = new FloatRange(0, 0), initialWillRange = new FloatRange(0, 0) };
        pawn.health = new Pawn_HealthTracker(pawn) { surgeryBills = new BillStack(pawn) };
        pawn.guest = new Pawn_GuestTracker(pawn);
        Field(typeof(Pawn_GuestTracker), "guestStatusInt").SetValue(pawn.guest, status);
        Field(typeof(Pawn_GuestTracker), "hostFactionInt").SetValue(pawn.guest, status == GuestStatus.Prisoner ? Faction.OfPlayer : null);
        Field(typeof(Pawn_GuestTracker), "slaveFactionInt").SetValue(pawn.guest, status == GuestStatus.Slave ? Faction.OfPlayer : null);
        pawn.mindState = new Pawn_MindState(pawn);
        pawn.jobs = new Pawn_JobTracker(pawn);
        pawn.inventory = new Pawn_InventoryTracker(pawn);
        pawn.equipment = new Pawn_EquipmentTracker(pawn);
        pawn.apparel = new Pawn_ApparelTracker(pawn);
        pawn.workSettings = new Pawn_WorkSettings(pawn);
        Field(typeof(Pawn_WorkSettings), "priorities").SetValue(pawn.workSettings, new DefMap<WorkTypeDef, int>());
        pawn.foodRestriction = Empty<Pawn_FoodRestrictionTracker>();
        pawn.outfits = Empty<Pawn_OutfitTracker>();
        pawn.drugs = Empty<Pawn_DrugPolicyTracker>();
        pawn.timetable = Empty<Pawn_TimetableTracker>();
        pawn.reading = Empty<Pawn_ReadingTracker>();
        pawn.inventoryStock = Empty<Pawn_InventoryStockTracker>();
        pawn.playerSettings = new Pawn_PlayerSettings(pawn);
        return pawn;
    }

    private static void CheckLitter(int count, GuestStatus status)
    {
        Find.LetterStack.LettersListForReading.Clear();
        delivered.Clear(); newborns.Clear(); litterSize = count;
        var mother = Fixture(status);
        for (int i = 0; i < count; i++) newborns.Enqueue(Fixture(GuestStatus.Guest));
        PregnancyUtility.ApplyBirthOutcome(null, 1f, null, null, mother, mother);
        int letters = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_BirthIdentity>().Count();
        int expected = status == GuestStatus.Guest ? 0 : count;
        Require(letters == expected, $"{status} litter: identity letters {letters}/{count}, expected {expected}");
        Require(delivered.All(p => p.guest.GuestStatus == status), $"{status} litter: every newborn receives maternal identity");
        var targets = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_BirthIdentity>()
            .Select(letter => (Pawn)Field(typeof(ChoiceLetter_BirthIdentity), "baby").GetValue(letter)!).ToHashSet();
        Require(targets.Count == expected && (expected == 0 || delivered.All(targets.Contains)),
            $"{status} litter: each newborn has exactly one identity letter");
    }

    // Replace only engine generation/delivery: replay RHAH's first return + extra GeneratePawn calls.
    private static bool GenerateFixture(ref Pawn __result) { __result = newborns.Dequeue(); return false; }
    private static Pawn Generate(Pawn mother) => PawnGenerator.GeneratePawn(new PawnGenerationRequest
    {
        KindDef = mother.kindDef, Faction = mother.Faction,
        AllowedDevelopmentalStages = DevelopmentalStage.Newborn
    });
    private static bool DeliverFirst(Thing birtherThing, ref Thing __result)
    {
        var baby = Generate((Pawn)birtherThing); delivered.Add(baby); __result = baby; return false;
    }
    private static void DeliverLitter(Thing birtherThing)
    {
        for (int i = 1; i < litterSize; i++) delivered.Add(Generate((Pawn)birtherThing));
    }
}
