using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_AllyTransfer
    {
        const int GiftOpenDelay = 110;
        const int DefaultFlightTicks = 200;

        internal static int Goodwill(float totalValue)
        {
            if (!(totalValue > 0f))
            {
                return 0;
            }

            return totalValue >= 5000f ? 10 : (int)Math.Ceiling(totalValue / 500d);
        }

        internal static void OnArrival(RHAH_ChoiceRecord record, Faction faction, Verse.Pawn pawn, int tick)
        {
            if (record == null || faction == null || pawn == null || pawn.Dead)
            {
                return;
            }

            float value = pawn.MarketValue;
            if (value > 0f)
            {
                record.AllyTransferredValue += value;
            }
            GrantGoodwill(record, faction);

            if (record.AllyGiftChecked)
            {
                return;
            }

            record.AllyGiftChecked = true;
            if (faction.def == null || faction.def.techLevel < TechLevel.Industrial ||
                !ShouldGiveGift(faction.def.techLevel, Rand.Value))
            {
                return;
            }

            ThingDef incoming = faction.def.dropPodIncoming ?? ThingDefOf.DropPodIncoming;
            int flightTicks = incoming.skyfaller.ticksToImpactRange.max;
            long due = (long)tick + GiftDelay(Rand.Value, flightTicks);
            record.AllyGiftDueTick = due > int.MaxValue ? int.MaxValue : (int)due;
        }

        static void GrantGoodwill(RHAH_ChoiceRecord record, Faction faction)
        {
            int target = Goodwill(record.AllyTransferredValue);
            if (target <= record.AllyGoodwillGranted)
            {
                return;
            }

            Faction player = Faction.OfPlayer;
            int before = faction.BaseGoodwillWith(player);
            while (record.AllyGoodwillGranted < target)
            {
                int remaining = target - record.AllyGoodwillGranted;
                int requested = remaining;
                // 原版向自然好感靠拢时额外增加25%，反算以守住每批上限
                while (requested > 0 && faction.CalculateAdjustedGoodwillChange(player, requested) > remaining)
                {
                    requested--;
                }
                if (requested == 0)
                {
                    break;
                }

                int previous = faction.BaseGoodwillWith(player);
                if (!faction.TryAffectGoodwillWith(player, requested, canSendMessage: false))
                {
                    break;
                }
                int gained = faction.BaseGoodwillWith(player) - previous;
                if (gained <= 0)
                {
                    break;
                }
                record.AllyGoodwillGranted += gained;
            }

            int after = faction.BaseGoodwillWith(player);
            if (after > before && Current.ProgramState == ProgramState.Playing)
            {
                Messages.Message("MessageGoodwillChangedWithReason".Translate(faction.Name,
                    before.ToString("F0"), after.ToString("F0"), "RHAH_Choice_AllyGoodwillReason".Translate()),
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        internal static bool ShouldGiveGift(TechLevel techLevel, float roll)
        {
            return techLevel >= TechLevel.Industrial && roll >= 0f && roll < 0.5f;
        }

        internal static int GiftDelay(float roll, int flightTicks = DefaultFlightTicks)
        {
            // 抵达和开舱也必须计入三天，额外留一个tick给开舱判定
            int maximum = Math.Max(0, 3 * GenDate.TicksPerDay - GiftOpenDelay - 1 - Math.Max(0, flightTicks));
            int minimum = Math.Min(GenDate.TicksPerDay / 4, maximum);
            float fraction = !(roll > 0f) ? 0f : roll >= 1f ? 1f : roll;
            return minimum + (int)((maximum - minimum) * (double)fraction);
        }

        internal static List<Thing> CreateGift()
        {
            List<Thing> gift = new List<Thing>(6);
            int first = Rand.Range(0, 4);
            int second = Rand.Range(0, 3);
            if (second >= first)
            {
                second++;
            }
            AddGiftMaterial(gift, first);
            AddGiftMaterial(gift, second);
            return gift;
        }

        static void AddGiftMaterial(List<Thing> gift, int material)
        {
            ThingDef def;
            int count;
            switch (material)
            {
                case 0:
                    def = ThingDefOf.ComponentIndustrial;
                    count = Rand.RangeInclusive(1, 5);
                    break;
                case 1:
                    def = ThingDefOf.MealSimple;
                    count = Rand.RangeInclusive(10, 30);
                    break;
                case 2:
                    def = ThingDefOf.WoodLog;
                    count = Rand.RangeInclusive(75, 200);
                    break;
                default:
                    def = ThingDefOf.Steel;
                    count = Rand.RangeInclusive(50, 150);
                    break;
            }

            while (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(def);
                thing.stackCount = Math.Min(count, def.stackLimit);
                gift.Add(thing);
                count -= thing.stackCount;
            }
        }

        internal static void Tick(RHAH_ChoiceRecord record, int tick)
        {
            if (record == null || record.AllyGiftDueTick < 0 || tick < record.AllyGiftDueTick)
            {
                return;
            }

            record.AllyGiftDueTick = -1;
            Faction faction = RHAH_ChoiceRuntime.FindFaction(record.AllyFactionId);
            Map map = RHAH_ChoiceRuntime.FindMap(record.MapId);
            if (map == null || !map.IsPlayerHome)
            {
                map = Find.AnyPlayerHomeMap;
            }
            if (map == null || faction == null)
            {
                return;
            }

            List<Thing> gift = CreateGift();
            IntVec3 cell = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(cell, map, gift, GiftOpenDelay,
                false, false, true, false, true, faction);
            Find.LetterStack.ReceiveLetter("RHAH_Choice_AllyGiftLabel".Translate(),
                "RHAH_Choice_AllyGiftText".Translate(faction.NameColored), LetterDefOf.PositiveEvent,
                new TargetInfo(cell, map), faction);
        }
    }
}
