using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_ColonyGateTests
    {
        static RHAH_ColonyGateTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        [Fact]
        public void MissingOrigin_PassesCaptureAndTrade()
        {
            Assert.True(RHAH_ColonyGates.AllowsCapture(null, null));
            Assert.True(RHAH_ColonyGates.AllowsTrade(null));

            Tradeable_Pawn idle = new Tradeable_Pawn();
            Assert.Equal(TradeAction.None, idle.ActionToDo);
            Tradeable_Pawn moving = new Tradeable_Pawn();
            moving.thingsTrader.Add(new Thing());
            moving.ForceTo(-1);
            Assert.NotEqual(TradeAction.None, moving.ActionToDo);
            Assert.True(RHAH_ColonyGates.AllowsTrade(moving));
        }
    }
}
