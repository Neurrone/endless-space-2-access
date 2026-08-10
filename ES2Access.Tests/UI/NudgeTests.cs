using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// When the mod finishes a job the game dropped.
    ///
    /// Both waits are the design and both are easy to lose: without the settle the mod pushes a page
    /// that was merely slow, and without the pause it pushes again every frame while its own first
    /// push is still being carried out - which on the planet page would mean racing the game's own
    /// show coroutine rather than replacing it.
    /// </summary>
    public class NudgeTests
    {
        private static bool Run(Nudge nudge, int calls, bool stalled = true, bool safe = true)
        {
            bool pushed = false;
            for (int i = 0; i < calls; i++)
            {
                pushed |= nudge.Due(stalled, safe);
            }

            return pushed;
        }

        [Fact]
        public void SomethingTheGameIsMerelySlowAboutIsLeftAlone()
        {
            Nudge nudge = new Nudge(3, 5);
            Assert.False(nudge.Due(true, true));
            Assert.False(nudge.Due(true, true));
        }

        [Fact]
        public void SomethingStillUndoneAfterTheSettleIsPushedOnce()
        {
            Nudge nudge = new Nudge(3, 5);
            Assert.False(nudge.Due(true, true));
            Assert.False(nudge.Due(true, true));
            Assert.True(nudge.Due(true, true));
            Assert.False(Run(nudge, 5));
        }

        [Fact]
        public void TheGameIsLeftAloneForThePauseAndPushedAgainAfterIt()
        {
            Nudge nudge = new Nudge(2, 4);
            Assert.True(Run(nudge, 2));
            Assert.False(Run(nudge, 4));
            Assert.True(Run(nudge, 2));
        }

        [Fact]
        public void AStallThatClearsAndComesBackStartsTheSettleOver()
        {
            Nudge nudge = new Nudge(3, 5);
            Assert.False(nudge.Due(true, true));
            Assert.False(nudge.Due(false, true));
            Assert.False(nudge.Due(true, true));
            Assert.False(nudge.Due(true, true));
            Assert.True(nudge.Due(true, true));
        }

        [Fact]
        public void AFrameTheGameCouldNotHaveFinishedInDoesNotCount()
        {
            Nudge nudge = new Nudge(2, 5);
            Assert.False(Run(nudge, 10, stalled: true, safe: false));
            Assert.False(nudge.Due(true, true));
            Assert.True(nudge.Due(true, true));
        }

        [Fact]
        public void ForgettingPutsBothWaitsBack()
        {
            Nudge nudge = new Nudge(2, 10);
            Assert.True(Run(nudge, 2));
            nudge.Forget();
            Assert.False(nudge.Due(true, true));
            Assert.True(nudge.Due(true, true));
        }
    }
}
