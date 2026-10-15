using System;
using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

using VersePawn = Verse.Pawn;
namespace HungerAndHavoc.Generation
{
    internal static class RHAH_Fertility
    {

        internal static bool Has(VersePawn pawn, string defName)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null || string.IsNullOrEmpty(defName))
            {
                return false;
            }

            GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
            return gene != null && pawn.genes.HasActiveGene(gene);
        }

        internal static bool Either(VersePawn first, VersePawn second, string defName)
        {
            return Has(first, defName) || Has(second, defName);
        }

        internal static VersePawn Female(VersePawn first, VersePawn second)
        {
            if (first != null && first.gender == Gender.Female)
            {
                return first;
            }

            if (second != null && second.gender == Gender.Female)
            {
                return second;
            }

            return null;
        }

        internal static VersePawn Male(VersePawn first, VersePawn second)
        {
            if (first != null && first.gender == Gender.Male)
            {
                return first;
            }

            if (second != null && second.gender == Gender.Male)
            {
                return second;
            }

            return null;
        }

        internal static bool AgesAllow(VersePawn first, VersePawn second, float minimumYears)
        {
            return first?.ageTracker != null &&
                second?.ageTracker != null &&
                RHAH_FertilityRules.AgeAllows(first.ageTracker.AgeBiologicalYearsFloat, minimumYears) &&
                RHAH_FertilityRules.AgeAllows(second.ageTracker.AgeBiologicalYearsFloat, minimumYears);
        }

        internal static bool CanProduce(VersePawn first, VersePawn second)
        {
            if (first == null || second == null || first.Dead || second.Dead || first.gender == second.gender)
            {
                return false;
            }

            if (!Find.Storyteller.difficulty.ChildrenAllowed)
            {
                return false;
            }

            VersePawn female = Female(first, second);
            VersePawn male = Male(first, second);
            if (female == null || male == null || female.Sterile() || male.Sterile())
            {
                return false;
            }

            if (PregnancyUtility.GetPregnancyHediff(female) != null)
            {
                return false;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            float minimum = settings == null ? RHAH_FertilityRules.DefaultFertileAge : settings.fertileMinAge;
            bool early = Either(first, second, RHAH_FertilityRules.EarlyFertility);
            bool room = Has(female, RHAH_FertilityRules.RoomFertility);
            if (!early && !room)
            {
                return PregnancyUtility.CanEverProduceChild(first, second).Accepted;
            }

            return AgesAllow(first, second, early ? minimum : RHAH_FertilityRules.MinFertileAge);
        }

        internal static float PregnancyFactor(VersePawn woman, VersePawn man)
        {
            if (!Either(woman, man, RHAH_FertilityRules.HighFertility))
            {
                return 1f;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            float percent = settings == null ? RHAH_FertilityRules.DefaultFertilityPercent : settings.fertilityPercent;
            return RHAH_FertilityRules.FertilityFactor(percent);
        }

        internal static float GestationDays(VersePawn mother)
        {
            if (!Has(mother, RHAH_FertilityRules.FastBirth) || mother?.RaceProps == null)
            {
                return mother?.RaceProps == null ? 0f : mother.RaceProps.gestationPeriodDays;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            float requested = settings == null ? RHAH_FertilityRules.DefaultGestationDays : settings.gestationDays;
            return RHAH_FertilityRules.GestationDays(requested, mother.RaceProps.gestationPeriodDays);
        }

        internal static int LitterCount(VersePawn mother)
        {
            if (!Has(mother, RHAH_FertilityRules.LargeLitter))
            {
                return 1;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            int minimum = settings == null ? RHAH_FertilityRules.DefaultLitterMin : settings.litterMin;
            int peak = settings == null ? RHAH_FertilityRules.DefaultLitterPeak : settings.litterPeak;
            int maximum = settings == null ? RHAH_FertilityRules.DefaultLitterMax : settings.litterMax;
            return RHAH_FertilityRules.RollLitter(minimum, peak, maximum, () => Rand.Value);
        }

        internal static PawnGenerationRequest LitterRequest(VersePawn mother, VersePawn father)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                mother.kindDef,
                mother.Faction,
                PawnGenerationContext.NonPlayer,
                developmentalStages: DevelopmentalStage.Newborn);
            request.ForceNoBackstory = true;
            request = AddEndogenes(request, father);
            request = AddEndogenes(request, mother);
            XenotypeDef xenotype = InheritedXenotype(mother, father);
            if (xenotype != null)
            {
                request.ForcedXenotype = xenotype;
            }

            return request;
        }

        internal static PawnGenerationRequest AddEndogenes(PawnGenerationRequest request, VersePawn parent)
        {
            if (parent?.genes == null)
            {
                return request;
            }

            List<Gene> genes = parent.genes.Endogenes;
            for (int i = 0; i < genes.Count; i++)
            {
                GeneDef gene = genes[i]?.def;
                if (gene != null && gene.endogeneCategory != EndogeneCategory.Melanin && gene.biostatArc <= 0)
                {
                    request.AddForcedGene(gene, false);
                }
            }

            return request;
        }

        // 关系模组改的是生成概率 Child 由 Parent 推导
        internal static void LinkParent(VersePawn child, VersePawn parent)
        {
            if (child?.relations == null || parent == null || child == parent ||
                PawnRelationDefOf.Parent == null || child.relations.DirectRelationExists(PawnRelationDefOf.Parent, parent))
            {
                return;
            }

            child.relations.AddDirectRelation(PawnRelationDefOf.Parent, parent);
        }

        internal static XenotypeDef InheritedXenotype(VersePawn mother, VersePawn father)
        {
            XenotypeDef motherType = HeritableXenotype(mother);
            XenotypeDef fatherType = HeritableXenotype(father);
            if (motherType != null && fatherType != null)
            {
                return motherType == fatherType ? motherType : null;
            }

            return motherType ?? fatherType;
        }

        static XenotypeDef HeritableXenotype(VersePawn pawn)
        {
            if (pawn?.genes == null)
            {
                return null;
            }

            XenotypeDef xenotype = pawn.genes.Xenotype;
            return xenotype != null && xenotype.inheritable ? xenotype : null;
        }

        internal static void KeepGermline(VersePawn baby, VersePawn mother, VersePawn father)
        {
            if (!ModsConfig.BiotechActive || baby?.genes == null)
            {
                return;
            }

            XenotypeDef xenotype = InheritedXenotype(mother, father);
            if (xenotype == null)
            {
                return;
            }

            List<GeneDef> genes = new List<GeneDef>();
            CopyGeneDefs(genes, baby.genes.Endogenes);
            CopyXenotypeGenes(genes, xenotype);
            baby.genes.SetXenotype(xenotype);
            for (int i = 0; i < genes.Count; i++)
            {
                if (!baby.genes.HasEndogene(genes[i]))
                {
                    baby.genes.AddGene(genes[i], false);
                }
            }
        }

        static void CopyGeneDefs(List<GeneDef> genes, List<Gene> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                GeneDef gene = source[i]?.def;
                if (gene != null && !genes.Contains(gene))
                {
                    genes.Add(gene);
                }
            }
        }

        static void CopyXenotypeGenes(List<GeneDef> genes, XenotypeDef xenotype)
        {
            if (xenotype?.genes == null)
            {
                return;
            }

            for (int i = 0; i < xenotype.genes.Count; i++)
            {
                GeneDef gene = xenotype.genes[i];
                if (gene != null && !genes.Contains(gene))
                {
                    genes.Add(gene);
                }
            }
        }

        internal static float PregnancyChance(VersePawn woman, VersePawn man)
        {
            float chance = PregnancyUtility.PregnancyChanceForPartners(woman, man);
            if (!Has(woman, RHAH_FertilityRules.RoomFertility))
            {
                return chance;
            }

            float lactation = LactationFactor(woman);
            if (lactation <= 0f || lactation >= 1f)
            {
                return chance;
            }

            return chance / lactation;
        }

        static float LactationFactor(VersePawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return 1f;
            }

            float factor = 1f;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff?.def != HediffDefOf.Lactating || hediff.CurStage == null)
                {
                    continue;
                }

                factor *= hediff.CurStage.fertilityFactor;
            }

            return factor;
        }

        internal static void TryRoomConception(VersePawn initiator)
        {
            if (!ModsConfig.BiotechActive || initiator?.CurJob == null || initiator.CurJob.def != JobDefOf.Lovin)
            {
                return;
            }

            VersePawn partner = initiator.CurJob.targetA.Thing as VersePawn;
            VersePawn mother = Female(initiator, partner);
            VersePawn father = Male(initiator, partner);
            if (mother == null || father == null || !Has(mother, RHAH_FertilityRules.RoomFertility))
            {
                return;
            }

            if (!CanProduce(mother, father))
            {
                return;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            float percent = settings == null ? RHAH_FertilityRules.DefaultLovinPregnancyPercent : settings.lovinPregnancyPercent;
            float chance = RHAH_FertilityRules.LovinPregnancyChance(percent) * PregnancyChance(mother, father);
            if (!Rand.Chance(chance))
            {
                return;
            }

            Hediff_Pregnant pregnancy = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, mother);
            pregnancy.SetParents(null, father, null);
            mother.health.AddHediff(pregnancy);
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.CanEverProduceChild))]
    internal static class RHAH_EarlyFertilityPatch
    {
        static void Postfix(VersePawn first, VersePawn second, ref AcceptanceReport __result)
        {
            if (__result.Accepted || !RHAH_Fertility.Either(first, second, RHAH_FertilityRules.EarlyFertility))
            {
                return;
            }

            if (RHAH_Fertility.CanProduce(first, second))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.PregnancyChanceForPartners))]
    internal static class RHAH_HighFertilityPatch
    {
        static void Postfix(VersePawn woman, VersePawn man, ref float __result)
        {
            __result *= RHAH_Fertility.PregnancyFactor(woman, man);
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), "TickInterval")]
    internal static class RHAH_FastBirthPatch
    {
        static void Postfix(Hediff_Pregnant __instance, int delta)
        {
            VersePawn mother = __instance?.pawn;
            if (mother == null || __instance.Severity >= 1f || !RHAH_Fertility.Has(mother, RHAH_FertilityRules.FastBirth))
            {
                return;
            }

            float days = RHAH_Fertility.GestationDays(mother);
            float raceDays = mother.RaceProps == null ? 0f : mother.RaceProps.gestationPeriodDays;
            if (days <= 0f || raceDays <= 0f || days >= raceDays)
            {
                return;
            }

            float vanilla = PawnUtility.BodyResourceGrowthSpeed(mother) / (raceDays * 60000f);
            __instance.Severity += vanilla * (raceDays / days - 1f) * delta;
            if (__instance.Severity < 1f)
            {
                return;
            }

            __instance.Severity = 1f;
            if (mother.RaceProps.Humanlike)
            {
                __instance.StartLabor();
            }
            else
            {
                Hediff_Pregnant.DoBirthSpawn(mother, __instance.Father);
            }

            mother.health.RemoveHediff(__instance);
        }
    }

    [HarmonyPatch(typeof(JobGiver_DoLovin), "TryGiveJob")]
    internal static class RHAH_RoomLovinPatch
    {
        static void Postfix(VersePawn pawn, ref Job __result)
        {
            if (__result != null || !RHAH_Fertility.Has(pawn, RHAH_FertilityRules.RoomFertility) || pawn?.Map == null)
            {
                return;
            }

            // 原版没躺上目标床会立刻失败且不写冷却 同一 tick 会反复重开
            if (!RHAH_FertilityRules.RoomLovinReady(
                Find.TickManager.TicksGame,
                pawn.mindState == null ? 0 : pawn.mindState.canLovinTick,
                pawn.Drafted,
                pawn.Awake(),
                pawn.CurJobDef == JobDefOf.Lovin))
            {
                return;
            }

            VersePawn partner = Partner(pawn);
            Building_Bed bed = pawn.CurrentBed();
            if (!RHAH_FertilityRules.RoomLovinBed(
                bed != null,
                bed != null && bed.Medical,
                partner != null && partner.CurrentBed() == bed,
                partner?.mindState == null ? 0 : partner.mindState.canLovinTick,
                Find.TickManager.TicksGame) ||
                !RHAH_Fertility.CanProduce(pawn, partner))
            {
                return;
            }

            int until = Find.TickManager.TicksGame + RHAH_FertilityRules.MinRoomLovinCooldownTicks;
            pawn.mindState.canLovinTick = until;
            partner.mindState.canLovinTick = until;
            __result = JobMaker.MakeJob(JobDefOf.Lovin, partner, bed);
        }

        static VersePawn Partner(VersePawn pawn)
        {
            if (pawn?.Map?.mapPawns == null)
            {
                return null;
            }

            Room room = pawn.GetRoom();
            if (room == null || room.PsychologicallyOutdoors)
            {
                return null;
            }

            VersePawn partner = null;
            System.Collections.Generic.IReadOnlyList<VersePawn> spawned = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                VersePawn other = spawned[i];
                Room otherRoom = other?.GetRoom();
                if (!RHAH_FertilityRules.RoomPartner(
                    other != null && other.Map == pawn.Map,
                    otherRoom == room,
                    otherRoom != null && otherRoom.PsychologicallyOutdoors,
                    other != null && other.gender != pawn.gender,
                    other != null && other.Dead,
                    other == pawn))
                {
                    continue;
                }

                if (partner == null || other.thingIDNumber < partner.thingIDNumber)
                {
                    partner = other;
                }
            }

            return partner;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Lovin), "MakeNewToils")]
    internal static class RHAH_RoomBirthPatch
    {
        static void Postfix(JobDriver_Lovin __instance)
        {
            if (__instance?.pawn == null)
            {
                return;
            }

            VersePawn initiator = __instance.pawn;
            Action<JobCondition> finish = condition => FinishRoomConception(initiator, condition);
            int slot = __instance.globalFinishActions.FindIndex(existing => existing.Method == finish.Method);
            if (slot >= 0)
            {
                __instance.globalFinishActions[slot] = finish;
            }
            else
            {
                __instance.AddFinishAction(finish);
            }
        }

        static void FinishRoomConception(VersePawn initiator, JobCondition condition)
        {
            if (condition == JobCondition.Succeeded)
            {
                RHAH_Fertility.TryRoomConception(initiator);
            }
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    internal static class RHAH_LargeLitterPatch
    {
        static void Postfix(VersePawn geneticMother, Thing birtherThing, VersePawn father, Thing __result)
        {
            VersePawn mother = geneticMother ?? birtherThing as VersePawn;
            VersePawn baby = __result as VersePawn;
            if (mother != null && baby != null)
            {
                HungerAndHavoc.EventMgr.RHAH_EventFollowMood.NoteBirth(mother, baby);
                RHAH_Fertility.KeepGermline(baby, mother, father);
            }

            if (mother == null || !RHAH_Fertility.Has(mother, RHAH_FertilityRules.LargeLitter))
            {
                return;
            }

            int extra = RHAH_Fertility.LitterCount(mother) - 1;
            for (int i = 0; i < extra; i++)
            {
                VersePawn extraBaby = PawnGenerator.GeneratePawn(RHAH_Fertility.LitterRequest(mother, father));
                if (extraBaby == null)
                {
                    continue;
                }


                RHAH_RatkinName.Apply(extraBaby);
                RHAH_Fertility.KeepGermline(extraBaby, mother, father);
                if (mother.MapHeld != null)
                {
                    GenSpawn.Spawn(extraBaby, mother.PositionHeld, mother.MapHeld);
                }

                RHAH_Fertility.LinkParent(extraBaby, mother);
                if (father != null)
                {
                    RHAH_Fertility.LinkParent(extraBaby, father);
                }
            }
        }
    }
}
