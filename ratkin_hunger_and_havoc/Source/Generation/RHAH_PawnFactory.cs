using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using VersePawn = Verse.Pawn;
namespace HungerAndHavoc.Generation
{
    internal static class RHAH_PawnFactory
    {
        internal static RHAH_PawnCreationResult Create(RHAH_PawnRequest request)
        {
            return Create(request, true);
        }

        internal static RHAH_PawnCreationResult Create(RHAH_PawnRequest request, bool registerBatch)
        {
            RHAH_PawnCreationResult validation = Validate(request);
            if (validation != null)
            {
                return validation;
            }

            if (RHAH_Runtime.IsBatchActive(request.Map, request.SpawnBatchId))
            {
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.DuplicateBatch);
            }

            VersePawn pawn = Generate(request);
            if (pawn == null)
            {
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.GenerationFailed);
            }

            List<VersePawn> created = new List<VersePawn> { pawn };
            ApplyPlayerIdeo(pawn);
            RHAH_PawnSeed seed = new RHAH_PawnSeed(
                request.SourceIncidentDisplayId,
                request.SpawnBatchId,
                request.RelationshipGroupId,
                request.Role,
                RHAH_Lifecycle.Arriving,
                request.CarriesPlague,
                request.AttitudeAtArrival,
                -1,
                request.ParentPawnLoadId,
                request.ChildPawnLoadIds);

