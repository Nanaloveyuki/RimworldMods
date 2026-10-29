using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Legacy mod entry point. Settings and configuration now live in <see cref="TPE_Mod"/>.
    /// This class is kept for backward compatibility with older save files.
    /// </summary>
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content) { }
    }
}
