namespace IrisMenus
{
    /// <summary>A window that can fully occlude lower windows and the game HUD.</summary>
    public interface IFullscreenUiSurface
    {
        bool CoversScreen { get; }
    }
}
