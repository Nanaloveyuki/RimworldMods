using System;
using System.Collections.Generic;

namespace Oxygendsl.Core.Events
{
    public enum EventChainEnd { None = 0, Completed = 1, Failed = 2, Expired = 3, Cancelled = 4 }
    public enum EventChainMode { Active = 0, Paused = 1, Disabled = 2 }

    public sealed class EventChainReceipt
    {
        public string Key { get; set; } = "";
        public string ActionId { get; set; } = "";
        public string Status { get; set; } = "pending";
        public string Data { get; set; } = "";
        public string Reason { get; set; } = "";
        public int Revision { get; set; }
        public int Tick { get; set; }
    }

    public sealed class EventChainInstance
    {
        public int Id { get; set; }
        public int DefinitionVersion { get; set; } = 1;
        public int SiteId { get; set; }
        public int Tile { get; set; }
        public int Stage { get; set; }
        public int StartedTick { get; set; }
        public int DeadlineTick { get; set; } = -1;
        public int NextTick { get; set; } = -1;
        public int Revision { get; set; }
        public string DefinitionId { get; set; } = "";
        public string Payload { get; set; } = "";
        public string PauseReason { get; set; } = "";
        public EventChainEnd End { get; set; }
        public List<EventChainReceipt> Receipts { get; set; } = new List<EventChainReceipt>();
        // This typed host cache is never part of the persistence contract.
        public object PreparedState { get; set; }
    }

    public sealed class EventChainProviderState
    {
        public int SchemaVersion { get; set; } = 1;
        public int StateVersion { get; set; } = 1;
        public int NextId { get; set; } = 1;
        public int PausedTick { get; set; } = -1;
        public string ProviderId { get; set; } = "";
        public string Reason { get; set; } = "";
        public string MigrationStatus { get; set; } = "";
        public EventChainMode Mode { get; set; }
        public List<EventChainInstance> Instances { get; set; } = new List<EventChainInstance>();
    }

    public interface IEventChainProvider
    {
        string ProviderId { get; }
        int ApiVersion { get; }
        int StateVersion { get; }
        IReadOnlyList<IEventChainDefinition> Definitions { get; }
    }

    public interface IEventChainDefinition
    {
        string Id { get; }
        int Version { get; }
        void Prepare(EventChainInstance instance);
        EventChainDecision Evaluate(EventChainInstance instance, int tick);
        EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId);
    }

    public sealed class EventChainDecision
    {
        public string ActionId { get; set; }
        public int NextTick { get; set; } = -1;
        public int Stage { get; set; } = -1;
        public EventChainEnd End { get; set; }
        public static EventChainDecision Wait(int nextTick) { return new EventChainDecision { NextTick = nextTick }; }
        public static EventChainDecision Finish(EventChainEnd end) { return new EventChainDecision { End = end }; }
        public static EventChainDecision Action(string actionId, int nextTick, int stage = -1)
        { return new EventChainDecision { ActionId = actionId, NextTick = nextTick, Stage = stage }; }
    }

    public sealed class EventChainActionResult
    {
        public string Status { get; set; }
        public string Data { get; set; } = "";
        public string Reason { get; set; } = "";
        public EventChainEnd End { get; set; }
        public static EventChainActionResult Applied(string data = "", EventChainEnd end = EventChainEnd.None)
        { return new EventChainActionResult { Status = "applied", Data = data, End = end }; }
        public static EventChainActionResult Ignored(string reason = "")
        { return new EventChainActionResult { Status = "ignored", Reason = reason }; }
        public static EventChainActionResult Failed(string reason)
        { return new EventChainActionResult { Status = "failed", Reason = reason }; }
    }
}
