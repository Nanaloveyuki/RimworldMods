using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using RimWorld;
using Verse;

namespace OhYouAreSoFast
{
    internal static class SaveLoadTrace
    {
        private const string Prefix = "[OhYouAreSoFast][SaveTrace]";

        [ThreadStatic]
        private static System.Collections.Generic.Stack<ProfilerScope> profilerScopes;

        private static int activeOperationCount;

        internal static bool LightEnabled
        {
            get
            {
                return OhYouAreSoFastMod.Settings != null && OhYouAreSoFastMod.Settings.saveLoadTrace;
            }
        }

        internal static bool FullEnabled
        {
            get
            {
                return OhYouAreSoFastMod.Settings != null && OhYouAreSoFastMod.Settings.saveLoadFullTrace;
            }
        }

        internal static bool Enabled
        {
            get
            {
                return LightEnabled || FullEnabled;
            }
        }

        internal sealed class Scope
        {
            internal readonly string Name;
            internal readonly long StartedAt;

            internal Scope(string name)
            {
                Name = name;
                StartedAt = Stopwatch.GetTimestamp();
            }
        }

        private sealed class ProfilerScope
        {
            internal readonly string Label;
            internal readonly long StartedAt;

            internal ProfilerScope(string label)
            {
                Label = label;
                StartedAt = Stopwatch.GetTimestamp();
            }
        }

        internal static void LogEnabledState()
        {
            if (Enabled)
            {
                Write("Tracing is enabled level=" + (FullEnabled ? "full" : "light") + ".");
            }
        }

        internal static Scope Begin(string name, string detail = null)
        {
            if (!Enabled)
            {
                return null;
            }

            Interlocked.Increment(ref activeOperationCount);
            Write("BEGIN " + name + FormatDetail(detail));
            return new Scope(name);
        }

        internal static void End(Scope scope, string detail = null)
        {
            if (scope == null)
            {
                return;
            }

            double elapsedMilliseconds = ElapsedMilliseconds(scope.StartedAt);
            Write("END " + scope.Name + " durationMs=" + FormatNumber(elapsedMilliseconds) + FormatDetail(detail));
            Interlocked.Decrement(ref activeOperationCount);
        }

        internal static void Write(string message)
        {
            if (!Enabled)
            {
                return;
            }

            Log.Message(Prefix + " thread=" + Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture) + " " + Clean(message));
        }

        internal static void ProfilerBegin(string label)
        {
            if (Volatile.Read(ref activeOperationCount) <= 0 || !FullEnabled)
            {
                return;
            }

            if (profilerScopes == null)
            {
                profilerScopes = new System.Collections.Generic.Stack<ProfilerScope>();
            }

            string safeLabel = string.IsNullOrEmpty(label) ? "<unnamed>" : label;
            profilerScopes.Push(new ProfilerScope(safeLabel));
            Write("PROFILE BEGIN depth=" + profilerScopes.Count.ToString(CultureInfo.InvariantCulture) + " label=" + safeLabel);
        }

        internal static void ProfilerEnd()
        {
            if (profilerScopes == null || profilerScopes.Count == 0)
            {
                return;
            }

            ProfilerScope scope = profilerScopes.Pop();
            if (FullEnabled)
            {
                Write("PROFILE END depth=" + profilerScopes.Count.ToString(CultureInfo.InvariantCulture) + " label=" + scope.Label + " durationMs=" + FormatNumber(ElapsedMilliseconds(scope.StartedAt)));
            }

            if (profilerScopes.Count == 0)
            {
                profilerScopes = null;
            }
        }

