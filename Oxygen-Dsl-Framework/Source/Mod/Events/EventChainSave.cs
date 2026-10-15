using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using Oxygendsl.Core.Events;
using Verse;

namespace Oxygendsl.Mod.Events
{
    // Wrappers always retain the runtime's canonical objects; PreparedState is never scribed.
    public sealed class EventChainProviderSave : IExposable
    {
        public EventChainProviderState State { get; private set; }
        List<EventChainInstanceSave> instances = new List<EventChainInstanceSave>();
        XmlElement original;
        bool preserveOriginal;
        static readonly HashSet<string> fields = new HashSet<string>(StringComparer.Ordinal)
        {
            "providerId", "schemaVersion", "stateVersion", "nextId", "pausedTick", "reason", "migrationStatus", "mode", "instances"
        };

        public EventChainProviderSave() : this(new EventChainProviderState()) { }
        public EventChainProviderSave(EventChainProviderState state) { State = state ?? throw new ArgumentNullException(nameof(state)); }

        internal void PreserveIfUnavailable(IEventChainProvider provider)
        {
            if (original == null) return;
            preserveOriginal = provider == null || State.SchemaVersion != EventChainRuntime.SchemaVersion
                || State.StateVersion != provider.StateVersion || provider.ApiVersion != EventChainRuntime.ApiVersion
                || !Enum.IsDefined(typeof(EventChainMode), State.Mode);
            if (preserveOriginal) return;
            EventChainSaveXml.Synchronize(State.Instances, ref instances, item => new EventChainInstanceSave(item), item => item.State);
            for (int i = 0; i < instances.Count; i++)
                if (instances[i] != null) instances[i].PreserveIfUnavailable(provider.Definitions);
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) original = EventChainSaveXml.Capture();
            if (Scribe.mode == LoadSaveMode.Saving && preserveOriginal)
            {
                EventChainSaveXml.WriteContents(original);
                return;
            }
            if (Scribe.mode == LoadSaveMode.Saving) EventChainSaveXml.WriteAttributes(original);
            string providerId = State.ProviderId;
            int schemaVersion = State.SchemaVersion, stateVersion = State.StateVersion, nextId = State.NextId, pausedTick = State.PausedTick;
            string reason = State.Reason, migrationStatus = State.MigrationStatus;
            EventChainMode mode = State.Mode;
            Scribe_Values.Look(ref providerId, "providerId", string.Empty, true);
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1, true);
            Scribe_Values.Look(ref stateVersion, "stateVersion", 1, true);
            Scribe_Values.Look(ref nextId, "nextId", 1, true);
            Scribe_Values.Look(ref pausedTick, "pausedTick", -1, true);
            Scribe_Values.Look(ref reason, "reason", string.Empty, true);
            Scribe_Values.Look(ref migrationStatus, "migrationStatus", string.Empty, true);
            Scribe_Values.Look(ref mode, "mode", EventChainMode.Active, true);
            if (Scribe.mode == LoadSaveMode.Saving) EventChainSaveXml.Synchronize(State.Instances, ref instances, item => new EventChainInstanceSave(item), item => item.State);
            EventChainSaveXml.LookList(ref instances, "instances", original);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                State.ProviderId = providerId;
                State.SchemaVersion = schemaVersion;
                State.StateVersion = stateVersion;
                State.NextId = nextId;
                State.PausedTick = pausedTick;
                State.Reason = reason;
                State.MigrationStatus = migrationStatus;
                State.Mode = mode;
                State.Instances = EventChainSaveXml.Unwrap(instances, item => item.State);
            }
            if (Scribe.mode == LoadSaveMode.Saving) EventChainSaveXml.WriteUnknown(original, fields);
        }
    }

    public sealed class EventChainInstanceSave : IExposable
    {
        public EventChainInstance State { get; private set; }
        List<EventChainReceiptSave> receipts = new List<EventChainReceiptSave>();
        XmlElement original;
        bool preserveOriginal;
        static readonly HashSet<string> fields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "definitionId", "definitionVersion", "siteId", "tile", "stage", "startedTick", "deadlineTick", "nextTick", "revision", "payload", "pauseReason", "end", "receipts"
        };

        public EventChainInstanceSave() : this(new EventChainInstance()) { }
        public EventChainInstanceSave(EventChainInstance state) { State = state ?? throw new ArgumentNullException(nameof(state)); }

        internal void PreserveIfUnavailable(IReadOnlyList<IEventChainDefinition> definitions)
        {
            if (original == null) return;
            preserveOriginal = true;
            if (!Enum.IsDefined(typeof(EventChainEnd), State.End)) return;
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i].Id == State.DefinitionId && definitions[i].Version == State.DefinitionVersion) { preserveOriginal = false; return; }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) original = EventChainSaveXml.Capture();
            if (Scribe.mode == LoadSaveMode.Saving && preserveOriginal)
            {
                EventChainSaveXml.WriteContents(original);
                return;
            }
            if (Scribe.mode == LoadSaveMode.Saving) EventChainSaveXml.WriteAttributes(original);
            int id = State.Id, definitionVersion = State.DefinitionVersion, siteId = State.SiteId, tile = State.Tile, stage = State.Stage;
            int startedTick = State.StartedTick, deadlineTick = State.DeadlineTick, nextTick = State.NextTick, revision = State.Revision;
            string definitionId = State.DefinitionId, payload = State.Payload, pauseReason = State.PauseReason;
            EventChainEnd end = State.End;
            Scribe_Values.Look(ref id, "id", 0, true);
            Scribe_Values.Look(ref definitionId, "definitionId", string.Empty, true);
            Scribe_Values.Look(ref definitionVersion, "definitionVersion", 1, true);
            Scribe_Values.Look(ref siteId, "siteId", 0, true);
            Scribe_Values.Look(ref tile, "tile", 0, true);
            Scribe_Values.Look(ref stage, "stage", 0, true);
            Scribe_Values.Look(ref startedTick, "startedTick", 0, true);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick", -1, true);
            Scribe_Values.Look(ref nextTick, "nextTick", -1, true);
            Scribe_Values.Look(ref revision, "revision", 0, true);
            Scribe_Values.Look(ref payload, "payload", string.Empty, true);
            Scribe_Values.Look(ref pauseReason, "pauseReason", string.Empty, true);
            Scribe_Values.Look(ref end, "end", EventChainEnd.None, true);
            if (Scribe.mode == LoadSaveMode.Saving) EventChainSaveXml.Synchronize(State.Receipts, ref receipts, item => new EventChainReceiptSave(item), item => item.State);
            EventChainSaveXml.LookList(ref receipts, "receipts", original);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                State.Id = id;
                State.DefinitionId = definitionId;
                State.DefinitionVersion = definitionVersion;
                State.SiteId = siteId;
                State.Tile = tile;
                State.Stage = stage;
                State.StartedTick = startedTick;
                State.DeadlineTick = deadlineTick;
                State.NextTick = nextTick;
                State.Revision = revision;
                State.Payload = payload;
                State.PauseReason = pauseReason;
                State.End = end;
                State.Receipts = EventChainSaveXml.Unwrap(receipts, item => item.State);
            }
            if (Scribe.mode == LoadSaveMode.Saving) EventChainSaveXml.WriteUnknown(original, fields);
        }
    }

    public sealed class EventChainReceiptSave : IExposable
    {
        public EventChainReceipt State { get; private set; }
        XmlElement original;
        static readonly HashSet<string> fields = new HashSet<string>(StringComparer.Ordinal)
        { "key", "actionId", "status", "data", "reason", "revision", "tick" };

        public EventChainReceiptSave() : this(new EventChainReceipt()) { }
        public EventChainReceiptSave(EventChainReceipt state) { State = state ?? throw new ArgumentNullException(nameof(state)); }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) original = EventChainSaveXml.Capture();
            if (Scribe.mode == LoadSaveMode.Saving) EventChainSaveXml.WriteAttributes(original);
            string key = State.Key, actionId = State.ActionId, status = State.Status, data = State.Data, reason = State.Reason;
            int revision = State.Revision, tick = State.Tick;
            Scribe_Values.Look(ref key, "key", string.Empty, true);
            Scribe_Values.Look(ref actionId, "actionId", string.Empty, true);
            Scribe_Values.Look(ref status, "status", string.Empty, true);
            Scribe_Values.Look(ref data, "data", string.Empty, true);
            Scribe_Values.Look(ref reason, "reason", string.Empty, true);
            Scribe_Values.Look(ref revision, "revision", 0, true);
            Scribe_Values.Look(ref tick, "tick", 0, true);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                State.Key = key;
                State.ActionId = actionId;
                State.Status = status;
                State.Data = data;
                State.Reason = reason;
                State.Revision = revision;
                State.Tick = tick;
            }
            if (Scribe.mode == LoadSaveMode.Saving) EventChainSaveXml.WriteUnknown(original, fields);
        }
    }

    internal static class EventChainSaveXml
    {
        // Verse has no public raw XML writer. This cold save boundary preserves data from newer owners;
        // it is not an action dispatch or a Harmony patch, and must fail rather than discard their fields.
        static readonly FieldInfo writerField = typeof(ScribeSaver).GetField("writer", BindingFlags.Instance | BindingFlags.NonPublic);
        static XmlWriter Writer
        {
            get
            {
                XmlWriter writer = writerField == null ? null : writerField.GetValue(Scribe.saver) as XmlWriter;
                if (writer == null)
                {
                    Scribe.saver.ForceStop();
                    throw new InvalidOperationException("Cannot preserve EventChain save XML: Verse Scribe writer is unavailable. Saving stopped rather than discarding owner data.");
                }
                return writer;
            }
        }

        internal static XmlElement Capture() { return Clone(Scribe.loader.curXmlParent) as XmlElement; }
        internal static XmlNode Clone(XmlNode node)
        {
            return node == null ? null : new XmlDocument().ImportNode(node, true);
        }
        internal static void WriteNode(XmlNode node) { if (node != null) node.WriteTo(Writer); }
        internal static void WriteAttributes(XmlElement original)
        {
            if (original == null || original.Attributes.Count == 0) return;
            XmlWriter writer = Writer;
            foreach (XmlAttribute attribute in original.Attributes)
                if (attribute.Name != "Class" && attribute.Name != "IsNull") attribute.WriteTo(writer);
        }
        internal static void WriteContents(XmlElement original)
        {
            if (original == null) return;
            foreach (XmlAttribute attribute in original.Attributes) attribute.WriteTo(Writer);
            original.WriteContentTo(Writer);
        }
        internal static void WriteUnknown(XmlElement original, HashSet<string> fields)
        {
            if (original == null) return;
            foreach (XmlNode node in original.ChildNodes)
                if (node is XmlElement && !fields.Contains(node.Name)) WriteNode(node);
        }
        internal static void LookList<T>(ref List<T> saves, string label, XmlElement original) where T : class, IExposable
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                XmlElement list = Scribe.loader.curXmlParent == null ? null : Scribe.loader.curXmlParent[label];
                saves = new List<T>();
                if (list != null)
                    foreach (XmlNode node in list.ChildNodes)
                        if (node is XmlElement && node.Name == "li") saves.Add(ScribeExtractor.SaveableFromNode<T>(node, Array.Empty<object>()));
                return;
            }
            if (Scribe.mode != LoadSaveMode.Saving) return;
            if (!Scribe.EnterNode(label)) throw new InvalidOperationException("Cannot write EventChain list: " + label);
            try
            {
                XmlElement list = original == null ? null : original.Name == label ? original : original[label];
                WriteAttributes(list);
                var originalEntries = new List<XmlNode>();
                if (list != null)
                    foreach (XmlNode node in list.ChildNodes)
                        if (node is XmlElement && node.Name == "li") originalEntries.Add(node);
                if (saves != null)
                    for (int i = 0; i < saves.Count; i++)
                    {
                        T saved = saves[i];
                        if (saved == null && i < originalEntries.Count) WriteNode(originalEntries[i]);
                        else Scribe_Deep.Look(ref saved, "li");
                    }
                if (list != null)
                    foreach (XmlNode node in list.ChildNodes)
                        if (node is XmlElement && node.Name != "li") WriteNode(node);
            }
            finally { Scribe.ExitNode(); }
        }
        internal static List<TState> Unwrap<TState, TSave>(List<TSave> saves, Func<TSave, TState> state) where TSave : class where TState : class
        {
            var result = new List<TState>(saves == null ? 0 : saves.Count);
            if (saves != null) for (int i = 0; i < saves.Count; i++) result.Add(saves[i] == null ? null : state(saves[i]));
            return result;
        }
        internal static void Synchronize<TState, TSave>(IReadOnlyList<TState> states, ref List<TSave> saves, Func<TState, TSave> wrap, Func<TSave, TState> state) where TState : class where TSave : class
        {
            if (saves == null) saves = new List<TSave>();
            bool identical = states != null && saves.Count == states.Count;
            if (identical)
                for (int i = 0; i < states.Count; i++)
                    if (!ReferenceEquals(states[i], saves[i] == null ? null : state(saves[i]))) { identical = false; break; }
            if (identical) return;
            var previous = new Dictionary<TState, TSave>();
            for (int i = 0; i < saves.Count; i++) if (saves[i] != null) previous[state(saves[i])] = saves[i];
            saves.Clear();
            if (states == null) return;
            for (int i = 0; i < states.Count; i++)
            {
                TSave saved;
                saves.Add(states[i] == null ? null : previous.TryGetValue(states[i], out saved) ? saved : wrap(states[i]));
            }
        }
    }
}
