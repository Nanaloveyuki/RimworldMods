using HungerAndHavoc.Api;
using Oxygendsl.Core;
using Oxygendsl.Mod.Runtime;
using RimWorld;
using Verse;

namespace Oxygendsl.Rhah
{
    public sealed class RhahBehaviorAdapter : IRHAH_PawnBehavior
    {
        static readonly RhahBehaviorAdapter Instance = new RhahBehaviorAdapter();
        static bool registered;

        public static void Activate()
        {
            if (registered) return;
            RHAH_PawnBehaviors.Register(Instance);
            registered = true;
        }

        public bool? Allows(Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
        {
            if (pawn == null) return null;
            PawnContext context = PawnFacts.Create(pawn);
            context.Facts = new GateFacts(context.Facts, gate.ToString());
            Map map = pawn.MapHeld;
            GameContext game = new GameContext
            {
                Tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame,
                Hour = map == null ? 0d : GenLocalDate.HourFloat(map)
            };
            return NativeRuleRuntime.EvaluateGate(context, gate.ToString(), game);
        }

        public bool? ShouldReleaseToColony(Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
        {
            return null;
        }

        sealed class GateFacts : IOxygenHostValue
        {
            readonly string gate;
            readonly IOxygenHostValue facts;
            public GateFacts(IOxygenHostValue facts, string gate) { this.facts = facts; this.gate = gate; }

            public bool TryGetMember(string name, out object value)
            {
                if (name == "gate")
                {
                    value = gate;
                    return true;
                }
                return facts.TryGetMember(name, out value);
            }
        }
    }
}
