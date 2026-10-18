using IrisMenus;
using Oxygendsl.Mod;
using Verse;

namespace Oxygendsl.IrisMenus
{
    [StaticConstructorOnStartup]
    public static class OxygenFrameworkMenus
    {
        static OxygenFrameworkMenus()
        {
            LongEventHandler.ExecuteWhenFinished(Register);
        }

        static void Register()
        {
            OxygendslMod owner = LoadedModManager.GetMod<OxygendslMod>();
            var documentation = new OxygenDocumentationPage();
            MenuRegistry.RegisterSubItemListing(owner, "general",
                () => "Oxygendsl_SettingsTitle".Translate(), owner.DrawSettingsPage);
            MenuRegistry.RegisterSubItem(owner, "documentation",
                () => "Oxygendsl_DocumentationTitle".Translate(), documentation.Draw);
            MenuRegistry.RegisterSearchProvider(owner, "documentation",
                documentation.SearchEntries, documentation.Focus);
        }
    }
}
