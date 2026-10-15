using HungerAndHavoc.Core;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using Verse;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_SuiyinTrust
    {
        internal const int Hold = -1;
        internal const int Kill = -2;
        internal const int Deliver = 1;
        internal const int Leave = 1;
        internal const int Expel = -1;

        internal static int Value(int fallback, int configured)
        {
            return Core.RHAH_Mod.Settings == null ? fallback : configured;
        }

        internal static void Note(int people, int each)
        {
            if (people <= 0 || each == 0 || !RHAH_EndingRuntime.CountsNow())
            {
                return;
            }

            Current.Game?.GetComponent<NarrativeState>()?.RecordTrust(people * each);
        }
    }
}
