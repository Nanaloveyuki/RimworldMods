using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal sealed class RHAH_PawnProfile
    {
        public bool UseExplicitApparel { get; set; }
        public List<ThingDef> Apparel { get; } = new List<ThingDef>();
        public bool UseExplicitBackstory { get; set; }
        public BackstoryDef Childhood { get; set; }
        public BackstoryDef Adulthood { get; set; }
        public bool UseExplicitHealth { get; set; }
        public List<HediffDef> Hediffs { get; } = new List<HediffDef>();
        public bool UseExplicitXenotype { get; set; }
        public XenotypeDef Xenotype { get; set; }
        public string XenotypeDefName { get; set; }
    }
}
