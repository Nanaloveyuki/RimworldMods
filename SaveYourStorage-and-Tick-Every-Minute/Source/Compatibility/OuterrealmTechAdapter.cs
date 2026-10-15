using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SaveYourStorageAndTickEveryMinute.Collections;
using SaveYourStorageAndTickEveryMinute.Scheduling;
using Verse;
using Verse.AI;

namespace SaveYourStorageAndTickEveryMinute.Compatibility
{
    internal static class OuterrealmTechAdapter
    {
        private const string RoutingTypeName =
            "FullyAutomaticOmniCrafter.OuterrealmStorage.OuterrealmIdentityRouting";
        private const string ViewOwnerTypeName =
            "FullyAutomaticOmniCrafter.OuterrealmStorage.OuterrealmVaultViewThingOwner";
        private const string ExpectedAssemblyName = "FullyAutomaticOmniCrafter";
        private const string OuterrealmHarmonyId = "Jeremie.Fully.Automatic.OmniCrafter";
        private const string VaultDeliveryJobDefName = "FAOC_VaultDeliverResources";
        private const int ReservationRetryTicks = 30;

        private static readonly BudgetedWorkScheduler<object> Scheduler =
            new BudgetedWorkScheduler<object>(ReferenceEqualityComparer<object>.Instance);

        private static MethodInfo moveToHomeMethod;
        private static MethodInfo isReservedMethod;
        private static MethodInfo prepareForSearchMethod;
        private static PropertyInfo statesForReadingProperty;
        private static FieldInfo softClaimantField;
        private static FieldInfo softUntilTickField;
        private static Type viewOwnerType;
        private static Game schedulerGame;
        private static bool applied;
        private static bool disabledAfterError;
        private static bool bookSearchPreRoutingDisabled;
        private static List<ThingDef> bookDefs;

        [ThreadStatic]
        private static int leaseTickDepth;

        [ThreadStatic]
        private static bool invokeOriginalMove;

