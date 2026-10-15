namespace MouseDisaster
{
    public enum MouseDisasterEventAttitude
    {
        Neutral = 0,
        Hostile = 1,
        HostileLeaning = 2,
        FriendlyLeaning = 3,
        Friendly = 4
    }

    public enum MouseDisasterEventReaction { None, GroupFlee, GroupHostile }

    [System.Flags]
    public enum MouseDisasterPawnBehavior
    {
        None = 0,
        SeekFood = 1,
        Beg = 2,
        Steal = 4,
        IgnoreCombatFear = 8,
        ReliefOnly = 16
    }

    public static class MouseDisasterEventPolicy
    {
        public static MouseDisasterEventAttitude Normalize(MouseDisasterEventAttitude attitude)
        {
            return attitude >= MouseDisasterEventAttitude.Neutral && attitude <= MouseDisasterEventAttitude.Friendly
                ? attitude : MouseDisasterEventAttitude.Neutral;
        }

        public static MouseDisasterEventReaction React(MouseDisasterEventAttitude attitude, bool forcedAway)
        {
            switch (attitude)
            {
                case MouseDisasterEventAttitude.Hostile:
                case MouseDisasterEventAttitude.HostileLeaning:
                    return MouseDisasterEventReaction.GroupHostile;
                case MouseDisasterEventAttitude.FriendlyLeaning:
                case MouseDisasterEventAttitude.Friendly:
                    return MouseDisasterEventReaction.GroupFlee;
                default:
                    return forcedAway ? MouseDisasterEventReaction.GroupHostile : MouseDisasterEventReaction.None;
            }
        }

        public static MouseDisasterPawnBehavior Compose(bool thief, bool beggar, MouseDisasterEventAttitude attitude)
        {
            if (attitude == MouseDisasterEventAttitude.Friendly)
                return MouseDisasterPawnBehavior.SeekFood | MouseDisasterPawnBehavior.ReliefOnly;
            if (thief)
                return MouseDisasterPawnBehavior.SeekFood | MouseDisasterPawnBehavior.Steal |
                    (attitude == MouseDisasterEventAttitude.FriendlyLeaning ? MouseDisasterPawnBehavior.None : MouseDisasterPawnBehavior.IgnoreCombatFear);
            return beggar ? MouseDisasterPawnBehavior.SeekFood | MouseDisasterPawnBehavior.Beg : MouseDisasterPawnBehavior.None;
        }
    }
}
