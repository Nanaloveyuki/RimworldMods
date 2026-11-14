using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ApparelPoolTests : IDisposable
    {
        readonly List<ThingDef> previousDefs = new List<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading);
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly Harmony harmony = new Harmony("rhah.apparel.pool.tests");
        static Verse.Pawn childClothingPawn;

        public RHAH_ApparelPoolTests()
        {
            DefDatabase<ThingDef>.Clear();
            RHAH_Mod.Settings = new RHAH_Settings();
            harmony.Patch(AccessTools.Method(typeof(ApparelUtility), nameof(ApparelUtility.HasPartsToWear)),
                prefix: new HarmonyMethod(typeof(RHAH_ApparelPoolTests), nameof(HasParts)));
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            childClothingPawn = null;
            RHAH_Mod.Settings = previousSettings;
            DefDatabase<ThingDef>.Clear();
            foreach (ThingDef def in previousDefs) DefDatabase<ThingDef>.Add(def);
        }

        [Theory]
        [InlineData(DevelopmentalStage.Baby)]
        [InlineData(DevelopmentalStage.Child)]
        [InlineData(DevelopmentalStage.Adult)]
        public void OrdinaryPawnsUseTheApparelDevelopmentalFilter(DevelopmentalStage stage)
        {
            ThingDef baby = Clothing("Apparel_BabyOnesie", DevelopmentalStage.Baby, "ludeon.rimworld.biotech");
            ThingDef child = Clothing("RK_ApronSkirtChildren", DevelopmentalStage.Child);
            ThingDef adult = Clothing("RK_ApronSkirt", DevelopmentalStage.Adult);

            ThingDef actual = Assert.Single(RHAH_ApparelAssigner.Pool(Pawn(stage)));

            Assert.Same(stage == DevelopmentalStage.Baby ? baby : stage == DevelopmentalStage.Child ? child : adult, actual);
        }

        [Fact]
        public void AdultWithChildClothingPermissionKeepsTheSourceAndSettingsWhitelist()
        {
            ThingDef child = Clothing("RK_ApronSkirtChildren", DevelopmentalStage.Child);
            Clothing("RK_ApronSkirt", DevelopmentalStage.Adult);
            Clothing("Foreign_ChildDress", DevelopmentalStage.Child, "foreign.mod");
            childClothingPawn = Pawn(DevelopmentalStage.Adult);
            harmony.Patch(AccessTools.Method(typeof(ApparelProperties), nameof(ApparelProperties.PawnCanWear),
                    new[] { typeof(Verse.Pawn), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(RHAH_ApparelPoolTests), nameof(ChildClothingPermission)));

            Assert.Same(child, Assert.Single(RHAH_ApparelAssigner.Pool(childClothingPawn)));

            RHAH_Mod.Settings.SetRefugeeApparelEnabled(child.defName, false);
            Assert.Empty(RHAH_ApparelAssigner.Pool(childClothingPawn));
        }

        static ThingDef Clothing(string name, DevelopmentalStage stage, string packageId = "solaris.ratkinracemod")
        {
            ModContentPack pack = (ModContentPack)RuntimeHelpers.GetUninitializedObject(typeof(ModContentPack));
            AccessTools.Field(typeof(ModContentPack), "packageIdInt").SetValue(pack, packageId);
            AccessTools.Field(typeof(ModContentPack), "official").SetValue(pack, packageId.StartsWith("ludeon."));
            ThingDef def = new ThingDef
            {
                defName = name,
                thingClass = typeof(Apparel),
                modContentPack = pack,
                techLevel = TechLevel.Neolithic,
                stuffCategories = new List<StuffCategoryDef> { new StuffCategoryDef() },
                apparel = new ApparelProperties { countsAsClothingForNudity = true, developmentalStageFilter = stage }
            };
            DefDatabase<ThingDef>.Add(def);
            return def;
        }

        static Verse.Pawn Pawn(DevelopmentalStage stage)
        {
            LifeStageAge lifeStage = (LifeStageAge)RuntimeHelpers.GetUninitializedObject(typeof(LifeStageAge));
            lifeStage.def = new LifeStageDef { developmentalStage = stage };
            Verse.Pawn pawn = new Verse.Pawn
            {
                def = new ThingDef { race = new RaceProperties { lifeStageAges = new List<LifeStageAge> { lifeStage } } }
            };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            return pawn;
        }

        static bool HasParts(ref bool __result)
        {
            __result = true;
            return false;
        }

        static void ChildClothingPermission(ApparelProperties __instance, Verse.Pawn pawn, ref bool __result)
        {
            if (pawn == childClothingPawn)
            {
                __result = __instance.developmentalStageFilter.Has(DevelopmentalStage.Child);
            }
        }
    }
}
