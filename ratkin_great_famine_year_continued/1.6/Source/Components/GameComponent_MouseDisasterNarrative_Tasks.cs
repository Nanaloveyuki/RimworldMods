using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public partial class GameComponent_MouseDisasterNarrative
    {
        private Pawn envoy;
        private int envoyMapId = -1;
        private int envoyPhase; // 0 not started, 1 waiting, 2 checking, 3 verified, 4 ended
        private int envoyDeadline;
        private int envoyCheckTick;
        private bool envoyTraded;
        private bool envoyVerified;
        private int nextTaskAttempt;
        private Site storySite;
        private Thing storyCache;
        private bool relicStarted;
        private bool relicVisited;
        private int relicDeadline;
        private int relicOutcome; // 1 take, 2 leave, 3 hand over, 4 share, 5 destroy, 6 missed
        private Pawn recoveredVisitor;
        private int returnMapId = -1;
        private int returnPhase; // 0 unscheduled, 1 scheduled, 2 visiting, 3 ended
        private int returnDueTick;
        private int returnDeadline;
        private int identityAnsweredTier;

        private void ExposeStoryTaskData()
        {
            Scribe_References.Look(ref envoy, "storyEnvoy");
            Scribe_Values.Look(ref envoyMapId, "envoyMapId", -1);
            Scribe_Values.Look(ref envoyPhase, "envoyPhase");
            Scribe_Values.Look(ref envoyDeadline, "envoyDeadline");
            Scribe_Values.Look(ref envoyCheckTick, "envoyCheckTick");
            Scribe_Values.Look(ref envoyTraded, "envoyTraded");
            Scribe_Values.Look(ref envoyVerified, "envoyVerified");
            Scribe_Values.Look(ref nextTaskAttempt, "nextTaskAttempt");
            Scribe_References.Look(ref storySite, "storySite");
            Scribe_References.Look(ref storyCache, "storyCache");
            Scribe_Values.Look(ref relicStarted, "relicStarted");
            Scribe_Values.Look(ref relicVisited, "relicVisited");
            Scribe_Values.Look(ref relicDeadline, "relicDeadline");
            Scribe_Values.Look(ref relicOutcome, "relicOutcome");
            Scribe_References.Look(ref recoveredVisitor, "recoveredVisitor");
            Scribe_Values.Look(ref returnMapId, "returnMapId", -1);
            Scribe_Values.Look(ref returnPhase, "returnPhase");
            Scribe_Values.Look(ref returnDueTick, "returnDueTick");
            Scribe_Values.Look(ref returnDeadline, "returnDeadline");
            Scribe_Values.Look(ref identityAnsweredTier, "identityAnsweredTier");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && returnPhase == 1 && recoveredVisitor != null)
                Find.WorldPawns.ForcefullyKeptPawns.Add(recoveredVisitor);
        }

        private void ProcessStoryTasks()
        {
            int now = CurrentNarrativeTick;
            Map home = Find.AnyPlayerHomeMap;
            if (MouseDisasterRuntime.AllowsNewContent && home != null && now >= nextTaskAttempt)
            {
                nextTaskAttempt = now + GenDate.TicksPerDay;
                if (envoyPhase == 0 && NarrativeEnabled("N008") && (observedIncidentDefNames.Count >= (MouseDisasterMod.Settings?.narrativeEnvoyGoal ?? 5) || narrativeFlags.Contains("N005Care") || narrativeFlags.Contains("N007Recovered")))
                    StartEnvoy(home);
                if (!relicStarted && NarrativeEnabled("N009") && observedIncidentDefNames.Count >= (MouseDisasterMod.Settings?.narrativeRelicGoal ?? 8)) TryStartRelic(home);
            }
            Map envoyMap = Find.Maps.FirstOrDefault(m => m.uniqueID == envoyMapId);
            if (envoyPhase > 0 && envoyPhase < 4)
            {
                if (envoy == null || envoy.Destroyed || envoyMap == null) FinishEnvoy("N008Missing");
                else if (envoy.Dead) FinishEnvoy("N008Dead");
                else if (envoy.MapHeld != envoyMap || envoy.Faction == Faction.OfPlayer || envoy.IsPrisoner || envoy.IsSlave) FinishEnvoy("N008Left");
                else if (now >= envoyDeadline) FinishEnvoy("N008Timeout");
                else if (envoyPhase == 2 && now >= envoyCheckTick)
                {
                    if (narrativeVisitSummaries.Count > 0 || narrativeFlags.Any(f => f.StartsWith("N00")))
                    {
                        envoyPhase = 3;
                        envoyVerified = true;
                        SendJournalOnce("N008Verified", envoyMap);
                    }
                    else FinishEnvoy("N008Unverified");
                }
                if (envoyPhase == 1 || envoyPhase == 3) EnsureStoryLetter("Envoy", envoyMap);
            }
            if (relicStarted && relicOutcome == 0)
            {
                if (now >= relicDeadline) FinishRelic(6, null);
                else if (relicVisited && (storySite == null || storySite.Destroyed || !storySite.HasMap)) FinishRelic(2, null);
                else if (storySite == null || storySite.Destroyed) FinishRelic(6, null);
                else if (relicVisited && (storyCache == null || storyCache.Destroyed)) FinishRelic(5, null);
            }
            if (relicOutcome != 0 && storySite != null && !storySite.Destroyed && !storySite.HasMap) storySite.Destroy();
            ProcessRecoveredReturn(home);
            if (CanUseStoryLetter("Identity")) EnsureStoryLetter("Identity", home);
            foreach (var letter in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_MouseDisasterStory>().ToList())
                if (!CanUseStoryLetter(letter.stage)) Find.LetterStack.RemoveLetter(letter);
        }

        public bool IsWaitingEnvoy(Pawn pawn) => pawn != null && pawn == envoy && envoyPhase > 0 && envoyPhase < 4;

        private void StartEnvoy(Map map)
        {
            if (!MouseDisasterUtility.TryFindFormerFaction(out Faction faction) || MouseDisasterUtility.IsHostileTo(faction, Faction.OfPlayer) ||
                !MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell)) return;
            Pawn pawn;
            using (MouseDisasterPawnHistoryCatalog.PushContext(null, map))
            {
                pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult,
                    faction, DevelopmentalStage.Adult, 0.7f);
            }
            if (pawn == null) return;
            GenSpawn.Spawn(pawn, cell, map);
            envoy = pawn;
            envoyMapId = map.uniqueID;
            envoyPhase = 1;
            envoyDeadline = CurrentNarrativeTick + GenDate.TicksPerDay * 3;
            HoldN007Pawns(new[] { pawn });
            var behavior = GameComponent_MouseDisasterEventBehavior.Component;
            if (behavior != null) behavior.Register(behavior.CreateGroup("N008"), new[] { pawn });
            TrackNarrativeVisit("S14", map, new[] { pawn });
            EnsureStoryLetter("Envoy", map);
        }

        public bool CanUseStoryLetter(string stage)
        {
            if (stage == "Envoy") return (envoyPhase == 1 || envoyPhase == 3) && envoy != null && !envoy.Dead &&
                envoy.Spawned && envoy.Map.uniqueID == envoyMapId && CurrentNarrativeTick < envoyDeadline;
            if (stage == "Return") return returnPhase == 2 && recoveredVisitor != null && !recoveredVisitor.Dead &&
                recoveredVisitor.Spawned && recoveredVisitor.Map.uniqueID == returnMapId && CurrentNarrativeTick < returnDeadline;
            if (stage == "Identity") return MouseDisasterRuntime.AllowsNewContent && IsNarratorActive() && narratorTrust >= 50 &&
                (NarrativeEnabled("R01") || Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_MouseDisasterStory>().Any(l => l.stage == "Identity")) &&
                (relicOutcome >= 1 && relicOutcome <= 4 || narrativeFlags.Contains("E01") || narrativeFlags.Contains("E02")) &&
                identityAnsweredTier < (narratorTrust >= 75 ? 2 : 1);
            return false;
        }

        private void EnsureStoryLetter(string stage, Map map)
        {
            if (Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_MouseDisasterStory>().Any(l => l.stage == stage)) return;
            string key = stage == "Envoy" ? "N008Entry" : stage == "Return" ? "N007Return" : "IdentityEntry";
            var letter = (ChoiceLetter_MouseDisasterStory)LetterMaker.MakeLetter(
                ("MouseDisaster_Story_" + key + "_Label").Translate(),
                ("MouseDisaster_Story_" + key + "_Text").Translate(stage == "Envoy" ? envoy.LabelShortCap.ToString() : ""),
                MouseDisasterDefOf.MouseDisaster_StoryLetter, map == null ? LookTargets.Invalid : new LookTargets(new TargetInfo(map.Center, map)));
            letter.stage = stage;
            Find.LetterStack.ReceiveLetter(letter);
        }

        public bool ResolveStoryChoice(string stage, string choice, out string failure)
        {
            failure = "MouseDisaster_Story_Stale".Translate();
            if (!CanUseStoryLetter(stage)) return false;
            if (stage == "Identity")
            {
                identityAnsweredTier = narratorTrust >= 75 ? 2 : 1;
                if (choice == "Ask") SendJournalOnce(identityAnsweredTier == 2 ? "IdentityFull" : "IdentityPartial", Find.AnyPlayerHomeMap);
                return true;
            }
            if (stage == "Return")
            {
                if (choice == "Receive" && !MouseDisasterPhase2Utility.TryConsumeRequest(recoveredVisitor.Map,
                    MouseDisasterRequestKind.SimpleMeal, 2, out failure)) return false;
                if (choice == "Receive")
                {
                    NotifyNarrativeDelivery(new[] { recoveredVisitor });
                    SendJournalOnce("N007ReturnReceived", recoveredVisitor.Map);
                }
                EndRecoveredReturn();
                return true;
            }
            Map map = envoy.Map;
            if (choice == "Verify")
            {
                if (envoyPhase != 1) return false;
                envoyPhase = 2;
                envoyCheckTick = CurrentNarrativeTick + GenDate.TicksPerDay;
                return true;
            }
            if (choice == "Trade")
            {
                if (!MouseDisasterRuntime.AllowsNewContent) return false;
                // Check resources before creating a new site; the consume operation revalidates the same stacks.
                if (map.listerThings.ThingsOfDef(ThingDefOf.MealSimple).Where(t => !t.IsForbidden(Faction.OfPlayer)).Sum(t => t.stackCount) < 6)
                { failure = "MouseDisaster_Story_NoMeals".Translate(); return false; }
                Site extraSite = null;
                if (!relicStarted && NarrativeEnabled("N009"))
                {
                    if (!TryStartRelic(map)) return false;
                }
                else if ((relicOutcome != 0 || !relicStarted) && !MouseDisasterPhase2Utility.TryCreateIntelSite(map, MouseDisasterIntelSiteKind.Treasure, out extraSite, out failure)) return false;
                if (!MouseDisasterPhase2Utility.TryConsumeRequest(map, MouseDisasterRequestKind.SimpleMeal, 6, out failure))
                {
                    extraSite?.Destroy();
                    return false;
                }
                envoyTraded = true;
                NotifyNarrativeDelivery(new[] { envoy });
                if (extraSite != null) Find.LetterStack.ReceiveLetter("MouseDisaster_Story_N009Entry_Label".Translate(),
                    "MouseDisaster_Story_N008ExtraSite".Translate(), LetterDefOf.NeutralEvent, extraSite);
                FinishEnvoy("N008Traded");
                return true;
            }
            bool reacted = false;
            if (choice == "Drive")
            {
                NotifyNarrativeForce(new[] { envoy });
                reacted = GameComponent_MouseDisasterEventBehavior.Component?.React(new[] { envoy }, true, out _) == true;
            }
            FinishEnvoy("N008Rejected", release: !reacted);
            return true;
        }

        private void FinishEnvoy(string result, bool release = true)
        {
            if (envoyPhase >= 4) return;
            envoyPhase = 4;
            CompleteNarrativeFlag("N008");
            Map map = envoy?.Map;
            if (release && envoy != null && !envoy.Dead && envoy.Spawned && envoy.Faction != Faction.OfPlayer && !envoy.IsPrisoner && !envoy.IsSlave)
                StartN007Release(map, new[] { envoy });
            SendJournalOnce(result, map);
        }

        private bool TryStartRelic(Map map)
        {
            if (relicStarted || !NarrativeEnabled("N009") ||
                !TileFinder.TryFindNewSiteTile(out PlanetTile tile, 5, 22)) return false;
            Site site = SiteMaker.MakeSite(MouseDisasterDefOf.MouseDisaster_RecordSite, tile, null, ifHostileThenMustRemainHostile: false);
            if (site == null) return false;
            Find.WorldObjects.Add(site);
            storySite = site;
            relicStarted = true;
            relicDeadline = CurrentNarrativeTick + GenDate.TicksPerDay * 15;
            Find.LetterStack.ReceiveLetter("MouseDisaster_Story_N009Entry_Label".Translate(),
                "MouseDisaster_Story_N009Entry_Text".Translate(), LetterDefOf.NeutralEvent, site);
            return true;
        }

        public void NotifyRecordSiteGenerated(Map map)
        {
            if (map?.Parent != storySite || relicOutcome != 0 || relicVisited) return;
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 15);
            if (!cell.Standable(map)) return;
            storyCache = GenSpawn.Spawn(MouseDisasterDefOf.MouseDisaster_RecordCache, cell, map);
            relicVisited = true;
            if (HasEnvoyContact && !envoy.Spawned && !envoy.IsCaravanMember() && Find.WorldPawns.Contains(envoy) &&
                envoy.Faction != Faction.OfPlayer && !envoy.HostileTo(Faction.OfPlayer))
            {
                Find.WorldPawns.RemovePawn(envoy);
                GenSpawn.Spawn(envoy, CellFinder.RandomClosewalkCellNear(cell, map, 5), map);
                HoldN007Pawns(new[] { envoy });
            }
        }

        public bool CanResolveRelic(Thing cache)
        {
            return relicOutcome == 0 && cache != null && cache == storyCache && cache.Spawned &&
                cache.Map.Parent == storySite && CurrentNarrativeTick < relicDeadline &&
                cache.Map.mapPawns.FreeColonistsSpawned.Any(p => p.CanReach(cache, PathEndMode.Touch, Danger.Some));
        }

        public bool HasEnvoyContact => (envoyVerified || envoyTraded) && envoy != null && !envoy.Dead && !envoy.Destroyed;
        public bool HasRelicWitness => HasEnvoyContact && envoy.Spawned && envoy.Map == storySite?.Map;
        public int NarrativeReward(int basis) => MouseDisasterNarrativePolicy.Reward(basis, narratorTrust, IsNarratorActive());

        public void ResolveRelic(Thing cache, int choice)
        {
            if (!CanResolveRelic(cache) || choice < 1 || choice > 5 || ((choice == 3 || choice == 4) && !HasRelicWitness))
            {
                Messages.Message("MouseDisaster_Story_Stale".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            FinishRelic(choice, cache.Map);
        }

        private void FinishRelic(int choice, Map map)
        {
            if (relicOutcome != 0) return;
            relicOutcome = choice;
            CompleteNarrativeFlag("N009");
            if (map != null)
            {
                var rewards = new List<Thing>();
                if (choice == 1 || choice == 4) rewards.Add(ThingMaker.MakeThing(MouseDisasterDefOf.MouseDisaster_RecordPages));
                if (choice == 1 || choice == 3)
                {
                    Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = NarrativeReward(choice == 1 ? 200 : 100);
                    rewards.Add(silver);
                }
                foreach (var thing in rewards) GenPlace.TryPlaceThing(thing, storyCache.Position, map, ThingPlaceMode.Near);
                if ((choice == 3 || choice == 4) && HasRelicWitness)
                {
                    Thing copy = ThingMaker.MakeThing(MouseDisasterDefOf.MouseDisaster_RecordPages);
                    if (envoy.inventory?.innerContainer.TryAdd(copy) != true)
                        GenPlace.TryPlaceThing(copy, envoy.Position, map, ThingPlaceMode.Near);
                }
                ChangeNarratorTrust(choice == 5 ? -2 : choice == 4 ? 2 : 0);
            }
            if (HasRelicWitness) StartN007Release(envoy.Map, new[] { envoy });
            if (storyCache != null && !storyCache.Destroyed) storyCache.Destroy();
            SendJournalOnce("N009Result" + choice, map);
        }

        private void ScheduleRecoveredReturn(MouseDisasterN007Record record)
        {
            if (returnPhase != 0 || !NarrativeEnabled("N007Return")) return;
            Pawn pawn = record.pawnRecords.Where(p => p.leftMap && p.recovered && !p.died).Select(p => p.pawn)
                .FirstOrDefault(p => p != null && !p.Dead && !p.Destroyed && Find.WorldPawns.Contains(p));
            if (pawn == null) return;
            if (!narrativeDebugForce && !Rand.Chance(MouseDisasterNarrativePolicy.ScaledChance(1f, MouseDisasterMod.Settings?.narrativeReturnChancePercent ?? 100f))) return;
            recoveredVisitor = pawn;
            returnMapId = record.mapId;
            returnPhase = 1;
            returnDueTick = CurrentNarrativeTick + GenDate.TicksPerDay * (MouseDisasterMod.Settings?.narrativeReturnDelayDays ?? 15);
            Find.WorldPawns.ForcefullyKeptPawns.Add(pawn);
        }

        private void ProcessRecoveredReturn(Map home)
        {
            if (returnPhase != 1 && returnPhase != 2) return;
            if (recoveredVisitor == null || recoveredVisitor.Dead || recoveredVisitor.Destroyed ||
                (returnPhase == 1 && !NarrativeEnabled("N007Return")) || HasN007Plague(recoveredVisitor)) { EndRecoveredReturn(); return; }
            if (returnPhase == 1 && CurrentNarrativeTick >= returnDueTick)
            {
                Map map = Find.Maps.FirstOrDefault(m => m.uniqueID == returnMapId) ?? home;
                if (map == null || recoveredVisitor.Spawned || recoveredVisitor.IsCaravanMember() ||
                    recoveredVisitor.Faction == Faction.OfPlayer || recoveredVisitor.HostileTo(Faction.OfPlayer) ||
                    !Find.WorldPawns.Contains(recoveredVisitor)) { EndRecoveredReturn(); return; }
                if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell))
                {
                    if (CurrentNarrativeTick > returnDueTick + GenDate.TicksPerDay) EndRecoveredReturn();
                    return;
                }
                Find.WorldPawns.RemovePawn(recoveredVisitor);
                GenSpawn.Spawn(recoveredVisitor, cell, map);
                returnMapId = map.uniqueID;
                returnPhase = 2;
                returnDeadline = CurrentNarrativeTick + GenDate.TicksPerDay;
                HoldN007Pawns(new[] { recoveredVisitor });
                TrackNarrativeVisit("S14", map, new[] { recoveredVisitor });
            }
            if (returnPhase == 2)
            {
                if (CurrentNarrativeTick >= returnDeadline || recoveredVisitor.MapHeld?.uniqueID != returnMapId) EndRecoveredReturn();
                else EnsureStoryLetter("Return", recoveredVisitor.MapHeld);
            }
        }

        private void EndRecoveredReturn()
        {
            if (returnPhase == 2 && recoveredVisitor != null && !recoveredVisitor.Dead && recoveredVisitor.Spawned)
                StartN007Release(recoveredVisitor.Map, new[] { recoveredVisitor });
            if (recoveredVisitor != null) Find.WorldPawns.ForcefullyKeptPawns.Remove(recoveredVisitor);
            recoveredVisitor = null;
            returnMapId = -1;
            returnDueTick = 0;
            returnDeadline = 0;
            returnPhase = 3;
        }

        [DebugAction("MouseDisaster", "Narrative: start envoy", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStartNarrativeEnvoy()
        {
            RunNarrativeDebug("N008");
        }

        [DebugAction("MouseDisaster", "Narrative: start record site", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStartNarrativeSite()
        {
            RunNarrativeDebug("N009");
        }

        [DebugAction("MouseDisaster", "Narrative: inspect state", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugInspectNarrative()
        {
            var n = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
            if (n == null) return;
            Log.Message($"[MouseDisaster Narrative] trust={n.narratorTrust}; aid={n.aidCompleted}; broadcasts={n.successfulBroadcasts}; " +
                $"driven={n.forceDepartures}; annualAdults={n.adultRatkinCount}; visits={n.narrativeVisits.Count + n.narrativeVisitSummaries.Count}; " +
                $"pendingVisits={n.narrativeVisits.Count(v => !v.resolved)}; envoy={n.envoyPhase}; relic={n.relicOutcome}; return={n.returnPhase}; " +
                $"identity={n.identityAnsweredTier}; flags={string.Join(",", n.narrativeFlags)}");
        }
    }
}
