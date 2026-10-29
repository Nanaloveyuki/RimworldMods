using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using IrisMenus;
using UnityEngine;
using Verse;

internal static class AdapterChecks
{
    internal static void Run(Action<bool, string> check)
    {
        CharacterEditorFlow(check);
        XmlSettingsFlow(check);
        Lifecycle(check);
    }

    private static void CharacterEditorFlow(Action<bool, string> check)
    {
        var owner = new CharacterEditor.CharacterEditorMod();
        LoadedModManager.ModHandles.Add(owner);
        string id = MenuRegistry.OwnerId(owner);
        IrisMenusMod.Instance.Settings.EnabledAdapters.Add(id);
        Find.WindowStack.Windows.Clear();
        CharacterEditor.CEditor.ModOptions.Reset();
        try
        {
            MenuPage page = MenuRegistry.Adaptable().Single(candidate => candidate.Id == id);
            var session = new MenuSession((_, exception) => throw exception);
            session.Select(page);
            page.BindHostClose(() => session.Select(null));
            Rect rect = new Rect(120f, 80f, 520f, 400f);
            for (int i = 0; i < 6; i++) session.Draw(rect);
            check(owner.Draws == 0 && CharacterEditor.CEditor.ModOptions.Creates == 1 &&
                CharacterEditor.CEditor.ModOptions.Draws == 6 && Find.WindowStack.Windows.Count == 0,
                "CharacterEditor settings body is automatically usable without launcher clicks or stacked windows");
            check(Widgets.Groups == 0 && Widgets.LastContent.width >= 800f,
                "CharacterEditor fixed native geometry remains reachable in a bounded narrow host");
            check(session.Save() && CharacterEditor.CEditor.ModOptions.SettingsSaves == 1 &&
                CharacterEditor.CEditor.ModOptions.SlotSaves == 1 && owner.Saves == 0,
                "CharacterEditor saves its real options and pawn slots rather than empty owner settings");
            session.Draw(rect);
            check(CharacterEditor.CEditor.ModOptions.Creates == 1, "CharacterEditor save does not recreate its active native body");
            CharacterEditor.CEditor.ModOptions.ClickSave = true;
            session.Draw(rect);
            check(session.Selected == null && CharacterEditor.CEditor.ModOptions.SettingsSaves == 2 &&
                CharacterEditor.CEditor.ModOptions.SlotSaves == 2 && Find.WindowStack.Windows.Count == 0,
                "CharacterEditor native Save exits to host navigation without duplicate saves or orphan windows");
            session.Select(page);
            page.BindHostClose(() => session.Select(null));
            session.Draw(rect);
            check(CharacterEditor.CEditor.ModOptions.Creates == 2,
                "CharacterEditor settings reopen automatically after native close");
            session.Select(null);
            MenuPage routed = MenuRegistry.ResolveEntry(owner);
            routed.Draw(rect);
            check(owner.Draws == 0 && Find.WindowStack.Windows.Count == 0,
                "Original CharacterEditor entry routing shares the integrated adapter");
            routed.Save();
            routed.Deactivate();
            MenuRegistry.Register(owner, "native", () => "Native", _ => { });
            check(!MenuRegistry.ResolveEntry(owner).Adapted && MenuRegistry.Adaptable().All(candidate => candidate.Id != id),
                "Explicit CharacterEditor registration supersedes compatibility hosting");
        }
        finally
        {
            Find.WindowStack.Windows.Clear();
            IrisMenusMod.Instance.Settings.EnabledAdapters.Remove(id);
            LoadedModManager.ModHandles.Remove(owner);
        }
    }

