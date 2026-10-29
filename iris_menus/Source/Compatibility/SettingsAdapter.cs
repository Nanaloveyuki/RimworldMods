using System;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal static class SettingsAdapter
    {
        private const string CharacterEditorMod = "CharacterEditor.CharacterEditorMod";

        internal static MenuPage Create(Mod owner, Func<string> title)
        {
            NativeSettingsBody body = owner.GetType().FullName == CharacterEditorMod
                ? new CharacterEditorSettingsBody(owner)
                : XmlExtensionsSettingsBody.Matches(owner) ? new XmlExtensionsSettingsBody(owner) : null;
            var page = new MenuPage(MenuRegistry.OwnerId(owner), owner, title,
                body == null ? owner.DoSettingsWindowContents : body.Draw,
                body == null ? owner.WriteSettings : body.Save, true);
            if (body != null)
            {
                page.Deactivate = body.Deactivate;
                page.BindHostClose = body.BindHostClose;
            }
            return page;
        }
    }
}
