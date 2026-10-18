using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum KingdomMaterialSelectionPurpose { Handover, Publication, Copy }

    internal sealed class Window_RHAH_KingdomMaterials : Window
    {
        readonly KingdomStage stage;
        readonly int revision;
        readonly KingdomMaterialSelectionPurpose purpose;
        readonly List<Thing> items;
        readonly HashSet<Thing> selected = new HashSet<Thing>();
        Vector2 scroll;
        string errorKey;

        internal static void Open(KingdomStage stage, int revision, KingdomMaterialSelectionPurpose purpose)
        {
            if (!Find.WindowStack.IsOpen<Window_RHAH_KingdomMaterials>())
            {
                Find.WindowStack.Add(new Window_RHAH_KingdomMaterials(stage, revision, purpose));
            }
        }

        internal static void OpenCopy()
        {
            KingdomArcProgress progress = KingdomArcActions.Progress;
            if (KingdomArcRuntime.CanAct && KingdomArcRuntime.State.Invitation == KingdomInvitation.Accepted &&
                progress != null && KingdomArcActions.Home != null)
            {
                Open(progress.Stage, progress.Revision, KingdomMaterialSelectionPurpose.Copy);
            }
        }

        internal Window_RHAH_KingdomMaterials(KingdomStage stage, int revision, KingdomMaterialSelectionPurpose purpose)
        {
            this.stage = stage;
            this.revision = revision;
            this.purpose = purpose;
            items = KingdomMaterials.Held(KingdomArcActions.Home);
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnCancel = true;
            closeOnAccept = false;
            doCloseX = true;
        }

        public override Vector2 InitialSize => new Vector2(720f, 620f);

        public override void DoWindowContents(Rect inRect)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f),
                    (purpose == KingdomMaterialSelectionPurpose.Copy ? "RHAH_MaterialSelection_CopyTitle" :
                        purpose == KingdomMaterialSelectionPurpose.Publication ? "RHAH_MaterialSelection_PublicationTitle" : "RHAH_MaterialSelection_HandoverTitle").Translate());
                Text.Font = GameFont.Small;
                float y = inRect.y + 42f;
                string help = (purpose == KingdomMaterialSelectionPurpose.Copy ? "RHAH_MaterialSelection_CopyHelp" :
                    purpose == KingdomMaterialSelectionPurpose.Publication ? "RHAH_MaterialSelection_PublicationHelp" : "RHAH_MaterialSelection_HandoverHelp").Translate();
                DrawParagraph(inRect, ref y, help);
                if (purpose == KingdomMaterialSelectionPurpose.Publication)
                {
                    string privacy = KingdomArcActions.Progress.NamedConsent
                        ? "RHAH_MaterialSelection_NamedConsent" : "RHAH_MaterialSelection_Redacted";
                    DrawParagraph(inRect, ref y, privacy.Translate());
                }
                DrawParagraph(inRect, ref y, "RHAH_MaterialSelection_ChooseHelp".Translate());

                string error = errorKey == null ? null : errorKey.Translate().ToString();
                float errorHeight = error == null ? 0f : Text.CalcHeight(error, inRect.width);
                float footerY = inRect.yMax - 64f - errorHeight;
                Rect listRect = new Rect(inRect.x, y, inRect.width, Mathf.Max(30f, footerY - y - 8f));
                DrawItems(listRect);
                Widgets.Label(new Rect(inRect.x, footerY, inRect.width, 26f),
                    "RHAH_MaterialSelection_Count".Translate(selected.Count, items.Count));
                if (error != null)
                {
                    Widgets.Label(new Rect(inRect.x, footerY + 25f, inRect.width, errorHeight), error);
                }
                Rect cancel = new Rect(inRect.x, inRect.yMax - 32f, 150f, 32f);
                if (Widgets.ButtonText(cancel, "CancelButton".Translate())) Close();
                Rect accept = new Rect(inRect.xMax - 240f, inRect.yMax - 32f, 240f, 32f);
                string label = (purpose == KingdomMaterialSelectionPurpose.Copy ? "RHAH_MaterialSelection_MakeCopies" :
                    purpose == KingdomMaterialSelectionPurpose.Publication ? "RHAH_MaterialSelection_SendCopies" : "RHAH_MaterialSelection_HandOver").Translate();
                if (Widgets.ButtonText(accept, label, active: selected.Count > 0))
                {
                    if (TryAccept()) Close();
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
            }
        }

        static void DrawParagraph(Rect rect, ref float y, string text)
        {
            float height = Text.CalcHeight(text, rect.width);
            Widgets.Label(new Rect(rect.x, y, rect.width, height), text);
            y += height + 8f;
        }

        void DrawItems(Rect rect)
        {
            if (items.Count == 0)
            {
                Widgets.Label(rect, "RHAH_MaterialSelection_Empty".Translate());
                return;
            }
            float width = Mathf.Max(1f, rect.width - 18f);
            float height = 0f;
            for (int i = 0; i < items.Count; i++) height += RowHeight(items[i], width);
            Rect view = new Rect(0f, 0f, width, height);
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                float y = 0f;
                for (int i = 0; i < items.Count; i++)
                {
                    Thing item = items[i];
                    float rowHeight = RowHeight(item, width);
                    Rect row = new Rect(0f, y, width, rowHeight - 4f);
                    Widgets.DrawHighlightIfMouseover(row);
                    bool chosen = selected.Contains(item);
                    float titleHeight = Mathf.Max(26f, Text.CalcHeight(ItemLabel(item), width - 28f));
                    Widgets.CheckboxLabeled(new Rect(row.x, row.y, row.width, titleHeight), ItemLabel(item), ref chosen);
                    if (chosen) selected.Add(item); else selected.Remove(item);
                    Widgets.Label(new Rect(row.x + 28f, row.y + titleHeight + 2f, row.width - 28f, row.height - titleHeight - 2f), Provenance(item));
                    y += rowHeight;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        static string ItemLabel(Thing item)
        {
            Comp_RHAH_KingdomMaterial comp = item.TryGetComp<Comp_RHAH_KingdomMaterial>();
            return "RHAH_MaterialSelection_Item".Translate(comp.Number,
                ("RHAH_Kingdom_Material_" + comp.Kind).Translate(), item.LabelCap);
        }

        static string Provenance(Thing item)
        {
            Comp_RHAH_KingdomMaterial comp = item.TryGetComp<Comp_RHAH_KingdomMaterial>();
            return "RHAH_MaterialSelection_Provenance".Translate(
                ("RHAH_Kingdom_Source_" + comp.Source).Translate(),
                (comp.Redacted ? "RHAH_Kingdom_Redacted" : "RHAH_Kingdom_Unredacted").Translate());
        }

        static float RowHeight(Thing item, float width)
        {
            float titleHeight = Mathf.Max(26f, Text.CalcHeight(ItemLabel(item), Mathf.Max(1f, width - 28f)));
            return titleHeight + 2f + Text.CalcHeight(Provenance(item), Mathf.Max(1f, width - 28f)) + 8f;
        }

        internal bool TryAccept()
        {
            KingdomArcProgress progress = KingdomArcActions.Progress;
            if (!KingdomArcRuntime.CanAct || KingdomArcRuntime.State.Invitation != KingdomInvitation.Accepted || progress == null ||
                !progress.Matches(stage, revision) || (purpose != KingdomMaterialSelectionPurpose.Copy &&
                    (!KingdomArcActions.Narrator || !progress.ResultSent)))
            {
                errorKey = "RHAH_MaterialSelection_Stale";
                return false;
            }
            List<Thing> chosen = new List<Thing>();
            for (int i = 0; i < items.Count; i++) if (selected.Contains(items[i])) chosen.Add(items[i]);
            if (!KingdomMaterials.ValidateSelection(KingdomArcActions.Home, chosen))
            {
                errorKey = "RHAH_MaterialSelection_Missing";
                return false;
            }
            bool accepted = purpose == KingdomMaterialSelectionPurpose.Copy
                ? KingdomMaterials.CopyToHome(KingdomArcActions.Home, chosen)
                : purpose == KingdomMaterialSelectionPurpose.Publication
                    ? KingdomArcActions.BeginPublication(stage, revision, chosen)
                    : KingdomArcActions.Handover(stage, revision, chosen);
            errorKey = accepted ? null : "RHAH_MaterialSelection_Failed";
            return accepted;
        }
    }
}
