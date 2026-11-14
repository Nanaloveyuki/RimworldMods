using System;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorldCaravan = RimWorld.Planet.Caravan;

namespace HungerAndHavoc.Incidents
{
    // XML 和存档按类型名恢复世界地点
    public sealed class WorldObject_RHAH_BackgroundSite : WorldObject
    {
        RHAH_BackgroundStory story = RHAH_BackgroundStory.SameCallsign;
        int undergroundId;
        int warlordId;
        bool evacuated;
        bool joinOffered;
        bool generationAttempted;
        int memorialId;
        int nameListId;
        bool settled;

        internal RHAH_BackgroundStory Story => story;
        internal bool NameList => memorialId > 0;
        internal bool WaitingForNames => nameListId > 0;
        internal bool JoinPending => evacuated && joinOffered;
        internal bool CanRecruit => !generationAttempted;
        public override string Label => ("RHAH_Background_Site" + (NameList ? "NameList" : story.ToString())).Translate();

        internal static WorldObject_RHAH_BackgroundSite Create(RHAH_BackgroundStory story, Map home, RHAH_BackgroundOptions options)
        {
            Faction underground = RHAH_BackgroundStories.FindFaction(RHAH_WarlordCompat.UndergroundFaction);
            Faction warlord = RHAH_BackgroundStories.FindFaction(RHAH_BackgroundStories.WarlordDef);
            if (home == null || underground == null || warlord == null) return null;
            WorldObject_RHAH_BackgroundSite site = Place(story, home.Tile, options.storyDistance);
            if (site == null) return null;
            site.undergroundId = underground.loadID;
            site.warlordId = warlord.loadID;
            site.joinOffered = story == RHAH_BackgroundStory.SameCallsign && Rand.Chance(options.woundedJoinChancePercent / 100f);
            return site;
        }

        static WorldObject_RHAH_BackgroundSite Place(RHAH_BackgroundStory story, PlanetTile origin, IntRange distance)
        {
            if (!origin.Valid || !TileFinder.TryFindPassableTileWithTraversalDistance(origin, distance.min, distance.max,
                out PlanetTile tile, candidate => TileFinder.IsValidTileForNewSettlement(candidate))) return null;
            WorldObject_RHAH_BackgroundSite site = (WorldObject_RHAH_BackgroundSite)WorldObjectMaker.MakeWorldObject(
                DefDatabase<WorldObjectDef>.GetNamed("RHAH_BackgroundSite"));
            site.story = story;
            site.Tile = tile;
            Find.WorldObjects.Add(site);
            return site;
        }

        internal bool CanVisit(WorldCaravan caravan)
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings?.background;
            return RHAH_Runtime.AllowsNewContent && options?.enabled == true &&
                (story == RHAH_BackgroundStory.SameCallsign ? options.sameCallsignEnabled : options.memorialEnabled) &&
                Spawned && !Destroyed && !settled && caravan?.Spawned == true && caravan.IsPlayerControlled &&
                caravan.PawnsListForReading.Count > 0;
        }

        internal bool AtSite(WorldCaravan caravan) => CanVisit(caravan) && caravan.Tile == Tile;

        internal void Visit(WorldCaravan caravan)
        {
            if (!AtSite(caravan)) return;
            if (NameList)
            {
                FindNames(caravan);
                return;
            }
            if (story == RHAH_BackgroundStory.SameCallsign && !evacuated)
            {
                evacuated = true;
                RHAH_BackgroundOptions options = RHAH_Mod.Settings.background;
                GrantGoodwill(options.evacuationGoodwill, options.evacuationGoodwill);
                if (!joinOffered)
                {
                    Finish("Evacuated", caravan);
                    return;
                }
            }
            Find.WindowStack.Add(new Dialog_RHAH_BackgroundSite(this, caravan));
        }

