using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraFlightJobFix
{
    internal static class DraftAnythingTrackerCleanup
    {
        private static readonly ConditionalWeakTable<Pawn, TemporaryTrackers> AddedTrackers = new();

        internal static void Install()
        {
            Type utils = AccessTools.TypeByName("DraftAnything.Utils");
            MethodInfo assign = utils == null ? null : AccessTools.Method(utils, "AssignComponents", new[] { typeof(Pawn) });
            MethodInfo restore = utils == null ? null : AccessTools.Method(utils, "RestoreOriginalState", new[] { typeof(Pawn) });
            if (assign == null || restore == null)
            {
                Log.Warning("[DraftAnythingFixes] Draft Anything tracker methods were not found; tracker cleanup fix disabled.");
                return;
            }

            var harmony = new Harmony(MiliraFlightJobFixMod.HarmonyId + ".trackers");
            try
            {
                harmony.Patch(assign,
                    prefix: new HarmonyMethod(typeof(DraftAnythingTrackerCleanup), nameof(BeforeAssign)),
                    postfix: new HarmonyMethod(typeof(DraftAnythingTrackerCleanup), nameof(AfterAssign)));
                harmony.Patch(restore,
                    transpiler: new HarmonyMethod(typeof(DraftAnythingTrackerCleanup), nameof(PreserveExistingTrackers)),
                    postfix: new HarmonyMethod(typeof(DraftAnythingTrackerCleanup), nameof(AfterRestore)));
                Log.Message("[DraftAnythingFixes] Tracker cleanup preserves existing player settings and draft controllers.");
            }
            catch (Exception exception)
            {
                harmony.UnpatchAll(harmony.Id);
                Log.Error("[DraftAnythingFixes] Could not install tracker cleanup fix: " + exception);
            }
        }

        private static void BeforeAssign(Pawn pawn, out TrackersBefore __state)
        {
            __state = new TrackersBefore(pawn.playerSettings, pawn.drafter);
        }

        private static void AfterAssign(Pawn pawn, TrackersBefore __state)
        {
            bool addedSettings = __state.PlayerSettings == null && pawn.playerSettings != null;
            bool addedDrafter = __state.Drafter == null && pawn.drafter != null;
            if (!addedSettings && !addedDrafter)
            {
                return;
            }

            TemporaryTrackers added = AddedTrackers.GetValue(pawn, _ => new TemporaryTrackers());
            if (addedSettings)
            {
                added.PlayerSettings = pawn.playerSettings;
            }
            if (addedDrafter)
            {
                added.Drafter = pawn.drafter;
            }
        }

        private static IEnumerable<CodeInstruction> PreserveExistingTrackers(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            ReplaceClear(code, nameof(Pawn.playerSettings), nameof(ClearTemporaryPlayerSettings));
            ReplaceClear(code, nameof(Pawn.drafter), nameof(ClearTemporaryDrafter));
            return code;
        }

        private static void ReplaceClear(List<CodeInstruction> code, string fieldName, string replacement)
        {
            FieldInfo field = AccessTools.Field(typeof(Pawn), fieldName);
            int[] stores = Enumerable.Range(0, code.Count).Where(i => code[i].StoresField(field)).ToArray();
            if (stores.Length != 1 || stores[0] == 0 || code[stores[0] - 1].opcode != OpCodes.Ldnull)
            {
                throw new InvalidOperationException("Unexpected Draft Anything cleanup for Pawn." + fieldName);
            }

            CodeInstruction store = code[stores[0]];
            store.opcode = OpCodes.Call;
            store.operand = AccessTools.Method(typeof(DraftAnythingTrackerCleanup), replacement);
        }

        private static void ClearTemporaryPlayerSettings(Pawn pawn, Pawn_PlayerSettings unused)
        {
            if (AddedTrackers.TryGetValue(pawn, out TemporaryTrackers added)
                && ReferenceEquals(added.PlayerSettings, pawn.playerSettings)
                && pawn.Faction?.IsPlayer != true
                && pawn.HostFaction?.IsPlayer != true
                && !pawn.IsOnHoldingPlatform)
            {
                pawn.playerSettings = null;
            }
        }

        private static void ClearTemporaryDrafter(Pawn pawn, Pawn_DraftController unused)
        {
            if (AddedTrackers.TryGetValue(pawn, out TemporaryTrackers added)
                && ReferenceEquals(added.Drafter, pawn.drafter)
                && !(pawn.RaceProps.Humanlike && pawn.Faction?.IsPlayer == true)
                && !(pawn.RaceProps.IsMechanoid && pawn.IsColonyMech))
            {
                pawn.drafter = null;
            }
        }

        private static void AfterRestore(Pawn pawn)
        {
            AddedTrackers.Remove(pawn);
        }

        private readonly struct TrackersBefore
        {
            internal readonly Pawn_PlayerSettings PlayerSettings;
            internal readonly Pawn_DraftController Drafter;

            internal TrackersBefore(Pawn_PlayerSettings playerSettings, Pawn_DraftController drafter)
            {
                PlayerSettings = playerSettings;
                Drafter = drafter;
            }
        }

        private sealed class TemporaryTrackers
        {
            internal Pawn_PlayerSettings PlayerSettings;
            internal Pawn_DraftController Drafter;
        }
    }
}
