using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_LongClaimFollowTests : IDisposable
    {
        readonly Game previousGame;
        readonly Dictionary<int, Verse.Pawn> indexed;
        readonly Dictionary<int, Verse.Pawn> previousIndex;
        readonly int previousBuiltTick;
        readonly Game previousBuiltGame;
        readonly Map map;
        const int Tick = 741;

        public RHAH_LongClaimFollowTests()
        {
            RimWorldAssemblies.EnsureResolved();
            previousGame = Current.Game;
            Current.Game = Uninitialized<Game>();
            Current.Game.tickManager = new TickManager();
            Current.Game.letterStack = new LetterStack();
            Current.Game.World = Uninitialized<World>();
            Current.Game.World.factionManager = new FactionManager();
            Faction player = new Faction { def = new FactionDef { isPlayer = true } };
            Field(typeof(FactionManager), "ofPlayer").SetValue(Current.Game.World.factionManager, player);
            map = Uninitialized<Map>();
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map> { map });
            indexed = (Dictionary<int, Verse.Pawn>)Field(typeof(RHAH_PawnIndex), "byId").GetValue(null);
            previousIndex = new Dictionary<int, Verse.Pawn>(indexed);
            previousBuiltTick = (int)Field(typeof(RHAH_PawnIndex), "builtTick").GetValue(null);
            previousBuiltGame = (Game)Field(typeof(RHAH_PawnIndex), "builtGame").GetValue(null);
            Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, Current.Game);
            indexed.Clear();
            Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, Tick);
        }

        public void Dispose()
        {
            indexed.Clear();
            foreach (KeyValuePair<int, Verse.Pawn> entry in previousIndex) indexed.Add(entry.Key, entry.Value);
            Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, previousBuiltTick);
            Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, previousBuiltGame);
            Current.Game = previousGame;
        }

        [Fact]
        public void ClosedKeepsakeCustodyOffersOnlyReturnWithoutOrigin()
        {
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                DisplayId = "I-012",
                Opened = true,
                Phase = 8,
                KeepsakeTaken = true
            };

            RHAH_LongOption option = Assert.Single(RHAH_LongClaimFollow.Options(record, map));
            Assert.Equal("return", option.Action);
            Assert.Equal("RHAH_LongClaim_ReturnKeepsake", option.Key);
            Assert.Equal("RHAH_LongClaim_ItemUnavailable", option.DisabledKey);
            Assert.Equal("RHAH_LongClaim_KeepsakeWithheld", RHAH_LongClaimFollow.TextKey(record));
        }

        [Fact]
        public void RecordedPermanentTransferClosesButTemporaryAbsenceDoesNot()
        {
            Verse.Pawn child = Pawn(91, PawnHealthState.Mobile, false);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                DisplayId = "I-041",
                Opened = true,
                Phase = 14,
                SubjectId = child.thingIDNumber,
                LetterOpen = true,
                RevengePending = true
            };
            Assert.False(RHAH_LongClaimFollow.ResolvePending(record, map, Tick));
            Assert.False(record.Completed);
            Assert.True(record.RevengePending);
            record.DepartedIds.Add(record.SubjectId);

            Assert.True(RHAH_LongClaimFollow.ResolvePending(record, map, Tick));
            Assert.True(record.Completed);
            Assert.False(record.LetterOpen);
            Assert.False(record.RevengePending);
        }

        [Theory]
        [InlineData(PawnHealthState.Mobile, false)]
        [InlineData(PawnHealthState.Down, true)]
        [InlineData(PawnHealthState.Dead, true)]
        public void OriginalPartyDefeatDistinguishesIncapacitationFromHealthyExit(PawnHealthState state, bool defeated)
        {
            Verse.Pawn visitor = Pawn(92, state, false);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { Phase = 14, SubjectId = 91 };
            record.VisitorIds.Add(visitor.thingIDNumber);
            Assert.Equal(defeated, OriginalPartyDefeated(record));

            record.VisitorIds.Clear();
            record.DepartedIds.Add(visitor.thingIDNumber);
            Assert.Equal(defeated, OriginalPartyDefeated(record));
        }

        [Fact]
        public void OriginalPartyRemainsUnresolvedWhileOneLoyalistCanStillLeave()
        {
            Verse.Pawn downed = Pawn(92, PawnHealthState.Down, false);
            Verse.Pawn capable = Pawn(93, PawnHealthState.Mobile, true);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { Phase = 14, SubjectId = 91 };
            record.VisitorIds.Add(downed.thingIDNumber);
            record.VisitorIds.Add(capable.thingIDNumber);
            Assert.False(OriginalPartyDefeated(record));
            Field(typeof(Thing), "mapIndexOrState").SetValue(capable, (sbyte)-1);
            Assert.False(OriginalPartyDefeated(record));
        }

        [Fact]
        public void CapturedOriginalLoyalistCannotKeepRevengePending()
        {
            Verse.Pawn visitor = Pawn(92, PawnHealthState.Mobile, true);
            visitor.guest = Uninitialized<Pawn_GuestTracker>();
            visitor.guest.guestStatusInt = GuestStatus.Prisoner;
            Field(typeof(Pawn_GuestTracker), "hostFactionInt").SetValue(visitor.guest, Faction.OfPlayer);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { Phase = 14, SubjectId = 91 };
            record.VisitorIds.Add(visitor.thingIDNumber);

            Assert.True(OriginalPartyDefeated(record));
        }

        bool OriginalPartyDefeated(RHAH_LongFollowRecord record)
        {
            MethodInfo method = typeof(RHAH_LongClaimFollow).GetMethod("OriginalPartyDefeated", BindingFlags.Static | BindingFlags.NonPublic);
            return (bool)method.Invoke(null, new object[] { record, Tick });
        }

        Verse.Pawn Pawn(int id, PawnHealthState state, bool onMap)
        {
            Verse.Pawn pawn = Uninitialized<Verse.Pawn>();
            pawn.thingIDNumber = id;
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, state);
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, onMap ? (sbyte)0 : (sbyte)-1);
            indexed.Add(id, pawn);
            return pawn;
        }

        static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(type.FullName + "." + name);
        }
    }
}
