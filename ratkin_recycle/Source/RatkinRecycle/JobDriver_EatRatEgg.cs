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
	private bool consumed;
	private int consecutiveNoDamageBites;

	private Pawn Victim => job?.GetTarget(TargetIndex.A).Thing as Pawn;

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_References.Look(ref victimStored, "victimStored");
		Scribe_Values.Look(ref kind, "kind", EaterKind.None);
		Scribe_Values.Look(ref own, "own");
		Scribe_Values.Look(ref tier, "tier");
		Scribe_Values.Look(ref carried, "carried");
		Scribe_Values.Look(ref finished, "finished");
		Scribe_Values.Look(ref nextBiteTick, "nextBiteTick");
		Scribe_Values.Look(ref consumed, "consumed");
		Scribe_Values.Look(ref consecutiveNoDamageBites, "consecutiveNoDamageBites");
		if (Scribe.mode == LoadSaveMode.PostLoadInit && victimStored == null)
		{
			// Old jobs did not save their state. Recover ownership, never past rewards.
			victimStored = Victim;
			carried = OwnsVictimOrCorpse();
			consumed = false;
			kind = RatkinRecycleUtility.GetEaterKind(pawn);
			own = RatkinRecycleUtility.IsOwnEgg(pawn, victimStored);
			tier = victimStored == null ? 0 : RatkinRecycleUtility.GrowthTier(victimStored);
			nextBiteTick = Find.TickManager.TicksGame + Mathf.Max(10, RatkinRecycleMod.Settings.biteInterval);
		}
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		Pawn victim = Victim;
		EaterKind eaterKind = RatkinRecycleUtility.GetEaterKind(pawn);
		return pawn.carryTracker != null && pawn.carryTracker.CarriedThing == null
			&& RatkinRecycleUtility.CanTarget(pawn, victim, eaterKind)
			&& pawn.Reserve(victim, job, 1, -1, null, errorOnFailed);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		AddFinishAction(_ => DropOwnedVictim());
		// Despawn is expected after pickup; this failure belongs only to the approach.
		Toil approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
		approach.FailOnDespawnedNullOrForbidden(TargetIndex.A);
		yield return approach;
		Toil pickup = ToilMaker.MakeToil("carryRatkinYoung");
		pickup.defaultCompleteMode = ToilCompleteMode.Instant;
		pickup.initAction = CarryAction;
		yield return pickup;
		Toil chew = ToilMaker.MakeToil("chewRatkinYoung");
		chew.defaultCompleteMode = ToilCompleteMode.Never;
		chew.tickAction = ChewTick;
		chew.PlaySustainerOrSound(() => SoundDefOf.RawMeat_Eat);
		yield return chew;
	}

	private void CarryAction()
	{
		victimStored = Victim;
		kind = RatkinRecycleUtility.GetEaterKind(pawn);
		if (!RatkinRecycleUtility.CanEat(pawn)
			|| !RatkinRecycleUtility.CanTarget(pawn, victimStored, kind)
			|| pawn.carryTracker == null || pawn.carryTracker.CarriedThing != null)
		{
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		own = RatkinRecycleUtility.IsOwnEgg(pawn, victimStored);
		tier = RatkinRecycleUtility.GrowthTier(victimStored);
		// The count overload splits/despawns the pawn before moving it into the owner.
		carried = pawn.carryTracker.TryStartCarry(victimStored, 1) == 1;
		if (!carried || !OwnsLivingVictim())
		{
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		nextBiteTick = Find.TickManager.TicksGame + Mathf.Max(10, RatkinRecycleMod.Settings.biteInterval);
	}

	private bool OwnsLivingVictim()
	{
		return victimStored != null && !victimStored.Destroyed && !victimStored.Dead
			&& !victimStored.Spawned && pawn?.carryTracker?.CarriedThing == victimStored
			&& victimStored.ParentHolder == pawn.carryTracker;
	}

	private bool OwnsVictimOrCorpse()
	{
		Thing thing = pawn?.carryTracker?.CarriedThing;
		return victimStored != null && thing != null && thing.ParentHolder == pawn.carryTracker
			&& (thing == victimStored || thing == victimStored.Corpse);
	}

	private bool CanContinue()
	{
		return carried && OwnsLivingVictim() && RatkinRecycleUtility.GetEaterKind(pawn) != EaterKind.None
			&& RatkinRecycleUtility.IsVictimAllowed(victimStored);
	}

	private void ChewTick()
	{
		if (finished)
		{
			EndJobWith(JobCondition.Succeeded);
			return;
		}
		// A death between our bites is not a completed meal, even after earlier bites.
		if (!carried || !OwnsLivingVictim() || pawn.Dead || pawn.Downed)
		{
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		if (IsFull())
		{
			if (CanContinue())
			{
				StopPartially("full");
			}
			else
			{
				EndJobWith(JobCondition.Incompletable);
			}
			return;
		}
		int ticks = Find.TickManager.TicksGame;
		if (ticks >= nextBiteTick)
		{
			nextBiteTick = ticks + Mathf.Max(10, RatkinRecycleMod.Settings.biteInterval);
			Bite();
		}
	}

	private bool IsFull()
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		return settings != null && settings.stopAtFullness > 0f && pawn.needs?.food != null
			&& pawn.needs.food.CurLevelPercentage >= settings.stopAtFullness;
	}

	private void StopPartially(string reason)
	{
		if (finished)
		{
			return;
		}
		finished = true;
		DropOwnedVictim();
		if (consumed)
		{
			RatkinRecycleUtility.OnEggPartiallyEaten(pawn, victimStored, kind, own, tier, reason);
		}
		EndJobWith(JobCondition.Succeeded);
	}

	private void Bite()
	{
		if (!CanContinue())
		{
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		BodyPartRecord part = RatkinRecycleUtility.PickBitePart(victimStored);
		if (part == null)
		{
			StopPartially("exhausted");
			return;
		}
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		DamageInfo damage = new DamageInfo(DamageDefOf.Bite, settings.biteDamage + Rand.Range(0f, 2f), 0f, -1f, pawn, part);
		// The whitelist restricts direct targets; losing a part can still remove descendants
		// and its wounds can bleed. Do not redirect or spread damage to other parts.
		damage.SetAllowDamagePropagation(false);
		DamageWorker.DamageResult result;
		bool killedByBite;
		using (RatkinYoungDiplomacy.BeginBite(victimStored, pawn))
		{
			result = victimStored.TakeDamage(damage);
			killedByBite = RatkinYoungDiplomacy.BiteKilledVictim;
		}
		if (result == null || result.totalDamageDealt <= 0f)
		{
			// Armor or damage immunity must not keep a non-expiring chew toil alive forever.
			if (++consecutiveNoDamageBites >= 3)
			{
				EndJobWith(JobCondition.Incompletable);
			}
			return;
		}
		consecutiveNoDamageBites = 0;
		consumed = true;
		if (pawn.needs?.food != null)
		{
			pawn.needs.food.CurLevel = Mathf.Min(pawn.needs.food.MaxLevel, pawn.needs.food.CurLevel + settings.nutritionPerBite);
		}
		if (victimStored.Dead)
		{
			if (killedByBite)
			{
				finished = true;
				DropOwnedVictim();
				RatkinRecycleUtility.OnEggEaten(pawn, victimStored, kind, own, tier);
				EndJobWith(JobCondition.Succeeded);
			}
			else
			{
				EndJobWith(JobCondition.Incompletable);
			}
		}
		else if (IsFull())
		{
			StopPartially("full");
		}
	}

	private void DropOwnedVictim()
	{
		if (carried && OwnsVictimOrCorpse() && pawn.MapHeld != null && pawn.PositionHeld.IsValid)
		{
			// Failed placement leaves the owned thing in the carry tracker, never destroys it.
			if (pawn.carryTracker.innerContainer.TryDrop(pawn.carryTracker.CarriedThing,
				pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near, out _))
			{
				pawn.MapHeld.resourceCounter.UpdateResourceCounts();
			}
		}
	}
}
