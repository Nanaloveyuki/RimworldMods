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
    internal static class RHAH_QuarantineMigration
    {
        const string RetiredLetterClass = "HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Quarantine";
        const string RetiredHoldKey = "rhah:quarantineHold";

        static void Prefix()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Clean(Scribe.loader.curXmlParent);
            }
        }

        // 在 Verse 创建深存档对象前移除退休类型 原文件保持不变
        internal static void Clean(XmlNode game)
        {
            if (game?.Name != "game") return;

            HashSet<string> removedLetters = new HashSet<string>(StringComparer.Ordinal);
            RemoveLetters(game["history"]?["archive"]?["archivables"], removedLetters);
            RemoveLetters(game["letterStack"]?["letters"], removedLetters);
            RemoveLetters(game["letterStack"]?["letterQueue"], removedLetters);
            RemoveReferences(game["letterStack"]?["letters"], removedLetters);
            RemoveReferences(game["letterStack"]?["letterQueue"], removedLetters);

            XmlNode components = game["components"];
            if (components == null) return;
            foreach (XmlNode component in components.ChildNodes)
            {
                string type = component.Attributes?["Class"]?.Value;
                if (type == "HungerAndHavoc.Core.GameComponent_RHAH_Game")
                {
                    RemoveChild(component, "plagueReturnLoadId");
                    RemoveChild(component, "plagueReturnMapId");
                    RemoveChild(component, "plagueReturnPhase");
                    RemoveChild(component, "plagueReturnDueTick");
                    RemoveChild(component, "plagueReturnLeaveTick");
                }
                else if (type == "HungerAndHavoc.Narrative.NarrativeState")
                {
                    RemoveChild(component, "quarantineCases");
                    XmlNode notices = component["pendingNotices"];
                    if (notices != null)
                    {
                        for (int i = notices.ChildNodes.Count - 1; i >= 0; i--)
                        {
                            XmlNode notice = notices.ChildNodes[i];
                            string letter = notice["letter"]?.InnerText;
                            if (RetiredLetter(letter) ||
                                ((letter == "Aside" || letter == "57") && notice["arg"]?.InnerText == "3"))
                            {
                                notices.RemoveChild(notice);
                            }
                        }
                    }
                    ResetSlot(component["suiyinEnabled"], "False");
                    ResetSlot(component["suiyinStarted"], "False");
                    ResetSlot(component["suiyinDeadlineTick"], "-1");
                    XmlNode asides = component["asidesSent"];
                    if (asides != null)
                    {
                        for (int i = asides.ChildNodes.Count - 1; i >= 0; i--)
                        {
                            if (asides.ChildNodes[i].InnerText == "3") asides.RemoveChild(asides.ChildNodes[i]);
                        }
                    }
                }
            }
        }

        static bool RetiredLetter(string letter)
        {
            return letter != null && (letter.StartsWith("N007", StringComparison.Ordinal) ||
                (int.TryParse(letter, out int value) && ((value >= 30 && value <= 38) || value == 58)));
        }

        static void RemoveLetters(XmlNode list, HashSet<string> removed)
        {
            if (list == null) return;
            for (int i = list.ChildNodes.Count - 1; i >= 0; i--)
            {
                XmlNode letter = list.ChildNodes[i];
                if (letter.Attributes?["Class"]?.Value != RetiredLetterClass &&
                    letter["def"]?.InnerText != "RHAH_QuarantineLetter") continue;
                removed.Add("Letter_" + (letter["ID"]?.InnerText ?? "0"));
                list.RemoveChild(letter);
            }
        }

        static void RemoveReferences(XmlNode list, HashSet<string> removed)
        {
            if (list == null) return;
            for (int i = list.ChildNodes.Count - 1; i >= 0; i--)
            {
                if (removed.Contains(list.ChildNodes[i].InnerText)) list.RemoveChild(list.ChildNodes[i]);
            }
        }

        static void RemoveChild(XmlNode parent, string name)
        {
            XmlNode child = parent[name];
            if (child != null) parent.RemoveChild(child);
        }

        static void ResetSlot(XmlNode list, string value)
        {
            if (list != null && list.ChildNodes.Count > 6) list.ChildNodes[6].InnerText = value;
        }

        internal static void RestoreVisitorGates(RHAH_PawnState state)
        {
            if (!state.TryGetExtra(RetiredHoldKey, out string previous)) return;
            state.SetExtra(RetiredHoldKey, null);
            if (previous == null || previous.Length != 2 ||
                state.TryGetExtra("rhah:envoyHold", out _) || state.TryGetExtra("rhah:longHold", out _)) return;
            RestoreGate(state, RHAH_BehaviorGate.LeaveAfterFed, previous[0]);
            RestoreGate(state, RHAH_BehaviorGate.ExitMap, previous[1]);
        }

        static void RestoreGate(RHAH_PawnState state, RHAH_BehaviorGate gate, char previous)
        {
            if (state.GetGateOverride(gate) == false && (previous == 'u' || previous == 't' || previous == 'f'))
            {
                state.SetGate(gate, previous == 'u' ? (bool?)null : previous == 't');
            }
        }
    }
}
