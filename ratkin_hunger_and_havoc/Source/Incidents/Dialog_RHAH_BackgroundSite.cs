using HungerAndHavoc.Core;
using RimWorld;
using UnityEngine;
using Verse;
using WorldCaravan = RimWorld.Planet.Caravan;

namespace HungerAndHavoc.Incidents
{
    internal sealed class Dialog_RHAH_BackgroundSite : Window
    {
        readonly WorldObject_RHAH_BackgroundSite site;
        readonly WorldCaravan caravan;
        public override Vector2 InitialSize => new Vector2(620f, 560f);

        internal Dialog_RHAH_BackgroundSite(WorldObject_RHAH_BackgroundSite site, WorldCaravan caravan)
        {
            this.site = site;
            this.caravan = caravan;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!site.AtSite(caravan)) { Close(); return; }
            Listing_Standard list = new Listing_Standard();
            list.Begin(new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 50f));
            list.Label(site.Label);
            list.Label(site.GetInspectString());
            RHAH_BackgroundOptions options = RHAH_Mod.Settings.background;
            if (site.JoinPending)
            {
                if (site.CanRecruit && list.ButtonText("RHAH_Background_AcceptWounded".Translate())) Resolve(site.ResolveJoin(caravan, true));
                if (list.ButtonText("RHAH_Background_DeclineWounded".Translate())) Resolve(site.ResolveJoin(caravan, false));
            }
            else if (site.Story == RHAH_BackgroundStory.Memorial && !site.WaitingForNames)
            {
                Supply(list, false, false, options.memorialSteel, options.materialsGoodwill);
                Supply(list, false, true, options.memorialWood, options.materialsGoodwill);
                if (list.ButtonText("RHAH_Background_SearchNames".Translate(options.namesUndergroundGoodwill, options.namesWarlordGoodwill)))
                    Resolve(site.SearchNames(caravan));
                Supply(list, true, false, options.restorationSteel, options.restorationGoodwill);
                Supply(list, true, true, options.restorationWood, options.restorationGoodwill);
            }
            list.End();
        }

        void Supply(Listing_Standard list, bool restore, bool wood, int count, int goodwill)
        {
            string key = restore ? "Restore" : "Supply";
            if (list.ButtonText(("RHAH_Background_" + key).Translate(count, (wood ? ThingDefOf.WoodLog : ThingDefOf.Steel).LabelCap, goodwill)))
                Resolve(site.Supply(caravan, restore, wood));
        }

        void Resolve(bool success)
        {
            if (success) Close();
            else Messages.Message("RHAH_Background_InteractionUnavailable".Translate(), MessageTypeDefOf.RejectInput);
        }
    }
}
