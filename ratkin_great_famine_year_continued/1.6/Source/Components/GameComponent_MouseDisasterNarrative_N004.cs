using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public enum MouseDisasterN004Decision
    {
        Pending,
        Accepted,
        Rejected,
        Ignored
    }

    public enum MouseDisasterN004Outcome
    {
        Pending,
        ChildSurvived,
        FamilySurvived,
        ChildEnslaved,
        FamilyDied,
        FamilySeparated,
        ChildDied,
        FamilyChanged,
        Unknown
    }

    public enum MouseDisasterN004RevisitDecision
    {
        Pending,
        Ignored,
        Rescued,
        Killed
    }

    public sealed class MouseDisasterN004Record : IExposable
    {
        public Pawn mother;
        public List<Pawn> children = new List<Pawn>();
        public int mapId = -1;
        public IntVec3 foodCell = IntVec3.Invalid;
        public int createdTick;
        public MouseDisasterN004Decision decision;
        public MouseDisasterN004Outcome outcome;
        public int predatorHuntDeadlineTick = -1;
        public bool predatorHuntStarted;
        public bool outcomeEffectsApplied;
        public int missingMotherBreakWindowUntilTick = -1;
        public bool missingMotherBreakTraitApplied;
        public int revisitDeadlineTick = -1;
        public bool revisitTriggered;
        public MouseDisasterN004RevisitDecision revisitDecision;
        public int rescueRewardDueTick = -1;
        public bool rescueRewardGranted;

        public void ExposeData()
        {
            Scribe_References.Look(ref mother, "mother");
            Scribe_Collections.Look(ref children, "children", LookMode.Reference);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref foodCell, "foodCell", IntVec3.Invalid);
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
            Scribe_Values.Look(ref decision, "decision", MouseDisasterN004Decision.Pending);
            Scribe_Values.Look(ref outcome, "outcome", MouseDisasterN004Outcome.Pending);
            Scribe_Values.Look(ref predatorHuntDeadlineTick, "predatorHuntDeadlineTick", -1);
            Scribe_Values.Look(ref predatorHuntStarted, "predatorHuntStarted", false);
            Scribe_Values.Look(ref outcomeEffectsApplied, "outcomeEffectsApplied", false);
            Scribe_Values.Look(ref missingMotherBreakWindowUntilTick, "missingMotherBreakWindowUntilTick", -1);
            Scribe_Values.Look(ref missingMotherBreakTraitApplied, "missingMotherBreakTraitApplied", false);
            Scribe_Values.Look(ref revisitDeadlineTick, "revisitDeadlineTick", -1);
            Scribe_Values.Look(ref revisitTriggered, "revisitTriggered", false);
            Scribe_Values.Look(ref revisitDecision, "revisitDecision", MouseDisasterN004RevisitDecision.Pending);
            Scribe_Values.Look(ref rescueRewardDueTick, "rescueRewardDueTick", -1);
            Scribe_Values.Look(ref rescueRewardGranted, "rescueRewardGranted", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                children ??= new List<Pawn>();
                children.RemoveAll(child => child == null);
            }
        }
    }

    public partial class GameComponent_MouseDisasterNarrative
    {
        private const int N004CheckIntervalTicks = 2500;
        private const int N004PredatorHuntDurationTicks = GenDate.TicksPerDay * 2;
        private const int N004RevisitDurationTicks = GenDate.TicksPerYear * 4;

        private int narratorTrust;
        private List<MouseDisasterN004Record> n004Records = new List<MouseDisasterN004Record>();

        public int NarratorTrust => narratorTrust;

        public MouseDisasterN004Record RegisterN004(Pawn mother, IEnumerable<Pawn> children, Map map, IntVec3 foodCell)
        {
            List<Pawn> childList = children?.Where(child => child != null && !child.Dead).Distinct().ToList() ?? new List<Pawn>();
            if (mother == null || childList.Count == 0)
            {
                return null;
            }

            MouseDisasterN004Record existing = n004Records.FirstOrDefault(record => record?.mother == mother && record.outcome == MouseDisasterN004Outcome.Pending);
            if (existing != null)
            {
                return existing;
            }

            if (!NarrativeEnabled("N004")) return null;

            MouseDisasterN004Record record = new MouseDisasterN004Record
            {
                mother = mother,
                children = childList,
                mapId = map?.uniqueID ?? mother.Map?.uniqueID ?? -1,
                foodCell = foodCell,
                createdTick = CurrentNarrativeTick,
                decision = MouseDisasterN004Decision.Pending,
                outcome = MouseDisasterN004Outcome.Pending
            };
            n004Records.Add(record);
            return record;
        }

        public bool RecordN004Decision(Pawn mother, MouseDisasterN004Decision decision)
        {
            MouseDisasterN004Record record = FindN004Record(mother);
            if (record == null || record.outcome != MouseDisasterN004Outcome.Pending || record.decision != MouseDisasterN004Decision.Pending)
            {
                return false;
            }

            record.decision = decision;
            switch (decision)
            {
                case MouseDisasterN004Decision.Accepted:
                    ChangeNarratorTrust(5);
                    break;
                case MouseDisasterN004Decision.Rejected:
                    record.predatorHuntDeadlineTick = CurrentNarrativeTick + N004PredatorHuntDurationTicks;
                    ChangeNarratorTrust(-5);
                    break;
                case MouseDisasterN004Decision.Ignored:
                    ChangeNarratorTrust(-1);
                    break;
            }

            return true;
        }

        public override void GameComponentTick()
        {
            TryRunDailyMouseDisasterPawnCleanup();
            if (Find.TickManager != null && Find.TickManager.TicksGame % MouseDisasterNarrativePolicy.CheckTicks == 0)
                ProcessNarrativeJournal();
            if (Find.TickManager == null || Find.TickManager.TicksGame % N004CheckIntervalTicks != 0)
            {
                return;
            }

            ProcessN004RescueRewards();
            ProcessN006();
            ProcessN007();
            if (n004Records == null || n004Records.Count == 0)
            {
                return;
            }

            for (int i = 0; i < n004Records.Count; i++)
            {
                MouseDisasterN004Record record = n004Records[i];
                if (record == null || record.outcome != MouseDisasterN004Outcome.Pending)
                {
                    continue;
                }

                ProcessN004PredatorHunt(record);
                TryResolveN004(record);
            }
        }

        private int CurrentNarrativeTick => Find.TickManager?.TicksGame ?? 0;

        private MouseDisasterN004Record FindN004Record(Pawn mother)
        {
            return mother == null ? null : n004Records.FirstOrDefault(record => record?.mother == mother && record.outcome == MouseDisasterN004Outcome.Pending);
        }

        private void ProcessN004PredatorHunt(MouseDisasterN004Record record)
        {
            if (record.decision != MouseDisasterN004Decision.Rejected || record.predatorHuntStarted || record.children == null)
            {
                return;
            }

            Map map = ResolveN004Map(record);
            Pawn prey = record.children.FirstOrDefault(child => IsPresentOnMap(child) && child.Spawned && child.Map == map &&
                !MouseDisasterUtility.IsPlayerAffiliatedRatkin(child));
            if (map != null && prey != null)
            {
                List<Pawn> predators = map.mapPawns.AllPawnsSpawned
                    .Where(pawn => pawn != null && !pawn.Dead && !pawn.Downed && pawn.IsAnimal && pawn.Faction == null && pawn.RaceProps.predator && pawn.meleeVerbs?.TryGetMeleeVerb(null) != null && pawn.CurJobDef != JobDefOf.PredatorHunt &&
                        pawn.CanReach(prey, PathEndMode.Touch, Danger.Deadly))
                    .ToList();
                if (predators.TryRandomElement(out Pawn predator))
                {
                    Job huntJob = JobMaker.MakeJob(JobDefOf.PredatorHunt, prey);
                    predator.jobs.StartJob(huntJob, JobCondition.InterruptForced);
                    if (predator.CurJob == huntJob && predator.CurJobDef == JobDefOf.PredatorHunt)
                    {
                        record.predatorHuntStarted = true;
                        return;
                    }
                }
            }

            if (CurrentNarrativeTick >= record.predatorHuntDeadlineTick && map != null)
            {
                List<Pawn> remainingChildren = record.children.Where(child => IsPresentOnMap(child) && child.Spawned && child.Map == map &&
                    !MouseDisasterUtility.IsPlayerAffiliatedRatkin(child)).ToList();
                if (remainingChildren.Count > 0)
                {
                    MouseDisasterUtility.MakeTravelAndExitLord(map, remainingChildren, map.Center);
                }

                record.predatorHuntStarted = true;
            }
        }

        private void TryResolveN004(MouseDisasterN004Record record)
        {
            List<Pawn> children = record.children?.Where(child => child != null).ToList() ?? new List<Pawn>();
            if (children.Count == 0)
            {
                if (CurrentNarrativeTick - record.createdTick >= MouseDisasterNarrativePolicy.MissingGraceTicks)
                    ResolveN004(record, MouseDisasterN004Outcome.Unknown);
                return;
            }

            // A lost reference is not evidence of death.
            if (record.mother == null || record.mother.Destroyed && !record.mother.Dead ||
                children.Any(child => child.Destroyed && !child.Dead))
            {
                if (CurrentNarrativeTick - record.createdTick >= MouseDisasterNarrativePolicy.MissingGraceTicks)
                    ResolveN004(record, MouseDisasterN004Outcome.Unknown);
                return;
            }
            bool motherDead = record.mother.Dead;
            List<Pawn> aliveChildren = children.Where(child => !child.Dead).ToList();
            bool allChildrenDead = aliveChildren.Count == 0;

            if (motherDead)
            {
                if (allChildrenDead)
                {
                    ResolveN004(record, MouseDisasterN004Outcome.FamilyDied);
                    return;
                }

                List<Pawn> childAgeChildren = aliveChildren.Where(IsChildAge).ToList();
                if (aliveChildren.All(child => !IsN004Present(child)))
                {
                    ResolveN004(record, MouseDisasterN004Outcome.FamilyChanged);
                    return;
                }
                if (aliveChildren.Any(child => !IsN004Present(child)) ||
                    aliveChildren.Any(child => child.IsPrisonerOfColony || child.IsSlaveOfColony) &&
                    aliveChildren.Any(IsPlayerCare))
                {
                    ResolveN004(record, MouseDisasterN004Outcome.FamilyChanged);
                    return;
                }
                if (aliveChildren.All(child => child.IsPrisonerOfColony || child.IsSlaveOfColony))
                {
                    ResolveN004(record, MouseDisasterN004Outcome.ChildEnslaved);
                    return;
                }
                if (childAgeChildren.Count != aliveChildren.Count)
                {
                    TryExpireN004Observation(record, aliveChildren);
                    return;
                }

                if (childAgeChildren.Any(child => child.IsPrisonerOfColony || child.IsSlaveOfColony))
                {
                    ResolveN004(record, MouseDisasterN004Outcome.ChildEnslaved);
                }
                else if (childAgeChildren.All(IsPlayerCare))
                {
                    ResolveN004(record, MouseDisasterN004Outcome.ChildSurvived);
                }

                TryExpireN004Observation(record, aliveChildren);
                return;
            }

            if (allChildrenDead)
            {
                ResolveN004(record, MouseDisasterN004Outcome.ChildDied);
                return;
            }

            bool motherPresent = IsN004Present(record.mother);
            bool childrenPresent = aliveChildren.Any(IsN004Present);
            if (!motherPresent)
            {
                if (!childrenPresent)
                {
                    ResolveN004(record, MouseDisasterN004Outcome.FamilySeparated);
                }
                else
                {
                    ResolveN004(record, MouseDisasterN004Outcome.FamilyChanged);
                }

                return;
            }

            if (aliveChildren.Any(child => !IsN004Present(child)))
            {
                ResolveN004(record, MouseDisasterN004Outcome.FamilyChanged);
                return;
            }
            if (aliveChildren.All(child => IsChildAge(child) && IsN004Together(record.mother, child)))
            {
                ResolveN004(record, MouseDisasterN004Outcome.FamilySurvived);
            }
            TryExpireN004Observation(record, aliveChildren);
        }

        private void TryExpireN004Observation(MouseDisasterN004Record record, List<Pawn> aliveChildren)
        {
            bool growingInCare = aliveChildren.Any(child => !IsChildAge(child) && IsN004InPlayerDomain(child));
            if (!growingInCare && CurrentNarrativeTick - record.createdTick >= MouseDisasterNarrativePolicy.ObservationTicks)
                ResolveN004(record, MouseDisasterN004Outcome.Unknown);
        }

        private void ResolveN004(MouseDisasterN004Record record, MouseDisasterN004Outcome outcome)
        {
            if (record.outcome != MouseDisasterN004Outcome.Pending)
            {
                return;
            }

            record.outcome = outcome;
            CompleteNarrativeFlag("N004");
            ApplyN004OutcomeEffects(record);
            ChangeNarratorTrust(TrustDeltaForOutcome(outcome));
            if (!IsNarratorActive())
            {
                return;
            }

            if (outcome == MouseDisasterN004Outcome.FamilyDied)
            {
                Messages.Message(
                    "MouseDisaster_N004_4_Message".Translate(),
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
                return;
            }

            string suffix = ((int)outcome).ToString();
            ReceiveNarrativeLetter(
                "MouseDisaster_N004_" + suffix + "_Label",
                "MouseDisaster_N004_" + suffix + "_Text",
                ResolveN004Map(record));
        }

        private void ChangeNarratorTrust(int amount)
        {
            if (!CountsNarrativeState) return;
            narratorTrust = Mathf.Clamp(narratorTrust + amount, -100, 100);
        }

        private static int TrustDeltaForOutcome(MouseDisasterN004Outcome outcome)
        {
            switch (outcome)
            {
                case MouseDisasterN004Outcome.ChildSurvived:
                case MouseDisasterN004Outcome.FamilySurvived:
                    return 5;
                case MouseDisasterN004Outcome.ChildEnslaved:
                    return -5;
                case MouseDisasterN004Outcome.FamilyDied:
                    return -2;
                case MouseDisasterN004Outcome.ChildDied:
                    return -3;
                case MouseDisasterN004Outcome.FamilySeparated:
                    return -1;
                default:
                    return 0;
            }
        }

        private Map ResolveN004Map(MouseDisasterN004Record record)
        {
            Map map = record.mother?.Map;
            if (map != null)
            {
                return map;
            }

            map = record.children?.FirstOrDefault(child => child?.Map != null)?.Map;
            if (map != null)
            {
                return map;
            }

            return Find.Maps?.FirstOrDefault(candidate => candidate.uniqueID == record.mapId);
        }

        private static bool IsPresentOnMap(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && (pawn.Spawned || pawn.MapHeld != null);
        }

        private static bool IsChildAge(Pawn pawn)
        {
            return pawn?.ageTracker != null && pawn.ageTracker.AgeBiologicalYearsFloat >= 3f;
        }

        private static bool IsPlayerCare(Pawn pawn)
        {
            return pawn != null && pawn.Faction == Faction.OfPlayer && !pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony;
        }
    }
}
