using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>模组自带贴图（Textures/ 目录）。找不到时回退原版图标，绝不让 UI 因缺图报错。</summary>
    [StaticConstructorOnStartup]
    public static class PPTETextures
    {
        private static Texture2D _ticket;

        /// <summary>饭票图标（Textures/UI/PPTE2_TicketIcon.png）。</summary>
        public static Texture2D Ticket
            => _ticket ?? (_ticket = ContentFinder<Texture2D>.Get("UI/PPTE2_TicketIcon", false)
                                      ?? TexButton.Info);
    }
}
