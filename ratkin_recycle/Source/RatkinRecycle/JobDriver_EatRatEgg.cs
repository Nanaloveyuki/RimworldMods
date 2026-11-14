using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinRecycle;

public class JobDriver_EatRatEgg : JobDriver
{
	private const float StopFullness = 0.95f;
	private Pawn victimStored;
	private EaterKind kind;
	private bool own;
	private int tier;
	private bool carried;
	private bool finished;
	private int biteTicks;
	private int biteDuration;
	private bool startedChewing;
	private bool ignoreFullnessAndSafety;
	private bool consumed;
	private bool thoughtsGranted;
	private int consecutiveNoDamageBites;

	private Pawn Victim => job?.GetTarget(TargetIndex.A).Thing as Pawn;

	// The distinct JobDef preserves the entry point through queueing and save/load.
	private bool Manual => job?.def == RatkinRecycleDefOf.ManualRecycleRatkinYoung;
	private bool FoodPreventsRecycling => !Manual && RatkinRecycleFood.HasAvailableFood(pawn);

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_References.Look(ref victimStored, "victimStored");
		Scribe_Values.Look(ref kind, "kind", EaterKind.None);
		Scribe_Values.Look(ref own, "own");
		Scribe_Values.Look(ref tier, "tier");
		Scribe_Values.Look(ref carried, "carried");
		Scribe_Values.Look(ref finished, "finished");
		Scribe_Values.Look(ref biteTicks, "biteTicks");
		Scribe_Values.Look(ref biteDuration, "biteDuration");
		Scribe_Values.Look(ref startedChewing, "startedChewing");
		Scribe_Values.Look(ref ignoreFullnessAndSafety, "ignoreFullnessAndSafety");
		Scribe_Values.Look(ref consumed, "consumed");
		Scribe_Values.Look(ref thoughtsGranted, "thoughtsGranted");
		Scribe_Values.Look(ref consecutiveNoDamageBites, "consecutiveNoDamageBites");
		if (Scribe.mode == LoadSaveMode.PostLoadInit && victimStored == null)
		{
			// Old jobs did not save their state. Recover ownership, never past rewards.
			victimStored = Victim;
			carried = OwnsVictimOrCorpse();
			consumed = false;
			kind = RatkinRecycleUtility.GetEaterKind(pawn, Manual);
			own = RatkinRecycleUtility.IsOwnEgg(pawn, victimStored);
			tier = victimStored == null ? 0 : RatkinRecycleUtility.GrowthTier(victimStored);
		}
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		Pawn victim = Victim;
		EaterKind eaterKind = RatkinRecycleUtility.GetEaterKind(pawn, Manual);
		return pawn.carryTracker != null && pawn.carryTracker.CarriedThing == null
			&& RatkinRecycleUtility.CanTarget(pawn, victim, eaterKind, Manual)
			&& !FoodPreventsRecycling
			&& pawn.Reserve(victim, job, 1, -1, null, errorOnFailed);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		AddFinishAction(_ => DropOwnedVictim());
		// Despawn is expected after pickup; this failure belongs only to the approach.
		Toil approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
		approach.FailOnDespawnedOrNull(TargetIndex.A);
		approach.FailOn(() => !Manual && Victim.IsForbidden(pawn));
		yield return approach;
		Toil pickup = ToilMaker.MakeToil("carryRatkinYoung");
		pickup.defaultCompleteMode = ToilCompleteMode.Instant;
		pickup.initAction = CarryAction;
		yield return pickup;
		Toil chew = ToilMaker.MakeToil("chewRatkinYoung");
		chew.defaultCompleteMode = ToilCompleteMode.Never;
		chew.tickAction = () => ChewTick(approach);
		ShowBiteProgress(chew);
		chew.PlaySustainerOrSound(() => SoundDefOf.RawMeat_Eat);
		yield return chew;
	}

	private void ShowBiteProgress(Toil chew)
	{
		Effecter progress = null;
		// Vanilla's WithProgressBar skips visitors and traders.
		chew.AddPreTickAction(() =>
		{
			progress ??= EffecterDefOf.ProgressBar.Spawn();
			progress.EffectTick(pawn, TargetInfo.Invalid);
			if (progress.children.Count == 0) return;
			MoteProgressBar mote = ((SubEffecter_ProgressBar)progress.children[0]).mote;
			if (mote == null) return;
			mote.progress = biteDuration > 0 ? Mathf.Clamp01((float)biteTicks / biteDuration) : 0f;
			mote.offsetZ = -0.5f;
			mote.alwaysShow = true;
		});
		chew.AddFinishAction(() =>
		{
			progress?.Cleanup();
			progress = null;
		});
	}

	private void CarryAction()
	{
		victimStored = Victim;
		kind = RatkinRecycleUtility.GetEaterKind(pawn, Manual);
		if (!RatkinRecycleUtility.CanEat(pawn, Manual)
			|| !RatkinRecycleUtility.CanTarget(pawn, victimStored, kind, Manual)
			|| FoodPreventsRecycling
			|| pawn.carryTracker == null || pawn.carryTracker.CarriedThing != null)
		{
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		if (!startedChewing)
		{
			own = RatkinRecycleUtility.IsOwnEgg(pawn, victimStored);
			tier = RatkinRecycleUtility.GrowthTier(victimStored);
			StartChewing();
		}
		// Toddlers' initial self-care stage is distinct from carrying capacity.
		if (CanCarryVictim())
		{
			carried = pawn.carryTracker.TryStartCarry(victimStored, 1) == 1;
			if (!carried || !OwnsLivingVictim())
			{
				EndJobWith(JobCondition.Incompletable);
				return;
			}
		}
		if (biteDuration == 0) StartNextBite();
	}

	private bool CanCarryVictim()
	{
		if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
			|| pawn.carryTracker.AvailableStackSpace(victimStored.def) <= 0) return false;
		foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
			if (hediff.def.defName == "LearningManipulation" && hediff.CurStageIndex == 0) return false;
		return true;
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

	private bool HasLivingVictim()
	{
		return carried ? OwnsLivingVictim()
			: victimStored != null && !victimStored.Destroyed && !victimStored.Dead
				&& victimStored.Spawned && pawn.Spawned && victimStored.Map == pawn.Map
				&& pawn.carryTracker.CarriedThing == null;
	}

	private bool CanContinue()
	{
		return HasLivingVictim() && RatkinRecycleUtility.GetEaterKind(pawn, Manual) != EaterKind.None
			&& RatkinRecycleUtility.IsVictimAllowed(victimStored, Manual);
	}

	private void ChewTick(Toil approach)
	{
		if (finished)
		{
			EndJobWith(JobCondition.Succeeded);
			return;
		}
		// A death between our bites is not a completed meal, even after earlier bites.
		if (!HasLivingVictim() || !RatkinRecycleUtility.CanEat(pawn, Manual))
		{
			EndJobWith(JobCondition.Incompletable);
			return;
		}
		if (!startedChewing) StartChewing();
		if (!carried && !pawn.CanReachImmediate(victimStored, PathEndMode.Touch))
		{
			JumpToToil(approach);
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
		if (biteDuration == 0) StartNextBite();
		if (++biteTicks >= biteDuration)
		{
			StartNextBite();
			Bite();
		}
	}

	private void StartChewing()
	{
		startedChewing = true;
		ignoreFullnessAndSafety = pawn.needs.food.CurLevelPercentage > StopFullness;
	}

	private void StartNextBite()
	{
		biteTicks = 0;
		biteDuration = Mathf.Clamp(RatkinRecycleMod.Settings.biteInterval, 60, 1200);
	}

	private bool IsFull()
	{
		return !ignoreFullnessAndSafety && pawn.needs?.food != null
			&& pawn.needs.food.CurLevelPercentage >= StopFullness;
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
		if (FoodPreventsRecycling)
		{
			EndJobWith(JobCondition.InterruptForced);
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
		if (WouldBreachSafetyLimit(damage))
		{
			StopPartially("safety");
			return;
		}
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
		if (!thoughtsGranted)
		{
			RatkinRecycleUtility.GainEggThoughts(pawn, victimStored, kind, own, tier);
			thoughtsGranted = true;
		}
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

	private bool WouldBreachSafetyLimit(DamageInfo damage)
	{
		if (ignoreFullnessAndSafety || !RatkinRecycleMod.Settings.victimSafetyLimit
			|| pawn.needs.food.CurCategory != HungerCategory.Fed || damage.Amount <= 0f) return false;
		if (victimStored.def.damageMultipliers != null)
			foreach (DamageMultiplier multiplier in victimStored.def.damageMultipliers)
				if (multiplier.damageDef == damage.Def)
					damage.SetAmount(Mathf.RoundToInt(damage.Amount * multiplier.multiplier));
		float factor = 1f;
		if (ModsConfig.BiotechActive && victimStored.genes != null)
			factor *= victimStored.genes.FactorForDamage(damage);
		factor *= victimStored.health.FactorForDamage(damage);
		// Use unarmored damage so armor randomness cannot turn a predicted safe bite lethal.
		float severity = damage.Amount * factor;
		if (severity <= 0f) return false;
		// Armor can round sub-point damage up to one.
		if (damage.Def.armorCategory != null) severity = Mathf.Max(1f, severity);
		if (damage.Def.ExternalViolenceFor(victimStored))
			severity *= victimStored.GetStatValue(StatDefOf.IncomingDamageFactor);
		BodyPartRecord part = damage.HitPart;
		HediffDef injury = HealthUtility.GetHediffDefFromDamage(damage.Def, victimStored, part);
		if (victimStored.health.WouldDieAfterAddingHediff(injury, part, severity)) return true;
		// Non-colony pawns can also die randomly from a newly downing injury.
		bool diesWhenDowned = victimStored.kindDef.forceDeathOnDowned
			|| (!victimStored.health.forceDowned && !victimStored.IsWildMan() && !victimStored.IsDeactivated()
				&& victimStored.Faction?.IsPlayer != true && victimStored.HostFaction?.IsPlayer != true);
		return !victimStored.Downed && diesWhenDowned
			&& victimStored.health.WouldBeDownedAfterAddingHediff(injury, part, severity);
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
