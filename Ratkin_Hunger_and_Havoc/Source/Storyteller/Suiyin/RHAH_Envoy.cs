using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using HungerAndHavoc.Narrative;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_Envoy
    {
        internal const int DayTicks = 60000;
        internal const int CheckInterval = GenDate.TicksPerHour * 2;
        internal const int MealCost = 6;
        internal const float AdultAge = 18f;
        internal const string LetterDefName = "RHAH_EnvoyLetter";

        internal static void Tick(int tick)
        {
            if (tick < 0 || Current.Game == null)
            {
                return;
            }

            NarrativeState state = Current.Game.GetComponent<NarrativeState>();
            SuiyinBook book = state?.Book;
            if (book == null)
            {
                return;
            }

            if (RHAH_NarrativePace.Due(tick, CheckInterval, RHAH_NarrativePace.Spread * 4))
            {
                Watch(state, book, tick);
            }

            if (tick % DayTicks != 0)
            {
                return;
            }

            TryArrive(book, tick);
        }

        static void TryArrive(SuiyinBook book, int tick)
        {
            if (book.N008 == null || book.N008.Count > 0)
            {
                return;
            }

            if (book.Gates != null && !book.Gates.Enabled(SuiyinNode.N008) && !book.Gates.Started(SuiyinNode.N008))
            {
                return;
            }

            if (book.Distinct < book.Config.EnvoyKinds && !book.EnvoyClue)
            {
                return;
            }

            Map map = Find.AnyPlayerHomeMap;
            if (map == null)
            {
                return;
            }

            IntVec3 cell;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal, false, null))
            {
                return;
            }

            int batch = tick <= 0 ? 1 : tick;
            RHAH_PawnCreationResult result = RHAH_PawnFactory.Create(new RHAH_PawnRequest
            {
                SourceIncidentDisplayId = "N-008",
                SpawnBatchId = batch,
                RelationshipGroupId = batch,
                Role = RHAH_PawnRole.Envoy,
                AttitudeAtArrival = RHAH_Attitude.Neutral,
                Map = map,
                PawnKind = RHAH_DefOf.RHAH_PawnKind_Ratkin,
                Faction = RHAH_AttitudeFactions.Require(RHAH_Attitude.Neutral),
                SpawnCell = cell,
                BiologicalAge = Core.RHAH_Mod.Settings == null ? AdultAge : Core.RHAH_Mod.Settings.narrativeAdultYears
            });
            if (result == null || !result.Succeeded || result.Pawns.Count == 0 || result.Pawns[0] == null)
            {
                return;
            }

            Verse.Pawn pawn = result.Pawns[0];
            if (!book.ArriveEnvoy(map.uniqueID, pawn.thingIDNumber, tick))
            {
                if (!pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }

                return;
            }

            Hold(pawn);
            OpenLetter(map, pawn);
        }

        static void Watch(NarrativeState state, SuiyinBook book, int tick)
        {
            if (book.N008 == null)
            {
                return;
            }

            for (int i = 0; i < book.N008.Count; i++)
            {
                SuiyinN008Case record = book.N008[i];
                if (record == null)
                {
                    continue;
                }

                Refresh(state, record, tick);
                Verse.Pawn pawn = FindPawn(record.PawnId);
                if (OpenCase(record.Outcome))
                {
                    Hold(pawn);
                    if (record.Presence == SuiyinPresence.Here && !LetterOpen(record))
                    {
                        OpenLetter(pawn.Map, pawn);
                    }
                }
                else
                {
                    Complete(record);
                }
            }
        }

        internal static bool Refresh(NarrativeState state, SuiyinN008Case record, int tick)
        {
            if (state == null || record == null || !OpenCase(record.Outcome))
            {
                return false;
            }

            MigrateHold(record, FindPawn(record.PawnId));
            record.Presence = PresenceOf(FindPawn(record.PawnId), record.MapId);
            if (record.Presence != SuiyinPresence.Unknown)
            {
                record.MissingSince = -1;
            }
            Core.RHAH_Mod.Settings?.CopyNarrative(state.Book.Config);


            state.Commit(item => item.FinishEnvoy(record, tick));
            if (!OpenCase(record.Outcome))
            {
                Complete(record);
                return false;
            }

            return record.Presence == SuiyinPresence.Here;
        }

        internal static void Complete(SuiyinN008Case record)
        {
            Verse.Pawn pawn = FindPawn(record.PawnId);
            MigrateHold(record, pawn);
            if (RHAH_NarrativePace.HoldFor(record.Outcome) == RHAH_EnvoyHold.Leave)
            {
                Release(pawn);
            }
            else
            {
                RHAH_NarrativePace.ReturnVisitorGates(pawn, RHAH_NarrativePace.EnvoyHoldKey);
            }

            RemoveLetters(record);
        }

        static bool LetterOpen(SuiyinN008Case record)
        {
            List<Letter> letters = Find.LetterStack?.LettersListForReading;
            if (letters == null)
            {
                return false;
            }

            for (int i = 0; i < letters.Count; i++)
            {
                ChoiceLetter_RHAH_Envoy letter = letters[i] as ChoiceLetter_RHAH_Envoy;
                if (letter != null && letter.mapId == record.MapId && letter.pawnId == record.PawnId)
                {
                    return true;
                }
            }

            return false;
        }

        static void RemoveLetters(SuiyinN008Case record)
        {
            List<Letter> letters = Find.LetterStack?.LettersListForReading;
            if (letters == null)
            {
                return;
            }

            for (int i = letters.Count - 1; i >= 0; i--)
            {
                ChoiceLetter_RHAH_Envoy letter = letters[i] as ChoiceLetter_RHAH_Envoy;
                if (letter != null && letter.mapId == record.MapId && letter.pawnId == record.PawnId)
                {
                    Find.LetterStack.RemoveLetter(letter);
                }
            }
        }


        static void MigrateHold(SuiyinN008Case record, Verse.Pawn pawn)
        {
            if (record.HoldMigrated || pawn == null)
            {
                return;
            }

            record.HoldMigrated = true;
            HungerAndHavoc.Identity.CompRHAH_Pawn comp = HungerAndHavoc.Identity.CompRHAH_Pawn.TryGet(pawn);
            if (comp != null && comp.State.sourceIncidentDisplayId == "N-008" &&
                !comp.State.TryGetExtra(RHAH_NarrativePace.EnvoyHoldKey, out string previous) &&
                comp.State.GetGateOverride(RHAH_BehaviorGate.LeaveAfterFed) == false &&
                comp.State.GetGateOverride(RHAH_BehaviorGate.ExitMap) == false)
            {
                RHAH_Api.SetExtra(pawn, RHAH_NarrativePace.EnvoyHoldKey, "uu");
            }
        }

        internal static void Hold(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || !RHAH_Api.IsVisitor(pawn) || pawn.Faction == Faction.OfPlayer)
            {
                return;
            }


            bool held = RHAH_Api.TryGetExtra(pawn, RHAH_NarrativePace.EnvoyHoldKey, out string previous);
            RHAH_NarrativePace.HoldVisitor(pawn, RHAH_NarrativePace.EnvoyHoldKey);
            Lord lord = pawn.GetLord();
            if (lord?.CurLordToil is LordToil_RHAH_VisitorLeave && lord.Graph != null && lord.Graph.lordToils != null && lord.Graph.lordToils.Count > 1)
            {
                lord.GotoToil(lord.Graph.lordToils[1]);
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (!held && snapshot != null && snapshot.Lifecycle == RHAH_Lifecycle.Leaving)
            {
                pawn.jobs?.StopAll();
                if (pawn.mindState != null)
                {
                    pawn.mindState.duty = new PawnDuty(RHAH_DefOf.RHAH_VisitorSeek);
                }
            }
        }

        internal static void Release(Verse.Pawn pawn)
        {
            bool held = RHAH_Api.TryGetExtra(pawn, RHAH_NarrativePace.EnvoyHoldKey, out string previous);
            RHAH_NarrativePace.ReturnVisitorGates(pawn, RHAH_NarrativePace.EnvoyHoldKey);
            if (pawn == null || pawn.Dead || pawn.Destroyed || !RHAH_Api.IsVisitor(pawn) || pawn.Faction == Faction.OfPlayer ||
                RHAH_Api.TryGetExtra(pawn, HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.HoldKey, out string other) ||
                (!held && RHAH_Api.Get(pawn)?.Lifecycle == RHAH_Lifecycle.Leaving))
            {
                return;
            }

            RHAH_BatchAttitude.OrderVisitorLeave(new List<Verse.Pawn> { pawn });
        }

        static void OpenLetter(Map map, Verse.Pawn pawn)
        {
            if (map == null || pawn == null || Find.LetterStack == null)
            {
                return;
            }

            LetterDef def = DefDatabase<LetterDef>.GetNamedSilentFail(LetterDefName);
            if (def == null)
            {
                return;
            }

            ChoiceLetter_RHAH_Envoy letter = (ChoiceLetter_RHAH_Envoy)LetterMaker.MakeLetter(def);
            letter.mapId = map.uniqueID;
            letter.pawnId = pawn.thingIDNumber;
            letter.Label = "RHAH_Suiyin_N008Arrive_Label".Translate();
            SuiyinConfig config = Current.Game.GetComponent<NarrativeState>().Book.Config;
            letter.Text = "RHAH_Suiyin_N008Arrive_Text".Translate(pawn.LabelShort,
                Core.RHAH_Mod.Settings == null ? MealCost : Core.RHAH_Mod.Settings.envoyMealCost, config.EnvoyWaitDays);
            letter.lookTargets = new LookTargets(pawn);
            Find.LetterStack.ReceiveLetter(letter);
        }

        internal static SuiyinPresence PresenceOf(Verse.Pawn pawn, int mapId)
        {
            if (pawn == null || (pawn.Destroyed && !pawn.Dead))
            {
                return SuiyinPresence.Unknown;
            }

            if (pawn.Dead)
            {
                return SuiyinPresence.Dead;
            }

            if (!RHAH_Api.IsVisitor(pawn) || pawn.Faction == Faction.OfPlayer || pawn.IsPrisoner || pawn.IsSlaveOfColony)
            {
                return SuiyinPresence.Left;
            }

            if (!pawn.Spawned || pawn.Map == null || pawn.Map.uniqueID != mapId)
            {
                return SuiyinPresence.Left;
            }

            return SuiyinPresence.Here;
        }

        internal static bool OpenCase(SuiyinN008Outcome outcome)
        {
            return outcome == SuiyinN008Outcome.Waiting || outcome == SuiyinN008Outcome.Checking;
        }


        internal static Verse.Pawn FindPawn(int pawnId)
        {
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            return RHAH_PawnIndex.Find(pawnId, tick);
        }

        internal static bool HasProof(SuiyinBook book)
        {
            if (book == null)
            {
                return false;
            }

            if (book.Seen != null && book.Seen.Count > 0)
            {
                return true;
            }

            if (book.Journals == null)
            {
                return false;
            }

            for (int i = 0; i < book.Journals.Count; i++)
            {
                if (book.Journals[i] != null && book.Journals[i].Closed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
