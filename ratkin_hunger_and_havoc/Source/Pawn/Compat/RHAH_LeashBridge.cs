using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    // 运行时找 LeadYourPetApi 不编译引用对方程序集
    internal static class RHAH_LeashBridge
    {
        internal const string PackageId = "nanaloveyuki.leadyourpet.continued";

        static bool logged;
        static bool resolved;
        static MethodInfo available;
        static MethodInfo mother;
        static MethodInfo childLeash;
        static MethodInfo special;
        static MethodInfo copy;
        static MethodInfo endMaster;
        static MethodInfo clear;

        internal static bool Available
        {
            get
            {
                Resolve();
                if (available == null)
                {
                    return false;
                }

                try
                {
                    return available.Invoke(null, null) is bool ready && ready;
                }
                catch (Exception exception)
                {
                    LogOnce(exception);
                    return false;
                }
            }
        }

        internal static void TryLeashArrivals(IReadOnlyList<Verse.Pawn> pawns)
        {
            if (!Available || pawns == null || pawns.Count == 0)
            {
                return;
            }

            Call(() =>
            {
                List<IRHAH_Pawn> snapshots = new List<IRHAH_Pawn>(pawns.Count);
                List<bool> gates = new List<bool>(pawns.Count);
                List<bool> players = new List<bool>(pawns.Count);
                bool[] parents = new bool[pawns.Count * pawns.Count];
                for (int i = 0; i < pawns.Count; i++)
                {
                    Verse.Pawn pawn = pawns[i];
                    IRHAH_Pawn snapshot = pawn == null ? null : RHAH_Api.Get(pawn);
                    snapshots.Add(snapshot);
                    gates.Add(snapshot != null && RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Leash));
                    players.Add(pawn?.Faction != null && pawn.Faction.IsPlayer);
                }

                for (int young = 0; young < pawns.Count; young++)
                {
                    for (int adult = 0; adult < pawns.Count; adult++)
                    {
                        parents[young * pawns.Count + adult] = IsParent(pawns[young], pawns[adult]);
                    }
                }

                if (!RHAH_LeashPlan.TryPair(snapshots, gates, parents, players, out List<RHAH_LeashPair> pairs))
                {
                    return;
                }

                for (int i = 0; i < pairs.Count; i++)
                {
                    RHAH_LeashPair pair = pairs[i];
                    Verse.Pawn adult = pawns[pair.AdultIndex];
                    Verse.Pawn young = pawns[pair.ChildIndex];
                    bool started = pair.SpecialSource == 0
                        ? InvokeBool(childLeash, adult, young)
                        : InvokeBool(mother, adult, young);
                    if (started && pair.SpecialSource != 0)
                    {
                        special?.Invoke(null, new object[] { young, pair.SpecialSource });
                    }
                }
            });
        }

        internal static void ClearDeparture(Verse.Pawn pawn)
        {
            if (!Available || pawn == null)
            {
                return;
            }

            Call(() =>
            {
                List<Verse.Pawn> linked = new List<Verse.Pawn>();
                copy?.Invoke(null, new object[] { pawn, linked });
                endMaster?.Invoke(null, new object[] { pawn });
                clear?.Invoke(null, new object[] { pawn });
                for (int i = 0; i < linked.Count; i++)
                {
                    if (linked[i] != null && linked[i] != pawn)
                    {
                        clear?.Invoke(null, new object[] { linked[i] });
                    }
                }
            });
        }

        static void Resolve()
        {
            if (resolved)
            {
                return;
            }

            if (ModLister.GetActiveModWithIdentifier(PackageId, false) == null)
            {
                return;
            }

            resolved = true;

            Type api = AccessTools.TypeByName("LeadYourPet.LeadYourPetApi");
            if (api == null)
            {
                LogOnce(null);
                return;
            }

            available = AccessTools.PropertyGetter(api, "Available");
            mother = AccessTools.Method(api, "TryStartMotherLeash", new[] { typeof(Verse.Pawn), typeof(Verse.Pawn) });
            childLeash = AccessTools.Method(api, "TryStartChildLeash", new[] { typeof(Verse.Pawn), typeof(Verse.Pawn) });
            special = AccessTools.Method(api, "SetSpecialSource", new[] { typeof(Verse.Pawn), typeof(int) });
            copy = AccessTools.Method(api, "TryCopyLinkedPets", new[] { typeof(Verse.Pawn), typeof(List<Verse.Pawn>) });
            endMaster = AccessTools.Method(api, "EndForMaster", new[] { typeof(Verse.Pawn) });
            clear = AccessTools.Method(api, "ClearOwnership", new[] { typeof(Verse.Pawn) });
            if (available == null || mother == null || childLeash == null ||
                special == null || copy == null || endMaster == null || clear == null)
            {
                available = null;
                LogOnce(null);
            }
        }

        static bool InvokeBool(MethodInfo method, Verse.Pawn adult, Verse.Pawn young)
        {
            return method?.Invoke(null, new object[] { adult, young }) is bool started && started;
        }

        static bool IsParent(Verse.Pawn young, Verse.Pawn adult)
        {
            return young != null && adult != null && young != adult &&
                young.relations?.DirectRelationExists(PawnRelationDefOf.Parent, adult) == true;
        }

        static void Call(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                LogOnce(exception);
            }
        }

        static void LogOnce(Exception exception)
        {
            if (logged)
            {
                return;
            }

            logged = true;
            string detail = exception == null ? "public API was not found" : exception.Message;
            Log.Warning("[RHAH] Lead Your Pet is inactive. Visitors continue without a leash. " + detail);
        }
    }
}
