using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using HungerAndHavoc.EventMgr;
using Oxygendsl.Mod.Events;

namespace HungerAndHavoc.Core
{
    internal sealed class RHAH_SaveCleanupPlan
    {
        internal readonly HashSet<string> OwnedDefs = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> OwnedClasses = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> ThingDefs = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> GeneDefs = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> ThoughtStageKeys = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> CampObjectDefs = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> CampObjectClasses = new HashSet<string>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> MapGeneratorReplacements = new Dictionary<string, string>(StringComparer.Ordinal);
        internal string PackageId;
    }

    // 只改序列化副本，不改正在运行的世界
    internal sealed class RHAH_SaveCleanup
    {
        static readonly HashSet<string> RecordParents = new HashSet<string>(StringComparer.Ordinal)
        {
            "hediffs", "imList", "allTraits", "memories", "xenogenes", "endogenes",
            "queuedIncidents", "archivables", "letters", "tales"
        };

        readonly RHAH_SaveCleanupPlan plan;
        readonly HashSet<string> removedReferences = new HashSet<string>(StringComparer.Ordinal);
        internal int RemovedEntries { get; private set; }
        internal int ReplacedDefs { get; private set; }

        internal RHAH_SaveCleanup(RHAH_SaveCleanupPlan plan)
        {
            this.plan = plan;
        }

