using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

using WorldCaravan = RimWorld.Planet.Caravan;
namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum KingdomSiteKind { Hall, Charity, Distribution, Meeting, SafeHouse }

    public sealed class WorldObject_RHAH_KingdomSite : WorldObject
    {
        internal KingdomSiteKind Kind;
        internal List<Verse.Pawn> Recipients = new List<Verse.Pawn>();
        internal int RecipientGroup;
        internal bool PacketReceived;
        internal List<Thing> Supply = new List<Thing>();
        public override string GetInspectString() => "RHAH_Kingdom_SiteInspect".Translate(Label);
        public override string Label => RecipientGroup == 0 ? ("RHAH_Kingdom_Site_" + Kind).Translate() : ("RHAH_Kingdom_PublicGroup_" + RecipientGroup).Translate();
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(WorldCaravan caravan)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(caravan)) yield return option;
            if (caravan == null || !caravan.IsPlayerControlled) yield break;
            bool here = caravan.Tile == Tile;
            yield return new FloatMenuOption(here ? "RHAH_Kingdom_Interact".Translate() : "RHAH_Kingdom_Travel".Translate(),
                () => { if (caravan.Tile == Tile) KingdomArcActions.Interact(this, caravan); else caravan.pather.StartPath(Tile, new CaravanArrivalAction_RHAH_KingdomSite(this), true, false); });
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Kind, "kingdomSiteKind", KingdomSiteKind.Hall);
            Scribe_Collections.Look(ref Recipients, "kingdomRecipients", LookMode.Reference);
            Scribe_Collections.Look(ref Supply, "kingdomSupply", LookMode.Deep);
            Scribe_Values.Look(ref RecipientGroup, "kingdomRecipientGroup", 0);
            Scribe_Values.Look(ref PacketReceived, "kingdomPacketReceived", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Recipients = Recipients ?? new List<Verse.Pawn>();
                Supply = Supply ?? new List<Thing>();
            }
        }
    }

    public sealed class CaravanArrivalAction_RHAH_KingdomSite : CaravanArrivalAction
    {
        private WorldObject_RHAH_KingdomSite site;
        public CaravanArrivalAction_RHAH_KingdomSite() { }
        internal CaravanArrivalAction_RHAH_KingdomSite(WorldObject_RHAH_KingdomSite site) { this.site = site; }
        public override string Label => "RHAH_Kingdom_Travel".Translate();
        public override string ReportString => "RHAH_Kingdom_Travel".Translate();
        public override FloatMenuAcceptanceReport StillValid(WorldCaravan caravan, PlanetTile destination)
        {
            return site != null && site.Spawned && !site.Destroyed && site.Tile == destination;
        }
        public override void Arrived(WorldCaravan caravan)
        {
            // 抵达后仍需明确开始办理
            Messages.Message("RHAH_Kingdom_Arrived".Translate(site.Label), MessageTypeDefOf.NeutralEvent, false);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "kingdomArrivalSite");
        }
    }
}
