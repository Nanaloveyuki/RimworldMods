using System;
using HarmonyLib;
using IrisMenus.UI;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace IrisMenus
{
    internal sealed partial class HostedGuiTheme
    {
        private UiHoverCard helpCard;
        private Rect helpAnchorScreen;
        private string helpText = string.Empty;
        private bool helpObserved;
        private int helpPriority;
        private int helpSignalId;
        private string helpGetterText;
        private int helpGetterFrame = -1;
        private Func<string> helpGetter;

        private static void InstallHoverCards(Harmony harmony)
        {
#if RIMWORLD_1_7
            harmony.Patch(AccessTools.Method(typeof(TooltipHandler), nameof(TooltipHandler.TipRegion),
                new[] { typeof(Rect), typeof(TipSignal) }),
                prefix: new HarmonyMethod(typeof(HostedGuiTheme), nameof(HostedTipRegion)));
#endif
        }

        private void BeginHoverCards()
        {
            if (helpCard == null) helpCard = new UiHoverCard(context);
            helpObserved = false;
            helpPriority = int.MinValue;
        }

        private void EndHoverCards()
        {
            if (!context.IsRepaint) return;
            Rect anchor = helpObserved ? UiCoordinates.FromScreen(helpAnchorScreen) : default(Rect);
            helpCard.Observe(anchor, helpText);
        }

        internal void DrawHoverCards(Rect viewport)
        {
            if (helpCard == null || context == null) return;
            if (!context.IsRepaint)
                helpCard.Observe(UiCoordinates.FromScreen(helpAnchorScreen), helpText);
            float font = context.FontSize;
            FontStyle style = context.FontStyle;
            try
            {
                context.FontSize = 14f * context.Scale;
                context.FontStyle = FontStyle.Normal;
                helpCard.DrawOverlay(viewport, context.Scale);
            }
            finally { context.FontSize = font; context.FontStyle = style; }
        }

        internal void DismissHoverCards() => helpCard?.Dismiss();

        private void DisposeHoverCards()
        {
            helpCard?.Dismiss();
            helpCard = null;
            helpGetter = null;
            helpText = string.Empty;
            helpGetterText = null;
            helpGetterFrame = -1;
        }

        private static bool HostedTipRegion(Rect rect, TipSignal tip)
        {
            HostedGuiTheme owner = current;
            if (owner == null || owner.bypass || !owner.context.IsInputAllowed || !GUI.enabled ||
                !owner.context.Hit(rect) || SteamDeck.KeyboardShowing) return true;
            string content = tip.text;
            if (tip.textGetter != null)
            {
                if (owner.helpSignalId != tip.uniqueId || owner.helpGetter != tip.textGetter || owner.helpGetterFrame != owner.context.Frame)
                {
                    owner.helpSignalId = tip.uniqueId;
                    owner.helpGetter = tip.textGetter;
                    owner.helpGetterFrame = owner.context.Frame;
                    owner.helpGetterText = tip.textGetter() ?? string.Empty;
                }
                content = owner.helpGetterText;
            }
            // Terse labels retain the game's tooltip; only hosted explanations are adapted.
            if (string.IsNullOrEmpty(content) || content.Length < 60 && content.IndexOf('\n') < 0) return true;
            if (!owner.helpObserved || (int)tip.priority >= owner.helpPriority)
            {
                owner.helpObserved = true;
                owner.helpPriority = (int)tip.priority;
                owner.helpAnchorScreen = UiCoordinates.ToScreen(rect);
                owner.helpText = content;
            }
            return false;
        }
    }
}
