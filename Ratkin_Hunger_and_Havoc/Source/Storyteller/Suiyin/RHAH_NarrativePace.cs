using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum RHAH_EnvoyHold
    {
        None = 0,
        Stay = 1,
        Leave = 2
    }

    internal static class RHAH_NarrativePace
    {
        internal const int Spread = 250;

        internal const string EnvoyHoldKey = "rhah:envoyHold";

        internal static void HoldVisitor(Verse.Pawn pawn, string key)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null || comp.State.TryGetExtra(key, out string saved))
            {
                return;
            }

            string previous;
            string otherKey = key == EnvoyHoldKey ? HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.HoldKey : EnvoyHoldKey;
            if (!comp.State.TryGetExtra(otherKey, out previous))
            {
                previous = GateValue(comp.State.GetGateOverride(RHAH_BehaviorGate.LeaveAfterFed)) +
                    GateValue(comp.State.GetGateOverride(RHAH_BehaviorGate.ExitMap));
            }
            RHAH_Api.SetExtra(pawn, key, previous);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.LeaveAfterFed, false);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, false);
        }

        internal static void ReturnVisitorGates(Verse.Pawn pawn, string key)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null || !comp.State.TryGetExtra(key, out string previous) || previous == null || previous.Length != 2)
            {
                return;
            }

            RHAH_Api.SetExtra(pawn, key, null);
            string otherKey = key == EnvoyHoldKey ? HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.HoldKey : EnvoyHoldKey;
            if (!comp.State.TryGetExtra(otherKey, out string other))
            {
                RestoreGate(pawn, comp, RHAH_BehaviorGate.LeaveAfterFed, previous[0]);
                RestoreGate(pawn, comp, RHAH_BehaviorGate.ExitMap, previous[1]);
            }
        }

        static string GateValue(bool? value)
        {
            return value.HasValue ? (value.Value ? "t" : "f") : "u";
        }

        static void RestoreGate(Verse.Pawn pawn, CompRHAH_Pawn comp, RHAH_BehaviorGate gate, char previous)
        {
            if (comp.State.GetGateOverride(gate) == false)
            {
                RHAH_Api.SetGate(pawn, gate, previous == 'u' ? (bool?)null : previous == 't');
            }
        }
        internal static bool Due(int tick, int interval, int phase)
        {
            if (tick < 0 || interval <= 1)
            {
                return false;
            }

            int slot = phase < 0 ? 0 : phase % interval;
            return tick % interval == slot;
        }

        internal static RHAH_EnvoyHold HoldFor(SuiyinN008Outcome outcome)
        {
            if (outcome == SuiyinN008Outcome.Waiting || outcome == SuiyinN008Outcome.Checking)
            {
                return RHAH_EnvoyHold.Stay;
            }

            if (outcome == SuiyinN008Outcome.Traded ||
                outcome == SuiyinN008Outcome.Refused ||
                outcome == SuiyinN008Outcome.Driven ||
                outcome == SuiyinN008Outcome.NoProof ||
                outcome == SuiyinN008Outcome.TimedOut)
            {
                return RHAH_EnvoyHold.Leave;
            }

            return RHAH_EnvoyHold.None;
        }

        internal static List<int> MemberIds(IList<int> loadIds, int count)
        {
            List<int> ids = new List<int>();
            if (loadIds == null || count <= 0)
            {
                return ids;
            }

            int take = count < loadIds.Count ? count : loadIds.Count;
            for (int i = 0; i < take; i++)
            {
                if (loadIds[i] > 0)
                {
                    ids.Add(loadIds[i]);
                }
            }

            return ids;
        }
    }
}
