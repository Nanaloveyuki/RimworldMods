using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_DoorPatchTests : IDisposable
    {
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly IRHAH_ApiHost previousHost;
        readonly FactionDef[] previousDefs;
        readonly FactionDef[] attitudeDefs = new FactionDef[5];

        public RHAH_DoorPatchTests()
        {
            FieldInfo binding = typeof(DefOfHelper).GetField("bindingNow", BindingFlags.Static | BindingFlags.NonPublic);
            bool previousBinding = (bool)binding.GetValue(null);
            try
            {
                binding.SetValue(null, true);
                RuntimeHelpers.RunClassConstructor(typeof(RHAH_DefOf).TypeHandle);
            }
            finally
            {
                binding.SetValue(null, previousBinding);
            }
            previousDefs = new[]
            {
                RHAH_DefOf.RHAH_Faction_Hostile, RHAH_DefOf.RHAH_Faction_LeaningHostile,
                RHAH_DefOf.RHAH_Faction_Neutral, RHAH_DefOf.RHAH_Faction_LeaningFriendly,
                RHAH_DefOf.RHAH_Faction_Friendly
            };
            for (int i = 0; i < attitudeDefs.Length; i++)
            {
                attitudeDefs[i] = new FactionDef { hidden = true };
            }
            SetDefs(attitudeDefs);
            previousHost = (IRHAH_ApiHost)typeof(RHAH_Api).GetField("host", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            RHAH_Api.Bind(new RHAH_ApiHost());
            RHAH_Mod.Settings = new RHAH_Settings();
        }

        public void Dispose()
        {
            RHAH_Mod.Settings = previousSettings;
            RHAH_Api.Bind(previousHost);
            SetDefs(previousDefs);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void PlayerWorkersWithActiveOriginKeepVanillaDoorPermission(int kind)
        {
            Verse.Pawn pawn = Member(new Faction { def = new FactionDef { isPlayer = true } });
            CompRHAH_Pawn.TryGet(pawn).SetStay((int)kind, 600000, 600000);
            Assert.True(RHAH_Api.IsVisitor(pawn));
            Assert.True(OpenResult(pawn, true));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void EveryAttitudeFactionStillUsesTheSetting(int index)
        {
            Verse.Pawn pawn = Member(new Faction { def = attitudeDefs[index] });
            Assert.False(OpenResult(pawn, true));
            RHAH_Mod.Settings.famineVisitorsOpenDoors = true;
            Assert.True(OpenResult(pawn, true));
            Assert.False(OpenResult(pawn, false));
        }

        [Fact]
        public void OtherFactionsAndFactionlessDeparturesKeepVanillaPermission()
        {
            Assert.True(OpenResult(Member(new Faction { def = new FactionDef() }), true));
            Verse.Pawn pawn = Member(null);
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            Assert.True(OpenResult(pawn, true));
        }

        [Fact]
        public void ReleasedAndUnmarkedPawnsAreNotRestricted()
        {
            Verse.Pawn pawn = Member(new Faction { def = attitudeDefs[2] });
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Released);
            Assert.True(OpenResult(pawn, true));
            pawn.health.hediffSet.hediffs.Clear();
            Assert.True(OpenResult(pawn, true));
        }

        static bool OpenResult(Verse.Pawn pawn, bool original)
        {
            object[] args = { null, pawn, original };
            typeof(RHAH_DoorPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            return (bool)args[2];
        }

        static Verse.Pawn Member(Faction faction)
        {
            Verse.Pawn pawn = new Verse.Pawn();
            pawn.def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            pawn.def.category = ThingCategory.Pawn;
            pawn.SetFactionDirect(faction);
            pawn.health = (Pawn_HealthTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_HealthTracker));
            pawn.health.hediffSet = new HediffSet(pawn);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn };
            CompRHAH_Pawn comp = new CompRHAH_Pawn { parent = mark };
            mark.comps = new List<HediffComp> { comp };
            pawn.health.hediffSet.hediffs.Add(mark);
            comp.ApplySeed(new RHAH_PawnSeed("I-005", 7, 0, RHAH_PawnRole.Refugee, RHAH_Lifecycle.SeekingFood));
            return pawn;
        }

        static void SetDefs(FactionDef[] defs)
        {
            RHAH_DefOf.RHAH_Faction_Hostile = defs[0];
            RHAH_DefOf.RHAH_Faction_LeaningHostile = defs[1];
            RHAH_DefOf.RHAH_Faction_Neutral = defs[2];
            RHAH_DefOf.RHAH_Faction_LeaningFriendly = defs[3];
            RHAH_DefOf.RHAH_Faction_Friendly = defs[4];
        }
    }
}
