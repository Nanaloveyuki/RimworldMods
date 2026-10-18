using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Incidents;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_AllyDestinationTests
    {
        [Theory]
        [InlineData(0f, 0)]
        [InlineData(1f, 1)]
        [InlineData(500f, 1)]
        [InlineData(501f, 2)]
        [InlineData(5000f, 10)]
        [InlineData(50000f, 10)]
        public void TransferredValueGoodwillRoundsUpAndCapsPerBatch(float value, int expected)
        {
            Assert.Equal(expected, RHAH_AllyTransfer.Goodwill(value));
        }

        [Theory]
        [InlineData(TechLevel.Medieval, 0f, false)]
        [InlineData(TechLevel.Industrial, 0.499f, true)]
        [InlineData(TechLevel.Industrial, 0.5f, false)]
        [InlineData(TechLevel.Spacer, 0.499f, true)]
        public void GiftsRequireIndustrialTechnologyAndWinningHalfOfRolls(TechLevel techLevel, float roll, bool expected)
        {
            Assert.Equal(expected, RHAH_AllyTransfer.ShouldGiveGift(techLevel, roll));
        }

        [Fact]
        public void GiftDeadlineIncludesTravelAndOpeningTime()
        {
            Assert.Equal(15000, RHAH_AllyTransfer.GiftDelay(0f));
            Assert.True(RHAH_AllyTransfer.GiftDelay(1f, 500) + 500 + 110 < 3 * GenDate.TicksPerDay);
        }

        [Fact]
        public void PlayerOnlyWorldHasNoAllyDestinationOrRelationErrors()
        {
            Game previousGame = Current.Game;
            int previousErrors = Log.Messages.Where(message => message.type == LogMessageType.Error).Sum(message => message.repeats);
            try
            {
                Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                World world = (World)FormatterServices.GetUninitializedObject(typeof(World));
                world.factionManager = new FactionManager();
                world.worldObjects = new WorldObjectsHolder();
                Current.Game.World = world;
                Faction player = new Faction
                {
                    def = new FactionDef { isPlayer = true, humanlikeFaction = true },
                    loadID = 70
                };
                world.factionManager.AllFactionsListForReading.Add(player);
                typeof(FactionManager).GetField("ofPlayer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(world.factionManager, player);

                Assert.False(RHAH_ChoiceRuntime.HasAllyDestination());
                Assert.Empty(RHAH_ChoiceRuntime.AllyDestinations());
                Assert.Equal(previousErrors, Log.Messages.Where(message => message.type == LogMessageType.Error).Sum(message => message.repeats));
            }
            finally
            {
                Current.Game = previousGame;
            }
        }
    }
}
