using System;
using System.Reflection;
using Xunit;

namespace LeadYourPet.Tests
{
    public class OptionalModApiTests
    {
        private static class Original
        {
            public static string Generate(int a, string b, bool c, float d) => "original";
        }

        private static class Continued
        {
            public static string Generate(int a, string b, bool c, float d, bool optional = false)
                => optional ? "wrong" : "continued";
            public static string Generate(int a, string b, bool c, float d, bool optional, object gender)
                => "wrong overload";
        }

        private class Visitors
        {
            public static bool TryMakeHostile(string[] pawns, out int count)
            {
                count = pawns.Length;
                return true;
            }
            public bool InstanceOnly(string[] pawns, out int count) { count = 0; return true; }
        }

        [Theory]
        [InlineData(typeof(Original), "original")]
        [InlineData(typeof(Continued), "continued")]
        public void ResolvesOriginalAndContinuedGeneration(Type type, string expected)
        {
            MethodInfo method = OptionalModApi.Resolve(type, "Generate", typeof(int), typeof(string), typeof(bool), typeof(float));
            Assert.NotNull(method);
            Assert.Equal(expected, OptionalModApi.Invoke(method, 1, "pawn", false, 0.35f));
        }

        [Fact]
        public void OptionalParametersAreNotFilledByReflectionItself()
        {
            MethodInfo method = OptionalModApi.Resolve(typeof(Continued), "Generate", typeof(int), typeof(string), typeof(bool), typeof(float));
            Assert.Throws<TargetParameterCountException>(() => method.Invoke(null, new object[] { 1, "pawn", false, 0.35f }));
        }

        [Fact]
        public void MissingModAndWrongSignaturesAreUnavailable()
        {
            Assert.Null(OptionalModApi.Resolve(null, "Generate"));
            Assert.Null(OptionalModApi.Resolve(typeof(Continued), "Missing"));
            Assert.Null(OptionalModApi.Resolve(typeof(Continued), "Generate", typeof(string)));
        }

        [Fact]
        public void VisitorOutParameterIsPreserved()
        {
            MethodInfo method = OptionalModApi.Resolve(typeof(Visitors), "TryMakeHostile", typeof(string[]), typeof(int).MakeByRefType());
            object[] arguments = { new[] { "pawn" }, 0 };
            Assert.Equal(true, OptionalModApi.Invoke(method, arguments));
            Assert.Equal(1, arguments[1]);
            Assert.Null(OptionalModApi.Resolve(typeof(Visitors), "InstanceOnly", typeof(string[]), typeof(int).MakeByRefType()));
        }
    }
}
