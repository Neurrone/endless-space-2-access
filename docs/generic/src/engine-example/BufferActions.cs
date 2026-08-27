namespace ES2Access.UI.Input
{
    /// <summary>The action names the review buffers understand. Separate from
    /// <see cref="UiActions"/> because these move a reading cursor over text, not the player's focus
    /// over controls - the same keys with a modifier, and a different thing entirely.</summary>
    public static class BufferActions
    {
        public const string LineUp = "buffer.lineUp";
        public const string LineDown = "buffer.lineDown";
        public const string Prev = "buffer.prev";
        public const string Next = "buffer.next";
        public const string First = "buffer.first";
        public const string Last = "buffer.last";
    }
}
