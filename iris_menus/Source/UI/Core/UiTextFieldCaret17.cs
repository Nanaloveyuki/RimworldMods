#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace IrisMenus.UI
{
    // UnityStubs ignores GUISettings.cursorColor; preserve its native editing/IME geometry.
    [HarmonyPatch(typeof(GUI), "DrawTextFieldChrome")]
    internal static class UiTextFieldCaret17
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            ConstructorInfo color = AccessTools.Constructor(typeof(Color), new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
            MethodInfo caret = AccessTools.Method(typeof(UiTextFieldCaret17), nameof(CaretColor));
            int matches = 0;
            for (int i = 4; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Newobj || !Equals(code[i].operand, color) ||
                    !Loads(code[i - 4], 0.92f) || !Loads(code[i - 3], 0.92f) ||
                    !Loads(code[i - 2], 0.92f) || !Loads(code[i - 1], 0.9f)) continue;
                CodeInstruction style = new CodeInstruction(OpCodes.Ldarg_S, (byte)5);
                style.labels.AddRange(code[i].labels);
                style.blocks.AddRange(code[i].blocks);
                code[i] = style;
                code.Insert(++i, new CodeInstruction(OpCodes.Call, caret));
                matches++;
            }
            if (matches != 1) throw new InvalidOperationException("IrisMenus could not locate the native text-field caret color.");
            return code;
        }

        private static bool Loads(CodeInstruction instruction, float value) =>
            instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float loaded && loaded == value;

        private static Color CaretColor(float r, float g, float b, float a, GUIStyle style)
        {
            if (HostedGuiTheme.TryGetInputCaretColor(style, out Color hosted)) return hosted;
            if (style == null || style.name != "IrisMenusTextField") return new Color(r, g, b, a);
            Color ink = style.focused.textColor;
            ink.a *= GUI.color.a;
            return ink;
        }
    }
}
#endif
