using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Oxygendsl.Core;
using RimWorld;
using Verse;

namespace Oxygendsl.Mod
{
    public sealed class OxygenPawnEvent
    {
        public long Sequence { get; internal set; }
        public string Provider { get; internal set; }
        public string Id { get; internal set; }
        public Pawn Subject { get; internal set; }
        public IOxygenHostValue Facts { get; internal set; }
    }

    public static class OxygenEvents
    {
        static readonly Dictionary<string, Subscription> subscriptions = new Dictionary<string, Subscription>(StringComparer.Ordinal);
        static Subscription[] listeners = Array.Empty<Subscription>();
        static readonly ConditionalWeakTable<Pawn, BreakStamp> prisonBreaks = new ConditionalWeakTable<Pawn, BreakStamp>();
        static long sequence;
        static int threadId;
        static bool installed;

        internal static void Install()
        {
            if (installed) return;
            LongEventHandler.ExecuteWhenFinished(() => threadId = Thread.CurrentThread.ManagedThreadId);
            var harmony = new Harmony("nanaloveyuki.oxygendsl.events");
            harmony.Patch(AccessTools.Method(typeof(InteractionWorker_RecruitAttempt), "Interacted"),
                prefix: new HarmonyMethod(typeof(OxygenEvents), nameof(BeforeRecruitment)),
                postfix: new HarmonyMethod(typeof(OxygenEvents), nameof(AfterRecruitment)));
            harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.Notify_PrisonBreakout)),
                postfix: new HarmonyMethod(typeof(OxygenEvents), nameof(AfterPrisonBreak)));
            installed = true;
        }

        public static IDisposable Subscribe(string owner, Action<OxygenPawnEvent> callback)
        {
            RequireGameThread();
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("An event owner is required.", nameof(owner));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (subscriptions.ContainsKey(owner)) throw new ArgumentException("Event owner is already subscribed: " + owner, nameof(owner));
            var subscription = new Subscription(owner, callback);
            subscriptions.Add(owner, subscription);
            RefreshListeners();
            return subscription;
        }

        public static void Publish(string provider, string id, Pawn subject, IOxygenHostValue facts)
        {
            RequireGameThread();
            if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("An event provider is required.", nameof(provider));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An event ID is required.", nameof(id));
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (listeners.Length == 0) return;
            var message = new OxygenPawnEvent { Sequence = ++sequence, Provider = provider, Id = id, Subject = subject, Facts = facts };
            Subscription[] current = listeners;
            for (int index = 0; index < current.Length; index++)
            {
                Subscription listener = current[index];
                if (!listener.Active) continue;
                try { listener.Callback(message); }
                catch (Exception exception) { Log.Error("[Oxygendsl] Event " + provider + "/" + id + " failed for " + listener.Owner + ": " + exception); }
            }
        }

        static void BeforeRecruitment(Pawn recipient, out float __state)
        {
            __state = listeners.Length != 0 && recipient != null && recipient.IsPrisonerOfColony && recipient.guest != null
                ? recipient.guest.resistance : float.NaN;
        }

        static void AfterRecruitment(Pawn initiator, Pawn recipient, float __state)
        {
            if (float.IsNaN(__state) || recipient == null || recipient.guest == null) return;
            float after = recipient.guest.resistance;
            if (after >= __state || float.IsNaN(after) || float.IsInfinity(after)) return;
            Publish("std", "prisoner.resistance_reduced", recipient, new DictionaryOxygenHostValue(new Dictionary<string, object>
            {
                ["before"] = (double)__state, ["after"] = (double)after,
                ["amount"] = (double)(__state - after), ["actor_id"] = initiator == null ? 0 : initiator.thingIDNumber
            }));
        }

        static void AfterPrisonBreak(Pawn __instance)
        {
            if (listeners.Length == 0 || __instance == null || !__instance.IsPrisonerOfColony || __instance.guest == null
                || !PrisonBreakUtility.IsPrisonBreaking(__instance)) return;
            BreakStamp stamp = prisonBreaks.GetOrCreateValue(__instance);
            int tick = __instance.guest.lastPrisonBreakTicks;
            if (ReferenceEquals(stamp.Game, Current.Game) && stamp.Tick == tick) return;
            stamp.Game = Current.Game;
            stamp.Tick = tick;
            Publish("std", "prisoner.prison_break", __instance, new DictionaryOxygenHostValue(new Dictionary<string, object>
            {
                ["tick"] = Find.TickManager == null ? 0 : Find.TickManager.TicksGame
            }));
        }

        sealed class BreakStamp
        {
            public Game Game;
            public int Tick = int.MinValue;
            public BreakStamp() { }
        }

        static void RequireGameThread()
        {
            if (threadId == 0 || Thread.CurrentThread.ManagedThreadId != threadId)
                throw new InvalidOperationException("Oxygen events must run on the initialized game thread.");
        }

        static void RefreshListeners()
        {
            listeners = new Subscription[subscriptions.Count];
            subscriptions.Values.CopyTo(listeners, 0);
        }

        sealed class Subscription : IDisposable
        {
            internal readonly string Owner;
            internal readonly Action<OxygenPawnEvent> Callback;
            internal bool Active = true;
            internal Subscription(string owner, Action<OxygenPawnEvent> callback) { Owner = owner; Callback = callback; }
            public void Dispose()
            {
                RequireGameThread();
                if (!Active) return;
                Active = false;
                subscriptions.Remove(Owner);
                RefreshListeners();
            }
        }
    }
}
