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

        public static bool TryFindFormerFaction(out Faction formerFaction)
        {
            int groupId = MouseDisasterEventExecution.Current?.groupId ?? 0;
            if (groupId > 0)
            {
                formerFaction = GameComponent_MouseDisasterEventBehavior.Component?.FactionForGroup(groupId);
                if (formerFaction != null) return true;
            }
            return TryGetMouseDisasterHiddenFaction(out formerFaction);
        }

        public static bool TryGetMouseDisasterHiddenFaction(out Faction faction)
        {
            faction = null;
            if (Current.CreatingWorld != null ||
                Find.FactionManager == null || MouseDisasterDefOf.MouseDisaster_HiddenFaction == null)
            {
                return false;
            }

            if (mouseDisasterHiddenFaction != null && Find.FactionManager.AllFactionsListForReading.Contains(mouseDisasterHiddenFaction))
            {
                faction = mouseDisasterHiddenFaction;
                NormalizeHiddenFactionDisplayName(faction);
                EnsureHiddenFactionRelations(faction);
                return true;
            }

            faction = Find.FactionManager.FirstFactionOfDef(MouseDisasterDefOf.MouseDisaster_HiddenFaction);
            if (faction == null)
            {
                if (!MouseDisasterRuntime.AllowsNewContent) return false;
                faction = CreateMouseDisasterHiddenFaction();
                if (faction == null)
                {
                    return false;
                }

                Find.FactionManager.Add(faction);
            }

            mouseDisasterHiddenFaction = faction;
            NormalizeHiddenFactionDisplayName(faction);
            EnsureHiddenFactionRelations(faction);
            return faction != null;
        }

        private static Faction CreateMouseDisasterHiddenFaction(FactionDef requestedDef = null)
        {
            FactionDef factionDef = requestedDef ?? MouseDisasterDefOf.MouseDisaster_HiddenFaction;
            if (factionDef == null || Find.UniqueIDsManager == null)
            {
                return null;
            }

            Faction faction = new Faction
            {
                def = factionDef,
                loadID = Find.UniqueIDsManager.GetNextFactionID(),
                hidden = true
            };
            faction.colorFromSpectrum = FactionGenerator.NewRandomColorFromSpectrum(faction);

            if (factionDef.humanlikeFaction)
            {
                faction.ideos = new FactionIdeosTracker(faction);
                faction.ideos.ChooseOrGenerateIdeo(new IdeoGenerationParms(
                    factionDef,
                    forceNoExpansionIdeo: false,
                    forcedMemes: factionDef.forcedMemes,
                    classicExtra: false,
                    forceNoWeaponPreference: false,
                    forNewFluidIdeo: false,
                    fixedIdeo: factionDef.fixedIdeo,
                    name: factionDef.ideoName,
                    styles: factionDef.styles,
                    deities: factionDef.deityPresets,
                    hidden: factionDef.hiddenIdeo,
                    description: factionDef.ideoDescription,
                    requiredPreceptsOnly: factionDef.requiredPreceptsOnly));
            }

            faction.Name = !factionDef.fixedName.NullOrEmpty()
                ? factionDef.fixedName
                : HiddenFactionChineseName;
            return faction;
        }

        public static void TryRefreshHiddenFactionRelations()
        {
            if (TryGetMouseDisasterHiddenFaction(out Faction faction))
            {
                EnsureHiddenFactionRelations(faction);
            }
        }

        public static void TryRepairMissingFactionRelations()
        {
            if (Find.FactionManager == null)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            IReadOnlyList<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            if (factions == null || factions.Count == 0)
            {
                return;
            }

            if (factions.Count == lastFactionRelationRepairFactionCount && nowTick - lastFactionRelationRepairTick < MissingFactionRelationRepairIntervalTicks)
            {
                return;
            }

            lastFactionRelationRepairTick = nowTick;
            lastFactionRelationRepairFactionCount = factions.Count;
            int repairedRelations = 0;

            for (int i = 0; i < factions.Count; i++)
            {
                Faction first = factions[i];
                if (first == null)
                {
                    continue;
                }

                for (int j = i + 1; j < factions.Count; j++)
                {
                    Faction second = factions[j];
                    if (second == null || (!IsMouseDisasterManagedFaction(first) && !IsMouseDisasterManagedFaction(second)))
                    {
                        continue;
                    }

                    if (EnsureFactionRelationExists(first, second))
                    {
                        repairedRelations++;
                    }

                    if (EnsureFactionRelationExists(second, first))
                    {
                        repairedRelations++;
                    }
                }
            }

            if (repairedRelations > 0 && IsPrisonerScavengeDebugLogEnabled)
            {
                Log.Message("[MouseDisaster][FactionRelationRepair] repaired missing relations: " + repairedRelations);
            }
        }

        public static void MakeFactionHostileToPlayer(Faction faction, bool explicitDriveAway = false)
        {
            if (faction == null || Faction.OfPlayer == null)
            {
                return;
            }

            if (IsMouseDisasterManagedFaction(faction) && !IsFactionListed(faction))
            {
                return;
            }

            if (IsHostileTo(faction, Faction.OfPlayer))
            {
                return;
            }

            bool hiddenFaction = IsMouseDisasterHiddenFaction(faction);
            if (hiddenFaction &&
                !MouseDisasterVisitorHostilityPolicy.ShouldForceHiddenFactionHostility(explicitDriveAway, hiddenFaction))
            {
                EnsureFactionRelationWithPlayer(faction, FactionRelationKind.Neutral, 0);
                return;
            }

            if (hiddenFaction && explicitDriveAway)
            {
                hiddenFactionExplicitHostilityUntilTick = (Find.TickManager?.TicksGame ?? 0) + HiddenFactionExplicitHostilityDurationTicks;
            }

            Faction.OfPlayer.TryAffectGoodwillWith(faction, Faction.OfPlayer.GoodwillToMakeHostile(faction), canSendMessage: false, canSendHostilityLetter: false);
            faction.TryAffectGoodwillWith(Faction.OfPlayer, faction.GoodwillToMakeHostile(Faction.OfPlayer), canSendMessage: false, canSendHostilityLetter: false);
            EnsureFactionRelationWithPlayer(faction, FactionRelationKind.Hostile, -100);
        }

        public static void MakeFactionNeutralToPlayer(Faction faction, bool force = false)
        {
            if (IsEventBehaviorFaction(faction)) return;
            if (faction == null || Faction.OfPlayer == null)
            {
                return;
            }

            bool hiddenFaction = IsMouseDisasterHiddenFaction(faction);
            if (hiddenFaction)
            {
                hiddenFactionExplicitHostilityUntilTick = 0;
                cachedHostilePawnCheckTick = -1;
                cachedHostilePawnExists = false;
            }

            EnsureFactionRelationWithPlayer(faction, FactionRelationKind.Neutral, hiddenFaction ? 0 : 20);
        }

        private static void EnsureHiddenFactionRelations(Faction faction)
        {
            if (faction == null || Find.FactionManager == null)
            {
                return;
            }

            if (Faction.OfPlayer != null && !IsHiddenFactionExplicitHostilityActive())
            {
                EnsureFactionRelationPair(faction, Faction.OfPlayer, FactionRelationKind.Neutral, 0);
            }

            IReadOnlyList<Faction> allFactions = Find.FactionManager.AllFactionsListForReading;
            if (allFactions == null || allFactions.Count == lastHiddenFactionRelationSyncFactionCount)
            {
                return;
            }

            lastHiddenFactionRelationSyncFactionCount = allFactions.Count;
            for (int i = 0; i < allFactions.Count; i++)
            {
                Faction other = allFactions[i];
                if (other == null || other == faction || other == Faction.OfPlayer)
                {
                    continue;
                }

                bool eventFaction = IsEventBehaviorFaction(other);
                EnsureFactionRelationPair(faction, other, eventFaction ? FactionRelationKind.Neutral : FactionRelationKind.Hostile, eventFaction ? 0 : -100);
            }
        }

        public static void EnsureMouseDisasterFactionNeutralOnMap(Map map, Faction faction)
        {
            if (map == null || faction == null)
            {
                return;
            }

            MakeFactionNeutralToPlayer(faction, force: true);
            IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                return;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || pawn.Faction != faction || !IsRatkin(pawn))
                {
                    continue;
                }

                if (pawn.InAggroMentalState)
                {
                    pawn.mindState?.mentalStateHandler?.Reset();
                }
            }
        }

        private static void EnsureFactionRelationWithPlayer(Faction faction, FactionRelationKind kind, int goodwill)
        {
            if (faction == null || Faction.OfPlayer == null)
            {
                return;
            }

            bool hiddenFaction = IsMouseDisasterHiddenFaction(faction);
            bool explicitDriveAway = hiddenFaction && kind == FactionRelationKind.Hostile && IsHiddenFactionExplicitHostilityActive();
            if (!MouseDisasterVisitorHostilityPolicy.ShouldKeepRequestedRelationKind(hiddenFaction, explicitDriveAway))
            {
                kind = FactionRelationKind.Neutral;
                goodwill = 0;
            }

            EnsureFactionRelationPair(faction, Faction.OfPlayer, kind, goodwill);
        }

        private static bool IsMouseDisasterHiddenFaction(Faction faction)
        {
            return ClassifyManagedFaction(faction) == MouseDisasterManagedFactionKind.Hidden;
        }

        private static bool IsHiddenFactionExplicitHostilityActive()
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (hiddenFactionExplicitHostilityUntilTick > nowTick)
            {
                return true;
            }

            if (cachedHostilePawnCheckTick == nowTick)
            {
                return cachedHostilePawnExists;
            }

            cachedHostilePawnCheckTick = nowTick;
            cachedHostilePawnExists = false;

            if (Find.Maps == null)
            {
                return false;
            }

            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                Map map = Find.Maps[mapIndex];
                if (map?.mapPawns == null)
                {
                    continue;
                }

                IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < spawned.Count; i++)
                {
                    Pawn pawn = spawned[i];
                    if (pawn != null &&
                        !pawn.Dead &&
                        IsRatkin(pawn) &&
                        pawn.Faction != null &&
                        IsMouseDisasterHiddenFaction(pawn.Faction) &&
                        Faction.OfPlayer != null &&
                        IsHostileTo(pawn.Faction, Faction.OfPlayer))
                    {
                        cachedHostilePawnExists = true;
                        return true;
                    }
                }
            }

            return false;
        }

        private static void EnsureFactionRelationPair(Faction first, Faction second, FactionRelationKind kind, int goodwill)
        {
            if (first == null || second == null || first == second)
            {
                return;
            }

            FactionRelation relationToSecond = first.RelationWith(second, allowNull: true);
            if (relationToSecond == null)
            {
                first.SetRelation(new FactionRelation
                {
                    other = second,
                    kind = kind,
                    baseGoodwill = goodwill
                });
                relationToSecond = first.RelationWith(second, allowNull: true);
            }

            if (relationToSecond != null)
            {
                relationToSecond.kind = kind;
                relationToSecond.baseGoodwill = goodwill;
            }

            FactionRelation relationToFirst = second.RelationWith(first, allowNull: true);
            if (relationToFirst == null)
            {
                second.SetRelation(new FactionRelation
                {
                    other = first,
                    kind = kind,
                    baseGoodwill = goodwill
                });
                relationToFirst = second.RelationWith(first, allowNull: true);
            }

            if (relationToFirst != null)
            {
                relationToFirst.kind = kind;
                relationToFirst.baseGoodwill = goodwill;
            }
        }

        private static bool EnsureFactionRelationExists(Faction owner, Faction other)
        {
            if (owner == null || other == null || owner == other)
            {
                return false;
            }

            FactionRelation relation = owner.RelationWith(other, allowNull: true);
            if (relation != null)
            {
                return false;
            }

            ResolveManagedFactionRelation(owner, other, out FactionRelationKind kind, out int goodwill);
            owner.SetRelation(new FactionRelation
            {
                other = other,
                kind = kind,
                baseGoodwill = goodwill
            });
            return true;
        }

        internal static MouseDisasterManagedFactionKind ClassifyManagedFaction(Faction faction)
        {
            if (faction?.def == null)
            {
                return MouseDisasterManagedFactionKind.None;
            }

            if (MouseDisasterDefOf.MouseDisaster_HiddenFaction != null &&
                faction.def == MouseDisasterDefOf.MouseDisaster_HiddenFaction)
            {
                return MouseDisasterManagedFactionKind.Hidden;
            }

            if (MouseDisasterDefOf.MouseDisaster_NeutralVisitors != null &&
                faction.def == MouseDisasterDefOf.MouseDisaster_NeutralVisitors)
            {
                return MouseDisasterManagedFactionKind.NeutralVisitors;
            }

            if (MouseDisasterDefOf.MouseDisaster_HostileVisitors != null &&
                faction.def == MouseDisasterDefOf.MouseDisaster_HostileVisitors)
            {
                return MouseDisasterManagedFactionKind.HostileVisitors;
            }

            if (MouseDisasterDefOf.MouseDisaster_FriendlyVisitors != null &&
                faction.def == MouseDisasterDefOf.MouseDisaster_FriendlyVisitors)
            {
                return MouseDisasterManagedFactionKind.FriendlyVisitors;
            }

            return MouseDisasterManagedFactionKind.None;
        }

        internal static bool IsMouseDisasterManagedFaction(Faction faction)
        {
            return ClassifyManagedFaction(faction) != MouseDisasterManagedFactionKind.None;
        }

        public static bool IsHostileTo(Faction faction, Faction other)
        {
            if (faction == null || other == null || faction == other)
            {
                return false;
            }

            if (IsMouseDisasterManagedFaction(faction) || IsMouseDisasterManagedFaction(other))
            {
                return TryGetOrRepairManagedFactionRelation(faction, other, out FactionRelation relation) &&
                       relation != null &&
                       relation.kind == FactionRelationKind.Hostile;
            }

            return faction.HostileTo(other);
        }

        internal static bool ShouldSkipIdentityNormalizationDuringFactionTeardown(Pawn pawn)
        {
            Faction faction = pawn?.Faction;
            return MouseDisasterFactionRelationPolicy.ShouldSkipIdentityNormalizationDuringFactionTeardown(
                IsMouseDisasterManagedFaction(faction),
                IsFactionListed(faction));
        }

        internal static bool TryGetOrRepairManagedFactionRelation(Faction owner, Faction other, out FactionRelation relation)
        {
            relation = null;
            if (owner == null || other == null || owner == other)
            {
                return false;
            }

            relation = owner.RelationWith(other, allowNull: true);
            if (relation != null)
            {
                return true;
            }

            ResolveManagedFactionRelation(owner, other, out FactionRelationKind kind, out int goodwill);
            if (MouseDisasterFactionRelationPolicy.ShouldPersistRepairedRelation(IsFactionListed(owner), IsFactionListed(other)))
            {
                EnsureFactionRelationPair(owner, other, kind, goodwill);
                relation = owner.RelationWith(other, allowNull: true);
                if (relation != null)
                {
                    return true;
                }
            }

            relation = new FactionRelation
            {
                other = other,
                kind = kind,
                baseGoodwill = goodwill
            };
            return true;
        }

        private static bool IsFactionListed(Faction faction)
        {
            if (faction == null || Find.FactionManager == null)
            {
                return false;
            }

            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            if (factions == null)
            {
                return false;
            }

            for (int i = 0; i < factions.Count; i++)
            {
                if (factions[i] == faction)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResolveManagedFactionRelation(Faction first, Faction second, out FactionRelationKind kind, out int goodwill)
        {
            MouseDisasterFactionRelationPolicy.ResolveDefaultRelation(
                ClassifyManagedFaction(first),
                first != null && first.IsPlayer,
                first?.def != null && first.def.permanentEnemy,
                ClassifyManagedFaction(second),
                second != null && second.IsPlayer,
                second?.def != null && second.def.permanentEnemy,
                out kind,
                out goodwill);
        }

        internal static Faction GetEventFaction(bool hostile, bool friendly)
        {
            if (Current.CreatingWorld != null || Find.FactionManager == null) return null;
            FactionDef def = hostile ? MouseDisasterDefOf.MouseDisaster_HostileVisitors : friendly ?
                MouseDisasterDefOf.MouseDisaster_FriendlyVisitors : MouseDisasterDefOf.MouseDisaster_NeutralVisitors;
            Faction faction = CreateMouseDisasterHiddenFaction(def);
            if (faction == null) return null;
            faction.temporary = true;
            Find.FactionManager.Add(faction);
            foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
            {
                if (other == faction) continue;
                bool sameMod = other.def == MouseDisasterDefOf.MouseDisaster_HiddenFaction || IsEventBehaviorFaction(other);
                FactionRelationKind kind = other.IsPlayer ? (hostile ? FactionRelationKind.Hostile : friendly ? FactionRelationKind.Ally : FactionRelationKind.Neutral) :
                    (sameMod ? FactionRelationKind.Neutral : FactionRelationKind.Hostile);
                EnsureFactionRelationPair(faction, other, kind, kind == FactionRelationKind.Ally ? 100 : kind == FactionRelationKind.Hostile ? -100 : 0);
            }
            return faction;
        }

        internal static bool IsEventBehaviorFaction(Faction faction)
        {
            MouseDisasterManagedFactionKind kind = ClassifyManagedFaction(faction);
            return kind == MouseDisasterManagedFactionKind.NeutralVisitors ||
                   kind == MouseDisasterManagedFactionKind.HostileVisitors ||
                   kind == MouseDisasterManagedFactionKind.FriendlyVisitors;
        }

        public static bool IsMouseDisasterNeutralFaction(Faction faction)
        {
            return faction != null &&
                   (IsMouseDisasterHiddenFaction(faction) ||
                    (MouseDisasterDefOf.MouseDisaster_NeutralVisitors != null &&
                     faction.def == MouseDisasterDefOf.MouseDisaster_NeutralVisitors));
        }

        private static void NormalizeHiddenFactionDisplayName(Faction faction)
        {
            if (faction == null || MouseDisasterDefOf.MouseDisaster_HiddenFaction == null || faction.def != MouseDisasterDefOf.MouseDisaster_HiddenFaction)
            {
                return;
            }

            if (!IsChineseLanguageActive())
            {
                return;
            }

            string currentName = faction.Name ?? string.Empty;
            if (!HasChineseCharacters(currentName) ||
                currentName.IndexOf("MouseDisaster", StringComparison.OrdinalIgnoreCase) >= 0 ||
                currentName.IndexOf("survivor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                faction.Name = HiddenFactionChineseName;
            }
        }
    }
}
