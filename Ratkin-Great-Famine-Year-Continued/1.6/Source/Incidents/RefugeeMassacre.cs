using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    public class IncidentWorker_MouseDisasterRefugeeMassacre : IncidentWorker
    {
        internal static List<Faction> Sponsors()
        {
            var eligible = Find.FactionManager.AllFactionsListForReading.Where(f =>
                !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def.humanlikeFaction &&
                !f.HostileTo(Faction.OfPlayer) && Find.WorldObjects.Settlements.Any(s => s.Faction == f)).ToList();
            var allies = eligible.Where(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally).ToList();
            return allies.Count > 0 ? allies : eligible;
        }

        protected override bool CanFireNowSub(IncidentParms parms) => base.CanFireNowSub(parms) &&
            MouseDisasterRuntime.AllowsNewContent && parms.target is Map map && map.IsPlayerHome &&
            Find.Storyteller.difficulty.allowViolentQuests && Sponsors().Count > 0;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            // Shuffle rings instead of preferring the closest valid ring every time.
            var distances = new List<int> { 4, 5, 6, 7, 8 };
            distances.Shuffle();
            PlanetTile tile = PlanetTile.Invalid;
            foreach (int distance in distances)
                if (TileFinder.TryFindNewSiteTile(out tile, map.Tile, distance, distance,
                    selectLandmarkChance: 0f, layer: map.Tile.Layer)) break;
            if (!tile.Valid || !MouseDisasterUtility.TryGetMouseDisasterHiddenFaction(out Faction residents)) return false;
            var slate = new Slate();
            slate.Set("sponsor", Sponsors().RandomElement());
            slate.Set("residents", residents);
            slate.Set("tile", tile);
            slate.Set("points", parms.points);
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(
                DefDatabase<QuestScriptDef>.GetNamed("MouseDisaster_RefugeeMassacre"), slate);
            QuestUtility.SendLetterQuestAvailable(quest);
            return true;
        }
    }

    public class QuestNode_MouseDisasterRefugeeMassacre : QuestNode
    {
        protected override bool TestRunInt(Slate slate) => slate.Get<Faction>("sponsor") != null &&
            slate.Get<PlanetTile>("tile").Valid;

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Faction sponsor = slate.Get<Faction>("sponsor");
            var site = (MouseDisasterRefugeeSite)SiteMaker.MakeSite(
                DefDatabase<SitePartDef>.GetNamed("MouseDisaster_RefugeeCamp"),
                slate.Get<PlanetTile>("tile"), slate.Get<Faction>("residents"), false, 0f,
                DefDatabase<WorldObjectDef>.GetNamed("MouseDisaster_RefugeeCamp"));
            slate.Set("site", site);
            slate.Set("sponsorName", sponsor.Name);
            quest.SpawnWorldObject(site);
            string cleared = QuestGenUtility.HardcodedSignalWithQuestID("site.ResidentsCleared");
            quest.End(QuestEndOutcome.Success, 12, sponsor, cleared, sendStandardLetter: true);
            quest.End(QuestEndOutcome.Fail, inSignal: QuestGenUtility.HardcodedSignalWithQuestID("site.Destroyed"), sendStandardLetter: true);
            quest.WorldObjectTimeout(site, 15 * GenDate.TicksPerDay);
            quest.Delay(15 * GenDate.TicksPerDay, () => quest.End(QuestEndOutcome.Fail, sendStandardLetter: true));
        }
    }

    public class MouseDisasterRefugeeSite : Site
    {
        public List<Pawn> residents = new List<Pawn>();
        private bool cleared;
        private int nextCheck;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref residents, "refugeeResidents", LookMode.Reference);
            Scribe_Values.Look(ref cleared, "refugeeCleared");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) residents ??= new List<Pawn>();
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (cleared || !HasMap || residents.Count == 0 || Find.TickManager.TicksGame < nextCheck) return;
            nextCheck = Find.TickManager.TicksGame + 250;
            // Downing, kidnapping or leaving the map alive is not a massacre.
            if (residents.Any(p => p != null && !p.Dead)) return;
            cleared = true;
            QuestUtility.SendQuestTargetSignals(questTags, "ResidentsCleared");
        }
    }

    public class GenStep_MouseDisasterRefugeeCamp : GenStep
    {
        public override int SeedPart => 183741903;

        public override void Generate(Map map, GenStepParams parms)
        {
            var site = map.Parent as MouseDisasterRefugeeSite;
            if (site == null) return;
            IntVec3 center = map.Center;
            // A dedicated natural-terrain generator never invokes Settlement/BaseGen.
            for (int hut = 0; hut < 4; hut++)
            {
                var rect = new CellRect(center.x - 11 + (hut % 2) * 13, center.z - 11 + (hut / 2) * 13, 9, 9);
                foreach (IntVec3 cell in rect)
                {
                    foreach (Thing thing in cell.GetThingList(map).ToList())
                        if (thing.def.category == ThingCategory.Building || thing is Plant || thing.def.category == ThingCategory.Item)
                            thing.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.WoodPlankFloor);
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                    if (cell.x != rect.minX && cell.x != rect.maxX && cell.z != rect.minZ && cell.z != rect.maxZ) continue;
                    bool door = cell.x == rect.CenterCell.x && cell.z == rect.minZ;
                    Thing wall = ThingMaker.MakeThing(door ? ThingDefOf.Door : ThingDefOf.Wall, ThingDefOf.WoodLog);
                    wall.SetFaction(site.Faction);
                    GenSpawn.Spawn(wall, cell, map);
                }
                for (int bed = 0; bed < 5; bed++)
                {
                    Thing sleeping = ThingMaker.MakeThing(ThingDefOf.SleepingSpot);
                    sleeping.SetFaction(site.Faction);
                    GenSpawn.Spawn(sleeping, new IntVec3(rect.minX + 2 + bed, 0, rect.minZ + 3), map);
                }
            }
            int adults = Rand.RangeInclusive(2, 4);
            int children = Rand.RangeInclusive(8, 16);
            children = System.Math.Min(children, MouseDisasterUtility.LimitEventPawnCount(adults + children, 10) - adults);
            for (int i = 0; i < adults + children; i++)
            {
                bool adult = i < adults;
                float age = adult ? Rand.Range(18f, 50f) : Rand.Range(1f / GenDate.DaysPerYear, 8f);
                var stage = adult ? DevelopmentalStage.Adult : age < 3f ? DevelopmentalStage.Baby : DevelopmentalStage.Child;
                Pawn pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(adult ?
                    MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult : MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild,
                    site.Faction, stage, 0.8f, true, fixedAgeYears: age);
                if (pawn == null) throw new InvalidOperationException("MouseDisaster: refugee camp pawn generation failed.");
                foreach (Trait trait in pawn.story.traits.allTraits.ToList()) pawn.story.traits.RemoveTrait(trait);
                if (age >= 3f) MouseDisasterUtility.MakeRatEggPureNegative(pawn, adult ? 3 : 2);
                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(center, map, 8), map);
                EquipResident(pawn, adult);
                site.residents.Add(pawn);
            }
            LordMaker.MakeNewLord(site.Faction, new LordJob_DefendPoint(center), map, site.residents.Where(p => !p.Downed).ToList());
        }

        internal static void EquipResident(Pawn pawn, bool adult)
        {
            pawn.equipment?.DestroyAllEquipment();
            pawn.apparel?.DestroyAll();
            pawn.inventory?.innerContainer.ClearAndDestroyContents();
            ThingDef tribal = DefDatabase<ThingDef>.GetNamed("Apparel_TribalA");
            if (pawn.apparel != null && ApparelUtility.HasPartsToWear(pawn, tribal))
                pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(tribal, ThingDefOf.Cloth));
            if (!adult || pawn.equipment == null || pawn.WorkTagIsDisabled(WorkTags.Violent)) return;
            var weapons = DefDatabase<ThingDef>.AllDefsListForReading.Where(AllowedWeapon).ToList();
            if (weapons.Count == 0) return;
            ThingDef weapon = weapons.RandomElement();
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(weapon, weapon.MadeFromStuff ? ThingDefOf.WoodLog : null));
        }

        internal static bool AllowedWeapon(ThingDef def) => def.IsWeapon &&
            ((def.IsMeleeWeapon && def.techLevel >= TechLevel.Neolithic && def.techLevel <= TechLevel.Medieval) ||
             def.defName == "Bow_Short") && (!def.MadeFromStuff ||
             def.stuffCategories.Any(category => ThingDefOf.WoodLog.stuffProps.categories.Contains(category)));
    }
}
