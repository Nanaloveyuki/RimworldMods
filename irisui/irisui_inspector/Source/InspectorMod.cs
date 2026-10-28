using HarmonyLib;
using IrisUI.Framework;
using UnityEngine;
using Verse;

namespace IrisUI.Inspector
{
    public sealed class InspectorMod : Mod
    {
        internal static InspectorMod Instance;
        internal static InspectorSettings Settings;
        private readonly Listing_Standard listing = new Listing_Standard();

        public InspectorMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<InspectorSettings>();
            IrisLayout.RegisterInspector(InspectorController.GetRequest);
            new Harmony("Nanaloveyuki.IrisUI.Inspector").PatchAll(typeof(InspectorMod).Assembly);
        }

        public override string SettingsCategory() => "IUI_InspectorName".Translate();

        public override void DoSettingsWindowContents(Rect rect)
        {
            listing.Begin(rect);
            listing.Label("IUI_InspectorMode".Translate());
            if (listing.RadioButton("IUI_InspectorMinimum".Translate(), Settings.Mode == InspectorMode.Minimum))
                InspectorController.SetMode(InspectorMode.Minimum);
            if (listing.RadioButton("IUI_InspectorNormal".Translate(), Settings.Mode == InspectorMode.Normal))
                InspectorController.SetMode(InspectorMode.Normal);
            if (listing.RadioButton("IUI_InspectorFullscreen".Translate(), Settings.Mode == InspectorMode.Fullscreen))
                InspectorController.SetMode(InspectorMode.Fullscreen);
            listing.End();
        }
    }

    public sealed class InspectorSettings : ModSettings
    {
        public InspectorMode Mode = InspectorMode.Normal;
        public InspectorMode CompactMode = InspectorMode.Normal;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref Mode, "mode", InspectorMode.Normal);
            Scribe_Values.Look(ref CompactMode, "compactMode", InspectorMode.Normal);
            if ((int)Mode < (int)InspectorMode.Minimum || (int)Mode > (int)InspectorMode.Fullscreen) Mode = InspectorMode.Normal;
            if (CompactMode != InspectorMode.Minimum && CompactMode != InspectorMode.Normal) CompactMode = InspectorMode.Normal;
            if (Mode != InspectorMode.Fullscreen) CompactMode = Mode;
        }
    }
}
