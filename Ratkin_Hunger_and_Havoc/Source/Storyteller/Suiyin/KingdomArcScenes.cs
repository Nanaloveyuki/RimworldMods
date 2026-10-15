namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal readonly struct KingdomArcSegment
    {
        internal string TitleKey { get; }
        internal string SpeakerKey { get; }
        internal string BodyKey { get; }
        internal string TexturePath { get; }
        internal double Seconds { get; }

        internal KingdomArcSegment(string titleKey, string speakerKey, string bodyKey, string texturePath, double seconds)
        {
            TitleKey = titleKey;
            SpeakerKey = speakerKey;
            BodyKey = bodyKey;
            TexturePath = texturePath;
            Seconds = seconds;
        }
    }

    internal static class KingdomArcScenes
    {
        private static readonly KingdomArcSegment[] segments =
        {
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E1_01", "KingdomArc/K1-E1-garden-1", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Child", "RHAH_Kingdom_E1_02", "KingdomArc/K1-E1-garden-2", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Father", "RHAH_Kingdom_E1_03", "KingdomArc/K1-E1-garden-2", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Child", "RHAH_Kingdom_E1_04", "KingdomArc/K1-E1-garden-2", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Child", "RHAH_Kingdom_E1_05", "KingdomArc/K1-E1-garden-3", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Father", "RHAH_Kingdom_E1_06", "KingdomArc/K1-E1-garden-3", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Child", "RHAH_Kingdom_E1_07", "KingdomArc/K1-E1-garden-3", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Child", "RHAH_Kingdom_E1_08", "KingdomArc/K1-E1-garden-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Father", "RHAH_Kingdom_E1_09", "KingdomArc/K1-E1-garden-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Child", "RHAH_Kingdom_E1_10", "KingdomArc/K1-E1-garden-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Father", "RHAH_Kingdom_E1_11", "KingdomArc/K1-E1-garden-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Father", "RHAH_Kingdom_E1_12", "KingdomArc/K1-E1-garden-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_FatherQuiet", "RHAH_Kingdom_E1_13", "KingdomArc/K1-E1-garden-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Child", "RHAH_Kingdom_E1_14", "KingdomArc/K1-E1-garden-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E1Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E1_15", "KingdomArc/K1-E1-garden-1", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Memory", "RHAH_Kingdom_E2_01", "KingdomArc/K1-E2-beyond-the-wall-1", 1.5d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Young", "RHAH_Kingdom_E2_02", "KingdomArc/K1-E2-beyond-the-wall-2", 1.5d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Memory", "RHAH_Kingdom_E2_03", "KingdomArc/K1-E2-beyond-the-wall-3", 1.5d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Memory", "RHAH_Kingdom_E2_04", "KingdomArc/K1-E2-beyond-the-wall-4", 1.5d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Young", "RHAH_Kingdom_E2_05", "KingdomArc/K1-E2-beyond-the-wall-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E2_06", "KingdomArc/K1-E2-beyond-the-wall-4", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E2_07", "KingdomArc/K1-E2-beyond-the-wall-5", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E2_08", "KingdomArc/K1-E2-beyond-the-wall-6", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Young", "RHAH_Kingdom_E2_09", "KingdomArc/K1-E2-beyond-the-wall-6", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E2_10", "KingdomArc/K1-E2-beyond-the-wall-6", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Young", "RHAH_Kingdom_E2_11", "KingdomArc/K1-E2-beyond-the-wall-7", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Memory", "RHAH_Kingdom_E2_12", "KingdomArc/K1-E2-beyond-the-wall-7", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Young", "RHAH_Kingdom_E2_13", "KingdomArc/K1-E2-beyond-the-wall-8", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Father", "RHAH_Kingdom_E2_14", "KingdomArc/K1-E2-beyond-the-wall-8", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Mother", "RHAH_Kingdom_E2_15", "KingdomArc/K1-E2-beyond-the-wall-8", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Mother", "RHAH_Kingdom_E2_16", "KingdomArc/K1-E2-beyond-the-wall-8", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E2Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E2_17", "KingdomArc/K1-E2-beyond-the-wall-9", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E3Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E3_01", "KingdomArc/K1-E3-randy-hand-1", 4d),
            new KingdomArcSegment("RHAH_Kingdom_E3Title", "RHAH_Kingdom_Young", "RHAH_Kingdom_E3_02", "KingdomArc/K1-E3-randy-hand-2", 4d),
            new KingdomArcSegment("RHAH_Kingdom_E3Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E3_03", "KingdomArc/K1-E3-randy-hand-3", 3d),
            new KingdomArcSegment("RHAH_Kingdom_E3Title", "RHAH_Kingdom_Memory", "RHAH_Kingdom_E3_04", "KingdomArc/K1-E3-randy-hand-4", 1.5d),
            new KingdomArcSegment("RHAH_Kingdom_E3Title", "RHAH_Kingdom_Randy", "RHAH_Kingdom_E3_05", "KingdomArc/K1-E3-randy-hand-5", 5d),
            new KingdomArcSegment("RHAH_Kingdom_E3Title", "RHAH_Kingdom_Memory", "RHAH_Kingdom_E3_06", "KingdomArc/K1-E3-randy-hand-6", 1.5d),
            new KingdomArcSegment("RHAH_Kingdom_E3Title", "RHAH_Kingdom_Memory", "RHAH_Kingdom_E3_07", "KingdomArc/K1-E3-randy-hand-7", 2d),
            new KingdomArcSegment("RHAH_Kingdom_E3Title", "RHAH_Kingdom_Diary", "RHAH_Kingdom_E3_08", "KingdomArc/K1-E3-randy-hand-8", 3d),
            new KingdomArcSegment("RHAH_Kingdom_E4Title", "RHAH_Kingdom_Letter", "RHAH_Kingdom_E4_01", "KingdomArc/K1-E4-private-letter-1", 0d),
            new KingdomArcSegment("RHAH_Kingdom_E4Title", "RHAH_Kingdom_Letter", "RHAH_Kingdom_E4_02", "KingdomArc/K1-E4-private-letter-1", 0d),
            new KingdomArcSegment("RHAH_Kingdom_E4Title", "RHAH_Kingdom_Letter", "RHAH_Kingdom_E4_03", "KingdomArc/K1-E4-private-letter-1", 0d),
            new KingdomArcSegment("RHAH_Kingdom_E4Title", "RHAH_Kingdom_Letter", "RHAH_Kingdom_E4_04", "KingdomArc/K1-E4-private-letter-1", 0d)
        };

        internal static int Count => segments.Length;
        internal static int ChoiceIndex => segments.Length - 1;
        internal static KingdomArcSegment Get(int index) => segments[index];
    }
}
