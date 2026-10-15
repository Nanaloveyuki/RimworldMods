using System;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using Xunit;

namespace RimCarbon14.Tests
{
    public sealed class WatchTests
    {
        [Fact] public void SharedWindowIncludesZeroCallTicksAndDoesNotDoubleCountSelf()
        {
            var w = new WatchWindow(3);
            w.BeginTick(100, 0);
            int a = w.Enter("A", -1, 1); int b = w.Enter("B", 1, 2);
            w.Exit(b, 5, false); w.Exit(a, 9, false);
            int c = w.Enter("C", -2, 10); w.Exit(c, 12, false); w.EndTick(20, false);
            w.BeginTick(101, 20); w.EndTick(40, false);
            var r = w.Report(); var nodes = (JArray)r["nodes"]!;
            Assert.Equal(2, (int)r["completedTicks"]!);
            Assert.Equal(4, (double)nodes[0]["inclusiveMsPerTick"]!);
            Assert.Equal(2.5, (double)nodes[0]["selfMsPerTick"]!);
            Assert.Equal(0.2, (double)nodes[0]["tickShare"]!);
            Assert.Equal(0.375, (double)nodes[1]["parentShare"]!);
            Assert.Equal(0.5, (double)nodes[2]["callsPerTick"]!);
        }
        [Fact] public void PathsCallSitesAndRecursionStayDistinct()
        {
            var w = new WatchWindow(4); w.BeginTick(1, 0);
            int a = w.Enter("A", -1, 0); int b = w.Enter("B", 1, 1); int recurse = w.Enter("B", 3, 2);
            w.Exit(recurse, 3, true); w.Exit(b, 4, false);
            b = w.Enter("B", 2, 5); w.Exit(b, 6, false); w.Exit(a, 7, false); w.EndTick(8, false);
            Assert.Equal(4, w.Nodes.Count); Assert.Equal(1, w.Nodes[2].ParentId);
            Assert.Equal(1, w.Nodes[2].Exceptions); Assert.Equal(0, w.Nodes[1].Exceptions);
            Assert.Equal(w.Nodes[1].TotalMs, w.Nodes[1].SelfMs + w.Nodes[2].TotalMs);
        }
        [Fact] public void NoTicksIsUndefinedAndP95AccountsForZeros()
        {
            var w = new WatchWindow(0);
            Assert.Equal(0, (int)w.Report()["completedTicks"]!);
            for (int i = 0; i < 100; i++) { w.BeginTick(i, i * 10); if (i == 0) { int a = w.Enter("A", -1, 1); w.Exit(a, 5, false); } w.EndTick(i * 10 + 10, false); }
            Assert.Equal(0, (double)w.Report()["nodes"]![0]!["p95MsPerTick"]!);
            Assert.Equal(4, (double)w.Report()["nodes"]![0]!["maxMsPerTick"]!);
        }
        [Fact] public void LimitsDiscardWholeIncompleteTickAndDepthLeavesResidualSelf()
        {
            var w = new WatchWindow(0, 1, 1); w.BeginTick(1, 0);
            int a = w.Enter("A", -1, 0); Assert.Equal(0, w.Enter("B", 1, 1)); w.Exit(a, 5, false); w.EndTick(6, false);
            Assert.Equal(5, w.Nodes[0].SelfMs); Assert.Equal(1, w.DepthLimitedCalls);
            w.BeginTick(2, 6); a = w.Enter("A", -1, 7); w.Exit(a, 8, false);
            Assert.Null(w.EndTick(9, false)); Assert.Equal("sample_limit", w.Limit); Assert.Single(w.Ticks);
        }
        [Fact] public void AbortedTicksDoNotPolluteAggregates()
        {
            var w = new WatchWindow(1); w.BeginTick(1, 0); int a = w.Enter("A", -1, 1); w.Exit(a, 2, false); w.AbortTick();
            Assert.Empty(w.Ticks); Assert.Equal(0, w.Nodes[0].Calls);
            w.BeginTick(2, 3); a = w.Enter("A", -1, 4); w.Exit(a, 5, false); w.EndTick(6, false);
            Assert.Equal(1, w.Nodes[0].Calls);
        }
        [Fact] public void PerTickPeakIsTheSumOfCallsNotTheLargestInvocation()
        {
            var w = new WatchWindow(1); w.BeginTick(1, 0);
            int a = w.Enter("A", -1, 1); w.Exit(a, 3, false);
            a = w.Enter("A", -1, 4); w.Exit(a, 7, false); w.EndTick(10, false);
            var node = w.Report()["nodes"]![0]!;
            Assert.Equal(5, (double)node["maxMsPerTick"]!); Assert.Equal(2, (double)node["callsPerTick"]!);
        }
        [Fact] public void NestedTicksAndUnbalancedCallsAreNotSilentlyAccepted()
        {
            var w = new WatchWindow(2); w.BeginTick(1, 0);
            Assert.Throws<InvalidOperationException>(() => w.BeginTick(2, 1));
            int a = w.Enter("A", -1, 2); w.Enter("B", 1, 3); w.Exit(a, 4, false);
            Assert.Null(w.EndTick(5, false)); Assert.Equal("unbalanced_calls", w.Limit); Assert.Empty(w.Ticks);
        }
    }
}
