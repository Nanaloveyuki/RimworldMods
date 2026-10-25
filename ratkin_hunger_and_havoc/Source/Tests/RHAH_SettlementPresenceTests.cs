using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Pawn;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_SettlementPresenceTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly int previousTick = (int)Field(typeof(RHAH_PawnIndex), "builtTick").GetValue(null);
        readonly Game previousOwner = (Game)Field(typeof(RHAH_PawnIndex), "builtGame").GetValue(null);
        readonly Dictionary<int, Verse.Pawn> previousIndex = new Dictionary<int, Verse.Pawn>(Index);
        readonly NarrativeState state;
        readonly Map home;

        public RHAH_SettlementPresenceTests()
        {
            Current.Game = Uninitialized<Game>();
            Current.Game.tickManager = new TickManager();
            Current.Game.components = new List<GameComponent>();
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            Current.Game.World = Uninitialized<World>();
            Current.Game.World.worldPawns = new WorldPawns();
            Current.Game.World.worldObjects = new WorldObjectsHolder();
            state = new NarrativeState(Current.Game);
            Current.Game.components.Add(state);
            home = Uninitialized<Map>();
            home.uniqueID = 0;
            Find.Maps.Add(home);
            PrimeIndex();
        }

        public void Dispose()
        {
            Index.Clear();
            foreach (KeyValuePair<int, Verse.Pawn> entry in previousIndex)
            {
                Index.Add(entry.Key, entry.Value);
            }
            Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, previousTick);
            Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, previousOwner);
            RHAH_SettlementProgress.Invalidate(state);
            Current.Game = previousGame;
        }

        [Fact]
        public void SuccessfulExitSurvivesResolutionLossAndInvalidatesThePreview()
        {
            Verse.Pawn pawn = Visitor(11);
            SuiyinMember person = new SuiyinMember { LoadId = 11, LastCareTick = 0 };
            SuiyinJournalCase journal = new SuiyinJournalCase { Id = 14, MapId = 0, Delivered = true, People = { person } };
            state.Book.Journals.Add(journal);
            Assert.Equal(RHAH_SettlementBlocker.None, RHAH_SettlementProgress.Read(state, 0)[0].Members[0].Blockers);

            object exit = BeginExit(pawn);
            SetMapIndex(pawn, -1);
            FinishExit(pawn, exit);
            PrimeIndex();
            ObserveJournal(journal, person, 60000);

            Assert.Equal(SuiyinPresence.Left, person.Presence);
            Assert.Equal(-1, person.MissingSince);
            Assert.Equal(-1, person.LastCareTick);
            Assert.Equal(RHAH_SettlementBlocker.Left, RHAH_SettlementProgress.Read(state, 0)[0].Members[0].Blockers);
            Assert.True(state.Book.CloseJournal(journal, 60000));
            Assert.True(journal.Counted);
        }

        [Fact]
        public void LeavingIntentAndAnUnsuccessfulExitDoNotReplaceUnknownWithLeft()
        {
            Verse.Pawn pawn = Visitor(12);
            CompRHAH_Pawn.TryGet(pawn).State.lifecycle = RHAH_Lifecycle.Leaving;
            SuiyinMember person = new SuiyinMember { LoadId = 12 };
            SuiyinJournalCase journal = new SuiyinJournalCase { Id = 14, People = { person } };
            state.Book.Journals.Add(journal);
            object exit = BeginExit(pawn);
            FinishExit(pawn, exit);
            Assert.Equal(SuiyinPresence.Here, person.Presence);

            SetMapIndex(pawn, -1);
            PrimeIndex();
            ObserveJournal(journal, person, 100);
            Assert.Equal(SuiyinPresence.Unknown, person.Presence);
            Assert.Equal(100, person.MissingSince);
            Assert.False(state.Book.CloseJournal(journal, 100 + 100 * GenDate.TicksPerDay));
            Assert.Equal(SuiyinPresence.Missing, person.Presence);
            Assert.False(journal.Counted);
        }

        [Fact]
        public void ExitUpdatesTrackedMotherAndChildrenWithoutReopeningSettledHistory()
        {
            Verse.Pawn pawn = Visitor(13);
            SuiyinMember child = new SuiyinMember { LoadId = 13, Presence = SuiyinPresence.Missing, LastCareTick = 50 };
            SuiyinN004Case entrust = new SuiyinN004Case
            {
                MotherId = 13, MapId = 0, Mother = SuiyinPresence.Missing,
                MissingSince = 1, Children = { child }
            };
            SuiyinN005Case care = new SuiyinN005Case
            {
                MapId = 0, Outcome = SuiyinN005Outcome.Missing, CareClosed = true,
                Children = { new SuiyinMember { LoadId = 13, Presence = SuiyinPresence.Missing } }
            };
            state.Book.N004.Add(entrust);
            state.Book.N005.Add(care);
            object exit = BeginExit(pawn);
            SetMapIndex(pawn, -1);
            FinishExit(pawn, exit);
            PrimeIndex();
            Method(typeof(RHAH_EntrustCare), "ReadMember").Invoke(null, new object[] { child, 0, 60000, false });
            entrust.Mother = SuiyinMember.ReadPresence(null, 0, 60000, entrust.Mother, ref entrust.MissingSince);

            Assert.Equal(SuiyinPresence.Left, entrust.Mother);
            Assert.Equal(SuiyinPresence.Left, child.Presence);
            Assert.Equal(SuiyinPresence.Left, care.Children[0].Presence);
            Assert.True(care.CareClosed);
            Assert.Equal(SuiyinN005Outcome.Missing, care.Outcome);
            Assert.Equal(0, state.Snapshot().Trust);
        }

        [Fact]
        public void MissingLegacyRecordsNeedActualLocationEvidence()
        {
            SuiyinMember person = new SuiyinMember { LoadId = 14, Presence = SuiyinPresence.Missing, MissingSince = 10 };
            person.ObservePresence(null, 0, 60000);
            Assert.Equal(SuiyinPresence.Missing, person.Presence);
            Assert.Equal(10, person.MissingSince);

            Verse.Pawn pawn = Visitor(14);
            Map other = Uninitialized<Map>();
            other.uniqueID = 41;
            Find.Maps.Add(other);
            SetMapIndex(pawn, 1);
            person.ObservePresence(pawn, 0, 60001);
            Assert.Equal(SuiyinPresence.Left, person.Presence);
            Assert.Equal(-1, person.MissingSince);
        }

        [Fact]
        public void CareRequiresTheRecordedHomeAndDoesNotAccumulateForWorldPawns()
        {
            Verse.Pawn pawn = Visitor(15);
            SuiyinMember child = new SuiyinMember { LoadId = 15, LastCareTick = 0 };
            PrimeIndex(pawn);
            MethodInfo observe = Method(typeof(RHAH_EntrustCare), "ReadMember");
            observe.Invoke(null, new object[] { child, 0, 100, false });
            Assert.Equal(SuiyinPresence.Here, child.Presence);

            SetMapIndex(pawn, -1);
            ((HashSet<Verse.Pawn>)Field(typeof(WorldPawns), "pawnsAlive").GetValue(Find.WorldPawns)).Add(pawn);
            observe.Invoke(null, new object[] { child, 0, 200, false });
            child.ObserveSettlement(200, false);
            Assert.Equal(SuiyinPresence.Left, child.Presence);
            Assert.Equal(0, child.CareTicks);
            Assert.Equal(-1, child.LastCareTick);

            Map other = Uninitialized<Map>();
            other.uniqueID = 41;
            Find.Maps.Add(other);
            SetMapIndex(pawn, 1);
            observe.Invoke(null, new object[] { child, 0, 300, false });
            Assert.Equal(SuiyinPresence.Left, child.Presence);
            SetMapIndex(pawn, 0);
            observe.Invoke(null, new object[] { child, 0, 400, false });
            Assert.Equal(SuiyinPresence.Here, child.Presence);
        }

        [Theory]
        [InlineData(GuestStatus.Prisoner)]
        [InlineData(GuestStatus.Slave)]
        public void CustodyExitDoesNotBecomeConfirmedSuccessfulDeparture(GuestStatus custody)
        {
            Verse.Pawn pawn = Visitor(16);
            pawn.guest = new Pawn_GuestTracker(pawn);
            Field(typeof(Pawn_GuestTracker), "guestStatusInt").SetValue(pawn.guest, custody);
            SuiyinMember person = new SuiyinMember { LoadId = 16 };
            SuiyinJournalCase journal = new SuiyinJournalCase { Id = 14, People = { person } };
            state.Book.Journals.Add(journal);
            object exit = BeginExit(pawn);
            SetMapIndex(pawn, -1);
            FinishExit(pawn, exit);
            PrimeIndex();
            ObserveJournal(journal, person, 100);
            Assert.Equal(SuiyinPresence.Unknown, person.Presence);
        }

        [Fact]
        public void ACarriedChildKeepsItsDepartureAfterTheOriginalExitDetachesIt()
        {
            Verse.Pawn mother = Visitor(19);
            Verse.Pawn child = Visitor(20);
            SetMapIndex(child, -1);
            mother.carryTracker = new Pawn_CarryTracker(mother);
            mother.carryTracker.innerContainer.InnerListForReading.Add(child);
            child.holdingOwner = mother.carryTracker.innerContainer;
            SuiyinMember person = new SuiyinMember { LoadId = 20, Child = true };
            SuiyinN004Case entrust = new SuiyinN004Case { MapId = 0, MotherId = 19, Children = { person } };
            state.Book.N004.Add(entrust);

            object exit = BeginExit(mother);
            mother.carryTracker.innerContainer.InnerListForReading.Clear();
            child.holdingOwner = null;
            SetMapIndex(mother, -1);
            FinishExit(mother, exit);
            person.ObservePresence(null, 0, 60000);

            Assert.Equal(SuiyinPresence.Left, entrust.Mother);
            Assert.Equal(SuiyinPresence.Left, person.Presence);
            Assert.Equal(-1, person.MissingSince);
        }

        [Fact]
        public void ConfirmedExitDoesNotOverrideDeathOrUnrelatedMaps()
        {
            SuiyinMember dead = new SuiyinMember { LoadId = 17, Presence = SuiyinPresence.Dead };
            SuiyinMember other = new SuiyinMember { LoadId = 17 };
            state.Book.Journals.Add(new SuiyinJournalCase { MapId = 0, People = { dead } });

            state.Book.Journals.Add(new SuiyinJournalCase { MapId = 41, People = { other } });
            Assert.False(state.Book.NoteExit(17, 0));
            Assert.Equal(SuiyinPresence.Dead, dead.Presence);
            Assert.Equal(SuiyinPresence.Here, other.Presence);
        }

        [Fact]
        public void DestroyedObjectsAreUnknownUnlessDepartureWasAlreadyConfirmed()
        {
            Verse.Pawn pawn = Visitor(18);
            SetMapIndex(pawn, -2);
            Assert.True(pawn.Destroyed);
            SuiyinMember missing = new SuiyinMember { LoadId = 18 };
            missing.ObservePresence(pawn, 0, 100);
            Assert.Equal(SuiyinPresence.Unknown, missing.Presence);
            Assert.Equal(100, missing.MissingSince);

            SuiyinMember left = new SuiyinMember { LoadId = 18, Presence = SuiyinPresence.Left };
            left.ObservePresence(pawn, 0, 100);
            Assert.Equal(SuiyinPresence.Left, left.Presence);
            Assert.Equal(-1, left.MissingSince);
        }

        static object BeginExit(Verse.Pawn pawn)
        {
            object[] args = { pawn, null };
            Method(typeof(RHAH_ExitTrustPatch), "Prefix").Invoke(null, args);
            return args[1];
        }

        static void FinishExit(Verse.Pawn pawn, object exit)
        {
            Method(typeof(RHAH_ExitTrustPatch), "Postfix").Invoke(null, new object[] { pawn, exit });
        }

        static void ObserveJournal(SuiyinJournalCase journal, SuiyinMember person, int tick)
        {
            Method(typeof(RHAH_JournalRuntime), "Note").Invoke(null, new object[] { journal, person, tick });
        }

        static Verse.Pawn Visitor(int id)
        {
            Verse.Pawn pawn = new Verse.Pawn { thingIDNumber = id, def = Uninitialized<ThingDef>() };
            pawn.def.category = ThingCategory.Pawn;
            pawn.def.race = new RaceProperties
            {
                lifeStageAges = new List<LifeStageAge>
                {
                    new LifeStageAge { minAge = 0f, def = new LifeStageDef { developmentalStage = DevelopmentalStage.Adult } }
                }
            };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Mobile);
            pawn.health.hediffSet = new HediffSet(pawn);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn };
            CompRHAH_Pawn comp = new CompRHAH_Pawn { parent = mark };
            mark.comps = new List<HediffComp> { comp };
            pawn.health.hediffSet.hediffs.Add(mark);
            comp.ApplySeed(new RHAH_PawnSeed("I-006"));
            SetMapIndex(pawn, 0);
            return pawn;
        }

        static void SetMapIndex(Verse.Pawn pawn, sbyte value)
        {
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, value);
        }

        static void PrimeIndex(params Verse.Pawn[] pawns)
        {
            Index.Clear();
            foreach (Verse.Pawn pawn in pawns)
            {
                Index.Add(pawn.thingIDNumber, pawn);
            }
            Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, Find.TickManager.TicksGame);
            Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, Current.Game);
        }

        static Dictionary<int, Verse.Pawn> Index => (Dictionary<int, Verse.Pawn>)Field(typeof(RHAH_PawnIndex), "byId").GetValue(null);
        static T Uninitialized<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
        static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
    }
}
