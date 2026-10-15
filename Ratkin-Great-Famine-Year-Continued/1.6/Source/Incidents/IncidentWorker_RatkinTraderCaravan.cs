using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class IncidentWorker_RatkinTraderCaravan : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms?.target is Map map &&
                   base.CanFireNowSub(parms) &&
                   Find.Storyteller.difficulty.ChildrenAllowed &&
                   MouseDisasterUtility.TryFindEntryCell(map, out _) &&
                   MouseDisasterUtility.TryFindFormerFaction(out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms?.target is Map map))
            {
                return false;
            }

            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell) || !MouseDisasterUtility.TryFindFormerFaction(out Faction faction))
            {
                return false;
            }

            MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
            MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(map, faction);
            int saleChildren = Rand.RangeInclusive(5, 30);
            return GameComponent_MouseDisasterPawnGeneration.TryStartTraderCaravan(def, parms, map, cell, faction, saleChildren);
        }

        internal static void PrepareChattelChild(Pawn child, Faction faction)
        {
            if (child == null)
            {
                return;
            }

            if (child.Faction != faction)
            {
                child.SetFaction(faction);
            }

            MouseDisasterUtility.PrepareTradablePrisoner(child, faction);
        }

        internal static Pawn GenerateFemaleTraderPawn(Faction faction)
        {
            Pawn pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(
                MouseDisasterDefOf.MouseDisaster_TraderRatkinAdult,
                faction,
                DevelopmentalStage.Adult,
                0.65f,
                allowViolenceDisabledTraits: false,
                fixedGender: Gender.Female);
            return pawn;
        }

        internal static void FillTraderInventory(Pawn traderPawn)
        {
            TraderKindDef traderKind = traderPawn?.trader?.traderKind;
            ThingDef meatDef = DefDatabase<ThingDef>.GetNamedSilentFail("Meat_Human");
            if (CanTraderActuallySell(traderKind, meatDef))
            {
                Thing meat = ThingMaker.MakeThing(meatDef);
                meat.stackCount = 50;
                traderPawn.inventory.innerContainer.TryAdd(meat);
            }

            HashSet<ThingDef> goodsToAdd = new HashSet<ThingDef>();
            foreach (string defName in MouseDisasterTraderTradePolicy.OptionalGoodDefNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def != null)
                {
                    goodsToAdd.Add(def);
                }
            }

            List<ThingDef> linkedCuisineGoods = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(IsRatEggCuisineIngredient)
                .InRandomOrder()
                .Take(16)
                .ToList();
            for (int i = 0; i < linkedCuisineGoods.Count; i++)
            {
                goodsToAdd.Add(linkedCuisineGoods[i]);
            }

            foreach (ThingDef def in goodsToAdd)
            {
                if (!CanTraderActuallySell(traderKind, def))
                {
                    continue;
                }

                Thing thing = ThingMaker.MakeThing(def);
                thing.stackCount = def.stackLimit > 1 ? System.Math.Min(def.stackLimit, Rand.RangeInclusive(3, 15)) : 1;
                traderPawn.inventory.innerContainer.TryAdd(thing);
            }
        }

        private static bool IsRatEggCuisineIngredient(ThingDef def)
        {
            if (def == null || def.category != ThingCategory.Item || def.IsCorpse || def.BaseMarketValue <= 0f)
            {
                return false;
            }

            return MouseDisasterTraderTradePolicy.MatchesCuisineKeywords(def.defName, def.label);
        }

        private static bool CanTraderActuallySell(TraderKindDef traderKind, ThingDef def)
        {
            if (traderKind == null || def == null)
            {
                return false;
            }

            return (def.tradeability.TraderCanSell() ||
                    MouseDisasterTraderTradePolicy.CanTraderStockRatEggTradeGood(traderKind.defName, def.defName, def.label)) &&
                   traderKind.WillTrade(def);
        }
    }
}
