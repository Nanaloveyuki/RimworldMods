using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace LeadYourPet
{
    // Optional PA_GodHands bridge. Reflection occurs only at grab time, never per tick.
    internal static class GodHandsCompat
    {
        private sealed class ReferenceComparer : IEqualityComparer<Pawn>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(Pawn x, Pawn y) => ReferenceEquals(x, y);
            public int GetHashCode(Pawn pawn) => RuntimeHelpers.GetHashCode(pawn);
        }

        private static readonly Dictionary<Pawn, object> Controllers = new Dictionary<Pawn, object>(ReferenceComparer.Instance);
        private static readonly List<Pawn> Released = new List<Pawn>();
        private static FieldInfo coreField;

        internal static int TrackedPawnCount => Controllers.Count;
        internal static bool IsGrabbed(Pawn pawn) => pawn != null && Controllers.ContainsKey(pawn);

        internal static void RecordGrabbed(Pawn pawn, object controller)
        {
            if (pawn != null && controller != null)
            {
                Controllers[pawn] = controller;
            }
        }

        internal static void RecordReleased(object controller)
        {
            if (controller == null)
            {
                return;
            }

            foreach (KeyValuePair<Pawn, object> entry in Controllers)
            {
                if (ReferenceEquals(entry.Value, controller))
                {
                    Released.Add(entry.Key);
                }
            }

            for (int i = 0; i < Released.Count; i++)
            {
                Controllers.Remove(Released[i]);
                LeadYourPetUtility.Component?.ResumeAfterExternalGrab(Released[i]);
            }
            Released.Clear();
        }

        internal static void Reset()
        {
            Controllers.Clear();
            Released.Clear();
        }

        [HarmonyPatch]
        internal static class Patch_GodHandPawnHandler_OnGrabbed
        {
            private static MethodBase TargetMethod() => AccessTools.Method("GodHandMod.GodHandPawnHandler:OnGrabbed", new[] { typeof(Pawn) });

            public static bool Prepare()
            {
                Type handler = AccessTools.TypeByName("GodHandMod.GodHandPawnHandler");
                Type controller = AccessTools.TypeByName("GodHandMod.GodHandController");
                coreField = handler == null ? null : AccessTools.Field(handler, "core");
                return controller != null && coreField?.FieldType == controller && TargetMethod() != null;
            }

            // Record before EndCurrentJob can synchronously request a new duty/interaction job.
            public static void Prefix(object __instance, Pawn p)
            {
                RecordGrabbed(p, coreField.GetValue(__instance));
            }
        }

        [HarmonyPatch]
        internal static class Patch_GodHandController_ForceReleaseGrab
        {
            private static MethodBase TargetMethod() => AccessTools.Method("GodHandMod.GodHandController:ForceReleaseGrab");
            public static bool Prepare() => TargetMethod() != null;
            public static void Postfix(object __instance) => RecordReleased(__instance);
        }
    }
}
