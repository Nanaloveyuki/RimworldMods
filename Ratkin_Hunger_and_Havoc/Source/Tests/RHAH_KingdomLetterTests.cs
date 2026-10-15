using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_KingdomLetterTests
    {
        [Fact]
        public void SwitchingNarrator_BlocksInvitationAndPreservesReadingState()
        {
            Game previousGame = Current.Game;
            ProgramState previousProgramState = Current.ProgramState;
            RHAH_Settings previousSettings = RHAH_Mod.Settings;
            bool previousInitializing = UnityDataInitializer.initializing;
            UnityDataInitializer.initializing = true;
            try
            {
                RuntimeHelpers.RunClassConstructor(typeof(UnityData).TypeHandle);
            }
            finally
            {
                UnityDataInitializer.initializing = previousInitializing;
            }
            try
            {
                Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                Current.Game.components = new List<GameComponent>();
                Current.Game.storyteller = (RimWorld.Storyteller)FormatterServices.GetUninitializedObject(typeof(RimWorld.Storyteller));
                Current.Game.storyteller.def = new StorytellerDef { defName = "RHAH_Suiyin" };
                NarrativeState narrative = new NarrativeState(Current.Game);
                Current.Game.components.Add(narrative);
                Current.ProgramState = ProgramState.Playing;
                RHAH_Mod.Settings = new RHAH_Settings { enableNewContent = true };
                KingdomArcState state = KingdomArcRuntime.State;
                state.Begin(7);
                state.SetSegment(12);
                Assert.True(KingdomArcRuntime.CanUse);

                foreach (string defName in new[] { "Randy", "Cassandra", "Phoebe", "OtherModStoryteller", null })
                {
                    Current.Game.storyteller.def.defName = defName;
                    Assert.False(KingdomArcRuntime.CanUse);
                    Assert.False(KingdomArcRuntime.Open());
                    Assert.False(KingdomArcRuntime.Reply(KingdomInvitation.Accepted, 7));
                    KingdomArcRuntime.Notify(7);
                    Assert.Equal(KingdomInvitation.Pending, state.Invitation);
                    Assert.Equal(7, state.InstanceId);
                    Assert.Equal(12, state.Segment);
                }

                Current.Game.storyteller.def.defName = "RHAH_Suiyin";
                Assert.True(KingdomArcRuntime.CanUse);
                Assert.True(state.CanReply);
                Assert.Equal(12, state.Segment);
                RHAH_Mod.Settings.enableNewContent = false;
                Assert.False(KingdomArcRuntime.CanUse);
            }
            finally
            {
                Current.Game = previousGame;
                Current.ProgramState = previousProgramState;
                RHAH_Mod.Settings = previousSettings;
            }
        }
    }
}
