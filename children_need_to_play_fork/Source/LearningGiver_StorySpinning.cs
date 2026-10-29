using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Learning giver for the Story Spinning learning desire.
    /// Children pick up a plush toy, find a comfortable spot, and tell it a story.
    /// </summary>
    public class LearningGiver_StorySpinning : LearningGiver
    {
        /// <summary>All plush toy defs that can be used for storytelling.</summary>
        static readonly ThingDef[] PlushDefs = new ThingDef[]
        {
            TPE_ThingDefOf.TPE_PlushTeddy,
            TPE_ThingDefOf.TPE_PlushMuffalo,
            TPE_ThingDefOf.TPE_PlushSheep,
            TPE_ThingDefOf.TPE_PlushDino,
            TPE_ThingDefOf.TPE_PlushMonster,
            TPE_ThingDefOf.TPE_PlushFox
        };

        /// <summary>
        /// Returns true if the pawn is a child and any plush toy is available on the map.
        /// </summary>
        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;

            return FindBestPlushToy(pawn) != null;
        }

        /// <summary>
        /// Creates a StorySpinning job: go to the nearest plush toy, pick it up,
        /// find a spot to sit, and tell a story.
        /// </summary>
        public override Job TryGiveJob(Pawn pawn)
        {
            Thing plushToy = FindBestPlushToy(pawn);
            if (plushToy == null)
                return null;

            return JobMaker.MakeJob(def.jobDef, plushToy);
        }

        /// <summary>
        /// Finds the nearest reachable, unforbidden plush toy on the map.
        /// </summary>
        static Thing FindBestPlushToy(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null)
                return null;

            Thing best = null;
            float bestDistSq = float.MaxValue;

            foreach (ThingDef def in PlushDefs)
            {
                foreach (Thing t in map.listerThings.ThingsOfDef(def))
                {
                    if (t.IsForbidden(pawn))
                        continue;
                    if (!pawn.CanReserve(t))
                        continue;

                    float d = pawn.Position.DistanceToSquared(t.Position);
                    if (d < bestDistSq)
                    {
                        bestDistSq = d;
                        best = t;
                    }
                }
            }

            return best;
        }
    }
}
