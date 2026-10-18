namespace MouseDisaster
{
    public static class MouseDisasterRuntime
    {
        public static bool AllowsNewContent =>
            Verse.Current.Game?.GetComponent<GameComponent_MouseDisasterRemoval>()?.NewContentDisabled != true &&
            (GameComponent_MouseDisasterNarrative.DebugForcing || MouseDisasterMod.Settings == null || MouseDisasterMod.Settings.enableNewContent);
    }
}
