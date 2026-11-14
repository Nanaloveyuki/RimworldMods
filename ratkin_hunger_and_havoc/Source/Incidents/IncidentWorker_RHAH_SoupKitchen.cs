using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal sealed class IncidentWorker_RHAH_SoupKitchen : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            return RHAH_Runtime.AllowsNewContent && map != null && map.IsPlayerHome && map.Tile.Valid &&
                RHAH_Mod.Settings?.IsIncidentEnabled("I-058") != false &&
                Find.FactionManager.AllFactionsListForReading.Exists(RHAH_SoupKitchen.EligibleFaction);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            if (!TileFinder.TryFindPassableTileWithTraversalDistance(map.Tile, 3, 8, out PlanetTile tile,
                candidate => !Find.WorldObjects.AnyWorldObjectAt(candidate))) return false;

            List<Faction> factions = Find.FactionManager.AllFactionsListForReading.FindAll(RHAH_SoupKitchen.EligibleFaction);
            Faction faction = factions.RandomElement();
            WorldObject_RHAH_SoupKitchen site = (WorldObject_RHAH_SoupKitchen)WorldObjectMaker.MakeWorldObject(
                DefDatabase<WorldObjectDef>.GetNamed("RHAH_SoupKitchen"));
            site.Tile = tile;
            site.SetFaction(faction);
            site.Configure(Rand.RangeInclusive(5, 20));
            Find.WorldObjects.Add(site);
            site.GetComponent<TimeoutComp>().StartTimeout(RHAH_SoupKitchen.DurationDays * GenDate.TicksPerDay);
            Find.LetterStack.ReceiveLetter("RHAH_Incident_SoupKitchen_Label".Translate(),
                "RHAH_Incident_SoupKitchen_Text".Translate(faction.Name) + "\n\n" +
                "RHAH_SoupKitchen_Terms".Translate(RHAH_SoupKitchen.DurationDays),
                LetterDefOf.PositiveEvent, new LookTargets(site), faction);
            return true;
        }
    }
}
