using HarmonyLib;
using RimWorld;
using Verse;

namespace Odf.PrisonerNames
{
    public static class NamePatches
    {
        static Harmony harmony;

        public static void Install()
        {
            if (harmony != null) return;
            harmony = new Harmony("odf.nanaloveyuki.prisonernames");
            harmony.PatchAll(typeof(NamePatches).Assembly);
        }

        static bool TryAlias(Pawn pawn, out string alias)
        {
            alias = null;
            GameComponent_PrisonerNames component = GameComponent_PrisonerNames.Current;
            if (component == null) return false;
            alias = component.GetAlias(pawn);
            return !string.IsNullOrEmpty(alias);
        }

        static string Title(Pawn pawn)
        {
            if (pawn.story == null || pawn.IsSubhuman || pawn.story.TitleShortCap.NullOrEmpty()) return null;
            return pawn.story.TitleShortCap;
        }

        static string ColoredName(string alias)
        {
            return alias.Colorize(ColoredText.NameColor);
        }

        [HarmonyPatch(typeof(Pawn), "get_LabelShort")]
        static class LabelShortPatch
        {
            static void Postfix(Pawn __instance, ref string __result)
            {
                string alias;
                if (TryAlias(__instance, out alias)) __result = alias;
            }
        }

        [HarmonyPatch(typeof(Pawn), "get_LabelNoCount")]
        static class LabelNoCountPatch
        {
            static void Postfix(Pawn __instance, ref string __result)
            {
                string alias;
                if (!TryAlias(__instance, out alias)) return;
                string title = Title(__instance);
                __result = alias;
                if (title != null) __result += (", " + title).Colorize(ColoredText.SubtleGrayColor);
            }
        }

        [HarmonyPatch(typeof(Pawn), "get_LabelNoCountColored")]
        static class LabelNoCountColoredPatch
        {
            static void Postfix(Pawn __instance, ref TaggedString __result)
            {
                string alias;
                if (!TryAlias(__instance, out alias)) return;
                string title = Title(__instance);
                string text = ColoredName(alias);
                if (title != null) text += (", " + title).Colorize(ColoredText.SubtleGrayColor);
                __result = text;
            }
        }

        [HarmonyPatch(typeof(Pawn), "get_NameShortColored")]
        static class NameShortColoredPatch
        {
            static void Postfix(Pawn __instance, ref TaggedString __result)
            {
                string alias;
                if (TryAlias(__instance, out alias)) __result = ColoredName(alias);
            }
        }

        [HarmonyPatch(typeof(Pawn), "get_NameFullColored")]
        static class NameFullColoredPatch
        {
            static void Postfix(Pawn __instance, ref TaggedString __result)
            {
                string alias;
                if (TryAlias(__instance, out alias)) __result = ColoredName(alias);
            }
        }
    }
}