        internal void Clean(XDocument document)
        {
            XElement game = document.Root?.Element("game");
            if (document.Root?.Name != "savegame" || game == null)
            {
                throw new InvalidOperationException("Expected a RimWorld savegame/game document.");
            }

            RequireReplacements(game);
            RequireKnownTypes(game);
            RemovedEntries += OxygenEventChainHost.RemoveProviderState(document, RHAH_EventChainProvider.ProviderKey);
            // 先撤销短工加的原版锁 再移除身份 Hediff
            foreach (XElement ownedLocks in ContentNodes(game).Where(item => item.Name == "shortStayLockedApparel").ToList())
            {
                XElement pawn = ownedLocks.Ancestors().FirstOrDefault(item => item.Element("apparel") != null);
                XElement locked = pawn?.Element("apparel")?.Element("lockedApparel");
                if (locked == null)
                {
                    continue;
                }

                HashSet<string> references = new HashSet<string>(ownedLocks.Elements("li").Select(item => item.Value));
                foreach (XElement reference in locked.Elements("li").Where(item => references.Contains(item.Value)).ToList())
                {
                    reference.Remove();
                }
            }
            foreach (XElement quest in ContentNodes(game).Where(item => item.Name == "quests").Elements()
                .Where(item => plan.OwnedDefs.Contains((string)item.Element("root") ?? string.Empty)).ToList())
            {
                Remove(quest);
            }

            foreach (XElement site in ContentNodes(game).Where(item => ClassName(item) == "HungerAndHavoc.Storyteller.Suiyin.WorldObject_RHAH_KingdomSite").ToList())
            {
                TransferKingdomSupply(game, site);
            }
            foreach (XElement site in ContentNodes(game).Where(IsMappedCamp).ToList())
            {
                ConvertCamp(site);
            }

            foreach (XElement node in ContentNodes(game).Where(IsOwnedClass).ToList())
            {
                if (node.Document == null || IsMappedCamp(node))
                {
                    continue;
                }

                if (node.Name == "lordJob")
                {
                    Remove(node.Parent);
                }
                else if (node.Name == "curDriver")
                {
                    ClearCurrentJob(node.Parent);
                }
                else
                {
                    Remove(node);
                }
            }

            foreach (XElement cache in ContentNodes(game).Where(item => item.Name == "SpokenThoughtTicks" &&
                item.Parent?.Parent?.Name == "hediffs").ToList())
            {
                foreach (XElement key in (cache.Element("keys")?.Elements("li") ?? Enumerable.Empty<XElement>())
                    .Where(item => plan.ThoughtStageKeys.Contains(item.Value)).ToList())
                {
                    Remove(key);
                }
            }

            foreach (XElement node in ContentNodes(game).Where(IsOwnedScalar).ToList())
            {
                if (node.Document == null)
                {
                    continue;
                }

                if (plan.Replacements.TryGetValue(node.Value, out string replacement))
                {
                    node.Value = replacement;
                    ReplacedDefs++;
                    continue;
                }

                RemoveDefReference(node);
            }

            while (true)
            {
                List<XElement> references = ContentNodes(game)
                    .Where(item => !item.HasElements && removedReferences.Contains(item.Value)).ToList();
                if (references.Count == 0)
                {
                    break;
                }

                for (int i = 0; i < references.Count; i++)
                {
                    XElement node = references[i];
                    if (node.Document == null)
                    {
                        continue;
                    }

                    XElement job = node.Ancestors().FirstOrDefault(item => item.Name == "curJob");
                    XElement queued = node.Ancestors().FirstOrDefault(item => item.Name == "li" && item.Parent?.Name == "jobs");
                    XElement reservation = node.Ancestors().FirstOrDefault(item => item.Name == "li" &&
                        (item.Parent?.Name == "reservations" || (item.Parent?.Name == "list" &&
                            item.Ancestors().Any(ancestor => ancestor.Name == "pawnDestinationReservationManager"))));
                    if (job != null)
                    {
                        ClearCurrentJob(job.Parent);
                    }
                    else if (queued != null)
                    {
                        Remove(queued);
                    }
                    else if (reservation != null)
                    {
                        Remove(reservation);
                    }
                    else if (node.Name == "li")
                    {
                        Remove(node);
                    }
                    else
                    {
                        node.Value = "null";
                    }
                }
            }

            List<string> unresolved = new List<string>();
            foreach (XElement item in ContentNodes(game))
            {
                if (unresolved.Count >= 12)
                {
                    break;
                }

                string text = (item.Value ?? string.Empty).Trim();
                bool ownedText = !item.Elements().Any() &&
                    (plan.OwnedDefs.Contains(text) || removedReferences.Contains(text) || text.StartsWith("RHAH_", StringComparison.Ordinal));
                string typeName = ClassName(item);
                bool unknownType = typeName != null &&
                    typeName.StartsWith("HungerAndHavoc.", StringComparison.Ordinal) &&
                    !plan.OwnedClasses.Contains(typeName);
                if (IsOwnedClass(item) || ownedText || unknownType)
                {
                    unresolved.Add(unknownType ? PathOf(item) + "=" + typeName :
                        item.Elements().Any() ? PathOf(item) : PathOf(item) + "=" + text);
                }
            }

            if (unresolved.Count > 0)
            {
                throw new InvalidOperationException("Unresolved mod references: " + string.Join(", ", unresolved));
            }
            StripPackage(document.Root.Element("meta"));
        }

        void TransferKingdomSupply(XElement game, XElement site)
        {
            List<XElement> supplies = (site.Element("kingdomSupply")?.Elements("li") ?? Enumerable.Empty<XElement>())
                .Where(item => !plan.ThingDefs.Contains((string)item.Element("def") ?? string.Empty)).ToList();
            if (supplies.Count == 0)
            {
                return;
            }

            XElement pawns = game.Element("world")?.Element("worldPawns")?.Element("pawnsAlive");
            List<XElement> inventories = new List<XElement>();
            foreach (XElement recipient in site.Element("kingdomRecipients")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                XElement pawn = pawns?.Elements("li").FirstOrDefault(item => "Thing_" + (string)item.Element("id") == recipient.Value);
                XElement inventory = pawn?.Element("inventory");
                XElement container = inventory?.Element("innerContainer");
                XElement innerList = container?.Element("innerList");
                if (IsSerializedNull(pawns) || IsSerializedNull(pawn) || IsSerializedNull(inventory) ||
                    IsSerializedNull(container) || IsSerializedNull(innerList) || IsOwnedClass(pawn) ||
                    plan.ThingDefs.Contains((string)pawn.Element("def") ?? string.Empty))
                {
                    continue;
                }

                if (!inventories.Contains(innerList))
                {
                    inventories.Add(innerList);
                }
            }

            if (inventories.Count == 0)
            {
                throw new InvalidOperationException("Cannot preserve kingdomSupply at " + PathOf(site) +
                    " (ID " + (string)site.Element("ID") + "): no safe world pawn inventory/innerContainer/innerList.");
            }

            for (int i = 0; i < supplies.Count; i++)
            {
                XElement supply = supplies[i];
                supply.Remove();
                inventories[i % inventories.Count].Add(supply);
            }
        }

