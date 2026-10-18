using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Trade
{
    // XML 反射入口 只购买营养食物 不生成出售食物
    public sealed class StockGenerator_RHAH_Food : StockGenerator
    {
        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            yield break;
        }

        public override bool HandlesThingDef(ThingDef thingDef)
        {
            return thingDef != null && thingDef.IsNutritionGivingIngestible &&
                thingDef.ingestible.HumanEdible && !thingDef.IsDrug && !thingDef.IsCorpse;
        }

        public override Tradeability TradeabilityFor(ThingDef thingDef)
        {
            return HandlesThingDef(thingDef) && thingDef.tradeability != Tradeability.None &&
                thingDef.tradeability != Tradeability.Buyable ? Tradeability.Sellable : Tradeability.None;
        }
    }
}
