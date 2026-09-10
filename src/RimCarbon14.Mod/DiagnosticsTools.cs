using System;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using UnityEngine;
using Verse;

namespace RimCarbon14.Mod
{
    public static class DiagnosticsTools
    {
        private static readonly Func<long>? AllocatedBytes = CreateAllocationCounter();
        private static Func<long>? CreateAllocationCounter()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return null;
            var counter = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            var before = counter();
            var probe = new byte[1024];
            var after = counter();
            GC.KeepAlive(probe);
            // Some Unity Mono builds expose this API but always return zero.
            return after > before ? counter : null;
        }
        private sealed class Sample
        {
            public long StartTime; public long LastTime; public long MaxInterval; public long StartAllocation;
            public long StartMemory; public int StartFrame; public int StartTick; public int Gen0; public int LastFrame;
        }
        public static void Register(BridgeHost host)
        {
            host.Add("diagnostics_sample", "Measure real frame/Tick rates, main-thread allocations and GC over a bounded frame interval. Measurements include bridge overhead, not isolated method CPU cost.", "read", false, (_, op) =>
            {
                var frames = (int?)_["frames"] ?? 120;
                if (frames < 2 || frames > 3600) throw new BridgeException("invalid_frames", "frames must be 2..3600.");
                if (op.Context == null)
                {
                    op.Context = new Sample { StartTime = Stopwatch.GetTimestamp(), LastTime = Stopwatch.GetTimestamp(), StartFrame = Time.frameCount, LastFrame = Time.frameCount, StartTick = Current.Game?.tickManager?.TicksGame ?? 0, StartAllocation = AllocatedBytes?.Invoke() ?? 0, StartMemory = GC.GetTotalMemory(false), Gen0 = GC.CollectionCount(0) };
                    return null;
                }
                var sample = (Sample)op.Context;
                if (sample.LastFrame == Time.frameCount) return null;
                var now = Stopwatch.GetTimestamp();
                sample.MaxInterval = Math.Max(sample.MaxInterval, now - sample.LastTime); sample.LastTime = now; sample.LastFrame = Time.frameCount;
                var elapsedFrames = Time.frameCount - sample.StartFrame;
                if (elapsedFrames < frames) return null;
                var seconds = (now - sample.StartTime) / (double)Stopwatch.Frequency;
                var ticks = (Current.Game?.tickManager?.TicksGame ?? 0) - sample.StartTick;
                return Wire.Token(new { elapsedFrames, elapsedTicks = ticks, seconds, fps = elapsedFrames / seconds, tps = ticks / seconds, maxFrameIntervalMs = sample.MaxInterval * 1000d / Stopwatch.Frequency, allocationCounterSupported = AllocatedBytes != null, mainThreadAllocatedBytes = AllocatedBytes == null ? (long?)null : AllocatedBytes() - sample.StartAllocation, managedMemoryDeltaBytes = GC.GetTotalMemory(false) - sample.StartMemory, gen0Collections = GC.CollectionCount(0) - sample.Gen0 });
            }, "frames:integer");
            host.Add("ui_capture", "Enable/disable semantic UI observation for A/B measurements. Patches and HTTP remain loaded; this is not a no-mod baseline.", "write", false, (a, _) =>
            {
                host.Ui.SetCaptureEnabled((bool)a["enabled"]!);
                return new JObject { ["enabled"] = (bool)a["enabled"]! };
            }, "enabled!:boolean");
        }
    }
}
