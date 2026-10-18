using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal abstract class IncidentWorker_RHAH_Feral : IncidentWorker_RHAH_Group
    {
        protected abstract string ParentId { get; }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = RHAH_MapResolver.Resolve(parms?.target as Map);
            return map != null && base.CanFireNowSub(parms) &&
                (Role != RHAH_PawnRole.WildChild || Find.Storyteller.difficulty.ChildrenAllowed) &&
                !map.GameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout) &&
                (!ModsConfig.BiotechActive || !map.GameConditionManager.ConditionIsActive(GameConditionDefOf.NoxiousHaze)) &&
                map.mapTemperature.SeasonAcceptableFor(RHAH_DefOf.RHAH_PawnKind_Ratkin.race) &&
                Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out _, false, true);
        }

        protected override bool SubmitFacts(RHAH_IncidentContext context)
        {
            if (!Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out Faction formerFaction, false, true))
                return false;

            List<Verse.Pawn> arrived = new List<Verse.Pawn>(context.PawnCount);
            for (int i = 0; i < context.PawnCount; i++)
            {
                float age = RHAH_IncidentFacts.GenerationAge(Role, ParentId).Value;
                // 野人必须能自行游荡，不能生成原版无法照料的婴儿。
                age = Role == RHAH_PawnRole.WildChild ? System.Math.Max(4f, age) : System.Math.Max(12f, age);
                Verse.Pawn pawn = RHAH_PawnFactory.Generate(new RHAH_PawnRequest
                {
                    SourceIncidentDisplayId = ParentId,
                    Role = Role,
                    Map = context.Map,
                    PawnKind = RHAH_DefOf.RHAH_PawnKind_Ratkin,
                    Faction = formerFaction,
                    BiologicalAge = age,
                    Profile = new RHAH_PawnProfile { UseExplicitApparel = true }
                });
                if (pawn == null)
                {
                    for (int j = 0; j < arrived.Count; j++) arrived[j].Destroy(DestroyMode.Vanish);
                    return false;
                }

                MakeWild(pawn);
                arrived.Add(pawn);
            }

            for (int i = 0; i < arrived.Count; i++)
                GenSpawn.Spawn(arrived[i], context.SpawnCell, context.Map);

            bool young = Role == RHAH_PawnRole.WildChild && arrived[0].ageTracker.AgeBiologicalYearsFloat < 14f;
            string key = young ? "RHAH_Choice_WildYoung" : "RHAH_Choice_Wild";
            string textKey = young ? key + "_Text" : arrived.Count == 1 ? "RHAH_Choice_WildSingle_Text" : key + "_Text";
            Find.LetterStack.ReceiveLetter((key + "_Label").Translate(), textKey.Translate(),
                LetterDefOf.NeutralEvent, new LookTargets(arrived));
            return true;
        }

        internal static void MakeWild(Verse.Pawn pawn)
        {
            // 原版按 WildMan 的引用判定，ChangeKind 不替换鼠族种族。
            pawn.ChangeKind(PawnKindDefOf.WildMan);
            if (pawn.Faction != null) pawn.SetFaction(null);
        }
    }

    internal sealed class IncidentWorker_FeralRatkinWandersIn : IncidentWorker_RHAH_Feral
    {
        protected override string DisplayId => "I-008-01";
        protected override string ParentId => "I-008";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Wild;
    }

    internal sealed class IncidentWorker_FeralRatkinChildWandersIn : IncidentWorker_RHAH_Feral
    {
        protected override string DisplayId => "I-009-01";
        protected override string ParentId => "I-009";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.WildChild;
    }

    internal sealed class IncidentWorker_FeralRatkinGroupWandersIn : IncidentWorker_RHAH_Feral
    {
        protected override string DisplayId => "I-010-01";
        protected override string ParentId => "I-010";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Wild;
    }
}
