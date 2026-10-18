using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using Verse;

namespace WashingMachine
{
    public sealed class WashingMachineSettings : ModSettings
    {
        public float PowerWatts = 200f;
        public float MaxBodySize = 0.95f;
        public float SmallPawnDeathChance = 0.05f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref PowerWatts, "powerWatts", 200f);
            Scribe_Values.Look(ref MaxBodySize, "maxBodySize", 0.95f);
            Scribe_Values.Look(ref SmallPawnDeathChance, "smallPawnDeathChance", 0.05f);
            ClampValues();
        }

        public void ClampValues()
        {
            PowerWatts = ClampFinite(PowerWatts, 0f, 1000f, 200f);
            MaxBodySize = ClampFinite(MaxBodySize, 0.1f, 10f, 0.95f);
            SmallPawnDeathChance = ClampFinite(SmallPawnDeathChance, 0f, 1f, 0.05f);
        }

        private static float ClampFinite(float value, float minimum, float maximum, float defaultValue)
        {
            return float.IsNaN(value) ? defaultValue : Mathf.Clamp(value, minimum, maximum);
        }
    }

    public sealed class WashingMachineMod : Mod
    {
        public static WashingMachineMod Instance { get; private set; }
        public static WashingMachineSettings Settings { get; private set; }

        private static bool irisRegistrationAttempted;
        private readonly Listing_Standard listing = new Listing_Standard();
        private Vector2 scrollPosition;
        private float contentHeight = 500f;
        private string powerBuffer;
        private string bodySizeBuffer;
        private string deathChanceBuffer;

        public WashingMachineMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<WashingMachineSettings>();
            Settings.ClampValues();
            powerBuffer = Format(Settings.PowerWatts);
            bodySizeBuffer = Format(Settings.MaxBodySize);
            deathChanceBuffer = Format(Settings.SmallPawnDeathChance * 100f);
            LongEventHandler.ExecuteWhenFinished(RegisterIrisMenus);
        }

        public override string SettingsCategory()
        {
            return "PWM_Settings_Title".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.ClampValues();
            float oldPower = Settings.PowerWatts;
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(1f, inRect.width - 20f), contentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            listing.Begin(new Rect(0f, 0f, viewRect.width, 100000f));
            try
            {
                listing.Label("PWM_Settings_Intro".Translate());
                listing.Gap(12f);
                DrawNumber("PWM_Settings_Power", "PWM_Settings_PowerTip", ref Settings.PowerWatts,
                    ref powerBuffer, 0f, 1000f);
                DrawNumber("PWM_Settings_MaxBodySize", "PWM_Settings_MaxBodySizeTip", ref Settings.MaxBodySize,
                    ref bodySizeBuffer, 0.1f, 10f);
                float deathPercent = Settings.SmallPawnDeathChance * 100f;
                DrawNumber("PWM_Settings_DeathChance", "PWM_Settings_DeathChanceTip", ref deathPercent,
                    ref deathChanceBuffer, 0f, 100f);
                Settings.SmallPawnDeathChance = deathPercent / 100f;
                contentHeight = listing.CurHeight;
            }
            finally
            {
                listing.End();
                Widgets.EndScrollView();
            }
            Settings.ClampValues();
            if (Settings.PowerWatts != oldPower)
            {
                RefreshExistingWashers();
            }
        }

        public override void WriteSettings()
        {
            Settings.ClampValues();
            base.WriteSettings();
            RefreshExistingWashers();
        }

        private void DrawNumber(string labelKey, string tipKey, ref float value, ref string buffer,
            float minimum, float maximum)
        {
            listing.Label(labelKey.Translate(Format(value)), tooltip: tipKey.Translate());
            float sliderValue = listing.Slider(value, minimum, maximum);
            if (sliderValue != value)
            {
                value = sliderValue;
                buffer = Format(value);
            }
            listing.TextFieldNumeric(ref value, ref buffer, minimum, maximum);
            listing.Gap(12f);
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static void RefreshExistingWashers()
        {
            if (Current.Game == null || WashingMachineDefOf.PWM_WashingMachine == null)
            {
                return;
            }
            var maps = Find.Maps;
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                var washers = maps[mapIndex].listerThings.ThingsOfDef(WashingMachineDefOf.PWM_WashingMachine);
                for (int washerIndex = 0; washerIndex < washers.Count; washerIndex++)
                {
                    if (washers[washerIndex] is Building_WashingMachine washer)
                    {
                        washer.RefreshPower();
                    }
                }
            }
        }

        private void RegisterIrisMenus()
        {
            if (irisRegistrationAttempted)
            {
                return;
            }
            irisRegistrationAttempted = true;
            if (ModLister.GetActiveModWithIdentifier("Nanaloveyuki.IrisMenus", true) == null)
            {
                return;
            }
            try
            {
                Type registry = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "IrisMenus")
                    {
                        registry = assembly.GetType("IrisMenus.MenuRegistry", false);
                        break;
                    }
                }
                MethodInfo register = registry?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(Mod), typeof(string), typeof(Func<string>), typeof(Action<Rect>), typeof(Action) }, null);
                if (register == null)
                {
                    Log.Warning("[WashingMachine] IrisMenus is enabled but its public MenuRegistry.Register API is unavailable.");
                    return;
                }
                register.Invoke(null, new object[]
                {
                    this, "settings", new Func<string>(SettingsCategory),
                    new Action<Rect>(DoSettingsWindowContents), new Action(WriteSettings)
                });
            }
            catch (Exception exception)
            {
                Exception cause = (exception as TargetInvocationException)?.InnerException ?? exception;
                Log.Warning("[WashingMachine] Could not register the optional IrisMenus settings page: " + cause);
            }
        }
    }
}
