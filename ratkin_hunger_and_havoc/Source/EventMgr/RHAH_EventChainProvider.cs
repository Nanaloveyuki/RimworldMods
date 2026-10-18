using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal sealed class RHAH_EventChainProvider : IEventChainProvider
    {
        internal const string ProviderKey = "nanaloveyuki.ratkin.hungerandhavoc";
        internal const string ArrivalId = "RHAH_PredationArrival";
        readonly IReadOnlyList<IEventChainDefinition> definitions;

        internal RHAH_EventChainProvider()
        {
            IEventChainDefinition[] items = new IEventChainDefinition[57];
            Lazy<EventChainScript> follow = new Lazy<EventChainScript>(() => EventChainScript.Compile(ReadScript("RHAH_EventFollow")));
            Lazy<EventChainScript> arrival = new Lazy<EventChainScript>(() => EventChainScript.Compile(ReadScript("RHAH_PredationArrival")));
            for (int i = 0; i < 54; i++)
            {
                items[i] = new Definition("I-" + (i + 1).ToString("000", CultureInfo.InvariantCulture), follow);
            }

            items[54] = new Definition(ArrivalId, arrival);
            items[55] = new KingdomArcDefinition();
            items[56] = new KingdomCollaborationDefinition();
            definitions = Array.AsReadOnly(items);
        }

        public string ProviderId => ProviderKey;
        public int ApiVersion => EventChainRuntime.ApiVersion;
        public int StateVersion => 1;
        public IReadOnlyList<IEventChainDefinition> Definitions => definitions;

        internal static string ReadScript(string name)
        {
            using (Stream stream = typeof(RHAH_EventChainProvider).Assembly.GetManifestResourceStream("HungerAndHavoc.EventMgr." + name + ".oxy"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Missing embedded event chain script: " + name);
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        sealed class Definition : IEventChainDefinition
        {
            readonly Lazy<EventChainScript> script;

            internal Definition(string id, Lazy<EventChainScript> script)
            {
                Id = id;
                this.script = script;
            }

            public string Id { get; }
            public int Version => 1;

            public void Prepare(EventChainInstance instance)
            {
                if (instance == null || instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.Stage < 0 || instance.Stage > 3)
                {
                    throw new FormatException("Invalid RHAH chain definition, version or stage.");
                }

                if (instance.End == EventChainEnd.None && instance.Stage == 3 && !RHAH_EventFollowRules.HasLongFollow(Id))
                {
                    throw new FormatException("Invalid RHAH interactive long-chain stage.");
                }
                Facts cached = instance.PreparedState as Facts;
                if (cached != null && cached.Source == instance.Payload && cached.DefinitionId == Id && cached.SiteId == instance.SiteId)
                {
                    return;
                }

                if (instance.End != EventChainEnd.None || instance.Stage == 2)
                {
                    instance.PreparedState = new Facts(Id, instance.Payload, 0, instance.SiteId);
                    return;
                }

                int batchId = 0;
                int siteId;
                if (Id == ArrivalId)
                {
                    if (!int.TryParse(instance.Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out siteId) || siteId < 0 || siteId != instance.SiteId)
                    {
                        throw new FormatException("Invalid RHAH predation arrival map payload.");
                    }
                }
                else
                {
                    if (!RHAH_EventFollowRules.ReadPayload(instance.Payload, out string displayId, out batchId, out siteId) || displayId != Id || siteId != instance.SiteId)
                    {
                        throw new FormatException("Invalid RHAH follow payload or long-chain stage.");
                    }
                }

                instance.PreparedState = new Facts(Id, instance.Payload, batchId, siteId);
            }

            public EventChainDecision Evaluate(EventChainInstance instance, int tick)
            {
                Facts facts = instance.PreparedState as Facts;
                if (facts == null)
                {
                    throw new InvalidOperationException("RHAH chain was not prepared.");
                }

                if (Id != ArrivalId && instance.Stage == 3)
                {
                    return RHAH_LongFollowRuntime.Evaluate(instance.Id, facts.SiteId, tick);
                }
                if (Id != ArrivalId && instance.Stage == 1 && !RHAH_EventFollowRules.HasLongFollow(Id))
                {
                    return EventChainDecision.Wait(-1);
                }
                facts.Stage = instance.Stage;
                facts.Tick = tick;
                facts.NextTick = instance.NextTick;
                facts.NextLongTick = instance.Stage == 1 ? RHAH_EventChainMigration.NextLongTick(tick, instance.Id) : -1;
                facts.LongReady = instance.Stage == 1 && RHAH_EventFollowRuntime.LongReady(Id, facts.BatchId, facts.SiteId);
                return script.Value.Evaluate(facts);
            }

            public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
            {
                Facts facts = instance.PreparedState as Facts;
                if (facts == null)
                {
                    return EventChainActionResult.Failed("RHAH chain payload is not prepared.");
                }

                if (Id != ArrivalId && instance.Stage == 0 && actionId == "roll_predator")
                {
                    return EventChainActionResult.Applied(RHAH_EventFollowRuntime.TryPredator(facts.SiteId, facts.BatchId));
                }

                if (Id != ArrivalId && instance.Stage == 1 && actionId == "notify_long")
                {
                    RHAH_LongFollowRuntime.RegisterChain(instance.Id, Id, facts.BatchId, facts.SiteId, instance.DeadlineTick);
                    return EventChainActionResult.Applied("interactive_chain_opened");
                }

                if (Id != ArrivalId && instance.Stage == 3 && actionId == "advance_long")
                {
                    string result = RHAH_LongFollowRuntime.Dispatch(instance.Id, Id, facts.BatchId, facts.SiteId, tick);
                    RHAH_LongFollowRecord record = RHAH_LongFollowRuntime.FindRecord(instance.Id);
                    return EventChainActionResult.Applied(result, record != null && record.Completed ? EventChainEnd.Completed : EventChainEnd.None);
                }
                if (Id == ArrivalId && instance.Stage == 0 && actionId == "arrive_predator")
                {
                    Map map = RHAH_EventFollowRuntime.FindMap(facts.SiteId);
                    MapComponent_RHAH_Map component = map?.GetComponent<MapComponent_RHAH_Map>();
                    if (component == null)
                    {
                        return EventChainActionResult.Applied("map_unavailable", EventChainEnd.Completed);
                    }

                    RHAH_CampPredation.TryArrive(component, map);
                    component.PredationArrivalInstanceId = 0;
                    return EventChainActionResult.Applied("arrival_consumed", EventChainEnd.Completed);
                }

                return EventChainActionResult.Failed("Unknown RHAH typed event action: " + actionId);
            }
        }

        sealed class Facts : IOxygenHostValue
        {
            internal readonly string DefinitionId;
            internal readonly string Source;
            internal readonly int BatchId;
            internal readonly int SiteId;
            internal int Stage;
            internal int Tick;
            internal int NextTick;
            internal int NextLongTick;
            internal bool LongReady;

            internal Facts(string definitionId, string source, int batchId, int siteId)
            {
                DefinitionId = definitionId;
                Source = source;
                BatchId = batchId;
                SiteId = siteId;
            }

            public bool TryGetMember(string name, out object value)
            {
                switch (name)
                {
                    case "definition_id": value = DefinitionId; return true;
                    case "batch_id": value = BatchId; return true;
                    case "site_id": value = SiteId; return true;
                    case "stage": value = Stage; return true;
                    case "tick": value = Tick; return true;
                    case "next_tick": value = NextTick; return true;
                    case "next_long_tick": value = NextLongTick; return true;
                    case "long_ready": value = LongReady; return true;
                    default: value = null; return false;
                }
            }
        }
    }

    internal static class RHAH_EventChainStartup
    {
        internal static void Initialize()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                OxygenEventChainHost.Register(new RHAH_EventChainProvider());
                OxygenEventChainHost.Preparing += RHAH_EventChainMigration.PrepareGame;
                OxygenEventChainHost.Preparing += RHAH_LongFollowRuntime.PrepareGame;
            });
        }
    }
}
