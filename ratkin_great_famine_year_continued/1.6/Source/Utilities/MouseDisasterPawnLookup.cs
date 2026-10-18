using System.Collections.Generic;
using Verse;

namespace MouseDisaster
{
    internal static class MouseDisasterPawnLookup
    {
        // The caller owns the buffer and decides whether it can be reused.
        internal static Dictionary<int, Pawn> Populate(IReadOnlyList<Pawn> pawns, Dictionary<int, Pawn> destination)
        {
            destination.Clear();
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn != null) destination[pawn.thingIDNumber] = pawn;
                }
            }
            return destination;
        }
    }
}
