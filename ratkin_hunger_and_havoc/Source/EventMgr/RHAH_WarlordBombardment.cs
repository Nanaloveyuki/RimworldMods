using System;
using System.Collections.Generic;
using System.Globalization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core.Events;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal sealed class RHAH_WarlordBombardment : IEventChainDefinition
    {
        internal const string AttackedKey = "rhah:warlordAttacked";
        internal const string ShellsKey = "rhah:warlordShells";
        internal const string TargetKey = "rhah:warlordBombardmentTarget";
        internal const string PenaltyKey = "rhah:warlordDeathCounted";
        internal const int Delay = 12 * GenDate.TicksPerHour;
        const int ExplosionSettleTicks = 120;

        public string Id => RHAH_WarlordCompat.SuicideSquad;
        public int Version => 1;

        public void Prepare(EventChainInstance instance)
        {
            if (instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.SiteId < 0 ||
                instance.Stage < 0 || instance.Stage > 1)
                throw new FormatException("Invalid RHAH warlord bombardment instance.");
            if (instance.End != EventChainEnd.None) return;
            if (!RHAH_EventFollowRules.ReadPayload(instance.Payload, out string displayId, out int batchId, out int mapId) ||
                displayId != Id || mapId != instance.SiteId)
                throw new FormatException("Invalid RHAH warlord bombardment payload.");
            instance.PreparedState = batchId;
        }

        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            if (Core.RHAH_Mod.Settings?.IsIncidentEnabled(Id) == false)
                return EventChainDecision.Finish(EventChainEnd.Cancelled);
            return instance.Stage == 0
                ? EventChainDecision.Action("bombard_children", NextTick(tick, ExplosionSettleTicks), 1)
                : EventChainDecision.Action("finish_bombardment", -1);
        }

        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (Core.RHAH_Mod.Settings?.IsIncidentEnabled(Id) == false)
                return EventChainActionResult.Applied("content_disabled", EventChainEnd.Cancelled);
            if (!(instance.PreparedState is int batchId))
                return EventChainActionResult.Failed("Warlord bombardment is not prepared.");
            Map map = RHAH_ChoiceRuntime.FindMap(instance.SiteId);
            if (map == null) return EventChainActionResult.Applied("map_unavailable", EventChainEnd.Completed);
            List<Verse.Pawn> children = Children(batchId, map);
            if (instance.Stage == 0 && actionId == "bombard_children")
            {
                return Bombard(map, children);
            }
            if (instance.Stage == 1 && actionId == "finish_bombardment")
            {
                for (int i = 0; i < children.Count; i++)
                {
                    Verse.Pawn child = children[i];
                    if (child.Dead || !child.Spawned || child.Downed || child.IsPrisoner || child.IsSlave ||
                        child.Faction == Faction.OfPlayer || !RHAH_Api.TryGetExtra(child, TargetKey, out _)) continue;
                    HealthUtility.DamageUntilDowned(child, true, DamageDefOf.Bomb,
                        DefDatabase<ThingDef>.GetNamed("Shell_HighExplosive"));
                }
                return EventChainActionResult.Applied("bombardment_finished", EventChainEnd.Completed);
            }
            return EventChainActionResult.Failed("Unknown warlord bombardment action: " + actionId);
        }

        internal static int NextTick(int tick, int delay)
        {
            return (int)Math.Min(int.MaxValue, (long)tick + delay);
        }

        internal static bool Eligible(bool alive, bool onOriginalMap, bool protectedStatus, bool attackedByPlayer)
        {
            return alive && onOriginalMap && !protectedStatus && !attackedByPlayer;
        }

        internal static void NotePlayerAttack(Verse.Pawn child)
        {
            if (RHAH_Api.Get(child)?.SourceIncidentDisplayId == RHAH_WarlordCompat.SuicideSquad)
                RHAH_Api.SetExtra(child, AttackedKey, "1");
        }

        static List<Verse.Pawn> Children(int batchId, Map map)
        {
            List<Verse.Pawn> result = new List<Verse.Pawn>();
            List<Verse.Pawn> pawns = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
                if (pawn.MapHeld == map && snapshot?.SourceIncidentDisplayId == RHAH_WarlordCompat.SuicideSquad &&
                    snapshot.SpawnBatchId == batchId) result.Add(pawn);
            }
            return result;
        }

        static EventChainActionResult Bombard(Map map, List<Verse.Pawn> children)
        {
            ThingDef shellDef = DefDatabase<ThingDef>.GetNamed("Shell_HighExplosive");
            if (shellDef.GetCompProperties<CompProperties_Explosive>() == null)
                return EventChainActionResult.Failed("High-explosive shell has no explosive component.");
            List<Thing> shells = new List<Thing>();
            List<Verse.Pawn> targets = new List<Verse.Pawn>();
            for (int i = 0; i < children.Count; i++)
            {
                Verse.Pawn child = children[i];
                bool protectedStatus = child.IsPrisoner || child.IsSlave || child.Faction == Faction.OfPlayer ||
                    RHAH_Api.Get(child)?.Lifecycle == RHAH_Lifecycle.Released;
                if (!Eligible(!child.Dead, child.SpawnedOrAnyParentSpawned && child.MapHeld == map, protectedStatus,
                    RHAH_Api.TryGetExtra(child, AttackedKey, out _))) continue;
                Thing shell = ThingMaker.MakeThing(shellDef);
                shells.Add(shell);
                targets.Add(child);
            }
            if (targets.Count == 0) return EventChainActionResult.Applied("no_targets", EventChainEnd.Completed);

            string[] ids = new string[shells.Count];
            for (int i = 0; i < shells.Count; i++) ids[i] = shells[i].thingIDNumber.ToString(CultureInfo.InvariantCulture);
            string shellIds = "|" + string.Join("|", ids) + "|";
            for (int i = 0; i < children.Count; i++)
                if (!children[i].Dead) RHAH_Api.SetExtra(children[i], ShellsKey, shellIds);
            for (int i = 0; i < shells.Count; i++)
            {
                GenSpawn.Spawn(shells[i], targets[i].PositionHeld, map);
                RHAH_Api.SetExtra(targets[i], TargetKey, "1");
                CompExplosive explosive = shells[i].TryGetComp<CompExplosive>();
                explosive.StartWick();
                explosive.wickTicksLeft = 1;
            }
            Find.LetterStack.ReceiveLetter("RHAH_Warlord_BombardmentLabel".Translate(),
                "RHAH_Warlord_BombardmentText".Translate(), LetterDefOf.NegativeEvent, new LookTargets(targets));
            return EventChainActionResult.Applied("shells_armed");
        }

        internal static void NotifyDeath(Verse.Pawn child, DamageInfo? damage)
        {
            Thing instigator = damage?.Instigator;
            if (!child.Dead || instigator == null || damage?.Def != DamageDefOf.Bomb ||
                RHAH_Api.Get(child)?.SourceIncidentDisplayId != RHAH_WarlordCompat.SuicideSquad ||
                !RHAH_Api.TryGetExtra(child, ShellsKey, out string shells) ||
                shells.IndexOf("|" + instigator.thingIDNumber.ToString(CultureInfo.InvariantCulture) + "|", StringComparison.Ordinal) < 0 ||
                RHAH_Api.TryGetExtra(child, PenaltyKey, out _)) return;
            RHAH_Api.SetExtra(child, PenaltyKey, "1");
            Map map = child.MapHeld;
            if (map == null || Find.WorldObjects == null) return;
            HashSet<Faction> affected = new HashSet<Faction>();
            List<Settlement> settlements = Find.WorldObjects.Settlements;
            for (int i = 0; i < settlements.Count; i++)
            {
                Settlement settlement = settlements[i];
                Faction faction = settlement.Faction;
                if (settlement.Destroyed || faction == null || faction.IsPlayer || affected.Contains(faction) ||
                    settlement.Tile.Layer != map.Tile.Layer ||
                    Find.WorldGrid.ApproxDistanceInTiles(map.Tile, settlement.Tile) > 20f) continue;
                affected.Add(faction);
                faction.TryAffectGoodwillWith(Faction.OfPlayer, -1, canSendMessage: false);
            }
        }
    }
}
