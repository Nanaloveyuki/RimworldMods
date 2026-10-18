using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterPhase2Utility
    {

        public static bool TryCreateIntelSite(Map map, MouseDisasterIntelSiteKind kind, out Site site, out string failureReason)
        {
            site = null;
            failureReason = string.Empty;
            if (map == null || Find.World == null)
            {
                failureReason = "MouseDisaster_UI_WorldUnavailable".Translate().Resolve();
                return false;
            }

            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, 5, 22))
            {
                failureReason = "MouseDisaster_UI_IntelTileMissing".Translate().Resolve();
                return false;
            }

            switch (kind)
            {
                case MouseDisasterIntelSiteKind.Treasure:
                    site = SiteMaker.MakeSite(DefDatabase<SitePartDef>.GetNamed("ItemStash"), tile, null, ifHostileThenMustRemainHostile: false);
                    break;
                case MouseDisasterIntelSiteKind.StructureCluster:
                    site = SiteMaker.MakeSite(DefDatabase<SitePartDef>.GetNamed("Outpost"), tile, Find.FactionManager.RandomEnemyFaction());
                    break;
                case MouseDisasterIntelSiteKind.SmallSettlement:
                    site = SiteMaker.MakeSite(DefDatabase<SitePartDef>.GetNamed("BanditCamp"), tile, Find.FactionManager.RandomEnemyFaction());
                    break;
            }

            if (site == null)
            {
                failureReason = "MouseDisaster_UI_IntelSiteFailed".Translate().Resolve();
                return false;
            }

            Find.WorldObjects.Add(site);
            return true;
        }
    }
}
