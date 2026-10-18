using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MouseDisaster
{
    // Opt-in on individual TraitDefs and ThingDefs; only used during incident generation.
    public class MouseDisasterGenerationExtension : DefModExtension
    {
        public bool allowTrait;
        public bool allowRefugeeApparel;
    }

    public static class MouseDisasterGenerationPolicy
    {
        public const string OwnedTraitPrefix = "MouseDisaster_Trait_";

        private static List<TraitDef> prohibitedTraits;
        public static IEnumerable<TraitDef> ProhibitedTraits
        {
            get
            {
                if (prohibitedTraits == null)
                {
                    prohibitedTraits = DefDatabase<TraitDef>.AllDefsListForReading.Where(def => !AllowsTrait(def)).ToList();
                }

                return prohibitedTraits;
            }
        }

        public static bool IsOwnedTrait(TraitDef def)
        {
            return def?.defName != null && def.defName.StartsWith(OwnedTraitPrefix, StringComparison.Ordinal);
        }

        public static bool AllowsTrait(TraitDef def)
        {
            return def != null && (IsOwnedTrait(def) || def.modContentPack?.IsOfficialMod == true ||
                def.GetModExtension<MouseDisasterGenerationExtension>()?.allowTrait == true);
        }

        public static bool AllowsApparel(ThingDef def)
        {
            if (def == null || !def.IsApparel) return false;
            return def.defName == "Apparel_TribalA" || def.defName == "Apparel_BasicShirt" ||
                def.defName == "Apparel_Pants" || def.defName == "Apparel_Parka" ||
                def.defName == "Apparel_BabyOnesie" || def.defName == "Apparel_KidTribal" ||
                def.defName == "Apparel_WarmerHat" || def.defName == "Apparel_SunHat" ||
                def.GetModExtension<MouseDisasterGenerationExtension>()?.allowRefugeeApparel == true;
        }
    }
}
