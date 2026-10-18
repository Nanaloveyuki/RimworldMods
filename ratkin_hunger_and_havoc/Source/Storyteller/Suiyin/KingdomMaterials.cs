using System.Collections.Generic;
using System.Text;
using RimWorld;
using WorldCaravan = RimWorld.Planet.Caravan;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum KingdomMaterialKind { ReceiptA, ReceiptB, Statement, VerifiedCopy, CharityReport, EnvoyReceipt, GuardReceipt, DistributionRecord }

    internal sealed class KingdomMaterialRecord : IExposable
    {
        public KingdomMaterialRecord() { }
        internal int Number;
        internal KingdomMaterialKind Kind;
        internal string Source = "";
        internal int ExternalMask;
        internal bool Envoy;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Number, "number", 0);
            Scribe_Values.Look(ref Kind, "kind", KingdomMaterialKind.ReceiptA);
            Scribe_Values.Look(ref Source, "source", "");
            Scribe_Values.Look(ref ExternalMask, "externalMask", 0);
            Scribe_Values.Look(ref Envoy, "envoy", false);
        }
    }

    public sealed class CompProperties_RHAH_KingdomMaterial : CompProperties
    {
        public CompProperties_RHAH_KingdomMaterial() { compClass = typeof(Comp_RHAH_KingdomMaterial); }
    }

    public sealed class Comp_RHAH_KingdomMaterial : ThingComp
    {
        internal int Number;
        internal KingdomMaterialKind Kind;
        internal string Source = "";
        internal bool Redacted;
        public override string CompInspectStringExtra()
        {
            return "RHAH_Kingdom_MaterialProvenance".Translate(Number, ("RHAH_Kingdom_Source_" + Source).Translate(), Redacted ? "RHAH_Kingdom_Redacted".Translate() : "RHAH_Kingdom_Unredacted".Translate());
        }
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref Number, "materialNumber", 0);
            Scribe_Values.Look(ref Kind, "materialKind", KingdomMaterialKind.ReceiptA);
            Scribe_Values.Look(ref Source, "materialSource", "");
            Scribe_Values.Look(ref Redacted, "materialRedacted", false);
        }
    }

    internal static class KingdomMaterials
    {
        internal static Thing Create(KingdomArcProgress progress, KingdomMaterialKind kind, string source)
        {
            int number = progress.Materials.Count + 1;
            // 实际交到后才记取得
            return Make(number, kind, source, false);
        }

        internal static bool Acquire(KingdomArcProgress progress, KingdomMaterialKind kind, string source, Map home, WorldCaravan caravan = null)
        {
            Thing item = Create(progress, kind, source);
            if (!Deliver(item, home, caravan))
            {
                item.Destroy(DestroyMode.Vanish);
                return false;
            }
            Comp_RHAH_KingdomMaterial comp = item.TryGetComp<Comp_RHAH_KingdomMaterial>();
            progress.Materials.Add(new KingdomMaterialRecord { Number = comp.Number, Kind = kind, Source = source });
            return true;
        }

        private static Thing Make(int number, KingdomMaterialKind kind, string source, bool redacted)
        {
            Thing item = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RHAH_KingdomMaterial"));
            Comp_RHAH_KingdomMaterial comp = item.TryGetComp<Comp_RHAH_KingdomMaterial>();
            comp.Number = number;
            comp.Kind = kind;
            comp.Source = source;
            comp.Redacted = redacted;
            return item;
        }

        internal static Thing Copy(Thing actual, bool redact)
        {
            Comp_RHAH_KingdomMaterial comp = actual?.TryGetComp<Comp_RHAH_KingdomMaterial>();
            return comp == null || actual.Destroyed ? null : Make(comp.Number, comp.Kind, comp.Source, redact || comp.Redacted);
        }

        internal static List<Thing> Held(Map home, WorldCaravan caravan = null)
        {
            List<Thing> found = new List<Thing>();
            if (home != null)
            {
                List<Thing> things = home.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("RHAH_KingdomMaterial"));
                for (int i = 0; i < things.Count; i++)
                {
                    if (!things[i].Destroyed && !things[i].IsForbidden(Faction.OfPlayer)) found.Add(things[i]);
                }
                List<Verse.Pawn> pawns = home.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < pawns.Count; i++) AddInventory(found, pawns[i]);
            }
            if (caravan != null)
            {
                for (int i = 0; i < caravan.PawnsListForReading.Count; i++) AddInventory(found, caravan.PawnsListForReading[i]);
            }
            return found;
        }

        internal static bool ValidateSelection(Map home, List<Thing> selected)
        {
            if (home == null || selected == null || selected.Count == 0) return false;
            List<Thing> held = Held(home);
            for (int i = 0; i < selected.Count; i++)
            {
                Thing item = selected[i];
                if (item == null || item.Destroyed || !held.Contains(item)) return false;
                for (int j = 0; j < i; j++) if (ReferenceEquals(item, selected[j])) return false;
            }
            return true;
        }

        internal static bool CopyToHome(Map home, List<Thing> selected)
        {
            if (!ValidateSelection(home, selected)) return false;
            List<Thing> copies = new List<Thing>(selected.Count);
            for (int i = 0; i < selected.Count; i++)
            {
                Thing copy = ValidateSelection(home, selected) ? Copy(selected[i], false) : null;
                if (copy == null || !Deliver(copy, home))
                {
                    if (copy != null && !copy.Destroyed) copy.Destroy(DestroyMode.Vanish);
                    for (int j = 0; j < copies.Count; j++) if (!copies[j].Destroyed) copies[j].Destroy(DestroyMode.Vanish);
                    return false;
                }
                copies.Add(copy);
            }
            if (!ValidateSelection(home, selected))
            {
                for (int i = 0; i < copies.Count; i++) if (!copies[i].Destroyed) copies[i].Destroy(DestroyMode.Vanish);
                return false;
            }
            return true;
        }

        private static void AddInventory(List<Thing> found, Verse.Pawn pawn)
        {
            ThingOwner<Thing> items = pawn.inventory?.innerContainer;
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i].Destroyed && items[i].TryGetComp<Comp_RHAH_KingdomMaterial>() != null) found.Add(items[i]);
            }
        }

        internal static string List(Map home)
        {
            StringBuilder text = new StringBuilder();
            List<Thing> held = Held(home);
            for (int i = 0; i < held.Count; i++)
            {
                Comp_RHAH_KingdomMaterial comp = held[i].TryGetComp<Comp_RHAH_KingdomMaterial>();
                text.AppendLine("RHAH_Kingdom_MaterialEntry".Translate(comp.Number, ("RHAH_Kingdom_Material_" + comp.Kind).Translate(), ("RHAH_Kingdom_Source_" + comp.Source).Translate()));
            }
            return text.ToString();
        }

        internal static bool HasReceipts(List<Thing> items)
        {
            bool a = false;
            bool b = false;
            for (int i = 0; i < items.Count; i++)
            {
                Comp_RHAH_KingdomMaterial comp = items[i].TryGetComp<Comp_RHAH_KingdomMaterial>();
                if (comp == null) continue;
                a |= comp.Kind == KingdomMaterialKind.ReceiptA;
                b |= comp.Kind == KingdomMaterialKind.ReceiptB;
            }
            return a && b;
        }

        internal static bool Deliver(Thing item, Map home, WorldCaravan caravan = null)
        {
            if (item == null || item.Destroyed) return false;
            if (caravan != null)
            {
                for (int i = 0; i < caravan.PawnsListForReading.Count; i++)
                    if (caravan.PawnsListForReading[i].inventory?.innerContainer.TryAdd(item) == true) return true;
                return false;
            }
            return home != null && GenPlace.TryPlaceThing(item, home.Center, home, ThingPlaceMode.Near);
        }
    }
}
