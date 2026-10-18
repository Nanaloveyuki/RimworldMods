using System.Collections.Generic;
using HungerAndHavoc.Api;

namespace HungerAndHavoc.Pawn.Compat
{
    internal enum RHAH_LeashKind
    {
        None = 0,
        Mother = 1,
        Child = 2
    }

    internal readonly struct RHAH_LeashPair
    {
        internal RHAH_LeashPair(int adultIndex, int childIndex, int specialSource)
        {
            AdultIndex = adultIndex;
            ChildIndex = childIndex;
            SpecialSource = specialSource;
        }

        internal int AdultIndex { get; }

        internal int ChildIndex { get; }

        internal int SpecialSource { get; }
    }

    // 只决定谁可以牵 不调用 Lead Your Pet
    internal static class RHAH_LeashPlan
    {
        internal const int BeggarFamilySource = 1;
        internal const int ChildExchangeSource = 2;

        internal static bool IsLeashChild(RHAH_PawnRole role)
        {
            return role == RHAH_PawnRole.RatkinYoung ||
                role == RHAH_PawnRole.BeggarChild ||
                role == RHAH_PawnRole.ThiefChild ||
                role == RHAH_PawnRole.WildChild;
        }

        internal static RHAH_LeashKind Kind(string displayId, bool parentRelation, bool sameGroup, bool childAllowed, bool playerFaction)
        {
            if (!childAllowed || playerFaction || string.IsNullOrEmpty(displayId) || !sameGroup)
            {
                return RHAH_LeashKind.None;
            }

            if (displayId == "I-004" && parentRelation)
            {
                return RHAH_LeashKind.Mother;
            }

            if (displayId == "I-013" && parentRelation)
            {
                return RHAH_LeashKind.Mother;
            }

            if ((displayId == "I-004" || displayId == "I-013") && !parentRelation)
            {
                return RHAH_LeashKind.None;
            }

            return RHAH_LeashKind.Child;
        }

        internal static int SpecialSource(string displayId, RHAH_LeashKind kind)
        {
            if (kind != RHAH_LeashKind.Mother)
            {
                return 0;
            }

            if (displayId == "I-004")
            {
                return BeggarFamilySource;
            }

            if (displayId == "I-013")
            {
                return ChildExchangeSource;
            }

            return 0;
        }

        internal static bool TryPair(
            IReadOnlyList<IRHAH_Pawn> snapshots,
            IReadOnlyList<bool> gateAllows,
            IReadOnlyList<bool> parentRelations,
            IReadOnlyList<bool> playerFactions,
            out List<RHAH_LeashPair> pairs)
        {
            pairs = new List<RHAH_LeashPair>();
            if (snapshots == null || snapshots.Count == 0)
            {
                return false;
            }

            return PairExisting(snapshots, gateAllows, parentRelations, playerFactions, pairs);
        }

        static bool PairExisting(
            IReadOnlyList<IRHAH_Pawn> snapshots,
            IReadOnlyList<bool> gateAllows,
            IReadOnlyList<bool> parentRelations,
            IReadOnlyList<bool> playerFactions,
            List<RHAH_LeashPair> pairs)
        {
            bool paired = false;
            for (int child = 0; child < snapshots.Count; child++)
            {
                IRHAH_Pawn young = snapshots[child];
                if (young == null || !IsLeashChild(young.Role) || string.IsNullOrEmpty(young.SourceIncidentDisplayId))
                {
                    continue;
                }

                bool allowed = gateAllows == null || child >= gateAllows.Count || gateAllows[child];
                bool childPlayer = playerFactions != null && child < playerFactions.Count && playerFactions[child];
                if (!allowed || childPlayer)
                {
                    continue;
                }

                int parentIndex = -1;
                int kinIndex = -1;
                for (int adult = 0; adult < snapshots.Count; adult++)
                {
                    if (adult == child)
                    {
                        continue;
                    }

                    IRHAH_Pawn elder = snapshots[adult];
                    if (elder == null || IsLeashChild(elder.Role) || elder.SourceIncidentDisplayId != young.SourceIncidentDisplayId)
                    {
                        continue;
                    }

                    bool player = playerFactions != null && adult < playerFactions.Count && playerFactions[adult];
                    if (player)
                    {
                        continue;
                    }

                    int relation = child * snapshots.Count + adult;
                    bool isParent = parentRelations != null && relation < parentRelations.Count && parentRelations[relation];
                    bool sameGroup = elder.RelationshipGroupId > 0 && elder.RelationshipGroupId == young.RelationshipGroupId;
                    if (!sameGroup)
                    {
                        continue;
                    }

                    if (isParent && parentIndex < 0)
                    {
                        parentIndex = adult;
                    }
                    else if (!isParent && kinIndex < 0)
                    {
                        kinIndex = adult;
                    }
                }

                int adultIndex = parentIndex >= 0 ? parentIndex : kinIndex;
                if (adultIndex < 0)
                {
                    continue;
                }

                RHAH_LeashKind chosen = Kind(young.SourceIncidentDisplayId, adultIndex == parentIndex, true, true, false);
                if (chosen != RHAH_LeashKind.Mother && chosen != RHAH_LeashKind.Child)
                {
                    continue;
                }

                pairs.Add(new RHAH_LeashPair(adultIndex, child, SpecialSource(young.SourceIncidentDisplayId, chosen)));
                paired = true;
            }

            return paired;
        }
    }
}
