using RimWorld;
using Verse;

namespace LeadYourPet
{
    public class MouseEggState : IExposable
    {
        public Pawn Pawn;
        public Pawn CurrentMaster;
        public bool IsPet;
        public bool IsTravelStock;
        public bool SellAsPrisoner;
        public MouseEggSpecialSource SpecialSource;
        public bool TravelEggAggressionHandled;

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_References.Look(ref CurrentMaster, "currentMaster");
            Scribe_Values.Look(ref IsPet, "isPet");
            Scribe_Values.Look(ref IsTravelStock, "isTravelStock");
            Scribe_Values.Look(ref SellAsPrisoner, "sellAsPrisoner");
            Scribe_Values.Look(ref SpecialSource, "specialSource", MouseEggSpecialSource.None);
            Scribe_Values.Look(ref TravelEggAggressionHandled, "travelEggAggressionHandled", false);
        }
    }
}
