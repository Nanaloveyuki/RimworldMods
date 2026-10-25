namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class SuiyinNodes
    {
        internal const int TrustMin = -100;
        internal const int TrustMax = 100;

        internal static int ClampTrust(int trust)
        {
            if (trust < TrustMin)
            {
                return TrustMin;
            }

            if (trust > TrustMax)
            {
                return TrustMax;
            }

            return trust;
        }
    }
}
