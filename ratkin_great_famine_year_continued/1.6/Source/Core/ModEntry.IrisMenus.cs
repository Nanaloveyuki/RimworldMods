using System;
using System.Reflection;
using Verse;

namespace MouseDisaster
{
    public partial class MouseDisasterMod
    {
        private enum SettingsPage
        {
            Safety,
            General,
            Environment,
            PawnBehavior,
            FoodWeights,
            PawnHistory,
            PawnTrait,
            Predation,
            OriginalEvents,
            ContinuedEvents,
            Endings,
            Developer
        }

        private delegate void RegisterSubItemListing(Mod owner, string pageId, Func<string> title,
            Action<Listing_Standard> draw, Action save);

        private void RegisterIrisMenus()
        {
            if (!ModsConfig.IsActive("Nanaloveyuki.IrisMenus")) return;

            try
            {
                // Bind only the public API, keeping IrisMenus an optional runtime dependency.
                Type registry = GenTypes.GetTypeInAnyAssembly("IrisMenus.MenuRegistry");
                MethodInfo method = registry?.GetMethod("RegisterSubItemListing", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(Mod), typeof(string), typeof(Func<string>), typeof(Action<Listing_Standard>), typeof(Action) }, null);
                if (method == null)
                {
                    Log.Warning("[MouseDisaster] IrisMenus RegisterSubItemListing API unavailable; keeping the original settings page.");
                    return;
                }

                var register = (RegisterSubItemListing)Delegate.CreateDelegate(typeof(RegisterSubItemListing), method);
                foreach (SettingsPage page in Enum.GetValues(typeof(SettingsPage)))
                {
                    SettingsPage selected = page;
                    register(this, "settings." + page, () => ("MouseDisaster_IrisMenus_" + selected).Translate().ToString(),
                        listing => DrawSettings(listing, selected), SaveSettings);
                }
            }
            catch (Exception exception)
            {
                // IrisMenus has no unregister API; report partial registration instead of retrying duplicates.
                Log.Error("[MouseDisaster] IrisMenus settings registration failed; the original settings renderer remains available: " + exception);
            }
        }
    }
}
