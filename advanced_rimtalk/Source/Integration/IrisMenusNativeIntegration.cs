#if ART_IRISMENUS
extern alias iris;
using System;
using System.Runtime.CompilerServices;
using AdvancedRimTalk.UI;
using iris::IrisMenus;
using iris::IrisMenus.UI;
using UnityEngine;
using Verse;

namespace AdvancedRimTalk.Integration
{
    internal static class IrisMenusNativeIntegration
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Register(AdvancedRimTalkMod owner)
        {
            MenuRegistry.RegisterPresentation(owner, "general", new MenuPresentation(context =>
                new NativePage(context, null, owner.DrawSettingsPage)));
            Register(owner, "arti-repl", owner.DrawArtiRepl);
            Register(owner, "arti-log", owner.DrawArtiLog);
            Register(owner, "arti-once", owner.DrawArtiOnce);
            Register(owner, "arti-editor", owner.DrawArtiEditor);
            Register(owner, "takeover-prompt-parts", owner.DrawTakeoverPromptParts);
            Register(owner, "prompt-preview", owner.DrawPromptPreview);
            Register(owner, "response-ignore", owner.DrawResponseSettings);
            Register(owner, "documentation", owner.DrawDocumentation, owner.Documentation.Focus);
        }

        private static void Register(AdvancedRimTalkMod owner, string id, Action<Rect> draw, Action<string> focus = null) =>
            MenuRegistry.RegisterPresentation(owner, id, new MenuPresentation(context => new NativePage(context, draw, null, focus)));

        private sealed class NativePage : MenuNativePage
        {
            private readonly ArtiNativeUi.NativeState state;
            private readonly Action<Rect> draw;
            private readonly Action<Listing_Standard> listing;
            private readonly Action<string> focus;
            private readonly MenuScrollView scroll;

            internal NativePage(UiContext context, Action<Rect> draw, Action<Listing_Standard> listing, Action<string> focus = null)
            {
                state = new ArtiNativeUi.NativeState(context);
                this.draw = draw;
                this.listing = listing;
                this.focus = focus;
                if (listing != null) scroll = new MenuScrollView(context);
            }

            public override void Draw(Rect rect)
            {
                ArtiNativeUi.NativeState previous = ArtiNativeUi.Current;
                bool previousNative = ArtiUi.IsNative;
                state.Begin();
                ArtiNativeUi.Current = state;
                ArtiUi.IsNative = true;
                using (state.Context.Clip(rect))
                {
                    try
                    {
                        Rect local = new Rect(0f, 0f, rect.width, rect.height);
                        if (listing != null) scroll.Draw(local, listing);
                        else draw(local);
                    }
                    finally { state.End(); ArtiNativeUi.Current = previous; ArtiUi.IsNative = previousNative; }
                }
            }
            public override void Focus(string anchor) => focus?.Invoke(anchor);
            public override void Dispose() { scroll?.Dispose(); state.Dispose(); }
        }
    }
}
#endif
