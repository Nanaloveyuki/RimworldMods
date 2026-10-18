using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Oxygendsl.Core.Events;
using Verse;

namespace Oxygendsl.Mod.Events
{
    public static class OxygenEventChainHost
    {
        static readonly List<IEventChainProvider> providers = new List<IEventChainProvider>();
        static Action<EventChainRuntime> preparing;
        static bool initializing;

        public static event Action<EventChainRuntime> Preparing
        {
            add { RequireMainThread(); if (initializing) throw new InvalidOperationException("Cannot change EventChain migrations during preparation."); preparing += value; }
            remove { RequireMainThread(); if (initializing) throw new InvalidOperationException("Cannot change EventChain migrations during preparation."); preparing -= value; }
        }

        public static EventChainRuntime Runtime
        {
            get
            {
                RequireMainThread();
                GameComponent_Oxygendsl component = GameComponent_Oxygendsl.Current;
                return component == null ? null : component.EventChains;
            }
        }

        public static void Register(IEventChainProvider provider)
        {
            RequireMainThread();
            if (initializing) throw new InvalidOperationException("Cannot change EventChain providers during preparation.");
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            GameComponent_Oxygendsl component = GameComponent_Oxygendsl.Current;
            bool registered = false;
            for (int i = 0; i < providers.Count; i++)
                if (string.Equals(providers[i].ProviderId, provider.ProviderId, StringComparison.Ordinal)) { registered = true; break; }
            if (!registered && component != null && component.EventChainFinalizeComplete)
                throw new InvalidOperationException("New EventChain providers must register before game FinalizeInit: " + provider.ProviderId);
            // A disposable validator prevents a rejected live replacement from changing future games.
            var validator = new EventChainRuntime(new List<EventChainProviderState>());
            for (int i = 0; i < providers.Count; i++) validator.Register(providers[i]);
            validator.Register(provider);
            EventChainRuntime runtime = Runtime;
            if (runtime != null) runtime.Register(provider);
            for (int i = 0; i < providers.Count; i++)
                if (string.Equals(providers[i].ProviderId, provider.ProviderId, StringComparison.Ordinal))
                {
                    providers[i] = provider;
                    if (component != null) component.PreserveUnavailableEventChains(providers);
                    return;
                }
            providers.Add(provider);
            if (component != null) component.PreserveUnavailableEventChains(providers);
        }

        public static void Initialize(GameComponent_Oxygendsl component, int tick)
        {
            RequireMainThread();
            if (component == null) throw new ArgumentNullException(nameof(component));
            if (initializing) throw new InvalidOperationException("EventChain initialization is not reentrant.");
            if (!component.EventChainFinalizeComplete || Scribe.mode != LoadSaveMode.Inactive
                || !ReferenceEquals(GameComponent_Oxygendsl.Current, component))
                throw new InvalidOperationException("EventChains require the current finalized game and resolved save references.");
            if (!component.BeginEventChainInitialization()) return;
            initializing = true;
            try
            {
                EventChainRuntime runtime = component.CreateEventChainRuntime();
                if (runtime == null) return; // Unknown root schema remains intact and cannot execute or migrate.
                for (int i = 0; i < providers.Count; i++) runtime.Register(providers[i]);
                component.PreserveUnavailableEventChains(providers);
                if (preparing != null)
                    foreach (Action<EventChainRuntime> migration in preparing.GetInvocationList()) migration(runtime);
                runtime.Activate(tick);
                component.CompleteEventChainInitialization();
            }
            catch (Exception exception)
            {
                component.FailEventChainInitialization(exception.Message);
                Log.Error("[Oxygendsl] EventChain preparation paused: " + exception);
            }
            finally { initializing = false; }
        }

        public static int RemoveProviderState(XDocument document, string providerId)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrWhiteSpace(providerId)) throw new ArgumentException("An exact EventChain provider ID is required.", nameof(providerId));
            XElement root = document.Root;
            if (root == null || root.Name != "savegame" || root.Element("game") == null)
                throw new ArgumentException("Expected a RimWorld savegame/game document.", nameof(document));
            XElement components = root.Element("game").Element("components");
            if (components == null) return 0;
            var partitions = new List<XElement>();
            foreach (XElement component in components.Elements("li"))
            {
                if ((string)component.Attribute("Class") != typeof(GameComponent_Oxygendsl).FullName) continue;
                XElement savedProviders = component.Element("eventChainProviders");
                if (savedProviders == null) continue;
                foreach (XElement partition in savedProviders.Elements("li"))
                    if (string.Equals((string)partition.Element("providerId"), providerId, StringComparison.Ordinal)) partitions.Add(partition);
            }
            for (int i = 0; i < partitions.Count; i++) partitions[i].Remove();
            return partitions.Count;
        }

        internal static void RequireMainThread()
        {
            if (!UnityData.IsInMainThread) throw new InvalidOperationException("Oxygen EventChains must run on the game main thread.");
        }
    }
}
