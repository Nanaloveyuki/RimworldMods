using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    // XML 和存档通过类型名创建世界地点
    public sealed class WorldObject_RHAH_SoupKitchen : WorldObject
    {
        int mealCount;

        internal void Configure(int meals)
        {
            mealCount = meals;
        }

        internal bool CanVisit(RimWorld.Planet.Caravan caravan)
        {
            return RHAH_Runtime.AllowsNewContent && Spawned && !Destroyed && mealCount > 0 &&
                GetComponent<TimeoutComp>()?.Passed != true && RHAH_SoupKitchen.EligibleFaction(Faction) &&
                caravan != null && caravan.Spawned && caravan.IsPlayerControlled && caravan.PawnsListForReading.Count > 0;
        }

        internal bool Visit(RimWorld.Planet.Caravan caravan)
        {
            if (!CanVisit(caravan) || caravan.Tile != Tile) return false;
            float wealth = RHAH_SoupKitchen.Wealth(caravan);
            int loss = RHAH_SoupKitchen.GoodwillLoss(wealth);
            if (loss > 0)
            {
                Faction.TryAffectGoodwillWith(RimWorld.Faction.OfPlayer, -loss);
                Find.LetterStack.ReceiveLetter("RHAH_Incident_SoupKitchen_Label".Translate(),
                    "RHAH_SoupKitchen_Rejected".Translate(Faction.Name, wealth.ToStringMoney(), loss),
                    LetterDefOf.NegativeEvent, new LookTargets(caravan), Faction);
                return false;
            }

            if (!DeliverMeals(caravan))
            {
                Messages.Message("RHAH_SoupKitchen_CannotCarry".Translate(), new LookTargets(caravan), MessageTypeDefOf.RejectInput);
                return false;
            }
            int received = mealCount;
            mealCount = 0;
            Destroy();
            Find.LetterStack.ReceiveLetter("RHAH_Incident_SoupKitchen_Label".Translate(),
                "RHAH_SoupKitchen_Received".Translate(Faction.Name, received),
                LetterDefOf.PositiveEvent, new LookTargets(caravan), Faction);
            return true;
        }

        bool DeliverMeals(RimWorld.Planet.Caravan caravan)
        {
            List<Thing> delivered = new List<Thing>();
            int remaining = mealCount;
            while (remaining > 0)
            {
                Thing meal = ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack);
                meal.stackCount = Math.Min(remaining, meal.def.stackLimit);
                bool added = false;
                foreach (Verse.Pawn pawn in caravan.PawnsListForReading)
                {
                    if (pawn.inventory?.innerContainer.TryAdd(meal, false) != true) continue;
                    added = true;
                    break;
                }
                if (!added)
                {
                    meal.Destroy();
                    foreach (Thing item in delivered)
                    {
                        item.holdingOwner.Remove(item);
                        item.Destroy();
                    }
                    return false;
                }
                delivered.Add(meal);
                remaining -= meal.stackCount;
            }
            return true;
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(RimWorld.Planet.Caravan caravan)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(caravan)) yield return option;
            foreach (FloatMenuOption option in CaravanArrivalActionUtility.GetFloatMenuOptions(
                () => CanVisit(caravan), () => new CaravanArrivalAction_RHAH_SoupKitchen(this),
                "RHAH_SoupKitchen_Visit".Translate(), caravan, Tile, this)) yield return option;
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(RimWorld.Planet.Caravan caravan)
        {
            foreach (Gizmo gizmo in base.GetCaravanGizmos(caravan)) yield return gizmo;
            if (caravan.Tile == Tile && CanVisit(caravan))
                yield return new Command_Action
                {
                    defaultLabel = "RHAH_SoupKitchen_Visit".Translate(),
                    defaultDesc = "RHAH_SoupKitchen_Terms".Translate(RHAH_SoupKitchen.DurationDays),
                    action = () => Visit(caravan)
                };
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n" + "RHAH_SoupKitchen_Eligibility".Translate();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mealCount, "mealCount", 0);
        }
    }
}
