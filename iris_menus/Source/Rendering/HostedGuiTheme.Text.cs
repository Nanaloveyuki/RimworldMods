#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using UnityEngine;
using UnityStubs.Gui;

namespace IrisMenus
{
    internal sealed partial class HostedGuiTheme
    {
        [ThreadStatic] private static TextTint textTint;
        private static Type textBackendType;
        private readonly ContrastEntry[] contrastCache = new ContrastEntry[128];
        private int contrastCount, contrastNext;
        private Color contrastSurface, contrastRaised, contrastSunken;
        private float darkTextLimit, lightTextLimit;

        internal static void InstallText(Harmony harmony)
        {
            textBackendType = AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiGuiBackend")
                ?? throw new InvalidOperationException("IrisMenus could not locate the native GUI text backend.");
            MethodInfo backend = RequireTextMethod(textBackendType, nameof(IGuiBackend.DrawText));
            harmony.Patch(backend, transpiler: new HarmonyMethod(typeof(HostedGuiTheme), nameof(PatchTextPieceTint)));
            var textCalls = new HarmonyMethod(typeof(HostedGuiTheme), nameof(PatchTextDrawCalls));
            harmony.Patch(RequireTextMethod(typeof(GUIStyle), "DrawTextIn"), transpiler: textCalls);
            harmony.Patch(RequireTextMethod(typeof(GUI), "DrawTextFieldChrome"), transpiler: textCalls);
            harmony.Patch(RequireTextMethod(typeof(GUI), "DrawTextAreaContent"), transpiler: textCalls);
        }

        private static MethodInfo RequireTextMethod(Type type, string name) => AccessTools.Method(type, name)
            ?? throw new InvalidOperationException("IrisMenus could not locate " + type.FullName + "." + name + ".");

        private static IEnumerable<CodeInstruction> PatchTextDrawCalls(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            MethodInfo draw = RequireTextMethod(typeof(IGuiBackend), nameof(IGuiBackend.DrawText));
            MethodInfo replacement = RequireTextMethod(typeof(HostedGuiTheme), nameof(DrawHostedText));
            int styleArgument = __originalMethod.DeclaringType == typeof(GUIStyle) ? 0 :
                __originalMethod.Name == "DrawTextFieldChrome" ? 5 : 4;
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (!instruction.Calls(draw)) { yield return instruction; continue; }
                // Append only the drawing style; control IDs, editor strings and layout stay native.
                var style = new CodeInstruction(OpCodes.Ldarg, styleArgument);
                style.labels.AddRange(instruction.labels);
                style.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear();
                instruction.blocks.Clear();
                yield return style;
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                yield return instruction;
                matches++;
            }
            if (matches != 1) throw new InvalidOperationException("IrisMenus could not locate the final text draw in " + __originalMethod.Name + ".");
        }

