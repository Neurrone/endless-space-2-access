namespace ES2Access.UI
{
    /// <summary>
    /// The arithmetic behind a subtree signature: a sequence of small integers folded into one
    /// value that changes when any of them changes.
    ///
    /// FNV-1a, 64 bit, because it is a multiply and an exclusive-or per number - no allocation, no
    /// table - and because it is position-sensitive: the same flags read in a different order, or
    /// one flag flipped anywhere in the sequence, fold to a different value. That is the whole
    /// promise a repaint check needs (<see cref="TableSheet"/>'s paint signature), and it is why
    /// this half is separated from the walk that feeds it: the walk needs the engine, this does
    /// not, so it is proved off-engine.
    /// </summary>
    public static class PaintFold
    {
        /// <summary>Where a fold starts. Nothing folded in yet.</summary>
        public const long Seed = unchecked((long)14695981039346656037UL);

        /// <summary>The signature so far, with one more number folded into it.</summary>
        public static long Fold(long hash, int value)
        {
            unchecked
            {
                return (hash ^ value) * 1099511628211L;
            }
        }
    }
}
