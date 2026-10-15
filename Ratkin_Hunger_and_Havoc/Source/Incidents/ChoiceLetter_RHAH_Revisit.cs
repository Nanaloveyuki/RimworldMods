using System.Collections.Generic;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    public class ChoiceLetter_RHAH_Revisit : ChoiceLetter
    {
        public int caseId;
        internal int pawnId;
        internal PlanetTile meetingTile = PlanetTile.Invalid;
        internal int meetingMapId;
        internal int targetCaravanId;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                SuiyinN004Case record = FindRecord();
                if (ArchivedOnly || record == null || record.Revisit != SuiyinN004Revisit.None)
                {
                    yield return Option_Close;
                    yield break;
                }

                if (!RHAH_Revisit.TryMeeting(record, pawnId, meetingTile, meetingMapId, targetCaravanId, out Verse.Pawn pawn, out RimWorld.Planet.Caravan caravan)
                    || lookTargets == null || lookTargets.PrimaryTarget.Thing != pawn)
                {
                    DiaOption stale = new DiaOption("RHAH_Revisit_Stale".Translate());
                    stale.Disable("RHAH_Revisit_Stale".Translate());
                    yield return stale;
                    yield return Option_Close;
                    yield break;
                }

                int cost = RHAH_Revisit.CostFor(record);
                yield return Act("RHAH_Revisit_Leave", Leave);
                DiaOption pay = new DiaOption("RHAH_Revisit_Pay".Translate(cost));
                if (!RHAH_Revisit.CanPay(caravan))
                {
                    pay.Disable("RHAH_Revisit_NoSilver".Translate(cost));
                }
                else
                {
                    pay.action = () => Wrapped(pay, Pay);
                    pay.resolveTree = false;
                }

                yield return pay;
                yield return Act("RHAH_Revisit_Kill", Kill);
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
            Scribe_Values.Look(ref caseId, "caseId", 0);
            Scribe_Values.Look(ref pawnId, "pawnId", 0);
            Scribe_Values.Look(ref meetingTile, "meetingTile", PlanetTile.Invalid);
            Scribe_Values.Look(ref meetingMapId, "meetingMapId", 0);
            Scribe_Values.Look(ref targetCaravanId, "targetCaravanId", 0);
        }

        void Leave()
        {
            Choose(SuiyinN004Revisit.Ignore);
        }

        void Pay()
        {
            Choose(SuiyinN004Revisit.Rescue);
        }

        void Kill()
        {
            Choose(SuiyinN004Revisit.Kill);
        }

        void Choose(SuiyinN004Revisit choice)
        {
            NarrativeState state = Current.Game?.GetComponent<NarrativeState>();
            SuiyinN004Case record = FindRecord();
            if (Find.LetterStack == null || !Find.LetterStack.LettersListForReading.Contains(this)
                || state == null || record == null || record.Outcome != SuiyinN004Outcome.Banished
                || record.Revisit != SuiyinN004Revisit.None || !record.RevisitSeen
                || (choice == SuiyinN004Revisit.Rescue && record.RescuePaid)
                || !RHAH_Revisit.TryMeeting(record, pawnId, meetingTile, meetingMapId, targetCaravanId, out Verse.Pawn pawn, out RimWorld.Planet.Caravan caravan)
                || lookTargets == null || lookTargets.PrimaryTarget.Thing != pawn)
            {
                Messages.Message("RHAH_Revisit_Stale".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (choice == SuiyinN004Revisit.Rescue && !RHAH_Revisit.Spend(caravan))
            {
                Messages.Message("RHAH_Revisit_NoSilver".Translate(RHAH_Revisit.CostFor(record)), MessageTypeDefOf.RejectInput);
                return;
            }

            if (choice == SuiyinN004Revisit.Kill && !RHAH_Revisit.Kill(pawn))
            {
                Messages.Message("RHAH_Revisit_Stale".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            if (!state.Commit(book => book.ChooseRevisit(record, choice, Now(), true)))
            {
                Messages.Message("RHAH_Revisit_Stale".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Find.LetterStack.RemoveLetter(this);
        }

        static int Now()
        {
            return Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
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
            action();
            if (open && (Find.LetterStack == null || !Find.LetterStack.LettersListForReading.Contains(this)))
            {
                option.dialog?.Close();
            }
        }

        SuiyinN004Case FindRecord()
        {
            return RHAH_Revisit.FindCase(caseId);
        }

    }
}
