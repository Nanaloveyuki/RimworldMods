using System.IO;
using System.Runtime.CompilerServices;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_VisitorTests
    {
        static RHAH_VisitorTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        [Fact]
        public void HostBind_NonVisitorGateFalse()
        {
            RHAH_Api.Bind(new RHAH_ApiHost());
            try
            {
                Assert.False(RHAH_Api.IsVisitor(null));
                Assert.False(RHAH_VisitorGate.IsVisitor(null));
                Assert.False(RHAH_VisitorGate.Allows(null, RHAH_BehaviorGate.Beg));
            }
            finally
            {
                RHAH_Api.Bind(null);
            }
        }

        [Fact]
        public void TypeNamesMatchPawnContract()
        {
            Assert.Equal("HungerAndHavoc.Pawn.LordJob_RHAH_Visitor", typeof(LordJob_RHAH_Visitor).FullName);
            Assert.Equal(
                "HungerAndHavoc.Pawn.ThinkNode_ConditionalRHAH_Visitor",
                typeof(ThinkNode_ConditionalRHAH_Visitor).FullName);
            Assert.Equal("HungerAndHavoc.Pawn.JobGiver_RHAH_Visitor", typeof(JobGiver_RHAH_Visitor).FullName);
            Assert.True(typeof(LordJob_RHAH_Visitor).IsPublic);
            Assert.True(typeof(ThinkNode_ConditionalRHAH_Visitor).IsPublic);
            Assert.True(typeof(JobGiver_RHAH_Visitor).IsPublic);
            Assert.False(typeof(RHAH_VisitorGate).IsPublic);
            Assert.False(typeof(RHAH_VisitorGroup).IsPublic);
        }



        [Fact]
        public void NotifyReleasedRemovesLordAndDuty()
        {
            string source = File.ReadAllText(PawnPath("RHAH_VisitorGroup.cs"));
            int notify = source.IndexOf("internal static void NotifyReleased");
            Assert.True(notify >= 0, "NotifyReleased missing");
            string body = source.Substring(notify);
            Assert.Contains("GetLord", body);
            Assert.Contains("RemovePawn", body);
            Assert.Contains("mindState.duty = null", body);
            Assert.Contains("pawn.jobs?.StopAll()", body);
        }


        [Fact]
        public void ThinkTreeInsertsHumanlikePostDutyWithoutHumanlikeXpath()
        {
            string xml = File.ReadAllText(ThinkTreePath());
            Assert.Contains("<insertTag>Humanlike_PostDuty</insertTag>", xml);
            Assert.Contains("HungerAndHavoc.Pawn.ThinkNode_ConditionalRHAH_Visitor", xml);
            Assert.Contains("HungerAndHavoc.Pawn.JobGiver_RHAH_Visitor", xml);

            string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThinkTreePath()), "..", ".."));
            foreach (string file in Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                Assert.DoesNotContain("Humanlike.xml", text);
            }
        }

        [Fact]
        public void VisitorGateWrapsIsVisitorThenAllowsWithoutLinq()
        {
            string source = File.ReadAllText(PawnPath("RHAH_VisitorGate.cs"));
            int visitor = source.IndexOf("RHAH_Api.IsVisitor");
            int allows = source.IndexOf("RHAH_Api.Allows");
            Assert.True(visitor >= 0 && allows > visitor);
            Assert.DoesNotContain("System.Linq", source);
        }






        static string HostPath([CallerFilePath] string testFile = null)
        {
            string testsDir = Path.GetDirectoryName(testFile);
            return Path.GetFullPath(Path.Combine(testsDir, "..", "Identity", "RHAH_ApiHost.cs"));
        }

        static string PawnPath(string fileName, [CallerFilePath] string testFile = null)
        {
            string testsDir = Path.GetDirectoryName(testFile);
            return Path.GetFullPath(Path.Combine(testsDir, "..", "Pawn", fileName));
        }


        static string ThinkTreePath([CallerFilePath] string testFile = null)
        {
            string testsDir = Path.GetDirectoryName(testFile);
            return Path.GetFullPath(Path.Combine(
                testsDir,
                "..",
                "..",
                "1.6",
                "Defs",
                "ThinkTreeDefs",
                "RHAH_ThinkTrees.xml"));
        }
    }
}
