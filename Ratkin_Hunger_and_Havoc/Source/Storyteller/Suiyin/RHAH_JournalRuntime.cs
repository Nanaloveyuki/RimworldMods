using System.Collections.Generic;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using HungerAndHavoc.Narrative;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_JournalRuntime
    {
        internal const int CheckInterval = 2500;

        internal static void Open(RHAH_IncidentContext context, IList<int> loadIds)
        {
            if (context == null || context.Map == null || context.SpawnBatchId <= 0 || loadIds == null || loadIds.Count == 0)
            {
                return;
            }

            int journal = SuiyinBook.JournalFor(context.DisplayId);
            if (journal <= 0)
            {
                return;
            }

            NarrativeState state = Current.Game?.GetComponent<NarrativeState>();
            if (state?.Book == null)
            {
                return;
            }

            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            state.Book.OpenJournal(
                journal,
                context.Map.uniqueID,
                context.SpawnBatchId,
                tick,
                loadIds,
                false);
        }

        internal static void Tick(int tick)
        {
            if (!RHAH_NarrativePace.Due(tick, CheckInterval, RHAH_NarrativePace.Spread) || Current.Game == null)
            {
                return;
            }

            NarrativeState state = Current.Game.GetComponent<NarrativeState>();
            SuiyinBook book = state?.Book;
            if (book?.Journals == null)
            {
                return;
            }

            for (int i = 0; i < book.Journals.Count; i++)
            {
                SuiyinJournalCase record = book.Journals[i];
                if (record == null || record.Closed)
                {
                    continue;
                }

                Watch(record, tick);
                if (book.CloseJournal(record, tick))
                {
                    if (record.Id == 14)
                    {
                        HungerAndHavoc.EventMgr.RHAH_EventFollowMood.OnRelief(AidDisplay(record), Living(record));
                    }

                    if (RHAH_EndingRuntime.CountsNow())
                    {
                        state.NoteAid(tick);
                        state.NoteCompletedKind(tick, record.Id);
                    }
                }
            }
        }

        internal static bool HasJournal(int journal)
        {
            if (journal < 1 || journal > 14 || Current.Game == null)
            {
                return false;
            }

            SuiyinBook book = Current.Game.GetComponent<NarrativeState>()?.Book;
            if (book == null)
            {
                return false;
            }

            if (book.JournalNoted != null && book.JournalNoted.Contains(journal))
            {
                return true;
            }

            if (book.Journals == null)
            {
                return false;
            }

            for (int i = 0; i < book.Journals.Count; i++)
            {
                if (book.Journals[i] != null && book.Journals[i].Id == journal)
                {
                    return true;
                }
            }

            return false;
        }

        static string AidDisplay(SuiyinJournalCase record)
        {
            if (record?.People == null)
            {
                return "";
            }

            for (int i = 0; i < record.People.Count; i++)
            {
                Verse.Pawn pawn = Member(record.People[i]);
                Identity.CompRHAH_Pawn comp = Identity.CompRHAH_Pawn.TryGet(pawn);
                if (comp != null && !string.IsNullOrEmpty(comp.State.sourceIncidentDisplayId))
                {
                    return comp.State.sourceIncidentDisplayId;
                }
            }

            return "";
        }

        static List<Verse.Pawn> Living(SuiyinJournalCase record)
        {
            List<Verse.Pawn> living = new List<Verse.Pawn>();
            if (record?.People == null)
            {
                return living;
            }

            for (int i = 0; i < record.People.Count; i++)
            {
                Verse.Pawn pawn = Member(record.People[i]);
                if (pawn != null && !pawn.Dead)
                {
                    living.Add(pawn);
                }
            }

            return living;
        }

        static Verse.Pawn Member(SuiyinMember member)
        {
            if (member == null)
            {
                return null;
            }

            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            return RHAH_PawnIndex.Find(member.LoadId, tick);
        }

        static void Watch(SuiyinJournalCase record, int tick)
        {
            if (record.People == null)
            {
                return;
            }

            bool mugging = record.Id == 12;
            if (mugging && (record.Delivered || Fighting(record)))
            {
                for (int i = 0; i < record.People.Count; i++)
                {
                    SuiyinMember mugger = record.People[i];
                    if (mugger != null && mugger.Presence == SuiyinPresence.Here)
                    {
                        mugger.Presence = SuiyinPresence.Left;
                    }
                }

                return;
            }

            for (int i = 0; i < record.People.Count; i++)
            {
                SuiyinMember person = record.People[i];
                Note(record, person, tick);
                person?.ObserveSettlement(tick, SuiyinMember.HasMalnutrition(Member(person)));
            }
        }

        static void Note(SuiyinJournalCase record, SuiyinMember person, int tick)
        {
            if (person == null || person.Presence == SuiyinPresence.Dead || person.Presence == SuiyinPresence.Left || person.Presence == SuiyinPresence.Missing)
            {
                return;
            }

            Verse.Pawn pawn = FindPawn(person.LoadId);
            if (pawn == null || pawn.Destroyed)
            {
                if (person.MissingSince < 0)
                {
                    person.MissingSince = tick;
                }

                person.Presence = SuiyinPresence.Unknown;
                return;
            }

            person.MissingSince = -1;
            if (pawn.Dead)
            {
                person.Presence = SuiyinPresence.Dead;
                return;
            }


            Map home = MapOf(record.MapId);
            bool onHome = home != null && pawn.MapHeld == home;

            if (!onHome && pawn.MapHeld != null)
            {
                person.Presence = SuiyinPresence.Left;
                return;
            }

            if (home == null)
            {
                person.Presence = SuiyinPresence.Unknown;
                if (person.MissingSince < 0)
                {
                    person.MissingSince = tick;
                }

                return;
            }

            person.Presence = SuiyinPresence.Here;
        }

        static bool Fighting(SuiyinJournalCase record)
        {
            if (record.People == null || Faction.OfPlayer == null)
            {
                return false;
            }

            for (int i = 0; i < record.People.Count; i++)
            {
                Verse.Pawn pawn = record.People[i] == null ? null : FindPawn(record.People[i].LoadId);
                if (pawn?.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer))
                {
                    return true;
                }
            }

            return false;
        }


        static Map MapOf(int mapId)
        {
            if (Find.Maps == null)
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

        static int Days(int days)
        {
            return days * 60000;
        }
    }
}
