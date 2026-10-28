#if ART_IRISMENUS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AdvancedRimTalk.UI;
using HarmonyLib;
using UnityEngine;

namespace AdvancedRimTalk.Integration
{
    [HarmonyPatch(typeof(GUI), "DrawTextFieldChrome")]
    internal static class ArtiNativeCaretPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo recolor = AccessTools.Method(typeof(ArtiNativeCaretPatch), nameof(CaretColor));
            for (int i = 0; i + 4 < code.Count; i++)
            {
                if (!Loads(code[i], .92f) || !Loads(code[i + 1], .92f) ||
                    !Loads(code[i + 2], .92f) || !Loads(code[i + 3], .9f)) continue;
                for (int j = i + 4; j < Math.Min(code.Count, i + 8); j++)
                {
                    bool producesColor = code[j].opcode == OpCodes.Newobj && code[j].operand is ConstructorInfo constructor && constructor.DeclaringType == typeof(Color)
                        || code[j].opcode == OpCodes.Call && code[j].operand is MethodInfo method && method.ReturnType == typeof(Color);
                    if (!producesColor) continue;
                    code.Insert(j + 1, new CodeInstruction(OpCodes.Ldarg_S, (byte)5));
                    code.Insert(j + 2, new CodeInstruction(OpCodes.Call, recolor));
                    return code;
                }
            }
            throw new InvalidOperationException("Advanced RimTalk could not locate the native input caret color.");
        }

        private static bool Loads(CodeInstruction instruction, float value) =>
            instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float loaded && loaded == value;

        private static Color CaretColor(Color original, GUIStyle style)
        {
            if (!ArtiUi.IsNative || style == null ||
                (style.name != "AdvancedRimTalk.ArtiInput" && style.name != "AdvancedRimTalk.NativeArea")) return original;
            Color ink = ArtiUi.Ink;
            ink.a *= GUI.color.a;
            return ink;
        }
    }
}
#endif
