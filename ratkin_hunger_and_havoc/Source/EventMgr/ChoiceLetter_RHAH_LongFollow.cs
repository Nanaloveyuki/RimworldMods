using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    public sealed class ChoiceLetter_RHAH_LongFollow : ChoiceLetter
    {
        internal int chainId;
        internal int phase;
        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                RHAH_LongFollowRecord record = RHAH_LongFollowRuntime.FindRecord(chainId);
                if (ArchivedOnly || record == null || record.Completed || record.Phase != phase || !record.LetterOpen)
                {
                    yield return Option_Close;
                    yield break;
                }
                Map map = RHAH_EventFollowRuntime.FindMap(record.MapId);
                if (map == null || map.mapPawns == null)
                {
                    yield return Option_Postpone;
                    yield break;
                }
                foreach (RHAH_LongOption choice in RHAH_LongFollowRuntime.Options(record, map))
                {
                    RHAH_LongOption captured = choice;
                    Verse.Pawn target = RHAH_LongFollowRuntime.Pawn(choice.PawnId, RHAH_LongFollowRuntime.Now);
                    string label = choice.PawnId > 0 ? choice.Key.Translate(target?.LabelShort ?? "-").ToString() : choice.Key.Translate(record.FoodCount, record.MedicineCount).ToString();
                    DiaOption option = new DiaOption(label);
                    if (choice.DisabledKey != null) option.Disable(choice.DisabledKey.Translate());
                    else
                    {
                        option.action = () =>
                        {
                            if (!RHAH_LongFollowRuntime.Choose(chainId, phase, captured.Action, captured.PawnId))
                            {
                                Messages.Message("RHAH_Long_Stale".Translate(), MessageTypeDefOf.RejectInput);
                            }
                        };
                    }
                    yield return option;
                }
                if (lookTargets.IsValid()) yield return Option_JumpToLocationAndPostpone;
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref chainId, "chainId", 0);
            Scribe_Values.Look(ref phase, "phase", 0);
        }
    }
}
