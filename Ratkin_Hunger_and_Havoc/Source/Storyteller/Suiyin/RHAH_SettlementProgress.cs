using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HungerAndHavoc.Core;
using HungerAndHavoc.Narrative;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum RHAH_SettlementKind
    {
        Relief,
        Exchange,
        Entrust
    }

    [Flags]
    internal enum RHAH_SettlementBlocker
    {
        None = 0,
        Hungry = 1,
        Plague = 2,
        Captive = 4,
        Unknown = 8,
        Missing = 16,
        Dead = 32,
        Left = 64,
        Growing = 128,
        Malnutrition = 256
    }

    internal sealed class RHAH_SettlementMember
    {
        internal int LoadId { get; }
        internal string Name { get; }
        internal int RemainingTicks { get; }
        internal RHAH_SettlementBlocker Blockers { get; }
        internal SuiyinPresence? MotherPresence { get; }
        internal bool Ready { get; }
        internal Verse.Pawn Pawn { get; }

        internal RHAH_SettlementMember(SuiyinMember member, int remainingTicks,
            RHAH_SettlementBlocker blockers, SuiyinPresence? motherPresence, bool ready, Verse.Pawn pawn)
        {
            LoadId = member.LoadId;
            Name = pawn?.LabelShort;
            RemainingTicks = remainingTicks;
            Blockers = blockers;
            MotherPresence = motherPresence;
            Ready = ready;
            Pawn = pawn;
        }
    }

    internal sealed class RHAH_SettlementGroup
    {
        internal RHAH_SettlementKind Kind { get; }
        internal int Id { get; }
        internal int BatchId { get; }
        internal int MapId { get; }
        internal int StartedTick { get; }
        internal string SourceDisplayId { get; }
        internal int RemainingObservationTicks { get; }
        internal bool Failed { get; }
        internal bool Driven { get; }
        internal bool Delivered { get; }
        internal int MotherLoadId { get; }
        internal string MotherName { get; }
        internal IReadOnlyList<RHAH_SettlementMember> Members { get; }

        internal RHAH_SettlementGroup(RHAH_SettlementKind kind, int id, int batchId, int mapId,
            int startedTick, int remainingObservationTicks, bool failed, bool driven, bool delivered,
            IReadOnlyList<RHAH_SettlementMember> members, int motherLoadId = 0, string motherName = null)
        {
            Kind = kind;
            Id = id;
            BatchId = batchId;
            MapId = mapId;
            StartedTick = startedTick;
            SourceDisplayId = SourceOf(members);
            RemainingObservationTicks = remainingObservationTicks;
            Failed = failed;
            Driven = driven;
            Delivered = delivered;
            MotherLoadId = motherLoadId;
            MotherName = motherName;
            Members = members;
        }

        static string SourceOf(IReadOnlyList<RHAH_SettlementMember> members)
        {
            string source = null;
            for (int i = 0; i < members.Count; i++)
            {
                string candidate = Identity.CompRHAH_Pawn.TryGet(members[i].Pawn)?.State.sourceIncidentDisplayId;
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                if (source != null && source != candidate)
                {
                    return null;
                }

                source = candidate;
            }

            return source;
        }
    }

    internal static class RHAH_SettlementProgress
    {
        sealed class Cache
        {
            internal int Tick = -1;
            internal int ObserveDays;
            internal int RecordCount;
            internal IReadOnlyList<RHAH_SettlementGroup> Groups;

            internal readonly Func<int, Verse.Pawn> Resolve;

            public Cache()
            {
                Resolve = loadId => RHAH_PawnIndex.Find(loadId, Tick);
            }
        }

        static readonly ConditionalWeakTable<NarrativeState, Cache> caches = new ConditionalWeakTable<NarrativeState, Cache>();
        static readonly IReadOnlyList<RHAH_SettlementGroup> empty = Array.Empty<RHAH_SettlementGroup>();

        internal static IReadOnlyList<RHAH_SettlementGroup> Read(NarrativeState state, int tick)
        {
            SuiyinBook book = state?.Book;
            if (book == null)
            {
                return empty;
            }

            int observeDays = RHAH_Mod.Settings?.narrativeObserveDays ?? book.Config.ObserveDays;
            int count = book.Journals.Count + book.N005.Count + book.N004.Count;
            Cache cache = caches.GetOrCreateValue(state);
            if (cache.Groups == null || tick < cache.Tick || tick / RHAH_JournalRuntime.CheckInterval != cache.Tick / RHAH_JournalRuntime.CheckInterval ||
                cache.ObserveDays != observeDays || cache.RecordCount != count)
            {
                cache.Tick = tick;
                cache.Groups = Build(book, tick, observeDays, cache.Resolve);
                cache.ObserveDays = observeDays;
                cache.RecordCount = count;
            }

            return cache.Groups;
        }

        internal static void Invalidate(NarrativeState state)
        {
            if (state != null)
            {
                caches.Remove(state);
            }
        }


        internal static IReadOnlyList<RHAH_SettlementGroup> Build(SuiyinBook book, int tick,
            int observeDays, Func<int, Verse.Pawn> resolve = null)
        {
            List<RHAH_SettlementGroup> groups = new List<RHAH_SettlementGroup>();
            foreach (SuiyinJournalCase record in book.Journals)
            {
                if (record == null || record.Closed || record.Id <= 0)
                {
                    continue;
                }

                groups.Add(new RHAH_SettlementGroup(RHAH_SettlementKind.Relief, record.Id, record.BatchId,
                    record.MapId, record.StartedTick, Remaining(observeDays, tick - record.StartedTick), record.Failed,
                    record.Driven, record.Delivered,
                    Members(record.People, RHAH_SettlementKind.Relief, null, resolve)));
            }

            foreach (SuiyinN005Case record in book.N005)
            {
                if (record == null || record.CareClosed || record.Outcome != SuiyinN005Outcome.Accepted)
                {
                    continue;
                }

                groups.Add(new RHAH_SettlementGroup(RHAH_SettlementKind.Exchange, record.Id, 0,
                    record.MapId, record.StartedTick, -1, false, false, true,
                    Members(record.Children, RHAH_SettlementKind.Exchange, null, resolve)));
            }

            foreach (SuiyinN004Case record in book.N004)
            {
                if (record == null || record.Outcome != SuiyinN004Outcome.Pending)
                {
                    continue;
                }

                groups.Add(new RHAH_SettlementGroup(RHAH_SettlementKind.Entrust, record.Id, 0,
                    record.MapId, record.StartedTick, Remaining(observeDays, tick - record.StartedTick), false, false, false,
                    Members(record.Children, RHAH_SettlementKind.Entrust, record.Mother, resolve),
                    record.MotherId, resolve?.Invoke(record.MotherId)?.LabelShort));
            }

            return groups;
        }

        static IReadOnlyList<RHAH_SettlementMember> Members(List<SuiyinMember> members,
            RHAH_SettlementKind kind, SuiyinPresence? mother,
            Func<int, Verse.Pawn> resolve)
        {
            if (members == null)
            {
                return Array.Empty<RHAH_SettlementMember>();
            }

            List<RHAH_SettlementMember> result = new List<RHAH_SettlementMember>(members.Count);
            foreach (SuiyinMember member in members)
            {
                if (member == null)
                {
                    continue;
                }

                Verse.Pawn pawn = resolve?.Invoke(member.LoadId);
                RHAH_SettlementBlocker blockers = Conditions(member, kind, pawn);
                int ticks = kind == RHAH_SettlementKind.Entrust ? -1 :
                    Math.Max(0, SuiyinMember.SettlementTicks + 1 - member.CareTicks);
                bool ready;
                if (kind == RHAH_SettlementKind.Entrust)
                {
                    ready = member.Presence == SuiyinPresence.Here && !member.Child &&
                        (mother == SuiyinPresence.Dead ||
                        (mother == SuiyinPresence.Here && (blockers & RHAH_SettlementBlocker.Captive) == 0));
                }
                else
                {
                    ready = member.Presence == SuiyinPresence.Here && member.IsSettled;
                }
                result.Add(new RHAH_SettlementMember(member, ticks, blockers, mother, ready, pawn));
            }

            return result;
        }

        static RHAH_SettlementBlocker Conditions(SuiyinMember member, RHAH_SettlementKind kind, Verse.Pawn pawn)
        {
            switch (member.Presence)
            {
                case SuiyinPresence.Dead: return RHAH_SettlementBlocker.Dead;
                case SuiyinPresence.Left: return RHAH_SettlementBlocker.Left;
                case SuiyinPresence.Missing: return RHAH_SettlementBlocker.Missing;
                case SuiyinPresence.Unknown: return RHAH_SettlementBlocker.Unknown;
            }

            RHAH_SettlementBlocker blockers = RHAH_SettlementBlocker.None;
            if (kind == RHAH_SettlementKind.Entrust)
            {
                if (member.Child)
                {
                    blockers |= RHAH_SettlementBlocker.Growing;
                }
            }
            else
            {
                bool malnutrition = pawn == null ? member.Care == SuiyinCare.Malnutrition : SuiyinMember.HasMalnutrition(pawn);
                return malnutrition ? RHAH_SettlementBlocker.Malnutrition : RHAH_SettlementBlocker.None;
            }

            bool captive = pawn == null ? member.Care == SuiyinCare.Captive : pawn.IsPrisoner || pawn.IsSlave;
            if (captive)
            {
                blockers |= RHAH_SettlementBlocker.Captive;
            }

            return blockers;
        }

        static int Remaining(int days, int elapsed)
        {
            return Math.Max(0, days * GenDate.TicksPerDay - Math.Max(0, elapsed));
        }
    }
}
