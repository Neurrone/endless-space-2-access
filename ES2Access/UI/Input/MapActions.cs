namespace ES2Access.UI.Input
{
    /// <summary>
    /// The actions that belong to the GALAXY MAP rather than to a control on it.
    ///
    /// They are separate from <see cref="UiActions"/> because the navigator knows nothing about them:
    /// they are offered to the focused SCREEN (<c>Screen.AnyKey</c>), which is how a mode the map
    /// imposes on the whole page takes a key ahead of whatever the cursor is standing on.
    /// </summary>
    public static class MapActions
    {
        /// <summary>Turn the map's inspect cursor on and off - a square of galaxy the player sweeps
        /// with the arrows, hearing what is inside it.</summary>
        public const string Inspect = "galaxy.inspect";

        // The cursor's size, on the two keys that mean bigger and smaller everywhere. Claimed from the
        // game only while the cursor is up, so the game keeps its own minus key the rest of the time.
        public const string InspectGrow = "galaxy.inspectGrow";
        public const string InspectShrink = "galaxy.inspectShrink";
    }
}
