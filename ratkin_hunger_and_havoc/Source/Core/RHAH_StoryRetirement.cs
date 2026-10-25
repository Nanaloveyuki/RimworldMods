using System;
using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using Verse;

namespace HungerAndHavoc.Core
{
    [HarmonyPatch(typeof(Game), "ExposeSmallComponents")]
    internal static class RHAH_StoryRetirement
    {
        static readonly HashSet<string> letterDefs = new HashSet<string>(StringComparer.Ordinal)
        {
            "RHAH_GrainHoleLetter", "RHAH_EnvoyLetter", "RHAH_RevisitLetter", "RHAH_RecordLetter", "RHAH_KingdomArcLetter"
        };
        static readonly HashSet<string> letterClasses = new HashSet<string>(StringComparer.Ordinal)
        {
            "HungerAndHavoc.Incidents.ChoiceLetter_RHAH_GrainHole",
            "HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Envoy",
            "HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Revisit",
            "HungerAndHavoc.Storyteller.Suiyin.ChoiceLetter_RHAH_KingdomArc"
        };
        static readonly HashSet<string> thingDefs = new HashSet<string>(StringComparer.Ordinal)
        {
            "RHAH_GrainHole", "RHAH_RecordBox", "RHAH_MigrationRecord", "RHAH_KingdomMaterial"
        };
        static readonly HashSet<string> thoughtDefs = new HashSet<string>(StringComparer.Ordinal)
        {
            "RHAH_Thought_LeftAlone", "RHAH_Thought_CaptiveYear", "RHAH_Thought_FamilyHere", "RHAH_Thought_Regret"
        };

