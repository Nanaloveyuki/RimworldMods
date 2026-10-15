using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using HungerAndHavoc.Core;

namespace HungerAndHavoc.Pawn
{
    internal enum RHAH_BatchReaction
    {
        Harm,
        Expel,
        Reject
    }

    internal static class RHAH_BatchAttitude
    {
        internal static bool TryShift(Verse.Pawn affected, RHAH_BatchReaction reaction)
        {
            if (RHAH_MouseSwarm.TryProvoke(affected))
            {
                if (reaction == RHAH_BatchReaction.Harm)
                    HungerAndHavoc.EventMgr.RHAH_EventFollowMood.NoteHurt(affected);
                return true;
            }
            if (!CanOrderLeave(affected))
            {
                return false;
            }

            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(affected);
            if (reaction == RHAH_BatchReaction.Harm)
            {
                HungerAndHavoc.EventMgr.RHAH_EventFollowMood.NoteHurt(affected);
            }

            RHAH_AttitudeShift shift = RHAH_AttitudePolicy.React(comp.State.attitude, reaction == RHAH_BatchReaction.Harm);
            if (shift == RHAH_AttitudeShift.None)
            {
                return false;
            }

            List<Verse.Pawn> members = Collect(affected.MapHeld, comp.State.spawnBatchId);
            RHAH_Settings settings = RHAH_Mod.Settings;
            bool turnHostile = shift == RHAH_AttitudeShift.Hostile && (settings == null || settings.batchTurnsHostile);
            bool leave = settings == null || settings.batchLeavesTogether;
            if (members.Count == 0 || (!turnHostile && !leave))
            {
                return false;
            }

            if (turnHostile)
            {
                SetHostile(members);
            }
            if (reaction == RHAH_BatchReaction.Expel || reaction == RHAH_BatchReaction.Reject)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    RHAH_VisitorMood.NotifyExpelled(members[i], turnHostile);
                }
            }

