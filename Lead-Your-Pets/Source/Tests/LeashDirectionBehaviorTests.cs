using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using RimWorld;
using Verse;
using Xunit;

namespace LeadYourPet.Tests
{
    [Collection("Game state")]
    public class LeashDirectionBehaviorTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Game previousGame = Current.Game;
        private readonly LeadYourPetGameComponent component;
        private readonly Faction faction;

        public LeashDirectionBehaviorTests()
        {
            faction = Raw<Faction>();
            faction.def = new FactionDef { isPlayer = true };
            Game game = Raw<Game>();
            game.InitData = new GameInitData { playerFaction = faction };
            game.tickManager = Raw<TickManager>();
            component = new LeadYourPetGameComponent(game);
            Set(game, "components", new List<GameComponent> { component });
            Current.Game = game;
        }

        [Fact]
        public void ReversingLeashReleasesTheNewLeaderFromTheOldLink()
        {
            Pawn first = Pawn(1), second = Pawn(2);
            Assert.True(Start(first, second));
            Assert.True(Start(second, first));
            Assert.Null(component.GetLinkForPet(second));
            Assert.Same(second, component.GetLinkForPet(first).Master);
            Assert.False(component.EnsureMouseEggState(second).IsPet);
            Assert.Null(component.EnsureMouseEggState(second).CurrentMaster);
        }

        [Fact]
        public void ClosingALongerCycleReleasesTheNewLeaderButKeepsOtherLinks()
        {
            Pawn first = Pawn(1), second = Pawn(2), third = Pawn(3);
            Start(first, second);
            Start(second, third);
            Start(third, first);
            Assert.Null(component.GetLinkForPet(third));
            Assert.Same(third, component.GetLinkForPet(first).Master);
            Assert.Same(first, component.GetLinkForPet(second).Master);
        }

        [Fact]
        public void OrdinaryChainsAndMultiplePetsRetainTheirDirection()
        {
            Pawn first = Pawn(1), second = Pawn(2), third = Pawn(3), fourth = Pawn(4);
            Start(first, second);
            Start(second, third);
            Start(first, fourth);
            Assert.Same(first, component.GetLinkForPet(second).Master);
            Assert.Same(second, component.GetLinkForPet(third).Master);
            Assert.Same(first, component.GetLinkForPet(fourth).Master);
        }

        [Fact]
        public void RestoringReciprocalSaveLinksKeepsTheMostRecentlyCreatedDirection()
        {
            Pawn first = Pawn(1), second = Pawn(2);
            var older = new LeashLink { Master = first, Pet = second, CreatedTick = 10 };
            var newer = new LeashLink { Master = second, Pet = first, CreatedTick = 20 };
            var links = (List<LeashLink>)Get(component, "links");
            links.Add(newer);
            links.Add(older);
            typeof(LeadYourPetGameComponent).GetMethod("RebuildCaches", Fields).Invoke(component, null);
            Assert.Null(component.GetLinkForPet(second));
            Assert.Same(newer, component.GetLinkForPet(first));
        }

        private bool Start(Pawn master, Pawn pet)
        {
            return (bool)typeof(LeadYourPetGameComponent).GetMethod("StartLeashInternal", Fields)
                .Invoke(component, new object[] { master, pet, LeashLinkKind.MouseEggPet, false });
        }

        private Pawn Pawn(int id)
        {
            Pawn pawn = Raw<Pawn>();
            pawn.thingIDNumber = id;
            pawn.def = Raw<ThingDef>();
            pawn.def.defName = "Human";
            pawn.def.race = new RaceProperties { intelligence = Intelligence.Humanlike };
            pawn.health = Raw<Pawn_HealthTracker>();
            Set(pawn.health, "healthState", PawnHealthState.Mobile);
            pawn.health.hediffSet = new HediffSet(pawn);
            Set(pawn, "mapIndexOrState", (sbyte)-1);
            Set(pawn, "factionInt", faction);
            return pawn;
        }

        public void Dispose() { Current.Game = previousGame; GodHandsCompat.Reset(); }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(object obj, string name)
        {
            for (Type t = obj.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, Fields);
                if (f != null) return f;
            }
            throw new MissingFieldException(name);
        }
        private static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
        private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    }
}
