using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using RimWorld;
using UnityEngine;
using Verse;
using UnityStubs.Gui;

namespace RimCarbon14.Mod
{
    public sealed class UiTools : IDisposable
    {
        private readonly BridgeHost host;
        private readonly Harmony harmony = new Harmony("nanaloveyuki.rimcarbon14.ui");
        private readonly List<Element> drawing = new List<Element>();
        private List<Element> elements = new List<Element>();
        private int ordinal;
        private int version;
        private int frame;
        private string signature = "";
        private string surface = "root";
        private int drawingFrame = -1;
        private Pending? pending;
        private bool captureEnabled = true;
        private static readonly Element Unobserved = new Element { Disabled = true };
        private static readonly AccessTools.FieldRef<FloatMenu, List<FloatMenuOption>> MenuOptions = AccessTools.FieldRefAccess<FloatMenu, List<FloatMenuOption>>("options");
        private static UiTools Self => BridgeHost.Instance.Ui;
        public UiTools(BridgeHost host) { this.host = host; }
        private sealed class Element
        {
            public string Id = ""; public string Kind = ""; public string Label = ""; public string Surface = ""; public Rect Rect; public bool Disabled; public JToken? Value;
            public JObject Json() => new JObject { ["id"] = Id, ["kind"] = Kind, ["label"] = Label, ["surface"] = Surface, ["screenRect"] = Wire.Token(new { x = Rect.x, y = Rect.y, width = Rect.width, height = Rect.height }), ["disabled"] = Disabled, ["value"] = Value };
        }
        private sealed class Pending { public Element Element = null!; public int Version; public JToken? Value; public bool Done; public string? Error; }
        public void Register()
        {
            host.AddRead("ui_layout", "Read controls from the latest real repaint. Physical pixels, top-left origin. IDs require layoutVersion for actions.", _ => Snapshot());
            host.Add("ui_action", "Activate a currently drawn standard button/toggle/text field/slider. Reject changed or obscured targets.", "write", false, Action, "elementId!", "layoutVersion!:integer", "value:any");
            host.Add("ui_tab", "Open a main game tab by MainButtonDef name.", "write", true, (a, _) => { Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed(Wire.Required(a, "def"))); return new JObject { ["opened"] = (string?)a["def"] }; }, "def!");
            host.AddRead("ui_windows", "List open game windows with object handles.", _ => new JArray(Find.WindowStack.Windows.Select(w => new JObject { ["handle"] = host.Handles.Add(w), ["type"] = w.GetType().FullName, ["surface"] = Surface(w), ["rect"] = Wire.Token(new { x = w.windowRect.x, y = w.windowRect.y, width = w.windowRect.width, height = w.windowRect.height }) })));
            host.Add("ui_close", "Close an explicit window using its normal Close method.", "write", false, (a, _) => { ((Window)host.Handles.Get(Wire.Required(a, "handle"))).Close(); return new JObject { ["closed"] = true }; }, "handle!");
            host.Add("pawn_gizmos", "Enumerate a pawn's real gizmos. Enumeration may execute mod code.", "invoke", true, (a, _) => new JArray(GameTools.Pawn(host, a).GetGizmos().Select(g => new JObject { ["handle"] = host.Handles.Add(g), ["type"] = g.GetType().FullName, ["label"] = (g as Command)?.Label, ["hotKey"] = (g as Command)?.hotKey?.defName, ["disabled"] = g.Disabled })), "handle!");
            host.Add("ui_gizmo", "Invoke an enabled Gizmo through ProcessInput; target-mode commands may need a subsequent map click.", "write", true, (a, _) =>
            {
                var g = (Gizmo)host.Handles.Get(Wire.Required(a, "handle"));
                if (g.Disabled) throw new BridgeException("disabled_control", "Gizmo disabled.");
                g.ProcessInput(new Event { type = EventType.MouseDown, button = 0 }); return new JObject { ["accepted"] = true };
            }, "handle!");
            host.Add("pawn_menu", "Build and open the vanilla context menu for a pawn at a map cell.", "write", true, (a, _) =>
            {
                var p = GameTools.Pawn(host, a); Current.Game.CurrentMap = p.Map;
                var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { p }, GameTools.Cell(a).ToVector3Shifted(), out var context);
                // The agent chooses in a later request, without necessarily hovering the real mouse.
                Find.WindowStack.Add(new FloatMenu(options) { givesColonistOrders = true, vanishIfMouseDistant = false });
                return new JArray(options.Select(o => new JObject { ["handle"] = host.Handles.Add(o), ["label"] = o.Label, ["disabled"] = o.Disabled }));
            }, "handle!", "x!:integer", "z!:integer");
            host.Add("ui_menu_choose", "Choose a currently available enabled FloatMenuOption.", "write", true, (a, _) =>
            {
                var option = (FloatMenuOption)host.Handles.Get(Wire.Required(a, "handle"));
                var menu = Find.WindowStack.Windows.OfType<FloatMenu>().FirstOrDefault(w => MenuOptions(w).Contains(option));
                if (menu == null) throw new BridgeException("stale_control", "The menu containing this option is no longer open.");
                if (option.Disabled || option.action == null) throw new BridgeException("disabled_control", "Menu option unavailable.");
                if (option.tutorTag != null && !TutorSystem.AllowAction(option.tutorTag)) throw new BridgeException("disabled_control", "The tutorial currently blocks this option.");
                option.Chosen(menu.givesColonistOrders, menu);
                if (option.tutorTag != null) TutorSystem.Notify_Event(option.tutorTag);
                menu.Close();
                return new JObject { ["accepted"] = true };
            }, "handle!");
        }
        public JObject Snapshot()
        {
            FinalizeFrame();
            return new JObject { ["layoutVersion"] = version, ["frame"] = frame, ["screenWidth"] = Screen.width, ["screenHeight"] = Screen.height, ["uiScale"] = Prefs.UIScale, ["elements"] = new JArray(elements.Select(e => e.Json())) };
        }
        public void FinalizeFrame()
        {
            if (drawingFrame < 0 || drawingFrame == frame) return;
            var next = string.Join("|", drawing.Select(e => e.Id + e.Label + e.Rect + e.Disabled));
            if (next != signature) { version++; signature = next; }
            elements = drawing.ToList(); frame = drawingFrame;
        }
        public bool HasCurrentRepaint => frame > 0 && Time.frameCount - frame <= 1;
        public void SetCaptureEnabled(bool enabled) { Reset(); captureEnabled = enabled; }
        private Element Resolve(string id, int expected)
        {
            if (expected != version) throw new BridgeException("stale_layout", "Get a new ui_layout before acting.");
            return elements.SingleOrDefault(e => e.Id == id) ?? throw new BridgeException("element_not_found", id);
        }
        public Rect ResolveRect(string id, int expected) => Resolve(id, expected).Rect;
        private JToken? Action(JObject a, Operation op)
        {
            if (op.Context == null)
            {
                var element = Resolve(Wire.Required(a, "elementId"), (int?)a["layoutVersion"] ?? -1);
                if (element.Disabled) throw new BridgeException("disabled_control", element.Label);
                if (!new[] { "button", "checkbox", "text", "slider", "scroll" }.Contains(element.Kind)) throw new BridgeException("unsupported_control", element.Kind);
                var request = new Pending { Element = element, Version = version, Value = a["value"] };
                pending = request; op.Context = request;
                var prior = op.Cleanup; op.Cleanup = () => { if (pending == request) pending = null; prior?.Invoke(); };
            }
            var action = (Pending)op.Context;
            if (action.Error != null) throw new BridgeException("stale_layout", action.Error);
            return action.Done ? new JObject { ["activated"] = true, ["elementId"] = action.Element.Id } : null;
        }
        public void Advance()
        {
            FinalizeFrame();
            if (pending != null && pending.Version != version && !pending.Done) pending.Error = "Layout changed before execution.";
        }
        public void Reset() { elements.Clear(); drawing.Clear(); drawingFrame = -1; version++; signature = ""; if (pending != null) pending.Error = "Game session changed."; }
        public void Dispose() { Reset(); harmony.UnpatchAll(harmony.Id); }
        private static string Surface(Window w) => w.GetType().FullName + ":" + w.ID;
        public void Install()
        {
            Patch(typeof(Root), "OnGUI", nameof(BeginGui), null);
            Patch(typeof(Window), "InnerWindowOnGUI", nameof(BeginWindow), nameof(EndWindow));
            foreach (var m in typeof(Widgets).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var p = m.GetParameters();
                if (m.Name == "ButtonText" && p.Length == 7) Patch(m, nameof(ButtonPrefix), nameof(ButtonPostfix));
                if (m.Name == "CheckboxLabeled" && p.Length == 8) Patch(m, nameof(CheckPrefix), nameof(CheckPostfix));
                if (m.Name == "Checkbox" && p.Length == 8) Patch(m, nameof(SmallCheckPrefix), nameof(CheckPostfix));
                if (m.Name == "HorizontalSlider" && p.Length == 9) Patch(m, nameof(SliderPrefix), nameof(SliderPostfix));
            }
            Patch(AccessTools.Method(typeof(GUI), "ButtonImpl", new[] { typeof(Rect), typeof(GUIContent), typeof(GUIStyle), typeof(bool) }), nameof(GuiButtonPrefix), nameof(ButtonPostfix));
            Patch(AccessTools.Method(typeof(GUI), "Toggle", new[] { typeof(Rect), typeof(bool), typeof(GUIContent), typeof(GUIStyle) }), nameof(GuiTogglePrefix), nameof(GuiTogglePostfix));
            Patch(AccessTools.Method(typeof(GUI), "Label", new[] { typeof(Rect), typeof(string), typeof(GUIStyle) }), nameof(GuiLabelPrefix), null);
            Patch(AccessTools.Method(typeof(GUI), "Label", new[] { typeof(Rect), typeof(GUIContent), typeof(GUIStyle) }), nameof(GuiContentLabelPrefix), null);
            Patch(AccessTools.Method(typeof(GUI), "TextField", new[] { typeof(Rect), typeof(string), typeof(int), typeof(GUIStyle) }), nameof(TextPrefix), nameof(TextPostfix));
            Patch(AccessTools.Method(typeof(GUI), "BeginScrollViewImpl", new[] { typeof(Rect), typeof(Vector2), typeof(Rect), typeof(GUIStyle), typeof(GUIStyle) }), nameof(ScrollPrefix), null);
        }
        private void Patch(Type type, string name, string? prefix, string? postfix) => Patch(AccessTools.Method(type, name), prefix, postfix);
        private void Patch(MethodBase method, string? prefix, string? postfix) => harmony.Patch(method, prefix == null ? null : new HarmonyMethod(typeof(UiTools), prefix), postfix == null ? null : new HarmonyMethod(typeof(UiTools), postfix));
        private sealed class WindowState { public string Surface = ""; public int Ordinal; }
        private static void BeginWindow(Window __instance, out WindowState __state)
        {
            var s = Self; __state = new WindowState { Surface = s.surface, Ordinal = s.ordinal };
            s.surface = Surface(__instance); s.ordinal = 0;
        }
        private static void EndWindow(WindowState __state)
        {
            Self.surface = __state.Surface; Self.ordinal = __state.Ordinal;
        }
        private static void BeginGui()
        {
            var s = Self; s.ordinal = 0; s.surface = "root";
            if (Event.current.type == EventType.Repaint) s.EnsureFrame();
        }
        private void EnsureFrame() { if (drawingFrame != Time.frameCount) { drawing.Clear(); drawingFrame = Time.frameCount; } }
        private static Vector2 ScreenPoint(Vector2 point, Vector2 origin)
        {
            var transformed = GUI.matrix.MultiplyPoint3x4(new Vector3(point.x + origin.x, point.y + origin.y, 0));
            return new Vector2(transformed.x, transformed.y);
        }
        private static Vector2 PhysicalPoint(Vector2 point)
        {
            var backend = GuiBackend.Current;
            var origin = Vector2.zero;
            if (backend != null && backend.GroupCount > 0) backend.GetGroup(backend.GroupCount - 1, out _, out origin);
            return ScreenPoint(point, origin);
        }
        private static Rect VisibleRect()
        {
            var visible = new Rect(0, 0, Screen.width, Screen.height);
            var backend = GuiBackend.Current;
            if (backend == null || backend.GroupCount == 0) return visible;
            var previousOrigin = Vector2.zero;
            for (int i = 0; i < backend.GroupCount; i++)
            {
                backend.GetGroup(i, out var group, out var origin);
                var top = ScreenPoint(group.position, previousOrigin);
                var bottom = ScreenPoint(new Vector2(group.xMax, group.yMax), previousOrigin);
                visible = Rect.MinMaxRect(Mathf.Max(visible.xMin, top.x), Mathf.Max(visible.yMin, top.y), Mathf.Min(visible.xMax, bottom.x), Mathf.Min(visible.yMax, bottom.y));
                previousOrigin = origin;
            }
            return visible;
        }
        private static Element Observe(string kind, Rect rect, string label, bool disabled, JToken? value)
        {
            if (!Self.captureEnabled) return Unobserved;
            var s = Self; var top = PhysicalPoint(rect.position); var bottom = PhysicalPoint(new Vector2(rect.xMax, rect.yMax));
            var e = new Element { Id = s.surface + ":" + (++s.ordinal), Surface = s.surface, Kind = kind, Label = label, Rect = Rect.MinMaxRect(top.x, top.y, bottom.x, bottom.y), Disabled = disabled || !GUI.enabled, Value = value };
            var visible = VisibleRect();
            var xMin = Mathf.Max(e.Rect.xMin, visible.xMin); var yMin = Mathf.Max(e.Rect.yMin, visible.yMin);
            var xMax = Mathf.Min(e.Rect.xMax, visible.xMax); var yMax = Mathf.Min(e.Rect.yMax, visible.yMax);
            e.Rect = new Rect(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
            if (Event.current.type == EventType.Repaint && e.Rect.width > 0 && e.Rect.height > 0) { s.EnsureFrame(); s.drawing.Add(e); }
            return e;
        }
        private static bool Consume(Element e)
        {
            var s = Self; var p = s.pending;
            if (p == null || p.Done || p.Error != null || Event.current.type != EventType.Repaint || p.Element.Id != e.Id) return false;
            if (p.Version != s.version || e.Kind != p.Element.Kind || e.Label != p.Element.Label || e.Rect != p.Element.Rect || e.Disabled) { p.Error = "Control identity, geometry or enabled state changed."; return false; }
            var point = e.Rect.center / Prefs.UIScale;
            var front = Find.WindowStack.Windows.LastOrDefault(w => w.windowRect.Contains(point));
            if (front != null && Surface(front) != e.Surface) { p.Error = "Control is obscured by another window."; return false; }
            p.Done = true; return true;
        }
        private static void ButtonPrefix(Rect rect, string label, bool active, out Element __state) => __state = Observe("button", rect, label, !active, null);
        private static void ButtonPostfix(Element __state, ref bool __result) { if (Consume(__state)) __result = true; }
        private static void CheckPrefix(Rect rect, string label, bool checkOn, bool disabled, out Element __state) => __state = Observe("checkbox", rect, label, disabled, checkOn);
        private static void CheckPostfix(Element __state, ref bool checkOn) { if (Consume(__state)) checkOn = (bool?)Self.pending!.Value ?? !checkOn; }
        private static void TextPrefix(Rect position, string text, out Element __state) => __state = Observe("text", position, "", false, text);
        private static void TextPostfix(Element __state, MethodBase __originalMethod, object[] __args, ref string __result)
        {
            if (!Consume(__state)) return;
            var text = (string?)Self.pending!.Value ?? "";
            var parameters = __originalMethod.GetParameters();
            var index = Array.FindIndex(parameters, p => p.Name == "maxLength");
            var maximum = index < 0 ? -1 : (int)__args[index];
            __result = maximum >= 0 && text.Length > maximum ? text.Substring(0, maximum) : text;
        }
        private static void SliderPrefix(Rect rect, float value, out Element __state) => __state = Observe("slider", rect, "", false, value);
        private static void SliderPostfix(Element __state, float min, float max, ref float __result) { if (Consume(__state)) __result = Mathf.Clamp((float?)Self.pending!.Value ?? __result, min, max); }
        private static void SmallCheckPrefix(float x, float y, bool checkOn, float size, bool disabled, out Element __state) => __state = Observe("checkbox", new Rect(x, y, size, size), "", disabled, checkOn);
        private static void GuiButtonPrefix(Rect position, GUIContent content, out Element __state) => __state = Observe("button", position, content.text ?? "", false, null);
        private static void GuiLabelPrefix(Rect position, string text) => Observe("label", position, text ?? "", false, null);
        private static void GuiContentLabelPrefix(Rect position, GUIContent content) { if (content?.image != null) Observe("label", position, content.text ?? "", false, null); }
        private static void GuiTogglePrefix(Rect position, bool value, GUIContent content, out Element __state) => __state = Observe("checkbox", position, content.text ?? "", false, value);
        private static void GuiTogglePostfix(Element __state, ref bool __result) { if (Consume(__state)) __result = (bool?)Self.pending!.Value ?? !__result; }
        private static void ScrollPrefix(Rect position, ref Vector2 scrollPosition, Rect viewRect)
        {
            var e = Observe("scroll", position, "", false, Wire.Token(new { x = scrollPosition.x, y = scrollPosition.y, maxX = Mathf.Max(0, viewRect.width - position.width), maxY = Mathf.Max(0, viewRect.height - position.height) }));
            if (Consume(e) && Self.pending!.Value is JObject value) scrollPosition = new Vector2(Mathf.Clamp((float?)value["x"] ?? scrollPosition.x, 0, Mathf.Max(0, viewRect.width - position.width)), Mathf.Clamp((float?)value["y"] ?? scrollPosition.y, 0, Mathf.Max(0, viewRect.height - position.height)));
        }
    }
}