            int newlyLeaving = 0;
            if (leave)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (RHAH_Api.Get(members[i]).Lifecycle != RHAH_Lifecycle.Leaving)
                    {
                        newlyLeaving++;
                    }
                }
                OrderLeave(members);
            }

            if (reaction == RHAH_BatchReaction.Expel && newlyLeaving > 0)
            {
                if (RHAH_EndingRuntime.CountsNow())
                {
                    int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
                    Current.Game?.GetComponent<NarrativeState>()?.NoteExpulsion(tick);
                }
                RHAH_SuiyinTrust.Note(newlyLeaving, RHAH_SuiyinTrust.Value(RHAH_SuiyinTrust.Expel, settings == null ? RHAH_SuiyinTrust.Expel : settings.trustExpel));
            }

            return true;
        }

        internal static bool Attack(List<Verse.Pawn> pawns)
        {
            if (RHAH_MouseSwarm.Provoke(pawns)) return true;
            List<Verse.Pawn> members = Eligible(pawns);
            if (members.Count == 0)
            {
                return false;
            }

            SetHostile(members);
            if (RHAH_Mod.Settings == null || RHAH_Mod.Settings.batchLeavesTogether)
            {
                // 显式攻击不驱逐允许反击的成员
                for (int i = members.Count - 1; i >= 0; i--)
                {
                    if (RHAH_Api.Allows(members[i], RHAH_BehaviorGate.Fight))
                    {
                        members.RemoveAt(i);
                    }
                }
                OrderLeave(members);
            }
            return true;
        }
        internal static void StartArmedAssault(List<Verse.Pawn> members)
        {
            SetHostile(Eligible(members));
        }

        static void SetHostile(List<Verse.Pawn> members)
        {
            Faction hostile = RHAH_AttitudeFactions.Require(RHAH_Attitude.Hostile);
            RHAH_AttitudeFactions.LockGoodwill();
            // 直接固定关系不会更新原版已收录的目标
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Find.Maps[i].attackTargetsCache.Notify_FactionHostilityChanged(hostile, Faction.OfPlayer);
            }
            RHAH_AttitudeFactions.LockOutside(Faction.OfPlayer);
            for (int i = 0; i < members.Count; i++)
            {
                Verse.Pawn member = members[i];
                CompRHAH_Pawn.TryGet(member).State.SetAttitude(RHAH_Attitude.Hostile);
                if (!RHAH_Api.Allows(member, RHAH_BehaviorGate.Fight) || member.Faction == hostile)
                {
                    continue;
                }

                SetVisitorFaction(member, hostile);
            }
        }

        internal static void AcceptFood(Verse.Pawn receiver)
        {
            Faction neutral = RHAH_AttitudeFactions.Require(RHAH_Attitude.Neutral);
            if (neutral == null || !CanOrderLeave(receiver))
            {
                return;
            }

            List<Verse.Pawn> members = Collect(receiver.MapHeld, CompRHAH_Pawn.TryGet(receiver).State.spawnBatchId);
            if (members.Count == 0)
            {
                members.Add(receiver);
            }
            for (int i = 0; i < members.Count; i++)
            {
                if (RHAH_MouseSwarm.IsProvoked(members[i])) continue;
                if (CompRHAH_Pawn.TryGet(members[i]).State.attitude != RHAH_Attitude.Hostile)
                {
                    continue;
                }
                CompRHAH_Pawn.TryGet(members[i]).State.SetAttitude(RHAH_Attitude.Neutral);
                SetVisitorFaction(members[i], neutral);
            }
        }

        internal static void SetVisitorFaction(Verse.Pawn member, Faction faction)
        {
            Lord lord = member.GetLord();
            LordJob_RHAH_Visitor visitor = lord?.LordJob as LordJob_RHAH_Visitor;
            PawnDuty duty = member.mindState?.duty;
            if (visitor != null)
            {
                visitor.PreserveForAttitudeChange = true;
            }
            try
            {
                if (member.Faction != faction)
                {
                    member.SetFaction(faction);
                }
                if (visitor != null)
                {
                    visitor.SetFaction(faction);
                }
            }
            finally
            {
                if (visitor != null)
                {
                    visitor.PreserveForAttitudeChange = false;
                    if (member.mindState != null)
                    {
                        member.mindState.duty = duty;
                    }
                }
            }
        }

        internal static bool CanOrderLeave(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.MapHeld == null ||
                pawn.Faction == Faction.OfPlayer || pawn.IsPrisoner || pawn.IsSlave ||
                !RHAH_Api.IsVisitor(pawn) || !RHAH_VisitorRules.AllowsModBehavior(RHAH_VisitorStay.Kind(pawn)))
            {
                return false;
            }

            Lord lord = pawn.GetLord();
            return lord == null || lord.LordJob is LordJob_RHAH_Visitor;
        }

        internal static void OrderVisitorLeave(List<Verse.Pawn> members)
        {
            OrderLeave(Eligible(members));
        }

        static List<Verse.Pawn> Eligible(List<Verse.Pawn> pawns)
        {
            List<Verse.Pawn> members = new List<Verse.Pawn>();
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    if (CanOrderLeave(pawns[i]) && !members.Contains(pawns[i]))
                    {
                        members.Add(pawns[i]);
                    }
                }
            }
            return members;
        }

        static void OrderLeave(List<Verse.Pawn> members)
        {
            if (members.Count > 1)
            {
                Lord shared = members[0].GetLord();
                bool sameLord = shared != null;
                bool sameMap = true;
                Map map = members[0].MapHeld;
                for (int i = 1; i < members.Count; i++)
                {
                    sameLord &= members[i].GetLord() == shared;
                    sameMap &= members[i].MapHeld == map;
                }
                if (!sameLord && sameMap)
                {
                    RHAH_VisitorGroup.TryStart(members, map, members[0].PositionHeld, RHAH_Api.Get(members[0]).Role);
                }
            }

            for (int i = 0; i < members.Count; i++)
            {
                RHAH_Api.SetLifecycle(members[i], RHAH_Lifecycle.Leaving);
                if (members[i].mindState != null)
                {
                    members[i].mindState.duty = new PawnDuty(RHAH_DefOf.RHAH_VisitorLeave);
                }
            }

            for (int i = 0; i < members.Count; i++)
            {
                Lord lord = members[i].GetLord();
                bool completeLord = lord?.LordJob is LordJob_RHAH_Visitor;
                if (completeLord)
                {
                    for (int j = 0; j < lord.ownedPawns.Count; j++)
                    {
                        if (!members.Contains(lord.ownedPawns[j]))
                        {
                            completeLord = false;
                            break;
                        }
                    }
                }
                if (completeLord && !(lord.CurLordToil is LordToil_RHAH_VisitorLeave))
                {
                    lord.ReceiveMemo("RHAH_Leave");
                }

                Verse.Pawn pawn = members[i];
                if (pawn.Map == null)
                {
                    continue;
                }
                Job leave = JobGiver_RHAH_Leave.TryCreate(pawn);
                if (leave != null)
                {
                    pawn.jobs?.StartJob(leave, JobCondition.InterruptForced);
                }
            }
        }

        internal static List<Verse.Pawn> Collect(Map map, int batchId)
        {
            List<Verse.Pawn> members = new List<Verse.Pawn>();
            IReadOnlyList<Verse.Pawn> spawned = map?.mapPawns?.AllPawnsSpawned;
            if (spawned == null || batchId <= 0)
            {
                return members;
            }

            for (int i = 0; i < spawned.Count; i++)
            {
                AddBatchMember(members, spawned[i], batchId);
                AddBatchMember(members, spawned[i].carryTracker?.CarriedThing as Verse.Pawn, batchId);
            }
            return members;
        }

        static void AddBatchMember(List<Verse.Pawn> members, Verse.Pawn pawn, int batchId)
        {
            if (CanOrderLeave(pawn) && CompRHAH_Pawn.TryGet(pawn).State.spawnBatchId == batchId && !members.Contains(pawn))
            {
                members.Add(pawn);
            }
        }
    }
}
