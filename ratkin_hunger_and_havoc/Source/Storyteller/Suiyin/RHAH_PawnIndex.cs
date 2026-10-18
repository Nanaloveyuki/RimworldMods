using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_PawnIndex
    {
        static int builtTick = -1;
        static Game builtGame;
        static readonly Dictionary<int, Verse.Pawn> byId = new Dictionary<int, Verse.Pawn>();
        internal static Verse.Pawn Find(int loadId, int tick)
        {
            if (loadId <= 0)
            {
                return null;
            }

            if (builtTick != tick || !ReferenceEquals(builtGame, Current.Game))
            {
                Rebuild(tick);
            }

            Verse.Pawn pawn;
            return byId.TryGetValue(loadId, out pawn) ? pawn : null;
        }

        internal static void Clear()
        {
            builtTick = -1;
            builtGame = null;
            byId.Clear();
        }

        static void Rebuild(int tick)
        {
            byId.Clear();
            builtTick = tick;
            builtGame = Current.Game;
            if (Current.Game == null)
            {
                return;
            }

            List<Verse.Pawn> pawns = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            if (pawns == null)
            {
                return;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                if (pawn != null)
                {
                    byId[pawn.thingIDNumber] = pawn;
                }
            }
        }
    }
}
