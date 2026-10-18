using System.Collections.Generic;

namespace HungerAndHavoc.Api
{
    public interface IRHAH_Pawn
    {
        string SourceIncidentDisplayId { get; }

        int SpawnBatchId { get; }

        int RelationshipGroupId { get; }

        RHAH_PawnRole Role { get; }

        RHAH_Lifecycle Lifecycle { get; }

        bool HasBeenFed { get; }

        int LeaveAfterGameTick { get; }

        bool CarriesPlague { get; }

        RHAH_Attitude AttitudeAtArrival { get; }

        RHAH_Attitude Attitude { get; }

        int ParentPawnLoadId { get; }

        IReadOnlyList<int> ChildPawnLoadIds { get; }

        bool IsReleased { get; }

        bool IsActiveVisitor { get; }
    }
}
