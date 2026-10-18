using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Pawn;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Incidents
{
    public class WorldObject_RHAH_RefugeeCamp : Site
    {
        List<Verse.Pawn> residents = new List<Verse.Pawn>();
        bool cleared;
        int nextCheck = -1;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref residents, "residents", LookMode.Reference);
            Scribe_Values.Look(ref cleared, "cleared", false);
            Scribe_Values.Look(ref nextCheck, "nextCheck", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                residents = residents ?? new List<Verse.Pawn>();
            }
        }

        public void AddResident(Verse.Pawn pawn)
        {
            if (pawn != null && !residents.Contains(pawn))
            {
                residents.Add(pawn);
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (cleared || !HasMap || residents.Count == 0 || Find.TickManager.TicksGame < nextCheck)
            {
                return;
            }

            nextCheck = Find.TickManager.TicksGame + GenDate.TicksPerHour * 2;
            if (!AllDead())
            {
                return;
            }

            cleared = true;
            QuestUtility.SendQuestTargetSignals(questTags, "ResidentsCleared");
        }

        bool AllDead()
        {
            List<bool> dead = new List<bool>();
            for (int i = 0; i < residents.Count; i++)
            {
                Verse.Pawn pawn = residents[i];
                bool missing = pawn == null || pawn.Destroyed;
                bool downed = pawn != null && pawn.Downed;
                bool prisoner = pawn != null && pawn.IsPrisoner;
                bool onMap = pawn != null && pawn.Spawned;
                dead.Add(RHAH_RefugeeCampRules.CountsAsDead(missing, pawn != null && pawn.Dead, downed, prisoner, onMap));
            }

            return RHAH_RefugeeCampRules.Cleared(dead);
        }
    }

    public class QuestNode_RHAH_RefugeeCamp : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return slate.Get<Faction>("sponsor") != null && slate.Get<PlanetTile>("tile").Valid;
        }

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Faction sponsor = slate.Get<Faction>("sponsor");
            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail("RHAH_RefugeeCamp");
            WorldObjectDef worldDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("RHAH_RefugeeCamp");
            WorldObject_RHAH_RefugeeCamp site = SiteMaker.MakeSite(
                part,
                slate.Get<PlanetTile>("tile"),
                slate.Get<Faction>("residents"),
                false,
                0f,
                worldDef) as WorldObject_RHAH_RefugeeCamp;
            if (site == null)
            {
                return;
            }

            slate.Set("site", site);
            slate.Set("sponsorName", sponsor.Name);
            RHAH_Settings settings = RHAH_Mod.Settings;
            slate.Set("campMinAdults", GameComponent_RHAH_FamineYear.ScalePopulation(settings == null ? RHAH_RefugeeCampRules.MinAdults : settings.campMinAdults));
            slate.Set("campMaxAdults", GameComponent_RHAH_FamineYear.ScalePopulation(settings == null ? RHAH_RefugeeCampRules.MaxAdults : settings.campMaxAdults));
            slate.Set("campMinChildren", GameComponent_RHAH_FamineYear.ScalePopulation(settings == null ? RHAH_RefugeeCampRules.MinChildren : settings.campMinChildren));
            slate.Set("campMaxChildren", GameComponent_RHAH_FamineYear.ScalePopulation(settings == null ? RHAH_RefugeeCampRules.MaxChildren : settings.campMaxChildren));
            quest.GetFirstOrAddPart<QuestPart_InvolvedFactions>().factions.Add(sponsor);
            quest.SpawnWorldObject(site);
            string cleared = QuestGenUtility.HardcodedSignalWithQuestID("site.ResidentsCleared");
            int goodwill = Core.RHAH_Mod.Settings == null ? RHAH_RefugeeCampRules.Goodwill : Core.RHAH_Mod.Settings.campGoodwill;
            slate.Set("campGoodwill", goodwill);
            quest.End(QuestEndOutcome.Success, goodwill, sponsor, cleared, QuestPart.SignalListenMode.OngoingOnly, true, false);
            quest.End(QuestEndOutcome.Fail, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("site.Destroyed"), QuestPart.SignalListenMode.OngoingOnly, true, false);
            int days = Core.RHAH_Mod.Settings == null ? RHAH_RefugeeCampRules.Days : Core.RHAH_Mod.Settings.campDays;
            slate.Set("campDays", days);
            int ticks = days * GenDate.TicksPerDay;
            quest.WorldObjectTimeout(site, ticks);
            quest.Delay(ticks, () => quest.End(QuestEndOutcome.Fail, 0, null, null, QuestPart.SignalListenMode.OngoingOnly, true, false));
        }
    }

    public class GenStep_RHAH_RefugeeCamp : GenStep
    {
        public override int SeedPart => 183741903;

        public override void Generate(Map map, GenStepParams parms)
        {
            WorldObject_RHAH_RefugeeCamp site = map.Parent as WorldObject_RHAH_RefugeeCamp;
            if (site == null)
            {
                return;
            }

            IntVec3 center = map.Center;
            BuildHuts(map, site.Faction, center);
            List<Verse.Pawn> spawned = SpawnResidents(site.Faction, map, center);
            for (int i = 0; i < spawned.Count; i++)
            {
                site.AddResident(spawned[i]);
            }

            RHAH_CampPredation.Roll(map, spawned);
        }

        internal static bool TrySpawnOnMap(Map map)
        {
            Faction faction = RHAH_AttitudeFactions.Require(RHAH_Attitude.Neutral);
            if (map == null || faction == null || !RCellFinder.TryFindRandomPawnEntryCell(
                out IntVec3 cell, map, CellFinder.EdgeRoadChance_Animal, false, null))
            {
                return false;
            }

            List<Verse.Pawn> spawned = SpawnResidents(faction, map, cell);
            if (spawned.Count == 0)
            {
                return false;
            }

            EventMgr.RHAH_EventChainEntry.NoteStarted("I-051", map.uniqueID, 0,
                Find.TickManager.TicksGame, RHAH_Api.Get(spawned[0]).SpawnBatchId);
            return true;
        }

        static List<Verse.Pawn> SpawnResidents(Faction faction, Map map, IntVec3 center)
        {
            RHAH_CampPlan plan = RHAH_RefugeeCampRules.Plan(Rand.RangeInclusive(2, 4), Rand.RangeInclusive(8, 16));
            List<Verse.Pawn> spawned = new List<Verse.Pawn>();
            int tick = RHAH_Runtime.NextBatchId(map);
            for (int i = 0; i < plan.Adults + plan.Children; i++)
            {
                Verse.Pawn pawn = SpawnResident(faction, map, center, i < plan.Adults, tick);
                if (pawn != null)
                {
                    spawned.Add(pawn);
                }
            }

            if (spawned.Count > 0)
            {
                LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(center), map, spawned);
            }

            return spawned;
        }

        static void BuildHuts(Map map, Faction faction, IntVec3 center)
        {
            int huts = Core.RHAH_Mod.Settings == null ? RHAH_RefugeeCampRules.Huts : Core.RHAH_Mod.Settings.campHuts;
            for (int hut = 0; hut < huts; hut++)
            {
                CellRect rect = new CellRect(center.x - 11 + (hut % 2) * 13, center.z - 11 + (hut / 2) * 13, 9, 9);
                foreach (IntVec3 cell in rect)
                {
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.WoodPlankFloor);
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                    if (cell.x != rect.minX && cell.x != rect.maxX && cell.z != rect.minZ && cell.z != rect.maxZ)
                    {
                        continue;
                    }

                    bool door = cell.x == rect.CenterCell.x && cell.z == rect.minZ;
                    Thing wall = ThingMaker.MakeThing(door ? ThingDefOf.Door : ThingDefOf.Wall, ThingDefOf.WoodLog);
                    wall.SetFaction(faction);
                    GenSpawn.Spawn(wall, cell, map);
                }
            }
        }

        static Verse.Pawn SpawnResident(Faction faction, Map map, IntVec3 center, bool adult, int tick)
        {
            RHAH_Settings settings = Core.RHAH_Mod.Settings;
            float min = settings == null ? 0f : settings.minGeneratedAge;
            float max = settings == null ? 50f : settings.maxGeneratedAge;
            bool youngFollows = settings != null && settings.youngAgeFollowsRange;
            RHAH_PawnRole role = adult ? RHAH_PawnRole.Refugee : RHAH_PawnRole.RatkinYoung;
            float? age = adult
                ? Rand.Range(18f, 50f)
                : HungerAndHavoc.Pawn.RHAH_VisitorRules.GenerationAge(role, null, min, max, Rand.Value, youngFollows);

            RHAH_PawnCreationResult result = RHAH_PawnFactory.Create(new RHAH_PawnRequest
            {
                SourceIncidentDisplayId = "I-051",
                SpawnBatchId = RHAH_Runtime.NextBatchId(map),
                RelationshipGroupId = tick,
                Role = role,
                AttitudeAtArrival = RHAH_Attitude.Neutral,
                Map = map,
                PawnKind = Core.RHAH_DefOf.RHAH_PawnKind_Ratkin,
                Faction = faction,
                SpawnCell = CellFinder.RandomClosewalkCellNear(center, map, 8),
                BiologicalAge = age
            });
            if (!result.Succeeded)
            {
                return null;
            }

            Verse.Pawn pawn = result.Pawns[0];
            Equip(pawn, adult);
            return pawn;
        }

        static void Equip(Verse.Pawn pawn, bool adult)
        {
            if (!adult || pawn.equipment == null || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return;
            }

            pawn.equipment.DestroyAllEquipment();
            List<ThingDef> weapons = DefDatabase<ThingDef>.AllDefsListForReading;
            List<ThingDef> allowed = new List<ThingDef>();
            for (int i = 0; i < weapons.Count; i++)
            {
                ThingDef weapon = weapons[i];
                bool core = weapon.modContentPack != null && weapon.modContentPack.IsCoreMod;
                bool wooden = !weapon.MadeFromStuff || weapon.stuffCategories.Contains(StuffCategoryDefOf.Woody);
                if (RHAH_RefugeeCampRules.AllowedWeapon(weapon.defName, core, weapon.IsWeapon, weapon.IsMeleeWeapon, (int)weapon.techLevel, weapon.defName == "Bow_Short", wooden))
                {
                    allowed.Add(weapon);
                }
            }

            if (allowed.Count == 0)
            {
                return;
            }

            ThingDef chosen = allowed[Rand.Range(0, allowed.Count)];
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(chosen, chosen.MadeFromStuff ? ThingDefOf.WoodLog : null));
        }
    }

    internal static class RHAH_RefugeeCampQuest
    {
        internal static bool TryOffer(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            RHAH_Settings settings = RHAH_Mod.Settings;
            bool violent = Find.Storyteller?.difficulty == null || Find.Storyteller.difficulty.allowViolentQuests;
            Faction sponsor = Sponsor();
            PlanetTile tile = PlanetTile.Invalid;
            bool tileValid = map != null && TryTile(map, out tile);
            if (!RHAH_RefugeeCampRules.CanOffer(violent, settings == null || settings.refugeeCampEnabled, sponsor != null, tileValid))
            {
                return false;
            }

            Faction residents = RHAH_AttitudeFactions.Require(RHAH_Attitude.Neutral);
            if (residents == null)
            {
                return false;
            }

            Slate slate = new Slate();
            slate.Set("sponsor", sponsor);
            slate.Set("residents", residents);
            slate.Set("tile", tile);
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("RHAH_RefugeeMassacre");
            if (script == null)
            {
                return false;
            }

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(script, slate);
            if (quest == null)
            {
                return false;
            }

            QuestUtility.SendLetterQuestAvailable(quest);
            int started = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            EventMgr.RHAH_EventChainEntry.NoteStarted("I-051", map.uniqueID, 0, started, started);
            return true;
        }

        static bool TryTile(Map map, out PlanetTile tile)
        {
            tile = PlanetTile.Invalid;
            int[] distances = { 8, 7, 6, 5, 4 };
            for (int i = 0; i < distances.Length; i++)
            {
                if (TileFinder.TryFindNewSiteTile(out tile, map.Tile, distances[i], distances[i], false, null, 0f, true, TileFinderMode.Near, false, false, map.Tile.Layer, null))
                {
                    return tile.Valid;
                }
            }

            return false;
        }

        static Faction Sponsor()
        {
            if (Find.FactionManager == null)
            {
                return null;
            }

            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            Faction ally = null;
            Faction neutral = null;
            for (int i = 0; i < factions.Count; i++)
            {
                Faction faction = factions[i];
                if (faction == null || faction.IsPlayer || faction.Hidden || faction.defeated || faction.temporary || !faction.def.humanlikeFaction || faction.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }

                if (!HasSettlement(faction))
                {
                    continue;
                }

                if (faction.PlayerRelationKind == FactionRelationKind.Ally)
                {
                    ally = faction;
                }
                else if (neutral == null)
                {
                    neutral = faction;
                }
            }

            return ally ?? neutral;
        }

        static bool HasSettlement(Faction faction)
        {
            List<Settlement> settlements = Find.WorldObjects.Settlements;
            for (int i = 0; i < settlements.Count; i++)
            {
                if (settlements[i] != null && settlements[i].Faction == faction)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
