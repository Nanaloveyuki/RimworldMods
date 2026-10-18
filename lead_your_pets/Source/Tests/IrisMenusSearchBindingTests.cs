using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace LeadYourPet.Tests
{
    public class IrisMenusSearchBindingTests
    {
        sealed class Entry
        {
            public Entry(string id) => Id = id;
            public string Id { get; }
        }

        static class Host
        {
            public static Func<IEnumerable<Entry>> Provider;

            public static void RegisterSearchProvider(object owner, string pageId, Func<IEnumerable<Entry>> provider, Action<string> focus = null)
            {
                Provider = provider;
            }
        }

        [Fact]
        public void SearchCallbackMatchesRegisterSearchProvider()
        {
            MethodInfo register = typeof(Host).GetMethod(nameof(Host.RegisterSearchProvider));
            Type sequence = register.GetParameters()[2].ParameterType.GetGenericArguments()[0];
            Delegate callback = IrisMenusCompat.BindSearch(sequence, () => new object[] { new Entry("infinite") });

            register.Invoke(null, new object[] { new object(), "leash", callback, null });
            Entry found = Assert.Single(Host.Provider());
            Assert.Equal("infinite", found.Id);
            Assert.Equal(typeof(Func<IEnumerable<Entry>>), callback.GetType());
        }
    }
}