        public static void TryApply(Harmony harmony)
        {
            Type routingType = AccessTools.TypeByName(RoutingTypeName);
            if (routingType == null)
                return;

            if (routingType.Assembly.GetName().Name != ExpectedAssemblyName)
            {
                Log.Warning(
                    "[SaveYourStorage] Outerrealm routing type came from an unexpected assembly; "
                    + "the compatibility patch was not applied.");
                return;
            }

            MethodInfo tickMethod = FindMethod(routingType, "Tick", typeof(void), 0);
            moveToHomeMethod = FindMethod(routingType, "MoveToHome", typeof(void), 2);
            isReservedMethod = FindMethod(routingType, "IsReserved", typeof(bool), 2);
            prepareForSearchMethod = FindMethod(routingType, "PrepareForSearch", typeof(void), 4);
            viewOwnerType = routingType.Assembly.GetType(ViewOwnerTypeName, false);
            MethodInfo canReserveStackMethod = AccessTools.Method(
                typeof(ReservationManager),
                nameof(ReservationManager.CanReserveStack),
                new[]
                {
                    typeof(Pawn),
                    typeof(LocalTargetInfo),
                    typeof(int),
                    typeof(ReservationLayerDef),
                    typeof(bool)
                });
            MethodInfo resourceDeliverJobMethod = AccessTools.Method(
                typeof(WorkGiver_ConstructDeliverResources),
                "ResourceDeliverJobFor",
                new[]
                {
                    typeof(Pawn),
                    typeof(IConstructible),
                    typeof(bool),
                    typeof(bool)
                });
            statesForReadingProperty = AccessTools.Property(routingType, "StatesForReading");
            if (tickMethod == null || !tickMethod.IsStatic
                || moveToHomeMethod == null || !moveToHomeMethod.IsStatic
                || isReservedMethod == null || !isReservedMethod.IsStatic
                || prepareForSearchMethod == null || !prepareForSearchMethod.IsStatic
                || viewOwnerType == null
                || canReserveStackMethod == null || canReserveStackMethod.ReturnType != typeof(int)
                || resourceDeliverJobMethod == null || resourceDeliverJobMethod.ReturnType != typeof(Job)
                || statesForReadingProperty == null
                || statesForReadingProperty.GetGetMethod(true) == null
                || !statesForReadingProperty.GetGetMethod(true).IsStatic)
            {
                Log.Warning(
                    "[SaveYourStorage] Outerrealm Tech internals did not match the supported "
                    + "signature; the compatibility patch was not applied.");
                return;
            }

            ParameterInfo[] moveParameters = moveToHomeMethod.GetParameters();
            ParameterInfo[] reservedParameters = isReservedMethod.GetParameters();
            ParameterInfo[] searchParameters = prepareForSearchMethod.GetParameters();
            Type entryType = moveParameters[0].ParameterType;
            Type stateType = moveParameters[1].ParameterType;
            softClaimantField = AccessTools.Field(stateType, "SoftClaimant");
            softUntilTickField = AccessTools.Field(stateType, "SoftUntilTick");
            if (reservedParameters[0].ParameterType != entryType
                || reservedParameters[1].ParameterType != stateType
                || searchParameters[0].ParameterType != typeof(ThingDef)
                || searchParameters[1].ParameterType != typeof(Map)
                || searchParameters[2].ParameterType != typeof(IntVec3)
                || searchParameters[3].ParameterType != typeof(Pawn)
                || !typeof(IDictionary).IsAssignableFrom(statesForReadingProperty.PropertyType)
                || softClaimantField == null || softUntilTickField == null
                || softUntilTickField.FieldType != typeof(int))
            {
                Log.Warning(
                    "[SaveYourStorage] Outerrealm anchor state did not match the supported "
                    + "layout; the compatibility patch was not applied.");
                return;
            }

            try
            {
                harmony.Patch(
                    moveToHomeMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(
                        typeof(OuterrealmTechAdapter),
                        nameof(MoveToHomePrefix))));
                harmony.Patch(
                    tickMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(OuterrealmTechAdapter), nameof(TickPrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(OuterrealmTechAdapter), nameof(TickPostfix))),
                    finalizer: new HarmonyMethod(AccessTools.Method(typeof(OuterrealmTechAdapter), nameof(TickFinalizer))));
                harmony.Patch(
                    canReserveStackMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(OuterrealmTechAdapter),
                        nameof(CanReserveStackPostfix))));
                var vaultDeliveryPostfix = new HarmonyMethod(AccessTools.Method(
                    typeof(OuterrealmTechAdapter),
                    nameof(ResourceDeliverJobPostfix)))
                {
                    after = new[] { OuterrealmHarmonyId }
                };
                harmony.Patch(resourceDeliverJobMethod, postfix: vaultDeliveryPostfix);
            }
            catch (Exception exception)
            {
                Log.Warning(
                    "[SaveYourStorage] Outerrealm Tech compatibility patch failed and remains inactive: "
                    + Unwrap(exception));
                return;
            }

            applied = true;
            Log.Message("[SaveYourStorage] Outerrealm Tech lease-return throttle applied.");
            Log.Message("[SaveYourStorage] Outerrealm book-search enumeration guard applied.");
            Log.Message("[SaveYourStorage] Outerrealm reservation recheck compatibility applied.");
            Log.Message("[SaveYourStorage] Outerrealm vault-delivery reservation guard applied.");
        }

        private static MethodInfo FindMethod(
            Type declaringType,
            string name,
            Type returnType,
            int parameterCount)
        {
            MethodInfo found = null;
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(declaringType))
            {
                if (method.Name != name
                    || method.ReturnType != returnType
                    || method.GetParameters().Length != parameterCount)
                {
                    continue;
                }

                if (found != null)
                    return null;
                found = method;
            }
            return found;
        }

        private static void TickPrefix()
        {
            leaseTickDepth++;
        }

        private static void CanReserveStackPostfix(
            ReservationManager __instance,
            Pawn claimant,
            LocalTargetInfo target,
            ReservationLayerDef layer,
            bool ignoreOtherReservations,
            ref int __result)
        {
            if (ignoreOtherReservations
                || __instance == null
                || claimant == null
                || !target.HasThing)
            {
                return;
            }

            Thing thing = target.Thing;
            if (thing == null
                || thing.holdingOwner == null
                || viewOwnerType == null
                || !viewOwnerType.IsInstanceOfType(thing.holdingOwner))
            {
                return;
            }

            long restoredAvailability = Math.Max(0, __result);
            List<ReservationManager.Reservation> reservations = __instance.ReservationsReadOnly;
            for (int i = 0; i < reservations.Count; i++)
            {
                ReservationManager.Reservation reservation = reservations[i];
                if (reservation.Target != target
                    || reservation.Layer != layer
                    || reservation.Claimant != claimant)
                {
                    continue;
                }

                int count = reservation.StackCount == ReservationManager.StackCount_All
                    ? thing.stackCount
                    : reservation.StackCount;
                if (count > 0)
                    restoredAvailability += count;
            }

            __result = (int)Math.Min(restoredAvailability, int.MaxValue);
        }

        private static void ResourceDeliverJobPostfix(Pawn pawn, ref Job __result)
        {
            if (__result == null
                || __result.def == null
                || __result.def.defName != VaultDeliveryJobDefName)
            {
                return;
            }

            Map map = pawn?.Map;
            LocalTargetInfo source = __result.GetTarget(TargetIndex.B);
            LocalTargetInfo destination = __result.GetTarget(TargetIndex.C);
            int count = __result.count;
            if (map == null
                || count <= 0
                || !source.HasThing
                || !destination.HasThing
                || !map.reservationManager.CanReserve(pawn, source, 1, count, null, false)
                || !map.reservationManager.CanReserve(pawn, destination, 1, 1, null, false))
            {
                __result = null;
            }
        }

        private static void TickPostfix()
        {
            if (leaseTickDepth > 0)
                leaseTickDepth--;

            DrainDeferredMoves();
        }

        private static Exception TickFinalizer(Exception __exception)
        {
            leaseTickDepth = 0;
            return __exception;
        }

        private static bool MoveToHomePrefix(object __0, object __1)
        {
            if (Current.Game != null)
                ResetForCurrentGame();

            if (!applied
                || disabledAfterError
                || invokeOriginalMove
                || leaseTickDepth <= 0
                || !ThrottleEnabled()
                || Current.Game == null)
            {
                return true;
            }

            ScheduleMove(__0, __1, GenTicks.TicksGame);
            return false;
        }

        internal static void PrepareBooksForSearch(Pawn pawn)
        {
            if (!applied
                || pawn == null
                || pawn.Map == null
                || Current.Game == null)
            {
                return;
            }

            ResetForCurrentGame();
            if (bookSearchPreRoutingDisabled || disabledAfterError)
                return;

            try
            {
                if (bookDefs == null)
                {
                    bookDefs = new List<ThingDef>();
                    List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
                    for (int i = 0; i < allDefs.Count; i++)
                    {
                        ThingDef def = allDefs[i];
                        if (def.HasComp<CompBook>())
                            bookDefs.Add(def);
                    }
                }

                for (int i = 0; i < bookDefs.Count; i++)
                {
                    prepareForSearchMethod.Invoke(
                        null,
                        new object[] { bookDefs[i], pawn.Map, pawn.Position, pawn });
                }
            }
            catch (Exception exception)
            {
                bookSearchPreRoutingDisabled = true;
                Log.Warning(
                    "[SaveYourStorage] Outerrealm book-search pre-routing disabled after an error: "
                    + Unwrap(exception));
            }
        }

        private static void ScheduleMove(object entry, object state, long dueTick)
        {
            if (entry == null || state == null)
                return;

            Scheduler.Schedule(entry, dueTick, () => ExecuteMove(entry, state));
        }

        private static bool ExecuteMove(object entry, object state)
        {
            try
            {
                var states = statesForReadingProperty.GetValue(null, null) as IDictionary;
                if (states == null || !ReferenceEquals(states[entry], state))
                    return false;

                int now = GenTicks.TicksGame;
                object claimant = softClaimantField.GetValue(state);
                int softUntilTick = (int)softUntilTickField.GetValue(state);
                if (claimant != null || softUntilTick > now)
                    return false;

                bool reserved = (bool)isReservedMethod.Invoke(null, new[] { entry, state });
                if (reserved)
                {
                    ScheduleMove(entry, state, (long)now + ReservationRetryTicks);
                    return false;
                }

                invokeOriginalMove = true;
                try
                {
                    moveToHomeMethod.Invoke(null, new[] { entry, state });
                }
                finally
                {
                    invokeOriginalMove = false;
                }
                return true;
            }
            catch (Exception exception)
            {
                disabledAfterError = true;
                Scheduler.Clear();
                Log.Error(
                    "[SaveYourStorage] Outerrealm Tech throttle disabled after an error: "
                    + Unwrap(exception));
                return false;
            }
        }

        private static void DrainDeferredMoves()
        {
            if (!applied || Current.Game == null)
                return;

            ResetForCurrentGame();
            if (disabledAfterError || Scheduler.Count == 0)
                return;

            SaveYourStorageSettings settings = SaveYourStorageMod.Settings;
            int maximumItems = settings != null
                ? settings.outerrealmMaximumRehomesPerTick
                : 8;
            double maximumMilliseconds = settings != null
                ? settings.outerrealmMaximumMilliseconds
                : 0.5d;
            Scheduler.Drain(GenTicks.TicksGame, maximumItems, maximumMilliseconds);
        }

        private static void ResetForCurrentGame()
        {
            Game game = Current.Game;
            if (ReferenceEquals(game, schedulerGame))
                return;

            Scheduler.Clear();
            schedulerGame = game;
            disabledAfterError = false;
            bookSearchPreRoutingDisabled = false;
        }

        private static bool ThrottleEnabled()
        {
            SaveYourStorageSettings settings = SaveYourStorageMod.Settings;
            return settings == null || settings.outerrealmLeaseThrottle;
        }

        private static Exception Unwrap(Exception exception)
        {
            var invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null
                ? invocation.InnerException
                : exception;
        }
    }

    [HarmonyPatch(typeof(BookUtility), nameof(BookUtility.TryGetRandomBookToRead))]
    internal static class Patch_BookUtility_TryGetRandomBookToRead
    {
        private static void Prefix(Pawn pawn)
        {
            OuterrealmTechAdapter.PrepareBooksForSearch(pawn);
        }
    }
}
