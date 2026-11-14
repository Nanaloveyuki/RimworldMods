using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    // 远行队路径在存档中按具体类型恢复
    public sealed class CaravanArrivalAction_RHAH_SoupKitchen : CaravanArrivalAction
    {
        WorldObject_RHAH_SoupKitchen site;

        public CaravanArrivalAction_RHAH_SoupKitchen() { }
        internal CaravanArrivalAction_RHAH_SoupKitchen(WorldObject_RHAH_SoupKitchen site) { this.site = site; }

        public override string Label => "RHAH_SoupKitchen_Visit".Translate();
        public override string ReportString => "RHAH_SoupKitchen_Report".Translate();

        public override FloatMenuAcceptanceReport StillValid(RimWorld.Planet.Caravan caravan, PlanetTile destinationTile)
        {
            FloatMenuAcceptanceReport report = base.StillValid(caravan, destinationTile);
            return report && site != null && site.Tile == destinationTile && site.CanVisit(caravan);
        }

        public override void Arrived(RimWorld.Planet.Caravan caravan)
        {
            site?.Visit(caravan);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "site");
        }
    }
}
