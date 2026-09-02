using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The rule that decides whether something was ever really on the player's screen. None of it is
    /// visible in a transcript: what it produces is the ABSENCE of two lines about an event that did
    /// not happen, and the presence of the pair that did.
    /// </summary>
    public class SettledSightTests
    {
        private const float Window = 2f;
        private const ulong Fleet = 7UL;

        [Fact]
        public void ARiseAndFallInTheSameFrameIsNothingAtAll()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, true, 100f);
            sight.Note(Fleet, false, 100f);

            Assert.Null(sight.Due(100f));
            Assert.Null(sight.Due(1000f));
            Assert.False(sight.InSightNow(Fleet));
        }

        /// <summary>A fleet crossing the edge of sight mid-lane: measured live, one second between
        /// the rise and the fall, and nothing a player could have read off the map.</summary>
        [Fact]
        public void AFlashShorterThanTheWindowIsNothingAtAll()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, true, 100f);
            Assert.Null(sight.Due(100.9f));
            sight.Note(Fleet, false, 101f);

            Assert.Null(sight.Due(101f));
            Assert.Null(sight.Due(1000f));
            Assert.False(sight.InSightNow(Fleet));
        }

        [Fact]
        public void SightHeldForTheWholeWindowIsASighting()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, true, 100f);

            Assert.Null(sight.Due(101.9f));
            IList<SettledSight.Change> due = sight.Due(102f);

            Assert.Single(due);
            Assert.Equal(Fleet, due[0].Key);
            Assert.True(due[0].InSight);
            Assert.True(sight.InSightNow(Fleet));
            Assert.Null(sight.Due(200f));
        }

        /// <summary>Told once. A caller that took the change and dropped it is never offered it
        /// again, which is what keeps one crossing from becoming a repeating line.</summary>
        [Fact]
        public void TheSameCrossingIsOnlyEverDueOnce()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, true, 0f);
            sight.Note(Fleet, true, 0.5f);
            sight.Note(Fleet, true, 1.5f);

            Assert.NotNull(sight.Due(2f));
            Assert.Null(sight.Due(2f));
            Assert.Null(sight.Due(10f));
        }

        [Fact]
        public void LossAfterASightingIsNewsOnceItHolds()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, true, 0f);
            Assert.NotNull(sight.Due(2f));

            sight.Note(Fleet, false, 10f);

            Assert.Null(sight.Due(11.9f));
            IList<SettledSight.Change> due = sight.Due(12f);

            Assert.Single(due);
            Assert.False(due[0].InSight);
            Assert.False(sight.InSightNow(Fleet));
        }

        /// <summary>Out of sight and back inside the window: the fleet never stopped being in sight
        /// as far as the player is concerned, so there is no loss AND no second sighting.</summary>
        [Fact]
        public void GoingAndComingBackInsideTheWindowSaysNothing()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, true, 0f);
            Assert.NotNull(sight.Due(2f));

            sight.Note(Fleet, false, 10f);
            sight.Note(Fleet, true, 11f);

            Assert.Null(sight.Due(11f));
            Assert.Null(sight.Due(100f));
            Assert.True(sight.InSightNow(Fleet));
        }

        /// <summary>The galaxy as it stood when the mod arrived: everything in sight, nobody told.
        /// A held thing going out of sight afterwards is still a real loss.</summary>
        [Fact]
        public void TheBaselineIsHeldWithoutAnnouncingAnything()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Hold(Fleet);

            Assert.Null(sight.Due(0f));
            Assert.Null(sight.Due(100f));
            Assert.True(sight.InSightNow(Fleet));
            Assert.Equal(1, sight.InSightCount);

            sight.Note(Fleet, false, 100f);
            IList<SettledSight.Change> due = sight.Due(102f);

            Assert.Single(due);
            Assert.False(due[0].InSight);
        }

        /// <summary>A thing that has never been in sight cannot be LOST: a write saying it is out of
        /// sight matches where it already stood and is not a crossing at all.</summary>
        [Fact]
        public void SomethingNeverSeenIsNeverLost()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, false, 0f);

            Assert.Null(sight.Due(100f));
            Assert.Equal(0, sight.PendingCount);
        }

        /// <summary>What a caller asks before adopting something it finds already drawn: a key with
        /// news one moment away must not be quietly written off as always-having-been-there.
        /// </summary>
        [Fact]
        public void ACandidateIsVisibleToACallerAboutToAdoptIt()
        {
            SettledSight sight = new SettledSight(Window);

            Assert.False(sight.Settling(Fleet));

            sight.Note(Fleet, true, 0f);
            Assert.True(sight.Settling(Fleet));
            Assert.False(sight.InSightNow(Fleet));

            sight.Due(2f);
            Assert.False(sight.Settling(Fleet));
            Assert.True(sight.InSightNow(Fleet));
        }

        /// <summary>Holding a key that is mid-crossing cancels the crossing: an adoption says "this
        /// was always here", which is the opposite of news about to be told.</summary>
        [Fact]
        public void HoldingOverACandidateCancelsIt()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, true, 0f);
            sight.Hold(Fleet);

            Assert.False(sight.Settling(Fleet));
            Assert.True(sight.InSightNow(Fleet));
            Assert.Null(sight.Due(100f));
        }

        [Fact]
        public void ForgettingCancelsACandidateAndResetClearsEverything()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(Fleet, true, 0f);
            Assert.Equal(1, sight.PendingCount);
            sight.Forget(Fleet);

            Assert.Null(sight.Due(100f));

            sight.Hold(Fleet);
            sight.Reset();

            Assert.False(sight.InSightNow(Fleet));
            Assert.Equal(0, sight.InSightCount);
        }

        /// <summary>Two fleets crossing in one batch settle apart, each on its own clock.</summary>
        [Fact]
        public void EachThingKeepsItsOwnWindow()
        {
            SettledSight sight = new SettledSight(Window);

            sight.Note(1UL, true, 0f);
            sight.Note(2UL, true, 1f);

            IList<SettledSight.Change> first = sight.Due(2f);
            Assert.Single(first);
            Assert.Equal(1UL, first[0].Key);

            IList<SettledSight.Change> second = sight.Due(3f);
            Assert.Single(second);
            Assert.Equal(2UL, second[0].Key);
        }
    }
}
