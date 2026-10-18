using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public static class MouseDisasterSubtypeUtility
    {
        private const string DefaultSubtypeKey = "ratkin";
        private const string LocalSubtypeXenotypePrefix = "MouseDisasterSubtype_";

        private static readonly string[] SubtypeAnchorKeywords =
        {
            "rat",
            "mouse",
            "ratkin",
            "hamster",
            "mole",
            "gerbil",
            "shrew",
            "white",
            "albino",
            "experiment",
            "鼠",
            "仓鼠",
            "鼹",
            "沙鼠",
            "白鼠",
            "实验"
        };

        private static readonly Dictionary<string, string[]> ExternalSubtypeMarkerGeneNames = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "hamster", new[] { "Ratkin_HamsterEars" } },
            { "mole", new[] { "Ratkin_MoleEars", "Ratkin_MoleTail" } },
            { "white", new[] { "Ratkin_LabRatEars" } },
            { "experiment", new[] { "Ratkin_Prototype_Vole_Ears", "Ratkin_Prototype_Vole_Tail" } },
            { "gerbil", new[] { "Ratkin_VoleEars" } },
            { "shrew", new[] { "Ratkin_SquirrelEars", "Ratkin_SquirrelTail" } },
            { "ratkin", new[] { "Ratkin_Ears", "Ratkin_Tail" } }
        };

        private static readonly Dictionary<string, string[]> ExternalSubtypeXenotypeNames = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "hamster", new[] { "Ratkin_Hamster" } },
            { "mole", new[] { "Ratkin_Mole" } },
            { "white", new[] { "Ratkin_LabRat" } },
            { "experiment", new[] { "Ratkin_VolePrototype", "Ratkin_LabRat" } },
            { "gerbil", new[] { "Ratkin_Vole", "Ratkin_HouseMouse" } },
            { "shrew", new[] { "Ratkin_Squirrel", "Ratkin_HouseMouse" } },
            { "ratkin", new[] { "RK_XenoType_Ratkin", "Ratkin_HouseMouse", "Ratkin" } }
        };

        private static readonly string[] ExternalMarkerGeneNamePool =
        {
            "Ratkin_Ears",
            "Ratkin_Tail",
            "Ratkin_HamsterEars",
            "Ratkin_SquirrelEars",
            "Ratkin_SquirrelTail",
            "Ratkin_LabRatEars",
            "Ratkin_MoleEars",
            "Ratkin_MoleTail",
            "Ratkin_VoleEars",
            "Ratkin_WREars",
            "Ratkin_WRTail",
            "Ratkin_Prototype_Vole_Ears",
            "Ratkin_Prototype_Vole_Tail"
        };

        private static List<string> cachedSubtypeCandidateKeys;

        private static readonly string[] GenericTokens =
        {
            "mouse",
            "rat",
            "ratkin",
            "disaster",
            "wild",
            "thief",
            "beggar",
            "trader",
            "adult",
            "child",
            "baby",
            "newborn",
            "pawn",
            "鼠族",
            "鼠蛋",
            "野生",
            "偷窃",
            "乞讨",
            "商贩",
            "成人",
            "幼体",
            "婴儿",
            "灾荒"
        };

        private sealed class SubtypePreset
        {
            public readonly string key;
            public readonly string[] keywords;
            public readonly Func<List<GeneDef>> geneFactory;

            public SubtypePreset(string key, string[] keywords, Func<List<GeneDef>> geneFactory)
            {
                this.key = key;
                this.keywords = keywords;
                this.geneFactory = geneFactory;
            }
        }

        private static readonly List<SubtypePreset> KnownPresets = new List<SubtypePreset>
        {
            new SubtypePreset(
                "hamster",
                new[] { "hamster", "仓鼠", "金丝熊", "侏儒仓鼠", "dwarfhamster" },
                () => CreateGeneList(
                    MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter,
                    MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding)),

            new SubtypePreset(
                "mole",
                new[] { "mole", "molerat", "鼹", "鼹鼠" },
                () => CreateGeneList(
                    MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility,
                    MouseDisasterDefOf.MouseDisaster_Gene_MoreComing)),

            new SubtypePreset(
                "white",
                new[] { "white", "albino", "whiterat", "白鼠" },
                () => CreateGeneList(
                    MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility,
                    MouseDisasterDefOf.MouseDisaster_Gene_MoreComing)),

            new SubtypePreset(
                "experiment",
                new[] { "experiment", "specimen", "testsubject", "lab", "实验", "实验体", "改造" },
                () => CreateGeneList(
                    MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter,
                    MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility,
                    MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding,
                    MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility,
                    MouseDisasterDefOf.MouseDisaster_Gene_MoreComing)),

            new SubtypePreset(
                "gerbil",
                new[] { "gerbil", "沙鼠" },
                () => CreateGeneList(
                    MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter,
                    MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility)),

            new SubtypePreset(
                "shrew",
                new[] { "shrew", "鼩鼱", "鼩" },
                () => CreateGeneList(
                    MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding,
                    MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility))
        };

        public static void ApplySubtypeGenes(Pawn pawn, Pawn subtypeSource = null)
        {
            ApplySubtypeGenesCore(pawn, subtypeSource, chooseSpawnSubtype: true, forcedSubtypeKey: null);
        }

        public static void ApplyDefaultSubtypeGenes(Pawn pawn)
        {
            ApplySubtypeGenesCore(pawn, subtypeSource: null, chooseSpawnSubtype: false, forcedSubtypeKey: DefaultSubtypeKey);
        }

        public static void MigrateVirtualDefaultRatkinXenotype(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null ||
                !MouseDisasterAdaptiveXenotypeUtility.IsVirtualDefaultRatkinXenotype(pawn.genes.Xenotype))
            {
                return;
            }

            XenotypeDef defaultRatkinXenotype = MouseDisasterUtility.ResolveRatkinBaseXenotype();
            if (defaultRatkinXenotype != null && pawn.genes.Xenotype != defaultRatkinXenotype)
            {
                pawn.genes.SetXenotype(defaultRatkinXenotype);
            }
        }

        private static void ApplySubtypeGenesCore(
            Pawn pawn,
            Pawn subtypeSource,
            bool chooseSpawnSubtype,
            string forcedSubtypeKey)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null || !MouseDisasterUtility.IsRatkin(pawn))
            {
                return;
            }

            List<GeneDef> genePool = CreateGenePool();
            if (genePool.Count == 0)
            {
                return;
            }

            string subtypeKey = forcedSubtypeKey.NullOrEmpty()
                ? ResolveSubtypeKey(subtypeSource ?? pawn)
                : forcedSubtypeKey;
            if (chooseSpawnSubtype &&
                string.Equals(subtypeKey, DefaultSubtypeKey, StringComparison.OrdinalIgnoreCase) &&
                (subtypeSource == null || subtypeSource == pawn) &&
                !MouseDisasterAdaptiveXenotypeUtility.IsMouseDisasterXenotype(pawn.genes.Xenotype) &&
                !IsDefaultRatkinXenotype(pawn.genes.Xenotype))
            {
                subtypeKey = ChooseSpawnSubtypeKey(pawn);
            }

            List<GeneDef> targetGenes = ResolveSubtypeGenes(subtypeKey, genePool);
            if (targetGenes.Count == 0)
            {
                return;
            }

            ApplySubtypeXenotype(pawn, subtypeKey);
            RemoveMouseDisasterGenes(pawn, genePool);
            for (int i = 0; i < targetGenes.Count; i++)
            {
                GeneDef geneDef = targetGenes[i];
                if (geneDef != null && !pawn.genes.HasActiveGene(geneDef))
                {
                    pawn.genes.AddGene(geneDef, xenogene: true);
                }
            }

            if (MouseDisasterUtility.IsPrisonerScavengeDebugLogEnabled)
            {
                string xenotypeName = pawn.genes?.Xenotype?.defName ?? "null";
                Log.Message("[MouseDisaster][Subtype] pawn=" + pawn.LabelShortCap + ", subtype=" + subtypeKey + ", xenotype=" + xenotypeName);
            }
        }

        public static string ResolveSubtypeKey(Pawn pawn)
        {
            if (pawn == null)
            {
                return DefaultSubtypeKey;
            }

            if (IsDefaultRatkinXenotype(pawn.genes?.Xenotype))
            {
                return DefaultSubtypeKey;
            }

            string localXenotypeKey = ResolveLocalXenotypeKey(pawn.genes?.Xenotype);
            if (!localXenotypeKey.NullOrEmpty())
            {
                return localXenotypeKey;
            }

            string fromGenes = ResolveSubtypeKeyFromActiveGenes(pawn);
            if (!fromGenes.NullOrEmpty())
            {
                return fromGenes;
            }

            List<string> sources = EnumerateSources(pawn)
                .Where(source => !source.NullOrEmpty())
                .Distinct()
                .ToList();

            for (int presetIndex = 0; presetIndex < KnownPresets.Count; presetIndex++)
            {
                SubtypePreset preset = KnownPresets[presetIndex];
                for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                {
                    string sourceLower = sources[sourceIndex].ToLowerInvariant();
                    if (ContainsAnyKeyword(sourceLower, preset.keywords))
                    {
                        return preset.key;
                    }
                }
            }

            string dynamicKey = ResolveDynamicSubtypeKey(sources);
            return dynamicKey.NullOrEmpty() ? DefaultSubtypeKey : dynamicKey;
        }

        private static List<GeneDef> ResolveSubtypeGenes(string subtypeKey, List<GeneDef> genePool)
        {
            for (int i = 0; i < KnownPresets.Count; i++)
            {
                SubtypePreset preset = KnownPresets[i];
                if (string.Equals(preset.key, subtypeKey, StringComparison.OrdinalIgnoreCase))
                {
                    return preset.geneFactory()
                        .Where(def => def != null)
                        .Distinct()
                        .ToList();
                }
            }

            if (string.Equals(subtypeKey, DefaultSubtypeKey, StringComparison.OrdinalIgnoreCase))
            {
                return CreateGeneList(
                    MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter,
                    MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility,
                    MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding)
                    .Where(genePool.Contains)
                    .ToList();
            }

            return BuildDeterministicGeneProfile(subtypeKey, genePool);
        }

        private static List<GeneDef> BuildDeterministicGeneProfile(string subtypeKey, List<GeneDef> genePool)
        {
            if (genePool.NullOrEmpty())
            {
                return new List<GeneDef>();
            }

            if (genePool.Count == 1)
            {
                return new List<GeneDef> { genePool[0] };
            }

            int normalizedHash = StableHash(subtypeKey) & int.MaxValue;
            int desiredCount = Mathf.Clamp(2 + normalizedHash % 2, 1, genePool.Count);
            int start = normalizedHash % genePool.Count;
            int step = Mathf.Clamp(1 + (normalizedHash / 97) % genePool.Count, 1, genePool.Count - 1);

            List<GeneDef> result = new List<GeneDef>();
            int attempts = 0;
            int index = start;
            while (result.Count < desiredCount && attempts < genePool.Count * 3)
            {
                GeneDef candidate = genePool[index % genePool.Count];
                if (candidate != null && !result.Contains(candidate))
                {
                    result.Add(candidate);
                }

                index += step;
                attempts++;
            }

            if (result.Count == 0)
            {
                result.Add(genePool[normalizedHash % genePool.Count]);
            }

            return result;
        }

        private static void RemoveMouseDisasterGenes(Pawn pawn, List<GeneDef> genePool)
        {
            if (pawn?.genes == null || genePool.NullOrEmpty())
            {
                return;
            }

            List<Gene> genesToRemove = pawn.genes.GenesListForReading
                .Where(gene => gene != null && gene.def != null && genePool.Contains(gene.def))
                .ToList();

            using (new MouseDisasterGeneRemovalScope(pawn))
            {
                for (int i = 0; i < genesToRemove.Count; i++)
                {
                    pawn.genes.RemoveGene(genesToRemove[i]);
                }
            }
        }

        private static List<string> EnumerateSources(Pawn pawn)
        {
            List<string> sources = new List<string>();
            if (pawn == null)
            {
                return sources;
            }

            string kindDefName = pawn.kindDef?.defName;
            if (!kindDefName.NullOrEmpty())
            {
                sources.Add(kindDefName);
            }

            string kindLabel = pawn.kindDef?.label;
            if (!kindLabel.NullOrEmpty())
            {
                sources.Add(kindLabel);
            }

            string raceDefName = pawn.def?.defName;
            if (!raceDefName.NullOrEmpty())
            {
                sources.Add(raceDefName);
            }

            string raceLabel = pawn.def?.label;
            if (!raceLabel.NullOrEmpty())
            {
                sources.Add(raceLabel);
            }

            if (pawn.genes?.Xenotype != null)
            {
                if (!pawn.genes.Xenotype.defName.NullOrEmpty())
                {
                    sources.Add(pawn.genes.Xenotype.defName);
                }

                if (!pawn.genes.Xenotype.label.NullOrEmpty())
                {
                    sources.Add(pawn.genes.Xenotype.label);
                }
            }

            return sources;
        }

        private static string ResolveSubtypeKeyFromActiveGenes(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null)
            {
                return null;
            }

            List<GeneDef> activeGenes = CreateGenePool().Where(pawn.genes.HasActiveGene).ToList();
            if (activeGenes.Count == 0)
            {
                return null;
            }

            string bestKey = null;
            float bestScore = 0f;
            for (int i = 0; i < KnownPresets.Count; i++)
            {
                SubtypePreset preset = KnownPresets[i];
                List<GeneDef> presetGenes = preset.geneFactory().Where(def => def != null).Distinct().ToList();
                if (presetGenes.Count == 0)
                {
                    continue;
                }

                int overlap = 0;
                for (int geneIndex = 0; geneIndex < presetGenes.Count; geneIndex++)
                {
                    if (activeGenes.Contains(presetGenes[geneIndex]))
                    {
                        overlap++;
                    }
                }

                if (overlap == 0)
                {
                    continue;
                }

                float score = (float)overlap / presetGenes.Count;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestKey = preset.key;
                }
            }

            if (!bestKey.NullOrEmpty() && bestScore >= 0.6f)
            {
                return bestKey;
            }

            int defaultMatches = 0;
            List<GeneDef> defaultGenes = CreateGeneList(
                MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter,
                MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility,
                MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding);
            for (int i = 0; i < defaultGenes.Count; i++)
            {
                if (activeGenes.Contains(defaultGenes[i]))
                {
                    defaultMatches++;
                }
            }

            return defaultMatches >= 2 ? DefaultSubtypeKey : null;
        }

        private static string ChooseSpawnSubtypeKey(Pawn pawn)
        {
            List<string> candidateKeys = GetSubtypeCandidateKeys();
            if (candidateKeys.NullOrEmpty())
            {
                return DefaultSubtypeKey;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidateKeys.Count; i++)
            {
                totalWeight += GetSubtypeSpawnWeight(candidateKeys[i]);
            }

            if (totalWeight <= 0f)
            {
                return candidateKeys.RandomElement();
            }

            float roll = Rand.Range(0f, totalWeight);
            for (int i = 0; i < candidateKeys.Count; i++)
            {
                string key = candidateKeys[i];
                roll -= GetSubtypeSpawnWeight(key);
                if (roll <= 0f)
                {
                    return key;
                }
            }

            return candidateKeys.RandomElement();
        }

        private static List<string> GetSubtypeCandidateKeys()
        {
            if (cachedSubtypeCandidateKeys != null)
            {
                return cachedSubtypeCandidateKeys;
            }

            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < KnownPresets.Count; i++)
            {
                keys.Add(KnownPresets[i].key);
            }

            List<XenotypeDef> xenotypes = DefDatabase<XenotypeDef>.AllDefsListForReading;
            for (int i = 0; i < xenotypes.Count; i++)
            {
                XenotypeDef xenotype = xenotypes[i];
                string defName = xenotype?.defName;
                if (defName.NullOrEmpty())
                {
                    continue;
                }

                if (defName.StartsWith(LocalSubtypeXenotypePrefix, StringComparison.OrdinalIgnoreCase) &&
                    defName.Length > LocalSubtypeXenotypePrefix.Length)
                {
                    keys.Add(defName.Substring(LocalSubtypeXenotypePrefix.Length).ToLowerInvariant());
                }
            }

            keys.RemoveWhere(key => key.NullOrEmpty() || string.Equals(key, DefaultSubtypeKey, StringComparison.OrdinalIgnoreCase));
            cachedSubtypeCandidateKeys = keys.Count > 0
                ? keys.OrderBy(key => key).ToList()
                : new List<string> { DefaultSubtypeKey };
            return cachedSubtypeCandidateKeys;
        }

        private static void AddExtractedSubtypeKey(string source, HashSet<string> target)
        {
            if (target == null)
            {
                return;
            }

            string key = TryExtractSubtypeKey(source);
            if (!key.NullOrEmpty())
            {
                target.Add(key);
            }
        }

        private static string TryExtractSubtypeKey(string source)
        {
            if (source.NullOrEmpty())
            {
                return null;
            }

            string lowered = source.ToLowerInvariant();
            if (!ContainsAnyKeyword(lowered, SubtypeAnchorKeywords))
            {
                return null;
            }

            string key = ExtractFirstValidToken(lowered);
            return key.NullOrEmpty() || string.Equals(key, DefaultSubtypeKey, StringComparison.OrdinalIgnoreCase) ? null : key;
        }

        private static string ResolveDynamicSubtypeKey(List<string> sources)
        {
            if (sources.NullOrEmpty())
            {
                return DefaultSubtypeKey;
            }

            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                string source = sources[sourceIndex];
                string lowered = source.ToLowerInvariant();

                int ratkinIndex = lowered.IndexOf("ratkin", StringComparison.Ordinal);
                if (ratkinIndex >= 0)
                {
                    string tail = lowered.Substring(ratkinIndex + "ratkin".Length);
                    string extracted = ExtractFirstValidToken(tail);
                    if (!extracted.NullOrEmpty())
                    {
                        return extracted;
                    }
                }

                string token = ExtractFirstValidToken(lowered);
                if (!token.NullOrEmpty())
                {
                    return token;
                }
            }

            return DefaultSubtypeKey;
        }

        private static string ExtractFirstValidToken(string text)
        {
            if (text.NullOrEmpty())
            {
                return null;
            }

            foreach (string rawToken in SplitTokens(text))
            {
                string normalized = NormalizeToken(rawToken);
                if (!normalized.NullOrEmpty())
                {
                    return normalized;
                }
            }

            return null;
        }

        private static IEnumerable<string> SplitTokens(string text)
        {
            if (text.NullOrEmpty())
            {
                yield break;
            }

            int start = -1;
            for (int i = 0; i < text.Length; i++)
            {
                bool valid = char.IsLetterOrDigit(text[i]);
                if (valid)
                {
                    if (start < 0)
                    {
                        start = i;
                    }

                    continue;
                }

                if (start >= 0)
                {
                    yield return text.Substring(start, i - start);
                    start = -1;
                }
            }

            if (start >= 0)
            {
                yield return text.Substring(start);
            }
        }

        private static string NormalizeToken(string token)
        {
            if (token.NullOrEmpty())
            {
                return null;
            }

            string normalized = token.ToLowerInvariant();
            normalized = normalized.Replace("ratkin", string.Empty)
                                   .Replace("mouse", string.Empty)
                                   .Replace("disaster", string.Empty)
                                   .Replace("adult", string.Empty)
                                   .Replace("child", string.Empty)
                                   .Replace("baby", string.Empty)
                                   .Replace("wild", string.Empty)
                                   .Replace("thief", string.Empty)
                                   .Replace("beggar", string.Empty)
                                   .Replace("trader", string.Empty)
                                   .Replace("鼠族", string.Empty)
                                   .Replace("鼠蛋", string.Empty)
                                   .Replace("野生", string.Empty)
                                   .Replace("偷窃", string.Empty)
                                   .Replace("乞讨", string.Empty)
                                   .Replace("商贩", string.Empty)
                                   .Replace("成人", string.Empty)
                                   .Replace("幼体", string.Empty)
                                   .Replace("婴儿", string.Empty)
                                   .Trim();

            if (normalized.Length < 2 || ContainsAnyKeyword(normalized, GenericTokens))
            {
                return null;
            }

            return normalized;
        }

        private static bool ContainsAnyKeyword(string source, IEnumerable<string> keywords)
        {
            if (source.NullOrEmpty() || keywords == null)
            {
                return false;
            }

            foreach (string keyword in keywords)
            {
                if (!keyword.NullOrEmpty() && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<GeneDef> CreateGenePool()
        {
            return new List<GeneDef>
            {
                MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter,
                MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility,
                MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding,
                MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility,
                MouseDisasterDefOf.MouseDisaster_Gene_MoreComing
            }.Where(def => def != null)
             .Distinct()
             .ToList();
        }

        private static List<GeneDef> CreateGeneList(params GeneDef[] genes)
        {
            return genes
                .Where(gene => gene != null)
                .Distinct()
                .ToList();
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (value.NullOrEmpty())
                {
                    return hash;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }

                return hash;
            }
        }

        private static void ApplySubtypeXenotype(Pawn pawn, string subtypeKey)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null || subtypeKey.NullOrEmpty())
            {
                return;
            }

            XenotypeDef currentXenotype = pawn.genes.Xenotype;
            if (currentXenotype != null &&
                MouseDisasterBirthPolicy.IsRatkinLikeXenotype(currentXenotype.defName, currentXenotype.label) &&
                !MouseDisasterAdaptiveXenotypeUtility.IsVirtualDefaultRatkinXenotype(currentXenotype))
            {
                return;
            }

            XenotypeDef subtypeXenotype = ResolveSubtypeXenotypeDef(subtypeKey);
            if (subtypeXenotype != null && pawn.genes.Xenotype != subtypeXenotype)
            {
                pawn.genes.SetXenotype(subtypeXenotype);
            }
        }

        private static string ResolveLocalXenotypeKey(XenotypeDef xenotype)
        {
            string defName = xenotype?.defName;
            if (defName.NullOrEmpty() ||
                !defName.StartsWith(LocalSubtypeXenotypePrefix, StringComparison.OrdinalIgnoreCase) ||
                defName.Length <= LocalSubtypeXenotypePrefix.Length)
            {
                return null;
            }

            return defName.Substring(LocalSubtypeXenotypePrefix.Length).ToLowerInvariant();
        }

        private static XenotypeDef ResolveSubtypeXenotypeDef(string subtypeKey)
        {
            if (subtypeKey.NullOrEmpty())
            {
                return null;
            }

            if (string.Equals(subtypeKey, DefaultSubtypeKey, StringComparison.OrdinalIgnoreCase))
            {
                XenotypeDef defaultRatkinXenotype = MouseDisasterUtility.ResolveRatkinBaseXenotype();
                if (defaultRatkinXenotype != null)
                {
                    return defaultRatkinXenotype;
                }
            }

            bool isDefaultSubtype = string.Equals(subtypeKey, DefaultSubtypeKey, StringComparison.OrdinalIgnoreCase);
            XenotypeDef localSubtype = DefDatabase<XenotypeDef>.GetNamedSilentFail(LocalSubtypeXenotypePrefix + subtypeKey);
            if (!isDefaultSubtype && localSubtype != null)
            {
                return localSubtype;
            }

            if (ExternalSubtypeXenotypeNames.TryGetValue(subtypeKey, out string[] preferredNames))
            {
                for (int i = 0; i < preferredNames.Length; i++)
                {
                    XenotypeDef external = DefDatabase<XenotypeDef>.GetNamedSilentFail(preferredNames[i]);
                    if (external != null)
                    {
                        return external;
                    }
                }
            }

            if (ExternalSubtypeXenotypeNames.TryGetValue(DefaultSubtypeKey, out string[] fallbackNames))
            {
                for (int i = 0; i < fallbackNames.Length; i++)
                {
                    XenotypeDef fallback = DefDatabase<XenotypeDef>.GetNamedSilentFail(fallbackNames[i]);
                    if (fallback != null)
                    {
                        return fallback;
                    }
                }
            }

            return DefDatabase<XenotypeDef>.GetNamedSilentFail("Ratkin") ?? DefDatabase<XenotypeDef>.GetNamedSilentFail("Baseliner");
        }

        private static bool IsDefaultRatkinXenotype(XenotypeDef xenotype)
        {
            return xenotype != null &&
                   !MouseDisasterAdaptiveXenotypeUtility.IsVirtualDefaultRatkinXenotype(xenotype) &&
                   xenotype == MouseDisasterUtility.ResolveRatkinBaseXenotype();
        }

        private static bool CanResolveSubtypeXenotype(string subtypeKey)
        {
            return ResolveSubtypeXenotypeDef(subtypeKey) != null;
        }

        private static float GetSubtypeSpawnWeight(string subtypeKey)
        {
            if (subtypeKey.NullOrEmpty())
            {
                return 1f;
            }

            if (string.Equals(subtypeKey, "experiment", StringComparison.OrdinalIgnoreCase))
            {
                return 0.6f;
            }

            return 1f;
        }

        private static string ResolveSubtypeKeyFromExternalMarkerGenes(Pawn pawn)
        {
            if (pawn?.genes == null)
            {
                return null;
            }

            foreach (KeyValuePair<string, string[]> pair in ExternalSubtypeMarkerGeneNames)
            {
                List<GeneDef> defs = ResolveGeneDefs(pair.Value);
                if (defs.Count == 0)
                {
                    continue;
                }

                int matched = 0;
                for (int i = 0; i < defs.Count; i++)
                {
                    if (pawn.genes.HasActiveGene(defs[i]))
                    {
                        matched++;
                    }
                }

                if (matched == defs.Count)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private static void ApplyExternalSubtypeMarkers(Pawn pawn, string subtypeKey)
        {
            if (pawn?.genes == null)
            {
                return;
            }

            List<GeneDef> removable = ResolveGeneDefs(ExternalMarkerGeneNamePool);
            for (int i = 0; i < removable.Count; i++)
            {
                Gene existing = pawn.genes.GetGene(removable[i]);
                if (existing != null)
                {
                    pawn.genes.RemoveGene(existing);
                }
            }

            if (!ExternalSubtypeMarkerGeneNames.TryGetValue(subtypeKey ?? string.Empty, out string[] markerNames))
            {
                markerNames = ExternalSubtypeMarkerGeneNames[DefaultSubtypeKey];
            }

            List<GeneDef> targetMarkers = ResolveGeneDefs(markerNames);
            for (int i = 0; i < targetMarkers.Count; i++)
            {
                GeneDef marker = targetMarkers[i];
                if (marker != null && !pawn.genes.HasActiveGene(marker))
                {
                    pawn.genes.AddGene(marker, xenogene: true);
                }
            }
        }

        public static bool IsExternalSubtypeMarkerGene(GeneDef geneDef)
        {
            if (geneDef == null)
            {
                return false;
            }

            for (int i = 0; i < ExternalMarkerGeneNamePool.Length; i++)
            {
                if (geneDef.defName.Equals(ExternalMarkerGeneNamePool[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<GeneDef> ResolveGeneDefs(IEnumerable<string> defNames)
        {
            List<GeneDef> result = new List<GeneDef>();
            if (defNames == null)
            {
                return result;
            }

            foreach (string defName in defNames)
            {
                if (defName.NullOrEmpty())
                {
                    continue;
                }

                GeneDef def = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
                if (def != null && !result.Contains(def))
                {
                    result.Add(def);
                }
            }

            return result;
        }
    }
}
