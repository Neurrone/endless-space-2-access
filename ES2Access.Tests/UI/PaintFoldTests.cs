using ES2Access.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The fold a table's repaint check is built on. A row's cells are walked into a sequence of
    /// small numbers - visible, painted, how many children - and the one promise the check needs is
    /// that a change ANYWHERE in that sequence changes the value it folds to, however long the
    /// sequence and wherever the change sits.
    /// </summary>
    public class PaintFoldTests
    {
        private static long Signature(int[] flags)
        {
            long hash = PaintFold.Seed;
            for (int i = 0; i < flags.Length; i++)
            {
                hash = PaintFold.Fold(hash, flags[i]);
            }

            return hash;
        }

        [Fact]
        public void AFlagFlippedAnywhereInTheSequenceChangesTheSignature()
        {
            int[] flags = { 3, 3, 0, 1, 2, 3, 3, 0, 0, 7, 1, 3 };
            long baseline = Signature(flags);

            Assert.Equal(baseline, Signature(new[] { 3, 3, 0, 1, 2, 3, 3, 0, 0, 7, 1, 3 }));

            for (int i = 0; i < flags.Length; i++)
            {
                int was = flags[i];
                flags[i] = was + 1;
                Assert.NotEqual(baseline, Signature(flags));
                flags[i] = was;
            }
        }
    }
}
