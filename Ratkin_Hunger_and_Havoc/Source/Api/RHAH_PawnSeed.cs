using System.Collections.Generic;

namespace HungerAndHavoc.Api
{
    public sealed class RHAH_PawnSeed
    {
        public RHAH_PawnSeed(
            string sourceIncidentDisplayId = null,
            int spawnBatchId = 0,
            int relationshipGroupId = 0,
            RHAH_PawnRole role = RHAH_PawnRole.Unspecified,
            RHAH_Lifecycle lifecycle = RHAH_Lifecycle.Arriving,
            bool carriesPlague = false,
            RHAH_Attitude attitudeAtArrival = RHAH_Attitude.Neutral,
            int leaveAfterGameTick = -1,
            int parentPawnLoadId = 0,
            IEnumerable<int> childPawnLoadIds = null,
            IEnumerable<KeyValuePair<RHAH_BehaviorGate, bool>> gateOverrides = null)
        {
            SourceIncidentDisplayId = sourceIncidentDisplayId;
            SpawnBatchId = spawnBatchId;
            RelationshipGroupId = relationshipGroupId;
            Role = role;
            Lifecycle = lifecycle;
            CarriesPlague = carriesPlague;
            AttitudeAtArrival = attitudeAtArrival;
            LeaveAfterGameTick = leaveAfterGameTick;
            ParentPawnLoadId = parentPawnLoadId;
            ChildPawnLoadIds = CopyChildren(childPawnLoadIds);
            GateOverrides = CopyGates(gateOverrides);
        }

        public string SourceIncidentDisplayId { get; }

        public int SpawnBatchId { get; }

        public int RelationshipGroupId { get; }

        public RHAH_PawnRole Role { get; }

        public RHAH_Lifecycle Lifecycle { get; }

        public bool CarriesPlague { get; }

        public RHAH_Attitude AttitudeAtArrival { get; }

        public int LeaveAfterGameTick { get; }

        public int ParentPawnLoadId { get; }

        public IReadOnlyList<int> ChildPawnLoadIds { get; }

        public IReadOnlyDictionary<RHAH_BehaviorGate, bool> GateOverrides { get; }

        public RHAH_PawnSeed Clone()
        {
            return new RHAH_PawnSeed(
                SourceIncidentDisplayId,
                SpawnBatchId,
                RelationshipGroupId,
                Role,
                Lifecycle,
                CarriesPlague,
                AttitudeAtArrival,
                LeaveAfterGameTick,
                ParentPawnLoadId,
                ChildPawnLoadIds,
                GateOverrides);
        }

        static IReadOnlyList<int> CopyChildren(IEnumerable<int> source)
        {
            if (source == null)
            {
                return new List<int>();
            }

            return new List<int>(source);
        }

        static IReadOnlyDictionary<RHAH_BehaviorGate, bool> CopyGates(
            IEnumerable<KeyValuePair<RHAH_BehaviorGate, bool>> source)
        {
            Dictionary<RHAH_BehaviorGate, bool> copy = new Dictionary<RHAH_BehaviorGate, bool>();
            if (source == null)
            {
                return copy;
            }

            foreach (KeyValuePair<RHAH_BehaviorGate, bool> pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}
