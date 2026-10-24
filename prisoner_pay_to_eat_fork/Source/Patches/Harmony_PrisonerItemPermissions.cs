using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Prisoners retain their original faction. Only the built-in labour provider's eligible
    /// workers act for their host at the two vanilla product-forbidding sites and storage search.
    /// No existing item's forbidden flag, pawn faction, bill permission or reservation is changed.
    /// </summary>
    public static class PrisonerItemPermissions
    {
        public static Faction ProductionFaction(Pawn pawn) =>
            PrisonerWorkSystem.IsWorkEnabled(pawn) ? pawn.HostFaction : pawn.Faction;

        // Removing only the host exemption lets vanilla enforce the item's explicit flag,
        // allowed areas, duty limits and lord/ritual restrictions for working prisoners too.
        public static Faction ForbiddenHostFaction(Pawn pawn) =>
            PrisonerWorkSystem.IsWorkEnabled(pawn) ? null : pawn.HostFaction;

        public static void UseHostStorageFaction(Pawn carrier, Map map, ref Faction faction)
        {
            if (carrier == null || carrier.MapHeld != map || faction != carrier.Faction) return;
            if (PrisonerWorkSystem.IsWorkEnabled(carrier)) faction = carrier.HostFaction;
        }

        internal static MethodBase FindProductCallback(Type owner, string name, Type firstParameter)
        {
            MethodBase found = null;
            Find(owner);
            return found ?? throw new InvalidOperationException(
                "[PrisonersPayToEat2] Cannot find vanilla product callback on " + owner.FullName);

            void Find(Type type)
            {
                foreach (var method in AccessTools.GetDeclaredMethods(type))
                {
                    if (!method.Name.Contains(name) || method.ReturnType != typeof(void)) continue;
                    var parameters = method.GetParameters();
                    if (firstParameter == typeof(int))
                    {
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(int)) continue;
                    }
                    else if (parameters.Length != 2 || parameters[0].ParameterType != firstParameter
                        || parameters[1].ParameterType != typeof(int)) continue;
                    if (found != null)
                        throw new InvalidOperationException(
                            "[PrisonersPayToEat2] Ambiguous vanilla product callback on " + owner.FullName);
                    found = method;
                }
                foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                    Find(nested);
            }
        }

        internal static IEnumerable<CodeInstruction> ReplaceGetter(IEnumerable<CodeInstruction> instructions,
            MethodInfo getter, MethodInfo replacement, int expectedCount)
        {
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(getter))
                {
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = replacement;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != expectedCount)
                throw new InvalidOperationException(
                    "[PrisonersPayToEat2] Vanilla item-permission IL changed: expected "
                    + expectedCount + " calls to " + getter.Name + ", found " + replaced);
        }
    }

    // Change the producer check, not SetForbidden: mining's placement callback can receive a
    // preexisting merged stack whose explicit player forbiddance must remain untouched.
    [HarmonyPatch]
    public static class Patch_PrisonerHarvestProductFaction
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => PrisonerItemPermissions.FindProductCallback(
            typeof(JobDriver_PlantWork), "<MakeNewToils>", typeof(int));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            PrisonerItemPermissions.ReplaceGetter(instructions,
                AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Faction)),
                AccessTools.Method(typeof(PrisonerItemPermissions), nameof(PrisonerItemPermissions.ProductionFaction)), 1);
    }

    [HarmonyPatch]
    public static class Patch_PrisonerMiningProductFaction
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() => PrisonerItemPermissions.FindProductCallback(
            typeof(Mineable), "ForbidIfNecessary", typeof(Thing));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            PrisonerItemPermissions.ReplaceGetter(instructions,
                AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Faction)),
                AccessTools.Method(typeof(PrisonerItemPermissions), nameof(PrisonerItemPermissions.ProductionFaction)), 1);
    }

    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.CaresAboutForbidden))]
    public static class Patch_WorkingPrisonerCaresAboutForbidden
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            PrisonerItemPermissions.ReplaceGetter(instructions,
                AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.HostFaction)),
                AccessTools.Method(typeof(PrisonerItemPermissions), nameof(PrisonerItemPermissions.ForbiddenHostFaction)), 2);
    }

    [HarmonyPatch]
    public static class Patch_PrisonerHostStorageFaction
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStoreCellFor));
            yield return AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor));
            yield return AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell));
        }

        [HarmonyPrefix]
        public static void Prefix(Pawn carrier, Map map, ref Faction faction) =>
            PrisonerItemPermissions.UseHostStorageFaction(carrier, map, ref faction);
    }
}
