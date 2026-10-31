using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_CampResidents
    {
        internal static bool Eligible(Verse.Pawn pawn, Map map)
        {
            if (map == null || pawn == null || pawn.Dead || pawn.Destroyed || pawn.MapHeld != map ||
                (!pawn.Spawned && pawn.CarriedBy == null) || pawn.Faction == Faction.OfPlayer ||
                pawn.IsPrisoner || pawn.IsSlave)
            {
                return false;
            }

            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            return comp != null && comp.State.IsActiveVisitor && comp.State.sourceIncidentDisplayId == "I-051";
        }

        internal static Lord CaptureHarm(Verse.Pawn pawn)
        {
            Lord lord = pawn?.GetLord();
            return lord?.LordJob is LordJob_RHAH_Camp && Eligible(pawn, lord.Map) ? lord : null;
        }

        internal static Lord Assign(Map map, List<Verse.Pawn> residents, IntVec3 center)
        {
            List<Verse.Pawn> members = Collect(map, residents);
            if (members.Count == 0)
            {
                return null;
            }

            return AssignMembers(map, members, center);
        }

        static Lord AssignMembers(Map map, List<Verse.Pawn> members, IntVec3 center)
        {
            for (int i = 0; i < members.Count; i++)
            {
                Detach(members[i]);
                Prepare(members[i]);
            }

            return LordMaker.MakeNewLord(members[0].Faction, new LordJob_RHAH_Camp(center), map, members);
        }

        internal static void Reconcile(Map map, List<Verse.Pawn> residents)
        {
            if (map == null || residents == null)
            {
                return;
            }

            List<Verse.Pawn> members = Collect(map, residents);
            if (members.Count == 0)
            {
                return;
            }

            Lord shared = members[0].GetLord();
            bool intact = shared?.LordJob is LordJob_RHAH_Camp;
            for (int i = 0; intact && i < members.Count; i++)
            {
                intact = members[i].GetLord() == shared;
            }
            if (intact)
            {
                return;
            }

            AssignMembers(map, members, map.Center);
        }

        internal static bool NotifyPlayerHarm(Lord camp)
        {
            if (!(camp?.LordJob is LordJob_RHAH_Camp))
            {
                return false;
            }

            Faction hostile = RHAH_AttitudeFactions.Require(RHAH_Attitude.Hostile);
            if (camp.faction == hostile)
            {
                return true;
            }

            Map map = camp.Map;
            List<Verse.Pawn> members = Collect(map, camp.ownedPawns);
            if (members.Count == 0)
            {
                return true;
            }

            RHAH_AttitudeFactions.LockGoodwill();
            map.attackTargetsCache.Notify_FactionHostilityChanged(hostile, Faction.OfPlayer);
            RHAH_AttitudeFactions.LockOutside(Faction.OfPlayer);
            for (int i = 0; i < members.Count; i++)
            {
                Verse.Pawn member = members[i];
                Detach(member);
                CompRHAH_Pawn.TryGet(member).State.SetAttitude(RHAH_Attitude.Hostile);
                if (member.Faction != hostile)
                {
                    member.SetFaction(hostile);
                }
                Prepare(member);
            }

            // SetFaction 清空职责后再一次性建立共同进攻职责
            LordMaker.MakeNewLord(hostile, new LordJob_RHAH_Camp(map.Center), map, members);
            return true;
        }

        static List<Verse.Pawn> Collect(Map map, List<Verse.Pawn> residents)
        {
            List<Verse.Pawn> members = new List<Verse.Pawn>();
            for (int i = 0; residents != null && i < residents.Count; i++)
            {
                Verse.Pawn pawn = residents[i];
                if (Eligible(pawn, map) && !members.Contains(pawn))
                {
                    members.Add(pawn);
                }
            }
            return members;
        }

        static void Prepare(Verse.Pawn pawn)
        {
            RHAH_PawnState state = CompRHAH_Pawn.TryGet(pawn).State;
            if (state.lifecycle == RHAH_Lifecycle.Leaving)
            {
                state.lifecycle = RHAH_Lifecycle.SeekingFood;
            }
            state.foodWaitUntilTick = -1;
            state.leaveAfterGameTick = -1;
            pawn.jobs?.StopAll(ifLayingKeepLaying: true);
        }

        static void Detach(Verse.Pawn pawn)
        {
            Lord old = pawn.GetLord();
            if (old == null)
            {
                return;
            }

            old.RemovePawn(pawn);
            if (old.ownedPawns.Count == 0 && old.lordManager.lords.Contains(old))
            {
                old.lordManager.RemoveLord(old);
            }
        }
    }
}
