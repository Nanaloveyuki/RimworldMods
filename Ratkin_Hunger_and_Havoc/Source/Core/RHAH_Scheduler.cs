using HungerAndHavoc.Caravan;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Core
{
    internal static class RHAH_Scheduler
    {
        internal static bool QueueDebugIncident(string displayId)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            RHAH_Settings settings = RHAH_Mod.Settings;
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            IIncidentTarget target = ResolveTarget(entry);
            bool targetReady = target != null;
            if (!CanQueueDebug(entry, settings, game != null, targetReady))
            {
                Log.Warning(DebugRejectText(
                    displayId,
                    RHAH_Runtime.AllowsNewContent,
                    entry != null,
                    game != null,
                    targetReady,
                    settings == null || entry == null || settings.IsIncidentEnabled(entry == null ? displayId : entry.DisplayId)));
                return false;
            }

            float catalog = entry.DebugPoints;
            float points = settings == null
                ? catalog
                : settings.IncidentDebugPoints(entry.DisplayId, catalog);
            if (!game.QueueIncident(entry.DisplayId, points, RHAH_IncidentSchedule.TargetId(entry.Target == RHAH_IncidentTarget.Caravan, RHAH_IncidentSchedule.RawId(target))))
            {
                Log.Warning("[RHAH] Debug trigger did not queue " + entry.DisplayId +
                    ". It is already pending or the id is empty. points=" + points.ToString("0.##"));
                return false;
            }

            bool paused = Find.WindowStack != null && Find.WindowStack.WindowsForcePause;
            Log.Message("[RHAH] Debug trigger queued " + entry.DisplayId +
                ". def=" + entry.DefName +
                " points=" + points.ToString("0.##") +
                " target=" + entry.Target +
                " forcePause=" + paused +
                ". A paused menu waits until the next unpaused frame.");
            if (!paused)
            {
                game.TrySpawnPending(0);
            }

            return true;
        }

        internal static bool SpawnDebugIncidentOnMap(string displayId, Map map)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            RHAH_Settings settings = RHAH_Mod.Settings;
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            if (!CanQueueDebug(entry, settings, game != null, map != null))
            {
                return false;
            }

            float points = settings == null ? entry.DebugPoints : settings.IncidentDebugPoints(displayId, entry.DebugPoints);
            return game.SpawnDebugOnMap(entry, points, map);
        }

        internal static bool ShouldDrainOnFrame(bool paused, bool forcePause, int pending)
        {
            return paused && !forcePause && pending > 0;
        }

        internal static string DebugRejectText(
            string displayId,
            bool content,
            bool catalog,
            bool game,
            bool target,
            bool enabled)
        {
            return "[RHAH] Debug trigger rejected " + (displayId ?? "none") +
                ". content=" + content +
                " catalog=" + catalog +
                " game=" + game +
                " target=" + target +
                " enabled=" + enabled;
        }

        internal static bool CanQueueDebug(
            RHAH_IncidentEntry entry,
            RHAH_Settings settings,
            bool gameLoaded,
            bool targetReady)
        {
            return RHAH_Runtime.AllowsNewContent && entry != null && gameLoaded && targetReady &&
                (settings == null || settings.IsIncidentEnabled(entry.DisplayId)) &&
                GameComponent_RHAH_FamineYear.Available(entry.DisplayId);
        }

        static IIncidentTarget ResolveTarget(RHAH_IncidentEntry entry)
        {
            if (entry.Target == RHAH_IncidentTarget.Caravan)
            {
                return CaravanTargetResolver.ResolvePlayerCaravan();
            }

            return RHAH_MapResolver.Resolve();
        }

        internal static bool IsEligible(string displayId, Map map)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            return RHAH_Runtime.AllowsNewContent && entry != null &&
                entry.Target == RHAH_IncidentTarget.Map && map != null;
        }
    }
}
