using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Generation;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_GenerationOptimizerTests
    {
        static RHAH_GenerationOptimizerTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        [Fact]
        public void StageFollowsBiologicalAgeInsteadOfAlwaysAdult()
        {
            Assert.Equal(DevelopmentalStage.Baby, RHAH_GenerationOptimizer.StageFor(0.85f));
            Assert.Equal(DevelopmentalStage.Baby, RHAH_GenerationOptimizer.StageFor(3.9f));
            Assert.Equal(DevelopmentalStage.Child, RHAH_GenerationOptimizer.StageFor(4f));
            Assert.Equal(DevelopmentalStage.Child, RHAH_GenerationOptimizer.StageFor(11.9f));
            Assert.Equal(DevelopmentalStage.Adult, RHAH_GenerationOptimizer.StageFor(12f));
            Assert.Equal(DevelopmentalStage.Adult, RHAH_GenerationOptimizer.StageFor(null));
            Assert.Equal(DevelopmentalStage.Adult, RHAH_GenerationOptimizer.StageFor(float.NaN));
        }

        [Fact]
        public void ExternalRegistrationDoesNotReplaceBuiltinWeight()
        {
            RHAH_GeneCatalog.ResetForTests();
            RHAH_GeneCatalog.RegisterXenotype("Ratkin_OA", 40f, false);
            RHAH_GeneCatalog.RegisterXenotype(RHAH_GeneCatalog.DefaultXenotypeDefName, 1f, false);
            Assert.Equal(40f, RHAH_GeneCatalog.SuggestedWeight("Ratkin_OA"));
            Assert.False(RHAH_GeneCatalog.IsBuiltin(RHAH_GeneCatalog.FallbackXenotypeDefName));
        }

        [Fact]
        public void XenotypesGroupBySourceModAndKeepOrder()
        {
            XenotypeDef first = Xenotype("RK_XenoType_Ratkin", "New Ratkin Plus");
            XenotypeDef second = Xenotype("RHAH_Xenotype_Ratkin", "Ratkin: Hunger and Havoc");
            XenotypeDef third = Xenotype("Ratkin_OA", "New Ratkin Plus");
            XenotypeDef unknown = new XenotypeDef { defName = "LooseRatkin" };
            List<List<XenotypeDef>> groups = RHAH_XenotypeResolver.GroupBySourceMod(new List<XenotypeDef>
            {
                first,
                second,
                third,
                unknown
            });
            Assert.Equal(3, groups.Count);
            Assert.Equal(new[] { first, third }, groups[0]);
            Assert.Equal(new[] { second }, groups[1]);
            Assert.Equal(new[] { unknown }, groups[2]);
            Assert.Equal("New Ratkin Plus", RHAH_XenotypeResolver.SourceModName(first));
            Assert.Null(RHAH_XenotypeResolver.SourceModName(unknown));
            Assert.Empty(RHAH_XenotypeResolver.GroupBySourceMod(null));
        }

        static XenotypeDef Xenotype(string defName, string modName)
        {
            ModContentPack pack = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
            typeof(ModContentPack).GetField("nameInt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pack, modName);
            return new XenotypeDef
            {
                defName = defName,
                modContentPack = pack
            };
        }

    }
}
