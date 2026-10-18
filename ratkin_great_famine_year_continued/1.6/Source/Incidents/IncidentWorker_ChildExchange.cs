using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public class IncidentWorker_ChildExchange : IncidentWorker
    {
        private const int StayDurationTicks = 60000;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return base.CanFireNowSub(parms) &&
                   Find.Storyteller.difficulty.ChildrenAllowed &&
                   MouseDisasterUtility.TryFindEntryCell(map, out _) &&
                   MouseDisasterUtility.FindPlayerOwnedBabyForExchange(map) != null &&
                   MouseDisasterUtility.TryFindFormerFaction(out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell) || !MouseDisasterUtility.TryFindFormerFaction(out Faction faction))
            {
                return false;
            }

            MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
            MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(map, faction);

            Pawn trader = MouseDisasterUtility.GenerateFactionRatkinPawn(
                MouseDisasterDefOf.MouseDisaster_TraderRatkinAdult,
                faction,
                DevelopmentalStage.Adult,
                0.35f,
                allowViolenceDisabledTraits: false,
                fixedGender: Gender.Female);
            if (trader == null)
            {
                return false;
            }

            GenSpawn.Spawn(trader, cell, map);
            MouseDisasterUtility.EnsureTradeLeader(trader, MouseDisasterUtility.ResolveSlaveTraderKind());
            trader.health.AddHediff(MouseDisasterDefOf.MouseDisaster_ChildExchangeTrader);

            int childCount = MouseDisasterUtility.LimitEventPawnCount(Rand.RangeInclusive(6, 10) + 1, 2) - 1;
            List<Pawn> children = new List<Pawn>();
            for (int i = 0; i < childCount; i++)
            {
                Pawn child = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, faction, DevelopmentalStage.Baby, 0.22f);
                if (child == null)
                {
                    continue;
                }

                MouseDisasterUtility.SetBiologicalAgeYears(child, MouseDisasterUtility.RandomRatEggAgeYears());
                GenSpawn.Spawn(child, CellFinder.RandomClosewalkCellNear(cell, map, 5), map);
                MouseDisasterUtility.PrepareTradablePrisoner(child, faction);
                MouseDisasterUtility.StripRatEggInventory(child);
                child.health.AddHediff(MouseDisasterDefOf.MouseDisaster_ChildExchangeChild);
                children.Add(child);
            }

            if (children.Count == 0)
            {
                MouseDisasterUtility.DestroyFailedIncidentPawns(new[] { trader });
                return false;
            }

            MouseDisasterUtility.MarkChildExchangeMoodChildren(children);
            MouseDisasterUtility.LinkIncidentParentToChildren(trader, children);
            MouseDisasterUtility.RegisterChildExchange(trader, children, StayDurationTicks);
            List<Pawn> group = new List<Pawn> { trader };
            group.AddRange(children);
            MouseDisasterUtility.TryStartLeadYourPetRelatedAdultLeashes(group);

            LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(cell, 10f, 15f, isCaravanSendable: false, addFleeToil: false), map, group);
            trader.jobs?.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.GotoWander, cell), JobTag.Misc);

            MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(map, faction);

            ChoiceLetter_ChildExchange letter = LetterMaker.MakeLetter(def.letterLabel, def.letterText, MouseDisasterDefOf.MouseDisaster_ChildExchangeLetter, group) as ChoiceLetter_ChildExchange;
            if (letter == null)
            {
                MouseDisasterUtility.DestroyFailedIncidentPawns(group);
                return false;
            }

            letter.trader = trader;
            letter.map = map;
            Find.LetterStack.ReceiveLetter(letter, null);
            return true;
        }
    }
}
