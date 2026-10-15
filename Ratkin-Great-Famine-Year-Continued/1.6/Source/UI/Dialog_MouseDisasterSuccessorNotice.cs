using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class Dialog_MouseDisasterSuccessorNotice : Window
    {
        public const string WorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3807233439";

        public override Vector2 InitialSize => new Vector2(620f, 360f);

        public Dialog_MouseDisasterSuccessorNotice()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = true;
            closeOnCancel = true;
            doCloseX = true;
            doCloseButton = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 36f), "MouseDisaster_SuccessorNotice_Label".Translate());
            Text.Font = GameFont.Small;

            string intro = "MouseDisaster_SuccessorNotice_Text".Translate();
            string search = "MouseDisaster_SuccessorNotice_Search".Translate();
            float y = 44f;
            float introHeight = Text.CalcHeight(intro, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, introHeight), intro);
            y += introHeight + 12f;

            float linkHeight = Text.CalcHeight(WorkshopUrl, inRect.width);
            Rect link = new Rect(0f, y, inRect.width, linkHeight);
            Widgets.Label(link, WorkshopUrl);
            Widgets.DrawHighlightIfMouseover(link);
            if (Widgets.ButtonInvisible(link))
            {
                OpenWorkshopPage();
            }

            y += linkHeight + 12f;
            Widgets.Label(new Rect(0f, y, inRect.width, inRect.height - CloseButSize.y - 12f - y), search);

            Rect close = new Rect(inRect.width / 2f - CloseButSize.x / 2f, inRect.height - CloseButSize.y, CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(close, "MouseDisaster_SuccessorNotice_Close".Translate()))
            {
                Dismiss();
                Close();
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            Dismiss();
        }

        public static bool ShouldShow =>
            MouseDisasterMod.Settings != null && !MouseDisasterMod.Settings.successorNoticeDismissed;

        public static void TryOpen()
        {
            if (!ShouldShow || Find.WindowStack == null || Find.WindowStack.IsOpen<Dialog_MouseDisasterSuccessorNotice>())
            {
                return;
            }

            Find.WindowStack.Add(new Dialog_MouseDisasterSuccessorNotice());
        }

        public static void Dismiss()
        {
            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            if (settings == null || settings.successorNoticeDismissed)
            {
                return;
            }

            settings.successorNoticeDismissed = true;
            settings.Write();
        }

        public static void OpenWorkshopPage()
        {
            Application.OpenURL(WorkshopUrl);
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
    public static class Patch_MainMenuDrawer_SuccessorNotice
    {
        public static void Postfix()
        {
            if (!Dialog_MouseDisasterSuccessorNotice.ShouldShow || LongEventHandler.AnyEventNowOrWaiting || Find.WindowStack == null)
            {
                return;
            }

            Dialog_MouseDisasterSuccessorNotice.TryOpen();
        }
    }
}
