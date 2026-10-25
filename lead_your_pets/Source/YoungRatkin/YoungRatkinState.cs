using RimWorld;
using Verse;

namespace LeadYourPet
{
    public class YoungRatkinState : IExposable
    {
        public Pawn Pawn;
        public Pawn CurrentMaster;
        public bool IsPet;
        public bool IsTravelStock;
        public bool SellAsPrisoner;
        public YoungRatkinSpecialSource SpecialSource;
        public bool TravelEggAggressionHandled;

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_References.Look(ref CurrentMaster, "currentMaster");
            Scribe_Values.Look(ref IsPet, "isPet");
            Scribe_Values.Look(ref IsTravelStock, "isTravelStock");
            Scribe_Values.Look(ref SellAsPrisoner, "sellAsPrisoner");
            Scribe_Values.Look(ref SpecialSource, "specialSource", YoungRatkinSpecialSource.None);
            Scribe_Values.Look(ref TravelEggAggressionHandled, "travelEggAggressionHandled", false);
        }
    }
}
