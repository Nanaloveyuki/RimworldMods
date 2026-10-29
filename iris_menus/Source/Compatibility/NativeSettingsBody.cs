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
    // Only the two inspected launcher contracts are hosted here. Ordinary adapters stay direct.
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
            // No Add/PreOpen/PostOpen: the real MenuWindow already owns focus, pause and input.
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
        private static XmlExtensionsSettingsBody drawing;
        private readonly object menu;
        private readonly XmlApi api;
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
            XmlExtensionsSettingsBody previous = drawing;
            drawing = this;
            // DrawSettingsMenu opens its own group, then reuses the passed Rect for its
            // viewport. Local zero-origin coordinates avoid applying the host offset twice.
            GUI.BeginGroup(rect);
            try { drawMenu?.Invoke(new Rect(0f, 0f, rect.width, rect.height)); }
            finally { GUI.EndGroup(); drawing = previous; }
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

        private static bool CloseMenuPrefix(ref bool __result) => RequestClose(1, ref __result);
        private static bool RestartGamePrefix(ref bool __result) => RequestClose(2, ref __result);
        private static bool RequestClose(int mode, ref bool result)
        {
            if (drawing == null) return true;
            drawing.api.SetShouldClose(drawing.Dialog, mode);
            result = true;
            return false;
        }

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
                var harmony = new Harmony("Nanaloveyuki.IrisMenus.XmlSettingsBody");
                harmony.Patch(AccessTools.Method(assembly.GetType("XmlExtensions.Action.CloseMenu", true), "ApplyAction"),
                    prefix: new HarmonyMethod(typeof(XmlExtensionsSettingsBody), nameof(CloseMenuPrefix)));
                harmony.Patch(AccessTools.Method(assembly.GetType("XmlExtensions.Action.RestartGame", true), "ApplyAction"),
                    prefix: new HarmonyMethod(typeof(XmlExtensionsSettingsBody), nameof(RestartGamePrefix)));
            }
        }
    }
}
