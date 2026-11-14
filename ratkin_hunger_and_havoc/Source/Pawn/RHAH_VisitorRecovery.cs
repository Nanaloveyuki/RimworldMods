using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_VisitorRecovery
    {
        internal static void ReconnectYoungVisitors(List<Verse.Pawn> pawns, Map map)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                IRHAH_Pawn state = RHAH_Api.Get(pawn);
                if (!CanResume(pawn, state, map) || WasDropped(pawn)) continue;

                Lord existing = FindCareGroup(pawn, state, pawns, map);
                if (existing != null)
                {
                    existing.AddPawn(pawn);
                    continue;
                }

                // Resume care without replaying arrival choices or extending stored deadlines.
                Lord resumed = RHAH_VisitorGroup.TryStart(new[] { pawn }, map, pawn.PositionHeld, state.Role);
                if (resumed == null) continue;
                for (int j = 0; j < resumed.Graph.lordToils.Count; j++)
                {
                    if (resumed.Graph.lordToils[j] is LordToil_RHAH_VisitorSeek seek)
                    {
                        resumed.GotoToil(seek);
                        break;
                    }
                }
            }
        }

        static bool CanResume(Verse.Pawn pawn, IRHAH_Pawn state, Map map)
        {
            return pawn != null && pawn.Spawned && pawn.Map == map && pawn.GetLord() == null &&
                RHAH_BatchAttitude.CanOrderLeave(pawn) && state != null &&
                state.Lifecycle != RHAH_Lifecycle.Leaving && state.SpawnBatchId > 0 &&
                !RHAH_Interception.IsInterception(state.SpawnBatchId) &&
                (state.Role == RHAH_PawnRole.RatkinYoung || state.Role == RHAH_PawnRole.BeggarChild) &&
                pawn.DevelopmentalStage != DevelopmentalStage.Adult;
        }

        static Lord FindCareGroup(Verse.Pawn pawn, IRHAH_Pawn state, List<Verse.Pawn> pawns, Map map)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn member = pawns[i];
                if (member == null || member.MapHeld != map || member.Faction != pawn.Faction ||
                    !RHAH_BatchAttitude.CanOrderLeave(member)) continue;
                IRHAH_Pawn other = RHAH_Api.Get(member);
                if (other.SpawnBatchId != state.SpawnBatchId ||
                    other.RelationshipGroupId != state.RelationshipGroupId ||
                    other.Lifecycle == RHAH_Lifecycle.Leaving) continue;
                Lord lord = member.GetLord();
                if (lord?.LordJob is LordJob_RHAH_Visitor job && job.ArmedChoiceId == 0 && !job.ArmedAssault &&
                    (lord.CurLordToil is LordToil_RHAH_VisitorTravel ||
                     lord.CurLordToil is LordToil_RHAH_VisitorSeek ||
                     lord.CurLordToil is LordToil_RHAH_WaitFood)) return lord;
            }
            return null;
        }

        internal static bool WasDropped(Verse.Pawn child)
        {
            RHAH_PawnState state = CompRHAH_Pawn.TryGet(child)?.State;
            if (state == null || (state.role != RHAH_PawnRole.RatkinYoung && state.role != RHAH_PawnRole.BeggarChild) ||
                (state.parentPawnLoadId <= 0 && child.relations == null)) return false;
            List<Verse.Pawn> known = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            for (int i = 0; i < known.Count; i++)
            {
                Verse.Pawn parent = known[i];
                if (parent == null || (parent.thingIDNumber != state.parentPawnLoadId &&
                    child.relations?.DirectRelationExists(PawnRelationDefOf.Parent, parent) != true)) continue;
                RHAH_PawnState family = CompRHAH_Pawn.TryGet(parent)?.State;
                if (family != null && family.spawnBatchId == state.spawnBatchId &&
                    family.relationshipGroupId == state.relationshipGroupId &&
                    family.droppedChildLoadIds.Contains(child.thingIDNumber)) return true;
            }
            return false;
        }
    }
}
