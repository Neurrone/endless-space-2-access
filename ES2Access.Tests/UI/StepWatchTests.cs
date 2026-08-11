using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Telling the player a step changed, exactly once.
    ///
    /// Both halves are the design and both fail invisibly: without the arrival baseline the first
    /// step is announced twice (the screen's name says it too), and without "commit only when you
    /// spoke" a step whose words are not written yet is consumed on the frame it changed and never
    /// heard at all.
    /// </summary>
    public class StepWatchTests
    {
        [Fact]
        public void TheStepArrivedOnIsNotAnnouncedAgain()
        {
            StepWatch watch = new StepWatch();
            watch.Baseline(0);

            Assert.False(watch.IsNew(0));
        }

        [Fact]
        public void ANewStepIsAnnouncedOnceAndThenLeftAlone()
        {
            StepWatch watch = new StepWatch();
            watch.Baseline(0);

            Assert.True(watch.IsNew(1));
            watch.Told(1);
            Assert.False(watch.IsNew(1));
        }

        [Fact]
        public void AStepWhoseWordsAreNotWrittenYetStaysAnnounceable()
        {
            StepWatch watch = new StepWatch();
            watch.Baseline(0);

            // The frame the step changed: nothing to say yet, so nothing is committed.
            Assert.True(watch.IsNew(1));

            // The next frame, and the one after: still owed.
            Assert.True(watch.IsNew(1));
            watch.Told(1);
            Assert.False(watch.IsNew(1));
        }

        [Fact]
        public void GoingBackToAStepAnnouncesItAgain()
        {
            StepWatch watch = new StepWatch();
            watch.Told(1);
            watch.Told(2);

            Assert.True(watch.IsNew(1));
        }

        [Fact]
        public void NoStepIsNeverAnnounced()
        {
            StepWatch watch = new StepWatch();

            Assert.False(watch.IsNew(-1));
        }

        [Fact]
        public void ForgettingMakesTheNextStepNewAgain()
        {
            StepWatch watch = new StepWatch();
            watch.Told(2);
            watch.Forget();

            Assert.True(watch.IsNew(2));
        }
    }
}
