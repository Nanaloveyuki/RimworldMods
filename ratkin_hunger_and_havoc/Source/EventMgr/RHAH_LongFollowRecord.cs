using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    public sealed class RHAH_LongFollowRecord : IExposable
    {
        internal int ChainId;
        internal string DisplayId = "";
        internal int BatchId;
        internal int MapId;
        internal int Phase;
        internal int DueTick = -1;
        internal int DeadlineTick = -1;
        internal int LastTick = -1;
        internal int SubjectId;
        internal int SourceAdultId;
        internal int ResponsibleId;
        internal List<int> ChildIds = new List<int>();
        internal List<int> VisitorIds = new List<int>();
        internal List<int> DepartedIds = new List<int>();
        internal List<int> SelectedIds = new List<int>();
        internal List<int> PlayerWoundIds = new List<int>();
        internal bool Opened;
        internal bool Completed;
        internal bool FoodAlternative;
        internal bool AskedEarly;
        internal bool KeepsakeTaken;
        internal bool KeepsakeDiscarded;
        internal bool MedicalHold;
        internal bool PlayerKilledVisitor;
        internal bool LetterOpen;
        internal bool FollowupSelected;
        internal bool ReturnRequested;
        internal bool ClaimAcknowledged;
        internal bool RevengePending;
        internal int RehomeCount;
        internal int RevisitCount;
        internal int Branch;
        internal int FoodCount = 6;
        internal int MedicineCount = 2;
        internal int NextDependentBatchId;
        internal Thing Keepsake;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ChainId, "chainId", 0);
            Scribe_Values.Look(ref DisplayId, "displayId", "");
            Scribe_Values.Look(ref BatchId, "batchId", 0);
            Scribe_Values.Look(ref MapId, "mapId", 0);
            Scribe_Values.Look(ref Phase, "phase", 0);
            Scribe_Values.Look(ref DueTick, "dueTick", -1);
            Scribe_Values.Look(ref DeadlineTick, "deadlineTick", -1);
            Scribe_Values.Look(ref LastTick, "lastTick", -1);
            Scribe_Values.Look(ref SubjectId, "subjectId", 0);
            Scribe_Values.Look(ref SourceAdultId, "sourceAdultId", 0);
            Scribe_Values.Look(ref ResponsibleId, "responsibleId", 0);
            Scribe_Collections.Look(ref ChildIds, "childIds", LookMode.Value);
            Scribe_Collections.Look(ref VisitorIds, "visitorIds", LookMode.Value);
            Scribe_Collections.Look(ref DepartedIds, "departedIds", LookMode.Value);
            Scribe_Collections.Look(ref SelectedIds, "selectedIds", LookMode.Value);
            Scribe_Collections.Look(ref PlayerWoundIds, "playerWoundIds", LookMode.Value);
            Scribe_Values.Look(ref Opened, "opened", false);
            Scribe_Values.Look(ref Completed, "completed", false);
            Scribe_Values.Look(ref FoodAlternative, "foodAlternative", false);
            Scribe_Values.Look(ref AskedEarly, "askedEarly", false);
            Scribe_Values.Look(ref KeepsakeTaken, "keepsakeTaken", false);
            Scribe_Values.Look(ref KeepsakeDiscarded, "keepsakeDiscarded", false);
            Scribe_Values.Look(ref MedicalHold, "medicalHold", false);
            Scribe_Values.Look(ref PlayerKilledVisitor, "playerKilledVisitor", false);
            Scribe_Values.Look(ref LetterOpen, "letterOpen", false);
            Scribe_Values.Look(ref FollowupSelected, "followupSelected", false);
            Scribe_Values.Look(ref ReturnRequested, "returnRequested", false);
            Scribe_Values.Look(ref ClaimAcknowledged, "claimAcknowledged", false);
            Scribe_Values.Look(ref RevengePending, "revengePending", false);
            Scribe_Values.Look(ref RehomeCount, "rehomeCount", 0);
            Scribe_Values.Look(ref RevisitCount, "revisitCount", 0);
            Scribe_Values.Look(ref Branch, "branch", 0);
            Scribe_Values.Look(ref FoodCount, "foodCount", 6);
            Scribe_Values.Look(ref MedicineCount, "medicineCount", 2);
            Scribe_Values.Look(ref NextDependentBatchId, "nextDependentBatchId", 0);
            Scribe_References.Look(ref Keepsake, "keepsake");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                DisplayId = DisplayId ?? "";
                ChildIds = ChildIds ?? new List<int>();
                VisitorIds = VisitorIds ?? new List<int>();
                DepartedIds = DepartedIds ?? new List<int>();
                SelectedIds = SelectedIds ?? new List<int>();
                PlayerWoundIds = PlayerWoundIds ?? new List<int>();
            }
        }
    }

    internal readonly struct RHAH_LongOption
    {
        internal readonly string Action;
        internal readonly string Key;
        internal readonly int PawnId;
        internal readonly string DisabledKey;

        internal RHAH_LongOption(string action, string key, int pawnId = 0, string disabledKey = null)
        {
            Action = action;
            Key = key;
            PawnId = pawnId;
            DisabledKey = disabledKey;
        }
    }
}
