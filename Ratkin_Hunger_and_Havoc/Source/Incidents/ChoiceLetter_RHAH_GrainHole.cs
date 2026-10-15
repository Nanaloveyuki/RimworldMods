using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    public class ChoiceLetter_RHAH_GrainHole : ChoiceLetter
    {
        public int mapId;
        public bool followUp;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                SuiyinN006Case record = FindRecord();
                if (ArchivedOnly || record == null || !Open(record))
                {
                    yield return Option_Close;
                    yield break;
                }
                if (!RHAH_GrainHole.Refresh(ResolveMap(), record, Now()))
                {
                    yield return Option_Close;
                    yield break;
                }


                if (followUp)
                {
                    if (record.BaitUntil < 0 || Now() < record.BaitUntil)
                    {
                        yield return Option_Postpone;
                        yield break;
                    }

                    yield return Act("RHAH_Hole_Trace", Trace);
                    yield return Act("RHAH_Hole_Stop", Stop);
                }
                else
                {
                    yield return Act("RHAH_Hole_Seal", Seal);
                    yield return Act("RHAH_Hole_Bait", Bait);
                    yield return Act("RHAH_Hole_Clean", Clean);
                    yield return Act("RHAH_Hole_Ignore", Ignore);
                }

                if (lookTargets.IsValid())
                {
                    yield return Option_JumpToLocationAndPostpone;
                }

                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mapId, "mapId", 0);
            Scribe_Values.Look(ref followUp, "followUp", false);
        }

        void Seal()
        {
            Map map = ResolveMap();
            NarrativeState state = State();
            SuiyinN006Case record = FindRecord();
            if (state == null || !RHAH_GrainHole.Refresh(map, record, Now()) || record.Outcome != SuiyinN006Outcome.Pending)
            {
                Stale();
                return;
            }

            int cost = Core.RHAH_Mod.Settings == null ? RHAH_GrainHole.WoodCost : Core.RHAH_Mod.Settings.holeWoodCost;
            if (!RHAH_GrainHole.SpendWood(map, cost))
            {
                Messages.Message("RHAH_Hole_NoWood".Translate(RHAH_Mod.Settings == null ? RHAH_GrainHole.WoodCost : RHAH_Mod.Settings.holeWoodCost), MessageTypeDefOf.RejectInput);
                return;
            }

            record.Wood = cost;
            if (!state.Commit(book => book.ChooseHole(record, SuiyinN006Action.Seal, Now())))
            {
                record.Wood = 0;
                Stale();
                return;
            }


            RHAH_GrainHole.RemoveHole(map, map.GetComponent<MapComponent_RHAH_Map>());
            Close();
        }

        void Bait()
        {
            Map map = ResolveMap();
            NarrativeState state = State();
            SuiyinN006Case record = FindRecord();
            Thing hole = Hole(map);
            if (state == null || !RHAH_GrainHole.Refresh(map, record, Now()) || hole == null || record.Outcome != SuiyinN006Outcome.Pending || record.BaitUntil >= 0)
            {
                Stale();
                return;
            }

            if (!RHAH_GrainHole.TakeFood(map, hole.Position, 1, (Core.RHAH_Mod.Settings == null ? RHAH_GrainHole.LossRange : Core.RHAH_Mod.Settings.holeLossRange)))
            {
                Messages.Message("RHAH_Hole_NoFood".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            record.BaitStock = true;
            if (!state.Commit(book => book.ChooseHole(record, SuiyinN006Action.Bait, Now())))
            {
                record.BaitStock = false;
                Stale();
                return;
            }

            Close();
        }

        void Clean()
        {
            Map map = ResolveMap();
            NarrativeState state = State();
            SuiyinN006Case record = FindRecord();
            Thing hole = Hole(map);
            if (state == null || !RHAH_GrainHole.Refresh(map, record, Now()) || record.Outcome != SuiyinN006Outcome.Pending)
            {
                Stale();
                return;
            }

            if (hole != null)
            {
                RHAH_GrainHole.TakeFood(map, hole.Position, (Core.RHAH_Mod.Settings == null ? RHAH_GrainHole.CleanPortions : Core.RHAH_Mod.Settings.holeCleanPortions), (Core.RHAH_Mod.Settings == null ? RHAH_GrainHole.LossRange : Core.RHAH_Mod.Settings.holeLossRange));
            }

            if (!state.Commit(book => book.ChooseHole(record, SuiyinN006Action.Clean, Now())))
            {
                Stale();
                return;
            }

            RHAH_GrainHole.RemoveHole(map, map.GetComponent<MapComponent_RHAH_Map>());
            Close();
        }

        void Ignore()
        {
            Close();
        }

        void Trace()
        {
            Finish(true);
        }

        void Stop()
        {
            Finish(false);
        }

        void Finish(bool trace)
        {
            Map map = ResolveMap();
            NarrativeState state = State();
            SuiyinN006Case record = FindRecord();
            if (state == null || !RHAH_GrainHole.Refresh(map, record, Now()) || record.Outcome != SuiyinN006Outcome.BaitSet || record.BaitUntil < 0 || Now() < record.BaitUntil)
            {
                Stale();
                return;
            }

            if (trace)
            {
                if (!RHAH_ChoiceRuntime.TryCreateSite(map, RHAH_IntelSiteKind.Treasure,
                    () => state.Commit(book => book.FinishBait(record, Now(), true))))
                {
                    Messages.Message("RHAH_Choice_SiteUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }
            }
            else if (!state.Commit(book => book.FinishBait(record, Now(), false)))
            {
                Stale();
                return;
            }

            RHAH_GrainHole.RemoveHole(map, map.GetComponent<MapComponent_RHAH_Map>());
            Close();
        }

        void Close()
        {
            if (Find.LetterStack != null)
            {
                Find.LetterStack.RemoveLetter(this);
            }
        }

        static void Stale()
        {
            Messages.Message("RHAH_Hole_Stale".Translate(), MessageTypeDefOf.RejectInput);
        }

        static int Now()
        {
            return Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
        }

        DiaOption Act(string key, System.Action action)
        {
            TaggedString label = key == "RHAH_Hole_Seal"
                ? key.Translate(RHAH_Mod.Settings == null ? RHAH_GrainHole.WoodCost : RHAH_Mod.Settings.holeWoodCost)
                : key == "RHAH_Hole_Clean"
                    ? key.Translate(RHAH_Mod.Settings == null ? RHAH_GrainHole.CleanPortions : RHAH_Mod.Settings.holeCleanPortions)
                    : key.Translate();
            DiaOption option = new DiaOption(label);
            option.resolveTree = false;
            option.action = () =>
            {
                bool open = Find.LetterStack != null && Find.LetterStack.LettersListForReading.Contains(this);
                if (!open || !RHAH_GrainHole.Refresh(ResolveMap(), FindRecord(), Now()) || !Open(FindRecord()))
                {
                    Stale();
                    if (open && (Find.LetterStack == null || !Find.LetterStack.LettersListForReading.Contains(this)))
                    {
                        option.dialog?.Close();
                    }
                    return;
                }
                action();
                if (open && (Find.LetterStack == null || !Find.LetterStack.LettersListForReading.Contains(this)))
                {
                    option.dialog?.Close();
                }
            };
            return option;
        }

        bool Open(SuiyinN006Case record)
        {
            if (followUp)
            {
                return record.Outcome == SuiyinN006Outcome.BaitSet;
            }

            return record.Outcome == SuiyinN006Outcome.Pending && record.Hole;
        }

        SuiyinN006Case FindRecord()
        {
            SuiyinBook book = Book();
            if (book?.N006 == null)
            {
                return null;
            }

            for (int i = 0; i < book.N006.Count; i++)
            {
                SuiyinN006Case record = book.N006[i];
                if (record != null && record.MapId == mapId)
                {
                    return record;
                }
            }

            return null;
        }

        static NarrativeState State()
        {
            return Current.Game?.GetComponent<NarrativeState>();
        }

        static SuiyinBook Book()
        {
            return State()?.Book;
        }

        Map ResolveMap()
        {
            if (Find.Maps == null)
            {
                return null;
            }

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                if (Find.Maps[i] != null && Find.Maps[i].uniqueID == mapId)
                {
                    return Find.Maps[i];
                }
            }

            return null;
        }

        Thing Hole(Map map)
        {
            if (map == null)
            {
                return null;
            }

            MapComponent_RHAH_Map component = map.GetComponent<MapComponent_RHAH_Map>();
            if (component == null || component.HoleThingId <= 0)
            {
                return null;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(RHAH_GrainHole.HoleDefName);
            if (def == null)
            {
                return null;
            }

            List<Thing> holes = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < holes.Count; i++)
            {
                Thing hole = holes[i];
                if (hole != null && hole.thingIDNumber == component.HoleThingId && hole.Spawned)
                {
                    return hole;
                }
            }

            return null;
        }
    }
}
