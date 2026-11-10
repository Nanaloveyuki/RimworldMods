using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    // Only inspected native settings contracts are hosted here. Ordinary adapters stay direct.
    internal abstract class NativeSettingsBody
    {
        private static readonly ConditionalWeakTable<Window, NativeSettingsBody> hosted = new ConditionalWeakTable<Window, NativeSettingsBody>();
        private static bool closePatchInstalled;
        private Action closeHost;
        private bool closeRequested;
        protected Window Dialog;

        protected NativeSettingsBody()
        {
            if (closePatchInstalled) return;
            var harmony = new Harmony("Nanaloveyuki.IrisMenus.NativeSettingsBody");
            harmony.Patch(AccessTools.Method(typeof(Window), nameof(Window.Close)),
                prefix: new HarmonyMethod(typeof(NativeSettingsBody), nameof(ClosePrefix)));
            closePatchInstalled = true;
        }

        internal void BindHostClose(Action close) => closeHost = close;
        internal abstract void Draw(Rect rect);
        internal abstract void Save();

        protected void Attach(Window dialog)
        {
            Dialog = dialog;
            hosted.Add(dialog, this);
            // The host owns the dialog; callers run any contract-specific initialization first.
            Vector2 size = dialog.InitialSize;
            dialog.windowRect = new Rect(0f, 0f, size.x, size.y);
        }

        protected void FinishDraw()
        {
            if (!closeRequested) return;
            closeRequested = false;
            closeHost?.Invoke();
        }

        protected virtual void NativeCloseRequested() { }

        private static bool ClosePrefix(Window __instance)
        {
            if (!hosted.TryGetValue(__instance, out NativeSettingsBody body)) return true;
            body.NativeCloseRequested();
            body.closeRequested = true;
            return false;
        }

        internal virtual void Deactivate()
        {
            Window dialog = Dialog;
            if (dialog == null) return;
            dialog.PreClose();
            dialog.PostClose();
            hosted.Remove(dialog);
            Dialog = null;
            closeRequested = false;
        }

        protected static T Bind<T>(object target, string name) where T : Delegate =>
            (T)AccessTools.Method(target.GetType(), name).CreateDelegate(typeof(T), target);
    }

    internal sealed class CharacterEditorSettingsBody : NativeSettingsBody
    {
        private readonly Type dialogType;
        private Action saveSettings;
        private Action saveSlots;
        private Vector2 scroll;
        private bool savedByNativeButton;

        internal CharacterEditorSettingsBody(Mod owner)
        {
            dialogType = owner.GetType().Assembly.GetType("CharacterEditor.CEditor+ModOptions+DialogConfigurate", true);
        }

        internal override void Draw(Rect rect)
        {
            if (Dialog == null)
            {
                var dialog = (Window)Activator.CreateInstance(dialogType, true);
                object options = AccessTools.Field(dialogType, "mo").GetValue(dialog);
                saveSettings = Bind<Action>(options, "SaveSettings");
                saveSlots = Bind<Action>(options, "SaveSlots");
                Attach(dialog);
            }
            // CE's body uses fixed 800px geometry rather than inRect. Preserve its own two
            // inner scroll views; the outer view only exposes a body larger than our viewport.
            float width = Math.Max(800f, rect.width - 20f);
            float height = Math.Max(Dialog.InitialSize.y - 36f, rect.height);
            Widgets.BeginScrollView(rect, ref scroll, new Rect(0f, 0f, width, height));
            try { Dialog.DoWindowContents(new Rect(0f, 0f, width, height)); }
            finally { Widgets.EndScrollView(); }
            FinishDraw();
        }

        protected override void NativeCloseRequested()
        {
            // The inspected native DoAndClose saves both files immediately before Close.
            savedByNativeButton = true;
        }

        internal override void Save()
        {
            if (Dialog == null) return;
            if (!savedByNativeButton)
            {
                saveSettings();
                saveSlots();
            }
            savedByNativeButton = false;
        }

        internal override void Deactivate()
        {
            base.Deactivate();
            saveSettings = null;
            saveSlots = null;
            savedByNativeButton = false;
        }
    }

    internal sealed class XmlExtensionsSettingsBody : NativeSettingsBody
    {
        private static readonly Dictionary<Type, XmlApi> apis = new Dictionary<Type, XmlApi>();
        private readonly object menu;
        private readonly XmlApi api;
        private readonly Action<int> routeClose;
        private Action<Rect> drawMenu;
        private object activeMenu;

        internal static bool Matches(Mod owner) => owner.GetType().Name.StartsWith("XmlExtensions_Mod_", StringComparison.Ordinal)
            && AccessTools.Field(owner.GetType(), "settingsMenuDef")?.FieldType.FullName == "XmlExtensions.SettingsMenuDef";

        internal XmlExtensionsSettingsBody(Mod owner)
        {
            FieldInfo menuField = AccessTools.Field(owner.GetType(), "settingsMenuDef");
            menu = menuField.GetValue(null) ?? throw new InvalidOperationException("XML Extensions settings menu is not initialized.");
            if (!apis.TryGetValue(menuField.FieldType, out XmlApi cached))
            {
                cached = new XmlApi(menuField.FieldType);
                apis.Add(menuField.FieldType, cached);
            }
            api = cached;
            routeClose = RequestClose;
        }

        internal override void Draw(Rect rect)
        {
            if (Dialog == null) Attach(api.CreateDialog(menu));
            object current = api.ActiveMenu();
            if (!ReferenceEquals(activeMenu, current))
            {
                activeMenu = current;
                drawMenu = current == null ? null : Bind<Action<Rect>>(current, "DrawSettingsMenu");
            }
            Action<int> previous = XmlExtensionsCloseRouter.Enter(routeClose);
            try
            {
                // DrawSettingsMenu opens its own group, then reuses the passed Rect for its
                // viewport. Local zero-origin coordinates avoid applying the host offset twice.
                GUI.BeginGroup(rect);
                try { drawMenu?.Invoke(new Rect(0f, 0f, rect.width, rect.height)); }
                finally { GUI.EndGroup(); }
            }
            finally { XmlExtensionsCloseRouter.Exit(previous); }
            int shouldClose = api.GetShouldClose(Dialog);
            if (shouldClose > 0)
            {
                api.SetShouldClose(Dialog, 0);
                Dialog.Close();
            }
            FinishDraw();
            if (shouldClose == 2 && Dialog == null) api.Restart();
        }

        internal override void Save()
        {
            if (Dialog == null) return;
            // Native PreClose runs keyed actions, closes the active submenu (PostClose),
            // and writes XmlMod's real shared storage, not the emitted launcher's ModSettings.
            Deactivate();
        }

        internal override void Deactivate()
        {
            base.Deactivate();
            activeMenu = null;
            drawMenu = null;
        }

        private void RequestClose(int mode) => api.SetShouldClose(Dialog, mode);

        private sealed class XmlApi
        {
            internal readonly Func<object, Window> CreateDialog;
            internal readonly Func<object> ActiveMenu;
            internal readonly Func<Window, int> GetShouldClose;
            internal readonly Action<Window, int> SetShouldClose;
            internal readonly Action Restart;

            internal XmlApi(Type menuType)
            {
                Assembly assembly = menuType.Assembly;
                Type windowType = assembly.GetType("XmlExtensions.ModSettingsWindow", true);
                Type baseType = windowType.BaseType;
                ConstructorInfo constructor = windowType.GetConstructor(new[] { menuType, typeof(bool) })
                    ?? throw new MissingMethodException(windowType.FullName, ".ctor");
                ParameterExpression menu = Expression.Parameter(typeof(object), "menu");
                CreateDialog = Expression.Lambda<Func<object, Window>>(
                    Expression.New(constructor, Expression.Convert(menu, menuType), Expression.Constant(false)), menu).Compile();
                ActiveMenu = Expression.Lambda<Func<object>>(Expression.Convert(
                    Expression.Field(null, AccessTools.Field(baseType, "activeMenu")), typeof(object))).Compile();
                ParameterExpression window = Expression.Parameter(typeof(Window), "window");
                ParameterExpression value = Expression.Parameter(typeof(int), "value");
                MemberExpression shouldClose = Expression.Field(Expression.Convert(window, baseType), AccessTools.Field(baseType, "shouldClose"));
                GetShouldClose = Expression.Lambda<Func<Window, int>>(shouldClose, window).Compile();
                SetShouldClose = Expression.Lambda<Action<Window, int>>(Expression.Assign(shouldClose, value), window, value).Compile();
                Restart = (Action)AccessTools.Method(typeof(GenCommandLine), "Restart").CreateDelegate(typeof(Action));
                XmlExtensionsCloseRouter.Install(assembly);
            }
        }
    }

    internal sealed class XmlToolkitSettingsBody : NativeSettingsBody
    {
        private readonly XmlToolkitApi api;
        private readonly Action<int> routeClose;
        private Vector2 scroll;
        private bool restartRequested;
        private static readonly Dictionary<Assembly, XmlToolkitApi> apis = new Dictionary<Assembly, XmlToolkitApi>();

        internal static bool Matches(Mod owner) => owner.GetType().FullName == "XmlExtensions.XmlMod"
            && owner.GetType().Assembly.GetType("XmlExtensions.XmlExtensionsMenuModSettings", false) != null;

        internal XmlToolkitSettingsBody(Mod owner)
        {
            Assembly assembly = owner.GetType().Assembly;
            if (!apis.TryGetValue(assembly, out XmlToolkitApi cached))
            {
                cached = new XmlToolkitApi(assembly);
                apis.Add(assembly, cached);
            }
            api = cached;
            routeClose = RequestClose;
            XmlExtensionsCloseRouter.Install(assembly);
        }

        internal override void Draw(Rect rect)
        {
            if (Dialog == null)
            {
                Window dialog = api.CreateDialog();
                // The native stack calls PreOpen once before drawing; this builds the toolkit's
                // mod list and initial selection without adding its window to WindowStack.
                dialog.PreOpen();
                Attach(dialog);
            }
            float width = Math.Max(Dialog.InitialSize.x, rect.width);
            float height = Math.Max(Dialog.InitialSize.y - 36f, rect.height);
            Widgets.BeginScrollView(rect, ref scroll, new Rect(0f, 0f, width, height));
            Action<int> previous = XmlExtensionsCloseRouter.Enter(routeClose);
            try { Dialog.DoWindowContents(new Rect(0f, 0f, width, height)); }
            finally
            {
                XmlExtensionsCloseRouter.Exit(previous);
                Widgets.EndScrollView();
            }
            bool restart = restartRequested;
            FinishDraw();
            restartRequested = restart && Dialog != null;
            if (restart && Dialog == null) GenCommandLine.Restart();
        }

        internal override void Save()
        {
            if (Dialog != null) Deactivate();
        }

        private void RequestClose(int mode)
        {
            restartRequested |= mode == 2;
            api.SetShouldClose(Dialog, 1);
        }

        private sealed class XmlToolkitApi
        {
            internal readonly Func<Window> CreateDialog;
            internal readonly Action<Window, int> SetShouldClose;

            internal XmlToolkitApi(Assembly assembly)
            {
                Type windowType = assembly.GetType("XmlExtensions.XmlExtensionsMenuModSettings", true);
                Type menuType = assembly.GetType("XmlExtensions.SettingsMenuDef", true);
                ConstructorInfo constructor = windowType.GetConstructor(new[] { menuType, typeof(bool) })
                    ?? throw new MissingMethodException(windowType.FullName, ".ctor");
                ParameterExpression window = Expression.Parameter(typeof(Window), "window");
                ParameterExpression value = Expression.Parameter(typeof(int), "value");
                Type baseType = assembly.GetType("XmlExtensions.BaseSettingsWindow", true);
                MemberExpression shouldClose = Expression.Field(Expression.Convert(window, baseType), AccessTools.Field(baseType, "shouldClose"));
                SetShouldClose = Expression.Lambda<Action<Window, int>>(Expression.Assign(shouldClose, value), window, value).Compile();
                CreateDialog = Expression.Lambda<Func<Window>>(Expression.New(constructor,
                    Expression.Constant(null, menuType), Expression.Constant(true))).Compile();
            }
        }
    }

    internal static class XmlExtensionsCloseRouter
    {
        private static readonly HashSet<Assembly> installed = new HashSet<Assembly>();
        private static Action<int> activeRequest;

        internal static Action<int> Enter(Action<int> request)
        {
            Action<int> previous = activeRequest;
            activeRequest = request;
            return previous;
        }

        internal static void Exit(Action<int> previous) => activeRequest = previous;

        internal static void Install(Assembly assembly)
        {
            if (installed.Contains(assembly)) return;
            var harmony = new Harmony("Nanaloveyuki.IrisMenus.XmlSettingsBody");
            harmony.Patch(AccessTools.Method(assembly.GetType("XmlExtensions.Action.CloseMenu", true), "ApplyAction"),
                prefix: new HarmonyMethod(typeof(XmlExtensionsCloseRouter), nameof(CloseMenuPrefix)));
            harmony.Patch(AccessTools.Method(assembly.GetType("XmlExtensions.Action.RestartGame", true), "ApplyAction"),
                prefix: new HarmonyMethod(typeof(XmlExtensionsCloseRouter), nameof(RestartGamePrefix)));
            installed.Add(assembly);
        }

        internal static bool CloseMenuPrefix(ref bool __result) => Request(1, ref __result);
        internal static bool RestartGamePrefix(ref bool __result) => Request(2, ref __result);

        private static bool Request(int mode, ref bool result)
        {
            Action<int> request = activeRequest;
            if (request == null) return true;
            request(mode);
            result = true;
            return false;
        }
    }
}
