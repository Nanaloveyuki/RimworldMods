using System;
using System.Globalization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal sealed class RHAH_BackgroundFollow : IEventChainDefinition
    {
        internal const string CivilWarId = "RHAH_BackgroundCivilWar";
        internal const string TakeoverId = "RHAH_BackgroundTakeover";

        internal RHAH_BackgroundFollow(string id) { Id = id; }
        public string Id { get; }
        public int Version => 1;

        public void Prepare(EventChainInstance instance)
        {
            if (instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.Stage != 0 || instance.SiteId <= 0)
                throw new FormatException("Invalid RHAH background follow instance.");
            if (instance.End != EventChainEnd.None) return;
            if (!int.TryParse(instance.Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out int claimantId) ||
                (Id == TakeoverId ? claimantId <= 0 : claimantId != 0))
                throw new FormatException("Invalid RHAH background follow claimant.");
            instance.PreparedState = claimantId;
        }

        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            return Enabled(RHAH_Mod.Settings?.background, Id)
                ? EventChainDecision.Action("resolve_background", -1)
                : EventChainDecision.Finish(EventChainEnd.Cancelled);
        }

        internal static bool Enabled(RHAH_BackgroundOptions options, string id)
        {
            return options?.enabled == true && (id == CivilWarId ? options.civilWarEnabled : options.takeoverEnabled);
        }

        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (actionId != "resolve_background" || !(instance.PreparedState is int claimantId))
                return EventChainActionResult.Failed("Background follow is not prepared or has an unknown action.");
            RHAH_BackgroundOptions options = RHAH_Mod.Settings?.background;
            if (!Enabled(options, Id)) return EventChainActionResult.Applied("disabled", EventChainEnd.Cancelled);
            Faction kingdom = RHAH_ChoiceRuntime.FindFaction(instance.SiteId);
            if (!RHAH_BackgroundStories.LivingFaction(kingdom) || kingdom.def.defName != RHAH_BackgroundStories.KingdomDef)
                return EventChainActionResult.Applied("kingdom_unavailable", EventChainEnd.Completed);

            if (Id == CivilWarId)
            {
                if (!Rand.Chance(options.civilWarChancePercent / 100f))
                    return EventChainActionResult.Applied("no_civil_war", EventChainEnd.Completed);
                if (options.takeoverEnabled)
                {
                    ScheduleClaimant(kingdom, RHAH_WarlordCompat.UndergroundPackage, RHAH_WarlordCompat.UndergroundFaction, tick, options);
                    ScheduleClaimant(kingdom, RHAH_WarlordCompat.WarlordPackage, RHAH_BackgroundStories.WarlordDef, tick, options);
                }
                RHAH_BackgroundStories.SendLetter(RHAH_BackgroundStory.CivilWar, kingdom);
                return EventChainActionResult.Applied("civil_war", EventChainEnd.Completed);
            }

            Faction claimant = RHAH_ChoiceRuntime.FindFaction(claimantId);
            bool active = claimant?.def?.defName == RHAH_WarlordCompat.UndergroundFaction
                ? ModsConfig.IsActive(RHAH_WarlordCompat.UndergroundPackage)
                : claimant?.def?.defName == RHAH_BackgroundStories.WarlordDef && ModsConfig.IsActive(RHAH_WarlordCompat.WarlordPackage);
            bool captured = active && Rand.Chance(options.takeoverChancePercent / 100f) &&
                RHAH_BackgroundStories.TryTakeSettlement(kingdom, claimant);
            return EventChainActionResult.Applied(captured ? "settlement_captured" : "no_takeover", EventChainEnd.Completed);
        }

        internal static void StartCivilWar(Faction kingdom, int tick, float delayDays)
        {
            Start(CivilWarId, kingdom.loadID, 0, tick, delayDays);
        }

        static void ScheduleClaimant(Faction kingdom, string packageId, string factionDef, int tick, RHAH_BackgroundOptions options)
        {
            if (!ModsConfig.IsActive(packageId)) return;
            Faction claimant = RHAH_BackgroundStories.FindFaction(factionDef);
            if (claimant != null) Start(TakeoverId, kingdom.loadID, claimant.loadID, tick, options.takeoverDelayDays);
        }

        static void Start(string definition, int kingdomId, int claimantId, int tick, float delayDays)
        {
            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            if (runtime == null) throw new InvalidOperationException("ODF background follow queue is not ready.");
            int id = runtime.Start(RHAH_EventChainProvider.ProviderKey, definition, kingdomId, -1, tick, -1,
                claimantId.ToString(CultureInfo.InvariantCulture), 0, RHAH_BackgroundStories.DueTick(tick, delayDays));
            if (id <= 0) throw new InvalidOperationException("ODF rejected the RHAH background follow.");
        }
    }
}
