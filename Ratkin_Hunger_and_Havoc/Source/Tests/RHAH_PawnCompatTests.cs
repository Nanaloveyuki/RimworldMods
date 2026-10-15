using System;
using System.IO;
using System.Runtime.CompilerServices;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn.Compat;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PawnCompatTests
    {
        static RHAH_PawnCompatTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        [Fact]
        public void EmptyHooksAbstain()
        {
            RHAH_PawnSnapshot snapshot = DummySnapshot(RHAH_PawnRole.BeggarChild);
            RHAH_LeashCompat leash = new RHAH_LeashCompat();
            RHAH_ToddlerCompat toddler = new RHAH_ToddlerCompat();
            RHAH_PrisonerCompat prisoner = new RHAH_PrisonerCompat();

            Assert.Null(leash.Allows(null, snapshot, RHAH_BehaviorGate.Leash));
            Assert.Null(leash.ShouldReleaseToColony(null, snapshot, RHAH_ReleaseReason.Recruited));
            Assert.Null(toddler.Allows(null, snapshot, RHAH_BehaviorGate.Carry));
            Assert.Null(toddler.ShouldReleaseToColony(null, snapshot, RHAH_ReleaseReason.ModRequest));
            Assert.Null(prisoner.Allows(null, snapshot, RHAH_BehaviorGate.Imprison));
            Assert.Null(prisoner.ShouldReleaseToColony(null, snapshot, RHAH_ReleaseReason.Imprisoned));
        }

        [Fact]
        public void TryQueryLastRegisteredWinsAndSkipsNull()
        {
            RHAH_PawnBehaviors.ResetForTests();
            RHAH_PawnCompat.ResetForTests();
            RHAH_Api.Bind(new RHAH_ApiHost());
            try
            {
                Assert.Null(RHAH_PawnCompat.TryQuery(null, RHAH_BehaviorGate.Leash));

                RHAH_PawnCompat.Register(null, new StubHook { AllowsResult = false });
                RHAH_PawnCompat.Register("test.first", new StubHook { AllowsResult = true });
                RHAH_PawnCompat.Register("test.later", new StubHook { AllowsResult = false });
                RHAH_PawnCompat.Register("test.skip", new StubHook { AllowsResult = null });

                Assert.False(RHAH_PawnCompat.TryQuery(null, RHAH_BehaviorGate.Leash));
            }
            finally
            {
                RHAH_PawnCompat.ResetForTests();
                RHAH_PawnBehaviors.ResetForTests();
                RHAH_Api.Bind(null);
            }
        }

        [Fact]
        public void DenyLeashDoesNotChangeDefaultsUnlessAdapterWired()
        {
            RHAH_PawnBehaviors.ResetForTests();
            RHAH_PawnCompat.ResetForTests();
            RHAH_Api.Bind(new RHAH_ApiHost());
            try
            {
                RHAH_PawnState child = ChildVisitor();
                Assert.True(RHAH_PawnDefaults.Allows(child.ToSnapshot(), RHAH_BehaviorGate.Leash));
                Assert.True(child.Allows(RHAH_BehaviorGate.Leash));
                Assert.False(RHAH_Api.Allows(null, RHAH_BehaviorGate.Leash));

                RHAH_PawnCompat.Register("test.deny.leash", new DenyLeashHook());
                Assert.False(RHAH_PawnCompat.TryQuery(null, RHAH_BehaviorGate.Leash));
                Assert.True(child.Allows(RHAH_BehaviorGate.Leash));
                Assert.Null(RHAH_PawnBehaviors.Query(handler =>
                    handler.Allows(null, child.ToSnapshot(), RHAH_BehaviorGate.Leash)));
                Assert.False(RHAH_Api.Allows(null, RHAH_BehaviorGate.Leash));
            }
            finally
            {
                RHAH_PawnCompat.ResetForTests();
                RHAH_PawnBehaviors.ResetForTests();
                RHAH_Api.Bind(null);
            }
        }

        [Fact]
        public void WiredAdapterDenyLeashWinsAfterRegister()
        {
            RHAH_PawnBehaviors.ResetForTests();
            RHAH_PawnCompat.ResetForTests();
            RHAH_Api.Bind(new RHAH_ApiHost());
            try
            {
                RHAH_PawnState child = ChildVisitor();
                RHAH_PawnCompat.EnsureAdapterRegistered();
                Assert.True(child.Allows(RHAH_BehaviorGate.Leash));
                Assert.Null(RHAH_PawnBehaviors.Query(handler =>
                    handler.Allows(null, child.ToSnapshot(), RHAH_BehaviorGate.Leash)));

                RHAH_PawnCompat.Register("test.deny.leash", new DenyLeashHook());
                Assert.False(child.Allows(RHAH_BehaviorGate.Leash));
                Assert.False(RHAH_PawnBehaviors.Query(handler =>
                    handler.Allows(null, child.ToSnapshot(), RHAH_BehaviorGate.Leash)));
                Assert.False(RHAH_Api.Allows(null, RHAH_BehaviorGate.Leash));
            }
            finally
            {
                RHAH_PawnCompat.ResetForTests();
                RHAH_PawnBehaviors.ResetForTests();
                RHAH_Api.Bind(null);
            }
        }

        [Fact]
        public void StartupRegistersAdapterAndStaysPublic()
        {
            RHAH_PawnBehaviors.ResetForTests();
            RHAH_PawnCompat.ResetForTests();
            try
            {
                string source = File.ReadAllText(CompatPath());
                Assert.Contains("[StaticConstructorOnStartup]", source);
                Assert.Contains("public static class RHAH_PawnCompatStartup", source);
                Assert.Contains("EnsureAdapterRegistered", source);
                Assert.DoesNotContain("Harmony", source);

                Type startup = typeof(RHAH_PawnCompatStartup);
                Assert.True(startup.IsPublic);
                Assert.Equal("HungerAndHavoc.Pawn.Compat", startup.Namespace);
                Assert.True(Attribute.IsDefined(startup, typeof(StaticConstructorOnStartup)));
            }
            finally
            {
                RHAH_PawnCompat.ResetForTests();
                RHAH_PawnBehaviors.ResetForTests();
            }
        }

        [Fact]
        public void ReservedHookTypesStayInternalAndAbstainWhenRegistered()
        {
            RHAH_PawnBehaviors.ResetForTests();
            RHAH_PawnCompat.ResetForTests();
            RHAH_Api.Bind(new RHAH_ApiHost());
            try
            {
                RHAH_PawnCompat.EnsureAdapterRegistered();
                RHAH_PawnCompat.Register("unlimited.lead.your.pet", new RHAH_LeashCompat());
                RHAH_PawnCompat.Register("toddler.mod", new RHAH_ToddlerCompat());
                RHAH_PawnCompat.Register("prisoner.work.expansion", new RHAH_PrisonerCompat());

                RHAH_PawnState child = ChildVisitor();
                Assert.True(child.Allows(RHAH_BehaviorGate.Leash));
                Assert.False(child.Allows(RHAH_BehaviorGate.Carry));
                Assert.True(child.Allows(RHAH_BehaviorGate.Imprison));
                Assert.Null(RHAH_PawnCompat.TryQuery(null, RHAH_BehaviorGate.Leash));
                Assert.False(typeof(RHAH_LeashCompat).IsPublic);
                Assert.False(typeof(RHAH_ToddlerCompat).IsPublic);
                Assert.False(typeof(RHAH_PrisonerCompat).IsPublic);
            }
            finally
            {
                RHAH_PawnCompat.ResetForTests();
                RHAH_PawnBehaviors.ResetForTests();
                RHAH_Api.Bind(null);
            }
        }

        static RHAH_PawnState ChildVisitor()
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.ApplySeed(new RHAH_PawnSeed(
                sourceIncidentDisplayId: "I-001",
                role: RHAH_PawnRole.BeggarChild,
                lifecycle: RHAH_Lifecycle.SeekingFood,
                attitudeAtArrival: RHAH_Attitude.Neutral));
            return state;
        }

        static RHAH_PawnSnapshot DummySnapshot(RHAH_PawnRole role)
        {
            return new RHAH_PawnSnapshot(
                "I-001",
                0,
                0,
                role,
                RHAH_Lifecycle.SeekingFood,
                false,
                -1,
                false,
                RHAH_Attitude.Neutral,
                RHAH_Attitude.Neutral,
                0,
                null);
        }

        static string CompatPath([CallerFilePath] string testFile = null)
        {
            string testsDir = Path.GetDirectoryName(testFile);
            return Path.GetFullPath(Path.Combine(testsDir, "..", "Pawn", "Compat", "RHAH_PawnCompat.cs"));
        }

        sealed class DenyLeashHook : RHAH_PawnCompatHook
        {
            public bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
            {
                if (gate == RHAH_BehaviorGate.Leash)
                {
                    return false;
                }

                return null;
            }

            public bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
            {
                return null;
            }
        }

        sealed class StubHook : RHAH_PawnCompatHook
        {
            public bool? AllowsResult;

            public bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
            {
                return AllowsResult;
            }

            public bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
            {
                return null;
            }
        }
    }
}
