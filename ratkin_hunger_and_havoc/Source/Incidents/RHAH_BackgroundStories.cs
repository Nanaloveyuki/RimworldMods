using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal enum RHAH_BackgroundStory
    {
        Rebellion = 0,
        Ruins = 1,
        Unrest = 2,
        Mutiny = 3,
        CivilWar = 4,
        TwoTaxes = 5,
        SameCallsign = 6,
        HarvestFestival = 7,
        Memorial = 8,
        AidManifest = 9,
        CancelledBanquet = 10
    }

    internal static class RHAH_BackgroundStories
    {
        internal const string KingdomDef = "Rakinia";
        internal const string WarlordDef = "Rakinia_Warlord";
        internal const int MinimumKingdomSettlements = 3;
        internal const RHAH_ContentRating ContentRating = RHAH_ContentRating.AllAges;

        internal static void Tick(int tick)
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings?.background;
            if (!RHAH_Runtime.AllowsNewContent || options == null || !options.enabled || options.averageDays <= 0f ||
                GenDate.DaysPassedSinceSettleFloat <= RHAH_IncidentSchedule.GraceDays ||
                !Rand.MTBEventOccurs(options.averageDays, GenDate.TicksPerDay, GenDate.TicksPerHour)) return;

            Faction kingdom = FindFaction(KingdomDef);
            Map home = Find.AnyPlayerHomeMap;
            if (kingdom == null || home == null) return;
            RHAH_BackgroundStory? story = Select(options, Rand.Value);
            if (story.HasValue) TrySend(story.Value, kingdom, home, tick);
        }

        internal static RHAH_BackgroundStory? Select(RHAH_BackgroundOptions options, float roll)
        {
            if (options == null || !options.enabled || float.IsNaN(roll) || roll < 0f || roll >= 1f) return null;
            float total = 0f;
            for (int i = 0; i <= 10; i++) total += Weight(options, (RHAH_BackgroundStory)i);
            if (total <= 0f) return null;
            float remaining = roll * total;
            for (int i = 0; i <= 10; i++)
            {
                float weight = Weight(options, (RHAH_BackgroundStory)i);
                if (remaining < weight) return (RHAH_BackgroundStory)i;
                remaining -= weight;
            }
            return null;
        }

        static float Weight(RHAH_BackgroundOptions options, RHAH_BackgroundStory story)
        {
            switch (story)
            {
                case RHAH_BackgroundStory.Rebellion: return options.rebellionEnabled ? options.rebellionWeight : 0f;
                case RHAH_BackgroundStory.Ruins: return options.ruinsEnabled ? options.ruinsWeight : 0f;
                case RHAH_BackgroundStory.Unrest: return options.unrestEnabled ? options.unrestWeight : 0f;
                case RHAH_BackgroundStory.Mutiny: return options.mutinyEnabled ? options.mutinyWeight : 0f;
                case RHAH_BackgroundStory.TwoTaxes: return options.twoTaxesEnabled ? options.twoTaxesWeight : 0f;
                case RHAH_BackgroundStory.SameCallsign: return options.sameCallsignEnabled ? options.sameCallsignWeight : 0f;
                case RHAH_BackgroundStory.HarvestFestival: return options.harvestFestivalEnabled ? options.harvestFestivalWeight : 0f;
                case RHAH_BackgroundStory.Memorial: return options.memorialEnabled ? options.memorialWeight : 0f;
                case RHAH_BackgroundStory.AidManifest: return options.aidManifestEnabled ? options.aidManifestWeight : 0f;
                case RHAH_BackgroundStory.CancelledBanquet: return options.cancelledBanquetEnabled ? options.cancelledBanquetWeight : 0f;
                default: return 0f;
            }
        }

        internal static bool TrySend(RHAH_BackgroundStory story, Faction kingdom, Map home, int tick)
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings?.background;
            if (!RHAH_Runtime.AllowsNewContent || options == null || !options.enabled || !LivingFaction(kingdom) ||
                (story != RHAH_BackgroundStory.CivilWar && Weight(options, story) <= 0f) ||
                (story == RHAH_BackgroundStory.CivilWar && !options.civilWarEnabled)) return false;

            WorldObject target = null;
            switch (story)
            {
                case RHAH_BackgroundStory.Rebellion:
                    if (options.civilWarEnabled) RHAH_BackgroundFollow.StartCivilWar(kingdom, tick, options.civilWarDelayDays);
                    break;
                case RHAH_BackgroundStory.Ruins:
                    target = CreateRuins(home, options);
                    if (target == null) return false;
                    break;
                case RHAH_BackgroundStory.Unrest:
                    GameComponent_RHAH_Game game = Current.Game.GetComponent<GameComponent_RHAH_Game>();
                    game.BackgroundUnrestUntilTick = Math.Max(game.BackgroundUnrestUntilTick, DueTick(tick, options.unrestDays));
                    break;
                case RHAH_BackgroundStory.TwoTaxes:
                    Faction neighbor = FindTaxNeighbor(kingdom);
                    if (neighbor == null) return false;
                    SendLetter(story, kingdom, null, neighbor.Name);
                    return true;
                case RHAH_BackgroundStory.SameCallsign:
                case RHAH_BackgroundStory.Memorial:
                    target = WorldObject_RHAH_BackgroundSite.Create(story, home, options);
                    if (target == null) return false;
                    break;
                case RHAH_BackgroundStory.HarvestFestival:
                case RHAH_BackgroundStory.AidManifest:
                case RHAH_BackgroundStory.CancelledBanquet:
                    if (!RHAH_BackgroundTradeFollow.Start(story, kingdom, tick)) return false;
                    break;
            }
            SendLetter(story, kingdom, target);
            return true;
        }

        internal static void SendLetter(RHAH_BackgroundStory story, Faction kingdom, WorldObject target = null, string neighbor = "")
        {
            string prefix = "RHAH_Background_" + story;
            ChoiceLetter_RHAH_Background letter = (ChoiceLetter_RHAH_Background)LetterMaker.MakeLetter(
                (prefix + "_Label").Translate(), (prefix + "_Text").Translate(kingdom.Name, neighbor,
                    RHAH_Mod.Settings.background.tradeDiscountPercent.ToString("0.#")),
                DefDatabase<LetterDef>.GetNamed("RHAH_BackgroundLetter"), target == null ? null : new LookTargets(target));
            letter.Story = story;
            letter.relatedFaction = kingdom;
            Find.LetterStack.ReceiveLetter(letter);
        }

        static Faction FindTaxNeighbor(Faction kingdom)
        {
            List<Settlement> settlements = Find.WorldObjects.Settlements;
            Settlement border = null;
            float closest = float.MaxValue;
            foreach (Settlement town in settlements)
            {
                if (town.Faction != kingdom || !town.Tile.Valid) continue;
                foreach (Settlement other in settlements)
                {
                    Faction faction = other.Faction;
                    if (faction == kingdom || !LivingFaction(faction) || faction.def.hidden || !faction.def.humanlikeFaction || !other.Tile.Valid) continue;
                    float distance = Find.WorldGrid.ApproxDistanceInTiles(town.Tile, other.Tile);
                    if (distance >= closest) continue;
                    closest = distance;
                    border = other;
                }
            }
            return border?.Faction;
        }

        static WorldObject_RHAH_Ruins CreateRuins(Map home, RHAH_BackgroundOptions options)
        {
            if (home == null || !home.Tile.Valid || DefDatabase<ThingDef>.GetNamedSilentFail(WorldObject_RHAH_Ruins.HardtackDef) == null)
                return null;
            if (!TileFinder.TryFindPassableTileWithTraversalDistance(home.Tile, options.ruinsDistance.min,
                options.ruinsDistance.max, out PlanetTile tile,
                candidate => TileFinder.IsValidTileForNewSettlement(candidate))) return null;
            WorldObject_RHAH_Ruins ruins = (WorldObject_RHAH_Ruins)WorldObjectMaker.MakeWorldObject(
                DefDatabase<WorldObjectDef>.GetNamed("RHAH_Ruins"));
            ruins.Tile = tile;
            ruins.Configure(options.hardtack.RandomInRange, options.silver.RandomInRange, options.components.RandomInRange);
            Find.WorldObjects.Add(ruins);
            return ruins;
        }

        internal static int DueTick(int tick, float days)
        {
            return (int)Math.Min(int.MaxValue, (long)tick + Math.Max(1L, (long)(days * GenDate.TicksPerDay)));
        }

        internal static float NegativeRateMultiplier(int tick)
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings?.background;
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            return UnrestMultiplier(options, game?.BackgroundUnrestUntilTick ?? -1, tick);
        }

        internal static float UnrestMultiplier(RHAH_BackgroundOptions options, int untilTick, int tick)
        {
            return options?.enabled == true && options.unrestEnabled && tick < untilTick
                ? 1f + options.unrestBonusPercent / 100f : 1f;
        }

        internal static bool LivingFaction(Faction faction)
        {
            return faction != null && !faction.IsPlayer && !faction.defeated && !faction.temporary;
        }

        internal static Faction FindFaction(string defName)
        {
            List<Faction> factions = Find.FactionManager?.AllFactionsListForReading;
            for (int i = 0; factions != null && i < factions.Count; i++)
                if (factions[i].def.defName == defName && LivingFaction(factions[i])) return factions[i];
            return null;
        }

        internal static bool CanTakeSettlement(int kingdomCount)
        {
            return kingdomCount > MinimumKingdomSettlements;
        }

        internal static bool TryTakeSettlement(Faction kingdom, Faction claimant)
        {
            if (!LivingFaction(kingdom) || !LivingFaction(claimant) || kingdom == claimant) return false;
            List<Settlement> settlements = Find.WorldObjects.Settlements;
            int kingdomCount = 0;
            List<Settlement> candidates = new List<Settlement>();
            for (int i = 0; i < settlements.Count; i++)
            {
                Settlement settlement = settlements[i];
                if (!settlement.Spawned || settlement.Destroyed || settlement.Faction != kingdom) continue;
                kingdomCount++;
                if (!settlement.HasMap) candidates.Add(settlement);
            }
            if (!CanTakeSettlement(kingdomCount) || candidates.Count == 0) return false;
            Settlement captured = candidates.RandomElement();
            // 重建据点让原版派系材质与交易库存随占领方更新
            Settlement replacement = (Settlement)WorldObjectMaker.MakeWorldObject(captured.def);
            replacement.Tile = captured.Tile;
            replacement.SetFaction(claimant);
            replacement.Name = captured.Name;
            captured.Destroy();
            Find.WorldObjects.Add(replacement);
            Find.LetterStack.ReceiveLetter("RHAH_Background_Takeover_Label".Translate(),
                "RHAH_Background_Takeover_Text".Translate(claimant.Name, replacement.Label),
                LetterDefOf.NeutralEvent, new LookTargets(replacement), claimant);
            return true;
        }
    }
}
