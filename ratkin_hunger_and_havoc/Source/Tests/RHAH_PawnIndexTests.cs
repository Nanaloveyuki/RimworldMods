using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_PawnIndexTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly int previousTick = BuiltTick;
        readonly Game previousOwner = (Game)Field(typeof(RHAH_PawnIndex), "builtGame").GetValue(null);
        readonly Dictionary<int, Verse.Pawn> previousIndex = new Dictionary<int, Verse.Pawn>(Index);

        public RHAH_PawnIndexTests()
        {
            Current.Game = EmptyGame();
            RHAH_PawnIndex.Clear();
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
            Current.Game = previousGame;
        }

        [Fact]
        public void HitsReuseReferencesAcrossTicksUntilTheHourlyRefresh()
        {
            Verse.Pawn pawn = AddWorldPawn(11);

            Assert.Same(pawn, RHAH_PawnIndex.Find(11, 100));
            Assert.Same(pawn, RHAH_PawnIndex.Find(11, 101));
            Assert.Same(pawn, RHAH_PawnIndex.Find(11, 100 + GenDate.TicksPerHour - 1));
            Assert.Equal(100, BuiltTick);

            Assert.Same(pawn, RHAH_PawnIndex.Find(11, 100 + GenDate.TicksPerHour));
            Assert.Equal(100 + GenDate.TicksPerHour, BuiltTick);
        }

        [Fact]
        public void MissFindsANewWorldPawnBeforeTheHourlyRefresh()
        {
            Verse.Pawn existing = AddWorldPawn(12);
            Assert.Same(existing, RHAH_PawnIndex.Find(12, 100));
            Verse.Pawn arrived = AddWorldPawn(13);

            Assert.Same(arrived, RHAH_PawnIndex.Find(13, 101));
            Assert.Equal(101, BuiltTick);
            Assert.Same(existing, RHAH_PawnIndex.Find(12, 102));
            Assert.Equal(101, BuiltTick);
        }

        [Fact]
        public void SwitchingSavesAtTheSameTickResolvesTheNewPawnWithTheSameId()
        {
            Verse.Pawn first = AddWorldPawn(14);
            Assert.Same(first, RHAH_PawnIndex.Find(14, 100));
            Current.Game = EmptyGame();
            Verse.Pawn second = AddWorldPawn(14);

            Assert.Same(second, RHAH_PawnIndex.Find(14, 100));
            Assert.NotSame(first, second);
        }

        [Fact]
        public void RememberMakesAnArrivalVisibleAfterThisTicksScanWithoutRebuilding()
        {
            Assert.Null(RHAH_PawnIndex.Find(15, 100));
            Verse.Pawn pawn = AddWorldPawn(15);

            RHAH_PawnIndex.Remember(pawn);

            Assert.Same(pawn, RHAH_PawnIndex.Find(15, 100));
            Assert.Same(pawn, RHAH_PawnIndex.Find(15, 101));
            Assert.Equal(100, BuiltTick);
        }

        [Fact]
        public void DiscardedReferencesAreRejectedAndRememberRemovesThem()
        {
            Verse.Pawn pawn = AddWorldPawn(16);
            Assert.Same(pawn, RHAH_PawnIndex.Find(16, 100));
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)-3);
            WorldPawnsAlive.Clear();

            Assert.True(pawn.Discarded);
            Assert.Null(RHAH_PawnIndex.Find(16, 101));
            Assert.Equal(100, BuiltTick);
            RHAH_PawnIndex.Remember(pawn);
            Assert.False(Index.ContainsKey(16));
        }

        static int BuiltTick => (int)Field(typeof(RHAH_PawnIndex), "builtTick").GetValue(null);
        static Dictionary<int, Verse.Pawn> Index =>
            (Dictionary<int, Verse.Pawn>)Field(typeof(RHAH_PawnIndex), "byId").GetValue(null);
        static HashSet<Verse.Pawn> WorldPawnsAlive =>
            (HashSet<Verse.Pawn>)Field(typeof(WorldPawns), "pawnsAlive").GetValue(Find.WorldPawns);

        static Game EmptyGame()
        {
            Game game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
            Field(typeof(Game), "maps").SetValue(game, new List<Map>());
            game.World = (World)FormatterServices.GetUninitializedObject(typeof(World));
            game.World.worldPawns = new WorldPawns();
            game.World.worldObjects = new WorldObjectsHolder();
            return game;
        }

        static Verse.Pawn AddWorldPawn(int id)
        {
            Verse.Pawn pawn = new Verse.Pawn { thingIDNumber = id };
            WorldPawnsAlive.Add(pawn);
            return pawn;
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }
    }
}
