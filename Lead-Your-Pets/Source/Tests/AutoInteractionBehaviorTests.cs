using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using RimWorld;
using Verse;
using Verse.AI;
using Xunit;

namespace LeadYourPet.Tests
{
    [Collection("Game state")]
    public class AutoInteractionBehaviorTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Game previousGame = Current.Game;
        private readonly LeadYourPetGameComponent component;
        private readonly LeashLink link;

        public AutoInteractionBehaviorTests()
        {
            Faction faction = Raw<Faction>();
            faction.def = new FactionDef { isPlayer = true };
            Game game = Raw<Game>();
            game.InitData = new GameInitData { playerFaction = faction };
            component = new LeadYourPetGameComponent(game);
            Set(game, "components", new List<GameComponent> { component });
            Current.Game = game;
            Pawn master = Raw<Pawn>();
            Set(master, "factionInt", faction);
            master.pather = Raw<Pawn_PathFollower>();
            master.drafter = new Pawn_DraftController(master);
            link = new LeashLink
            {
                Master = master,
                Pet = Raw<Pawn>(),
                Kind = LeashLinkKind.MouseEggPet,
                NextAutoInteractionTick = 100,
                LastAutoInteractionPoolTick = 100,
                CachedPlayerControlled = true,
                CachedHasValidAutoInteractionPool = true,
                CachedAutoInteractionPool = new List<LeadYourPetInteractionKind>()
            };
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InteractionAttemptsRespectCooldownEvenWithAnEmptyPool(bool available)
        {
            if (available) link.CachedAutoInteractionPool.Add(LeadYourPetInteractionKind.StudyFloor);
            Process(100);
            int next = link.NextAutoInteractionTick;
            Assert.InRange(next, 1900, 3700);
            Process(next - 1);
            Assert.Equal(next, link.NextAutoInteractionTick);
            link.LastAutoInteractionPoolTick = next;
            Process(next);
            Assert.InRange(link.NextAutoInteractionTick, next + 1800, next + 3600);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void MovementAndDraftedOrdersDeferAutomaticInteractions(bool drafted, bool moving)
        {
            Set(link.Master.drafter, "draftedInt", drafted);
            Set(link.Master.pather, "moving", moving);
            Process(100);
            Assert.Equal(100, link.NextAutoInteractionTick);
            Set(link.Master.drafter, "draftedInt", false);
            Set(link.Master.pather, "moving", false);
            Process(100);
            Assert.InRange(link.NextAutoInteractionTick, 1900, 3700);
        }

        private void Process(int ticks)
        {
            typeof(LeadYourPetGameComponent).GetMethod("ProcessMouseEggPet", Fields)
                .Invoke(component, new object[] { link, false, 1f, ticks, false });
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            GodHandsCompat.Reset();
        }

        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static void Set(object instance, string name, object value)
        {
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Fields);
                if (field == null) continue;
                field.SetValue(instance, value);
                return;
            }
            throw new MissingFieldException(instance.GetType().FullName, name);
        }
    }
}
