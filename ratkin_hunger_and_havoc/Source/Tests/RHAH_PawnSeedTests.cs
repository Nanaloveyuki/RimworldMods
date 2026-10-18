using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HungerAndHavoc.Api;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PawnSeedTests
    {
        [Fact]
        public void ConstructorCopiesCollectionsAndRejectsLaterMutation()
        {
            List<int> children = new List<int> { 11, 22 };
            Dictionary<RHAH_BehaviorGate, bool> gates = new Dictionary<RHAH_BehaviorGate, bool>
            {
                { RHAH_BehaviorGate.Beg, true }
            };

            RHAH_PawnSeed seed = new RHAH_PawnSeed(
                sourceIncidentDisplayId: "I-005",
                spawnBatchId: 3,
                relationshipGroupId: 7,
                role: RHAH_PawnRole.Beggar,
                lifecycle: RHAH_Lifecycle.SeekingFood,
                carriesPlague: true,
                attitudeAtArrival: RHAH_Attitude.Hostile,
                leaveAfterGameTick: 40,
                parentPawnLoadId: 9,
                childPawnLoadIds: children,
                gateOverrides: gates);

            children.Add(33);
            gates[RHAH_BehaviorGate.Beg] = false;
            gates[RHAH_BehaviorGate.Steal] = true;

            Assert.Equal(new[] { 11, 22 }, seed.ChildPawnLoadIds.ToArray());
            Assert.True(seed.GateOverrides[RHAH_BehaviorGate.Beg]);
            Assert.False(seed.GateOverrides.ContainsKey(RHAH_BehaviorGate.Steal));
            Assert.NotNull(new RHAH_PawnSeed().ChildPawnLoadIds);
            Assert.Empty(new RHAH_PawnSeed().ChildPawnLoadIds);
            Assert.NotNull(new RHAH_PawnSeed().GateOverrides);
            Assert.Empty(new RHAH_PawnSeed().GateOverrides);
        }

        [Fact]
        public void CloneCopiesCollectionsIndependently()
        {
            RHAH_PawnSeed original = new RHAH_PawnSeed(
                childPawnLoadIds: new[] { 1, 2 },
                gateOverrides: new Dictionary<RHAH_BehaviorGate, bool>
                {
                    { RHAH_BehaviorGate.Leash, true }
                });
            RHAH_PawnSeed clone = original.Clone();

            Assert.NotSame(original, clone);
            Assert.Equal(original.ChildPawnLoadIds, clone.ChildPawnLoadIds);
            Assert.Equal(original.GateOverrides, clone.GateOverrides);
            Assert.NotSame(original.ChildPawnLoadIds, clone.ChildPawnLoadIds);
            Assert.NotSame(original.GateOverrides, clone.GateOverrides);

            IList<int> cloneChildren = clone.ChildPawnLoadIds as IList<int>;
            if (cloneChildren != null && !cloneChildren.IsReadOnly)
            {
                cloneChildren.Add(99);
                Assert.DoesNotContain(99, original.ChildPawnLoadIds);
            }

            IDictionary<RHAH_BehaviorGate, bool> cloneGates =
                clone.GateOverrides as IDictionary<RHAH_BehaviorGate, bool>;
            if (cloneGates != null && !cloneGates.IsReadOnly)
            {
                cloneGates[RHAH_BehaviorGate.Leash] = false;
                cloneGates[RHAH_BehaviorGate.Beg] = true;
                Assert.True(original.GateOverrides[RHAH_BehaviorGate.Leash]);
                Assert.False(original.GateOverrides.ContainsKey(RHAH_BehaviorGate.Beg));
            }
        }

        [Fact]
        public void PublicSurfaceIsImmutable()
        {
            foreach (PropertyInfo property in typeof(RHAH_PawnSeed).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.True(property.GetMethod != null && property.GetMethod.IsPublic, property.Name);
                Assert.True(property.SetMethod == null || !property.SetMethod.IsPublic, property.Name);
            }

            Assert.Empty(typeof(RHAH_PawnSeed).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
        }
    }
}
