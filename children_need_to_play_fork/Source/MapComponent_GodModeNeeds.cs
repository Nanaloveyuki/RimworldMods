using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Enforces god-mode need toggles: when active, continuously maxes out
    /// Learning and/or Play needs for all children on the map.
    /// Automatically skips non-children (they don't have these needs).
    /// </summary>
    public class MapComponent_GodModeNeeds : MapComponent
    {
        public MapComponent_GodModeNeeds(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return;

            if (!map.IsPlayerHome) return;
            if (!TPE_DebugActions.GodModeMaxLearning && !TPE_DebugActions.GodModeMaxPlay) return;

            // Only run every 60 ticks (~1 second) to avoid spamming
            if (Find.TickManager.TicksGame % 60 != 0) return;

            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (!pawn.DevelopmentalStage.Child()) continue;

                if (TPE_DebugActions.GodModeMaxLearning && pawn.needs?.learning != null)
                {
                    pawn.needs.learning.CurLevel = 1f;
                }

                if (TPE_DebugActions.GodModeMaxPlay)
                {
                    Need? playNeed = pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay);
                    if (playNeed != null)
                    {
                        playNeed.CurLevel = 1f;
                    }
                }
            }
        }
    }
}
