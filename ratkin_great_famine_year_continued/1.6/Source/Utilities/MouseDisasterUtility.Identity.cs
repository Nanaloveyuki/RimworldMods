using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static bool IsRatkin(Pawn pawn)
        {
            if (pawn?.def == null)
            {
                return false;
            }

            ThingDef expectedRace = ResolveRatkinRaceDef(pawn.kindDef);
            if (expectedRace != null)
            {
                return pawn.def == expectedRace;
            }

            return IsRatkinRaceDef(pawn.def);
        }

        public static bool IsMouseDisasterPawn(Pawn pawn)
        {
            if (!IsRatkin(pawn) || pawn.story == null)
            {
                return false;
            }

            bool hasChildhood = pawn.story.Childhood == MouseDisasterDefOf.MouseDisaster_Newborn ||
                                MouseDisasterPawnHistoryCatalog.IsMouseDisasterHistory(pawn.story.Childhood);
            bool hasAdulthood = pawn.story.Adulthood == null ||
                                pawn.story.Adulthood == MouseDisasterDefOf.MouseDisaster_Refugee ||
                                MouseDisasterPawnHistoryCatalog.IsMouseDisasterHistory(pawn.story.Adulthood);
            return hasChildhood && hasAdulthood;
        }

        public static bool IsMouseEggBaby(Pawn pawn)
        {
            return IsRatkin(pawn) && pawn?.ageTracker != null && pawn.ageTracker.AgeBiologicalYearsFloat < 3f;
        }

        public static bool IsMouseEggOrChild(Pawn pawn)
        {
            if (!IsRatkin(pawn) || pawn == null)
            {
                return false;
            }

            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child)
            {
                return true;
            }

            return pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYearsFloat < 7f;
        }

        public static bool IsPlayerAffiliatedRatkin(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            return pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony;
        }

        public static bool IsEligibleForMouseDisasterGenes(Pawn pawn)
        {
            return ModsConfig.BiotechActive &&
                   pawn?.genes != null &&
                   IsMouseDisasterPawn(pawn);
        }

        public static void MarkMapPawnCacheDirty(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            MarkMapPawnCacheDirty(pawn.MapHeld ?? pawn.Map);
        }

        public static void MarkMapPawnCacheDirty(Map map)
        {
            if (map == null)
            {
                return;
            }

            if (!MapPawnCaches.TryGetValue(map.uniqueID, out MapPawnClassificationCache cache))
            {
                cache = new MapPawnClassificationCache();
                MapPawnCaches[map.uniqueID] = cache;
            }

            cache.dirty = true;
        }

        private static List<Pawn> GetCachedRatkinPawns(Map map)
        {
            return GetMapPawnCache(map).ratkinPawns;
        }

        private static List<Pawn> GetCachedMouseDisasterPawns(Map map)
        {
            return GetMapPawnCache(map).mouseDisasterPawns;
        }

        private static List<Pawn> GetCachedIncidentMoodChildren(Map map)
        {
            return GetMapPawnCache(map).incidentMoodChildren;
        }

        private static List<Pawn> GetCachedDoorStuckCandidates(Map map)
        {
            return GetMapPawnCache(map).doorStuckCandidates;
        }

        private static Dictionary<int, Pawn> GetCachedSpawnedPawnLookup(Map map)
        {
            return GetMapPawnCache(map).spawnedPawnById;
        }

        private static GameComponent_MouseDisasterGeneRestoreState GeneRestoreState => Current.Game?.GetComponent<GameComponent_MouseDisasterGeneRestoreState>();

        private static MapPawnClassificationCache GetMapPawnCache(Map map)
        {
            if (map == null)
            {
                return new MapPawnClassificationCache();
            }

            if (!MapPawnCaches.TryGetValue(map.uniqueID, out MapPawnClassificationCache cache))
            {
                cache = new MapPawnClassificationCache();
                MapPawnCaches[map.uniqueID] = cache;
            }

            if (cache.dirty)
            {
                RebuildMapPawnCache(map, cache);
            }

            return cache;
        }

        private static void RebuildMapPawnCache(Map map, MapPawnClassificationCache cache)
        {
            cache.dirty = false;
            cache.ratkinPawns.Clear();
            cache.mouseDisasterPawns.Clear();
            cache.incidentMoodChildren.Clear();
            cache.doorStuckCandidates.Clear();
            cache.spawnedPawnById.Clear();

            IReadOnlyList<Pawn> pawns = map?.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                return;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead)
                {
                    continue;
                }

                cache.spawnedPawnById[pawn.thingIDNumber] = pawn;

                bool isRatkin = IsRatkin(pawn);
                if (!isRatkin)
                {
                    continue;
                }

                cache.ratkinPawns.Add(pawn);

                if (IsMouseDisasterPawn(pawn))
                {
                    cache.mouseDisasterPawns.Add(pawn);
                }

                if (HasChildExchangeMoodMarker(pawn) || IsMouseDisasterIncidentChild(pawn))
                {
                    cache.incidentMoodChildren.Add(pawn);
                }

                if (IsDoorStuckCandidate(pawn))
                {
                    cache.doorStuckCandidates.Add(pawn);
                }
            }
        }

        public static bool CanParticipateInMouseDisasterPregnancy(Pawn pawn)
        {
            return pawn?.ageTracker != null && pawn.ageTracker.AgeBiologicalYearsFloat >= MinimumMouseDisasterPregnancyAgeYears;
        }

        public static bool CanPrisonerScavenge(Pawn pawn)
        {
            if (!IsPrisonerScavengeEnabled)
            {
                return false;
            }

            return pawn != null &&
                   !pawn.Dead &&
                   !pawn.Downed &&
                   pawn.Spawned &&
                   pawn.Map != null &&
                   pawn.needs?.food != null &&
                   IsMouseEggOrChild(pawn) &&
                   IsColonyCaptiveForScavenge(pawn);
        }

        private static bool IsColonyCaptiveForScavenge(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
            {
                return true;
            }

            return pawn.guest != null &&
                   pawn.guest.HostFaction == Faction.OfPlayer &&
                   (pawn.guest.IsPrisoner || pawn.guest.IsSlave);
        }

        private static bool IsCompatibilitySensitivePawn(Pawn pawn)
        {
            if (pawn == null || !IsMouseEggOrChild(pawn))
            {
                return false;
            }

            return pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony;
        }

        private static bool ShouldUsePassiveAutoOrderMode(Pawn pawn)
        {
            return IsExtremeCompatJobModeEnabled && IsCompatibilitySensitivePawn(pawn);
        }

        private static bool CanAutoOrderJob(Pawn pawn, bool requireStarving)
        {
            if (pawn?.jobs == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
            {
                return false;
            }

            if (!ShouldUsePassiveAutoOrderMode(pawn))
            {
                return true;
            }

            if (pawn.CurJob != null)
            {
                JobDef currentDef = pawn.CurJob.def;
                if (currentDef != JobDefOf.Wait && currentDef != JobDefOf.Wait_Wander && currentDef != JobDefOf.LayDown && currentDef != JobDefOf.GotoWander)
                {
                    return false;
                }
            }

            if (!requireStarving)
            {
                return true;
            }

            return pawn.needs?.food != null && pawn.needs.food.CurCategory == HungerCategory.Starving;
        }

        private static bool TryTakeAutoOrderedJob(Pawn pawn, Job job, JobTag tag, bool requireStarving)
        {
            if (job == null || !CanAutoOrderJob(pawn, requireStarving))
            {
                return false;
            }

            return pawn.jobs.TryTakeOrderedJob(job, tag);
        }

        public static bool CanPrisonerBiteTail(Pawn pawn)
        {
            return IsExperimentalTailBiteEnabled &&
                   pawn != null &&
                   pawn.IsPrisonerOfColony &&
                   CanPrisonerScavenge(pawn) &&
                   pawn.needs.food.CurCategory >= HungerCategory.Hungry;
        }

        public static bool IsWildMouseDisasterKind(Pawn pawn)
        {
            return pawn != null && pawn.kindDef != null &&
                   (pawn.kindDef == MouseDisasterDefOf.MouseDisaster_WildRatkinAdult ||
                    pawn.kindDef == MouseDisasterDefOf.MouseDisaster_WildRatkinChild);
        }

        public static bool IsThiefPawn(Pawn pawn)
        {
            return pawn != null && pawn.kindDef != null &&
                   (pawn.kindDef == MouseDisasterDefOf.MouseDisaster_ThiefRatkinAdult ||
                    pawn.kindDef == MouseDisasterDefOf.MouseDisaster_ThiefRatkinChild);
        }

        public static bool IsInThiefMentalState(Pawn pawn)
        {
            return pawn?.MentalStateDef == MouseDisasterDefOf.MouseDisaster_ThievingState;
        }

        public static bool IsThiefChildPawn(Pawn pawn)
        {
            return pawn != null && pawn.kindDef == MouseDisasterDefOf.MouseDisaster_ThiefRatkinChild;
        }

        public static bool IsBeggarPawn(Pawn pawn)
        {
            return pawn != null && pawn.kindDef != null &&
                   (pawn.kindDef == MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult ||
                    pawn.kindDef == MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild);
        }

        public static bool IsMouseDisasterIncidentVisitor(Pawn pawn)
        {
            // A Ratkin child's age alone is not an incident marker; external Ratkin children must remain untouched.
            return IsBeggarPawn(pawn) ||
                   IsThiefPawn(pawn) ||
                   IsWildMouseDisasterKind(pawn) ||
                   IsMouseDisasterTraderAdult(pawn) ||
                   MouseDisasterVisitorUtility.IsManagedVisitor(pawn) ||
                   IsMouseDisasterIncidentChild(pawn);
        }

        public static bool IsInBeggarMentalState(Pawn pawn)
        {
            return pawn?.MentalStateDef == MouseDisasterDefOf.MouseDisaster_BeggingState;
        }

        public static bool CanMouseDisasterVisitorRetaliate(Pawn pawn)
        {
            if (GameComponent_MouseDisasterEventBehavior.Component?.TryGetGroup(pawn, out var group) == true)
                return group.hostile;
            if (pawn?.mindState == null)
            {
                return false;
            }

            if (IsHostileTo(pawn.Faction, Faction.OfPlayer))
            {
                return true;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            return nowTick - pawn.mindState.lastHarmTick <= 2500;
        }

        public static bool AreWildIncidentsEnabled => MouseDisasterMod.Settings == null || MouseDisasterMod.Settings.enableWildRatkinIncidents;

        public static bool AreThiefIncidentsEnabled => MouseDisasterMod.Settings == null || MouseDisasterMod.Settings.enableThiefIncidents;

        public static bool AreBeggarIncidentsEnabled => MouseDisasterMod.Settings == null || MouseDisasterMod.Settings.enableBeggarIncidents;

        public static bool IsGnawingEnabled => MouseDisasterMod.Settings == null || MouseDisasterMod.Settings.enableGnawing;

        public static bool IsPrisonerScavengeEnabled => MouseDisasterMod.Settings == null || MouseDisasterMod.Settings.enablePrisonerScavenge;

        public static bool IsFloatingTextEnabled => MouseDisasterMod.Settings == null || MouseDisasterMod.Settings.enableFloatingText;

        public static bool IsPrisonerScavengeDebugLogEnabled => MouseDisasterMod.Settings != null && MouseDisasterMod.Settings.enablePrisonerScavengeDebugLog;

        public static bool IsExperimentalTailBiteEnabled => MouseDisasterMod.Settings != null && MouseDisasterMod.Settings.enableExperimentalTailBite;


        public static bool IsExtremeCompatJobModeEnabled
        {
            get
            {
                if (extremeCompatJobModeCached.HasValue)
                {
                    return extremeCompatJobModeCached.Value;
                }

                extremeCompatJobModeCached = ComputeExtremeCompatJobMode();
                return extremeCompatJobModeCached.Value;
            }
        }

        private static bool ComputeExtremeCompatJobMode()
        {
            List<ModContentPack> runningMods = LoadedModManager.RunningModsListForReading;
            for (int i = 0; i < runningMods.Count; i++)
            {
                ModContentPack mod = runningMods[i];
                string name = mod?.Name ?? string.Empty;
                string packageId = mod?.PackageIdPlayerFacing ?? string.Empty;
                for (int keyIndex = 0; keyIndex < ExtremeCompatModKeywords.Length; keyIndex++)
                {
                    string keyword = ExtremeCompatModKeywords[keyIndex];
                    if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        packageId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsBabyExpansionEnabled
        {
            get
            {
                if (babyExpansionEnabledCached.HasValue)
                {
                    return babyExpansionEnabledCached.Value;
                }

                babyExpansionEnabledCached = LoadedModManager.RunningModsListForReading.Any(mod =>
                {
                    string packageId = mod?.PackageIdPlayerFacing ?? string.Empty;
                    return packageId.Equals(BabyExpansionPackageId, StringComparison.OrdinalIgnoreCase);
                });
                return babyExpansionEnabledCached.Value;
            }
        }

        public static bool IsLeadYourPetEnabled
        {
            get
            {
                if (leadYourPetEnabledCached.HasValue)
                {
                    return leadYourPetEnabledCached.Value;
                }

                leadYourPetEnabledCached = LoadedModManager.RunningModsListForReading.Any(mod =>
                {
                    string packageId = mod?.PackageIdPlayerFacing ?? string.Empty;
                    return MouseDisasterBabyLeashPolicy.IsKnownLeadYourPetPackageId(packageId);
                });
                return leadYourPetEnabledCached.Value;
            }
        }

        public static bool IsHospitalityEnabled
        {
            get
            {
                if (hospitalityEnabledCached.HasValue)
                {
                    return hospitalityEnabledCached.Value;
                }

                hospitalityEnabledCached = LoadedModManager.RunningModsListForReading.Any(mod =>
                {
                    string packageId = mod?.PackageIdPlayerFacing ?? string.Empty;
                    return packageId.Equals(HospitalityPackageId, StringComparison.OrdinalIgnoreCase);
                });
                return hospitalityEnabledCached.Value;
            }
        }

        public static bool IsVanillaNutrientPasteExpandedEnabled
        {
            get
            {
                if (vanillaNutrientPasteExpandedEnabledCached.HasValue)
                {
                    return vanillaNutrientPasteExpandedEnabledCached.Value;
                }

                vanillaNutrientPasteExpandedEnabledCached = LoadedModManager.RunningModsListForReading.Any(mod =>
                {
                    string packageId = mod?.PackageIdPlayerFacing ?? string.Empty;
                    return packageId.Equals(VanillaNutrientPasteExpandedPackageId, StringComparison.OrdinalIgnoreCase);
                });
                return vanillaNutrientPasteExpandedEnabledCached.Value;
            }
        }

        private static ThingDef ResolveRatkinRaceDef(PawnKindDef kindDef)
        {
            if (kindDef?.race != null && IsRatkinRaceDef(kindDef.race))
            {
                return kindDef.race;
            }

            if (ratkinRaceResolved)
            {
                return ratkinRaceDef;
            }

            ratkinRaceResolved = true;
            ratkinRaceDef = DefDatabase<ThingDef>.GetNamedSilentFail(RatkinRaceDefName);
            if (ratkinRaceDef?.race == null)
            {
                ratkinRaceDef = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(def => def?.race != null)
                    .OrderByDescending(def => def.defName.EqualsIgnoreCase(RatkinRaceDefName))
                    .ThenByDescending(def => IsRatkinRaceDef(def))
                    .FirstOrDefault(def => IsRatkinRaceDef(def));
            }

            return ratkinRaceDef;
        }

        internal static XenotypeDef ResolveRatkinBaseXenotype()
        {
            if (ratkinXenotypeResolved)
            {
                return ratkinXenotypeDef;
            }

            ratkinXenotypeResolved = true;
            bool preferChineseLabel = IsChineseLanguageActive();
            List<XenotypeDef> ratkinXenotypes = DefDatabase<XenotypeDef>.AllDefsListForReading
                .Where(def => def != null &&
                              IsRatkinXenotypeDef(def) &&
                              !MouseDisasterAdaptiveXenotypeUtility.IsVirtualDefaultRatkinXenotype(def))
                .ToList();

            for (int i = 0; i < PreferredRatkinXenotypeDefNames.Length; i++)
            {
                XenotypeDef preferred = DefDatabase<XenotypeDef>.GetNamedSilentFail(PreferredRatkinXenotypeDefNames[i]);
                if (preferred != null &&
                    IsRatkinXenotypeDef(preferred) &&
                    !MouseDisasterAdaptiveXenotypeUtility.IsVirtualDefaultRatkinXenotype(preferred))
                {
                    ratkinXenotypeDef = preferred;
                    return ratkinXenotypeDef;
                }
            }

            if (ratkinXenotypeDef != null)
            {
                return ratkinXenotypeDef;
            }

            if (preferChineseLabel)
            {
                XenotypeDef localized = ratkinXenotypes
                    .Where(def => HasChineseCharacters(def.label) || def.defName.EqualsIgnoreCase(RatkinXenotypeDefName))
                    .OrderByDescending(def => def.defName.EqualsIgnoreCase("RK_XenoType_Ratkin"))
                    .ThenByDescending(def => def.defName.EqualsIgnoreCase(RatkinXenotypeDefName))
                    .ThenByDescending(def => HasChineseCharacters(def.label))
                    .ThenBy(def => def.defName)
                    .FirstOrDefault();
                if (localized != null)
                {
                    ratkinXenotypeDef = localized;
                    return ratkinXenotypeDef;
                }
            }

            ratkinXenotypeDef = ratkinXenotypes
                .OrderBy(def => (def.defName ?? string.Empty).IndexOf("waster", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(IsRatkinXenotypeDef)
                .ThenByDescending(def => def.defName.EqualsIgnoreCase("RK_XenoType_Ratkin"))
                .ThenByDescending(def => def.defName.EqualsIgnoreCase(RatkinXenotypeDefName))
                .FirstOrDefault(IsRatkinXenotypeDef);
            return ratkinXenotypeDef;
        }

        private static List<XenotypeDef> ResolveAllowedRatkinXenotypes()
        {
            List<XenotypeDef> allowed = MouseDisasterAdaptiveXenotypeUtility.GetEnabledCandidates();
            for (int i = 0; i < allowed.Count; i++)
            {
                if (!MouseDisasterAdaptiveXenotypeUtility.IsVirtualDefaultRatkinXenotype(allowed[i]))
                {
                    continue;
                }

                allowed[i] = ResolveRatkinBaseXenotype();
            }

            allowed = allowed
                .Where(def => def != null)
                .Distinct()
                .ToList();
            if (allowed.Count == 0 && ModsConfig.BiotechActive)
            {
                XenotypeDef defaultRatkinXenotype = ResolveRatkinBaseXenotype();
                if (defaultRatkinXenotype != null)
                {
                    allowed.Add(defaultRatkinXenotype);
                }
                else if (XenotypeDefOf.Baseliner != null)
                {
                    allowed.Add(XenotypeDefOf.Baseliner);
                }
            }

            return allowed;
        }

        private static bool IsChineseLanguageActive()
        {
            LoadedLanguage language = LanguageDatabase.activeLanguage;
            if (language == null)
            {
                return false;
            }

            string folder = language.folderName ?? string.Empty;
            string englishName = language.FriendlyNameEnglish ?? string.Empty;
            string nativeName = language.FriendlyNameNative ?? string.Empty;
            return folder.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   englishName.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   nativeName.IndexOf("中文", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasChineseCharacters(string text)
        {
            if (text.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= '\u3400' && c <= '\u4DBF') || (c >= '\u4E00' && c <= '\u9FFF'))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRatkinXenotypeDef(XenotypeDef xenotypeDef)
        {
            if (xenotypeDef == null)
            {
                return false;
            }

            if (IsRatkinXenotypeDefCache.TryGetValue(xenotypeDef, out bool cached))
            {
                return cached;
            }

            bool result = MouseDisasterBirthPolicy.IsRatkinLikeXenotype(xenotypeDef.defName, xenotypeDef.label);
            IsRatkinXenotypeDefCache[xenotypeDef] = result;
            return result;
        }

        private static bool IsRatkinRaceDef(ThingDef raceDef)
        {
            if (raceDef?.race == null)
            {
                return false;
            }

            if (IsRatkinRaceDefCache.TryGetValue(raceDef, out bool cached))
            {
                return cached;
            }

            bool result = false;
            if (raceDef.defName.EqualsIgnoreCase(RatkinRaceDefName))
            {
                result = true;
            }
            else
            {
                string source = ((raceDef.defName ?? string.Empty) + " " + (raceDef.label ?? string.Empty)).ToLowerInvariant();
                for (int i = 0; i < RatkinRaceKeywords.Length; i++)
                {
                    if (source.Contains(RatkinRaceKeywords[i]))
                    {
                        result = true;
                        break;
                    }
                }
            }

            IsRatkinRaceDefCache[raceDef] = result;
            return result;
        }
    }
}
