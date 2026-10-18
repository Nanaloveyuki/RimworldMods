using System;
using Verse;

namespace Odf.PrisonerNames
{
    public sealed class PrisonerNamesSettings : ModSettings
    {
        internal string PolicyText = string.Empty;
        internal string ActiveSource = string.Empty;
        internal string DraftSource = string.Empty;
        internal string EventSource = string.Empty;
        internal string EventDraft = string.Empty;
        internal bool Enabled = true;
        internal bool CustomMode;

        public void Initialize()
        {
            if (!string.IsNullOrEmpty(PolicyText) || !string.IsNullOrEmpty(ActiveSource)
                || !string.IsNullOrEmpty(DraftSource) || !string.IsNullOrEmpty(EventSource)) return;
            PolicyText = GameComponent_PrisonerNames.PolicyTextFor(NamePolicy.CreateDefault());
            ActiveSource = DraftSource = GameComponent_PrisonerNames.ConfigSource();
            EventSource = EventDraft = NamingRules.DefaultEvents();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref PolicyText, "policyText", string.Empty);
            Scribe_Values.Look(ref ActiveSource, "activeSource", string.Empty);
            Scribe_Values.Look(ref DraftSource, "draftSource", string.Empty);
            Scribe_Values.Look(ref EventSource, "eventSource", string.Empty);
            Scribe_Values.Look(ref EventDraft, "eventDraft", string.Empty);
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref CustomMode, "customMode", false);
            base.ExposeData();
        }

        internal NamePolicy Policy()
        {
            NamePolicy value = GameComponent_PrisonerNames.ParsePolicyFor(PolicyText);
            try { value.ValidateNaming(); }
            catch (Exception) { value = NamePolicy.CreateDefault(); }
            return value;
        }
    }
}
