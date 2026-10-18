using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class RHAH_BroadcastMenu : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            Building_CommsConsole console = clickedThing as Building_CommsConsole;
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (console == null || settings == null || !settings.broadcastEnabled || !RHAH_Runtime.AllowsNewContent ||
                !console.Spawned || !console.Map.IsPlayerHome ||
                !RHAH_ApproachRules.AllowsHome(RHAH_Approach.SpaceHome(console.Map), RHAH_Approach.SpaceApproachEnabled()))
            {
                return null;
            }

            Verse.Pawn actor = context.FirstSelectedPawn;
            if (actor == null || actor.Faction != Faction.OfPlayer || actor.Dead)
            {
                return null;
            }

            if (!actor.CanReach(console, PathEndMode.InteractionCell, Danger.Some))
            {
                return new FloatMenuOption("CannotUseNoPath".Translate(), null);
            }
            if (!console.CanUseCommsNow)
            {
                return new FloatMenuOption("CannotUseNoPower".Translate(), null);
            }
            if (!actor.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                return new FloatMenuOption("CannotUseReason".Translate(
                    "IncapableOfCapacity".Translate(PawnCapacityDefOf.Talking.label, actor.Named("PAWN"))), null);
            }
            if (!actor.CanReserve(console))
            {
                return new FloatMenuOption("RHAH_Broadcast_Unavailable".Translate(), null);
            }

            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            int tick = Find.TickManager.TicksGame;
            if (game == null || !RHAH_BroadcastRules.Ready(tick, game.BroadcastCooldownUntilTick))
            {
                return new FloatMenuOption("RHAH_Broadcast_Cooldown".Translate(), null);
            }

            List<string> candidates = RHAH_BroadcastRules.Candidates(RHAH_IncidentCatalog.All, Disabled(settings));
            RemoveUnavailable(candidates, game);
            if (candidates.Count == 0)
            {
                return new FloatMenuOption("RHAH_Broadcast_Empty".Translate(), null);
            }

            return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption("RHAH_Broadcast_Label".Translate(), () =>
                    actor.jobs.TryTakeOrderedJob(JobMaker.MakeJob(RHAH_DefOf.RHAH_BroadcastHope, console), JobTag.Misc)),
                actor, console);
        }

        internal static bool Complete(Building_CommsConsole console)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            if (!RHAH_Runtime.AllowsNewContent || settings == null || !settings.broadcastEnabled ||
                game == null || console == null || !console.Spawned || !console.CanUseCommsNow || !console.Map.IsPlayerHome ||
                !RHAH_ApproachRules.AllowsHome(RHAH_Approach.SpaceHome(console.Map), RHAH_Approach.SpaceApproachEnabled()))
            {
                return false;
            }
            int tick = Find.TickManager.TicksGame;
            if (!RHAH_BroadcastRules.Ready(tick, game.BroadcastCooldownUntilTick))
            {
                return false;
            }
            List<string> candidates = RHAH_BroadcastRules.Candidates(RHAH_IncidentCatalog.All, Disabled(settings));
            RemoveUnavailable(candidates, game);
            bool famine = GameComponent_RHAH_FamineYear.Active;
            int desired = RHAH_BroadcastRules.ResponseCount(famine, settings.famineYearPawnMultiplier);
            int targetId = RHAH_IncidentSchedule.TargetId(false, console.Map.uniqueID);
            int queued = QueueResponses(game, targetId, candidates, desired);
            if (queued > 0)
            {
                game.BroadcastCooldownUntilTick = RHAH_BroadcastRules.NextCooldown(tick, settings.broadcastCooldownDays);
                if (HungerAndHavoc.Storyteller.Suiyin.RHAH_EndingRuntime.CountsNow())
                {
                    Current.Game?.GetComponent<HungerAndHavoc.Narrative.NarrativeState>()?.NoteBroadcast(tick);
                }

                string key = famine ? "RHAH_Broadcast_FamineQueued" : "RHAH_Broadcast_Queued";
                Messages.Message(key.Translate(queued), console, MessageTypeDefOf.NeutralEvent);
                return true;
            }
            return false;
        }

        internal static int QueueResponses(GameComponent_RHAH_Game game, int targetId, List<string> candidates, int count)
        {
            if (game == null || candidates == null || count <= 0)
            {
                return 0;
            }

            int queued = 0;
            while (queued < count && candidates.Count > 0)
            {
                int groups = RHAH_BroadcastRules.GroupCount(candidates);
                string displayId = RHAH_BroadcastRules.Pick(candidates, groups == 0 ? 0f : Rand.Range(0f, groups));
                if (displayId == null)
                {
                    break;
                }

                candidates.Remove(displayId);
                if (!Pending(game, displayId) && game.QueueIncident(displayId, GameComponent_RHAH_Game.PointsFor(displayId), targetId))
                {
                    queued++;
                }
            }

            return queued;
        }

        static void RemoveUnavailable(List<string> candidates, GameComponent_RHAH_Game game)
        {
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                string displayId = candidates[i];
                if (!GameComponent_RHAH_FamineYear.Available(displayId) || Pending(game, displayId))
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        static bool Pending(GameComponent_RHAH_Game game, string displayId)
        {
            IReadOnlyList<string> pending = game.PendingIncidentDisplayIds;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i] == displayId)
                {
                    return true;
                }
            }

            return false;
        }

        static HashSet<string> Disabled(RHAH_Settings settings)
        {
            HashSet<string> disabled = new HashSet<string>();
            IReadOnlyList<RHAH_IncidentEntry> entries = RHAH_IncidentCatalog.All;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!settings.IsIncidentEnabled(entries[i].DisplayId))
                {
                    disabled.Add(entries[i].DisplayId);
                }
            }

            return disabled;
        }
    }
}
