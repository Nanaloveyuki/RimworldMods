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

            if (builtTick < 0 || tick < builtTick || tick - builtTick >= GenDate.TicksPerHour ||
                !ReferenceEquals(builtGame, Current.Game))
            {
                Rebuild(tick);
            }

            if (byId.TryGetValue(loadId, out Verse.Pawn pawn))
            {
                return pawn.Discarded ? null : pawn;
            }

            // 新人物不能等下次小时刷新 同 tick 的查找共用一次扫描
            if (builtTick != tick)
            {
                Rebuild(tick);
            }
            return byId.TryGetValue(loadId, out pawn) && !pawn.Discarded ? pawn : null;
        }

        internal static void Remember(Verse.Pawn pawn)
        {
            if (pawn == null || !ReferenceEquals(builtGame, Current.Game)) return;
            if (pawn.Discarded) byId.Remove(pawn.thingIDNumber);
            else byId[pawn.thingIDNumber] = pawn;
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
