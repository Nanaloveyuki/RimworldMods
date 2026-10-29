using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Room role worker that identifies rooms containing toys as "Playgrounds".
    /// Any room with at least one toy (detected via <see cref="TPE_ToyUtility.IsAnyToyThing"/>)
    /// qualifies. The score scales with the number of toys so larger play areas
    /// outrank incidental storage rooms.
    /// </summary>
    public class RoomRoleWorker_Playground : RoomRoleWorker
    {
        /// <summary>Base score multiplier per toy in the room.</summary>
        const float ScorePerToy = 8f;

        /// <summary>
        /// Calculates the playground score for a room. Returns 0 if no toys are present.
        /// </summary>
        public override float GetScore(Room room)
        {
            int toyCount = 0;
            List<Thing> things = room.ContainedAndAdjacentThings;

            for (int i = 0; i < things.Count; i++)
            {
                if (TPE_ToyUtility.IsAnyToyThing(things[i]))
                {
                    toyCount++;
                }
            }

            if (toyCount == 0)
                return 0f;

            return toyCount * ScorePerToy;
        }

        /// <summary>
        /// Predicts how placing a new building would affect the playground score.
        /// Used by the room-role tooltip when the player is placing blueprints.
        /// </summary>
        public override float GetScoreDeltaIfBuildingPlaced(Room room, ThingDef buildingDef)
        {
            // Only toys affect the score
            if (TPE_ToyUtility.IsAnyToyDef(buildingDef))
                return ScorePerToy;

            return 0f;
        }
    }
}
