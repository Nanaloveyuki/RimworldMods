using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinRecycle;

public class JobDriver_EatRatEgg : JobDriver
{
	private Pawn victimStored;

	private EaterKind kind;

	private bool own;

	private int tier;

	private bool carried;

	private bool finished;

	private int nextBiteTick;

	private Pawn Victim
	{
		get
		{
			if (job == null || !job.GetTarget(TargetIndex.A).IsValid)
			{
				return null;
			}
			return job.GetTarget(TargetIndex.A).Thing as Pawn;
		}
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		Pawn victim = Victim;
		if (victim == null || victim.Dead || !victim.Spawned)
		{
			return false;
		}
		return pawn.Reserve(victim, job, 1, -1, null, errorOnFailed);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDestroyedOrNull(TargetIndex.A);
		Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
		toil.FailOnDespawnedNullOrForbidden(TargetIndex.A);
		yield return toil;
		Toil toil2 = ToilMaker.MakeToil("carryRatEgg");
		toil2.defaultCompleteMode = ToilCompleteMode.Instant;
		toil2.initAction = CarryAction;
		yield return toil2;
		Toil toil3 = ToilMaker.MakeToil("chewRatEgg");
		toil3.defaultCompleteMode = ToilCompleteMode.Never;
		toil3.tickAction = ChewTick;
		toil3.AddFinishAction(Cleanup);
		toil3.PlaySustainerOrSound(() => SoundDefOf.RawMeat_Eat);
		yield return toil3;
	}

	private void CarryAction()
	{
		if (victimStored == null)
		{
			victimStored = Victim;
		}
		kind = RatkinRecycleUtility.GetEaterKind(pawn);
		own = victimStored != null && RatkinRecycleUtility.IsOwnEgg(pawn, victimStored);
		tier = ((victimStored != null) ? RatkinRecycleUtility.GrowthTier(victimStored) : 0);
		if (victimStored == null || victimStored.Dead)
		{
			victimStored = null;
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		if (pawn.carryTracker != null)
		{
			if (pawn.carryTracker.CarriedThing == null)
			{
				carried = pawn.carryTracker.TryStartCarry(victimStored);
			}
			else
			{
				if (pawn.carryTracker.CarriedThing != victimStored)
				{
					carried = false;
					EndJobWith(JobCondition.Incompletable);
					return;
				}
				carried = true;
			}
		}
		else
		{
			carried = false;
		}
		nextBiteTick = Find.TickManager.TicksGame + RatkinRecycleMod.Settings.biteInterval;
	}

	private void ChewTick()
	{
		if (finished)
		{
			return;
		}
		Pawn pawn = victimStored;
		if (pawn == null)
		{
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		if (base.pawn.Dead || base.pawn.Downed)
		{
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		if (pawn.Dead)
		{
			FinishEating();
			return;
		}
		if (IsFull())
		{
			StopEatingFull();
			return;
		}
		int ticksGame = Find.TickManager.TicksGame;
		if (ticksGame >= nextBiteTick)
		{
			nextBiteTick = ticksGame + Mathf.Max(10, RatkinRecycleMod.Settings.biteInterval);
			Bite();
			if (pawn.Dead)
			{
				FinishEating();
			}
			else if (IsFull())
			{
				StopEatingFull();
			}
		}
	}

	private bool IsFull()
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null || settings.stopAtFullness <= 0f)
		{
			return false;
		}
		if (pawn.needs?.food != null)
		{
			return pawn.needs.food.CurLevelPercentage >= settings.stopAtFullness;
		}
		return false;
	}

	private void StopEatingFull()
	{
		if (!finished)
		{
			finished = true;
			if (carried && pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null)
			{
				pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var _);
			}
			Pawn victim = victimStored;
			RatkinRecycleUtility.OnEggPartiallyEaten(pawn, victim, kind, own, tier);
			EndJobWith(JobCondition.Succeeded);
		}
	}

	private void Bite()
	{
		Pawn pawn = victimStored;
		if (pawn != null && !pawn.Dead)
		{
			RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
			float num = settings.biteDamage + Rand.Range(0f, 2f);
			BodyPartRecord bodyPartRecord = RatkinRecycleUtility.PickBitePart(pawn);
			DamageInfo dinfo = ((bodyPartRecord != null) ? new DamageInfo(DamageDefOf.Bite, num, 0f, -1f, base.pawn, bodyPartRecord) : new DamageInfo(DamageDefOf.Bite, Mathf.Max(num, 10f), 0f, -1f, base.pawn));
			pawn.TakeDamage(dinfo);
			if (!pawn.Dead && base.pawn.needs?.food != null)
			{
				base.pawn.needs.food.CurLevel = Mathf.Min(base.pawn.needs.food.MaxLevel, base.pawn.needs.food.CurLevel + settings.nutritionPerBite);
			}
		}
	}

	private void FinishEating()
	{
		if (!finished)
		{
			finished = true;
			if (carried && pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null)
			{
				pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var _);
			}
			Pawn victim = victimStored;
			RatkinRecycleUtility.OnEggEaten(pawn, victim, kind, own, tier);
			EndJobWith(JobCondition.Succeeded);
		}
	}

	private void Cleanup()
	{
		if (pawn != null && pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null)
		{
			Thing carriedThing = pawn.carryTracker.CarriedThing;
			if (carriedThing == victimStored || (victimStored != null && carriedThing == victimStored.Corpse))
			{
				pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var _);
			}
		}
	}
}
