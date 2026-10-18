using System.Collections.Generic;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_AttitudeFactions
    {
        static readonly RHAH_Attitude[] Attitudes =
        {
            RHAH_Attitude.Hostile,
            RHAH_Attitude.LeaningHostile,
            RHAH_Attitude.Neutral,
            RHAH_Attitude.LeaningFriendly,
            RHAH_Attitude.Friendly
        };

        static readonly Faction[] Resolved = new Faction[5];
        static FactionManager resolvedManager;

        internal static Faction Resolve(RHAH_Attitude attitude)
        {
            FactionDef def = DefFor(attitude);
            FactionManager manager = Find.FactionManager;
            if (def == null || manager == null)
            {
                return null;
            }

            if (resolvedManager != manager)
            {
                resolvedManager = manager;
                for (int i = 0; i < Resolved.Length; i++)
                {
                    Resolved[i] = null;
                }
            }

            int index = (int)attitude;
            if (index < 0 || index >= Resolved.Length)
            {
                return manager.FirstFactionOfDef(def);
            }

            if (Resolved[index] == null)
            {
                Resolved[index] = manager.FirstFactionOfDef(def);
            }

            return Resolved[index];
        }

        internal static Faction Require(RHAH_Attitude attitude)
        {
            Faction faction = Resolve(attitude);
            if (faction == null || faction.IsPlayer)
            {
                Ensure(attitude);
                faction = Resolve(attitude);
                if (faction != null && !faction.IsPlayer)
                {
                    LockOutside(Faction.OfPlayer);
                }
            }

            return faction != null && !faction.IsPlayer ? faction : null;
        }


        internal static void Ensure(RHAH_Attitude attitude)
        {
            if (Resolve(attitude) != null || Find.FactionManager == null)
            {
                return;
            }

            FactionDef def = DefFor(attitude);
            if (def == null)
            {
                return;
            }

            FactionGenerator.CreateFactionAndAddToManager(def);
            int index = (int)attitude;
            if (index >= 0 && index < Resolved.Length)
            {
                Resolved[index] = null;
            }
        }

        internal static void EnsureAll()
        {
            if (Find.FactionManager == null)
            {
                return;
            }

            Ensure(RHAH_Attitude.Hostile);
            Ensure(RHAH_Attitude.LeaningHostile);
            Ensure(RHAH_Attitude.Neutral);
            Ensure(RHAH_Attitude.LeaningFriendly);
            Ensure(RHAH_Attitude.Friendly);
        }

        internal static FactionDef DefFor(RHAH_Attitude attitude)
        {
            switch (attitude)
            {
                case RHAH_Attitude.Hostile:
                    return HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_Hostile;
                case RHAH_Attitude.LeaningHostile:
                    return HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_LeaningHostile;
                case RHAH_Attitude.LeaningFriendly:
                    return HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_LeaningFriendly;
                case RHAH_Attitude.Friendly:
                    return HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_Friendly;
                default:
                    return HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_Neutral;
            }
        }

        internal static bool IsAttitudeFaction(Faction faction)
        {
            if (faction?.def == null)
            {
                return false;
            }

            FactionDef def = faction.def;
            return def == HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_Hostile ||
                   def == HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_LeaningHostile ||
                   def == HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_Neutral ||
                   def == HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_LeaningFriendly ||
                   def == HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_Friendly;
        }

        internal static void LockGoodwill()
        {
            if (Find.FactionManager == null)
            {
                return;
            }

            for (int i = 0; i < Attitudes.Length; i++)
            {
                Faction faction = Resolve(Attitudes[i]);
                if (faction == null || faction.IsPlayer)
                {
                    Ensure(Attitudes[i]);
                    faction = Resolve(Attitudes[i]);
                }

                Resolved[i] = faction;
            }

            Faction player = Faction.OfPlayer;
            if (player == null)
            {
                return;
            }

            for (int i = 0; i < Attitudes.Length; i++)
            {
                Pin(player, Resolved[i], RHAH_VisitorRules.LockedGoodwill(Attitudes[i]));
            }
        }

        internal static void LockOutside(Faction player)
        {
            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; i++)
            {
                Faction owner = factions[i];
                if (!IsAttitudeFaction(owner))
                {
                    continue;
                }

                bool ownerHostile = owner.def == HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_Hostile;
                bool ownerFriendly = owner.def == HungerAndHavoc.Core.RHAH_DefOf.RHAH_Faction_Friendly;
                for (int j = 0; j < factions.Count; j++)
                {
                    Faction other = factions[j];
                    if (other == null || other == owner || other == player || other.IsPlayer)
                    {
                        continue;
                    }

                    bool playerHostile = player != null && Hostile(player, other);
                    RHAH_VisitorRules.OutsideRelation(
                        ownerHostile,
                        ownerFriendly,
                        IsAttitudeFaction(other),
                        playerHostile,
                        out FactionRelationKind kind,
                        out int goodwill);
                    PinPair(owner, other, kind, goodwill);
                }
            }
        }

        static void PinPair(Faction owner, Faction other, FactionRelationKind kind, int goodwill)
        {
            FactionRelation forward = FindRelation(owner, other);
            FactionRelation backward = FindRelation(other, owner);
            if (forward != null && backward != null &&
                forward.kind == kind && backward.kind == kind &&
                forward.baseGoodwill == goodwill && backward.baseGoodwill == goodwill)
            {
                return;
            }

            if (forward == null || backward == null)
            {
                owner.SetRelation(new FactionRelation
                {
                    other = other,
                    kind = kind,
                    baseGoodwill = goodwill
                });
                FactionRelation reverse = FindRelation(other, owner);
                if (reverse != null)
                {
                    reverse.baseGoodwill = goodwill;
                }

                return;
            }

            forward.kind = kind;
            forward.baseGoodwill = goodwill;
            backward.kind = kind;
            backward.baseGoodwill = goodwill;
        }

        static bool Hostile(Faction owner, Faction other)
        {
            FactionRelation relation = FindRelation(owner, other);
            return relation != null && relation.kind == FactionRelationKind.Hostile;
        }

        static FactionRelation FindRelation(Faction owner, Faction other)
        {
            if (owner == null || other == null || owner == other)
            {
                return null;
            }

            return owner.RelationWith(other, true);
        }

        static void Pin(Faction player, Faction faction, int goodwill)
        {
            if (faction == null || faction == player)
            {
                return;
            }

            // 隐藏派系不走原版好感变动 直接固定双向关系
            PinPair(player, faction,
                goodwill < 0 ? FactionRelationKind.Hostile : FactionRelationKind.Neutral,
                goodwill);
        }
    }
}
