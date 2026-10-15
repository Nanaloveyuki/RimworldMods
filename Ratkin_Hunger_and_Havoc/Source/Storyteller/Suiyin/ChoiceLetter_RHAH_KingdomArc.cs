using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    public sealed class ChoiceLetter_RHAH_KingdomArc : ChoiceLetter
    {
        internal int InstanceId;
        internal KingdomStage Stage;
        internal int Revision;
        internal List<string> BodyKeys = new List<string>();
        internal List<string> SpeakerKeys = new List<string>();
        internal List<string> TexturePaths = new List<string>();
        internal string Conditions = "";

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return Option_Close;
            }
        }

        public override void OpenLetter()
        {
            KingdomArcState state = KingdomArcRuntime.State;
            bool matches = state != null && state.InstanceId == InstanceId && (Stage == KingdomStage.None || state.Progress.Matches(Stage, Revision));
            KingdomArcSegment[] snapshot = null;
            if (BodyKeys.Count > 0)
            {
                snapshot = new KingdomArcSegment[BodyKeys.Count];
                for (int i = 0; i < snapshot.Length; i++) snapshot[i] = new KingdomArcSegment("RHAH_Kingdom_Title", SpeakerKeys[i], BodyKeys[i], TexturePaths[i], 0d);
            }
            Find.WindowStack.Add(new Window_RHAH_KingdomArc(state, ArchivedOnly || !matches, Stage, Revision, snapshot, Conditions));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref InstanceId, "kingdomInstanceId", 0);
            Scribe_Values.Look(ref Stage, "kingdomStage", KingdomStage.None);
            Scribe_Values.Look(ref Revision, "kingdomRevision", 0);
            Scribe_Collections.Look(ref BodyKeys, "kingdomBodyKeys", LookMode.Value);
            Scribe_Collections.Look(ref SpeakerKeys, "kingdomSpeakerKeys", LookMode.Value);
            Scribe_Collections.Look(ref TexturePaths, "kingdomTexturePaths", LookMode.Value);
            Scribe_Values.Look(ref Conditions, "kingdomConditions", "");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                BodyKeys = BodyKeys ?? new List<string>();
                SpeakerKeys = SpeakerKeys ?? new List<string>();
                TexturePaths = TexturePaths ?? new List<string>();
            }
        }
    }
}
