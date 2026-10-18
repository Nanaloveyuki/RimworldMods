using System;
using System.IO;
using System.Linq;
using Verse;

namespace LeadYourPetContinuedGuard
{
    [StaticConstructorOnStartup]
    internal static class DuplicateModGuard
    {
        static DuplicateModGuard()
        {
            string[] originals = { "lezhizhong.leadyourpet", "codex.leadyourpet" };
            string[] active = ModsConfig.ActiveModsInLoadOrder.Select(mod => mod.PackageId).ToArray();
            string[] duplicates = active.Where(id => originals.Any(original =>
                string.Equals(id, original, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, original + "_steam", StringComparison.OrdinalIgnoreCase))).ToArray();
            if (duplicates.Length == 0)
                return;

            // Preserve the pre-migration load order without touching saves.
            if (File.Exists(GenFilePaths.ModsConfigFilePath))
                File.Copy(GenFilePaths.ModsConfigFilePath,
                    GenFilePaths.ModsConfigFilePath + ".LeadYourPetContinued-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak");
            foreach (string id in duplicates)
                ModsConfig.SetActive(id, false);
            ModsConfig.Save();
            LongEventHandler.ExecuteWhenFinished(() => Find.WindowStack.Add(new Dialog_MessageBox(
                "LeadYourPetContinued_DuplicateBody".Translate(),
                "LeadYourPetContinued_Restart".Translate(), GenCommandLine.Restart,
                title: "LeadYourPetContinued_DuplicateTitle".Translate())
            {
                closeOnAccept = false,
                closeOnCancel = false,
                closeOnClickedOutside = false
            }));
        }
    }
}
