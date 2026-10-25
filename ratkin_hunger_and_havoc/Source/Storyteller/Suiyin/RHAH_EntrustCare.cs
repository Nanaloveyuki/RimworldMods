using System.Collections.Generic;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using HungerAndHavoc.Narrative;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_EntrustCare
    {
        internal const int CheckInterval = 2500;

        internal static void OnChoice(RHAH_ChoiceRecord record)
        {
            if (record == null || record.Settled == RHAH_ChoiceAction.None || Current.Game == null)
            {
                return;
            }

            if (!RHAH_EndingRuntime.CountsNow() || Current.Game.GetComponent<NarrativeState>()?.CareTrustEnabled != true)
            {
                return;
            }

            NarrativeState state = Current.Game.GetComponent<NarrativeState>();
            SuiyinBook book = state?.Book;
            if (book == null)
            {
                return;
            }

            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            if (record.DisplayId == "I-037")
            {
                OpenEntrust(state, record, tick);
                return;
            }

            if (record.DisplayId == "I-013" && record.Settled == RHAH_ChoiceAction.Deliver)
            {
                state.Commit(item => item.AcceptExchange(record.Id, record.MapId, tick, ChildIds(record)));
            }
        }

        internal static void Tick(int tick)
        {
            if (!RHAH_NarrativePace.Due(tick, CheckInterval, RHAH_NarrativePace.Spread * 2) || Current.Game?.GetComponent<NarrativeState>()?.CareTrustEnabled != true)
            {
                return;
            }

            NarrativeState state = Current.Game.GetComponent<NarrativeState>();
            SuiyinBook book = state?.Book;
            if (book == null)
            {
                return;
            }

            TickEntrust(state, book, tick);
            TickCare(state, book, tick);
        }

        static void OpenEntrust(NarrativeState state, RHAH_ChoiceRecord record, int tick)
        {
            if (record.Settled != RHAH_ChoiceAction.Join &&
                record.Settled != RHAH_ChoiceAction.Ignore &&
                record.Settled != RHAH_ChoiceAction.Reject &&
                record.Settled != RHAH_ChoiceAction.Timeout)
            {
                return;
            }

            List<int> ids = record.PawnLoadIds;
            if (ids == null)
            {
                return;
            }

            int motherId = 0;
            List<int> children = new List<int>();
            for (int i = 0; i < ids.Count; i++)
            {
                Verse.Pawn pawn = FindPawn(ids[i]);
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    continue;
                }

                if (pawn.DevelopmentalStage.Adult())
                {
                    if (motherId == 0)
                    {
                        motherId = pawn.thingIDNumber;
                    }
                }
                else if (pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child)
                {
                    children.Add(pawn.thingIDNumber);
                }
            }

            if (motherId > 0 && children.Count > 0)
            {
                state.Commit(item => item.OpenEntrust(motherId, record.MapId, tick, children));
            }
        }

        static void TickEntrust(NarrativeState state, SuiyinBook book, int tick)
        {
            List<SuiyinN004Case> cases = book.N004;
            if (cases == null)
            {
                return;
            }

            for (int i = 0; i < cases.Count; i++)
            {
                SuiyinN004Case record = cases[i];
                if (record == null)
                {
                    continue;
                }

                if (record.Outcome == SuiyinN004Outcome.Pending)
                {
                    SuiyinPresence mother = SuiyinMember.ReadPresence(FindPawn(record.MotherId), record.MapId, tick,
                        record.Mother, ref record.MissingSince);
                    record.Mother = mother;
                    if (record.Children != null)
                    {
                        for (int childIndex = 0; childIndex < record.Children.Count; childIndex++)
                        {
                            ReadMember(record.Children[childIndex], record.MapId, tick, false);
                        }
                    }

                    state.Commit(item => item.ResolveEntrust(record, tick, mother));
                }


            }
        }


        static void TickCare(NarrativeState state, SuiyinBook book, int tick)
        {
            List<SuiyinN005Case> cases = book.N005;
            if (cases == null)
            {
                return;
            }

            for (int i = 0; i < cases.Count; i++)
            {
                SuiyinN005Case record = cases[i];
                if (record == null || record.Outcome != SuiyinN005Outcome.Accepted || record.CareClosed || record.Children == null)
                {
                    continue;
                }

                for (int childIndex = 0; childIndex < record.Children.Count; childIndex++)
                {
                    ReadMember(record.Children[childIndex], record.MapId, tick, true);
                }

                state.Commit(item => item.AdvanceCare(record, tick));
            }
        }


        static void ReadMember(SuiyinMember member, int mapId, int tick, bool care)
        {
            if (member == null)
            {
                return;
            }

            Verse.Pawn pawn = FindPawn(member.LoadId);
            member.ObservePresence(pawn, mapId, tick);
            if (pawn != null && (pawn.DevelopmentalStage == DevelopmentalStage.Child || pawn.DevelopmentalStage.Adult()))
            {
                member.Child = false;
            }

            if (care)
            {
                member.ObserveSettlement(tick, SuiyinMember.HasMalnutrition(pawn));
            }
        }


        static List<int> ChildIds(RHAH_ChoiceRecord record)
        {
            List<int> children = new List<int>();
            if (record.PawnLoadIds == null)
            {
                return children;
            }

            for (int i = 0; i < record.PawnLoadIds.Count; i++)
            {
                Verse.Pawn pawn = FindPawn(record.PawnLoadIds[i]);
                if (pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.DevelopmentalStage.Adult())
                {
                    children.Add(pawn.thingIDNumber);
                }
            }

            return children;
        }


        static Verse.Pawn FindPawn(int loadId)
        {
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            return RHAH_PawnIndex.Find(loadId, tick);
        }

    }
}
