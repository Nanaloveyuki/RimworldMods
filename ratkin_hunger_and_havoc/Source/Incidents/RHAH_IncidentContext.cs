using HungerAndHavoc.Generation;
using HungerAndHavoc.Api;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal sealed class RHAH_IncidentContext
    {
        public string DisplayId { get; set; }
        public int SpawnBatchId { get; set; }
        public int RelationshipGroupId { get; set; }
        public RHAH_PawnRole Role { get; set; }
        public RHAH_Attitude Attitude { get; set; }
        public bool CarriesPlague { get; set; }
        public Map Map { get; set; }
        public IntVec3 SpawnCell { get; set; }
        public bool DropPod { get; set; }
        public int PawnCount { get; set; }
        public float Points { get; set; }
        public RHAH_PawnProfile Profile { get; set; }
    }
}
