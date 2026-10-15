using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Narrative;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class KingdomArcRuntime
    {
        internal static KingdomArcState State => Current.Game?.GetComponent<NarrativeState>()?.Kingdom;
        internal static bool CanAct => Current.ProgramState == ProgramState.Playing && RHAH_Runtime.AllowsNewContent &&
            State != null && State.Invitation != KingdomInvitation.Disabled;
        internal static bool CanUse => CanAct && (KingdomArcActions.Narrator || State.Invitation == KingdomInvitation.Accepted);

        internal static bool Open()
        {
            KingdomArcState state = State;
            if (!CanUse)
            {
                return false;
            }

            if (state.Invitation == KingdomInvitation.Accepted)
            {
                if (state.Progress.Stage == KingdomStage.None && !KingdomArcActions.Start()) return false;
                KingdomArcActions.Wake();
                if (!Find.WindowStack.IsOpen<Window_RHAH_KingdomArc>())
                    Find.WindowStack.Add(new Window_RHAH_KingdomArc(state, state.EarlyComplete, state.Progress.Stage, state.Progress.Revision, null, KingdomArcPresentation.Conditions(state.Progress)));
                return true;
            }
            if (state.Invitation == KingdomInvitation.None || state.Invitation == KingdomInvitation.Declined)
            {
                EventChainRuntime runtime = OxygenEventChainHost.Runtime;
                int tick = Find.TickManager.TicksGame;
                if (runtime == null || tick == int.MaxValue)
                {
                    return false;
                }

                int id = runtime.Start(RHAH_EventChainProvider.ProviderKey, KingdomArcDefinition.DefinitionId,
                    0, -1, tick, -1, "", 0, tick + 1);
                if (id == 0)
                {
                    return false;
                }

                state.Begin(id);
            }

            if (!Find.WindowStack.IsOpen<Window_RHAH_KingdomArc>())
            {
                Find.WindowStack.Add(new Window_RHAH_KingdomArc(state, false));
            }
            return true;
        }

        internal static bool Reply(KingdomInvitation reply, int expectedInstanceId)
        {
            KingdomArcState state = State;
            if (!CanUse || !KingdomArcActions.Narrator || !state.CanReply || state.InstanceId != expectedInstanceId ||
                (reply != KingdomInvitation.Accepted && reply != KingdomInvitation.Declined))
            {
                return false;
            }

            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            if (runtime == null) return false;
            if (reply == KingdomInvitation.Accepted && !KingdomArcActions.Start()) return false;
            if (!runtime.End(RHAH_EventChainProvider.ProviderKey, state.InstanceId,
                Find.TickManager.TicksGame, reply == KingdomInvitation.Accepted ? EventChainEnd.Completed : EventChainEnd.Cancelled))
            {
                return false;
            }

            return state.TryReply(reply);
        }

        internal static void Notify(int instanceId)
        {
            KingdomArcState state = State;
            if (state == null || !state.CanReply || state.InstanceId != instanceId || !CanUse)
            {
                return;
            }

            LetterDef def = DefDatabase<LetterDef>.GetNamed("RHAH_KingdomArcLetter");
            ChoiceLetter_RHAH_KingdomArc letter = (ChoiceLetter_RHAH_KingdomArc)LetterMaker.MakeLetter(
                "RHAH_Kingdom_Title".Translate(), "RHAH_Kingdom_Arrival".Translate(), def);
            letter.InstanceId = instanceId;
            Find.LetterStack.ReceiveLetter(letter);
        }

        internal static void SendCurrent()
        {
            KingdomArcState state = State;
            KingdomArcProgress progress = state.Progress;
            KingdomArcSegment segment = KingdomArcPresentation.Scenes(progress.Stage, progress)[0];
            ChoiceLetter_RHAH_KingdomArc letter = (ChoiceLetter_RHAH_KingdomArc)LetterMaker.MakeLetter(
                "RHAH_Kingdom_Title".Translate(), segment.BodyKey.Translate(), DefDatabase<LetterDef>.GetNamed("RHAH_KingdomArcLetter"));
            letter.InstanceId = state.InstanceId;
            letter.Stage = progress.Stage;
            letter.Revision = progress.Revision;
            KingdomArcSegment[] scenes = KingdomArcPresentation.Scenes(progress.Stage, progress);
            for (int i = 0; i < scenes.Length; i++)
            {
                letter.BodyKeys.Add(scenes[i].BodyKey);
                letter.SpeakerKeys.Add(scenes[i].SpeakerKey);
                letter.TexturePaths.Add(scenes[i].TexturePath);
            }
            letter.Conditions = KingdomArcPresentation.Conditions(progress);
            Find.LetterStack.ReceiveLetter(letter);
        }

        internal static void DrawEntry(Listing_Standard listing)
        {
            string key = State == null || State.Invitation == KingdomInvitation.None || State.Invitation == KingdomInvitation.Declined
                ? "RHAH_Settings_Kingdom_Start" : State.EarlyComplete ? "RHAH_Settings_Kingdom_Read" : "RHAH_Settings_Kingdom_Continue";
            if (CanUse)
            {
                if (listing.ButtonText(key.Translate()) && !Open())
                    Messages.Message("RHAH_Settings_Kingdom_Unavailable".Translate(), RimWorld.MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                string reason = Current.ProgramState != ProgramState.Playing || State == null ? "RHAH_Settings_Kingdom_NoGame"
                    : !RHAH_Runtime.AllowsNewContent || State.Invitation == KingdomInvitation.Disabled ? "RHAH_Settings_Kingdom_Disabled"
                    : "RHAH_Settings_Kingdom_RequiresSuiyin";
                listing.Label(reason.Translate());
            }
            if (CanAct && State.Invitation == KingdomInvitation.Accepted && KingdomArcActions.Home != null && listing.ButtonText("RHAH_MaterialSelection_CopyOpen".Translate()))
            {
                Window_RHAH_KingdomMaterials.OpenCopy();
            }
        }
    }
}
