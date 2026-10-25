namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_EndingRules
    {
        internal static float ThreatFactor(int trust)
        {
            int clamped = SuiyinNodes.ClampTrust(trust);
            return 1f - clamped / 400f;
        }
    }
}
