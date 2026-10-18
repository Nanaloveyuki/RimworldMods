using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Core
{
    internal static class RHAH_Runtime
    {
        public const string PackageId = "nanaloveyuki.ratkin.hungerandhavoc";
        public const string HarmonyId = PackageId;
        public const string DefPrefix = "RHAH_";

        public static bool AllowsNewContent
        {
            get
            {
                if (RHAH_Mod.Settings != null && !RHAH_Mod.Settings.enableNewContent)
                {
                    return false;
                }

                GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
                if (game != null && (game.NewContentDisabled || game.EventChainMigrationStatus.StartsWith("failed:", System.StringComparison.Ordinal)))
                {
                    return false;
                }

                if (!UnityData.IsInMainThread)
                {
                    return true;
                }

                Oxygendsl.Core.Events.EventChainRuntime runtime = Current.Game?.GetComponent<Oxygendsl.Mod.GameComponent_Oxygendsl>()?.EventChains;
                Oxygendsl.Core.Events.EventChainProviderState state = runtime?.GetProviderState(EventMgr.RHAH_EventChainProvider.ProviderKey);
                return state != null ? state.Mode == Oxygendsl.Core.Events.EventChainMode.Active :
                    game == null || (!game.EventChainsLoaded && Current.ProgramState != ProgramState.Playing);
            }
        }

        internal static int NextBatchId(Map map, int offset = 0)
        {
            int batch = System.Math.Max(1, Find.TickManager.TicksGame + offset);
            // 暂停窗口内连续刷新也必须使用不同批次
            while (IsBatchActive(map, batch))
            {
                batch++;
            }

            return batch;
        }

        internal static bool IsBatchActive(Map map, int batchId)
        {
            GameComponent_RHAH_Game component = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            return component != null && ContainsBatch(component.ActiveGenerationBatches, BatchKey(map, batchId));
        }

        internal static void RegisterBatch(Map map, int batchId)
        {
            GameComponent_RHAH_Game component = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            if (component != null && map != null)
            {
                component.RegisterBatch(BatchKey(map, batchId));
            }
        }

        static string BatchKey(Map map, int batchId)
        {
            return map == null ? string.Empty : map.uniqueID + ":" + batchId;
        }

        static bool ContainsBatch(IReadOnlyList<string> batches, string key)
        {
            for (int i = 0; i < batches.Count; i++)
            {
                if (batches[i] == key)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
