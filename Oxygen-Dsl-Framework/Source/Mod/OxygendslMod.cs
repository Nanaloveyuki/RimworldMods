using UnityEngine;
using Verse;

namespace Oxygendsl.Mod
{
    public sealed class OxygendslMod : Verse.Mod
    {
        public OxygendslMod(ModContentPack content) : base(content)
        {
            OxygenContentHost.Install();
            OxygenEvents.Install();
            OxygenEditorHost.Install(content);
        }
        public override string SettingsCategory() { return "Oxygendsl"; }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);
            DrawSettingsPage(list);
            list.End();
        }

        public void DrawSettingsPage(Listing_Standard list)
        {
            list.Label("Oxygendsl_SettingsIntro".Translate());
            list.Gap(12f);
            if (Current.ProgramState != ProgramState.Playing || GameComponent_Oxygendsl.Current == null)
                list.Label("Oxygendsl_NoGame".Translate());
            else if (list.ButtonText("Oxygendsl_OpenWindow".Translate()))
                OxygendslApi.OpenWindow();
        }
    }

    public static class OxygendslApi
    {
        public static GameComponent_Oxygendsl Runtime { get { return GameComponent_Oxygendsl.Current; } }
        public static void OpenWindow()
        {
            if (Current.ProgramState != ProgramState.Playing || Runtime == null) return;
            if (!Find.WindowStack.IsOpen<ScriptWindow>()) Find.WindowStack.Add(new ScriptWindow(Runtime));
        }
    }
}
