using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Trade;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Incidents
{
    internal sealed class IncidentWorker_RHAH_BanditAttack : IncidentWorker_RHAH_Group
    {
        protected override string DisplayId => "I-054";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Thief;

        protected override bool SubmitFacts(RHAH_IncidentContext context)
        {
            LetterDef letterDef = DefDatabase<LetterDef>.GetNamedSilentFail("RHAH_BanditAttackLetter");
            if (letterDef == null) return false;
            Faction faction = TradeEventRouter.RequireAmbushFaction();
            if (faction == null) return false;

            List<Verse.Pawn> attackers = new List<Verse.Pawn>(context.PawnCount);
            for (int i = 0; i < context.PawnCount; i++)
            {
                RHAH_PawnCreationResult result = RHAH_PawnFactory.Create(new RHAH_PawnRequest
                {
                    SourceIncidentDisplayId = DisplayId,
                    SpawnBatchId = context.SpawnBatchId,
                    RelationshipGroupId = context.RelationshipGroupId,
                    Role = Role,
                    AttitudeAtArrival = RHAH_Attitude.Hostile,
                    PawnKind = RHAH_DefOf.RHAH_PawnKind_Ratkin,
                    Faction = faction,
                    BiologicalAge = RHAH_IncidentFacts.GenerationAge(Role, DisplayId)
                }, registerBatch: false);
                if (!result.Succeeded)
                {
                    Cleanup(attackers);
                    return false;
                }
                Verse.Pawn pawn = result.Pawns[0];
                attackers.Add(pawn);
                if (!RHAH_FamineWeapons.TryEquipRequired(pawn))
                {
                    Cleanup(attackers);
                    return false;
                }
            }

            // 关系锁定不更新原版目标缓存 必须在袭击者入图前通知
            context.Map.attackTargetsCache.Notify_FactionHostilityChanged(faction, Faction.OfPlayer);
            try
            {
                for (int i = 0; i < attackers.Count; i++)
                {
                    GenSpawn.Spawn(attackers[i], context.SpawnCell, context.Map);
                }
            }
            catch
            {
                Cleanup(attackers);
                throw;
            }
            if (!TradeEventRouter.TryCreateAssaultLord(faction, context.Map, attackers))
            {
                Cleanup(attackers);
                return false;
            }

            RHAH_Runtime.RegisterBatch(context.Map, context.SpawnBatchId);
            int tick = Find.TickManager.TicksGame;
            Current.Game?.GetComponent<Narrative.NarrativeState>()?.NoteIncident(
                new Storyteller.Suiyin.SuiyinIncidentFact(DisplayId, context.Map.uniqueID, tick,
                    context.SpawnBatchId, attackers.Count));
            List<int> loadIds = new List<int>(attackers.Count);
            for (int i = 0; i < attackers.Count; i++) loadIds.Add(attackers[i].thingIDNumber);
            context.Attitude = RHAH_Attitude.Hostile;
            Storyteller.Suiyin.RHAH_JournalRuntime.Open(context, loadIds);
            EventMgr.RHAH_EventChainEntry.NoteStarted(DisplayId, context.Map.uniqueID, 0, tick, context.SpawnBatchId);
            Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter(
                "RHAH_Incident_BanditAttack_Label".Translate(), "RHAH_BanditAttack_LetterText".Translate(),
                letterDef, new LookTargets(attackers), faction));
            return true;
        }

        static void Cleanup(List<Verse.Pawn> attackers)
        {
            for (int i = 0; i < attackers.Count; i++)
            {
                Verse.Pawn pawn = attackers[i];
                if (pawn == null || pawn.Destroyed) continue;
                Lord lord = pawn.GetLord();
                lord?.Notify_PawnLost(pawn, PawnLostCondition.Vanished, null);
                pawn.Destroy(DestroyMode.Vanish);
            }
        }
    }
}
