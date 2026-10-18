using System;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimCarbon14.Fixture
{
    public sealed class Probe
    {
        public int Clicks;
        public int OtherClicks;
        public bool Checked;
        public string Text = "initial";
        public float Slider = 0.25f;
        public int GetterCalls;
        public int WorkerCalls;
        public string InputEvents = "";
        public Probe Cycle => this;
        public int CountedGetter { get { GetterCalls++; return Clicks; } }
    }
    public static class Fixture
    {
        public static readonly Probe Root = new Probe();
        private static readonly Harmony Harmony = new Harmony("rimcarbon14.test.fixture");
        public static bool WatchEnabled;
        public static bool WatchCallbackEnabled;
        public static int WatchTicks;
        public static int WatchValue;
        private static readonly WatchBase WatchTarget = new WatchInherited();
        public static void WatchReset() { WatchTicks = WatchValue = 0; WatchEnabled = true; WatchCallbackEnabled = false; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WatchCallbackRoot(int depth) { if (depth == 0) WatchLeaf(); else WatchCallback<int>(depth - 1); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WatchCallback<T>(int depth) { WatchCallbackRoot(depth); }
        public static double WatchMeasureTicks(int count)
        {
            if (count < 1 || count > 600) throw new ArgumentOutOfRangeException(nameof(count));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < count; i++) Find.TickManager.DoSingleTick();
            return clock.Elapsed.TotalMilliseconds;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WatchA()
        {
            WatchLeaf(); WatchTarget.Hit();
            if (WatchTicks < 0) WatchNever();
            try { Throwing(); } catch (InvalidOperationException) { WatchValue++; }
            WatchRef(ref WatchValue);
            Recurse(2);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WatchB() { WatchLeaf(); WatchLeaf(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WatchLeaf() { WatchValue++; Thread.SpinWait(2000); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WatchNever() { WatchValue += 100000; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WatchRef(ref int value) { value += 2; }
        public static void OpenWindow() => Find.WindowStack.Add(new ProbeWindow());
        public static bool CrashOnce(string marker)
        {
            if (!Guid.TryParseExact(marker, "N", out _)) throw new ArgumentException("marker must be a GUID in N format.");
            var file = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimCarbon14", "fixture-crash-" + marker);
            if (File.Exists(file)) return false;
            File.WriteAllText(file, "abnormal process exit fixture");
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Recurse(int depth) => depth > 0 ? 1 + Recurse(depth - 1) : 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Throwing() => throw new InvalidOperationException("fixture exception");
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Skipped(int input) => input + 1;
        public static void InstallSkip() => Harmony.Patch(AccessTools.Method(typeof(Fixture), nameof(Skipped)), new HarmonyMethod(typeof(Fixture), nameof(SkipPrefix)));
        public static void RemoveSkip() => Harmony.UnpatchAll(Harmony.Id);
        private static bool SkipPrefix(ref int __result) { __result = 41; return false; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void WorkerHit() => Interlocked.Increment(ref Root.WorkerCalls);
        public static void Workers(int count)
        {
            var threads = new Thread[2];
            for (int i = 0; i < threads.Length; i++) { threads[i] = new Thread(() => { for (int j = 0; j < count; j++) WorkerHit(); }); threads[i].Start(); }
            foreach (var thread in threads) thread.Join();
        }
    }
    public class WatchBase
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public virtual void Hit() { Fixture.WatchValue += 100000; }
    }
    public class WatchDerived : WatchBase
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public override void Hit() { Fixture.WatchLeaf(); }
    }
    public sealed class WatchInherited : WatchDerived { }
    public sealed class WatchComponent : GameComponent
    {
        public WatchComponent(Game game) { }
        public override void GameComponentTick()
        {
            if (!Fixture.WatchEnabled) return;
            Fixture.WatchTicks++; Fixture.WatchA();
            if (Fixture.WatchTicks % 2 == 0) Fixture.WatchB();
            if (Fixture.WatchCallbackEnabled) Fixture.WatchCallbackRoot(1);
        }
    }
    public sealed class ProbeWindow : Window
    {
        private Vector2 scroll;
        public override Vector2 InitialSize => new Vector2(520, 540);
        public ProbeWindow() { doCloseX = true; draggable = true; }
        public override void DoWindowContents(Rect inRect)
        {
            var root = Fixture.Root;
            if (Event.current.isMouse && Event.current.type != EventType.MouseMove)
            {
                root.InputEvents += Event.current.type + " at " + Event.current.mousePosition + " blocked=" + Mouse.IsInputBlockedNow + "\n";
                if (root.InputEvents.Length > 4096) root.InputEvents = root.InputEvents.Substring(root.InputEvents.Length - 4096);
            }
            Widgets.Label(new Rect(0, 0, 400, 30), "RimCarbon14 acceptance fixture");
            if (Widgets.ButtonText(new Rect(0, 40, 200, 30), "Duplicate")) root.Clicks++;
            if (Widgets.ButtonText(new Rect(240, 40, 200, 30), "Duplicate")) root.OtherClicks++;
            Widgets.CheckboxLabeled(new Rect(0, 80, 300, 30), "Fixture checkbox", ref root.Checked);
            root.Text = Widgets.TextField(new Rect(0, 120, 440, 30), root.Text);
            root.Slider = Widgets.HorizontalSlider(new Rect(0, 160, 440, 30), root.Slider, 0f, 1f);
            Widgets.BeginScrollView(new Rect(0, 210, 460, 230), ref scroll, new Rect(0, 0, 440, 1000));
            for (int i = 0; i < 20; i++) Widgets.Label(new Rect(0, i * 45, 400, 30), "Row " + i);
            if (Widgets.ButtonText(new Rect(0, 930, 200, 30), "Bottom action")) root.Clicks++;
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(0, 460, 200, 30), "Disabled action", active: false)) root.OtherClicks++;
        }
    }
}
