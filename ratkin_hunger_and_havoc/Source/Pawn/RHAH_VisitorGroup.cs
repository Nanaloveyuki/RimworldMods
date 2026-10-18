using System.Collections.Generic;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_VisitorGroup
    {
        internal static Lord TryStart(
            IEnumerable<Verse.Pawn> pawns,
            Map map,
            IntVec3 waitSpot,
            RHAH_PawnRole familyRole)
        {
            if (map == null || !waitSpot.IsValid)
            {
                return null;
            }

            List<Verse.Pawn> pawnList = CollectPawns(pawns, map);
            if (pawnList.Count == 0)
            {
                return null;
            }

            DetachFromOldLords(pawnList);
            Faction faction = ResolveFaction(pawnList, familyRole);
            return LordMaker.MakeNewLord(
                faction,
                new LordJob_RHAH_Visitor(faction, waitSpot, familyRole),
                map,
                pawnList);
        }

        internal static void NotifyReleased(Verse.Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            Lord lord = pawn.GetLord();
            if (lord != null)
            {
                lord.RemovePawn(pawn);
                if (lord.ownedPawns.Count == 0 &&
                    lord.lordManager != null &&
                    lord.lordManager.lords.Contains(lord))
                {
                    lord.lordManager.RemoveLord(lord);
                }
            }

            if (pawn.mindState != null)
            {
                pawn.mindState.duty = null;
            }

            pawn.jobs?.StopAll();
        }

        internal static void NotifyDead(Verse.Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            NotifyReleased(pawn);
        }

        internal static void NotifyMapTeardown(Map map)
        {
            if (map == null || map.lordManager == null)
            {
                return;
            }

            List<Lord> lords = map.lordManager.lords;
            for (int i = lords.Count - 1; i >= 0; i--)
            {
                Lord lord = lords[i];
                if (lord?.LordJob is LordJob_RHAH_Visitor)
                {
                    map.lordManager.RemoveLord(lord);
                }
            }
        }

        static List<Verse.Pawn> CollectPawns(IEnumerable<Verse.Pawn> pawns, Map map)
        {
            List<Verse.Pawn> pawnList = new List<Verse.Pawn>();
            if (pawns == null)
            {
                return pawnList;
            }

            foreach (Verse.Pawn pawn in pawns)
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    continue;
                }

                if (pawn.MapHeld != map || (!pawn.Spawned && pawn.CarriedBy == null))
                {
                    continue;
                }

                if (!RHAH_VisitorGate.IsVisitor(pawn))
                {
                    continue;
                }

                bool already = false;
                for (int i = 0; i < pawnList.Count; i++)
                {
                    if (pawnList[i] == pawn)
                    {
                        already = true;
                        break;
                    }
                }

                if (!already)
                {
                    pawnList.Add(pawn);
                }
            }

            return pawnList;
        }

        static void DetachFromOldLords(List<Verse.Pawn> pawnList)
        {
            List<Lord> oldLords = new List<Lord>();
            for (int i = 0; i < pawnList.Count; i++)
            {
                Lord oldLord = pawnList[i].GetLord();
                if (oldLord == null)
                {
                    continue;
                }

                bool seen = false;
                for (int j = 0; j < oldLords.Count; j++)
                {
                    if (oldLords[j] == oldLord)
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                {
                    oldLords.Add(oldLord);
                }

                oldLord.RemovePawn(pawnList[i]);
            }

            for (int i = 0; i < oldLords.Count; i++)
            {
                Lord oldLord = oldLords[i];
                if (oldLord.ownedPawns.Count == 0 &&
                    oldLord.lordManager != null &&
                    oldLord.lordManager.lords.Contains(oldLord))
                {
                    oldLord.lordManager.RemoveLord(oldLord);
                }
            }
        }

        static Faction ResolveFaction(List<Verse.Pawn> pawnList, RHAH_PawnRole familyRole)
        {
            for (int i = 0; i < pawnList.Count; i++)
            {
                IRHAH_Pawn snapshot = RHAH_Api.Get(pawnList[i]);
                if (snapshot != null && snapshot.Role == familyRole)
                {
                    return pawnList[i].Faction;
                }
            }

            return pawnList[0].Faction;
        }
    }
}
