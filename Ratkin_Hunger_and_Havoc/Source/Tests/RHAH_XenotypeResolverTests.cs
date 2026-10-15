using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Generation;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_XenotypeResolverTests
    {
        [Fact]
        public void RestrictedDefaultCannotAbsorbTheNativePoolWeight()
        {
            XenotypeDef restricted = new XenotypeDef { defName = "RK_XenoType_Ratkin" };
            XenotypeDef first = new XenotypeDef { defName = "Ratkin_HouseMouse" };
            XenotypeDef second = new XenotypeDef { defName = "Ratkin_Mole" };
            XenotypeDef zero = new XenotypeDef { defName = "Ratkin_LabRat" };
            List<XenotypeChance> pool = new List<XenotypeChance>
            {
                new XenotypeChance(restricted, 99999f),
                new XenotypeChance(first, 0.35f),
                new XenotypeChance(second, 0.15f),
                new XenotypeChance(zero, 0f)
            };
            XenotypeSet set = new XenotypeSet();
            typeof(XenotypeSet).GetField("xenotypeChances", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(set, pool);
            Assert.Same(first, RHAH_XenotypeResolver.ChooseAllowed(set, null, 0f, (x, r) => x != restricted));
            Assert.Same(first, RHAH_XenotypeResolver.ChooseAllowed(set, null, 0.69f, (x, r) => x != restricted));
            Assert.Same(second, RHAH_XenotypeResolver.ChooseAllowed(set, null, 0.7f, (x, r) => x != restricted));
            Assert.Same(second, RHAH_XenotypeResolver.ChooseAllowed(set, null, 1f, (x, r) => x != restricted));
            Assert.Null(RHAH_XenotypeResolver.ChooseAllowed(set, null, 0f, (x, r) => false));
            pool[1].chance = 0f;
            pool[2].chance = 0f;
            Assert.Null(RHAH_XenotypeResolver.ChooseAllowed(set, null, 0.5f, (x, r) => x != restricted));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void InstallationRequiresEveryGeneInTheCorrectGenome(bool inheritable)
        {
            GeneDef ears = new GeneDef { defName = "Ears" };
            GeneDef tail = new GeneDef { defName = "Tail" };
            XenotypeDef xenotype = new XenotypeDef
            {
                inheritable = inheritable,
                genes = new List<GeneDef> { ears, tail }
            };
            Pawn_GeneTracker tracker = (Pawn_GeneTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_GeneTracker));
            List<Gene> endogenes = new List<Gene>();
            List<Gene> xenogenes = new List<Gene>();
            typeof(Pawn_GeneTracker).GetField("endogenes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tracker, endogenes);
            typeof(Pawn_GeneTracker).GetField("xenogenes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tracker, xenogenes);
            List<Gene> right = inheritable ? endogenes : xenogenes;
            List<Gene> wrong = inheritable ? xenogenes : endogenes;
            Assert.False(RHAH_XenotypeResolver.HasXenotypeGenes(tracker, xenotype));
            right.Add(new Gene { def = ears });
            Assert.False(RHAH_XenotypeResolver.HasXenotypeGenes(tracker, xenotype));
            wrong.Add(new Gene { def = tail });
            Assert.False(RHAH_XenotypeResolver.HasXenotypeGenes(tracker, xenotype));
            right.Add(new Gene { def = tail });
            Assert.True(RHAH_XenotypeResolver.HasXenotypeGenes(tracker, xenotype));
        }
    }
}
