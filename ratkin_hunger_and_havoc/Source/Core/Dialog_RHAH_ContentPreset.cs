using HarmonyLib;
using HungerAndHavoc.Incidents;
using RimWorld;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Core
{
    internal sealed class Dialog_RHAH_ContentPreset : Window
    {
        Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(760f, 650f);

        internal Dialog_RHAH_ContentPreset()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 38f), "RHAH_ContentPreset_Title".Translate());
            Text.Font = GameFont.Small;
            Rect body = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 230f);
            string description = "RHAH_ContentPreset_Introduction".Translate();
            Rect view = new Rect(0f, 0f, body.width - 20f, Text.CalcHeight(description, body.width - 20f));
            Widgets.BeginScrollView(body, ref scroll, view);
            Widgets.Label(view, description);
            Widgets.EndScrollView();
            float y = body.yMax + 12f;
            for (int i = 0; i < 4; i++)
            {
                RHAH_ContentRating rating = (RHAH_ContentRating)i;
                string label = RHAH_IncidentRating.LabelKey(rating).Translate();
                if (rating == RHAH_ContentRating.General) label += "RHAH_ContentPreset_Recommended".Translate();
                if (Widgets.ButtonText(new Rect(inRect.x, y, inRect.width, 32f), label))
                {
                    RHAH_ContentPreset.Apply(RHAH_Mod.Settings, rating);
                    RHAH_Mod.Settings.Mod.WriteSettings();
                    Close();
                }
                y += 36f;
            }
            if (Widgets.ButtonText(new Rect(inRect.x, y, inRect.width, 28f), "RHAH_ContentPreset_Later".Translate()))
            {
                RHAH_Mod.Settings.contentPreset132Chosen = true;
                RHAH_Mod.Settings.Mod.WriteSettings();
                Close();
            }
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
    internal static class RHAH_ContentPresetPrompt
    {
        static void Postfix()
        {
            if (Current.ProgramState == ProgramState.Entry && RHAH_Mod.Settings?.contentPreset132Chosen == false &&
                Find.WindowStack != null && !Find.WindowStack.IsOpen<Dialog_RHAH_ContentPreset>())
                Find.WindowStack.Add(new Dialog_RHAH_ContentPreset());
        }
    }
}
