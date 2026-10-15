using System;
using HungerAndHavoc.Storyteller.Suiyin;
using Oxygendsl.Core.Events;

namespace HungerAndHavoc.EventMgr
{
    internal sealed class KingdomCollaborationDefinition : IEventChainDefinition
    {
        internal const string DefinitionId = "RHAH_KingdomCollaboration";
        public string Id => DefinitionId;
        public int Version => 1;
        public void Prepare(EventChainInstance instance)
        {
            if (instance == null || instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.Payload != "" || instance.Stage != 0 || instance.DeadlineTick != -1)
                throw new FormatException("Invalid kingdom collaboration instance.");
        }
        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            KingdomArcProgress progress = KingdomArcRuntime.State?.Progress;
            if (progress == null || progress.ChainId != instance.Id) return EventChainDecision.Wait(-1);
            if (progress.Stage == KingdomStage.Complete) return EventChainDecision.Finish(EventChainEnd.Completed);
            if (!KingdomArcActions.NeedsClock(progress)) return EventChainDecision.Wait(-1);
            return EventChainDecision.Action("advance", KingdomArcActions.AddTicks(tick, RimWorld.GenDate.TicksPerHour));
        }
        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (actionId != "advance") return EventChainActionResult.Failed("Unknown kingdom collaboration action.");
            KingdomArcActions.Advance(tick);
            return EventChainActionResult.Applied();
        }
    }
}
