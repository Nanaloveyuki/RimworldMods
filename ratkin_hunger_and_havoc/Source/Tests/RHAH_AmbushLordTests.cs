using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Trade;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    using Pawn = Verse.Pawn;
    using Verse.AI;
    public class RHAH_AmbushLordTests : System.IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly object previousPrefs = typeof(Prefs).GetField("data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);

        public void Dispose()
        {
            Current.Game = previousGame;
            typeof(Prefs).GetField("data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, previousPrefs);
        }

        [Fact]
        public void WrongFactionAttackerGetsNoLord()
        {
            RHAH_AmbushLordFixture.Bind();
            Faction faction = RHAH_AmbushLordFixture.Faction();
            Faction other = RHAH_AmbushLordFixture.Faction();
            Map map = RHAH_AmbushLordFixture.Map();
            Pawn owned = RHAH_AmbushLordFixture.Attacker(faction);
            Pawn foreign = RHAH_AmbushLordFixture.Attacker(other);
            List<Pawn> attackers = new List<Pawn> { owned, foreign };

            Assert.False(TradeEventRouter.TryCreateAssaultLord(faction, map, attackers));

            Assert.Null(owned.GetLord());
            Assert.Null(foreign.GetLord());
            Assert.Empty(map.lordManager.lords);
        }

        [Fact]
        public void EmptyOrMissingMapDoesNotCreateALord()
        {
            RHAH_AmbushLordFixture.Bind();
            Faction faction = RHAH_AmbushLordFixture.Faction();
            Pawn attacker = RHAH_AmbushLordFixture.Attacker(faction);

            Assert.False(TradeEventRouter.TryCreateAssaultLord(faction, RHAH_AmbushLordFixture.Map(), new List<Pawn>()));
            Assert.False(TradeEventRouter.TryCreateAssaultLord(faction, null, new List<Pawn> { attacker }));
            Assert.False(TradeEventRouter.TryCreateAssaultLord(null, RHAH_AmbushLordFixture.Map(), new List<Pawn> { attacker }));
            Assert.Null(attacker.GetLord());
        }

        [Fact]
        public void AmbushUsesHostileFactionAndRepairsBothPlayerRelations()
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
                RHAH_AmbushLordFixture.Bind();
                World world = (World)FormatterServices.GetUninitializedObject(typeof(World));
                world.factionManager = new FactionManager();
                Current.Game.World = world;
                Faction player = new Faction { def = new FactionDef { isPlayer = true }, loadID = 70 };
                world.factionManager.AllFactionsListForReading.Add(player);
                typeof(FactionManager).GetField("ofPlayer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(world.factionManager, player);
                Faction[] saved = new Faction[5];
                for (int i = 0; i < saved.Length; i++)
                {
                    saved[i] = new Faction { def = new FactionDef { hidden = true }, loadID = 80 + i };
                    world.factionManager.AllFactionsListForReading.Add(saved[i]);
                }
                RHAH_DefOf.RHAH_Faction_Hostile = saved[0].def;
                RHAH_DefOf.RHAH_Faction_LeaningHostile = saved[1].def;
                RHAH_DefOf.RHAH_Faction_Neutral = saved[2].def;
                RHAH_DefOf.RHAH_Faction_LeaningFriendly = saved[3].def;
                RHAH_DefOf.RHAH_Faction_Friendly = saved[4].def;
                saved[0].SetRelation(new FactionRelation { other = player, kind = FactionRelationKind.Neutral, baseGoodwill = 0 });
                player.RelationWith(saved[0]).kind = FactionRelationKind.Ally;
                player.RelationWith(saved[0]).baseGoodwill = 50;

                Faction attackers = TradeEventRouter.RequireAmbushFaction();

                Assert.Same(saved[0], attackers);
                Assert.True(attackers.HostileTo(player));
                Assert.True(player.HostileTo(attackers));
                Assert.Equal(-100, attackers.RelationWith(player).baseGoodwill);
                Assert.Equal(-100, player.RelationWith(attackers).baseGoodwill);
            }
            finally
            {
                RHAH_DefOf.RHAH_Faction_Hostile = previousDefs[0];
                RHAH_DefOf.RHAH_Faction_LeaningHostile = previousDefs[1];
                RHAH_DefOf.RHAH_Faction_Neutral = previousDefs[2];
                RHAH_DefOf.RHAH_Faction_LeaningFriendly = previousDefs[3];
                RHAH_DefOf.RHAH_Faction_Friendly = previousDefs[4];
            }
        }
    }

    static class RHAH_AmbushLordFixture
    {
        public static void Bind()
        {
            Current.Game = (Game)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Game));
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Current.Game.tickManager = new TickManager();

            PrefsData data = new PrefsData();
            data.adaptiveTrainingEnabled = false;
            data.devMode = false;
            typeof(Prefs).GetField("data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, data);
        }

        public static Faction Faction()
        {
            Faction faction = new Faction();
            faction.def = new FactionDef();
            faction.def.humanlikeFaction = true;
            faction.def.autoFlee = false;
            faction.def.isPlayer = false;
            faction.Name = "Ambush";
            return faction;
        }

        public static Map Map()
        {
            Map map = new Map();
            map.info = new MapInfo();
            map.info.isPocketMap = true;
            map.pocketTileInfo = new Tile();
            map.pocketTileInfo.PrimaryBiome = new BiomeDef();
            map.pocketTileInfo.PrimaryBiome.canExitMap = true;
            map.events = new MapEvents(map);
            map.lordManager = new LordManager(map);
            return map;
        }

        public static Pawn Attacker(Faction faction)
        {
            Pawn pawn = new Pawn();
            pawn.mindState = new Pawn_MindState();
            pawn.jobs = new Pawn_JobTracker(pawn);
            pawn.def = (ThingDef)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ThingDef));
            pawn.def.category = ThingCategory.Pawn;
            pawn.SetFactionDirect(faction);
            return pawn;
        }

    }
}
