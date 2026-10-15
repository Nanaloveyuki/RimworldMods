using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_ApparelAssigner
    {
        internal static void Apply(Verse.Pawn pawn)
        {
            if (pawn?.apparel == null)
            {
                return;
            }

            int stage = Stage(pawn);
            List<ThingDef> pool = Pool(pawn, stage);
            if (pool.Count == 0)
            {
                return;
            }

            Shuffle(pool);
            int target = RHAH_ApparelPolicy.PieceCount(stage, Rand.Value);
            if (target > pool.Count)
            {
                target = pool.Count;
            }

            int worn = 0;
            for (int i = 0; i < pool.Count && worn < target; i++)
            {
                if (TryWear(pawn, pool[i]))
                {
                    worn++;
                }
            }
        }

        static int Stage(Verse.Pawn pawn)
        {
            DevelopmentalStage stage = pawn.DevelopmentalStage;
            if (stage == DevelopmentalStage.Baby)
            {
                return 0;
            }

            return stage == DevelopmentalStage.Child ? 1 : 2;
        }

        static List<ThingDef> Pool(Verse.Pawn pawn, int stage)
        {
            List<ThingDef> pool = new List<ThingDef>();
            List<ThingDef> all = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                ThingDef def = all[i];
                if (def == null || !Candidate(def) || !RHAH_ApparelPolicy.FitsStage(def.defName, stage))
                {
                    continue;
                }

                if (!ApparelUtility.HasPartsToWear(pawn, def))
                {
                    continue;
                }

                pool.Add(def);
            }

            return pool;
        }

        static bool Candidate(ThingDef def)
        {
            ModContentPack pack = def.modContentPack;
            Core.RHAH_Settings settings = RHAH_Mod.Settings;
            return RHAH_ApparelPolicy.AllowsApparel(
                def.defName,
                def.IsApparel,
                def.MadeFromStuff,
                def.apparel != null && def.apparel.countsAsClothingForNudity,
                pack == null ? null : pack.PackageId,
                pack != null && (pack.IsOfficialMod || pack.IsCoreMod),
                (int)def.techLevel,
                settings == null || settings.IsRefugeeApparelEnabled(def.defName));
        }

        internal static void AppendCandidates(List<ThingDef> result)
        {
            if (result == null || DefDatabase<ThingDef>.AllDefsListForReading == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i] != null)
                {
                    seen.Add(result[i].defName);
                }
            }

            List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef def = defs[i];
                if (def != null && Candidate(def) && seen.Add(def.defName))
                {
                    result.Add(def);
                }
            }
        }
        static bool TryWear(Verse.Pawn pawn, ThingDef def)
        {
            if (!pawn.apparel.CanWearWithoutDroppingAnything(def))
            {
                return false;
            }

            List<string> stuffs = new List<string>();
            foreach (ThingDef stuff in GenStuff.AllowedStuffsFor(def, TechLevel.Undefined, false))
            {
                if (stuff != null)
                {
                    stuffs.Add(stuff.defName);
                }
            }

            string chosen = RHAH_ApparelPolicy.PreferredStuff(stuffs, Rand.Value);
            ThingDef stuffDef = string.IsNullOrEmpty(chosen) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(chosen);
            if (stuffDef == null)
            {
                return false;
            }

            Apparel apparel = ThingMaker.MakeThing(def, stuffDef) as Apparel;
            if (apparel == null || !apparel.PawnCanWear(pawn, true))
            {
                if (apparel != null)
                {
                    apparel.Destroy();
                }

                return false;
            }

            CompQuality quality = apparel.TryGetComp<CompQuality>();
            if (quality != null)
            {
                Core.RHAH_Settings qualitySettings = Core.RHAH_Mod.Settings;
                int category = qualitySettings == null
                    ? RHAH_ApparelPolicy.Quality(Rand.Value)
                    : RHAH_ApparelPolicy.Quality(Rand.Value, qualitySettings.apparelAwfulPercent / 100f, qualitySettings.apparelPoorPercent / 100f);
                quality.SetQuality((QualityCategory)category, ArtGenerationContext.Outsider);
            }

            Core.RHAH_Settings apparelSettings = Core.RHAH_Mod.Settings;
            float corpse = apparelSettings == null ? RHAH_ApparelPolicy.CorpseChance : apparelSettings.apparelCorpsePercent / 100f;
            apparel.WornByCorpse = Rand.Chance(corpse);
            pawn.apparel.Wear(apparel, false, false);
            apparel.HitPoints = RHAH_ApparelPolicy.HitPoints(apparel.MaxHitPoints, Rand.Range(RHAH_ApparelPolicy.MinDurability, RHAH_ApparelPolicy.MaxDurability));
            return pawn.apparel.WornApparel.Contains(apparel);
        }

        static void Shuffle(List<ThingDef> pool)
        {
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int swap = Rand.Range(0, i + 1);
                ThingDef current = pool[i];
                pool[i] = pool[swap];
                pool[swap] = current;
            }
        }
    }
}
