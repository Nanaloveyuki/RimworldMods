using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MouseDisaster
{
    internal sealed class MouseDisasterCleanupPlan
    {
        public readonly HashSet<string> OwnedDefs = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> OwnedClasses = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> LegacyPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly HashSet<string> ThingDefs = new HashSet<string>(StringComparer.Ordinal);
        public string PackageId;
    }

    // Operates on a separate serialized snapshot, including world pawns and nested holders.
    internal sealed class MouseDisasterSaveCleanup
    {
        private readonly MouseDisasterCleanupPlan plan;
        private readonly HashSet<string> removedReferences = new HashSet<string>(StringComparer.Ordinal);
        public int RemovedEntries { get; private set; }
        public int ReplacedDefs { get; private set; }

        public MouseDisasterSaveCleanup(MouseDisasterCleanupPlan plan) { this.plan = plan; }

        public void Clean(XDocument document)
        {
            XElement game = document.Root?.Element("game");
            if (document.Root?.Name != "savegame" || game == null)
                throw new InvalidOperationException("Expected a RimWorld savegame/game document.");

            // A quest owns its pending world objects and parts. Remove it as a unit.
            foreach (XElement quest in game.Descendants("quests").Elements().Where(n =>
                plan.OwnedDefs.Contains((string)n.Element("root") ?? "")).ToList())
                Remove(quest);

            // Keep visited maps attached to the same world object after removing the custom class.
            foreach (XElement site in game.Descendants().Where(n => IsOwnedClass(n) &&
                ((string)n.Attribute("Class")).Split(',')[0].Trim() == "MouseDisaster.MouseDisasterRefugeeSite").ToList())
            {
                site.SetAttributeValue("Class", "RimWorld.Planet.Site");
                site.SetElementValue("def", "Site");
                Remove(site.Element("refugeeResidents"));
                Remove(site.Element("refugeeCleared"));
                ReplacedDefs++;
            }

            foreach (XElement node in game.Descendants().Where(IsOwnedClass).ToList())
            {
                if (node.Document == null) continue;
                if (node.Name == "lordJob") Remove(node.Parent);
                else if (node.Name == "curDriver") ClearCurrentJob(node.Parent);
                else Remove(node);
            }

            foreach (XElement node in game.Descendants().Where(n => !n.HasElements && plan.OwnedDefs.Contains(n.Value)).ToList())
            {
                if (node.Document == null) continue;
                string value = node.Value;
                if (plan.Replacements.TryGetValue(value, out string replacement))
                {
                    node.Value = replacement;
                    ReplacedDefs++;
                    continue;
                }
                RemoveDefReference(node);
            }

            while (true)
            {
                var references = game.Descendants().Where(n => !n.HasElements && removedReferences.Contains(n.Value)).ToList();
                if (references.Count == 0) break;
                foreach (XElement node in references)
                {
                    if (node.Document == null) continue;
                    XElement job = node.Ancestors().FirstOrDefault(n => n.Name == "curJob");
                    XElement queuedJob = node.Ancestors().FirstOrDefault(n => n.Name == "li" && n.Parent?.Name == "jobs");
                    XElement reservation = node.Ancestors().FirstOrDefault(n => n.Name == "li" &&
                        (n.Parent?.Name == "reservations" || (n.Parent?.Name == "list" &&
                            n.Ancestors().Any(a => a.Name == "pawnDestinationReservationManager"))));
                    if (job != null) ClearCurrentJob(job.Parent);
                    else if (queuedJob != null) Remove(queuedJob);
                    else if (reservation != null) Remove(reservation);
                    else if (node.Name == "li") Remove(node);
                    else node.Value = "null";
                }
            }

            var unresolved = game.Descendants().Where(n => IsOwnedClass(n) ||
                (!n.HasElements && (plan.OwnedDefs.Contains(n.Value) || removedReferences.Contains(n.Value))))
                .Select(PathOf).Take(12).ToList();
            if (unresolved.Count > 0)
                throw new InvalidOperationException("Unresolved mod references: " + string.Join(", ", unresolved));

            XElement ids = document.Root.Element("meta")?.Element("modIds");
            if (ids != null)
            {
                var entries = ids.Elements().ToList();
                for (int i = entries.Count - 1; i >= 0; i--)
                    if (IsOwnedPackageId(entries[i].Value))
                    {
                        foreach (string listName in new[] { "modNames", "modSteamIds" })
                            ids.Parent.Element(listName)?.Elements().ElementAtOrDefault(i)?.Remove();
                        entries[i].Remove();
                    }
            }
        }

        private bool IsOwnedPackageId(string packageId)
        {
            return string.Equals(packageId, plan.PackageId, StringComparison.OrdinalIgnoreCase) ||
                   plan.LegacyPackageIds.Contains(packageId);
        }

        private bool IsOwnedClass(XElement node)
        {
            string name = (string)node.Attribute("Class");
            return name != null && plan.OwnedClasses.Contains(name.Split(',')[0].Trim());
        }

        private void RemoveDefReference(XElement node)
        {
            XElement owner = node.Parent;
            if (node.Name == "li") { Remove(node); return; }
            if (node.Name == "recipe" && owner.Parent?.Name == "bills") { Remove(owner); return; }
            if (node.Name == "def" && owner.Name == "curJob") { ClearCurrentJob(owner.Parent); return; }
            if (node.Name == "def" && plan.ThingDefs.Contains(node.Value) && owner.Element("id") != null)
            {
                Remove(owner);
                return;
            }

            // These are independently serialized records, not scalar fields of a pawn.
            var record = node.Ancestors().FirstOrDefault(n => n.Parent != null &&
                new[] { "hediffs", "imList", "allTraits", "memories", "xenogenes", "endogenes",
                    "queuedIncidents", "archivables", "letters", "tales" }.Contains(n.Parent.Name.LocalName));
            if (record != null) { Remove(record); return; }
            var logEntry = node.Ancestors().FirstOrDefault(n => n.Parent?.Name == "entries" &&
                n.Ancestors().Any(a => a.Name == "battleLog" || a.Name == "playLog"));
            if (logEntry != null) { Remove(logEntry); return; }
            if (node.Name == "def" && (owner.Name == "duty" || owner.Name == "mentalState" ||
                (owner.Name == "curState" && owner.Parent?.Name == "mentalStateHandler")))
            {
                Remove(owner);
                return;
            }
            var queuedJob = node.Ancestors().FirstOrDefault(n => n.Name == "li" && n.Parent?.Name == "jobs");
            if (queuedJob != null) { Remove(queuedJob); return; }
            throw new InvalidOperationException("Unsupported mod reference at " + PathOf(node) + " = " + node.Value);
        }

        private void ClearCurrentJob(XElement jobs)
        {
            Remove(jobs.Element("curJob"));
            Remove(jobs.Element("curDriver"));
        }

        private void Remove(XElement node)
        {
            if (node?.Parent == null) return;
            foreach (XElement entry in node.DescendantsAndSelf())
            {
                if (entry.Parent?.Name == "quests" && entry.Element("root") != null)
                {
                    AddReference(entry, "id", "Quest_");
                    string questId = (string)entry.Element("id");
                    int index = 0;
                    foreach (XElement part in entry.Element("parts")?.Elements() ?? Enumerable.Empty<XElement>())
                        removedReferences.Add("QuestPart_" + questId + "_" + index++);
                }
                if (entry.Element("def") != null && (entry.Name == "worldObject" || entry.Parent?.Name == "worldObjects"))
                    AddReference(entry, "ID", "WorldObject_");
                if (entry.Parent?.Name == "bills" && entry.Element("recipe") != null)
                    AddReference(entry, "loadID", "Bill_" + entry.Element("recipe").Value + "_");
                if (entry.Element("id") != null && entry.Element("def") != null)
                    removedReferences.Add("Thing_" + entry.Element("id").Value);
                if (entry.Parent?.Name == "xenogenes" || entry.Parent?.Name == "endogenes")
                    AddReference(entry, "loadID", "Gene_");
                if (entry.Parent?.Name == "hediffs") AddReference(entry, "loadID", "Hediff_");
                if (entry.Parent?.Name == "lords") AddReference(entry, "loadID", "Lord_");
                if (entry.Name == "curJob" || entry.Name == "job") AddReference(entry, "loadID", "Job_");
                if (entry.Parent?.Name == "archivables" || entry.Parent?.Name == "letters")
                    AddReference(entry, "ID", "Letter_");
                if (entry.Parent?.Name == "areas" && IsOwnedClass(entry))
                    AddReference(entry, "ID", "Area_", "_MouseDisasterRelief");
            }
            // Scribe dictionaries keep parallel key/value lists.
            if (node.Parent.Name == "keys" || node.Parent.Name == "values")
            {
                XElement parent = node.Parent;
                int index = parent.Elements().TakeWhile(n => n != node).Count();
                string pairedName = parent.Name == "keys" ? "values" : "keys";
                parent.Parent.Element(pairedName)?.Elements().ElementAtOrDefault(index)?.Remove();
            }
            node.Remove();
            RemovedEntries++;
        }

        private void AddReference(XElement node, string idField, string prefix, string suffix = "")
        {
            if (node.Element(idField) != null) removedReferences.Add(prefix + node.Element(idField).Value + suffix);
        }

        private static string PathOf(XElement node)
        {
            return string.Join("/", node.AncestorsAndSelf().Reverse().Select(n => n.Name.LocalName));
        }
    }
}
