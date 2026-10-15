using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_InterceptionCrossingTests
    {
        static RHAH_InterceptionCrossingTests()
        {
            FieldInfo binding = typeof(DefOfHelper).GetField("bindingNow", BindingFlags.Static | BindingFlags.NonPublic);
            bool previous = (bool)binding.GetValue(null);
            try
            {
                binding.SetValue(null, true);
                RuntimeHelpers.RunClassConstructor(typeof(DutyDefOf).TypeHandle);
            }
            finally
            {
                binding.SetValue(null, previous);
            }
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(50, false)]
        [InlineData(88, false)]
        [InlineData(89, true)]
        [InlineData(90, true)]
        [InlineData(99, true)]
        public void OnlyMembersAtTheOppositeExitMayLeave(int x, bool mayLeave)
        {
            DutyDef previousTravel = DutyDefOf.TravelOrWait;
            DutyDef previousExit = DutyDefOf.ExitMapNearDutyTarget;
            try
            {
                DutyDefOf.TravelOrWait = new DutyDef();
                DutyDefOf.ExitMapNearDutyTarget = new DutyDef();
                IntVec3 exit = new IntVec3(99, 0, 50);
                Verse.Pawn pawn = Member(new IntVec3(x, 0, 50));
                LordToil toil = Crossing(exit, pawn);

                toil.UpdateAllDuties();

                Assert.Same(mayLeave ? DutyDefOf.ExitMapNearDutyTarget : DutyDefOf.TravelOrWait,
                    pawn.mindState.duty.def);
                Assert.Equal(exit, pawn.mindState.duty.focus.Cell);
            }
            finally
            {
                DutyDefOf.TravelOrWait = previousTravel;
                DutyDefOf.ExitMapNearDutyTarget = previousExit;
            }
        }

        [Fact]
        public void MembersSwitchIndependentlyAsTheyCrossAndDutiesCanBeRebuilt()
        {
            DutyDef previousTravel = DutyDefOf.TravelOrWait;
            DutyDef previousExit = DutyDefOf.ExitMapNearDutyTarget;
            try
            {
                DutyDefOf.TravelOrWait = new DutyDef();
                DutyDefOf.ExitMapNearDutyTarget = new DutyDef();
                IntVec3 exit = new IntVec3(99, 0, 50);
                Verse.Pawn first = Member(new IntVec3(0, 0, 50));
                Verse.Pawn second = Member(new IntVec3(0, 0, 50));
                LordToil toil = Crossing(exit, first, second);
                toil.UpdateAllDuties();
                Assert.Same(DutyDefOf.TravelOrWait, first.mindState.duty.def);

                first.Position = exit;
                toil.UpdateAllDuties();
                Assert.Same(DutyDefOf.ExitMapNearDutyTarget, first.mindState.duty.def);
                Assert.Same(DutyDefOf.TravelOrWait, second.mindState.duty.def);

                second.Position = new IntVec3(90, 0, 50);
                second.mindState.duty = null;
                toil.UpdateAllDuties();
                Assert.Same(DutyDefOf.ExitMapNearDutyTarget, second.mindState.duty.def);
                Assert.Equal(exit, second.mindState.duty.focus.Cell);
            }
            finally
            {
                DutyDefOf.TravelOrWait = previousTravel;
                DutyDefOf.ExitMapNearDutyTarget = previousExit;
            }
        }

        static Verse.Pawn Member(IntVec3 cell)
        {
            Verse.Pawn pawn = new Verse.Pawn();
            pawn.mindState = new Pawn_MindState();
            pawn.Position = cell;
            return pawn;
        }

        static LordToil Crossing(IntVec3 exit, params Verse.Pawn[] pawns)
        {
            Lord lord = (Lord)FormatterServices.GetUninitializedObject(typeof(Lord));
            lord.ownedPawns = new List<Verse.Pawn>(pawns);
            LordToil toil = new LordJob_RHAH_Intercept(exit).CreateGraph().StartingToil;
            toil.lord = lord;
            return toil;
        }
    }
}