        internal bool ResolveJoin(WorldCaravan caravan, bool accept)
        {
            if (!AtSite(caravan) || !JoinPending) return false;
            if (!accept)
            {
                Finish("Evacuated", caravan);
                return true;
            }
            if (generationAttempted) return false;
            Faction faction = RHAH_ChoiceRuntime.FindFaction(undergroundId);
            if (!RHAH_BackgroundStories.LivingFaction(faction)) return false;
            generationAttempted = true;
            RHAH_PawnCreationResult result;
            try
            {
                int batch = RHAH_Runtime.NextBatchId(null);
                result = RHAH_PawnFactory.Create(new RHAH_PawnRequest
                {
                    SourceIncidentDisplayId = "RHAH_BackgroundSameCallsign",
                    SpawnBatchId = batch,
                    RelationshipGroupId = batch,
                    Role = RHAH_PawnRole.Refugee,
                    AttitudeAtArrival = RHAH_Attitude.Friendly,
                    Faction = faction,
                    BiologicalAge = Rand.Range(20f, 45f),
                    SpawnCell = IntVec3.Invalid
                }, false);
                if (!result.Succeeded)
                {
                    generationAttempted = false;
                    Messages.Message("RHAH_Background_JoinUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                    return false;
                }
                Verse.Pawn pawn = result.Pawns[0];
                Hediff bruise = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise"), pawn, pawn.RaceProps.body.corePart);
                bruise.Severity = 4f;
                pawn.health.AddHediff(bruise);
                pawn.SetFaction(Faction.OfPlayer);
                if (!RHAH_Api.ReleaseToColony(pawn, RHAH_ReleaseReason.JoinedPlayerFaction))
                    throw new InvalidOperationException("The evacuated ratkin could not be released to the colony.");
                caravan.AddPawn(pawn, true);
                if (!caravan.PawnsListForReading.Contains(pawn))
                    throw new InvalidOperationException("The evacuated ratkin did not enter the caravan.");
            }
            catch (Exception exception)
            {
                Log.Error("[RHAH] Background evacuation stopped after an uncertain pawn mutation: " + exception);
                Messages.Message("RHAH_Background_JoinUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }
            Finish("WoundedJoined", caravan);
            return true;
        }

        internal bool Supply(WorldCaravan caravan, bool restore, bool wood)
        {
            if (!AtSite(caravan) || story != RHAH_BackgroundStory.Memorial || NameList || WaitingForNames) return false;
            RHAH_BackgroundOptions options = RHAH_Mod.Settings.background;
            ThingDef material = wood ? ThingDefOf.WoodLog : ThingDefOf.Steel;
            int needed = restore ? (wood ? options.restorationWood : options.restorationSteel) : (wood ? options.memorialWood : options.memorialSteel);
            if (!ConsumeMaterial(caravan, material, needed)) return false;
            settled = true;
            int goodwill = restore ? options.restorationGoodwill : options.materialsGoodwill;
            GrantGoodwill(goodwill, goodwill);
            Finish(restore ? "MemorialRestored" : "MemorialSupplied", caravan);
            return true;
        }

        internal static bool ConsumeMaterial(WorldCaravan caravan, ThingDef def, int needed)
        {
            if (def == null || needed < 0) return false;
            List<Thing> candidates = new List<Thing>();
            int total = 0;
            foreach (Verse.Pawn pawn in caravan.PawnsListForReading)
            {
                if (pawn.inventory == null) continue;
                foreach (Thing thing in pawn.inventory.innerContainer)
                {
                    if (thing.def != def) continue;
                    candidates.Add(thing);
                    total += thing.stackCount;
                }
            }
            if (total < needed) return false;
            // 先拆出实物 失败时返回各自的原容器
            List<Thing> payment = new List<Thing>();
            List<ThingOwner> owners = new List<ThingOwner>();
            int remaining = needed;
            foreach (Thing source in candidates)
            {
                if (remaining == 0) break;
                ThingOwner owner = source.holdingOwner;
                Thing part = source.SplitOff(Math.Min(source.stackCount, remaining));
                if (part != null)
                {
                    payment.Add(part);
                    owners.Add(owner);
                    remaining -= part.stackCount;
                }
            }
            if (remaining > 0)
            {
                for (int i = 0; i < payment.Count; i++)
                    if (!owners[i].TryAdd(payment[i])) throw new InvalidOperationException("Memorial payment rollback failed.");
                return false;
            }
            foreach (Thing item in payment) item.Destroy();
            return true;
        }

        internal bool SearchNames(WorldCaravan caravan)
        {
            if (!AtSite(caravan) || story != RHAH_BackgroundStory.Memorial || NameList || WaitingForNames) return false;
            WorldObject_RHAH_BackgroundSite names = Place(story, Tile, new IntRange(1, 3));
            if (names == null) return false;
            names.memorialId = ID;
            nameListId = names.ID;
            Find.LetterStack.ReceiveLetter("RHAH_Background_NameListLabel".Translate(),
                "RHAH_Background_NameListText".Translate(), LetterDefOf.NeutralEvent, new LookTargets(names));
            return true;
        }

        bool FindNames(WorldCaravan caravan)
        {
            WorldObject_RHAH_BackgroundSite memorial = null;
            foreach (WorldObject item in Find.WorldObjects.AllWorldObjects)
                if (item.ID == memorialId) memorial = item as WorldObject_RHAH_BackgroundSite;
            if (memorial == null || memorial.Destroyed || memorial.settled || memorial.nameListId != ID) return false;
            settled = true;
            memorial.settled = true;
            RHAH_BackgroundOptions options = RHAH_Mod.Settings.background;
            memorial.GrantGoodwill(options.namesUndergroundGoodwill, options.namesWarlordGoodwill);
            memorial.Destroy();
            Finish("NamesFound", caravan);
            return true;
        }

        void GrantGoodwill(int underground, int warlord)
        {
            GrantFactionGoodwill(RHAH_ChoiceRuntime.FindFaction(undergroundId), underground);
            GrantFactionGoodwill(RHAH_ChoiceRuntime.FindFaction(warlordId), warlord);
        }

        static void GrantFactionGoodwill(Faction faction, int points)
        {
            if (!RHAH_BackgroundStories.LivingFaction(faction)) return;
            Faction player = Faction.OfPlayer;
            int remaining = points;
            while (remaining > 0)
            {
                int request = remaining;
                while (request > 0 && faction.CalculateAdjustedGoodwillChange(player, request) > remaining) request--;
                int before = faction.BaseGoodwillWith(player);
                if (request == 0 || !faction.TryAffectGoodwillWith(player, request, canSendMessage: false, canSendHostilityLetter: false)) break;
                int gained = faction.BaseGoodwillWith(player) - before;
                if (gained <= 0) break;
                remaining -= gained;
            }
        }

        void Finish(string outcome, WorldCaravan caravan)
        {
            settled = true;
            Destroy();
            Find.LetterStack.ReceiveLetter(Label,
                ("RHAH_Background_" + outcome).Translate(), LetterDefOf.NeutralEvent, new LookTargets(caravan));
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(WorldCaravan caravan)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(caravan)) yield return option;
            foreach (FloatMenuOption option in CaravanArrivalActionUtility.GetFloatMenuOptions(
                () => CanVisit(caravan), () => new CaravanArrivalAction_RHAH_BackgroundSite(this),
                "RHAH_Background_Visit".Translate(), caravan, Tile, this)) yield return option;
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(WorldCaravan caravan)
        {
            foreach (Gizmo gizmo in base.GetCaravanGizmos(caravan)) yield return gizmo;
            if (AtSite(caravan)) yield return new Command_Action
            {
                defaultLabel = "RHAH_Background_Visit".Translate(),
                defaultDesc = "RHAH_Background_VisitDescription".Translate(),
                action = () => Visit(caravan)
            };
        }

        public override string GetInspectString()
        {
            string key = NameList ? "NameList" : WaitingForNames ? "WaitingForNames" : JoinPending ? "JoinOffer" : story.ToString();
            return ("RHAH_Background_SiteDescription" + key).Translate();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref story, "story", RHAH_BackgroundStory.SameCallsign);
            Scribe_Values.Look(ref undergroundId, "undergroundId", 0);
            Scribe_Values.Look(ref warlordId, "warlordId", 0);
            Scribe_Values.Look(ref evacuated, "evacuated", false);
            Scribe_Values.Look(ref joinOffered, "joinOffered", false);
            Scribe_Values.Look(ref generationAttempted, "generationAttempted", false);
            Scribe_Values.Look(ref memorialId, "memorialId", 0);
            Scribe_Values.Look(ref nameListId, "nameListId", 0);
            Scribe_Values.Look(ref settled, "settled", false);
        }
    }
}
