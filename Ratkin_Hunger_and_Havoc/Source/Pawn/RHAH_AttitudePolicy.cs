using HungerAndHavoc.Api;

namespace HungerAndHavoc.Pawn
{
    internal enum RHAH_AttitudeShift
    {
        None = 0,
        Hostile = 1,
        Leave = 2
    }

    internal static class RHAH_AttitudePolicy
    {
        internal static RHAH_Attitude ShiftDown(RHAH_Attitude attitude)
        {
            switch (attitude)
            {
                case RHAH_Attitude.Friendly:
                    return RHAH_Attitude.LeaningFriendly;
                case RHAH_Attitude.LeaningFriendly:
                    return RHAH_Attitude.Neutral;
                case RHAH_Attitude.Neutral:
                    return RHAH_Attitude.LeaningHostile;
                case RHAH_Attitude.LeaningHostile:
                    return RHAH_Attitude.Hostile;
                default:
                    return RHAH_Attitude.Hostile;
            }
        }

        internal static RHAH_Attitude Arrival(RHAH_Attitude configured, bool suiYin, int trust)
        {
            if (suiYin && trust < 0)
            {
                return ShiftDown(configured);
            }

            return configured;
        }

        internal static RHAH_AttitudeShift React(RHAH_Attitude attitude, bool harm)
        {
            switch (attitude)
            {
                case RHAH_Attitude.Hostile:
                    return RHAH_AttitudeShift.Hostile;
                case RHAH_Attitude.LeaningHostile:
                    return harm ? RHAH_AttitudeShift.Hostile : RHAH_AttitudeShift.Leave;
                case RHAH_Attitude.LeaningFriendly:
                case RHAH_Attitude.Friendly:
                    return RHAH_AttitudeShift.Leave;
                default:
                    return harm ? RHAH_AttitudeShift.None : RHAH_AttitudeShift.Leave;
            }
        }
    }
}
