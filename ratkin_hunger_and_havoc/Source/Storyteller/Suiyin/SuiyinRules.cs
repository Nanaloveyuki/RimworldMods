using System.Collections.Generic;
using Verse;
using RimWorld;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum SuiyinPresence
    {
        Unknown = 0,
        Here = 1,
        Dead = 2,
        Left = 3,
        Missing = 4
    }

    internal enum SuiyinCare
    {
        Free = 0,
        Captive = 1,
        Hungry = 2,
        Plague = 3,
        Malnutrition = 4
    }

    internal enum SuiyinN004Outcome
    {
        Pending = 0,
        ChildAlone = 1,
        FamilyHere = 2,
        Captive = 3,
        Story = 4,
        Banished = 5,
        Regret = 6,
        Unknown = 7
    }

    internal enum SuiyinN005Outcome
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        TimedOut = 3,
        Broken = 4,
        Cared = 5,
        Dead = 6,
        Left = 7,
        Missing = 8
    }

    public sealed class SuiyinMember : IExposable
    {
        internal int LoadId;
        internal SuiyinPresence Presence = SuiyinPresence.Here;
        internal SuiyinCare Care;
        internal bool Child;
        internal int CareTicks;
        internal int MissingSince = -1;
        internal int LastCareTick = -1;

        internal const int SettlementTicks = 3 * GenDate.TicksPerDay;
        internal bool IsSettled => CareTicks > SettlementTicks;

        internal void ObserveSettlement(int tick, bool malnutrition)
        {
            if (LastCareTick >= 0 && tick > LastCareTick && Presence == SuiyinPresence.Here &&
                !malnutrition && Care != SuiyinCare.Malnutrition)
            {
                CareTicks = (int)System.Math.Min(SettlementTicks + 1L, (long)CareTicks + tick - LastCareTick);
            }

            LastCareTick = Presence == SuiyinPresence.Here ? tick : -1;
            Care = malnutrition ? SuiyinCare.Malnutrition : SuiyinCare.Free;
        }

        internal static bool HasMalnutrition(Verse.Pawn pawn)
        {
            return RimWorld.HediffDefOf.Malnutrition != null &&
                pawn?.health?.hediffSet?.HasHediff(RimWorld.HediffDefOf.Malnutrition) == true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref LoadId, "loadId", 0);
            Scribe_Values.Look(ref Presence, "presence", SuiyinPresence.Here);
            Scribe_Values.Look(ref Care, "care", SuiyinCare.Free);
            Scribe_Values.Look(ref Child, "child", false);
            Scribe_Values.Look(ref CareTicks, "careTicks", 0);
            Scribe_Values.Look(ref MissingSince, "missingSince", -1);
            Scribe_Values.Look(ref LastCareTick, "lastCareTick", -1);
        }
    }

    public sealed class SuiyinN004Case : IExposable
    {
        internal int Id;
        internal int MotherId;
        internal int MapId;
        internal int StartedTick;
        internal SuiyinN004Outcome Outcome;
        internal SuiyinPresence Mother = SuiyinPresence.Here;
        internal int MissingSince = -1;
        internal bool EffectsApplied;
        internal List<SuiyinMember> Children = new List<SuiyinMember>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", 0);
            Scribe_Values.Look(ref MotherId, "motherId", 0);
            Scribe_Values.Look(ref MapId, "mapId", 0);
            Scribe_Values.Look(ref StartedTick, "startedTick", 0);
            Scribe_Values.Look(ref Outcome, "outcome", SuiyinN004Outcome.Pending);
            Scribe_Values.Look(ref Mother, "mother", SuiyinPresence.Here);
            Scribe_Values.Look(ref MissingSince, "missingSince", -1);
            Scribe_Values.Look(ref EffectsApplied, "effectsApplied", false);
            Scribe_Collections.Look(ref Children, "children", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Children = Children ?? new List<SuiyinMember>();
            }
        }
    }

    public sealed class SuiyinN005Case : IExposable
    {
        internal int Id;
        internal int MapId;
        internal int StartedTick;
        internal SuiyinN005Outcome Outcome;
        internal bool CareClosed;
        internal List<SuiyinMember> Children = new List<SuiyinMember>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", 0);
            Scribe_Values.Look(ref MapId, "mapId", 0);
            Scribe_Values.Look(ref StartedTick, "startedTick", 0);
            Scribe_Values.Look(ref Outcome, "outcome", SuiyinN005Outcome.Pending);
            Scribe_Values.Look(ref CareClosed, "careClosed", false);
            Scribe_Collections.Look(ref Children, "children", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Children = Children ?? new List<SuiyinMember>();
            }
        }
    }

    public sealed class SuiyinJournalCase : IExposable
    {
        internal int Id;
        internal int MapId;
        internal int BatchId;
        internal int StartedTick;
        internal bool Delivered;
        internal bool Driven;
        internal bool Failed;
        internal bool Empty;
        internal bool Closed;
        internal bool Counted;
        internal List<SuiyinMember> People = new List<SuiyinMember>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", 0);
            Scribe_Values.Look(ref MapId, "mapId", 0);
            Scribe_Values.Look(ref BatchId, "batchId", 0);
            Scribe_Values.Look(ref StartedTick, "startedTick", 0);
            Scribe_Values.Look(ref Delivered, "delivered", false);
            Scribe_Values.Look(ref Driven, "driven", false);
            Scribe_Values.Look(ref Failed, "failed", false);
            Scribe_Values.Look(ref Empty, "empty", false);
            Scribe_Values.Look(ref Closed, "closed", false);
            Scribe_Values.Look(ref Counted, "counted", false);
            Scribe_Collections.Look(ref People, "people", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                People = People ?? new List<SuiyinMember>();
            }
        }
    }

    // 普通来客的安置与逐次结算记录 不生成剧情或奖励
    internal sealed class SuiyinBook
    {
        internal List<SuiyinN004Case> N004 = new List<SuiyinN004Case>();
        internal List<SuiyinN005Case> N005 = new List<SuiyinN005Case>();
        internal List<SuiyinJournalCase> Journals = new List<SuiyinJournalCase>();
        internal int NextCaseId = 1;
        internal int Trust;
        internal bool EntrustEnabled = true;
        internal bool ExchangeEnabled = true;

        internal bool OpenEntrust(int motherId, int mapId, int tick, IList<int> childIds)
        {
            if (!EntrustEnabled || motherId <= 0 || childIds == null || childIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < N004.Count; i++)
            {
                if (N004[i].MotherId == motherId && N004[i].Outcome == SuiyinN004Outcome.Pending)
                {
                    return false;
                }
            }

            SuiyinN004Case record = new SuiyinN004Case
            {
                Id = NextCaseId++,
                MotherId = motherId,
                MapId = mapId,
                StartedTick = tick
            };
            for (int i = 0; i < childIds.Count; i++)
            {
                if (childIds[i] > 0)
                {
                    record.Children.Add(new SuiyinMember { LoadId = childIds[i], Child = true });
                }
            }

            if (record.Children.Count == 0)
            {
                return false;
            }

            N004.Add(record);
            return true;
        }

        internal SuiyinN004Outcome ResolveEntrust(SuiyinN004Case record, int tick, SuiyinPresence mother)
        {
            if (record == null || record.Outcome != SuiyinN004Outcome.Pending)
            {
                return record == null ? SuiyinN004Outcome.Pending : record.Outcome;
            }

            int alive = 0;
            int dead = 0;
            int present = 0;
            int captive = 0;
            int grown = 0;
            for (int i = 0; i < record.Children.Count; i++)
            {
                SuiyinMember child = record.Children[i];
                if (child.Presence == SuiyinPresence.Dead)
                {
                    dead++;
                    continue;
                }

                if (child.Presence == SuiyinPresence.Unknown)
                {
                    continue;
                }

                alive++;
                if (child.Presence == SuiyinPresence.Here)
                {
                    present++;
                }

                if (child.Care == SuiyinCare.Captive)
                {
                    captive++;
                }

                if (!child.Child)
                {
                    grown++;
                }
            }

            SuiyinN004Outcome next = SuiyinN004Outcome.Pending;
            if (mother == SuiyinPresence.Dead && alive == 0 && dead == record.Children.Count)
            {
                next = SuiyinN004Outcome.Story;
            }
            else if (mother == SuiyinPresence.Here && alive == 0 && dead > 0)
            {
                next = SuiyinN004Outcome.Regret;
            }
            else if (mother == SuiyinPresence.Here && alive > 0 && present == alive && grown == alive && captive == 0)
            {
                next = SuiyinN004Outcome.FamilyHere;
            }
            else if (mother == SuiyinPresence.Dead && alive > 0 && grown == alive && captive == alive)
            {
                next = SuiyinN004Outcome.Captive;
            }
            else if (mother == SuiyinPresence.Dead && alive > 0 && grown == alive && present == alive)
            {
                next = SuiyinN004Outcome.ChildAlone;
            }
            else if ((mother == SuiyinPresence.Left || mother == SuiyinPresence.Missing) && present < alive)
            {
                next = SuiyinN004Outcome.Banished;
            }
            else if (tick - record.StartedTick >= Days(Core.RHAH_Mod.Settings?.narrativeObserveDays ?? 30) && grown < alive)
            {
                next = SuiyinN004Outcome.Unknown;
            }

            if (next == SuiyinN004Outcome.Pending)
            {
                return next;
            }

            record.Outcome = next;
            if (!record.EffectsApplied)
            {
                record.EffectsApplied = true;
                Trust = SuiyinNodes.ClampTrust(Trust + TrustFor(next));
            }

            return next;
        }

        internal bool AcceptExchange(int id, int mapId, int tick, IList<int> childIds)
        {
            if (!ExchangeEnabled || id <= 0 || FindN005(id) != null)
            {
                return false;
            }

            SuiyinN005Case record = new SuiyinN005Case
            {
                Id = id,
                MapId = mapId,
                StartedTick = tick,
                Outcome = SuiyinN005Outcome.Accepted
            };
            if (childIds != null)
            {
                for (int i = 0; i < childIds.Count; i++)
                {
                    if (childIds[i] > 0)
                    {
                        record.Children.Add(new SuiyinMember { LoadId = childIds[i], Child = true, LastCareTick = tick });
                    }
                }
            }

            N005.Add(record);
            return true;
        }

        internal SuiyinN005Outcome AdvanceCare(SuiyinN005Case record, int tick)
        {
            if (record == null || record.Outcome != SuiyinN005Outcome.Accepted || record.CareClosed)
            {
                return record == null ? SuiyinN005Outcome.Pending : record.Outcome;
            }

            if (record.Children.Count == 0)
            {
                record.CareClosed = true;
                record.Outcome = SuiyinN005Outcome.Missing;
                return record.Outcome;
            }

            bool pending = false;
            bool anyDead = false;
            bool anyMissing = false;
            bool anyLeft = false;
            bool allSettled = true;
            for (int i = 0; i < record.Children.Count; i++)
            {
                SuiyinMember child = record.Children[i];
                if (child.Presence == SuiyinPresence.Unknown)
                {
                    if (child.MissingSince < 0)
                    {
                        child.MissingSince = tick;
                    }
                    if (tick - child.MissingSince >= Days(Core.RHAH_Mod.Settings?.narrativeMissingDays ?? 1))
                    {
                        child.Presence = SuiyinPresence.Missing;
                    }
                }


                if (child.Presence == SuiyinPresence.Here)
                {
                    if (!child.IsSettled)
                    {
                        pending = true;
                        allSettled = false;
                    }
                }
                else if (child.Presence == SuiyinPresence.Dead)
                {
                    anyDead = true;
                    allSettled = false;
                }
                else if (child.Presence == SuiyinPresence.Left)
                {
                    anyLeft = true;
                    allSettled = false;
                }
                else if (child.Presence == SuiyinPresence.Unknown)
                {
                    pending = true;
                    allSettled = false;
                }
                else if (child.Presence == SuiyinPresence.Missing)
                {
                    anyMissing = true;
                    allSettled = false;
                }
            }

            if (pending)
            {
                return record.Outcome;
            }

            record.CareClosed = true;
            if (anyDead)
            {
                record.Outcome = SuiyinN005Outcome.Dead;
            }
            else if (allSettled)
            {
                record.Outcome = SuiyinN005Outcome.Cared;
                Trust = SuiyinNodes.ClampTrust(Trust + (Core.RHAH_Mod.Settings == null ? 3 : Core.RHAH_Mod.Settings.trustExchange));
            }
            else if (anyMissing)
            {
                record.Outcome = SuiyinN005Outcome.Missing;
            }
            else if (anyLeft)
            {
                record.Outcome = SuiyinN005Outcome.Left;
            }
            else
            {
                record.Outcome = SuiyinN005Outcome.Missing;
            }

            return record.Outcome;
        }

        internal bool OpenJournal(int journal, int mapId, int batchId, int tick, IList<int> loadIds, bool delivered)
        {
            if (journal < 1 || journal > 14 || batchId <= 0 || FindJournal(batchId) != null)
            {
                return false;
            }

            List<int> ids = RHAH_NarrativePace.MemberIds(loadIds, loadIds == null ? 0 : loadIds.Count);
            if (ids.Count == 0)
            {
                return false;
            }

            SuiyinJournalCase record = new SuiyinJournalCase
            {
                Id = journal,
                MapId = mapId,
                BatchId = batchId,
                StartedTick = tick,
                Delivered = delivered
            };
            for (int i = 0; i < ids.Count; i++)
            {
                record.People.Add(new SuiyinMember { LoadId = ids[i], LastCareTick = tick });
            }

            Journals.Add(record);

            return true;
        }

        internal bool CloseJournal(SuiyinJournalCase record, int tick)
        {
            if (record == null || record.Closed || record.Counted)
            {
                return false;
            }

            if (record.Failed)
            {
                if (record.Id == 0)
                {
                    return false;
                }

                record.Closed = true;
                record.Counted = false;
                return false;
            }

            int living = 0;
            int left = 0;
            int settled = 0;
            int pending = 0;
            bool present = false;
            for (int i = 0; i < record.People.Count; i++)
            {
                SuiyinMember person = record.People[i];
                if (person.Presence == SuiyinPresence.Dead)
                {
                    continue;
                }

                if (person.Presence == SuiyinPresence.Unknown)
                {
                    if (person.MissingSince < 0)
                    {
                        person.MissingSince = tick;
                    }

                    if (tick - person.MissingSince >= Days(Core.RHAH_Mod.Settings?.narrativeMissingDays ?? 1))
                    {
                        person.Presence = SuiyinPresence.Missing;
                    }
                }

                living++;
                if (person.Presence == SuiyinPresence.Here)
                {
                    present = true;
                    if (person.IsSettled)
                    {
                        settled++;
                    }
                    else
                    {
                        pending++;
                    }
                }
                else if (person.Presence == SuiyinPresence.Left)
                {
                    left++;
                }
                else
                {
                    pending++;
                }
            }

            if (living == 0)
            {
                record.Closed = true;
                record.Counted = false;
                record.Empty = true;
                return false;
            }

            if (pending > 0 && (present || tick - record.StartedTick < Days(Core.RHAH_Mod.Settings?.narrativeObserveDays ?? 30)))
            {
                return false;
            }

            record.Closed = true;
            bool aid = !record.Driven && pending == 0 && left + settled == living && (record.Delivered || settled == living);
            record.Counted = aid;
            return aid;
        }

        internal bool OpenInterception(int mapId, int batchId, int tick, IList<int> loadIds)
        {
            if (batchId <= 0 || FindJournal(batchId) != null)
            {
                return false;
            }

            List<int> ids = RHAH_NarrativePace.MemberIds(loadIds, loadIds == null ? 0 : loadIds.Count);
            if (ids.Count == 0)
            {
                return false;
            }

            SuiyinJournalCase record = new SuiyinJournalCase
            {
                Id = 0,
                MapId = mapId,
                BatchId = batchId,
                StartedTick = tick,
                Failed = true,
                Counted = false,
                Closed = false
            };
            for (int i = 0; i < ids.Count; i++)
            {
                record.People.Add(new SuiyinMember { LoadId = ids[i], Presence = SuiyinPresence.Left });
            }

            Journals.Add(record);
            return true;
        }

        internal bool FailInterception(int batchId)
        {
            SuiyinJournalCase record = FindJournal(batchId);
            if (record == null || record.Closed || record.Counted || record.Id != 0 || !record.Failed)
            {
                return false;
            }

            record.Closed = true;
            record.Counted = false;
            record.Delivered = false;
            record.Driven = false;
            record.Empty = false;
            Trust = SuiyinNodes.ClampTrust(Trust - 2);
            return true;
        }

        internal bool MarkJournalFailed(int batchId)
        {
            SuiyinJournalCase record = FindJournal(batchId);
            if (record == null || record.Failed || record.Counted)
            {
                return false;
            }

            record.Failed = true;
            record.Closed = true;
            record.Counted = false;
            return true;
        }

        internal bool MarkJournalDelivered(int batchId)
        {
            SuiyinJournalCase record = FindJournal(batchId);
            if (record == null || record.Closed || record.Failed)
            {
                return false;
            }

            record.Delivered = true;
            return true;
        }

        internal static int JournalFor(string displayId)
        {
            if (string.IsNullOrEmpty(displayId))
            {
                return 0;
            }

            if (displayId == "I-032" || displayId == "I-046") return 1;
            if (displayId == "I-033" || displayId == "I-047") return 2;
            if (displayId == "I-029" || displayId == "I-044") return 3;
            if (displayId == "I-031" || displayId == "I-039") return 4;
            if (displayId == "I-006" || displayId == "I-007" || displayId == "I-043") return 5;
            if (displayId == "I-001") return 6;
            if (displayId.StartsWith("I-02") && displayId != "I-029") return 7;
            if (displayId == "I-012" || displayId == "I-038") return 8;
            if (displayId == "I-014" || displayId == "I-030" || displayId == "I-045") return 9;
            if (displayId == "I-034" || displayId == "I-048") return 10;
            if (displayId == "I-049") return 11;
            if (displayId == "I-035" || displayId == "I-050") return 12;
            if (displayId == "I-008" || displayId == "I-009" || displayId == "I-010" || displayId == "I-036" || displayId == "I-041") return 13;
            if (displayId == "I-015" || displayId == "I-016" || displayId == "I-017" || displayId == "I-018" || displayId == "I-019" ||
                displayId == "I-004" || displayId == "I-005" || displayId == "I-011" || displayId == "I-040" || displayId == "I-042") return 14;
            return 0;
        }

        static int TrustFor(SuiyinN004Outcome outcome)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            if (outcome == SuiyinN004Outcome.ChildAlone || outcome == SuiyinN004Outcome.FamilyHere)
            {
                return settings == null ? 5 : settings.trustEntrustGood;
            }

            if (outcome == SuiyinN004Outcome.Captive)
            {
                return settings == null ? -5 : settings.trustEntrustCaptive;
            }

            if (outcome == SuiyinN004Outcome.Story)
            {
                return settings == null ? -2 : settings.trustEntrustStory;
            }

            if (outcome == SuiyinN004Outcome.Regret)
            {
                return settings == null ? -3 : settings.trustEntrustRegret;
            }

            if (outcome == SuiyinN004Outcome.Banished)
            {
                return settings == null ? -1 : settings.trustEntrustBanished;
            }

            return 0;
        }

        SuiyinN005Case FindN005(int id)
        {
            for (int i = 0; i < N005.Count; i++)
            {
                if (N005[i].Id == id)
                {
                    return N005[i];
                }
            }

            return null;
        }

        SuiyinJournalCase FindJournal(int batchId)
        {
            for (int i = 0; i < Journals.Count; i++)
            {
                if (Journals[i].BatchId == batchId)
                {
                    return Journals[i];
                }
            }

            return null;
        }

        static int Days(int days)
        {
            return days * GenDate.TicksPerDay;
        }
    }
}
