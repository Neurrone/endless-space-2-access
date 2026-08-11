using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Reporting a flurry as one line. Three rules, each of which fails silently in the game: the same
    /// event noticed on sixty consecutive frames must be one report, a burst must be held long enough
    /// for the rest of it to arrive, and a stream the game replays from the start must be news again.
    /// </summary>
    public class BurstWatchTests
    {
        [Fact]
        public void OneThingNoticedEveryFrameIsReportedOnce()
        {
            BurstWatch watch = new BurstWatch(0.5f);

            watch.Note("ship-1", "Vanguard", 0f);
            watch.Note("ship-1", "Vanguard", 0.1f);
            watch.Note("ship-1", "Vanguard", 0.2f);

            IList<string> burst = watch.Due(1f);
            Assert.Equal(new[] { "Vanguard" }, burst);
            Assert.Null(watch.Due(2f));
        }

        [Fact]
        public void NothingIsOfferedWhileTheBurstIsStillGathering()
        {
            BurstWatch watch = new BurstWatch(0.5f);

            watch.Note("ship-1", "Vanguard", 10f);
            Assert.True(watch.Gathering);
            Assert.Null(watch.Due(10.2f));

            watch.Note("ship-2", "Patrol", 10.3f);
            Assert.Equal(new[] { "Vanguard", "Patrol" }, watch.Due(10.5f));
            Assert.False(watch.Gathering);
        }

        [Fact]
        public void AFreshBurstOpensAfterTheLastOneWasTaken()
        {
            BurstWatch watch = new BurstWatch(0.5f);

            watch.Note("ship-1", "Vanguard", 0f);
            Assert.NotNull(watch.Due(1f));

            watch.Note("ship-2", "Patrol", 2f);
            Assert.Null(watch.Due(2.1f));
            Assert.Equal(new[] { "Patrol" }, watch.Due(2.6f));
        }

        [Fact]
        public void AReplayedStreamIsNewsAgain()
        {
            BurstWatch watch = new BurstWatch(0.5f);

            watch.Note("ship-1", "Vanguard", 0f);
            watch.Due(1f);

            // The same battle watched a second time: the model is reset and every ship dies again.
            watch.Reset();
            watch.Note("ship-1", "Vanguard", 0f);
            Assert.Equal(new[] { "Vanguard" }, watch.Due(1f));
        }

        [Fact]
        public void ResettingMidBurstThrowsAwayWhatWasGathering()
        {
            BurstWatch watch = new BurstWatch(0.5f);

            watch.Note("ship-1", "Vanguard", 0f);
            watch.Reset();

            Assert.False(watch.Gathering);
            Assert.Null(watch.Due(1f));
        }

        [Fact]
        public void SomethingWithNoIdentityIsNotAnEvent()
        {
            BurstWatch watch = new BurstWatch(0.5f);

            watch.Note(null, "Vanguard", 0f);
            watch.Note("", "Vanguard", 0f);

            Assert.False(watch.Gathering);
            Assert.Null(watch.Due(1f));
        }
    }
}
