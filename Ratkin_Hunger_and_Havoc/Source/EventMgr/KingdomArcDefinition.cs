using System;
using HungerAndHavoc.Storyteller.Suiyin;
using Oxygendsl.Core.Events;

namespace HungerAndHavoc.EventMgr
{
    internal sealed class KingdomArcDefinition : IEventChainDefinition
    {
        internal const string DefinitionId = "RHAH_KingdomInvitation";
        public string Id => DefinitionId;
        public int Version => 1;

        public void Prepare(EventChainInstance instance)
        {
            if (instance == null || instance.DefinitionId != Id || instance.DefinitionVersion != Version ||
                instance.Stage != 0 || instance.Payload != "" || instance.DeadlineTick != -1)
            {
                throw new FormatException("Invalid kingdom invitation instance.");
            }
        }

        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            return KingdomArcActions.Narrator ? EventChainDecision.Action("notify_invitation", -1)
                : EventChainDecision.Wait(KingdomArcActions.AddTicks(tick, RimWorld.GenDate.TicksPerHour));
        }

        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (actionId != "notify_invitation")
            {
                return EventChainActionResult.Failed("Unknown kingdom invitation action: " + actionId);
            }

            KingdomArcRuntime.Notify(instance.Id);
            return EventChainActionResult.Applied();
        }
    }
}
