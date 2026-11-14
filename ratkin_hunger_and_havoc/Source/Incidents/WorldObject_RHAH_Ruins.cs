using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    // XML 和存档通过类型名创建世界地点
    public sealed class WorldObject_RHAH_Ruins : WorldObject
    {
        internal const string HardtackDef = "RK_Food_Hardtack";
        int hardtackRemaining;
        int silverRemaining;
        int componentsRemaining;

        internal void Configure(int hardtack, int silver, int components)
        {
            hardtackRemaining = hardtack;
            silverRemaining = silver;
            componentsRemaining = components;
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(RimWorld.Planet.Caravan caravan)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(caravan)) yield return option;
            foreach (FloatMenuOption option in CaravanArrivalActionUtility.GetFloatMenuOptions(
                () => CanExplore(caravan), () => new CaravanArrivalAction_RHAH_Ruins(this),
                "RHAH_Background_Explore".Translate(), caravan, Tile, this)) yield return option;
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(RimWorld.Planet.Caravan caravan)
        {
            foreach (Gizmo gizmo in base.GetCaravanGizmos(caravan)) yield return gizmo;
            if (caravan.Tile == Tile && CanExplore(caravan))
                yield return new Command_Action
                {
                    defaultLabel = "RHAH_Background_Explore".Translate(),
                    defaultDesc = "RHAH_Background_ExploreDescription".Translate(),
                    action = () => Explore(caravan)
                };
        }

        internal bool CanExplore(RimWorld.Planet.Caravan caravan)
        {
            return Spawned && !Destroyed && caravan != null && caravan.Spawned && caravan.IsPlayerControlled &&
                caravan.PawnsListForReading.Count > 0;
        }

        internal bool Explore(RimWorld.Planet.Caravan caravan)
        {
            if (!CanExplore(caravan) || caravan.Tile != Tile) return false;
            if (!Deliver(caravan, HardtackDef, ref hardtackRemaining) ||
                !Deliver(caravan, "Silver", ref silverRemaining) ||
                !Deliver(caravan, "ComponentIndustrial", ref componentsRemaining))
            {
                Messages.Message("RHAH_Background_ExploreRemaining".Translate(), new LookTargets(this), MessageTypeDefOf.RejectInput);
                return false;
            }
            Destroy();
            Find.LetterStack.ReceiveLetter("RHAH_Background_Explored_Label".Translate(),
                "RHAH_Background_Explored_Text".Translate(), LetterDefOf.NeutralEvent, new LookTargets(caravan));
            return true;
        }

        static bool Deliver(RimWorld.Planet.Caravan caravan, string defName, ref int remaining)
        {
            if (remaining <= 0) return true;
            ThingDef itemDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (itemDef == null)
            {
                Log.Error("[RHAH] Ruins reward definition is unavailable: " + defName);
                return false;
            }
            while (remaining > 0)
            {
                Thing item = ThingMaker.MakeThing(itemDef);
                item.stackCount = System.Math.Min(remaining, itemDef.stackLimit);
                int count = item.stackCount;
                bool delivered = false;
                List<Verse.Pawn> pawns = caravan.PawnsListForReading;
                for (int i = 0; i < pawns.Count; i++)
                {
                    if (pawns[i].inventory?.innerContainer.TryAdd(item) != true) continue;
                    delivered = true;
                    remaining -= count;
                    break;
                }
                if (delivered) continue;
                item.Destroy();
                return false;
            }
            return true;
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n" + "RHAH_Background_RuinsSupplies".Translate(
                hardtackRemaining, silverRemaining, componentsRemaining);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hardtackRemaining, "hardtackRemaining", 0);
            Scribe_Values.Look(ref silverRemaining, "silverRemaining", 0);
            Scribe_Values.Look(ref componentsRemaining, "componentsRemaining", 0);
        }
    }
}
