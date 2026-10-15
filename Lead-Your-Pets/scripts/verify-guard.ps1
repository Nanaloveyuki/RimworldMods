param([switch]$NoDuplicate)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Guard/Source/DuplicateModGuard.cs') -Raw
$stubs = @'
namespace Verse {
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class StaticConstructorOnStartup : System.Attribute {}
    public class ModMetaData { public string PackageId; }
    public static class ModsConfig {
        public static System.Collections.Generic.List<ModMetaData> ActiveModsInLoadOrder = new System.Collections.Generic.List<ModMetaData>();
        public static int Writes;
        public static void SetActive(string id, bool active) {
            if (active) throw new System.Exception("Guard must not enable unrelated mods.");
            ActiveModsInLoadOrder.RemoveAll(m => m.PackageId == id);
        }
        public static void Save() { Writes++; }
    }
    public static class GenFilePaths { public static string ModsConfigFilePath; }
    public static class LongEventHandler { public static void ExecuteWhenFinished(System.Action action) { action(); } }
    public static class GenCommandLine { public static void Restart() {} }
    public static class Translation { public static string Translate(this string key) { return key; } }
    public class Dialog_MessageBox {
        public bool closeOnAccept, closeOnCancel, closeOnClickedOutside;
        public System.Action RestartAction;
        public Dialog_MessageBox(string text, string buttonAText, System.Action buttonAAction, string title) { RestartAction = buttonAAction; }
    }
    public class Windows {
        public Dialog_MessageBox Dialog;
        public void Add(Dialog_MessageBox dialog) { Dialog = dialog; }
    }
    public static class Find { public static Windows WindowStack = new Windows(); }
}
public static class GuardHarness {
    public static void Run(bool noDuplicate) {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LeadYourPetGuard-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try {
            Verse.GenFilePaths.ModsConfigFilePath = System.IO.Path.Combine(directory, "ModsConfig.xml");
            System.IO.File.WriteAllText(Verse.GenFilePaths.ModsConfigFilePath, "original configuration");
            string[] ids = noDuplicate ? new[] { "core", "nanaloveyuki.leadyourpet.continued", "another.mod" }
                : new[] { "core", "lezhizhong.leadyourpet", "nanaloveyuki.leadyourpet.continued", "codex.leadyourpet_steam", "another.mod" };
            foreach (string id in ids) Verse.ModsConfig.ActiveModsInLoadOrder.Add(new Verse.ModMetaData { PackageId = id });
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(LeadYourPetContinuedGuard.DuplicateModGuard).TypeHandle);
            string actual = string.Join(",", Verse.ModsConfig.ActiveModsInLoadOrder.Select(m => m.PackageId));
            if (actual != "core,nanaloveyuki.leadyourpet.continued,another.mod") throw new System.Exception("Unrelated mods changed: " + actual);
            if (Verse.ModsConfig.Writes != (noDuplicate ? 0 : 1)) throw new System.Exception("Unexpected configuration write count.");
            string[] backups = System.IO.Directory.GetFiles(directory, "*.bak");
            if (backups.Length != (noDuplicate ? 0 : 1)) throw new System.Exception("Unexpected backup count.");
            if (!noDuplicate) {
                if (System.IO.File.ReadAllText(backups[0]) != "original configuration") throw new System.Exception("Backup contents changed.");
                var dialog = Verse.Find.WindowStack.Dialog;
                if (dialog == null || dialog.RestartAction == null || dialog.closeOnAccept || dialog.closeOnCancel || dialog.closeOnClickedOutside)
                    throw new System.Exception("Restart prompt not enforced.");
            } else if (Verse.Find.WindowStack.Dialog != null) throw new System.Exception("Unexpected restart prompt.");
        } finally {
            foreach (string file in System.IO.Directory.GetFiles(directory)) System.IO.File.Delete(file);
            System.IO.Directory.Delete(directory);
        }
    }
}
'@
Add-Type -TypeDefinition ($source + "`n" + $stubs)
[GuardHarness]::Run($NoDuplicate.IsPresent)
Write-Host "Guard engine-double checks passed (NoDuplicate=$NoDuplicate). Not an in-game restart test."
