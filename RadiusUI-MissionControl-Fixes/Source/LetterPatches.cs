using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RadiusUI.MissionControl.Fixes
{
    internal static class LetterPatches
    {
        private static AccessTools.FieldRef<object> settings;
        private static AccessTools.FieldRef<object, bool> panelEnabled;
        private static AccessTools.FieldRef<object, bool> lettersEnabled;
        private static Action<float> setStackBottom;
        private static FieldInfo styledLettersField;

        // The drawing owner and the interception gate must make the same decision.
        internal static bool StyledLettersActive(object currentSettings)
        {
            return panelEnabled(currentSettings) && lettersEnabled(currentSettings);
        }

        internal static void Install(Harmony harmony)
        {
            Type mod = RequireType("RadiusUI.MissionControl.StyledWarningsMod");
            Type configuration = RequireType("RadiusUI.MissionControl.StyledWarningsSettings");
            Type renderer = RequireType("RadiusUI.MissionControl.LetterRenderer");
            styledLettersField = RequireField(configuration, "enableLetters");
            settings = AccessTools.StaticFieldRefAccess<object>(RequireField(mod, "Settings"));
            panelEnabled = AccessTools.FieldRefAccess<object, bool>(RequireField(configuration, "enablePanel"));
            lettersEnabled = AccessTools.FieldRefAccess<object, bool>(styledLettersField);
            setStackBottom = AccessTools.MethodDelegate<Action<float>>(
                AccessTools.PropertySetter(renderer, "StackBottom") ?? throw new MissingMethodException(renderer.FullName, "set_StackBottom"));

            MethodInfo drawButton = RequireMethod(renderer, "DrawButton");
            MethodInfo drawStack = RequireMethod(renderer, "DrawStack");
            MethodInfo buttonGate = RequireMethod(RequireType("RadiusUI.MissionControl.Patch_Letter_DrawButtonAt"), "Prefix");
            MethodInfo hoverGate = RequireMethod(RequireType("RadiusUI.MissionControl.Patch_Letter_CheckForMouseOverTextAt"), "Prefix");
            MethodInfo layoutGate = RequireMethod(RequireType("RadiusUI.MissionControl.Patch_LetterStack_LettersOnGUI"), "Postfix");

            // Validate every affected method before installing any patch. An upstream
            // implementation change must not leave only half the routing fixed.
            EnvelopeClickTranspiler(PatchProcessor.GetOriginalInstructions(drawButton));
            StyledGateTranspiler(PatchProcessor.GetOriginalInstructions(buttonGate));
            StyledGateTranspiler(PatchProcessor.GetOriginalInstructions(hoverGate));
            StyledGateTranspiler(PatchProcessor.GetOriginalInstructions(layoutGate));

            harmony.Patch(drawButton, transpiler: PatchMethod(nameof(EnvelopeClickTranspiler)));
            harmony.Patch(buttonGate, transpiler: PatchMethod(nameof(StyledGateTranspiler)));
            harmony.Patch(hoverGate, transpiler: PatchMethod(nameof(StyledGateTranspiler)));
            harmony.Patch(layoutGate, transpiler: PatchMethod(nameof(StyledGateTranspiler)));
            harmony.Patch(drawStack, prefix: PatchMethod(nameof(DrawStackPrefix)));
        }

        internal static bool DrawStackPrefix(float anchorY)
        {
            object currentSettings = settings();
            if (StyledLettersActive(currentSettings))
                return true;

            // With the Radius HUD enabled there is no vanilla GlobalControls call
            // left to draw unstyled letters. Put them above the main-button bar.
            setStackBottom(anchorY);
            if (panelEnabled(currentSettings) && Find.LetterStack != null)
                Find.LetterStack.LettersOnGUI(UI.screenHeight - 45f);
            return false;
        }

        internal static IEnumerable<CodeInstruction> StyledGateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int matches = 0;
            MethodInfo replacement = AccessTools.Method(typeof(LetterPatches), nameof(StyledLettersActive));
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.opcode != OpCodes.Ldfld || !Equals(instruction.operand, styledLettersField))
                    continue;
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                matches++;
            }
            if (matches != 1)
                throw new InvalidOperationException("Unsupported Mission Control letter gate: expected one enableLetters read.");
            return code;
        }

        internal static IEnumerable<CodeInstruction> EnvelopeClickTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo button = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonInvisible), new[] { typeof(Rect), typeof(bool) });
            MethodInfo jump = AccessTools.Method(typeof(CameraJumper), nameof(CameraJumper.TryJumpAndSelect),
                new[] { typeof(RimWorld.Planet.GlobalTargetInfo), typeof(CameraJumper.MovementMode) });
            int envelopeButton = -1;
            int jumpIndex = -1;
            int buttons = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].Calls(button) && ++buttons == 2)
                    envelopeButton = i;
                if (code[i].Calls(jump))
                {
                    if (jumpIndex >= 0)
                        throw new InvalidOperationException("Unsupported Mission Control letter handler: multiple camera jumps.");
                    jumpIndex = i;
                }
            }
            if (buttons != 2 || envelopeButton < 0 || jumpIndex <= envelopeButton)
                throw new InvalidOperationException("Unsupported Mission Control envelope click handler.");

            int start = envelopeButton + 1;
            while (start < jumpIndex && code[start].opcode != OpCodes.Brfalse && code[start].opcode != OpCodes.Brfalse_S)
                start++;
            start++;
            MethodInfo currentEvent = AccessTools.PropertyGetter(typeof(Event), nameof(Event.current));
            int end = jumpIndex + 1;
            while (end < code.Count && !code[end].Calls(currentEvent))
                end++;
            if (start >= jumpIndex || end >= code.Count)
                throw new InvalidOperationException("Unsupported Mission Control envelope click boundaries.");

            // Remove the complete target/Collapse decision, not just the camera call.
            // Keep the surrounding GUI.Button and Event.Use lifecycle untouched.
            var entry = new CodeInstruction(OpCodes.Ldarg_0);
            entry.labels.AddRange(code[start].labels);
            entry.blocks.AddRange(code[start].blocks);
            code.RemoveRange(start, end - start);
            code.InsertRange(start, new[]
            {
                entry,
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Letter), nameof(Letter.OpenLetter)))
            });
            return code;
        }

        private static HarmonyMethod PatchMethod(string name)
        {
            return new HarmonyMethod(typeof(LetterPatches), name);
        }

        private static Type RequireType(string name)
        {
            return AccessTools.TypeByName(name) ?? throw new TypeLoadException(name);
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            return AccessTools.DeclaredMethod(type, name) ?? throw new MissingMethodException(type.FullName, name);
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
        }
    }
}