    private static void XmlSettingsFlow(Action<bool, string> check)
    {
        var storage = new XmlExtensions.XmlMod();
        var owner = new XmlExtensions_Mod_FrozenSnowFoxTweaksSettings();
        LoadedModManager.ModHandles.Add(storage);
        LoadedModManager.ModHandles.Add(owner);
        string id = MenuRegistry.OwnerId(owner);
        IrisMenusMod.Instance.Settings.EnabledAdapters.Add(id);
        XmlExtensions.BaseSettingsWindow.Reset();
        Find.WindowStack.Windows.Clear();
        var sentinel = new Window();
        Find.WindowStack.Add(sentinel);
        int failures = 0;
        var session = new MenuSession((_, exception) =>
        {
            if (!XmlExtensions.BaseSettingsWindow.FailWrite) throw exception;
            failures++;
        });
        try
        {
            MenuPage page = MenuRegistry.ResolveEntry(owner);
            session.Select(page);
            page.BindHostClose(() => session.Select(null));
            Rect rect = new Rect(120f, 80f, 520f, 400f);
            var menu = XmlExtensions_Mod_FrozenSnowFoxTweaksSettings.settingsMenuDef;
            menu.Next = () => menu.Value++;
            for (int i = 0; i < 6; i++) session.Draw(rect);
            check(owner.Draws == 0 && XmlExtensions.BaseSettingsWindow.Creates == 1 && menu.Opens == 1 && menu.Draws == 6,
                "FSF emitted launcher is bypassed and native settings activate only once across GUI events");
            check(menu.LastRect.x == 0f && menu.LastRect.y == 0f && menu.LastRect.width == rect.width &&
                menu.LastRect.height == rect.height && Widgets.Groups == 0 && menu.Value == 1,
                "FSF native body gets local bounded geometry and functional input");
            check(Find.WindowStack.Windows.Count == 1 && ReferenceEquals(Find.WindowStack.Windows[0], sentinel),
                "FSF host never opens original windows or consumes window-stack input");
            XmlExtensions.BaseSettingsWindow.FailWrite = true;
            check(!session.Select(null) && session.Selected == page && failures == 1,
                "FSF native storage failure keeps the selected page for retry");
            XmlExtensions.BaseSettingsWindow.FailWrite = false;
            check(session.Select(null) && storage.Saves == 1 && menu.Closes == 1 &&
                XmlExtensions.BaseSettingsWindow.AppliedValues.SequenceEqual(new[] { 1 }),
                "FSF exit preserves native keyed actions, PostClose and real XML Extensions saves");
            session.Select(page);
            page.BindHostClose(() => session.Select(null));
            session.Draw(rect);
            check(XmlExtensions.BaseSettingsWindow.Creates == 2 && menu.Opens == 2,
                "FSF settings reopen with one fresh native session");
            menu.Next = () => new XmlExtensions.Action.CloseMenu().Run();
            session.Draw(rect);
            check(session.Selected == null && menu.Closes == 2 && storage.Saves == 2 && Find.WindowStack.Windows.Count == 1,
                "XML Extensions native CloseMenu exits integrated hosting without locking or closing unrelated windows");
            session.Select(page);
            page.BindHostClose(() => session.Select(null));
            session.Draw(rect);
            menu.Next = () => new XmlExtensions.Action.RestartGame().Run();
            session.Draw(rect);
            check(session.Selected == null && storage.Saves == 3 && GenCommandLine.Restarts == 1,
                "XML Extensions restart action saves and deactivates before invoking its native restart");
            MenuRegistry.Register(owner, "native", () => "Native", _ => { });
            check(!MenuRegistry.ResolveEntry(owner).Adapted, "Explicit FSF registration retains precedence over launcher compatibility");
        }
        finally
        {
            session.Deactivate();
            Find.WindowStack.Windows.Clear();
            IrisMenusMod.Instance.Settings.EnabledAdapters.Remove(id);
            LoadedModManager.ModHandles.Remove(owner);
            LoadedModManager.ModHandles.Remove(storage);
        }
    }

    private static void Lifecycle(Action<bool, string> check)
    {
        int deactivations = 0;
        bool fail = true;
        bool unbound = false;
        var first = new MenuPage("life:first", new Mod(), () => "First", _ => { },
            () => { if (fail) throw new InvalidOperationException("save"); }, false)
        {
            Deactivate = () => deactivations++,
            BindHostClose = close => unbound = close == null
        };
        var second = new MenuPage("life:second", new Mod(), () => "Second", _ => { }, () => { }, false);
        var session = new MenuSession((_, __) => { });
        session.Select(first);
        check(!session.Select(second) && deactivations == 0 && !unbound, "Failed saves retain native session and close binding");
        fail = false;
        check(session.Select(second) && deactivations == 1 && unbound, "Successful leave deactivates and unbinds the previous native session");
        session.Select(first);
        session.Rebind(second);
        check(deactivations == 2, "Registry replacement deactivates removed native session without another save");
    }
}

