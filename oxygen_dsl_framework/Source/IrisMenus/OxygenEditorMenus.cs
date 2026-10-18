using IrisMenus;
using Oxygendsl.Mod;
using Verse;

namespace Oxygendsl.IrisMenus
{
    [StaticConstructorOnStartup]
    public static class OxygenEditorMenus
    {
        static OxygenEditorMenus()
        {
            LongEventHandler.ExecuteWhenFinished(Register);
        }

        static void Register()
        {
            foreach (ModContentPack provider in OxygenEditorHost.Providers)
            {
                ModContentPack target = provider;
                string packageId = target.PackageIdPlayerFacing;
                // Bundled examples remain editable without creating player-facing Mod Items.
                if (string.Equals(packageId, "nanaloveyuki.oxygen.example.traits", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(packageId, "nanaloveyuki.oxygen.example.rhah", System.StringComparison.OrdinalIgnoreCase)) continue;
                MenuRegistry.RegisterOwnerAction(target, "oxygen.edit",
                    () => "Oxygendsl_EditMod".Translate(), () => OxygenEditorHost.Open(packageId),
                    confirmation: () => "Oxygendsl_EditModConfirm".Translate(target.Name));
                MenuRegistry.RegisterOwnerAction(target, "oxygen.exit-editor",
                    () => "Oxygendsl_ExitEditor".Translate(), OxygenEditorHost.Stop,
                    visible: () => OxygenEditorHost.Running,
                    confirmation: () => "Oxygendsl_ExitEditorConfirm".Translate());
            }
        }
    }
}
