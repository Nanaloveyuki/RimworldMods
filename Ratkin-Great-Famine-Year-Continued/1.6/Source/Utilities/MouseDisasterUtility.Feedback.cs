using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static string RandomText(string[] keys, params object[] args)
        {
            if (keys == null || keys.Length == 0)
            {
                return string.Empty;
            }

            string key = keys.RandomElement();
            string translated = key.TranslateSimple();
            if (args == null || args.Length == 0)
            {
                return translated;
            }

            return string.Format(translated, args);
        }

        public static void TryThrowText(Map map, Vector3 drawPos, string text, float size = 3f)
        {
            if (!IsFloatingTextEnabled || map == null || text.NullOrEmpty())
            {
                return;
            }

            MoteMaker.ThrowText(drawPos, map, text, size);
        }

        public static bool TryThrowTextThrottled(Map map, Vector3 drawPos, string text, string throttleKey, int minIntervalTicks, float size = 3f)
        {
            if (!IsFloatingTextEnabled || map == null || text.NullOrEmpty())
            {
                return false;
            }

            if (!throttleKey.NullOrEmpty())
            {
                int nowTick = Find.TickManager?.TicksGame ?? 0;
                if (FloatingTextLastTickByKey.TryGetValue(throttleKey, out int lastTick) && nowTick - lastTick < Mathf.Max(1, minIntervalTicks))
                {
                    return false;
                }

                FloatingTextLastTickByKey[throttleKey] = nowTick;
            }

            MoteMaker.ThrowText(drawPos, map, text, size);
            return true;
        }
    }
}
