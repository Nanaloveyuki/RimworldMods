using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace OhYouAreSoFast
{
    [HarmonyPatch(
        typeof(CaravanTicksPerMoveUtility),
        nameof(CaravanTicksPerMoveUtility.GetTicksPerMove),
        new[]
        {
            typeof(List<Pawn>),
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(StringBuilder)
        })]
    [HarmonyAfter("com.turbojet.backpack")]
    [HarmonyPriority(Priority.Last)]
    internal static class Patch_CaravanTicksPerMove
    {
        private static void Postfix(bool isShuttle, ref int __result)
        {
            if (!(OhYouAreSoFastMod.Settings?.worldPathCorrectness ?? true))
                return;

            // Some equipment mods divide the vanilla result in a postfix and
            // round it to zero. Zero breaks WorldPathing's edge-cost contract.
            if (!isShuttle && __result < 1)
                __result = 1;
        }
    }
}
