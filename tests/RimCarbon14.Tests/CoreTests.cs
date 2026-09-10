using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using Xunit;

namespace RimCarbon14.Tests
{
    public sealed class CoreTests
    {
        private static CallRequest Request(DateTime now, string id = "a") => new CallRequest { RequestId = id, IssuedAtUtc = now, Tool = "test", TimeoutMs = 1000 };
        [Fact] public void RequestIdDeduplicatesAndConflictsAreRejected()
        {
            var now = DateTime.UtcNow; var ops = new Operations(new EventBuffer(), clock: () => now); int executions = 0;
            var r = Request(now); ops.Submit(r, _ => { executions++; return new JValue(true); });
            ops.Submit(r, _ => throw new Exception("must not run")); ops.Pump(100);
            Assert.Equal(1, executions); Assert.Equal("completed", (string?)ops.Get("a")["state"]);
            r.Arguments["other"] = true;
            Assert.Equal("request_conflict", Assert.Throws<BridgeException>(() => ops.Submit(r, _ => null)).Code);
        }
        [Fact] public void TimedOutQueuedWorkNeverExecutes()
        {
            var now = DateTime.UtcNow; var ops = new Operations(new EventBuffer(), clock: () => now); int executions = 0;
            ops.Submit(Request(now), _ => { executions++; return new JValue(true); }); now = now.AddSeconds(2); ops.Pump(100);
            Assert.Equal(0, executions); Assert.Equal("timeout", (string?)ops.Get("a").SelectToken("error.code"));
        }
        [Fact] public void CancellationCleansUpStartedWorkWithoutClaimingRollback()
        {
            var now = DateTime.UtcNow; var ops = new Operations(new EventBuffer()); bool cleaned = false;
            ops.Submit(Request(now), op => { op.Cleanup = () => cleaned = true; return null; });
            ops.Pump(100); ops.Cancel("a"); ops.Pump(100);
            Assert.True(cleaned); Assert.True((bool)ops.Get("a")["started"]!); Assert.Equal("cancelled", (string?)ops.Get("a")["state"]);
        }
        [Fact] public void OldRequestsCannotReplayAfterRetentionExpires()
        {
            var now = DateTime.UtcNow; var ops = new Operations(new EventBuffer(), clock: () => now); var old = Request(now);
            ops.Submit(old, _ => new JValue(true)); ops.Pump(100); now = now.AddMinutes(6);
            ops.Submit(Request(now, "b"), _ => new JValue(true));
            Assert.Equal("request_expired", Assert.Throws<BridgeException>(() => ops.Submit(old, _ => new JValue(true))).Code);
        }
        [Fact] public void QueueCapacityIsBounded()
        {
            var now = DateTime.UtcNow; var ops = new Operations(new EventBuffer(), capacity: 1);
            ops.Submit(Request(now), _ => null);
            Assert.Equal("queue_full", Assert.Throws<BridgeException>(() => ops.Submit(Request(now, "b"), _ => null)).Code);
        }
        [Fact] public void TimestampCannotBeImplicitlyRefreshedWhenReplayingJson()
        {
            var ops = new Operations(new EventBuffer());
            Assert.Equal("request_expired", Assert.Throws<BridgeException>(() => ops.Submit(new CallRequest { RequestId = "missing-time", Tool = "test" }, _ => new JValue(true))).Code);
        }
        [Fact] public void PublishedArgumentContractRejectsUnknownAndWrongTypes()
        {
            var spec = new ToolSpec { InputSchema = JObject.Parse("{\"properties\":{\"ticks\":{\"type\":\"integer\"}},\"required\":[\"ticks\"]}") };
            spec.Validate(JObject.Parse("{\"ticks\":2}"));
            Assert.Throws<BridgeException>(() => spec.Validate(new JObject()));
            Assert.Throws<BridgeException>(() => spec.Validate(JObject.Parse("{\"ticks\":\"2\"}")));
            Assert.Throws<BridgeException>(() => spec.Validate(JObject.Parse("{\"ticks\":2,\"typo\":true}")));
        }
        [Fact] public void BufferReportsLossAndPaginates()
        {
            var events = new EventBuffer(2); events.Add("x", 1); events.Add("x", 2); events.Add("x", 3);
            var page = events.Read(0, 1); Assert.Equal(1, (int)page["lost"]!); Assert.Equal(2, (int)page["nextCursor"]!);
            Assert.Equal(3, (int)events.Read(2, 1)["nextCursor"]!);
        }
        [Fact] public void HandlesExpireAcrossSessionsAndPreserveIdentity()
        {
            var handles = new ObjectHandles(1); var obj = new object(); var id = handles.Add(obj);
            Assert.Equal(id, handles.Add(obj)); Assert.Throws<BridgeException>(() => handles.Add(new object()));
            handles.Reset(); Assert.Equal("stale_handle", Assert.Throws<BridgeException>(() => handles.Get(id)).Code);
        }
        public sealed class Fixture
        {
            private int hidden = 7;
            public int GetterCalls;
            public int Value { get { GetterCalls++; return hidden; } }
            public int Sum(int a, ref int b, out int c) { b += a; c = b * 2; return c; }
            public void Throw() => throw new InvalidOperationException("fixture failure");
        }
        [Fact] public void BrowsingNeverRunsGettersAndPrivateFieldsAreReadable()
        {
            var handles = new ObjectHandles(); var target = new Fixture(); var args = new JObject { ["handle"] = handles.Add(target), ["member"] = "hidden" };
            var reflection = new ReflectionTools(handles); reflection.Members(args); Assert.Equal(0, target.GetterCalls);
            Assert.Equal(7, (int)reflection.Read(args, false)); args["member"] = "Value";
            Assert.Throws<BridgeException>(() => reflection.Read(args, false)); Assert.Equal(0, target.GetterCalls);
            Assert.Equal(7, (int)reflection.Read(args, true)); Assert.Equal(1, target.GetterCalls);
        }
        [Fact] public void InvokeReturnsRefOutAndPreservesTargetErrors()
        {
            var handles = new ObjectHandles(); var reflection = new ReflectionTools(handles); var target = new Fixture(); var id = handles.Add(target);
            var result = reflection.Invoke(new JObject { ["handle"] = id, ["methodId"] = ReflectionTools.MethodId(typeof(Fixture).GetMethod("Sum")!), ["arguments"] = new JArray(2, 3, JValue.CreateNull()) });
            Assert.Equal(10, (int)result["result"]!); Assert.Equal(5, (int)result["byRef"]![0]!["value"]!);
            var error = Assert.Throws<InvalidOperationException>(() => reflection.Invoke(new JObject { ["handle"] = id, ["methodId"] = ReflectionTools.MethodId(typeof(Fixture).GetMethod("Throw")!) }));
            Assert.Equal("fixture failure", error.Message);
        }
        [Fact] public void CyclicObjectsAreHandlesNotRecursiveJson()
        {
            var handles = new ObjectHandles(); var array = new object[1]; array[0] = array;
            var id = handles.Add(array); var page = new ReflectionTools(handles).Items(new JObject { ["handle"] = id });
            Assert.Equal(id, (string?)page.SelectToken("items[0].value.handle"));
        }
    }
}