        internal static string DescribeFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "file=<empty>";
            }

            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                return "file=" + fileInfo.Name + " bytes=" + (fileInfo.Exists ? fileInfo.Length.ToString(CultureInfo.InvariantCulture) : "missing");
            }
            catch (Exception ex)
            {
                return "file=" + Path.GetFileName(filePath) + " metadataError=" + ex.GetType().Name;
            }
        }

        internal static string DescribeMap(Map map)
        {
            if (map == null)
            {
                return "map=null";
            }

            return "mapHash=" + RuntimeHelpers.GetHashCode(map).ToString(CultureInfo.InvariantCulture) + " uniqueId=" + map.uniqueID.ToString(CultureInfo.InvariantCulture);
        }

        internal static string DescribeWorld(RimWorld.Planet.World world)
        {
            if (world == null)
            {
                return "world=null";
            }

            return "worldHash=" + RuntimeHelpers.GetHashCode(world).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDetail(string detail)
        {
            return string.IsNullOrEmpty(detail) ? string.Empty : " " + detail;
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }

        private static double ElapsedMilliseconds(long startedAt)
        {
            return (Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency;
        }

        private static string Clean(string value)
        {
            return (value ?? "<null>").Replace('\r', ' ').Replace('\n', ' ');
        }
    }

    [HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.Start), new[] { typeof(string) })]
    internal static class Patch_DeepProfiler_Start
    {
        private static void Prefix(string label)
        {
            SaveLoadTrace.ProfilerBegin(label);
        }
    }

    [HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.End))]
    internal static class Patch_DeepProfiler_End
    {
        private static void Postfix()
        {
            SaveLoadTrace.ProfilerEnd();
        }
    }

    [HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.SetCurrentEventText), new[] { typeof(string) })]
    internal static class Patch_LongEventHandler_SetCurrentEventText
    {
        private static void Prefix(string newText)
        {
            if (SaveLoadTrace.Enabled && !string.IsNullOrEmpty(newText))
            {
                SaveLoadTrace.Write("EVENT_TEXT text=" + newText);
            }
        }
    }

    [HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.LoadAllPlayData), new[] { typeof(bool) })]
    internal static class Patch_PlayDataLoader_LoadAllPlayData
    {
        private static void Prefix(bool recovering, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("PlayDataLoader.LoadAllPlayData", "recovering=" + recovering);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(PlayDataLoader), "DoPlayLoad")]
    internal static class Patch_PlayDataLoader_DoPlayLoad
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("PlayDataLoader.DoPlayLoad");
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadAllActiveMods), new[] { typeof(bool) })]
    internal static class Patch_LoadedModManager_LoadAllActiveMods
    {
        private static void Prefix(bool hotReload, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("LoadedModManager.LoadAllActiveMods", "hotReload=" + hotReload);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ScribeLoader), nameof(ScribeLoader.InitLoading), new[] { typeof(string) })]
    internal static class Patch_ScribeLoader_InitLoading
    {
        private static void Prefix(string filePath, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("ScribeLoader.InitLoading", SaveLoadTrace.DescribeFile(filePath));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ScribeLoader), nameof(ScribeLoader.InitLoadingMetaHeaderOnly), new[] { typeof(string) })]
    internal static class Patch_ScribeLoader_InitLoadingMetaHeaderOnly
    {
        private static void Prefix(string filePath, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("ScribeLoader.InitLoadingMetaHeaderOnly", SaveLoadTrace.DescribeFile(filePath));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ScribeLoader), nameof(ScribeLoader.FinalizeLoading))]
    internal static class Patch_ScribeLoader_FinalizeLoading
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("ScribeLoader.FinalizeLoading", "mode=" + Scribe.mode);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(CrossRefHandler), nameof(CrossRefHandler.ResolveAllCrossReferences))]
    internal static class Patch_CrossRefHandler_ResolveAllCrossReferences
    {
        private static void Prefix(CrossRefHandler __instance, out SaveLoadTrace.Scope __state)
        {
            int count = __instance.crossReferencingExposables == null ? 0 : __instance.crossReferencingExposables.Count;
            __state = SaveLoadTrace.Begin("CrossRefHandler.ResolveAllCrossReferences", "registered=" + count.ToString(CultureInfo.InvariantCulture));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(PostLoadIniter), nameof(PostLoadIniter.DoAllPostLoadInits))]
    internal static class Patch_PostLoadIniter_DoAllPostLoadInits
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("PostLoadIniter.DoAllPostLoadInits", "mode=" + Scribe.mode);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.ExposeData))]
    internal static class Patch_Game_ExposeData
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("Game.ExposeData", "mode=" + Scribe.mode);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Game), "ExposeSmallComponents")]
    internal static class Patch_Game_ExposeSmallComponents
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("Game.ExposeSmallComponents", "mode=" + Scribe.mode);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    internal static class Patch_Game_LoadGame
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("Game.LoadGame", "mode=" + Scribe.mode);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    internal static class Patch_Game_FinalizeInit
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("Game.FinalizeInit");
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.World), nameof(RimWorld.Planet.World.ExposeData))]
    internal static class Patch_World_ExposeData
    {
        private static void Prefix(RimWorld.Planet.World __instance, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("World.ExposeData", "mode=" + Scribe.mode + " " + SaveLoadTrace.DescribeWorld(__instance));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.World), nameof(RimWorld.Planet.World.FinalizeInit), new[] { typeof(bool) })]
    internal static class Patch_World_FinalizeInit
    {
        private static void Prefix(RimWorld.Planet.World __instance, bool fromLoad, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("World.FinalizeInit", "fromLoad=" + fromLoad + " " + SaveLoadTrace.DescribeWorld(__instance));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.ExposeData))]
    internal static class Patch_Map_ExposeData
    {
        private static void Prefix(Map __instance, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("Map.ExposeData", "mode=" + Scribe.mode + " " + SaveLoadTrace.DescribeMap(__instance));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(MapFileCompressor), nameof(MapFileCompressor.ExposeData))]
    internal static class Patch_MapFileCompressor_ExposeData
    {
        private static void Prefix(MapFileCompressor __instance, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("MapFileCompressor.ExposeData", "mode=" + Scribe.mode);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state, "mode=" + Scribe.mode);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeLoading))]
    internal static class Patch_Map_FinalizeLoading
    {
        private static void Prefix(Map __instance, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("Map.FinalizeLoading", SaveLoadTrace.DescribeMap(__instance));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    internal static class Patch_Map_FinalizeInit
    {
        private static void Prefix(Map __instance, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("Map.FinalizeInit", SaveLoadTrace.DescribeMap(__instance));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(MapComponentUtility), nameof(MapComponentUtility.FinalizeInit), new[] { typeof(Map) })]
    internal static class Patch_MapComponentUtility_FinalizeInit
    {
        private static void Prefix(Map map, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("MapComponentUtility.FinalizeInit", SaveLoadTrace.DescribeMap(map));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit))]
    internal static class Patch_GameComponentUtility_FinalizeInit
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("GameComponentUtility.FinalizeInit");
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.LoadedGame))]
    internal static class Patch_GameComponentUtility_LoadedGame
    {
        private static void Prefix(out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("GameComponentUtility.LoadedGame");
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldComponentUtility), nameof(RimWorld.Planet.WorldComponentUtility.FinalizeInit), new[] { typeof(RimWorld.Planet.World), typeof(bool) })]
    internal static class Patch_WorldComponentUtility_FinalizeInit
    {
        private static void Prefix(RimWorld.Planet.World world, bool fromLoad, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("WorldComponentUtility.FinalizeInit", "fromLoad=" + fromLoad + " " + SaveLoadTrace.DescribeWorld(world));
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(BackCompatibility), nameof(BackCompatibility.PostLoadSavegame), new[] { typeof(string) })]
    internal static class Patch_BackCompatibility_PostLoadSavegame
    {
        private static void Prefix(string loadingVersion, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("BackCompatibility.PostLoadSavegame", "version=" + loadingVersion);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.LoadGame), new[] { typeof(string) })]
    internal static class Patch_GameDataSaveLoader_LoadGame
    {
        private static void Prefix(string saveFileName)
        {
            SaveLoadTrace.Write("REQUEST LoadGame save=" + saveFileName);
        }
    }

    [HarmonyPatch(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.SaveGame), new[] { typeof(string) })]
    internal static class Patch_GameDataSaveLoader_SaveGame
    {
        private static void Prefix(string fileName, out SaveLoadTrace.Scope __state)
        {
            __state = SaveLoadTrace.Begin("GameDataSaveLoader.SaveGame", "save=" + fileName);
        }

        private static Exception Finalizer(SaveLoadTrace.Scope __state, Exception __exception)
        {
            SaveLoadTrace.End(__state);
            return __exception;
        }
    }
}
