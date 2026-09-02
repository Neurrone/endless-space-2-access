using ES2Access.ES2.UI;
using Xunit;

namespace ES2Access.Tests.ES2.UI
{
    /// <summary>
    /// Where the technology wheel looks when a branch is opened or closed.
    ///
    /// The rule is worth a test of its own because it is the whole design decision: the wheel only
    /// draws what is near the middle of the screen, so "which view does this keypress leave the player
    /// in" decides what exists to be read at all. Closing a stage is the case that is easy to get
    /// wrong - the player is not back at the whole wheel, they are back in the quadrant.
    /// </summary>
    public class ResearchCameraTests
    {
        [Fact]
        public void OpeningABranchLooksAtIt()
        {
            Assert.Equal(
                ResearchCamera.Aim.Quadrant,
                ResearchCamera.ForExpansion(ResearchCamera.Level.Quadrant, true)
            );
            Assert.Equal(
                ResearchCamera.Aim.Stage,
                ResearchCamera.ForExpansion(ResearchCamera.Level.Stage, true)
            );
        }

        [Fact]
        public void ClosingABranchLooksAtWhereverThatLeavesYou()
        {
            // Out of a quadrant is out of the tree: the whole wheel again.
            Assert.Equal(
                ResearchCamera.Aim.Overview,
                ResearchCamera.ForExpansion(ResearchCamera.Level.Quadrant, false)
            );

            // Out of a stage is still inside its quadrant, and that is what the player is looking at.
            Assert.Equal(
                ResearchCamera.Aim.Quadrant,
                ResearchCamera.ForExpansion(ResearchCamera.Level.Stage, false)
            );
        }
    }
}
