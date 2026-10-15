using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_DropChild : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        internal static Job TryCreate(Verse.Pawn pawn)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (comp == null || pawn.Map == null || settings == null || !RHAH_BatchAttitude.CanOrderLeave(pawn) ||
                pawn.Downed || (pawn.stances?.stunner?.Stunned ?? false) || RHAH_DefOf.RHAH_DropChild == null)
            {
                return null;
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.DropOffChild))
            {
                return null;
            }

            bool leaving = comp.State.lifecycle == RHAH_Lifecycle.Leaving;
            if (!RHAH_FamilyRules.CanDrop(comp.State.role, leaving, settings.familyDropEnabled))
            {
                return null;
            }

            if (RHAH_FamilyRules.AllDropped(comp.State.childPawnLoadIds, comp.State.droppedChildLoadIds))
            {
                return null;
            }

            Lord lord = pawn.GetLord();
            for (int i = 0; i < comp.State.childPawnLoadIds.Count; i++)
            {
                int childId = comp.State.childPawnLoadIds[i];
                if (comp.State.droppedChildLoadIds.Contains(childId))
                {
                    continue;
                }

                Verse.Pawn child = FindLinked(pawn, lord, childId);
                if (!RHAH_FamilyRules.StillCarried(pawn, child))
                {
                    continue;
                }

                return JobMaker.MakeJob(RHAH_DefOf.RHAH_DropChild, child);
            }

            return null;
        }

        internal static Verse.Pawn FindLinked(Verse.Pawn pawn, Lord lord, int loadId)
        {
            if (lord?.ownedPawns != null)
            {
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Verse.Pawn member = lord.ownedPawns[i];
                    if (member != null && member.thingIDNumber == loadId)
                    {
                        return member;
                    }
                }
            }

            Verse.Pawn carried = pawn.carryTracker?.CarriedThing as Verse.Pawn;
            return carried != null && carried.thingIDNumber == loadId ? carried : null;
        }
    }

    public class JobDriver_RHAH_DropChild : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            Toil drop = ToilMaker.MakeToil("DropChild");
            drop.initAction = () =>
            {
                Verse.Pawn child = job.GetTarget(TargetIndex.A).Pawn;
                if (!RHAH_FamilyRules.StillCarried(pawn, child))
                {
                    return;
                }

                if (!pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped) ||
                    dropped != child || child.CarriedBy != null || !child.Spawned)
                {
                    return;
                }

                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
                if (comp != null)
                {
                    RHAH_FamilyRules.MarkDropped(comp.State.droppedChildLoadIds, child.thingIDNumber);
                }
                child.GetLord()?.RemovePawn(child);
            };
            drop.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return drop;
        }
    }

    public class JobGiver_RHAH_MotherFeed : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        internal static Job TryCreate(Verse.Pawn pawn)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (comp == null || pawn.Map == null || settings == null || !RHAH_Api.IsVisitor(pawn))
            {
                return null;
            }

            Thing food = CarriedFood(pawn);
            if (food == null)
            {
                return null;
            }

            Lord lord = pawn.GetLord();
            for (int i = 0; i < comp.State.childPawnLoadIds.Count; i++)
            {
                Verse.Pawn child = JobGiver_RHAH_DropChild.FindLinked(pawn, lord, comp.State.childPawnLoadIds[i]);
                if (child == null || child.Map != pawn.Map)
                {
                    continue;
                }

                float childHungry = settings == null ? RHAH_FamilyRules.ChildHungry : settings.childHungryPercent / 100f;
                bool hungry = child.needs?.food != null && child.needs.food.CurLevelPercentage < childHungry;
                if (!RHAH_FamilyRules.CanMotherFeed(comp.State.role, settings.motherFeedEnabled, hungry))
                {
                    continue;
                }

                if (!RHAH_FamilyRules.CanGiveFood(true, child.RaceProps != null && child.RaceProps.CanEverEat(food)))
                {
                    continue;
                }

                if (!pawn.CanReach(child, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                return JobMaker.MakeJob(RHAH_DefOf.RHAH_MotherFeed, child, food);
            }

            return null;
        }

        static Thing CarriedFood(Verse.Pawn pawn)
        {
            ThingOwner container = pawn.inventory?.innerContainer;
            if (container == null)
            {
                return null;
            }

            for (int i = 0; i < container.Count; i++)
            {
                Thing thing = container[i];
                if (thing != null && thing.IngestibleNow)
                {
                    return thing;
                }
            }

            return null;
        }
    }

    public class JobDriver_RHAH_MotherFeed : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil feed = ToilMaker.MakeToil("MotherFeed");
            feed.initAction = () =>
            {
                Verse.Pawn child = job.GetTarget(TargetIndex.A).Pawn;
                Thing food = job.GetTarget(TargetIndex.B).Thing;
                if (child == null || food == null || food.Destroyed)
                {
                    return;
                }

                Thing given = food.stackCount > 1 ? food.SplitOff(1) : food;
                if (child.inventory != null && child.inventory.innerContainer.TryAdd(given))
                {
                    return;
                }

                GenPlace.TryPlaceThing(given, child.Position, child.Map, ThingPlaceMode.Near);
            };
            feed.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return feed;
        }
    }
    public class JobGiver_RHAH_Scavenge : ThinkNode_JobGiver
    {
        const float Radius = 12f;

        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        internal static Job TryCreate(Verse.Pawn pawn)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            float prisonerHungry = settings == null ? RHAH_FamilyRules.PrisonerHungry : settings.prisonerHungryPercent / 100f;
            bool hungry = pawn != null && pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < prisonerHungry;
            if (pawn == null || pawn.Map == null || settings == null || !RHAH_Api.IsOrigin(pawn) ||
                !RHAH_FamilyRules.CanScavenge(settings.prisonerScavengeEnabled, pawn.IsPrisoner, hungry))
            {
                return null;
            }

            MapComponent_RHAH_Map mapState = pawn.Map.GetComponent<MapComponent_RHAH_Map>();
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (mapState != null && !mapState.FoodSearchReady(pawn.thingIDNumber, now))
            {
                return null;
            }

            Filth filth = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Filth),
                PathEndMode.OnCell,
                TraverseParms.For(pawn),
                Radius,
                thing => RHAH_FamilyRules.ScavengeFilth(thing.Spawned, true, pawn.CanReserve(thing))) as Filth;
            if (filth == null)
            {
                mapState?.SetFoodSearchTick(pawn.thingIDNumber, now + RHAH_ReliefFood.RetryBaseTicks);
                return null;
            }

            return JobMaker.MakeJob(RHAH_DefOf.RHAH_Scavenge, filth);
        }
    }

    public class JobDriver_RHAH_Scavenge : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);
            yield return Toils_General.Wait(RHAH_FamilyRules.WorkTicks);
            Toil finish = ToilMaker.MakeToil("Scavenge");
            finish.initAction = () =>
            {
                Filth filth = job.GetTarget(TargetIndex.A).Thing as Filth;
                filth?.ThinFilth();
                Need_Food food = pawn.needs?.food;
                if (food != null)
                {
                    food.CurLevel += RHAH_Mod.Settings == null ? RHAH_FamilyRules.ScavengeNutrition : RHAH_Mod.Settings.scavengeNutrition;
                }

                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(RHAH_DefOf.RHAH_Thought_ScavengedFilth);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    public class JobGiver_RHAH_TailBite : ThinkNode_JobGiver
    {
        const float Radius = 12f;

        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        internal static Job TryCreate(Verse.Pawn pawn)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (pawn == null || pawn.Map == null || settings == null)
            {
                return null;
            }

            RHAH_Settings biteSettings = RHAH_Mod.Settings;
            float biteHungry = biteSettings == null ? RHAH_FamilyRules.PrisonerHungry : biteSettings.prisonerHungryPercent / 100f;
            bool hungry = pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < biteHungry;
            if (!RHAH_Api.IsOrigin(pawn) || !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.TailBite))
            {
                return null;
            }

            if (!RHAH_FamilyRules.CanTailBite(settings.tailBiteEnabled, pawn.IsPrisoner, hungry, true, 0f))
            {
                return null;
            }

            MapComponent_RHAH_Map mapState = pawn.Map.GetComponent<MapComponent_RHAH_Map>();
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (mapState != null && !mapState.FoodSearchReady(pawn.thingIDNumber, now))
            {
                return null;
            }

            Verse.Pawn target = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                Radius,
                thing => Biteable(pawn, thing as Verse.Pawn)) as Verse.Pawn;
            if (target == null)
            {
                mapState?.SetFoodSearchTick(pawn.thingIDNumber, now + RHAH_ReliefFood.RetryBaseTicks);
                return null;
            }

            return JobMaker.MakeJob(RHAH_DefOf.RHAH_TailBite, target);
        }

        static bool Biteable(Verse.Pawn pawn, Verse.Pawn target)
        {
            if (target == null || target == pawn || !target.IsPrisoner || target.Map != pawn.Map)
            {
                return false;
            }

            float age = target.ageTracker == null ? 99f : target.ageTracker.AgeBiologicalYearsFloat;
            return RHAH_FamilyRules.CanTailBite(true, true, true, !target.Awake(), age) &&
                Tail(target) != null &&
                pawn.CanReserve(target);
        }

        internal static BodyPartRecord Tail(Verse.Pawn target)
        {
            if (target?.health?.hediffSet == null)
            {
                return null;
            }

            BodyDef body = target.RaceProps?.body;
            if (body == null)
            {
                return null;
            }

            List<BodyPartRecord> parts = body.AllParts;
            for (int i = 0; i < parts.Count; i++)
            {
                BodyPartRecord part = parts[i];
                if (part.def.defName != RHAH_FamilyRules.TailPartDef)
                {
                    continue;
                }

                bool added = false;
                List<Hediff> hediffs = target.health.hediffSet.hediffs;
                for (int h = 0; h < hediffs.Count; h++)
                {
                    if (hediffs[h].Part == part && hediffs[h] is Hediff_AddedPart)
                    {
                        added = true;
                        break;
                    }
                }

                if (RHAH_FamilyRules.NaturalTail(true, target.health.hediffSet.PartIsMissing(part), added))
                {
                    return part;
                }
            }

            return null;
        }
    }

    public class JobDriver_RHAH_TailBite : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => JobGiver_RHAH_TailBite.Tail(job.GetTarget(TargetIndex.A).Pawn) == null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, RHAH_FamilyRules.WorkTicks, true, false, false, TargetIndex.A);
            Toil finish = ToilMaker.MakeToil("TailBite");
            finish.initAction = () =>
            {
                Verse.Pawn target = job.GetTarget(TargetIndex.A).Pawn;
                BodyPartRecord tail = JobGiver_RHAH_TailBite.Tail(target);
                if (target == null || tail == null)
                {
                    return;
                }

                bool asleep = !target.Awake();
                if (asleep)
                {
                    Hediff missing = target.health.AddHediff(HediffDefOf.MissingBodyPart, tail);
                    if (missing is Hediff_MissingPart part)
                    {
                        part.IsFresh = true;
                        part.lastInjury = DamageDefOf.Bite.hediff;
                    }

                    Need_Food food = pawn.needs?.food;
                    if (food != null)
                    {
                        food.CurLevel += RHAH_Mod.Settings == null ? RHAH_FamilyRules.TailNutrition : RHAH_Mod.Settings.tailNutrition;
                    }

                    Compat.RHAH_RatEggCuisine.TryDropTail(target);
                }
                else
                {
                    target.TakeDamage(new DamageInfo(DamageDefOf.Bite, RHAH_Mod.Settings == null ? RHAH_FamilyRules.TailFailDamage : RHAH_Mod.Settings.tailFailDamage, instigator: pawn, hitPart: tail));
                }

                ThoughtDef biter = asleep ? RHAH_DefOf.RHAH_Thought_BitATail : null;
                ThoughtDef bitten = asleep ? RHAH_DefOf.RHAH_Thought_TailBitten : null;
                if (biter != null)
                {
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(biter, target);
                }

                if (bitten != null)
                {
                    target.needs?.mood?.thoughts?.memories?.TryGainMemory(bitten, pawn);
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
