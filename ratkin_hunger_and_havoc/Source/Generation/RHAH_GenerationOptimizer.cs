using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_GenerationOptimizer
    {
        internal static bool Enabled =>
            Core.RHAH_Mod.Settings != null &&
            Core.RHAH_Mod.Settings.optimizeGeneration;

        internal static PawnGenerationRequest BuildRequest(RHAH_PawnRequest request, RHAH_PawnProfile profile, XenotypeDef xenotype)
        {
            RHAH_PawnProfile resolved = profile ?? new RHAH_PawnProfile();
            bool explicitBackstory = resolved.UseExplicitBackstory;
            bool explicitApparel = resolved.UseExplicitApparel;
            float? age = WalkingAge(request);
            Gender? gender = request.Gender ?? ResolveGender();
            PawnGenerationRequest generation = new PawnGenerationRequest(
                request.PawnKind,
                request.Faction,
                PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                allowDowned: StageFor(age) == DevelopmentalStage.Baby,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: request.SourceIncidentDisplayId == "I-052" || request.SourceIncidentDisplayId == "I-054",
                allowPregnant: false,
                allowFood: false,
                allowAddictions: false,
                forbidAnyTitle: true,
                forcedXenotype: xenotype,
                developmentalStages: StageFor(age),
                dontGiveWeapon: true,
                onlyUseForcedBackstories: explicitBackstory,
                forceNoGear: explicitApparel || ClearsGeneratedApparel())
            {
                FixedGender = gender,
                FixedBiologicalAge = age,
                ForceNoBackstory = true
            };
            if (SuppressVanillaTraits())
            {
                generation.MinimumAgeTraits = 0;
                generation.MaximumAgeTraits = 0;
            }
            return generation;
        }

        // 鼠族 HumanlikeBaby 到 4 岁 儿童到 12 岁 按人类 3 岁切会撞上永久倒地阶段
        internal static DevelopmentalStage StageFor(float? age)
        {
            if (!age.HasValue || float.IsNaN(age.Value) || float.IsInfinity(age.Value))
            {
                return DevelopmentalStage.Adult;
            }

            if (age.Value < 4f)
            {
                return DevelopmentalStage.Baby;
            }

            return age.Value < 12f ? DevelopmentalStage.Child : DevelopmentalStage.Adult;
        }

        static float? WalkingAge(RHAH_PawnRequest request)
        {
            if (!Identity.RHAH_PawnDefaults.IsYoungRole(request.Role))
            {
                return request.BiologicalAge;
            }

            return HungerAndHavoc.Pawn.RHAH_VisitorRules.WalkingAgeFloor(
                request.BiologicalAge,
                ModsConfig.IsActive("cyanobot.toddlers"),
                Core.RHAH_Mod.Settings != null && Core.RHAH_Mod.Settings.allowImmobileBabies);
        }

        static bool ClearsGeneratedApparel()
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            return settings != null && HungerAndHavoc.Pawn.RHAH_VisitorRules.ClearsApparel(settings.apparelMode, false);
        }

        static bool SuppressVanillaTraits()
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            return settings != null && !settings.allowVanillaTraits;
        }
        static Gender? ResolveGender()
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            int? resolved = HungerAndHavoc.Pawn.RHAH_VisitorRules.ResolveGender(
                null,
                settings == null ? 0 : settings.genderMode,
                settings == null ? 50 : settings.femaleSharePercent,
                Rand.Value);
            return resolved.HasValue ? (Gender)resolved.Value : (Gender?)null;
        }

    }
}
