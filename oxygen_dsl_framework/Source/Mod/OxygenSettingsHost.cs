using System;
using System.Collections.Generic;
using Oxygendsl.Core;
using Oxygendsl.Language;
using Verse;

namespace Oxygendsl.Mod
{
    public interface IOxygenSettingsBridge
    {
        void Replace(Verse.Mod owner, OxygenSettingsPlan plan, IOxygenSettingsValues values);
    }

    public static class OxygenSettingsHost
    {
        public static OxygenHostModuleRegistry Modules { get; } = new OxygenHostModuleRegistry();
        static readonly Dictionary<Verse.Mod, IOxygenSettingsValues> owners = new Dictionary<Verse.Mod, IOxygenSettingsValues>();
        static readonly HashSet<Verse.Mod> initialized = new HashSet<Verse.Mod>();
        static IOxygenSettingsBridge bridge;

        // Game thread only; the optional adapter is loaded without adding an IrisMenus assembly reference here.
        public static void InstallBridge(IOxygenSettingsBridge settingsBridge)
        {
            if (settingsBridge == null) throw new ArgumentNullException(nameof(settingsBridge));
            if (bridge != null) throw new InvalidOperationException("A settings bridge is already installed.");
            bridge = settingsBridge;
            foreach (Verse.Mod owner in initialized)
                if (OxygenModFiles.Ready) Apply(owner.Content, OxygenModFiles.Compile(owner.Content, false).Settings);
        }

        public static void Register(Verse.Mod owner, IOxygenSettingsValues values)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (owners.ContainsKey(owner)) throw new ArgumentException("Settings owner already registered.", nameof(owner));
            owners.Add(owner, values);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                initialized.Add(owner);
                if (OxygenModFiles.Ready) Apply(owner.Content, OxygenModFiles.Compile(owner.Content, false).Settings);
            });
        }

        public static bool Reload(Verse.Mod owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (!owners.ContainsKey(owner)) throw new ArgumentException("Settings owner is not registered.", nameof(owner));
            try
            {
                if (!OxygenModFiles.Ready) return false;
                return OxygenContentHost.ReloadRulesFor(owner.Content.PackageIdPlayerFacing, null);
            }
            catch (Exception exception)
            {
                Log.Error("[Oxygendsl] " + owner.Content.PackageIdPlayerFacing + "/Oxygen: "
                    + exception.Message + " Previous settings pages retained.");
                return false;
            }
        }

        internal static void Apply(ModContentPack provider, OxygenSettingsPlan plan)
        {
            foreach (KeyValuePair<Verse.Mod, IOxygenSettingsValues> owner in owners)
                if (ReferenceEquals(owner.Key.Content, provider))
                {
                    bridge?.Replace(owner.Key, plan, owner.Value);
                    return;
                }
            if (plan.Pages.Count != 0)
                throw new InvalidOperationException(provider.PackageIdPlayerFacing + "/Oxygen: Settings declarations require OxygenSettingsHost.Register(owner, values).");
        }
    }
}