        static bool IsSerializedNull(XElement node)
        {
            return node == null || string.Equals((string)node.Attribute("IsNull"), "True", StringComparison.OrdinalIgnoreCase);
        }

        void RequireKnownTypes(XElement game)
        {
            List<string> unknown = new List<string>();
            foreach (XElement item in ContentNodes(game))
            {
                if (unknown.Count >= 12)
                {
                    break;
                }

                string typeName = ClassName(item);
                if (typeName == null || !typeName.StartsWith("HungerAndHavoc.", StringComparison.Ordinal) ||
                    plan.OwnedClasses.Contains(typeName))
                {
                    continue;
                }

                unknown.Add(PathOf(item) + "=" + typeName);
            }

            if (unknown.Count > 0)
            {
                throw new InvalidOperationException("Unknown mod type: " + string.Join(", ", unknown));
            }
        }

        void RequireReplacements(XElement game)
        {
            HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement node in ContentNodes(game).Where(IsOwnedScalar))
            {
                if (!plan.Replacements.ContainsKey(node.Value) && NeedsReplacement(node))
                {
                    missing.Add(node.Value);
                }
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException("Missing replacement for " + string.Join(", ", missing.OrderBy(item => item, StringComparer.Ordinal)));
            }
        }

        bool NeedsReplacement(XElement node)
        {
            return node.Name == "kindDef" || node.Name == "childhood" || node.Name == "adulthood" ||
                node.Name == "xenotype" || node.Name == "storytellerDef" ||
                (node.Name == "def" && node.Parent?.Element("loadID") != null && node.Parent.Parent?.Name == "factions") ||
                node.Name == "factionDef";
        }

        bool IsMappedCamp(XElement node)
        {
            if (!HasMapOrResidents(node) || node.Element("def") == null)
            {
                return false;
            }

            string typeName = ClassName(node);
            return plan.CampObjectDefs.Contains((string)node.Element("def")) ||
                (typeName != null && plan.CampObjectClasses.Contains(typeName));
        }

        static bool HasMapOrResidents(XElement node)
        {
            if (node == null || node.Element("ID") == null)
            {
                return false;
            }

            XElement game = node.Ancestors().FirstOrDefault(item => item.Name == "game");
            string reference = "WorldObject_" + node.Element("ID").Value;
            if (game != null && game.Element("maps") != null)
            {
                foreach (XElement map in game.Element("maps").Elements())
                {
                    if ((string)map.Element("mapInfo")?.Element("parent") == reference)
                    {
                        return true;
                    }
                }
            }

            XElement residents = node.Element("residents");
            return residents != null && residents.Elements().Any();
        }

        void ConvertCamp(XElement site)
        {
            string typeName = ClassName(site);
            if (typeName != null && typeName != "RimWorld.Planet.Site" && !plan.CampObjectClasses.Contains(typeName))
            {
                throw new InvalidOperationException("Site cannot keep a generated map at " + PathOf(site));
            }

            if (typeName != null && typeName != "RimWorld.Planet.Site")
            {
                site.SetAttributeValue("Class", "RimWorld.Planet.Site");
            }

            site.SetElementValue("def", "Site");
            Remove(site.Element("parts"));
            Remove(site.Element("cleared"));
            Remove(site.Element("nextCheck"));
            Remove(site.Element("displayId"));
            Remove(site.Element("spawnBatchId"));
            Remove(site.Element("settled"));
            RetargetGenerator(site);
            ReplacedDefs++;
        }

