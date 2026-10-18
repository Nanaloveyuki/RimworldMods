using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RatkinRecycle;

// Only diplomacy calls inside the two vanilla faction notifications are intercepted.
// DamageInfo keeps its real instigator for guilt, memories, quests and kill credit.
internal static class RatkinYoungDiplomacy
{
	private sealed class Provenance
	{
		public Provenance() { }
		public bool wound;
		public bool pureBloodLoss;
	}

	internal struct Context
	{
		public Pawn victim;
		public Pawn eater;
		public bool insideDamage;
		public bool death;
		public bool killed;
		public bool diplomaticExemption;
	}

	[ThreadStatic] private static Context current;
	[ThreadStatic] private static Pawn bleedingPawn;
	private static readonly ConditionalWeakTable<Hediff, Provenance> provenance = new();

	internal readonly struct BiteScope : IDisposable
	{
		private readonly Context previous;
		public BiteScope(Pawn victim, Pawn eater)
		{
			previous = current;
			current = new Context { victim = victim, eater = eater,
				diplomaticExemption = eater != null && eater.Faction != Faction.OfPlayer
					&& !eater.IsPrisonerOfColony && !eater.IsSlaveOfColony && eater.HostFaction?.IsPlayer != true };
		}
		public void Dispose() => current = previous;
	}

	internal static BiteScope BeginBite(Pawn victim, Pawn eater) => new(victim, eater);
	internal static bool BiteKilledVictim => current.killed;

	private static bool IsBite(Pawn victim, DamageInfo? damage)
	{
		return current.victim == victim && current.insideDamage && !current.death
			&& damage.HasValue && damage.Value.Def == DamageDefOf.Bite
			&& damage.Value.Instigator == current.eater;
	}

	private static bool IsWound(Hediff hediff)
	{
		return hediff != null && provenance.TryGetValue(hediff, out Provenance value) && value.wound;
	}

	private static bool IsPureBloodLoss(Hediff hediff)
	{
		return hediff != null && hediff.def == HediffDefOf.BloodLoss
			&& provenance.TryGetValue(hediff, out Provenance value) && value.pureBloodLoss;
	}

	private static bool OnlyModBleeding(Pawn pawn)
	{
		bool any = false;
		List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
		for (int i = 0; i < hediffs.Count; i++)
		{
			if (hediffs[i].BleedRate > 0f)
			{
				if (!IsWound(hediffs[i]))
				{
					return false;
				}
				any = true;
			}
		}
		return any;
	}

	private static bool ProtectsRelation(Faction faction, Faction other)
	{
		Faction victimFaction = current.victim?.HomeFaction;
		return current.diplomaticExemption && current.victim != null && (current.insideDamage || current.death)
			&& victimFaction != null && victimFaction != Faction.OfPlayer
			&& ((faction == victimFaction && other == Faction.OfPlayer)
				|| (other == victimFaction && faction == Faction.OfPlayer));
	}

	private static bool AffectGoodwill(Faction faction, Faction other, int goodwillChange,
		bool canSendMessage, bool canSendHostilityLetter, HistoryEventDef reason, GlobalTargetInfo? lookTarget)
	{
		return goodwillChange < 0 && ProtectsRelation(faction, other) ? false
			: faction.TryAffectGoodwillWith(other, goodwillChange, canSendMessage, canSendHostilityLetter, reason, lookTarget);
	}

