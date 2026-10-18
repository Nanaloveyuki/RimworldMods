using System.Collections.Generic;
using HungerAndHavoc.Api;

namespace HungerAndHavoc.Identity
{
    // 无 Verse 的可测状态 非法转换不改值
    internal sealed class RHAH_PawnState
    {
        internal string sourceIncidentDisplayId;
        internal int spawnBatchId;
        internal int relationshipGroupId;
        internal RHAH_PawnRole role;
        internal RHAH_Lifecycle lifecycle = RHAH_Lifecycle.Arriving;
        internal bool hasBeenFed;
        internal int leaveAfterGameTick = -1;
        internal int stayKind;
        internal int stayRemainingTicks;
        internal int foodWaitUntilTick = -1;
        internal int begSlapCount;
        internal int gnawSearchUntilTick = -1;

        internal bool PrefersGnaw => begSlapCount >= 2;

        internal void NoteBegSlap()
        {
            if (begSlapCount < 2)
            {
                begSlapCount++;
            }
        }
        internal bool carriesPlague;
        internal RHAH_Attitude attitudeAtArrival = RHAH_Attitude.Neutral;
        internal RHAH_Attitude attitude = RHAH_Attitude.Neutral;
        internal int parentPawnLoadId;
        internal List<int> childPawnLoadIds = new List<int>();
        internal List<int> droppedChildLoadIds = new List<int>();
        internal Dictionary<RHAH_BehaviorGate, bool> gateOverrides = new Dictionary<RHAH_BehaviorGate, bool>();
        internal Dictionary<string, string> extraData = new Dictionary<string, string>();

        internal bool IsReleased => lifecycle == RHAH_Lifecycle.Released;

        internal bool IsActiveVisitor =>
            lifecycle != RHAH_Lifecycle.Released && lifecycle != RHAH_Lifecycle.Dead;

        internal void ApplySeed(RHAH_PawnSeed seed)
        {
            if (seed == null)
            {
                return;
            }

            sourceIncidentDisplayId = seed.SourceIncidentDisplayId;
            spawnBatchId = seed.SpawnBatchId;
            relationshipGroupId = seed.RelationshipGroupId;
            role = seed.Role;
            lifecycle = seed.Lifecycle;
            carriesPlague = seed.CarriesPlague;
            attitudeAtArrival = seed.AttitudeAtArrival;
            attitude = seed.AttitudeAtArrival;
            leaveAfterGameTick = seed.LeaveAfterGameTick;
            parentPawnLoadId = seed.ParentPawnLoadId;
            childPawnLoadIds = seed.ChildPawnLoadIds == null
                ? new List<int>()
                : new List<int>(seed.ChildPawnLoadIds);
            gateOverrides = new Dictionary<RHAH_BehaviorGate, bool>();
            if (seed.GateOverrides != null)
            {
                foreach (KeyValuePair<RHAH_BehaviorGate, bool> pair in seed.GateOverrides)
                {
                    gateOverrides[pair.Key] = pair.Value;
                }
            }

            EnsureCollections();
        }

        internal bool TrySetLifecycle(RHAH_Lifecycle next)
        {
            if (next == lifecycle)
            {
                return false;
            }

            if (!IsLegalTransition(lifecycle, next))
            {
                return false;
            }

            lifecycle = next;
            if (next == RHAH_Lifecycle.Fed)
            {
                hasBeenFed = true;
            }

            return true;
        }

        internal void SetLeaveAfter(int tick)
        {
            leaveAfterGameTick = tick;
        }

        internal void SetStay(int kind, int deadline, int remaining)
        {
            stayKind = kind;
            leaveAfterGameTick = deadline;
            stayRemainingTicks = remaining < 0 ? 0 : remaining;
        }

        internal void ClearFedTimer()
        {
            if (stayKind == 0)
            {
                leaveAfterGameTick = -1;
            }
        }

        internal void SetAttitude(RHAH_Attitude next)
        {
            attitude = next;
        }

