extern alias iris;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using iris::IrisMenus;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal sealed partial class RHAH_IrisMenusPages
    {
        void DrawDiagnosticPawns(Listing_Standard list)
        {
            if (Prefs.DevMode) DrawPawns(list);
        }

        void DrawDiagnosticEvents(Listing_Standard list)
        {
            if (Prefs.DevMode) DrawDevEvents(list);
            else RHAH_IrisMenusWidgets.Quote(list, "dev-events-note", "RHAH_Menu_DevEvents_Note".Translate());
        }

        IEnumerable<MenuSearchEntry> SearchDiagnosticPawns()
        {
            if (Prefs.DevMode)
                foreach (MenuSearchEntry entry in SearchPawns()) yield return entry;
        }

        IEnumerable<MenuSearchEntry> SearchDiagnosticEvents()
        {
            if (Prefs.DevMode)
                foreach (MenuSearchEntry entry in SearchDevEvents()) yield return entry;
        }
    }
}
