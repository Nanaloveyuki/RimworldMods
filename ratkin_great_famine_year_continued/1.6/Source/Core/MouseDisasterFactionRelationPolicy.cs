using RimWorld;

namespace MouseDisaster
{
    public enum MouseDisasterManagedFactionKind
    {
        None = 0,
        Hidden = 1,
        NeutralVisitors = 2,
        HostileVisitors = 3,
        FriendlyVisitors = 4
    }

    public static class MouseDisasterFactionRelationPolicy
    {
        public static bool ShouldInterceptRelationLookup(
            bool creatingWorld,
            bool allowNull,
            bool repairingRelation,
            bool ownerMissing,
            bool otherMissing,
            bool sameFaction,
            bool involvesManagedFaction)
        {
            if (creatingWorld || allowNull || repairingRelation || ownerMissing || otherMissing || sameFaction)
            {
                return false;
            }

            return involvesManagedFaction;
        }

        public static bool ShouldPersistRepairedRelation(bool ownerListed, bool otherListed)
        {
            return ownerListed && otherListed;
        }

        public static bool ShouldSkipIdentityNormalizationDuringFactionTeardown(bool managedFaction, bool factionListed)
        {
            return managedFaction && !factionListed;
        }

        public static void ResolveDefaultRelation(
            MouseDisasterManagedFactionKind first,
            bool firstIsPlayer,
            bool firstPermanentEnemy,
            MouseDisasterManagedFactionKind second,
            bool secondIsPlayer,
            bool secondPermanentEnemy,
            out FactionRelationKind kind,
            out int goodwill)
        {
            if (firstIsPlayer || secondIsPlayer)
            {
                MouseDisasterManagedFactionKind managed = firstIsPlayer ? second : first;
                bool managedPermanentEnemy = firstIsPlayer ? secondPermanentEnemy : firstPermanentEnemy;
                switch (managed)
                {
                    case MouseDisasterManagedFactionKind.HostileVisitors:
                        kind = FactionRelationKind.Hostile;
                        goodwill = -100;
                        return;
                    case MouseDisasterManagedFactionKind.FriendlyVisitors:
                        kind = FactionRelationKind.Ally;
                        goodwill = 100;
                        return;
                    case MouseDisasterManagedFactionKind.NeutralVisitors:
                        kind = FactionRelationKind.Neutral;
                        goodwill = 0;
                        return;
                    case MouseDisasterManagedFactionKind.Hidden:
                        kind = FactionRelationKind.Neutral;
                        goodwill = 20;
                        return;
                    default:
                        kind = managedPermanentEnemy ? FactionRelationKind.Hostile : FactionRelationKind.Neutral;
                        goodwill = managedPermanentEnemy ? -100 : 0;
                        return;
                }
            }

            if (first != MouseDisasterManagedFactionKind.None && second != MouseDisasterManagedFactionKind.None)
            {
                kind = FactionRelationKind.Neutral;
                goodwill = 0;
                return;
            }

            kind = FactionRelationKind.Hostile;
            goodwill = -100;
        }
    }
}