        void RetargetGenerator(XElement site)
        {
            XElement game = site.Ancestors().FirstOrDefault(item => item.Name == "game");
            string reference = "WorldObject_" + (string)site.Element("ID");
            if (game?.Element("maps") == null || string.IsNullOrEmpty((string)site.Element("ID")))
            {
                return;
            }

            foreach (XElement map in game.Element("maps").Elements())
            {
                if ((string)map.Element("mapInfo")?.Element("parent") != reference)
                {
                    continue;
                }

                XElement generator = map.Element("generatorDef");
                string name = (generator?.Value ?? string.Empty).Trim();
                if (generator == null || !plan.MapGeneratorReplacements.TryGetValue(name, out string replacement))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(replacement))
                {
                    throw new InvalidOperationException("Missing map generator replacement for " + name + " at " + PathOf(generator));
                }

                if (generator.Value == replacement)
                {
                    continue;
                }

                generator.Value = replacement;
                ReplacedDefs++;
            }
        }

        static IEnumerable<XElement> ContentNodes(XElement game)
        {
            foreach (XElement node in game.Descendants())
            {
                bool framework = node.AncestorsAndSelf().Any(item => item.Parent?.Name == "components" &&
                    ClassName(item) == "Oxygendsl.Mod.GameComponent_Oxygendsl");
                if (!framework)
                {
                    yield return node;
                }
            }
        }

        bool IsOwnedScalar(XElement node)
        {
            return plan.OwnedDefs.Contains((node.Value ?? string.Empty).Trim()) && !node.Elements().Any();
        }

        bool IsOwnedClass(XElement node)
        {
            string name = ClassName(node);
            return name != null && plan.OwnedClasses.Contains(name);
        }

        static string ClassName(XElement node)
        {
            string name = (string)node.Attribute("Class");
            if (name == null)
            {
                return null;
            }

            int comma = name.IndexOf(',');
            return (comma < 0 ? name : name.Substring(0, comma)).Trim();
        }

        void RemoveDefReference(XElement node)
        {
            XElement owner = node.Parent;
            if (node.Name == "li")
            {
                Remove(node);
                return;
            }

            if (node.Name == "recipe" && owner?.Parent?.Name == "bills")
            {
                Remove(owner);
                return;
            }

            if (node.Name == "def" && owner?.Name == "curJob")
            {
                ClearCurrentJob(owner.Parent);
                return;
            }

            if (node.Name == "def" && plan.ThingDefs.Contains(node.Value) && owner?.Element("id") != null)
            {
                Remove(owner);
                return;
            }

            if (node.Name == "def" && plan.GeneDefs.Contains(node.Value))
            {
                XElement gene = node.Ancestors().FirstOrDefault(item => item.Parent != null &&
                    (item.Parent.Name == "xenogenes" || item.Parent.Name == "endogenes"));
                if (gene != null)
                {
                    Remove(gene);
                    return;
                }
            }

            if (node.Name == "def" && plan.CampObjectDefs.Contains(node.Value) && owner?.Element("ID") != null)
            {
                Remove(owner);
                return;
            }

            XElement record = node.Ancestors().FirstOrDefault(item => item.Parent != null &&
                RecordParents.Contains(item.Parent.Name.LocalName));
            if (record != null)
            {
                Remove(record);
                return;
            }

            XElement logEntry = node.Ancestors().FirstOrDefault(item => item.Parent?.Name == "entries" &&
                item.Ancestors().Any(ancestor => ancestor.Name == "battleLog" || ancestor.Name == "playLog"));
            if (logEntry != null)
            {
                Remove(logEntry);
                return;
            }

            if (node.Name == "def" && (owner?.Name == "duty" || owner?.Name == "mentalState" ||
                (owner?.Name == "curState" && owner.Parent?.Name == "mentalStateHandler")))
            {
                Remove(owner);
                return;
            }

            XElement queued = node.Ancestors().FirstOrDefault(item => item.Name == "li" && item.Parent?.Name == "jobs");
            if (queued != null)
            {
                Remove(queued);
                return;
            }

            throw new InvalidOperationException("Unsupported mod reference at " + PathOf(node) + " = " + node.Value);
        }

        void ClearCurrentJob(XElement jobs)
        {
            if (jobs == null)
            {
                return;
            }

            Remove(jobs.Element("curJob"));
            Remove(jobs.Element("curDriver"));
        }

        void Remove(XElement node)
        {
            if (node?.Parent == null)
            {
                return;
            }

            foreach (XElement entry in node.DescendantsAndSelf())
            {
                Remember(entry);
            }

            if (node.Parent.Name == "keys" || node.Parent.Name == "values")
            {
                XElement parent = node.Parent;
                int index = 0;
                foreach (XElement sibling in parent.Elements())
                {
                    if (sibling == node)
                    {
                        break;
                    }

                    index++;
                }

                string paired = parent.Name == "keys" ? "values" : "keys";
                XElement pair = parent.Parent?.Element(paired);
                if (pair != null)
                {
                    int cursor = 0;
                    foreach (XElement sibling in pair.Elements())
                    {
                        if (cursor == index)
                        {
                            sibling.Remove();
                            break;
                        }

                        cursor++;
                    }
                }
            }

            node.Remove();
            RemovedEntries++;
        }

        void Remember(XElement entry)
        {
            if (entry.Parent?.Name == "quests" && entry.Element("root") != null)
            {
                AddReference(entry, "id", "Quest_");
                string questId = (string)entry.Element("id");
                int index = 0;
                foreach (XElement part in entry.Element("parts")?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    removedReferences.Add("QuestPart_" + questId + "_" + index);
                    index++;
                }
            }

            if (entry.Element("def") != null && (entry.Name == "worldObject" || entry.Parent?.Name == "worldObjects"))
            {
                AddReference(entry, "ID", "WorldObject_");
            }

            if (entry.Parent?.Name == "bills" && entry.Element("recipe") != null)
            {
                AddReference(entry, "loadID", "Bill_" + entry.Element("recipe").Value + "_");
            }

            if (entry.Element("id") != null && entry.Element("def") != null)
            {
                removedReferences.Add("Thing_" + entry.Element("id").Value);
            }

            if (entry.Parent?.Name == "xenogenes" || entry.Parent?.Name == "endogenes")
            {
                AddReference(entry, "loadID", "Gene_");
            }

            if (entry.Parent?.Name == "hediffs")
            {
                AddReference(entry, "loadID", "Hediff_");
            }

            if (entry.Parent?.Name == "lords")
            {
                AddReference(entry, "loadID", "Lord_");
            }

            if (entry.Name == "curJob" || entry.Name == "job")
            {
                AddReference(entry, "loadID", "Job_");
            }

            if (entry.Parent?.Name == "archivables" || entry.Parent?.Name == "letters")
            {
                AddReference(entry, "ID", "Letter_");
            }

            if (entry.Parent?.Name == "areas" && IsOwnedClass(entry))
            {
                AddReference(entry, "ID", "Area_", "_RHAH_Relief");
            }
        }

        void AddReference(XElement node, string idField, string prefix, string suffix = "")
        {
            if (node.Element(idField) != null)
            {
                removedReferences.Add(prefix + node.Element(idField).Value + suffix);
            }
        }

        void StripPackage(XElement meta)
        {
            XElement ids = meta?.Element("modIds");
            if (ids == null)
            {
                return;
            }

            List<XElement> entries = ids.Elements().ToList();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(entries[i].Value, plan.PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                RemoveAt(meta.Element("modNames"), i);
                RemoveAt(meta.Element("modSteamIds"), i);
                entries[i].Remove();
            }
        }

        static void RemoveAt(XElement parent, int index)
        {
            if (parent == null)
            {
                return;
            }

            int cursor = 0;
            foreach (XElement child in parent.Elements())
            {
                if (cursor == index)
                {
                    child.Remove();
                    return;
                }

                cursor++;
            }
        }

        static string PathOf(XElement node)
        {
            return string.Join("/", node.AncestorsAndSelf().Reverse().Select(item => item.Name.LocalName));
        }
    }
}
