using IrisMenus.UI;
using Verse;

namespace IrisMenus
{
    public static partial class MenuRegistry
    {
        // Game thread only; no queued messages survive closing the fullscreen host.
        public static bool ShowToast(string message, UiToastTone tone = UiToastTone.Neutral)
        {
            MenuWindow window = Find.WindowStack?.WindowOfType<MenuWindow>();
            return window != null && window.ShowToast(message, tone);
        }
    }
}
