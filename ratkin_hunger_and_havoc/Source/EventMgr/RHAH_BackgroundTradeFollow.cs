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
    internal sealed class RHAH_BackgroundTradeFollow : IEventChainDefinition
    {
        internal const string DefinitionId = "RHAH_BackgroundTrade";
        public string Id => DefinitionId;
        public int Version => 1;

        public void Prepare(EventChainInstance instance)
        {
            if (instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.SiteId <= 0 ||
                instance.Stage < 0 || instance.Stage > 1 ||
                !int.TryParse(instance.Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ||
                !IsTradeStory((RHAH_BackgroundStory)value))
                throw new FormatException("Invalid RHAH background trade instance.");
            instance.PreparedState = (RHAH_BackgroundStory)value;
        }

        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            return instance.Stage == 0 ? EventChainDecision.Wait(-1) : EventChainDecision.Action("trade_followup", -1);
        }

        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (actionId != "trade_followup" || instance.Stage != 1 || !(instance.PreparedState is RHAH_BackgroundStory story))
                return EventChainActionResult.Failed("Unknown background trade action.");
            Faction kingdom = RHAH_ChoiceRuntime.FindFaction(instance.SiteId);
            if (RHAH_BackgroundStories.LivingFaction(kingdom))
            {
                string prefix = "RHAH_Background_" + story + "_Follow";
                Find.LetterStack.ReceiveLetter((prefix + "Label").Translate(), (prefix + "Text").Translate(kingdom.Name),
                    LetterDefOf.NeutralEvent, null, kingdom);
            }
            return EventChainActionResult.Applied("trade_followup_sent", EventChainEnd.Completed);
        }

        internal static bool IsTradeStory(RHAH_BackgroundStory story)
        {
            return story == RHAH_BackgroundStory.HarvestFestival || story == RHAH_BackgroundStory.AidManifest ||
                story == RHAH_BackgroundStory.CancelledBanquet;
        }

        internal static bool Enabled(RHAH_BackgroundStory story)
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings?.background;
            if (!RHAH_Runtime.AllowsNewContent || options?.enabled != true) return false;
            switch (story)
            {
                case RHAH_BackgroundStory.HarvestFestival: return options.harvestFestivalEnabled;
                case RHAH_BackgroundStory.AidManifest: return options.aidManifestEnabled;
                case RHAH_BackgroundStory.CancelledBanquet: return options.cancelledBanquetEnabled;
                default: return false;
            }
        }

        internal static EventChainInstance Pending(int factionId, bool weapons)
        {
            EventChainProviderState state = OxygenEventChainHost.Runtime?.GetProviderState(RHAH_EventChainProvider.ProviderKey);
            if (state == null || state.Mode != EventChainMode.Active || state.Reason.Length != 0) return null;
            for (int i = 0; i < state.Instances.Count; i++)
            {
                EventChainInstance instance = state.Instances[i];
                if (instance.DefinitionId != DefinitionId || instance.SiteId != factionId || instance.Stage != 0 ||
                    instance.End != EventChainEnd.None || instance.PauseReason.Length != 0 ||
                    !(instance.PreparedState is RHAH_BackgroundStory story) || !Enabled(story)) continue;
                if ((story == RHAH_BackgroundStory.AidManifest) == weapons) return instance;
            }
            return null;
        }

        internal static bool Start(RHAH_BackgroundStory story, Faction kingdom, int tick)
        {
            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            EventChainProviderState state = runtime?.GetProviderState(RHAH_EventChainProvider.ProviderKey);
            if (state == null) return false;
            for (int i = 0; i < state.Instances.Count; i++)
            {
                EventChainInstance existing = state.Instances[i];
                if (existing.DefinitionId == DefinitionId && existing.SiteId == kingdom.loadID &&
                    existing.End == EventChainEnd.None && existing.PreparedState is RHAH_BackgroundStory active && active == story) return false;
            }
            return runtime.Start(RHAH_EventChainProvider.ProviderKey, DefinitionId, kingdom.loadID, -1, tick, -1,
                ((int)story).ToString(CultureInfo.InvariantCulture)) > 0;
        }

        internal static bool Consume(int instanceId, int tick)
        {
            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            EventChainInstance instance = runtime?.Find(RHAH_EventChainProvider.ProviderKey, instanceId);
            return instance != null && instance.Stage == 0 && runtime.SetStage(RHAH_EventChainProvider.ProviderKey,
                instanceId, 1, -1, instance.Payload, RHAH_BackgroundStories.DueTick(tick, 0f));
        }
    }
}
