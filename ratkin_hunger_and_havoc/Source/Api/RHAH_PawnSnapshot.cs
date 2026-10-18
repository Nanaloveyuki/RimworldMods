using System;
using System.Collections.Generic;

namespace HungerAndHavoc.Api
{
    // 只读拷贝 后续 Comp 变化不回写
    public sealed class RHAH_PawnSnapshot : IRHAH_Pawn
    {
        public RHAH_PawnSnapshot(
            string sourceIncidentDisplayId,
            int spawnBatchId,
            int relationshipGroupId,
            RHAH_PawnRole role,
            RHAH_Lifecycle lifecycle,
            bool hasBeenFed,
            int leaveAfterGameTick,
            bool carriesPlague,
            RHAH_Attitude attitudeAtArrival,
            RHAH_Attitude attitude,
            int parentPawnLoadId,
            IEnumerable<int> childPawnLoadIds)
        {
            SourceIncidentDisplayId = sourceIncidentDisplayId;
            SpawnBatchId = spawnBatchId;
            RelationshipGroupId = relationshipGroupId;
            Role = role;
            Lifecycle = lifecycle;
            HasBeenFed = hasBeenFed;
            LeaveAfterGameTick = leaveAfterGameTick;
            CarriesPlague = carriesPlague;
            AttitudeAtArrival = attitudeAtArrival;
            Attitude = attitude;
            ParentPawnLoadId = parentPawnLoadId;
            ChildPawnLoadIds = CopyChildren(childPawnLoadIds);
        }

        public RHAH_PawnSnapshot(IRHAH_Pawn source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            SourceIncidentDisplayId = source.SourceIncidentDisplayId;
            SpawnBatchId = source.SpawnBatchId;
            RelationshipGroupId = source.RelationshipGroupId;
            Role = source.Role;
            Lifecycle = source.Lifecycle;
            HasBeenFed = source.HasBeenFed;
            LeaveAfterGameTick = source.LeaveAfterGameTick;
            CarriesPlague = source.CarriesPlague;
            AttitudeAtArrival = source.AttitudeAtArrival;
            Attitude = source.Attitude;
            ParentPawnLoadId = source.ParentPawnLoadId;
            ChildPawnLoadIds = CopyChildren(source.ChildPawnLoadIds);
        }

        public string SourceIncidentDisplayId { get; }

        public int SpawnBatchId { get; }

        public int RelationshipGroupId { get; }

        public RHAH_PawnRole Role { get; }

        public RHAH_Lifecycle Lifecycle { get; }

        public bool HasBeenFed { get; }

        public int LeaveAfterGameTick { get; }

        public bool CarriesPlague { get; }

        public RHAH_Attitude AttitudeAtArrival { get; }

        public RHAH_Attitude Attitude { get; }

        public int ParentPawnLoadId { get; }

        public IReadOnlyList<int> ChildPawnLoadIds { get; }

        public bool IsReleased => Lifecycle == RHAH_Lifecycle.Released;

        public bool IsActiveVisitor => Lifecycle != RHAH_Lifecycle.Released && Lifecycle != RHAH_Lifecycle.Dead;

        static IReadOnlyList<int> CopyChildren(IEnumerable<int> source)
        {
            if (source == null)
            {
                return new List<int>();
            }

            return new List<int>(source);
        }
    }
}
