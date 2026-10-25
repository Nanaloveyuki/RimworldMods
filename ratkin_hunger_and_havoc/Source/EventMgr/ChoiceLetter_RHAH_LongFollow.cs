using System.Collections.Generic;
using HungerAndHavoc.Core;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    public sealed class ChoiceLetter_RHAH_LongFollow : ChoiceLetter
    {
        internal int chainId;
        internal int phase;
        public override bool CanDismissWithRightClick => !Pending(Current.Game?.GetComponent<GameComponent_RHAH_Game>());

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                RHAH_LongFollowRecord record = RHAH_LongFollowRuntime.FindRecord(chainId);
                if (ArchivedOnly || !Pending(Current.Game?.GetComponent<GameComponent_RHAH_Game>()))
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
                            if (ArchivedOnly)
                            {
                                option.dialog?.Close();
                                return;
                            }
                            if (RHAH_LongFollowRuntime.Choose(chainId, phase, captured.Action, captured.PawnId))
                            {
                                option.dialog?.Close();
                            }
                            else
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

        internal bool Pending(GameComponent_RHAH_Game game)
        {
            List<RHAH_LongFollowRecord> records = game?.LongFollows;
            for (int i = 0; records != null && i < records.Count; i++)
            {
                RHAH_LongFollowRecord record = records[i];
                if (record.ChainId != chainId) continue;
                if (record.Completed || record.Phase != phase || !record.LetterOpen ||
                    RHAH_EventFollowRuntime.FindMap(record.MapId) == null) return false;
                EventChainRuntime runtime = OxygenEventChainHost.Runtime;
                if (runtime == null) return true;
                EventChainInstance instance = runtime.Find(RHAH_EventChainProvider.ProviderKey, chainId);
                return instance == null || instance.End == EventChainEnd.None;
            }
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref chainId, "chainId", 0);
            Scribe_Values.Look(ref phase, "phase", 0);
        }
    }
}
