using System;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum KingdomInvitation
    {
        None = 0,
        Pending = 1,
        Accepted = 2,
        Declined = 3,
        Disabled = 4
    }

    internal sealed class KingdomArcState : IExposable
    {
        public KingdomArcState() { }
        internal int Segment;
        internal int Act = 1;
        internal int InstanceId;
        internal KingdomInvitation Invitation;
        internal KingdomArcProgress Progress = new KingdomArcProgress();
        internal bool EarlyComplete => Progress.Stage == KingdomStage.Complete;
        internal bool CanReply => Invitation == KingdomInvitation.Pending;

        internal void Begin(int instanceId)
        {
            if (instanceId <= 0 || (Invitation != KingdomInvitation.None && Invitation != KingdomInvitation.Declined))
            {
                throw new InvalidOperationException("Kingdom invitation cannot begin in this state.");
            }

            InstanceId = instanceId;
            Invitation = KingdomInvitation.Pending;
        }

        internal bool TryReply(KingdomInvitation reply)
        {
            if (!CanReply || (reply != KingdomInvitation.Accepted && reply != KingdomInvitation.Declined))
            {
                return false;
            }

            Invitation = reply;
            Act = reply == KingdomInvitation.Accepted ? 2 : 1;
            return true;
        }

        internal void Disable()
        {
            if (Invitation == KingdomInvitation.Pending)
            {
                Invitation = KingdomInvitation.Disabled;
            }
        }

        internal void SetSegment(int index)
        {
            Segment = Math.Max(0, Math.Min(index, KingdomArcScenes.Count - 1));
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Segment, "segment", 0);
            Scribe_Values.Look(ref Act, "act", 1);
            Scribe_Values.Look(ref InstanceId, "instanceId", 0);
            Scribe_Values.Look(ref Invitation, "invitation", KingdomInvitation.None);
            Scribe_Deep.Look(ref Progress, "progress");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Progress = Progress ?? new KingdomArcProgress();
                SetSegment(Segment);
            }
        }
    }
}
