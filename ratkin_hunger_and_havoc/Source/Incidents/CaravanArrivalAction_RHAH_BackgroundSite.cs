using RimWorld;
using RimWorld.Planet;
using Verse;
using WorldCaravan = RimWorld.Planet.Caravan;

namespace HungerAndHavoc.Incidents
{
    // 远行队路径按存档中的类型名恢复
    public sealed class CaravanArrivalAction_RHAH_BackgroundSite : CaravanArrivalAction
    {
        WorldObject_RHAH_BackgroundSite site;

        public CaravanArrivalAction_RHAH_BackgroundSite() { }
        internal CaravanArrivalAction_RHAH_BackgroundSite(WorldObject_RHAH_BackgroundSite site) { this.site = site; }
        public override string Label => "RHAH_Background_Visit".Translate();
        public override string ReportString => "RHAH_Background_VisitReport".Translate();

        public override FloatMenuAcceptanceReport StillValid(WorldCaravan caravan, PlanetTile destinationTile)
        {
            return base.StillValid(caravan, destinationTile) && site != null && site.Tile == destinationTile && site.CanVisit(caravan);
        }

        public override void Arrived(WorldCaravan caravan) { site?.Visit(caravan); }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "site");
        }
    }
}
