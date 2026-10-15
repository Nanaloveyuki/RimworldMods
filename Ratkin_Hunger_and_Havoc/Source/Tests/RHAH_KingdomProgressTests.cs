using System.Collections.Generic;
using System.Runtime.Serialization;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Narrative;
using Oxygendsl.Core.Events;
using Verse;
using HungerAndHavoc.Storyteller.Suiyin;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_KingdomProgressTests
    {
        [Fact]
        public void AlreadySentResponseDoesNotAccumulateReceiptsWhenWokenRepeatedly()
        {
            RimWorldAssemblies.EnsureResolved();
            Game previous = Current.Game;
            try
            {
                Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                NarrativeState narrative = new NarrativeState(Current.Game);
                Current.Game.components = new List<GameComponent> { narrative };
                KingdomArcProgress progress = narrative.Kingdom.Progress;
                progress.ChainId = 7;
                progress.Stage = KingdomStage.Response;
                progress.ResultSent = true;
                EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
                runtime.Register(new RHAH_EventChainProvider());
                EventChainInstance instance = new EventChainInstance { Id = 7, DefinitionId = KingdomCollaborationDefinition.DefinitionId, NextTick = 1 };
                Assert.True(runtime.Import(RHAH_EventChainProvider.ProviderKey, new[] { instance }, 8, "completed", out string error), error);
                runtime.Activate(0);
                for (int tick = 1; tick <= 100; tick++)
                {
                    Assert.True(runtime.SetStage(RHAH_EventChainProvider.ProviderKey, 7, 0, -1, "", tick));
                    runtime.Tick(tick);
                }
                EventChainInstance loaded = runtime.Find(RHAH_EventChainProvider.ProviderKey, 7);
                Assert.Empty(loaded.Receipts);
                Assert.Equal(-1, loaded.NextTick);
                Assert.Equal(KingdomStage.Response, progress.Stage);
                progress.Stage = KingdomStage.Complete;
                Assert.True(runtime.SetStage(RHAH_EventChainProvider.ProviderKey, 7, 0, -1, "", 101));
                runtime.Tick(101);
                Assert.Equal(EventChainEnd.Completed, loaded.End);
                Assert.Empty(loaded.Receipts);
            }
            finally
            {
                Current.Game = previous;
            }
        }

        [Theory]
        [InlineData(0, 3)]
        [InlineData(2, 2)]
        [InlineData(5, 1)]
        public void RepairUsesActualRecipients(int delivered, int outcome)
        {
            KingdomArcProgress progress = new KingdomArcProgress { Route = KingdomRoute.Repair, Delivered = delivered };
            progress.Finish(KingdomFailure.Expired);
            Assert.Equal((KingdomOutcome)outcome, progress.Outcome);
            Assert.Single(progress.RouteHistory);
            Assert.Equal(KingdomStage.Response, progress.Stage);
        }

        [Fact]
        public void PauseDoesNotInheritEarlierProtectSuccess()
        {
            KingdomArcProgress progress = new KingdomArcProgress { Route = KingdomRoute.Protect, Outcome = KingdomOutcome.Success };
            progress.Finish(KingdomFailure.None);
            progress.Route = KingdomRoute.Pause;
            progress.Finish(KingdomFailure.Cancelled);
            Assert.Equal(KingdomOutcome.Failed, progress.Outcome);
            Assert.Equal(2, progress.RouteHistory.Count);
        }

        [Fact]
        public void ChapterTransitionResetsCursorButKeepsDeliveredFacts()
        {
            KingdomArcProgress progress = new KingdomArcProgress { Cursor = 3, Delivered = 2, ConsentSafeSiteId = 19, SafeSiteId = 24 };
            progress.Move(KingdomStage.Routes);
            Assert.Equal(0, progress.Cursor);
            Assert.Equal(2, progress.Delivered);
            Assert.Equal(19, progress.ConsentSafeSiteId);
            Assert.Equal(24, progress.SafeSiteId);
        }
    }
}
