using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static bool IsDefenselessTailTarget(Pawn pawn)
        {
            return pawn != null &&
                   (pawn.Downed ||
                    !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving) ||
                    !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                    !pawn.health.capacities.CanBeAwake);
        }

        public static bool CanBeTailBiteTarget(Pawn target, Pawn actor)
        {
            return target != null &&
                   target != actor &&
                   target.Spawned &&
                   !target.Dead &&
                   target.IsPrisonerOfColony &&
                   IsMouseEggBaby(target) &&
                   CanReceiveTailBiteNow(target) &&
                   GetNaturalRatTail(target) != null &&
                   (!target.Awake() || IsDefenselessTailTarget(target));
        }

        private static bool CanReceiveTailBiteNow(Pawn target)
        {
            if (target == null)
            {
                return false;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            return !TailBiteLastVictimTickByPawnId.TryGetValue(target.thingIDNumber, out int lastTick) || nowTick - lastTick >= TailBiteVictimCooldownTicks;
        }

        public static Pawn FindTailBiteTarget(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                30f,
                thing => thing is Pawn target && CanBeTailBiteTarget(target, pawn) && pawn.CanReserve(thing)) as Pawn;
        }

        public static bool CanAttemptTailBite(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            return !TailBiteLastAttemptTickByPawnId.TryGetValue(pawn.thingIDNumber, out int lastTick) || nowTick - lastTick >= TailBiteAttemptCooldownTicks;
        }

        public static void RecordTailBiteAttempt(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            TailBiteLastAttemptTickByPawnId[pawn.thingIDNumber] = Find.TickManager?.TicksGame ?? 0;
        }

        public static void RecordTailBiteVictim(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            TailBiteLastVictimTickByPawnId[pawn.thingIDNumber] = Find.TickManager?.TicksGame ?? 0;
        }

        public static void NotifyTailBiteOffEvent(Pawn biter, Pawn target)
        {
            if (biter == null || target == null)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (TailBiteLastNotifyTickByPawnId.TryGetValue(target.thingIDNumber, out int lastNotifyTick) && nowTick - lastNotifyTick < TailBiteNotifyCooldownTicks)
            {
                return;
            }

            TailBiteLastNotifyTickByPawnId[target.thingIDNumber] = nowTick;

            string text = "MouseDisaster_TailBite_Event".Translate(target.Named("TARGET"), biter.Named("BITER")).Resolve();
            Messages.Message(text, target, MessageTypeDefOf.NegativeEvent, historical: true);
            if (IsPrisonerScavengeDebugLogEnabled)
            {
                Log.Message("[MouseDisaster][TailBite] " + text);
            }
        }

        public static void TryGainTailBittenHatredMemory(Pawn target, Pawn offender)
        {
            if (target?.needs?.mood?.thoughts?.memories == null || offender == null || MouseDisasterDefOf.MouseDisaster_TailBittenHatred == null)
            {
                return;
            }

            target.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(MouseDisasterDefOf.MouseDisaster_TailBittenHatred, offender);
            target.needs.mood.thoughts.memories.TryGainMemory(MouseDisasterDefOf.MouseDisaster_TailBittenHatred, offender);
        }
    }
}
