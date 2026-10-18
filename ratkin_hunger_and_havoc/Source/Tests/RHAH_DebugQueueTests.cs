using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_DebugQueueTests
    {
        [Fact]
        public void QueueingADebugIncidentDoesNotSpawnIt()
        {
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(null);
            Assert.True(game.QueueIncident("I-001", 240f));
            Assert.Equal(new[] { "I-001" }, game.PendingIncidentDisplayIds);
            Assert.Equal(new[] { 0 }, game.PendingIncidentTargetIds);
        }

        [Fact]
        public void StaggeredQueueDoesNotSpawnBetweenBatchTicks()
        {
            RHAH_Settings previous = RHAH_Mod.Settings;
            RHAH_Settings settings = previous ?? new RHAH_Settings();
            bool stagger = settings.staggerGeneration;
            settings.staggerGeneration = true;
            RHAH_Mod.Settings = settings;
            try
            {
                GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(null);
                Assert.True(game.QueueIncident("I-004", 80f));
                Assert.False(game.TrySpawnPending(1));
                Assert.Equal(new[] { "I-004" }, game.PendingIncidentDisplayIds);
            }
            finally
            {
                settings.staggerGeneration = stagger;
                RHAH_Mod.Settings = previous;
            }
        }

        [Fact]
        public void QueueKeepsDistinctTargetsForTheSameIncidentKind()
        {
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(null);
            Assert.True(game.QueueIncident("I-001", 240f, 4));
            Assert.True(game.QueueIncident("I-004", 80f, 9));
            Assert.Equal(new[] { "I-001", "I-004" }, game.PendingIncidentDisplayIds);
            Assert.Equal(new[] { 4, 9 }, game.PendingIncidentTargetIds);
        }


        [Fact]
        public void ForcedPauseDoesNotBlockTheFrameDrain()
        {
            Assert.True(RHAH_Scheduler.ShouldDrainOnFrame(paused: true, forcePause: false, pending: 1));
            Assert.False(RHAH_Scheduler.ShouldDrainOnFrame(paused: true, forcePause: true, pending: 1));
            Assert.False(RHAH_Scheduler.ShouldDrainOnFrame(paused: false, forcePause: false, pending: 1));
            Assert.False(RHAH_Scheduler.ShouldDrainOnFrame(paused: true, forcePause: false, pending: 0));
        }


    }
}
