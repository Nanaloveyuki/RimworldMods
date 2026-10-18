using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Caravan;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using Verse.AI.Group;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Trade
{
    internal static class TradeEventRouter
    {
        internal static bool TrySpawnTraderCaravan(RHAH_IncidentEntry entry, IncidentParms parms)
        {
            Map map = Core.RHAH_MapResolver.Resolve(parms?.target as Map);
            if (entry == null || map == null)
            {
                return false;
            }

            IntVec3 cell;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal, false, null))
            {
                return false;
            }

            int tick = Core.RHAH_Runtime.NextBatchId(map);
            RHAH_Attitude attitude = HungerAndHavoc.Incidents.RHAH_IncidentArrival.For(entry);
            int count = HungerAndHavoc.Incidents.RHAH_IncidentScale.Count(entry.DisplayId, parms == null ? 0f : parms.points, EventCap(), false);
            RHAH_IncidentContext context = new RHAH_IncidentContext
            {
                DisplayId = entry.DisplayId,
                SpawnBatchId = tick,
                RelationshipGroupId = tick,
                Role = RHAH_PawnRole.Trader,
                Attitude = attitude,
                CarriesPlague = entry.Category == RHAH_IncidentCategory.Plague,
                Map = map,
                SpawnCell = cell,
                PawnCount = count,
                Points = parms == null ? 0f : parms.points
            };
            return HungerAndHavoc.Incidents.RHAH_IncidentFacts.Submit(context);
        }

        internal static bool TrySpawnCaravanAmbush(RHAH_IncidentEntry entry, float points, RimWorld.Planet.Caravan selected, bool allowFallback, Map destination = null)
        {
            RimWorld.Planet.Caravan caravan = destination == null ? CaravanTargetResolver.Resolve(selected, allowFallback) : null;
            RHAH_Attitude attitude = RHAH_Attitude.Hostile;
            Faction faction = RequireAmbushFaction();
            if (entry == null || faction == null || (destination == null && (caravan == null ||
                !RimWorld.Planet.CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(caravan.Tile))))
            {
                return false;
            }

            IntVec3 cell = IntVec3.Invalid;
            if (destination != null && !RCellFinder.TryFindRandomPawnEntryCell(out cell, destination, CellFinder.EdgeRoadChance_Animal, false, null))
            {
                return false;
            }

            List<Verse.Pawn> attackers = new List<Verse.Pawn>();
            int tick = Core.RHAH_Runtime.NextBatchId(destination);
            int count = RHAH_IncidentScale.Count(entry.DisplayId, points, EventCap(), false);
            for (int i = 0; i < count; i++)
            {
                RHAH_PawnCreationResult result = RHAH_PawnFactory.Create(new RHAH_PawnRequest
                {
                    SourceIncidentDisplayId = entry.DisplayId,
                    SpawnBatchId = Core.RHAH_Runtime.NextBatchId(destination, i),
                    RelationshipGroupId = tick,
                    Role = RHAH_PawnRole.Thief,
                    AttitudeAtArrival = attitude,
                    CarriesPlague = entry.Category == RHAH_IncidentCategory.Plague,
                    Map = null,
                    PawnKind = Core.RHAH_DefOf.RHAH_PawnKind_Ratkin,
                    Faction = faction
                });
                if (!result.Succeeded)
                {
                    Cleanup(attackers);
                    return false;
                }

                attackers.Add(result.Pawns[0]);
                Core.RHAH_Runtime.RegisterBatch(destination, RHAH_Api.Get(result.Pawns[0]).SpawnBatchId);
            }

            Map map = destination;
            try
            {
                if (map == null)
                {
                    map = RimWorld.Planet.CaravanIncidentUtility.SetupCaravanAttackMap(caravan, attackers, true);
                }
                else
                {
                    for (int i = 0; i < attackers.Count; i++)
                    {
                        GenSpawn.Spawn(attackers[i], cell, map);
                    }
                }
            }
            catch
            {
                Cleanup(attackers);
                return false;
            }

            if (map == null)
            {
                Cleanup(attackers);
                return false;
            }
            if (destination != null)
            {
                // 直接锁关系不会通知原版缓存 现有地图需重新收录玩家目标
                map.attackTargetsCache.Notify_FactionHostilityChanged(faction, Faction.OfPlayer);
            }

            if (!TryCreateAssaultLord(faction, map, attackers))
            {
                Cleanup(attackers);
                return false;
            }

            EventMgr.RHAH_EventChainEntry.NoteStarted(entry.DisplayId, destination == null ? 0 : map.uniqueID,
                caravan == null ? 0 : caravan.ID, Find.TickManager.TicksGame, tick + 1);
            Find.LetterStack.ReceiveLetter(CreateAmbushLetter(entry, attackers, faction));
            if (destination == null)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            }
            return true;
        }

        internal static ChoiceLetter CreateAmbushLetter(RHAH_IncidentEntry entry, List<Verse.Pawn> attackers, Faction faction)
        {
            return LetterMaker.MakeLetter(entry.LabelKey.Translate(), "RHAH_CaravanAmbush_LetterText".Translate(),
                LetterDefOf.ThreatBig, new LookTargets(attackers), faction);
        }

        internal static Faction RequireAmbushFaction()
        {
            Faction player = Faction.OfPlayer;
            if (player == null)
            {
                return null;
            }

            Faction faction = HungerAndHavoc.Pawn.RHAH_AttitudeFactions.Require(RHAH_Attitude.Hostile);
            if (faction == null)
            {
                return null;
            }

            HungerAndHavoc.Pawn.RHAH_AttitudeFactions.LockGoodwill();
            return faction.HostileTo(player) && player.HostileTo(faction) ? faction : null;
        }

        internal static bool TryCreateAssaultLord(Faction faction, Map map, List<Verse.Pawn> attackers)
        {
            if (faction == null || map == null || attackers == null || attackers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < attackers.Count; i++)
            {
                if (attackers[i] == null || attackers[i].Faction != faction || attackers[i].GetLord() != null)
                {
                    return false;
                }
            }

            Lord lord = LordMaker.MakeNewLord(
                faction,
                new LordJob_AssaultColony(faction, true, false, false, false, true, false, false),
                map,
                attackers);
            if (lord == null || lord.LordJob is not LordJob_AssaultColony || !OwnsAll(lord, attackers))
            {
                Disband(lord);
                return false;
            }

            return true;
        }

        static bool OwnsAll(Lord lord, List<Verse.Pawn> attackers)
        {
            if (lord == null || lord.ownedPawns.Count != attackers.Count)
            {
                return false;
            }

            for (int i = 0; i < attackers.Count; i++)
            {
                if (attackers[i].GetLord() != lord || !lord.ownedPawns.Contains(attackers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        static void Disband(Lord lord)
        {
            if (lord == null)
            {
                return;
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                if (pawn != null && pawn.lord == lord)
                {
                    pawn.lord = null;
                    if (pawn.mindState != null)
                    {
                        pawn.mindState.duty = null;
                    }
                }
            }

            lord.ownedPawns.Clear();

            if (lord.lordManager != null)
            {
                lord.lordManager.lords.Remove(lord);
                lord.lordManager = null;
            }
        }

        static void Cleanup(List<Verse.Pawn> pawns)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                if (pawn == null || pawn.Destroyed)
                {
                    continue;
                }

                Lord lord = pawn.GetLord();
                if (lord != null)
                {
                    lord.Notify_PawnLost(pawn, PawnLostCondition.Vanished, null);
                }

                pawn.Destroy(DestroyMode.Vanish);
            }
        }
        static int EventCap()
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            return settings == null ? RHAH_IncidentScale.DefaultEventPawns : settings.maxEventPawns;
        }
    }
}