namespace CharacterEditor
{
    internal sealed class CharacterEditorMod : Mod
    {
        public override string SettingsCategory() => "CharacterEditor";
        public override void DoSettingsWindowContents(Rect rect)
        {
            Draws++;
            Find.WindowStack.Add(new CEditor.ModOptions.DialogConfigurate());
            foreach (Window window in Find.WindowStack.Windows.OfType<RimWorld.Dialog_ModSettings>().ToArray())
                Find.WindowStack.Windows.Remove(window);
        }
    }

    internal static class CEditor
    {
        internal sealed class ModOptions
        {
            internal static int Creates, Draws, SettingsSaves, SlotSaves;
            internal static bool ClickSave;
            internal static void Reset() { Creates = Draws = SettingsSaves = SlotSaves = 0; ClickSave = false; }
            private void SaveSettings() { SettingsSaves++; }
            private void SaveSlots() { SlotSaves++; }
            internal sealed class DialogConfigurate : Window
            {
                private readonly ModOptions mo = new ModOptions();
                internal DialogConfigurate() { Creates++; }
                public override void DoWindowContents(Rect rect)
                {
                    Draws++;
                    if (!ClickSave) return;
                    ClickSave = false;
                    mo.SaveSettings();
                    mo.SaveSlots();
                    Close();
                }
            }
        }
    }
}

// Mirrors the installed XML Extensions dynamically emitted launcher's exact type/field contract.
internal sealed class XmlExtensions_Mod_FrozenSnowFoxTweaksSettings : Mod
{
    public static XmlExtensions.SettingsMenuDef settingsMenuDef = new XmlExtensions.SettingsMenuDef();
    public XmlExtensions_Mod_FrozenSnowFoxTweaksSettings()
    {
        Content.PackageIdPlayerFacing = "FrozenSnowFox.FrozenSnowFoxTweaks";
    }
    public override string SettingsCategory() => "FrozenSnowFox Tweaks";
    public override void DoSettingsWindowContents(Rect rect)
    {
        Draws++;
        Find.WindowStack.Add(new XmlExtensions.ModSettingsWindow(settingsMenuDef, false));
    }
}

namespace XmlExtensions
{
    internal sealed class XmlMod : Mod { }
    internal sealed class SettingsMenuDef
    {
        internal int Opens, Closes, Draws, Value;
        internal Rect LastRect;
        internal System.Action Next;
        internal void DrawSettingsMenu(Rect rect)
        {
            LastRect = rect;
            Draws++;
            System.Action next = Next;
            Next = null;
            next?.Invoke();
        }
    }
    internal class BaseSettingsWindow : Window
    {
        public static SettingsMenuDef activeMenu;
        public static SettingsMenuDef SelectedMod;
        public int shouldClose;
        internal static int Creates, OldValue;
        internal static bool FailWrite;
        internal static List<int> AppliedValues = new List<int>();
        internal static void Reset()
        {
            activeMenu = SelectedMod = null;
            Creates = OldValue = 0;
            FailWrite = false;
            AppliedValues.Clear();
            XmlExtensions_Mod_FrozenSnowFoxTweaksSettings.settingsMenuDef = new SettingsMenuDef();
        }
    }
    internal sealed class ModSettingsWindow : BaseSettingsWindow
    {
        public ModSettingsWindow(SettingsMenuDef initialMenu, bool isXmlExtensions)
        {
            Creates++;
            activeMenu = SelectedMod = initialMenu;
            initialMenu.Opens++;
            OldValue = initialMenu.Value;
        }
        public override void PreClose()
        {
            if (SelectedMod != null)
            {
                AppliedValues.Add(SelectedMod.Value - OldValue);
                activeMenu.Closes++;
                activeMenu = SelectedMod = null;
            }
            if (FailWrite) throw new InvalidOperationException("storage");
            LoadedModManager.ModHandles.OfType<XmlMod>().Single().WriteSettings();
        }
    }
}

namespace XmlExtensions.Action
{
    internal sealed class CloseMenu
    {
        internal bool Run() => ApplyAction();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyAction()
        {
            BaseSettingsWindow window = Find.WindowStack.WindowOfType<BaseSettingsWindow>();
            if (window != null) window.shouldClose = 1;
            return true;
        }
    }
    internal sealed class RestartGame
    {
        internal bool Run() => ApplyAction();
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyAction()
        {
            BaseSettingsWindow window = Find.WindowStack.WindowOfType<BaseSettingsWindow>();
            if (window != null) window.shouldClose = 2;
            return true;
        }
    }
}
