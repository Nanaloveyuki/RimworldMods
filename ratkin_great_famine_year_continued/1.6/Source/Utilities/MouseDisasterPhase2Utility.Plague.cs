using System.Collections.Generic;
using Verse;

namespace MouseDisaster
{
    public static partial class MouseDisasterPhase2Utility
    {
        // Keep the existing public entry points while internal callers use the owner.
        public static bool IsPlagueCarrierMouseDisasterPawn(Pawn pawn) =>
            MouseDisasterPlagueUtility.IsPlagueCarrierMouseDisasterPawn(pawn);

        public static void InfectWithPlague(Pawn pawn, float? severity = null) =>
            MouseDisasterPlagueUtility.InfectWithPlague(pawn, severity);

        public static void InfectMany(IEnumerable<Pawn> pawns, bool leaderOnly = false) =>
            MouseDisasterPlagueUtility.InfectMany(pawns, leaderOnly);

        public static void DoPlagueSpreadCheck(Map map) =>
            MouseDisasterPlagueUtility.DoPlagueSpreadCheck(map);
    }
}