        private static IEnumerable<CodeInstruction> PatchTextPieceTint(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo pack = RequireTextMethod(textBackendType, "ToCol");
            MethodInfo replacement = RequireTextMethod(typeof(HostedGuiTheme), nameof(PackHostedText));
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(pack))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    matches++;
                }
                yield return instruction;
            }
            if (matches != 1) throw new InvalidOperationException("IrisMenus could not locate the native text piece tint.");
        }

        private static void DrawHostedText(IGuiBackend backend, Rect rect, string text, int fontIndex,
            float fontSize, FontStyle fontStyle, TextAnchor anchor, bool wordWrap, Color color,
            Rect clipRect, bool richText, GUIStyle style)
        {
            HostedGuiTheme owner = current;
            if (owner == null || owner.bypass || style?.name == "IrisMenusTextField" || backend.GetType() != textBackendType)
            {
                backend.DrawText(rect, text, fontIndex, fontSize, fontStyle, anchor, wordWrap, color, clipRect, richText);
                return;
            }
            owner.PrepareTextContrast();
            TextTint previous = textTint;
            textTint = new TextTint(owner, color.a);
            // Native rich color tags REPLACE the base color, including alpha. Apply the final
            // caller/host opacity to every parsed piece once, without rewriting any text/indices.
            color.a = 1f;
            try { backend.DrawText(rect, text, fontIndex, fontSize, fontStyle, anchor, wordWrap, color, clipRect, richText); }
            finally { textTint = previous; }
        }

        private static ImColor8 PackHostedText(in Color color)
        {
            Color result = color;
            TextTint tint = textTint;
            if (tint.Owner != null)
            {
                result = tint.Owner.ContrastText(color);
                result.a = color.a * tint.Opacity;
            }
            return new ImColor8(TextByte(result.r), TextByte(result.g), TextByte(result.b), TextByte(result.a));
        }

        private void PrepareTextContrast()
        {
            if (contrastSurface == palette.Surface && contrastRaised == palette.Raised && contrastSunken == palette.Sunken) return;
            contrastSurface = palette.Surface;
            contrastRaised = palette.Raised;
            contrastSunken = palette.Sunken;
            contrastCount = contrastNext = 0;
            float surface = TextLuminance(contrastSurface), raised = TextLuminance(contrastRaised), sunken = TextLuminance(contrastSunken);
            // Satisfy all common hosted surfaces, including input and section backgrounds.
            float minimum = Mathf.Min(surface, Mathf.Min(raised, sunken));
            float maximum = Mathf.Max(surface, Mathf.Max(raised, sunken));
            darkTextLimit = (minimum + 0.05f) / 4.5f - 0.05f;
            lightTextLimit = (maximum + 0.05f) * 4.5f - 0.05f;
            // During theme interpolation the surfaces can straddle the narrow luminance
            // interval where no single color can satisfy all three. Prioritize the actual
            // hosted surface rather than leave low-contrast text unchanged in that interval.
            if (darkTextLimit < 0f && lightTextLimit > 1f)
            {
                darkTextLimit = (surface + 0.05f) / 4.5f - 0.05f;
                lightTextLimit = (surface + 0.05f) * 4.5f - 0.05f;
            }
        }

        private Color ContrastText(Color color)
        {
            // Contrast is an RGB requirement; never increase intentional transparency to
            // force a composited ratio that the caller's alpha cannot physically achieve.
            if (color.a <= 0f) return color;
            uint key = (uint)(TextByte(color.r) << 16 | TextByte(color.g) << 8 | TextByte(color.b));
            for (int i = 0; i < contrastCount; i++)
                if (contrastCache[i].Rgb == key) return contrastCache[i].Color;
            Color packed = new Color(((key >> 16) & 255) / 255f, ((key >> 8) & 255) / 255f, (key & 255) / 255f, 1f);
            float luminance = TextLuminance(packed);
            Color result = packed;
            if (luminance > darkTextLimit && luminance < lightTextLimit)
            {
                bool canDarken = darkTextLimit >= 0f, canLighten = lightTextLimit <= 1f;
                if (canDarken && canLighten)
                {
                    Color dark = FindContrastingText(packed, false);
                    Color light = FindContrastingText(packed, true);
                    result = TextDistance(packed, dark) <= TextDistance(packed, light) ? dark : light;
                }
                else if (canDarken) result = FindContrastingText(packed, false);
                else if (canLighten) result = FindContrastingText(packed, true);
            }
            contrastCache[contrastNext] = new ContrastEntry(key, result);
            contrastNext = (contrastNext + 1) % contrastCache.Length;
            if (contrastCount < contrastCache.Length) contrastCount++;
            return result;
        }

        private Color FindContrastingText(Color source, bool lighten)
        {
            // Mixing toward an achromatic endpoint retains the original hue. Search actual
            // packed RGB so rounding cannot lower the promised contrast below 4.5:1.
            Color end = lighten ? Color.white : Color.black;
            Color best = end;
            float low = 0f, high = 1f;
            for (int i = 0; i < 12; i++)
            {
                float amount = (low + high) * 0.5f;
                Color candidate = Color.Lerp(source, end, amount);
                candidate.r = TextByte(candidate.r) / 255f;
                candidate.g = TextByte(candidate.g) / 255f;
                candidate.b = TextByte(candidate.b) / 255f;
                float luminance = TextLuminance(candidate);
                bool sufficient = lighten ? luminance >= lightTextLimit : luminance <= darkTextLimit;
                if (sufficient) { high = amount; best = candidate; }
                else low = amount;
            }
            return best;
        }

        private static float TextDistance(Color a, Color b) =>
            (a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b);

        private static float TextLuminance(Color color) =>
            0.2126f * TextLinear(color.r) + 0.7152f * TextLinear(color.g) + 0.0722f * TextLinear(color.b);

        private static float TextLinear(float value) => value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        private static byte TextByte(float value) => (byte)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f);

        private readonly struct TextTint
        {
            internal readonly HostedGuiTheme Owner;
            internal readonly float Opacity;
            internal TextTint(HostedGuiTheme owner, float opacity) { Owner = owner; Opacity = opacity; }
        }

        private readonly struct ContrastEntry
        {
            internal readonly uint Rgb;
            internal readonly Color Color;
            internal ContrastEntry(uint rgb, Color color) { Rgb = rgb; Color = color; }
        }
    }
}
#endif
