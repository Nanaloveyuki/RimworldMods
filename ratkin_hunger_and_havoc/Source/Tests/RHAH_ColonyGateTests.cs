using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Pawn;
using HungerAndHavoc.Trade;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_ColonyGateTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.trade.gate.tests");
        static int settlements;
        static int currencyTransfers;
        static bool tradeSucceeds;
        static Faction seller;

        static RHAH_ColonyGateTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        public RHAH_ColonyGateTests()
        {
            settlements = 0;
            currencyTransfers = 0;
            tradeSucceeds = true;
            MethodInfo execute = AccessTools.Method(typeof(TradeDeal), nameof(TradeDeal.TryExecute));
            harmony.CreateClassProcessor(typeof(RHAH_TradeGatePatch)).Patch();
            harmony.Patch(execute, prefix: new HarmonyMethod(typeof(RHAH_ColonyGateTests), nameof(SettleTrade))
            {
                priority = Priority.Last
            });
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            seller = null;
            fixture.Dispose();
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

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void DeniedPawnStopsTheWholeTradeBeforeCurrencyOrEarlierPawnsMove(bool buying, bool releaseDenied)
        {
            Verse.Pawn first = Visitor(2101);
            Verse.Pawn denied = Visitor(2102);
            if (!buying)
            {
                first.SetFactionDirect(Faction.OfPlayer);
                denied.SetFactionDirect(Faction.OfPlayer);
            }
            Faction original = first.Faction;
            if (releaseDenied) RHAH_PawnBehaviors.Register(new ReleasePolicy(denied));
            else RHAH_Api.SetGate(denied, RHAH_BehaviorGate.Transfer, false);
            TradeDeal deal = Deal(PawnTrade(first, buying), PawnTrade(denied, buying));

            Assert.False(deal.TryExecute(out bool actuallyTraded));

            Assert.False(actuallyTraded);
            Assert.Equal(0, settlements);
            Assert.Equal(0, currencyTransfers);
            Assert.Same(original, first.Faction);
            Assert.Same(original, denied.Faction);
            Assert.True(RHAH_Api.IsVisitor(first));
            Assert.True(RHAH_Api.IsVisitor(denied));
            Assert.Equal(2, deal.AllTradeables.Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OnlySuccessfulTradesReleaseTheTransferredVisitor(bool succeeds)
        {
            Verse.Pawn pawn = Visitor(2103);
            Faction original = pawn.Faction;
            TradeDeal deal = Deal(PawnTrade(pawn, true));
            tradeSucceeds = succeeds;

            Assert.Equal(succeeds, deal.TryExecute(out bool actuallyTraded));

            Assert.Equal(succeeds, actuallyTraded);
            Assert.Equal(1, settlements);
            Assert.Equal(succeeds ? 1 : 0, currencyTransfers);
            Assert.Same(succeeds ? Faction.OfPlayer : original, pawn.Faction);
            Assert.Equal(!succeeds, RHAH_Api.IsVisitor(pawn));
        }

        [Fact]
        public void PreflightLeavesAllowedVisitorsUntouched()
        {
            Verse.Pawn pawn = Visitor(2104);

            Assert.True(RHAH_ColonyGates.AllowsTrade(PawnTrade(pawn, true)));

            Assert.True(RHAH_Api.IsVisitor(pawn));
        }

        Verse.Pawn Visitor(int id)
        {
            Verse.Pawn pawn = (Verse.Pawn)AccessTools.Method(typeof(RHAH_AttackChoiceTests), "Visitor")
                .Invoke(fixture, new object[] { id });
            seller = pawn.Faction;
            return pawn;
        }

        static Tradeable_Pawn PawnTrade(Verse.Pawn pawn, bool buying)
        {
            Tradeable_Pawn trade = new Tradeable_Pawn();
            (buying ? trade.thingsTrader : trade.thingsColony).Add(pawn);
            trade.ForceTo(buying ? 1 : -1);
            return trade;
        }

        static TradeDeal Deal(params Tradeable[] tradeables)
        {
            TradeDeal deal = (TradeDeal)FormatterServices.GetUninitializedObject(typeof(TradeDeal));
            AccessTools.Field(typeof(TradeDeal), "tradeables").SetValue(deal, new List<Tradeable>(tradeables));
            return deal;
        }

        static bool SettleTrade(TradeDeal __instance, out bool actuallyTraded, ref bool __result)
        {
            settlements++;
            actuallyTraded = tradeSucceeds;
            __result = tradeSucceeds;
            if (!tradeSucceeds) return false;
            currencyTransfers++;
            foreach (Tradeable tradeable in __instance.AllTradeables)
            {
                if (tradeable is not Tradeable_Pawn trade) continue;
                bool buying = trade.ActionToDo == TradeAction.PlayerBuys;
                List<Thing> things = buying ? trade.thingsTrader : trade.thingsColony;
                foreach (Thing thing in things)
                    if (thing is Verse.Pawn pawn) pawn.SetFactionDirect(buying ? Faction.OfPlayer : seller);
            }
            return false;
        }

        sealed class ReleasePolicy : IRHAH_PawnBehavior
        {
            readonly Verse.Pawn denied;

            internal ReleasePolicy(Verse.Pawn denied)
            {
                this.denied = denied;
            }

            public bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
            {
                return null;
            }

            public bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
            {
                return pawn == denied ? false : (bool?)null;
            }
        }
    }
}
