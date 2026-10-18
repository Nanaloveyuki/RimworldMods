using System.Collections.Generic;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    public class ChoiceLetter_RHAH_Envoy : ChoiceLetter
    {
        public int mapId;
        public int pawnId;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                SuiyinN008Case record = FindRecord();
                if (ArchivedOnly || record == null || !RHAH_Envoy.OpenCase(record.Outcome))
                {
                    yield return Option_Close;
                    yield break;
                }
                if (!RHAH_Envoy.Refresh(State(), record, Now()))
                {
                    yield return Option_Close;
                    yield return Option_Postpone;
                    yield break;
                }


                Map map = ResolveMap();
                bool meals = map != null && RHAH_ChoiceRuntime.Stock(map, RHAH_RequestKind.SimpleMeal) >= (Core.RHAH_Mod.Settings == null ? RHAH_Envoy.MealCost : Core.RHAH_Mod.Settings.envoyMealCost);
                DiaOption trade = new DiaOption("RHAH_Envoy_Trade".Translate(Core.RHAH_Mod.Settings == null ? RHAH_Envoy.MealCost : Core.RHAH_Mod.Settings.envoyMealCost));
                if (!meals)
                {
                    trade.Disable("RHAH_Envoy_NoMeals".Translate(Core.RHAH_Mod.Settings == null ? RHAH_Envoy.MealCost : Core.RHAH_Mod.Settings.envoyMealCost));
                }
                else
                {
                    trade.action = () => Wrapped(trade, Trade);
                    trade.resolveTree = false;
                }

                yield return trade;
                if (record.Outcome == SuiyinN008Outcome.Waiting)
                {
                    yield return Act("RHAH_Envoy_Proof", Proof);
                }
                yield return Act("RHAH_Envoy_Refuse", Refuse);
                DiaOption drive = new DiaOption("RHAH_Envoy_Drive".Translate(Core.RHAH_Mod.Settings == null ? -2 : Core.RHAH_Mod.Settings.trustEnvoyFail));
                drive.action = () => Wrapped(drive, Drive);
                drive.resolveTree = false;
                yield return drive;
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
            Scribe_Values.Look(ref pawnId, "pawnId", 0);
        }

        void Trade()
        {
            Map map = ResolveMap();
            NarrativeState state = State();
            SuiyinN008Case record = FindRecord();
            if (map == null || !RHAH_Envoy.Refresh(state, record, Now()))
            {
                Stale();
                return;
            }

            int cost = Core.RHAH_Mod.Settings == null ? RHAH_Envoy.MealCost : Core.RHAH_Mod.Settings.envoyMealCost;
            if (RHAH_ChoiceRuntime.Stock(map, RHAH_RequestKind.SimpleMeal) < cost)
            {
                Messages.Message("RHAH_Envoy_NoMeals".Translate(cost), MessageTypeDefOf.RejectInput);
                return;
            }

            bool needsSite = state.Book.N009 != null || state.Book.RelicClue;
            if (needsSite && !RHAH_ChoiceRuntime.TryCreateSite(map, RHAH_IntelSiteKind.Treasure, () => CompleteTrade(state, record, map, cost)))
            {
                Messages.Message("RHAH_Choice_SiteUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (!needsSite && !CompleteTrade(state, record, map, cost))
            {
                return;
            }

            RHAH_Envoy.Complete(record);
        }

        static bool CompleteTrade(NarrativeState state, SuiyinN008Case record, Map map, int cost)
        {
            if (!RHAH_Envoy.Refresh(state, record, Now()))
            {
                Stale();
                return false;
            }

            if (!RHAH_ChoiceRuntime.TryConsume(map, RHAH_RequestKind.SimpleMeal, cost))
            {
                Messages.Message("RHAH_Envoy_NoMeals".Translate(cost), MessageTypeDefOf.RejectInput);
                return false;
            }

            record.MealsReady = true;
            return state.Commit(book => book.ChooseEnvoy(record, SuiyinN008Action.Trade, Now()));
        }

        void Proof()
        {
            NarrativeState state = State();
            SuiyinN008Case record = FindRecord();
            if (!RHAH_Envoy.Refresh(state, record, Now()) || record.Outcome != SuiyinN008Outcome.Waiting)
            {
                return;
            }

            record.ProofAvailable = RHAH_Envoy.HasProof(state.Book);
            if (!state.Commit(book => book.ChooseEnvoy(record, SuiyinN008Action.Proof, Now())))
            {
                record.ProofAvailable = false;
                return;
            }

            if (record.Outcome != SuiyinN008Outcome.Checking)
            {
                RHAH_Envoy.Complete(record);
            }
        }

        void Refuse()
        {
            Choose(SuiyinN008Action.Refuse);
        }

        void Drive()
        {
            Choose(SuiyinN008Action.Drive);
        }

        void Choose(SuiyinN008Action action)
        {
            NarrativeState state = State();
            SuiyinN008Case record = FindRecord();
            if (!RHAH_Envoy.Refresh(state, record, Now()))
            {
                return;
            }

            if (!state.Commit(book => book.ChooseEnvoy(record, action, Now())))
            {
                return;
            }

            RHAH_Envoy.Complete(record);
        }


        static int Now()
        {
            return Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
        }


        static void Stale()
        {
            Messages.Message("RHAH_Envoy_Stale".Translate(), MessageTypeDefOf.RejectInput);
        }

        DiaOption Act(string key, System.Action action)
        {
            DiaOption option = new DiaOption(key.Translate());
            option.resolveTree = false;
            option.action = () => Wrapped(option, action);
            return option;
        }

        // 仅当信件真的离开信栈才关闭它自己的窗口
        void Wrapped(DiaOption option, System.Action action)
        {
            bool open = Find.LetterStack != null && Find.LetterStack.LettersListForReading.Contains(this);
            SuiyinN008Case record = FindRecord();
            if (!open || !RHAH_Envoy.Refresh(State(), record, Now()))
            {
                Messages.Message("RHAH_Envoy_Stale".Translate(), MessageTypeDefOf.RejectInput);
                if (open && (Find.LetterStack == null || !Find.LetterStack.LettersListForReading.Contains(this)))
                {
                    option.dialog?.Close();
                }
                return;
            }

            SuiyinN008Outcome before = record.Outcome;
            action();
            if (open && (record.Outcome != before || Find.LetterStack == null || !Find.LetterStack.LettersListForReading.Contains(this)))
            {
                option.dialog?.Close();
            }
        }

        SuiyinN008Case FindRecord()
        {
            SuiyinBook book = Book();
            if (book?.N008 == null)
            {
                return null;
            }

            for (int i = 0; i < book.N008.Count; i++)
            {
                SuiyinN008Case record = book.N008[i];
                if (record != null && record.MapId == mapId && record.PawnId == pawnId)
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
    }
}