            if (RHAH_Api.TryMarkOrigin(pawn, seed) == null)
            {
                Cleanup(created);
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.MarkingFailed);
            }
            // 只在新角色登记来源后初始化 不挂入场或读档钩子
            RHAH_FamineArrival.Initialize(pawn);
            RHAH_FamineWeapons.Apply(pawn);

            if (!TryApplyRelationships(pawn, request))
            {
                Cleanup(created);
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.RelationshipFailed);
            }

            if (request.Map != null && request.SpawnCell.IsValid && !pawn.Spawned)
            {
                GenSpawn.Spawn(pawn, request.SpawnCell, request.Map);
            }

            if (request.Map != null && !pawn.Spawned)
            {
                Cleanup(created);
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.GenerationFailed);
            }

            bool plagueOn = Core.RHAH_Mod.Settings == null || Core.RHAH_Mod.Settings.plagueEnabled;
            if (plagueOn)
            {
                RHAH_Plague.InfectCarrier(pawn, request.CarriesPlague);
            }


            if (registerBatch)
            {
                RHAH_VisitorGroup.TryStart(created, request.Map, request.SpawnCell, request.Role);
                RHAH_Runtime.RegisterBatch(request.Map, request.SpawnBatchId);
            }
            return RHAH_PawnCreationResult.Success(created);
        }

        static void ApplyPlayerIdeo(VersePawn pawn)
        {
            int percent = Core.RHAH_Mod.Settings == null ? 100 : Core.RHAH_Mod.Settings.playerIdeoPercent;
            RHAH_VisitorRules.ApplyPlayerIdeo(pawn, percent, Rand.Value);
        }

        static RHAH_PawnCreationResult Validate(RHAH_PawnRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.SourceIncidentDisplayId) ||
                request.SpawnBatchId <= 0 || request.RelationshipGroupId <= 0 ||
                request.Role == RHAH_PawnRole.Unspecified)
            {
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.InvalidRequest);
            }

            if (request.PawnKind == null)
            {
                request.PawnKind = RHAH_DefOf.RHAH_PawnKind_Ratkin;
            }

            if (request.PawnKind == null)
            {
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.NoPawnKind);
            }

            if (request.Faction == null || request.Faction.IsPlayer)
            {
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.InvalidRequest);
            }

            if (request.Map != null &&
                (request.SpawnCell == IntVec3.Invalid || !request.SpawnCell.InBounds(request.Map)))
            {
                return RHAH_PawnCreationResult.Failed(RHAH_PawnCreationFailure.InvalidRequest);
            }

            return null;
        }

        internal static VersePawn Generate(RHAH_PawnRequest request)
        {
            RHAH_PawnProfile profile = request.Profile ?? new RHAH_PawnProfile();
            if (!RHAH_XenotypeResolver.TryResolve(profile, request.PawnKind, out XenotypeDef xenotype))
            {
                Log.Error("[RHAH] No permitted xenotype for " + request.PawnKind.defName +
                    "; generation stopped for " + request.SourceIncidentDisplayId);
                return null;
            }

            VersePawn pawn = PawnGenerator.GeneratePawn(RHAH_GenerationOptimizer.BuildRequest(request, profile, xenotype));
            if (pawn == null)
            {
                return null;
            }
            if (!RHAH_XenotypeResolver.Installed(pawn, request.PawnKind.race, xenotype))
            {
                Log.Error("[RHAH] Generated pawn has an incorrect race or incomplete xenotype: expected " +
                    request.PawnKind.race.defName + "/" + (xenotype?.defName ?? "natural") + ", actual " +
                    pawn.def?.defName + "/" + pawn.genes?.Xenotype?.defName +
                    "; discarding pawn for " + request.SourceIncidentDisplayId);
                pawn.Destroy(DestroyMode.Vanish);
                return null;
            }


            ApplyProfile(pawn, profile);

            RHAH_RatkinName.Apply(pawn);
            if (request.Shatter)
            {
                RHAH_PawnBody.Shatter(pawn);
            }

            if (request.StartLabor)
            {
                RHAH_PawnBody.StartLabor(pawn);
            }

            RHAH_RatkinAppearance.Apply(pawn, profile.UseExplicitApparel);
            RHAH_ContentApplier.Apply(pawn, request, !profile.UseExplicitBackstory);
            if (!profile.UseExplicitApparel && !RHAH_VisitorRules.ClearsApparel(ApparelMode(), false))
            {
                float outdoor = request.Map?.mapTemperature == null ? 21f : request.Map.mapTemperature.OutdoorTemp;
                pawn.apparel?.DestroyAll();
                RHAH_TemperatureApparel.Apply(pawn, outdoor);
                RHAH_ApparelAssigner.Apply(pawn);
            }

            RHAH_XenotypeResolver.ApplyEnabledGenes(pawn);
            return pawn;
        }


        static int ApparelMode()
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            return settings == null ? 0 : settings.apparelMode;
        }

        static void ApplyProfile(VersePawn pawn, RHAH_PawnProfile profile)
        {
            if (profile.UseExplicitBackstory)
            {
                ApplyBackstory(pawn, profile);
            }

            if (profile.UseExplicitApparel)
            {
                ApplyApparel(pawn, profile);
            }

            if (profile.UseExplicitHealth)
            {
                ApplyHealth(pawn, profile);
            }
        }

        static void ApplyBackstory(VersePawn pawn, RHAH_PawnProfile profile)
        {
            if (pawn.story == null)
            {
                return;
            }

            pawn.story.Childhood = profile.Childhood;
            pawn.story.Adulthood = pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYearsFloat >= 20f
                ? profile.Adulthood
                : null;
        }

        static void ApplyApparel(VersePawn pawn, RHAH_PawnProfile profile)
        {
            if (pawn.apparel == null)
            {
                return;
            }

            pawn.apparel.DestroyAll();
            for (int i = 0; i < profile.Apparel.Count; i++)
            {
                ThingDef def = profile.Apparel[i];
                if (def == null || !def.IsApparel || !ApparelUtility.HasPartsToWear(pawn, def))
                {
                    continue;
                }

                Thing thing = ThingMaker.MakeThing(def);
                Apparel apparel = thing as Apparel;
                if (apparel != null)
                {
                    pawn.apparel.Wear(apparel, false, false);
                }
            }
        }

        static void ApplyHealth(VersePawn pawn, RHAH_PawnProfile profile)
        {
            if (pawn.health == null)
            {
                return;
            }

            for (int i = 0; i < profile.Hediffs.Count; i++)
            {
                HediffDef def = profile.Hediffs[i];
                if (def != null && !pawn.health.hediffSet.HasHediff(def))
                {
                    pawn.health.AddHediff(def);
                }
            }
        }

        static bool TryApplyRelationships(Verse.Pawn pawn, RHAH_PawnRequest request)
        {
            if (request.ParentPawnLoadId == 0 &&
                (request.ChildPawnLoadIds == null || request.ChildPawnLoadIds.Length == 0))
            {
                return true;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            return snapshot != null && snapshot.RelationshipGroupId == request.RelationshipGroupId;
        }

        static void Cleanup(List<Verse.Pawn> pawns)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                VersePawn pawn = pawns[i];
                if (pawn == null)
                {
                    continue;
                }

                RHAH_VisitorGroup.NotifyReleased(pawn);
                if (!pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }
            }
        }
    }
}