        // 覆盖优先 再行为 再默认 无 Pawn 时行为收到 null
        internal bool Allows(RHAH_BehaviorGate gate)
        {
            bool? gateOverride = GetGateOverride(gate);
            if (gateOverride.HasValue)
            {
                return gateOverride.Value;
            }

            IRHAH_Pawn snapshot = ToSnapshot();
            bool? behavior = RHAH_PawnBehaviors.Query(
                handler => handler.Allows(null, snapshot, gate));
            return behavior ?? RHAH_PawnDefaults.Allows(snapshot, gate);
        }

        internal bool ReleaseToColony(RHAH_ReleaseReason reason)
        {
            if (IsReleased)
            {
                return true;
            }

            IRHAH_Pawn current = ToSnapshot();
            bool? decision = RHAH_PawnBehaviors.Query(
                handler => handler.ShouldReleaseToColony(null, current, reason));
            if (decision == false)
            {
                return false;
            }

            if (!TrySetLifecycle(RHAH_Lifecycle.Released))
            {
                return false;
            }

            IRHAH_Pawn snapshot = ToSnapshot();
            RHAH_Api.RaiseLifecycleChanged(null, snapshot, RHAH_Lifecycle.Released);
            RHAH_Api.RaiseReleasedToColony(null, snapshot, reason);
            return true;
        }

        internal void SetGate(RHAH_BehaviorGate gate, bool? allowed)
        {
            EnsureCollections();
            gateOverrides.Remove(gate);
            if (allowed.HasValue)
            {
                gateOverrides[gate] = allowed.Value;
            }
        }

        internal bool? GetGateOverride(RHAH_BehaviorGate gate)
        {
            if (gateOverrides != null && gateOverrides.TryGetValue(gate, out bool allowed))
            {
                return allowed;
            }

            return null;
        }

        internal void SetExtra(string key, string value)
        {
            EnsureCollections();
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (value == null)
            {
                extraData.Remove(key);
                return;
            }

            extraData[key] = value;
        }

        internal bool TryGetExtra(string key, out string value)
        {
            if (string.IsNullOrEmpty(key) || extraData == null)
            {
                value = null;
                return false;
            }

            if (extraData.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        internal void EnsureCollections()
        {
            if (childPawnLoadIds == null)
            {
                childPawnLoadIds = new List<int>();
            }
            if (droppedChildLoadIds == null)
            {
                droppedChildLoadIds = new List<int>();
            }


            if (gateOverrides == null)
            {
                gateOverrides = new Dictionary<RHAH_BehaviorGate, bool>();
            }

            if (extraData == null)
            {
                extraData = new Dictionary<string, string>();
            }
        }

        internal RHAH_PawnSnapshot ToSnapshot()
        {
            return new RHAH_PawnSnapshot(
                sourceIncidentDisplayId,
                spawnBatchId,
                relationshipGroupId,
                role,
                lifecycle,
                hasBeenFed,
                leaveAfterGameTick,
                carriesPlague,
                attitudeAtArrival,
                attitude,
                parentPawnLoadId,
                childPawnLoadIds);
        }

        internal static bool IsLegalTransition(RHAH_Lifecycle from, RHAH_Lifecycle to)
        {
            switch (from)
            {
                case RHAH_Lifecycle.Arriving:
                    return to == RHAH_Lifecycle.SeekingFood ||
                           to == RHAH_Lifecycle.Leaving ||
                           to == RHAH_Lifecycle.Released ||
                           to == RHAH_Lifecycle.Dead;
                case RHAH_Lifecycle.SeekingFood:
                    return to == RHAH_Lifecycle.Fed ||
                           to == RHAH_Lifecycle.Leaving ||
                           to == RHAH_Lifecycle.Released ||
                           to == RHAH_Lifecycle.Dead;
                case RHAH_Lifecycle.Fed:
                    return to == RHAH_Lifecycle.Leaving ||
                           to == RHAH_Lifecycle.Released ||
                           to == RHAH_Lifecycle.Dead;
                case RHAH_Lifecycle.Leaving:
                    return to == RHAH_Lifecycle.Released ||
                           to == RHAH_Lifecycle.Dead;
                case RHAH_Lifecycle.Released:
                    return to == RHAH_Lifecycle.Dead;
                default:
                    return false;
            }
        }
    }
}
