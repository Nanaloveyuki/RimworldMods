using Verse;

namespace PrisonLaborReachFix
{
    /// <summary>
    /// Loads the fix and installs it explicitly: Prison Labor is an optional dependency, so a missing
    /// mod or a renamed target method must leave the game running instead of throwing during load.
    /// </summary>
    public sealed class PrisonLaborReachFixMod : Mod
    {
        public const string HarmonyId = "nanaloveyuki.prisonlaborreachfix";

        public PrisonLaborReachFixMod(ModContentPack content) : base(content)
        {
            Patch_JobGiver_Labor.TryInstall();
        }
    }
}