        static void Prefix()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) Clean(Scribe.loader.curXmlParent);
        }

        // 在深存档类型与 Def 解析前处理内存 XML 不写回原档
        internal static void Clean(XmlNode game)
        {
            if (game?.Name != "game") return;
            HashSet<string> removed = new HashSet<string>(StringComparer.Ordinal);
            List<XmlNode> nodes = new List<XmlNode>();
            Collect(game, nodes);
            ReleaseLegacyEnvoys(game, nodes);
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                XmlNode node = nodes[i];
                string def = node["def"]?.InnerText;
                string type = node.Attributes?["Class"]?.Value;
                if (letterDefs.Contains(def ?? "") || letterClasses.Contains(type ?? ""))
                {
                    Remember(node, "ID", "Letter_", removed);
                    node.ParentNode?.RemoveChild(node);
                }
                else if (thingDefs.Contains(def ?? ""))
                {
                    Remember(node, "id", "Thing_", removed);
                    node.ParentNode?.RemoveChild(node);
                }
                else if (thoughtDefs.Contains(def ?? ""))
                {
                    node.ParentNode?.RemoveChild(node);
                }
                else if (type == "HungerAndHavoc.Storyteller.Suiyin.CaravanArrivalAction_RHAH_KingdomSite")
                {
                    node.ParentNode?.RemoveChild(node);
                }
            }

            XmlNode objects = game["world"]?["worldObjects"]?["worldObjects"];
            if (objects != null)
            {
                for (int i = objects.ChildNodes.Count - 1; i >= 0; i--)
                {
                    XmlNode site = objects.ChildNodes[i];
                    string def = site["def"]?.InnerText;
                    if (def != "RHAH_KingdomSite" && def != "RHAH_RecordSite") continue;
                    if (def == "RHAH_KingdomSite") TransferSupply(game, site);
                    string reference = "WorldObject_" + site["ID"]?.InnerText;
                    bool mapped = false;
                    XmlNode maps = game["maps"];
                    if (maps != null)
                    {
                        foreach (XmlNode map in maps.ChildNodes)
                        {
                            if (map["mapInfo"]?["parent"]?.InnerText != reference) continue;
                            mapped = true;
                            if (map["generatorDef"]?.InnerText == "RHAH_RecordSite") map["generatorDef"].InnerText = "Encounter";
                        }
                    }
                    if (mapped)
                    {
                        XmlAttribute className = site.Attributes["Class"] ?? site.OwnerDocument.CreateAttribute("Class");
                        className.Value = "RimWorld.Planet.Site";
                        site.Attributes.SetNamedItem(className);
                        site["def"].InnerText = "Site";
                        RemoveChild(site, "parts");
                        RemoveChild(site, "kingdomKind");
                        RemoveChild(site, "kingdomSupply");
                        RemoveChild(site, "kingdomRecipients");
                    }
                    else
                    {
                        removed.Add(reference);
                        objects.RemoveChild(site);
                    }
                }
            }
            RemoveReferences(game, removed);
        }

        static void ReleaseLegacyEnvoys(XmlNode game, List<XmlNode> nodes)
        {
            HashSet<string> legacy = new HashSet<string>(StringComparer.Ordinal);
            XmlNode components = game["components"];
            if (components == null) return;
            foreach (XmlNode component in components.ChildNodes)
            {
                if (component.Attributes?["Class"]?.Value != "HungerAndHavoc.Narrative.NarrativeState") continue;
                XmlNode cases = component["envoyCases"];
                if (cases == null) continue;
                foreach (XmlNode record in cases.ChildNodes)
                {
                    if (!string.Equals(record["holdMigrated"]?.InnerText, "true", StringComparison.OrdinalIgnoreCase) && record["pawnId"] != null)
                        legacy.Add(record["pawnId"].InnerText);
                }
            }

            foreach (XmlNode mark in nodes)
            {
                if (mark["def"]?.InnerText != "RHAH_HungerMark" || mark["sourceIncidentDisplayId"]?.InnerText != "N-008") continue;
                XmlNode pawn = mark.ParentNode;
                while (pawn != null && pawn["id"] == null) pawn = pawn.ParentNode;
                if (pawn == null || !legacy.Contains(pawn["id"].InnerText)) continue;
                XmlNode extras = mark["extraData"]?["keys"];
                bool held = false;
                if (extras != null)
                {
                    foreach (XmlNode key in extras.ChildNodes)
                        if (key.InnerText == "rhah:envoyHold" || key.InnerText == "rhah:longHold") held = true;
                }
                if (held) continue;
                XmlNode keys = mark["gateOverrides"]?["keys"];
                XmlNode values = mark["gateOverrides"]?["values"];
                if (keys == null || values == null || keys.ChildNodes.Count != values.ChildNodes.Count) continue;
                int fed = -1;
                int exit = -1;
                for (int i = 0; i < keys.ChildNodes.Count; i++)
                {
                    if (keys.ChildNodes[i].InnerText == "LeaveAfterFed" || keys.ChildNodes[i].InnerText == "3") fed = i;
                    if (keys.ChildNodes[i].InnerText == "ExitMap" || keys.ChildNodes[i].InnerText == "15") exit = i;
                }
                if (fed < 0 || exit < 0 || !string.Equals(values.ChildNodes[fed].InnerText, "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(values.ChildNodes[exit].InnerText, "false", StringComparison.OrdinalIgnoreCase)) continue;
                int first = Math.Max(fed, exit);
                int second = Math.Min(fed, exit);
                keys.RemoveChild(keys.ChildNodes[first]);
                values.RemoveChild(values.ChildNodes[first]);
                keys.RemoveChild(keys.ChildNodes[second]);
                values.RemoveChild(values.ChildNodes[second]);
            }
        }

        static void TransferSupply(XmlNode game, XmlNode site)
        {
            XmlNode supply = site["kingdomSupply"];
            if (supply == null || supply.ChildNodes.Count == 0) return;
            List<XmlNode> inventories = new List<XmlNode>();
            XmlNode pawns = game["world"]?["worldPawns"]?["pawnsAlive"];
            XmlNode recipients = site["kingdomRecipients"];
            if (pawns != null && recipients != null)
            {
                foreach (XmlNode recipient in recipients.ChildNodes)
                {
                    foreach (XmlNode pawn in pawns.ChildNodes)
                    {
                        if ("Thing_" + pawn["id"]?.InnerText != recipient.InnerText) continue;
                        XmlNode inventory = pawn["inventory"];
                        XmlNode container = inventory?["innerContainer"];
                        XmlNode innerList = container?["innerList"];
                        if (!IsNull(pawn) && !IsNull(inventory) && !IsNull(container) && !IsNull(innerList) && !inventories.Contains(innerList)) inventories.Add(innerList);
                    }
                }
            }
            if (inventories.Count == 0) throw new InvalidOperationException("Cannot retire Suiyin site " + site["ID"]?.InnerText + ": no recipient inventory for its existing supply.");
            int index = 0;
            while (supply.FirstChild != null)
            {
                XmlNode item = supply.FirstChild;
                supply.RemoveChild(item);
                inventories[index++ % inventories.Count].AppendChild(item);
            }
        }

        static bool IsNull(XmlNode node)
        {
            return node == null || string.Equals(node.Attributes?["IsNull"]?.Value, "True", StringComparison.OrdinalIgnoreCase);
        }

        static void Collect(XmlNode node, List<XmlNode> result)
        {
            if (node.NodeType != XmlNodeType.Element) return;
            result.Add(node);
            foreach (XmlNode child in node.ChildNodes) Collect(child, result);
        }

        static void Remember(XmlNode node, string key, string prefix, HashSet<string> removed)
        {
            if (node[key] != null) removed.Add(prefix + node[key].InnerText);
        }

        static void RemoveReferences(XmlNode node, HashSet<string> removed)
        {
            for (int i = node.ChildNodes.Count - 1; i >= 0; i--)
            {
                XmlNode child = node.ChildNodes[i];
                if (child.NodeType != XmlNodeType.Element) continue;
                if (child.ChildNodes.Count == 1 && child.FirstChild.NodeType == XmlNodeType.Text && removed.Contains(child.InnerText))
                {
                    if (child.Name == "li") node.RemoveChild(child);
                    else child.InnerText = "null";
                }
                else RemoveReferences(child, removed);
            }
        }

        static void RemoveChild(XmlNode parent, string name)
        {
            XmlNode child = parent[name];
            if (child != null) parent.RemoveChild(child);
        }

        internal static void RestoreVisitorGates(RHAH_PawnState state)
        {
            if (!state.TryGetExtra("rhah:envoyHold", out string previous)) return;
            state.SetExtra("rhah:envoyHold", null);
            if (previous == null || previous.Length != 2 || state.TryGetExtra("rhah:longHold", out _)) return;
            RestoreGate(state, RHAH_BehaviorGate.LeaveAfterFed, previous[0]);
            RestoreGate(state, RHAH_BehaviorGate.ExitMap, previous[1]);
        }

        static void RestoreGate(RHAH_PawnState state, RHAH_BehaviorGate gate, char previous)
        {
            if (state.GetGateOverride(gate) == false && (previous == 'u' || previous == 't' || previous == 'f'))
                state.SetGate(gate, previous == 'u' ? (bool?)null : previous == 't');
        }
    }
}
