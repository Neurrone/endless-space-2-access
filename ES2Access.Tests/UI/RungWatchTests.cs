using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Announcing how close the game is looking, wherever the player is standing.
    ///
    /// Every rule here is a way the same watcher goes wrong out loud: it greets a player who has just
    /// started looking with a zoom level nothing changed, it says the number the slider under the
    /// cursor has just said, it calls out a rung the camera was only flying through, or it repeats
    /// itself when the player comes back to a control they were already on.
    /// </summary>
    public class RungWatchTests
    {
        [Fact]
        public void TheRungTheGameWasAlreadyOnIsNotNews()
        {
            RungWatch watch = new RungWatch();

            Assert.False(watch.NeedsTelling(9, false, false));
        }

        [Fact]
        public void AChangedRungIsAnnouncedOnceAndThenLeftAlone()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(9, false, false);

            Assert.True(watch.NeedsTelling(10, false, false));
            watch.Told(10);
            Assert.False(watch.NeedsTelling(10, false, false));
        }

        [Fact]
        public void ARungTheWordsAreNotReadyForStaysOwed()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(9, false, false);

            Assert.True(watch.NeedsTelling(10, false, false));
            Assert.True(watch.NeedsTelling(10, false, false));
            watch.Told(10);
            Assert.False(watch.NeedsTelling(10, false, false));
        }

        [Fact]
        public void NothingIsSaidWhileTheValueIsStillSettling()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(12, false, false);

            Assert.False(watch.NeedsTelling(13, true, false));
        }

        [Fact]
        public void TheRungItSettlesOnIsStillNews()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(12, false, false);
            watch.NeedsTelling(13, true, false);

            Assert.True(watch.NeedsTelling(13, false, false));
        }

        [Fact]
        public void ARungReadOutByTheControlUnderTheCursorIsNotSaidTwice()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(9, false, false);

            Assert.False(watch.NeedsTelling(10, false, true));
        }

        [Fact]
        public void ARungHeardFromTheControlIsNotAnnouncedOnLeavingIt()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(9, false, false);
            watch.NeedsTelling(10, false, true);

            Assert.False(watch.NeedsTelling(10, false, false));
        }

        [Fact]
        public void WhereThereIsNoLadderThereIsNothingToSay()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(9, false, false);

            Assert.False(watch.NeedsTelling(-1, false, false));
        }

        [Fact]
        public void ComingBackToTheRungTheLadderWasLeftOnSaysNothing()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(9, false, false);
            watch.NeedsTelling(-1, false, false);

            Assert.False(watch.NeedsTelling(9, false, false));
        }

        [Fact]
        public void AnotherGamesFirstRungIsAFirstSightingAgain()
        {
            RungWatch watch = new RungWatch();
            watch.NeedsTelling(9, false, false);
            watch.Forget();

            Assert.False(watch.NeedsTelling(4, false, false));
            Assert.True(watch.NeedsTelling(5, false, false));
        }
    }
}
