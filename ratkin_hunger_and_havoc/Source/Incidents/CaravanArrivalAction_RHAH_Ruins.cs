using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    // 远行队路径在存档中按具体类型恢复
    public sealed class CaravanArrivalAction_RHAH_Ruins : CaravanArrivalAction
    {
        WorldObject_RHAH_Ruins ruins;

        public CaravanArrivalAction_RHAH_Ruins() { }
        internal CaravanArrivalAction_RHAH_Ruins(WorldObject_RHAH_Ruins ruins) { this.ruins = ruins; }

        public override string Label => "RHAH_Background_Explore".Translate();
        public override string ReportString => "RHAH_Background_ExploreReport".Translate();

        public override FloatMenuAcceptanceReport StillValid(RimWorld.Planet.Caravan caravan, PlanetTile destinationTile)
        {
            FloatMenuAcceptanceReport report = base.StillValid(caravan, destinationTile);
            return report && ruins != null && ruins.Tile == destinationTile && ruins.CanExplore(caravan);
        }

        public override void Arrived(RimWorld.Planet.Caravan caravan)
        {
            ruins?.Explore(caravan);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ruins, "ruins");
        }
    }
}
