namespace HungerAndHavoc.Storyteller.Suiyin
{

    internal enum RHAH_IdentityTier
    {
        None = 0,
        Partial = 1,
        Full = 2
    }


    internal static class RHAH_EndingRules
    {
        internal const int TrustFloor = 50;
        internal const int HopeTrust = 75;

        internal static float ThreatFactor(int trust)
        {
            int clamped = SuiyinNodes.ClampTrust(trust);
            return 1f - clamped / 400f;
        }


        internal static RHAH_IdentityTier IdentityOffer(int trust, int trustFloor, int hopeTrust)
        {
            if (trust >= hopeTrust)
            {
                return RHAH_IdentityTier.Full;
            }

            if (trust >= trustFloor)
            {
                return RHAH_IdentityTier.Partial;
            }

            return RHAH_IdentityTier.None;
        }
    }
}
