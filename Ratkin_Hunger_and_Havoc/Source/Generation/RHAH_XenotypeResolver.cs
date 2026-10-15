using System;
using HungerAndHavoc.Pawn.Compat;
using System.Collections.Generic;
using RimWorld;
using Verse;
using VersePawn = Verse.Pawn;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_XenotypeResolver
    {
        internal static bool TryResolve(RHAH_PawnProfile profile, PawnKindDef kind, out XenotypeDef xenotype)
        {
            xenotype = null;
            if (!ModsConfig.BiotechActive)
            {
                return true;
            }

            ThingDef race = kind?.race;
            if (profile != null && profile.UseExplicitXenotype)
            {
                xenotype = profile.Xenotype ?? Load(profile.XenotypeDefName);
                if (xenotype == null)
                {
                    return string.IsNullOrEmpty(profile.XenotypeDefName);
                }

                return RHAH_HarXenotypeBridge.Allows(xenotype, race);
            }

            if (RHAH_GenerationOptimizer.Enabled)
            {
                Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
                List<XenotypeDef> candidates = LoadedCandidates(settings, race);
                float total = 0f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    total += settings.XenotypeWeight(candidates[i].defName);
                }

                float cursor = Rand.Value * total;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float weight = settings.XenotypeWeight(candidates[i].defName);
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    xenotype = candidates[i];
                    cursor -= weight;
                    if (cursor < 0f)
                    {
                        break;
                    }
                }
                if (xenotype != null)
                {
                    return true;
                }
            }

            XenotypeDef primary = Load(RHAH_GeneCatalog.DefaultXenotypeDefName);
            if (RHAH_GenerationOptimizer.Enabled && RHAH_HarXenotypeBridge.Allows(primary, race))
            {
                xenotype = primary;
                return true;
            }

            // 读取上游已打补丁的基础鼠族池 不继承其装备和年龄限制
            XenotypeSet pool = kind?.xenotypeSet;
            if (pool == null && race?.defName == "Ratkin")
            {
                PawnKindDef colonist = DefDatabase<PawnKindDef>.GetNamedSilentFail("RatkinColonist");
                if (colonist?.race == race)
                {
                    pool = colonist.xenotypeSet;
                }
            }

            xenotype = ChooseAllowed(pool, race, Rand.Value, RHAH_HarXenotypeBridge.Allows);
            if (xenotype == null && RHAH_HarXenotypeBridge.Allows(primary, race))
            {
                xenotype = primary;
            }

            if (xenotype == null)
            {
                XenotypeDef fallback = Load(RHAH_GeneCatalog.FallbackXenotypeDefName);
                if (RHAH_HarXenotypeBridge.Allows(fallback, race))
                {
                    xenotype = fallback;
                }
            }

            return xenotype != null;
        }

        internal static XenotypeDef ChooseAllowed(XenotypeSet candidates, ThingDef race,
            float roll, Func<XenotypeDef, ThingDef, bool> allows)
        {
            if (candidates == null)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                XenotypeChance candidate = candidates[i];
                if (candidate != null && candidate.chance > 0f && !float.IsInfinity(candidate.chance) &&
                    allows(candidate.xenotype, race))
                {
                    total += candidate.chance;
                }
            }

            float cursor = Math.Max(0f, Math.Min(1f, roll)) * total;
            XenotypeDef last = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                XenotypeChance candidate = candidates[i];
                if (candidate == null || !(candidate.chance > 0f) || float.IsInfinity(candidate.chance) ||
                    !allows(candidate.xenotype, race))
                {
                    continue;
                }

                last = candidate.xenotype;
                cursor -= candidate.chance;
                if (cursor < 0f)
                {
                    return last;
                }
            }

            return last;
        }

        internal static bool Installed(VersePawn pawn, ThingDef race, XenotypeDef expected)
        {
            if (pawn == null || pawn.def != race)
            {
                return false;
            }

            if (!ModsConfig.BiotechActive)
            {
                return true;
            }

            XenotypeDef actual = pawn.genes?.Xenotype;
            if (actual == null || (expected != null && actual != expected) ||
                !RHAH_HarXenotypeBridge.Allows(actual, race))
            {
                return false;
            }

            return HasXenotypeGenes(pawn.genes, actual);
        }

        internal static bool HasXenotypeGenes(Pawn_GeneTracker tracker, XenotypeDef xenotype)
        {
            if (tracker == null || xenotype == null)
            {
                return false;
            }

            List<Gene> genes = xenotype.inheritable ? tracker.Endogenes : tracker.Xenogenes;
            for (int i = 0; i < xenotype.genes.Count; i++)
            {
                GeneDef expected = xenotype.genes[i];
                // 发色胡须只留一个 原版不会把名单里其余的装上
                if (expected == null || expected.RandomChosen)
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < genes.Count; j++)
                {
                    if (genes[j].def == expected)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        internal static void ApplyEnabledGenes(VersePawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null)
            {
                return;
            }

            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            if (settings == null)
            {
                return;
            }

            List<string> names = settings.EnabledGeneNames();
            for (int i = 0; i < names.Count; i++)
            {
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(names[i]);
                if (!RHAH_GeneCatalog.IsOwnedGene(gene) || pawn.genes.HasActiveGene(gene) || Conflicts(pawn, gene))
                {
                    continue;
                }

                pawn.genes.AddGene(gene, false);
            }
        }

        internal static List<XenotypeDef> LoadedCandidates(Core.RHAH_Settings settings, ThingDef race = null)
        {
            List<XenotypeDef> loaded = new List<XenotypeDef>();
            if (!ModsConfig.BiotechActive || settings == null)
            {
                return loaded;
            }

            AddKnown(loaded, settings);
            AddJoined(loaded, settings);
            race = race ?? DefDatabase<ThingDef>.GetNamedSilentFail("Ratkin");
            for (int i = loaded.Count - 1; i >= 0; i--)
            {
                if (!RHAH_HarXenotypeBridge.Allows(loaded[i], race))
                {
                    loaded.RemoveAt(i);
                }
            }
            return loaded;
        }

        internal static List<XenotypeDef> AvailableToJoin(Core.RHAH_Settings settings)
        {
            List<XenotypeDef> available = new List<XenotypeDef>();
            if (!ModsConfig.BiotechActive || settings == null)
            {
                return available;
            }

            List<XenotypeDef> all = DefDatabase<XenotypeDef>.AllDefsListForReading;
            List<XenotypeDef> loaded = LoadedCandidates(settings);
            ThingDef race = DefDatabase<ThingDef>.GetNamedSilentFail("Ratkin");
            for (int i = 0; i < all.Count; i++)
            {
                XenotypeDef xenotype = all[i];
                if (xenotype == null || IsFallback(xenotype.defName) || Contains(loaded, xenotype.defName) ||
                    !RHAH_HarXenotypeBridge.Allows(xenotype, race))
                {
                    continue;
                }

                if (RHAH_GeneCatalog.IsRegistered(xenotype.defName) || LooksRatkin(xenotype))
                {
                    available.Add(xenotype);
                }
            }

            return available;
        }

        static bool LooksRatkin(XenotypeDef xenotype)
        {
            string name = xenotype.defName ?? string.Empty;
            string label = xenotype.label ?? string.Empty;
            return name.IndexOf("ratkin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("ratkin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("鼠族", System.StringComparison.Ordinal) >= 0;
        }
        internal static List<GeneDef> LoadedOwnedGenes()
        {
            List<GeneDef> genes = new List<GeneDef>();
            if (!ModsConfig.BiotechActive)
            {
                return genes;
            }

            List<GeneDef> all = DefDatabase<GeneDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                if (RHAH_GeneCatalog.IsOwnedGene(all[i]))
                {
                    genes.Add(all[i]);
                }
            }

            return genes;
        }

        static void AddKnown(List<XenotypeDef> loaded, Core.RHAH_Settings settings)
        {
            IReadOnlyList<RHAH_XenotypeRegistration> registrations = RHAH_GeneCatalog.Registrations;
            for (int i = 0; i < registrations.Count; i++)
            {
                XenotypeDef xenotype = Load(registrations[i].DefName);
                if (xenotype == null || IsFallback(xenotype.defName))
                {
                    continue;
                }

                if (registrations[i].Builtin || settings.IsXenotypeEnabled(xenotype.defName))
                {
                    loaded.Add(xenotype);
                }
            }
        }

        static void AddJoined(List<XenotypeDef> loaded, Core.RHAH_Settings settings)
        {
            List<string> joined = settings.EnabledXenotypeNames();
            for (int i = 0; i < joined.Count; i++)
            {
                if (Contains(loaded, joined[i]) || IsFallback(joined[i]))
                {
                    continue;
                }

                XenotypeDef xenotype = Load(joined[i]);
                if (xenotype != null)
                {
                    loaded.Add(xenotype);
                }
            }
        }

        static bool IsFallback(string defName)
        {
            return string.Equals(defName, RHAH_GeneCatalog.FallbackXenotypeDefName, System.StringComparison.OrdinalIgnoreCase);
        }

        internal static string SourceModName(XenotypeDef xenotype)
        {
            string name = xenotype == null || xenotype.modContentPack == null
                ? null
                : xenotype.modContentPack.Name;
            return string.IsNullOrEmpty(name) ? null : name;
        }

        internal static List<List<XenotypeDef>> GroupBySourceMod(List<XenotypeDef> xenotypes)
        {
            List<List<XenotypeDef>> groups = new List<List<XenotypeDef>>();
            if (xenotypes == null)
            {
                return groups;
            }

            List<string> names = new List<string>();
            for (int i = 0; i < xenotypes.Count; i++)
            {
                string name = SourceModName(xenotypes[i]);
                int index = IndexOf(names, name);
                if (index < 0)
                {
                    names.Add(name);
                    groups.Add(new List<XenotypeDef>());
                    index = groups.Count - 1;
                }

                groups[index].Add(xenotypes[i]);
            }

            return groups;
        }

        static int IndexOf(List<string> names, string name)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
        static bool Contains(List<XenotypeDef> loaded, string defName)
        {
            for (int i = 0; i < loaded.Count; i++)
            {
                if (loaded[i].defName == defName)
                {
                    return true;
                }
            }

            return false;
        }


        static XenotypeDef Load(string defName)
        {
            if (string.IsNullOrEmpty(defName) || !ModsConfig.BiotechActive)
            {
                return null;
            }

            return DefDatabase<XenotypeDef>.GetNamedSilentFail(defName);
        }

        static bool Conflicts(VersePawn pawn, GeneDef gene)
        {
            if (gene.exclusionTags == null)
            {
                return false;
            }

            List<Gene> current = pawn.genes.GenesListForReading;
            for (int i = 0; i < current.Count; i++)
            {
                GeneDef other = current[i]?.def;
                if (other != null && gene.ConflictsWith(other))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
