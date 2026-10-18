using System;
using System.Linq;
using Verse;

namespace HungerAndHavoc.Guard
{
    [StaticConstructorOnStartup]
    internal static class DuplicateModGuard
    {
        static readonly string[] ConflictingPackageIds =
        {
            "lezhizhong.mouse.disaster.famine",
            "nanaloveyuki.mouse.disaster.famine.continued",
            "local.mousedisaster.greatfamine"
        };

        static DuplicateModGuard()
        {
            string[] active = ModsConfig.ActiveModsInLoadOrder.Select(mod => mod.PackageId).ToArray();
            bool conflict = active.Any(id => ConflictingPackageIds.Any(conflictId =>
                string.Equals(id, conflictId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, conflictId + "_steam", StringComparison.OrdinalIgnoreCase)));
            if (!conflict)
            {
                return;
            }

            DelayedErrorWindowRequest.Add(
                "RHAH_Guard_DuplicateBody".Translate(),
                "RHAH_Guard_DuplicateTitle".Translate());
        }
    }
}
