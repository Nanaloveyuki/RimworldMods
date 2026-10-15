using System.Reflection;
using HarmonyLib;

namespace SaveYourStorageAndTickEveryMinute.Compatibility
{
    internal sealed class OriginalMethodGuard
    {
        private readonly MethodBase method;
        private bool checkedOwners;
        private bool canReplace;

        public OriginalMethodGuard(MethodBase method)
        {
            this.method = method;
        }

        public bool CanReplace()
        {
            if (checkedOwners)
                return canReplace;

            Patches patches = method == null ? null : Harmony.GetPatchInfo(method);
            canReplace = method != null;
            if (patches != null)
            {
                foreach (string owner in patches.Owners)
                {
                    if (owner != SaveYourStorageMod.HarmonyId)
                    {
                        canReplace = false;
                        break;
                    }
                }
            }
            checkedOwners = true;
            return canReplace;
        }
    }
}
