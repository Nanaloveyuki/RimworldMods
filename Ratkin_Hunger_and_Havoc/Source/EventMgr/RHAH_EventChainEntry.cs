using System;
using System.Globalization;
using HungerAndHavoc.Core;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal static class RHAH_EventChainEntry
    {
        internal static void NoteStarted(string displayId, int mapId, int caravanId, int tick, int batchId)
        {
            if (string.IsNullOrEmpty(displayId) || !RHAH_Runtime.AllowsNewContent)
            {
                return;
            }

            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            if (runtime == null)
            {
                throw new InvalidOperationException("ODF RHAH event chains are not ready.");
            }

            int site = mapId >= 0 ? mapId : caravanId > 0 ? -caravanId : 0;
            bool longChain = RHAH_EventFollowRules.OpensLong(displayId, RHAH_Mod.Settings != null && RHAH_Mod.Settings.IncidentUsesLongChain(displayId));
            int deadline = longChain ? RHAH_EventFollowRules.LongDeadline(displayId, tick) : -1;
            int id = runtime.Start(RHAH_EventChainProvider.ProviderKey, displayId, site, RHAH_EventChainMigration.SiteTile(site), tick,
                longChain ? -1 : deadline, RHAH_EventFollowRules.Payload(displayId, batchId, site), longChain ? 3 : 0, tick == int.MaxValue ? -1 : tick + 1);
            if (id > 0 && longChain)
            {
                RHAH_LongFollowRuntime.RegisterChain(id, displayId, batchId, site, deadline);
                runtime.SetStage(RHAH_EventChainProvider.ProviderKey, id, 3, -1,
                    RHAH_EventFollowRules.Payload(displayId, batchId, site), RHAH_EventChainMigration.NextLongTick(tick, id));
            }
        }

        internal static int QueueArrival(Map map, int dueTick)
        {
            MapComponent_RHAH_Map component = map?.GetComponent<MapComponent_RHAH_Map>();
            if (component == null || dueTick < 0)
            {
                return 0;
            }

            if (!UnityData.IsInMainThread)
            {
                LongEventHandler.ExecuteWhenFinished(() => QueueArrival(map, dueTick));
                return -1;
            }

            EventChainRuntime runtime = Current.Game?.GetComponent<Oxygendsl.Mod.GameComponent_Oxygendsl>()?.EventChains;
            if (runtime == null)
            {
                GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
                if (game != null && game.EventChainMigrationStatus == "" && !game.EventChainsLoaded)
                {
                    // 新地图生成时 ODF 还未 Activate，由 Preparing 一次性导入
                    component.LegacyPredationPendingTick = dueTick;
                    return -1;
                }

                throw new InvalidOperationException("ODF predation arrival queue is not initialized.");
            }

            string payload = map.uniqueID.ToString(CultureInfo.InvariantCulture);
            EventChainInstance existing = component.PredationArrivalInstanceId > 0
                ? runtime.Find(RHAH_EventChainProvider.ProviderKey, component.PredationArrivalInstanceId) : null;
            if (existing != null && existing.End == EventChainEnd.None)
            {
                if (!string.IsNullOrEmpty(existing.PauseReason))
                {
                    throw new InvalidOperationException("Pending predator arrival is paused: " + existing.PauseReason);
                }

                for (int i = 0; i < existing.Receipts.Count; i++)
                {
                    string status = existing.Receipts[i].Status;
                    if (status == "pending" || status == "failed")
                    {
                        throw new InvalidOperationException("Pending predator arrival has an unresolved action receipt.");
                    }
                }

                return runtime.SetStage(RHAH_EventChainProvider.ProviderKey, existing.Id, 0, -1, payload, dueTick) ? existing.Id : 0;
            }

            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            int id = runtime.Start(RHAH_EventChainProvider.ProviderKey, RHAH_EventChainProvider.ArrivalId, map.uniqueID, map.Tile.tileId,
                tick, -1, payload, 0, dueTick);
            component.PredationArrivalInstanceId = id;
            return id;
        }
    }
}
