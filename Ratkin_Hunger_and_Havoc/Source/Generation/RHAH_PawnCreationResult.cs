using System.Collections.Generic;
using Verse;
using VersePawn = Verse.Pawn;
namespace HungerAndHavoc.Generation
{
    internal enum RHAH_PawnCreationFailure
    {
        None = 0,
        InvalidRequest = 1,
        NoMap = 2,
        NoPawnKind = 3,
        PopulationLimit = 4,
        DuplicateBatch = 5,
        GenerationFailed = 6,
        MarkingFailed = 7,
        RelationshipFailed = 8
    }

    internal sealed class RHAH_PawnCreationResult
    {
        public List<VersePawn> Pawns { get; } = new List<VersePawn>();
        public RHAH_PawnCreationFailure Failure { get; private set; }
        public bool Succeeded => Failure == RHAH_PawnCreationFailure.None && Pawns.Count > 0;

        public static RHAH_PawnCreationResult Success(List<VersePawn> pawns)
        {
            RHAH_PawnCreationResult result = new RHAH_PawnCreationResult();
            result.Pawns.AddRange(pawns);
            return result;
        }

        public static RHAH_PawnCreationResult Failed(RHAH_PawnCreationFailure failure)
        {
            return new RHAH_PawnCreationResult { Failure = failure };
        }
    }
}