	private static void SetRelation(Faction faction, Faction other, FactionRelationKind kind,
		bool canSendHostilityLetter, string reason, GlobalTargetInfo? lookTarget)
	{
		if (kind != FactionRelationKind.Hostile || !ProtectsRelation(faction, other))
		{
			faction.SetRelationDirect(other, kind, canSendHostilityLetter, reason, lookTarget);
		}
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
	private static class DamageScopePatch
	{
		private static void Prefix(Thing __instance, DamageInfo dinfo, out Context __state)
		{
			__state = current;
			if (!current.insideDamage && !current.death && current.victim == __instance
				&& dinfo.Def == DamageDefOf.Bite && dinfo.Instigator == current.eater)
			{
				current.insideDamage = true;
			}
			else
			{
				// A nested attack from another source is never part of our bite.
				current = default;
			}
		}

		private static Exception Finalizer(Exception __exception, Context __state)
		{
			bool killed = current.killed && current.victim == __state.victim;
			current = __state;
			current.killed |= killed;
			return __exception;
		}
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
	private static class DeathScopePatch
	{
		private static void Prefix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit, out Context __state)
		{
			__state = current;
			bool directBite = IsBite(__instance, dinfo);
			bool delayed = !dinfo.HasValue && exactCulprit?.pawn == __instance
				&& (IsWound(exactCulprit) || IsPureBloodLoss(exactCulprit));
			current = directBite || delayed ? new Context { victim = __instance, death = true,
				killed = directBite, diplomaticExemption = delayed || __state.diplomaticExemption } : default;
		}

		private static Exception Finalizer(Exception __exception, Pawn __instance, Context __state)
		{
			bool killed = current.killed && __instance.Dead && current.victim == __state.victim;
			current = __state;
			current.killed |= killed;
			return __exception;
		}
	}

	[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
	private static class WoundPatch
	{
		private static void Prefix(HediffSet __instance, Hediff hediff, DamageInfo? dinfo)
		{
			if (current.diplomaticExemption && IsBite(__instance.pawn, dinfo) && (hediff is Hediff_Injury || hediff is Hediff_MissingPart))
			{
				provenance.GetOrCreateValue(hediff).wound = true;
			}
		}
	}

	[HarmonyPatch(typeof(Hediff_Injury), nameof(Hediff_Injury.TryMergeWith))]
	private static class WoundMergePatch
	{
		private static bool Prefix(Hediff_Injury __instance, Hediff other, ref bool __result)
		{
			if (IsWound(__instance) == IsWound(other))
			{
				return true;
			}
			__result = false;
			return false;
		}
	}

	// Tag blood loss before its severity setter can synchronously kill the pawn.
	// Once mixed with an ordinary wound, the entire episode remains ordinary until zero.
	[HarmonyPatch(typeof(HediffGiver_Bleeding), nameof(HediffGiver_Bleeding.OnIntervalPassed))]
	private static class BleedingPatch
	{
		private static void Prefix(Pawn pawn, out Pawn __state)
		{
			__state = bleedingPawn;
			bleedingPawn = pawn;
		}

		private static Exception Finalizer(Exception __exception, Pawn __state)
		{
			bleedingPawn = __state;
			return __exception;
		}
	}

	[HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.AdjustSeverity))]
	private static class BloodLossAdjustmentPatch
	{
		private static void Prefix(Pawn pawn, HediffDef hdDef, float sevOffset)
		{
			if (hdDef != HediffDefOf.BloodLoss || sevOffset <= 0f)
			{
				return;
			}
			Hediff loss = pawn.health.hediffSet.GetFirstHediffOfDef(hdDef);
			// A fresh episode is created by the vanilla method. Its AddDirect hook below
			// records the contributors before CheckForStateChange, including fatal ticks.
			if (loss != null)
			{
				bool pure = bleedingPawn == pawn && OnlyModBleeding(pawn);
				if (loss.Severity <= 0f && pure)
				{
					provenance.GetOrCreateValue(loss).pureBloodLoss = true;
				}
				else if (!pure && provenance.TryGetValue(loss, out Provenance value))
				{
					value.pureBloodLoss = false;
				}
			}
		}
	}

	[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
	private static class BloodLossCreationPatch
	{
		private static void Prefix(HediffSet __instance, Hediff hediff)
		{
			if (hediff.def == HediffDefOf.BloodLoss && bleedingPawn == __instance.pawn && OnlyModBleeding(__instance.pawn))
			{
				provenance.GetOrCreateValue(hediff).pureBloodLoss = true;
			}
		}
	}

	[HarmonyPatch(typeof(Hediff), nameof(Hediff.ExposeData))]
	private static class ProvenanceSavePatch
	{
		private static void Postfix(Hediff __instance)
		{
			bool wound = IsWound(__instance);
			bool pureBloodLoss = IsPureBloodLoss(__instance);
			Scribe_Values.Look(ref wound, "ratkinYoungRecycleWound");
			Scribe_Values.Look(ref pureBloodLoss, "ratkinYoungRecycleBloodLoss");
			if (Scribe.mode == LoadSaveMode.LoadingVars && (wound || pureBloodLoss))
			{
				Provenance value = provenance.GetOrCreateValue(__instance);
				value.wound = wound;
				value.pureBloodLoss = pureBloodLoss;
			}
		}
	}

	[HarmonyPatch]
	private static class FactionDiplomacyPatch
	{
		private static void Prefix(Pawn member, MethodBase __originalMethod, out Context __state)
		{
			__state = current;
			if (member != current.victim || (__originalMethod.Name == nameof(Faction.Notify_MemberDied) && !current.death))
			{
				current = default;
			}
		}

		private static Exception Finalizer(Exception __exception, Context __state)
		{
			current = __state;
			return __exception;
		}

		private static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(Faction), nameof(Faction.Notify_MemberTookDamage));
			yield return AccessTools.Method(typeof(Faction), nameof(Faction.Notify_MemberDied));
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
		{
			MethodInfo goodwill = AccessTools.Method(typeof(Faction), nameof(Faction.TryAffectGoodwillWith));
			MethodInfo relation = AccessTools.Method(typeof(Faction), nameof(Faction.SetRelationDirect));
			MethodInfo goodwillWrapper = AccessTools.Method(typeof(RatkinYoungDiplomacy), nameof(AffectGoodwill));
			MethodInfo relationWrapper = AccessTools.Method(typeof(RatkinYoungDiplomacy), nameof(SetRelation));
			List<CodeInstruction> code = new(instructions);
			int goodwillCount = 0;
			int relationCount = 0;
			foreach (CodeInstruction instruction in code)
			{
				if (instruction.Calls(goodwill))
				{
					instruction.opcode = OpCodes.Call;
					instruction.operand = goodwillWrapper;
					goodwillCount++;
				}
				else if (instruction.Calls(relation))
				{
					instruction.opcode = OpCodes.Call;
					instruction.operand = relationWrapper;
					relationCount++;
				}
			}
			bool damage = __originalMethod.Name == nameof(Faction.Notify_MemberTookDamage);
			if (goodwillCount != (damage ? 1 : 3) || relationCount != (damage ? 1 : 0))
			{
				throw new InvalidOperationException("RatkinRecycle diplomacy patch requires review for " + __originalMethod
					+ $": found {goodwillCount} goodwill and {relationCount} direct-relation calls.");
			}
			return code;
		}
	}
}
