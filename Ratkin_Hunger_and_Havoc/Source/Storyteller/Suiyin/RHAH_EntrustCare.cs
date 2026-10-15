using System.Linq;
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

            if (!RHAH_EndingRuntime.CountsNow())
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
            if (!RHAH_NarrativePace.Due(tick, CheckInterval, RHAH_NarrativePace.Spread * 2) || Current.Game == null)
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
                    SuiyinPresence mother = ReadPresence(FindPawn(record.MotherId), tick, ref record.MissingSince);
                    record.Mother = mother;
                    if (record.Children != null)
                    {
                        for (int childIndex = 0; childIndex < record.Children.Count; childIndex++)
                        {
                            ReadMember(record.Children[childIndex], tick, false);
                        }
                    }

                    state.Commit(item => item.ResolveEntrust(record, tick, mother));
                }

                if (record.Outcome == SuiyinN004Outcome.ChildAlone)
                {
                    ApplyAlone(record, tick);
                }
                else if (record.Outcome == SuiyinN004Outcome.Captive && !record.EffectsApplied)
                {
                    ApplyCaptive(record);
                    record.EffectsApplied = true;
                }
                else if (record.Outcome == SuiyinN004Outcome.FamilyHere && !record.EffectsApplied)
                {
                    ApplyFamily(record);
                    record.EffectsApplied = true;
                }
                else if (record.Outcome == SuiyinN004Outcome.Regret && !record.EffectsApplied)
                {
                    ApplyRegret(record);
                    record.EffectsApplied = true;
                }

                int reward = book.ClaimRescue(record, tick);
                if (reward > 0)
                {
                    DropSilver(reward);
                }
            }
        }

        static void ApplyAlone(SuiyinN004Case record, int tick)
        {
            if (record.BreakUntil < 0 || tick < record.BreakUntil || record.Children == null)
            {
                return;
            }

            for (int i = 0; i < record.Children.Count; i++)
            {
                Verse.Pawn pawn = MemberPawn(record.Children[i]);
                if (pawn?.needs?.mood?.thoughts?.memories != null)
                {
                    TryMemory(pawn, DefDatabase<ThoughtDef>.GetNamedSilentFail("RHAH_Thought_LeftAlone"));
                }

                if (!record.BreakTrait && RHAH_Revisit.BrokeToday(pawn, tick, record.BreakUntil))
                {
                    record.BreakTrait = RHAH_Revisit.TryGloom(pawn);
                }
            }

            record.BreakUntil = -1;
            record.EffectsApplied = true;
        }

        static void ApplyCaptive(SuiyinN004Case record)
        {
            if (record.Children == null)
            {
                return;
            }

            for (int i = 0; i < record.Children.Count; i++)
            {
                Verse.Pawn pawn = MemberPawn(record.Children[i]);
                if (pawn?.needs?.mood?.thoughts?.memories != null)
                {
                    TryMemory(pawn, DefDatabase<ThoughtDef>.GetNamedSilentFail("RHAH_Thought_CaptiveYear"));
                }
            }
        }

        static void ApplyFamily(SuiyinN004Case record)
        {
            Verse.Pawn mother = FindPawn(record.MotherId);
            if (mother?.needs?.mood?.thoughts?.memories == null || record.Children == null)
            {
                return;
            }

            for (int i = 0; i < record.Children.Count; i++)
            {
                Verse.Pawn child = MemberPawn(record.Children[i]);
                if (child != null && !child.DevelopmentalStage.Adult())
                {
                    TryMemory(mother, DefDatabase<ThoughtDef>.GetNamedSilentFail("RHAH_Thought_FamilyHere"));
                    return;
                }
            }
        }

        static void ApplyRegret(SuiyinN004Case record)
        {
            Verse.Pawn mother = FindPawn(record.MotherId);
            if (mother?.needs?.mood?.thoughts?.memories != null)
            {
                TryMemory(mother, DefDatabase<ThoughtDef>.GetNamedSilentFail("RHAH_Thought_Regret"));
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
                    ReadMember(record.Children[childIndex], tick, true);
                }

                state.Commit(item => item.AdvanceCare(record, tick));
            }
        }


        static void ReadMember(SuiyinMember member, int tick, bool care)
        {
            if (member == null)
            {
                return;
            }

            Verse.Pawn pawn = FindPawn(member.LoadId);
            member.Presence = ReadPresence(pawn, tick, ref member.MissingSince);
            if (pawn != null && (pawn.DevelopmentalStage == DevelopmentalStage.Child || pawn.DevelopmentalStage.Adult()))
            {
                member.Child = false;
            }

            if (care)
            {
                member.ObserveSettlement(tick, SuiyinMember.HasMalnutrition(pawn));
            }
        }

        static SuiyinPresence ReadPresence(Verse.Pawn pawn, int tick, ref int missingSince)
        {
            if (pawn == null || pawn.Destroyed)
            {
                if (missingSince < 0)
                {
                    missingSince = tick;
                }

                return SuiyinPresence.Unknown;
            }

            if (pawn.Dead)
            {
                missingSince = -1;
                return SuiyinPresence.Dead;
            }

            if (OffMap(pawn))
            {
                missingSince = -1;
                return SuiyinPresence.Left;
            }

            if (!pawn.Spawned || pawn.MapHeld == null)
            {
                if (missingSince < 0)
                {
                    missingSince = tick;
                }

                return SuiyinPresence.Unknown;
            }

            missingSince = -1;
            return SuiyinPresence.Here;
        }

        static bool OffMap(Verse.Pawn pawn)
        {
            return (Find.WorldPawns != null && Find.WorldPawns.Contains(pawn)) || InPlayerCaravan(pawn);
        }

        static bool InPlayerCaravan(Verse.Pawn pawn)
        {
            if (pawn == null || Find.WorldObjects == null)
            {
                return false;
            }

            List<RimWorld.Planet.Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                RimWorld.Planet.Caravan caravan = caravans[i];
                if (caravan != null && caravan.IsPlayerControlled && caravan.PawnsListForReading.Contains(pawn))
                {
                    return true;
                }
            }

            return false;
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

        static Verse.Pawn MemberPawn(SuiyinMember member)
        {
            if (member == null || member.Presence == SuiyinPresence.Dead)
            {
                return null;
            }

            Verse.Pawn pawn = FindPawn(member.LoadId);
            return pawn == null || pawn.Dead || pawn.Destroyed ? null : pawn;
        }

        static Map MapById(int mapId)
        {
            if (mapId <= 0 || Find.Maps == null)
            {
                return null;
            }

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (map != null && map.uniqueID == mapId)
                {
                    return map;
                }
            }

            return null;
        }
        static Verse.Pawn FindPawn(int loadId)
        {
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            return RHAH_PawnIndex.Find(loadId, tick);
        }

        static void TryMemory(Verse.Pawn pawn, ThoughtDef def)
        {
            if (def == null || pawn.needs.mood.thoughts.memories.Memories.Any(memory => memory.def == def))
            {
                return;
            }

            pawn.needs.mood.thoughts.memories.TryGainMemory(def);
        }


        static void DropSilver(int amount)
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null || amount <= 0)
            {
                return;
            }

            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = amount;
            DropPodUtility.DropThingsNear(
                DropCellFinder.TradeDropSpot(map),
                map,
                new List<Thing> { silver },
                110,
                false,
                false,
                true,
                false);
        }
    }
}
