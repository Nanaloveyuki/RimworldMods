using System.Collections.Generic;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_PasserbyRules
    {
        internal static bool IsPasserbyDisplayId(string displayId)
        {
            return displayId == "I-031" || displayId == "I-039";
        }

        internal static bool IsPasserby(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.Faction == Faction.OfPlayer || pawn.IsPrisoner || pawn.IsSlave)
            {
                return false;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            return snapshot != null && snapshot.IsActiveVisitor &&
                IsPasserbyDisplayId(snapshot.SourceIncidentDisplayId);
        }

        internal static bool IsPasserbyLord(Lord lord)
        {
            if (lord?.ownedPawns == null || lord.ownedPawns.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                if (!IsPasserby(lord.ownedPawns[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool TryFindOppositeExit(Map map, List<Verse.Pawn> pawns, out IntVec3 exit)
        {
            exit = IntVec3.Invalid;
            Verse.Pawn anchor = FindAnchor(pawns);
            if (map == null || anchor == null || !anchor.Spawned)
            {
                return false;
            }

            Rot4 entrySide = NearestEdge(map, anchor.Position);
            Rot4 exitSide = entrySide.Opposite;
            return CellFinder.TryFindRandomEdgeCellWith(
                cell => cell.Standable(map) && anchor.CanReach(cell, PathEndMode.OnCell, Danger.Deadly),
                map,
                exitSide,
                0f,
                out exit);
        }

        static Verse.Pawn FindAnchor(List<Verse.Pawn> pawns)
        {
            if (pawns == null)
            {
                return null;
            }

            Verse.Pawn fallback = null;
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned)
                {
                    continue;
                }

                fallback = fallback ?? pawn;
                if (!pawn.Downed && pawn.CarriedBy == null)
                {
                    return pawn;
                }
            }

            return fallback;
        }

        static Rot4 NearestEdge(Map map, IntVec3 cell)
        {
            int west = cell.x;
            int east = map.Size.x - 1 - cell.x;
            int south = cell.z;
            int north = map.Size.z - 1 - cell.z;
            int nearest = west;
            Rot4 side = Rot4.West;
            if (east < nearest)
            {
                nearest = east;
                side = Rot4.East;
            }
            if (south < nearest)
            {
                nearest = south;
                side = Rot4.South;
            }
            if (north < nearest)
            {
                side = Rot4.North;
            }

            return side;
        }
    }
}
