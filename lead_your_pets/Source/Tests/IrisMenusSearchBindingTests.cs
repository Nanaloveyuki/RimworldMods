using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
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
            public static Action<LeashedPawnMapExitBehavior> ExitSelection;

            public static void RegisterSearchProvider(object owner, string pageId, Func<IEnumerable<Entry>> provider, Action<string> focus = null)
            {
                Provider = provider;
            }

            public static void Select<T>(Listing_Standard list, string label, T selected,
                IEnumerable<T> options, Func<T, string> optionLabel, Action<T> onSelected)
            {
                if (typeof(T) == typeof(LeashedPawnMapExitBehavior))
                    ExitSelection = (Action<LeashedPawnMapExitBehavior>)(object)onSelected;
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

        [Fact]
        public void ExitSelectionUpdatesSettingsWhenDeferredCallbackRuns()
        {
            SetSelectApi();
            var settings = new LeadYourPetSettings();

            IrisMenusCompat.SelectExit(null, "exit", settings.leashedPawnMapExitBehavior,
                value => settings.leashedPawnMapExitBehavior = value);
            Host.ExitSelection(LeashedPawnMapExitBehavior.LeaveMap);

            Assert.Equal(LeashedPawnMapExitBehavior.LeaveMap, settings.leashedPawnMapExitBehavior);
        }

        static void SetSelectApi()
        {
            typeof(IrisMenusCompat).GetField("select", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, typeof(Host).GetMethod(nameof(Host.Select)));
        }
    }
}
