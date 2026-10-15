using System;
using System.Reflection;
using System.Runtime.Serialization;
using Verse;
using Xunit;

namespace LeadYourPet.Tests
{
    [CollectionDefinition("Game state", DisableParallelization = true)]
    public class GameStateCollection
    {
    }

    // Behavioral tests for GodHandsCompat that never load the game or the GodHands assembly.
    // RecordGrabbed / RecordReleased are the same entry points the Harmony patches use, so
    // driving them directly is exactly the real path.
    [Collection("Game state")]
    public class GodHandsCompatTests : IDisposable
    {
        private sealed class FakeController
        {
        }

        private static Pawn NewPawn(int thingIdNumber)
        {
            Pawn pawn = (Pawn)FormatterServices.GetUninitializedObject(typeof(Pawn));
            SetThingId(pawn, thingIdNumber);
            return pawn;
        }

        private static void SetThingId(Thing thing, int value)
        {
            FieldInfo field = typeof(Thing).GetField("thingIDNumber", BindingFlags.Public | BindingFlags.Instance);
            field.SetValue(thing, value);
        }

        public GodHandsCompatTests()
        {
            GodHandsCompat.Reset();
        }

        public void Dispose() => GodHandsCompat.Reset();

        [Fact]
        public void GrabbedPawnIsReportedAndReleasedPawnIsNot()
        {
            FakeController controller = new FakeController();
            Pawn pawn = NewPawn(1);

            Assert.False(GodHandsCompat.IsGrabbed(pawn));

            GodHandsCompat.RecordGrabbed(pawn, controller);
            Assert.True(GodHandsCompat.IsGrabbed(pawn));

            GodHandsCompat.RecordReleased(controller);
            Assert.False(GodHandsCompat.IsGrabbed(pawn));
        }

        [Fact]
        public void ReleaseOnlyDropsPawnsOwnedByThatController()
        {
            FakeController handA = new FakeController();
            FakeController handB = new FakeController();
            Pawn pawnA = NewPawn(1);
            Pawn pawnB = NewPawn(2);

            GodHandsCompat.RecordGrabbed(pawnA, handA);
            GodHandsCompat.RecordGrabbed(pawnB, handB);

            GodHandsCompat.RecordReleased(handA);

            Assert.False(GodHandsCompat.IsGrabbed(pawnA));
            Assert.True(GodHandsCompat.IsGrabbed(pawnB));
            Assert.Equal(1, GodHandsCompat.TrackedPawnCount);
        }

        [Fact]
        public void ReleasingUnknownControllerLeavesTrackedPawnsUntouched()
        {
            FakeController hand = new FakeController();
            Pawn pawn = NewPawn(1);
            GodHandsCompat.RecordGrabbed(pawn, hand);

            GodHandsCompat.RecordReleased(new FakeController());
            GodHandsCompat.RecordReleased(null);

            Assert.True(GodHandsCompat.IsGrabbed(pawn));
        }

        [Fact]
        public void ReleasingControllerTwiceIsIdempotent()
        {
            FakeController hand = new FakeController();
            Pawn pawn = NewPawn(1);
            GodHandsCompat.RecordGrabbed(pawn, hand);

            GodHandsCompat.RecordReleased(hand);
            GodHandsCompat.RecordReleased(hand);

            Assert.False(GodHandsCompat.IsGrabbed(pawn));
            Assert.Equal(0, GodHandsCompat.TrackedPawnCount);
        }

        [Fact]
        public void ResetForgetsEveryControllerAndPawn()
        {
            FakeController handA = new FakeController();
            FakeController handB = new FakeController();
            Pawn pawnA = NewPawn(1);
            Pawn pawnB = NewPawn(2);
            GodHandsCompat.RecordGrabbed(pawnA, handA);
            GodHandsCompat.RecordGrabbed(pawnB, handB);

            GodHandsCompat.Reset();

            Assert.False(GodHandsCompat.IsGrabbed(pawnA));
            Assert.False(GodHandsCompat.IsGrabbed(pawnB));
            Assert.Equal(0, GodHandsCompat.TrackedPawnCount);
        }

        [Fact]
        public void TrackingIsPerInstanceNotPerThingId()
        {
            // Thing.GetHashCode returns thingIDNumber and -1 for unregistered things, so many
            // pawns collide. Tracking must never merge two distinct instances.
            FakeController controller = new FakeController();
            Pawn grabbed = NewPawn(7);
            Pawn ungrabbed = NewPawn(7);

            GodHandsCompat.RecordGrabbed(grabbed, controller);

            Assert.True(GodHandsCompat.IsGrabbed(grabbed));
            Assert.False(GodHandsCompat.IsGrabbed(ungrabbed));

            // Releasing the controller must not remove the untouched sibling.
            GodHandsCompat.RecordReleased(controller);
            Assert.False(GodHandsCompat.IsGrabbed(grabbed));
            Assert.False(GodHandsCompat.IsGrabbed(ungrabbed));
        }

        [Fact]
        public void ReGrabbedPawnIsOwnedByTheMostRecentController()
        {
            // A pawn cannot really be held by two hands at once. The last OnGrabbed wins, so
            // releasing the newest owner clears the mark and releasing the stale one is a no-op.
            FakeController first = new FakeController();
            FakeController second = new FakeController();
            Pawn pawn = NewPawn(3);

            GodHandsCompat.RecordGrabbed(pawn, first);
            GodHandsCompat.RecordGrabbed(pawn, second);

            GodHandsCompat.RecordReleased(first);
            Assert.True(GodHandsCompat.IsGrabbed(pawn));

            GodHandsCompat.RecordReleased(second);
            Assert.False(GodHandsCompat.IsGrabbed(pawn));
        }

        [Fact]
        public void NullInputsNeverChangeState()
        {
            GodHandsCompat.RecordGrabbed(null, new FakeController());
            GodHandsCompat.RecordGrabbed(NewPawn(1), null);
            GodHandsCompat.RecordReleased(null);

            Assert.Equal(0, GodHandsCompat.TrackedPawnCount);
            Assert.False(GodHandsCompat.IsGrabbed(null));
        }
    }
}
