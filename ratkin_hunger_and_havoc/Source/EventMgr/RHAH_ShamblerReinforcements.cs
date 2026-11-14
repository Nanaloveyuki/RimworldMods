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
    internal sealed class RHAH_ShamblerReinforcements : IEventChainDefinition
    {
        internal const string DefinitionId = "RHAH_ShamblerReinforcements";
        public string Id => DefinitionId;
        public int Version => 1;

        internal static int DueTick(int tick)
        {
            long due = (long)tick + 3 * GenDate.TicksPerDay;
            return due > int.MaxValue ? -1 : (int)due;
        }

        internal static int Schedule(Map map, int count, int tick)
        {
            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            int due = DueTick(tick);
            return runtime == null || due < 0 ? 0 : runtime.Start(RHAH_EventChainProvider.ProviderKey,
                DefinitionId, map.uniqueID, RHAH_EventChainMigration.SiteTile(map.uniqueID), tick, -1,
                count.ToString(CultureInfo.InvariantCulture), 0, due);
        }

        public void Prepare(EventChainInstance instance)
        {
            if (instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.SiteId < 0 || instance.Stage != 0)
                throw new FormatException("Invalid ratkin shambler reinforcement instance.");
            if (instance.End != EventChainEnd.None) return;
            if (!int.TryParse(instance.Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count < 1 || count > 200)
                throw new FormatException("Invalid ratkin shambler reinforcement count.");
            instance.PreparedState = count;
        }

        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            return tick < instance.NextTick ? EventChainDecision.Wait(instance.NextTick)
                : EventChainDecision.Action("check_shamblers", -1);
        }

        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (actionId != "check_shamblers" || !(instance.PreparedState is int count))
                return EventChainActionResult.Failed("Ratkin shambler reinforcement check is not prepared or has an unknown action.");
            if (!ModsConfig.AnomalyActive || !RHAH_Runtime.AllowsNewContent ||
                RHAH_Mod.Settings?.IsIncidentEnabled(RHAH_Shamblers.HordeId) == false)
                return EventChainActionResult.Applied("disabled", EventChainEnd.Cancelled);
            Map map = RHAH_MapResolver.MapById(instance.SiteId);
            bool spawned = map != null && RHAH_Shamblers.Reinforce(map, count);
            return EventChainActionResult.Applied(spawned ? "reinforced" : "no_reinforcements", EventChainEnd.Completed);
        }
    }
}
