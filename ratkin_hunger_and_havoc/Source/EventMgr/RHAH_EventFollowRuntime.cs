using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;

namespace HungerAndHavoc.EventMgr
{

    internal static class RHAH_EventFollowRuntime
    {
        static readonly List<Verse.Pawn> batchBuffer = new List<Verse.Pawn>();

        internal static string TryPredator(int mapId, int batchId)
        {
            if (mapId < 0 || Current.Game == null)
            {
                return "no_map";
            }

            Map map = FindMap(mapId);
            if (map == null)
            {
                return "map_unavailable";
            }

            int chance = Core.RHAH_Mod.Settings == null ? RHAH_EventFollowRules.ShortPredatorPercent : Core.RHAH_Mod.Settings.followPredatorPercent;
            float roll = Rand.Value;
            if (!RHAH_EventFollowRules.PredatorSelected(chance, roll))
            {
                return "roll=" + roll.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";selected=false";
            }

            List<Verse.Pawn> prey = Batch(map, batchId);
            if (prey.Count == 0)
            {
                return "roll=" + roll.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";selected=true;no_prey";
            }

            MapComponent_RHAH_Map component = map.GetComponent<MapComponent_RHAH_Map>();
            if (component == null)
            {
                return "map_component_unavailable";
            }

            for (int i = 0; i < prey.Count; i++)
            {
                component.RememberPredationPrey(prey[i]);
            }

            component.PredationSelected = true;
            if (RHAH_EventChainEntry.QueueArrival(map, Find.TickManager.TicksGame + HungerAndHavoc.Incidents.RHAH_PredationRules.ArrivalDelayTicks) == 0)
            {
                throw new System.InvalidOperationException("ODF predation arrival queue is unavailable.");
            }

            return "roll=" + roll.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";selected=true";
        }

        internal static bool LongReady(string displayId, int batchId, int mapId)
        {
            Map map = mapId >= 0 ? FindMap(mapId) : null;
            if (map == null)
            {
                return false;
            }

            List<Verse.Pawn> batch = Batch(map, batchId);
            if (batch.Count == 0 || !Ready(displayId, batch))
            {
                return false;
            }

            return true;
        }


        static bool Ready(string displayId, List<Verse.Pawn> batch)
        {
            float adult = Core.RHAH_Mod.Settings == null ? RHAH_EventFollowRules.AdultAge : Core.RHAH_Mod.Settings.followAdultAge;
            int pending = 0;
            int finished = 0;
            for (int i = 0; i < batch.Count; i++)
            {
                Verse.Pawn pawn = batch[i];
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
                if (comp == null || pawn.Dead || pawn.ageTracker == null || !RHAH_EventFollowRules.IsYoung(comp.State.role))
                {
                    continue;
                }

                if ((displayId == "I-012" && comp.State.lifecycle != RHAH_Lifecycle.Released) ||
                    pawn.ageTracker.AgeBiologicalYearsFloat < adult)
                {
                    pending++;
                }
                else
                {
                    finished++;
                }
            }

            return RHAH_EventFollowRules.BatchOutcomeDone(pending, finished);
        }


        static List<Verse.Pawn> Batch(Map map, int batchId)
        {
            batchBuffer.Clear();
            IReadOnlyList<Verse.Pawn> spawned = map.mapPawns?.AllPawnsSpawned;
            if (spawned == null)
            {
                return batchBuffer;
            }

            for (int i = 0; i < spawned.Count; i++)
            {
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(spawned[i]);
                if (comp != null && comp.State.spawnBatchId == batchId)
                {
                    batchBuffer.Add(spawned[i]);
                }
            }

            return batchBuffer;
        }

        internal static Map FindMap(int mapId)
        {
            if (Current.Game == null)
            {
                return null;
            }

            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return null;
            }

            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i] != null && maps[i].uniqueID == mapId)
                {
                    return maps[i];
                }
            }

            return null;
        }
    }
}
