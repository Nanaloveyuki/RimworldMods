using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_FactionInitTests
    {
        [Fact]
        public void FinalizeInit_RestoresHiddenFactionRelationsWithoutReplacingSavedFactions()
        {
            FieldInfo binding = typeof(DefOfHelper).GetField("bindingNow", BindingFlags.Static | BindingFlags.NonPublic);
            bool previousBinding = (bool)binding.GetValue(null);
            try
            {
                binding.SetValue(null, true);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RHAH_DefOf).TypeHandle);
            }
            finally
            {
                binding.SetValue(null, previousBinding);
            }

            Game previousGame = Current.Game;
            FactionDef[] previousDefs =
            {
                RHAH_DefOf.RHAH_Faction_Hostile,
                RHAH_DefOf.RHAH_Faction_LeaningHostile,
                RHAH_DefOf.RHAH_Faction_Neutral,
                RHAH_DefOf.RHAH_Faction_LeaningFriendly,
                RHAH_DefOf.RHAH_Faction_Friendly
            };
            try
            {
                Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                Current.Game.components = new List<GameComponent>();
                typeof(Game).GetField("maps", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Current.Game, new List<Map>());
                World world = (World)FormatterServices.GetUninitializedObject(typeof(World));
                world.factionManager = new FactionManager();
                world.worldPawns = new WorldPawns();
                world.worldObjects = new WorldObjectsHolder();
                Current.Game.World = world;
                Faction player = new Faction
                {
                    def = new FactionDef { isPlayer = true },
                    loadID = 70
                };
                world.factionManager.AllFactionsListForReading.Add(player);
                typeof(FactionManager).GetField("ofPlayer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(world.factionManager, player);
                Faction[] saved = new Faction[5];
                FactionDef[] defs = new FactionDef[5];
                for (int i = 0; i < saved.Length; i++)
                {
                    defs[i] = new FactionDef { hidden = true };
                    saved[i] = new Faction { def = defs[i], loadID = 80 + i, Name = "Saved faction " + i };
                    saved[i].SetRelation(new FactionRelation
                    {
                        other = player,
                        kind = i == 0 ? FactionRelationKind.Neutral : FactionRelationKind.Hostile,
                        baseGoodwill = i == 0 ? 0 : -60
                    });
                    world.factionManager.AllFactionsListForReading.Add(saved[i]);
                }
                RHAH_DefOf.RHAH_Faction_Hostile = defs[0];
                RHAH_DefOf.RHAH_Faction_LeaningHostile = defs[1];
                RHAH_DefOf.RHAH_Faction_Neutral = defs[2];
                RHAH_DefOf.RHAH_Faction_LeaningFriendly = defs[3];
                RHAH_DefOf.RHAH_Faction_Friendly = defs[4];
                GameComponent_RHAH_Game component = new GameComponent_RHAH_Game(Current.Game);

                component.FinalizeInit();
                component.FinalizeInit();

                Assert.Equal(6, world.factionManager.AllFactionsListForReading.Count);
                for (int i = 0; i < saved.Length; i++)
                {
                    Assert.Same(saved[i], world.factionManager.FirstFactionOfDef(defs[i]));
                    Assert.Equal(80 + i, saved[i].loadID);
                    Assert.Equal("Saved faction " + i, saved[i].Name);
                    Assert.False(saved[i].HasGoodwill);
                    int goodwill = i == 0 ? -100 : 0;
                    FactionRelationKind kind = i == 0 ? FactionRelationKind.Hostile : FactionRelationKind.Neutral;
                    Assert.Equal(goodwill, player.RelationWith(saved[i]).baseGoodwill);
                    Assert.Equal(goodwill, saved[i].RelationWith(player).baseGoodwill);
                    Assert.Equal(kind, player.RelationWith(saved[i]).kind);
                    Assert.Equal(kind, saved[i].RelationWith(player).kind);
                }
            }
            finally
            {
                Current.Game = previousGame;
                RHAH_DefOf.RHAH_Faction_Hostile = previousDefs[0];
                RHAH_DefOf.RHAH_Faction_LeaningHostile = previousDefs[1];
                RHAH_DefOf.RHAH_Faction_Neutral = previousDefs[2];
                RHAH_DefOf.RHAH_Faction_LeaningFriendly = previousDefs[3];
                RHAH_DefOf.RHAH_Faction_Friendly = previousDefs[4];
            }
        }
    }
}
