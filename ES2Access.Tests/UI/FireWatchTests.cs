using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The exchange of fire, gathered. Two rules carry the whole tier and neither is visible in a
    /// transcript: shots inside one window collapse into one line PER PAIR, and the window is what
    /// stops a fight of several hundred shots from becoming several hundred utterances.
    /// </summary>
    public class FireWatchTests
    {
        private const float Window = 5f;

        [Fact]
        public void NothingIsDueBeforeTheWindowCloses()
        {
            FireWatch watch = new FireWatch(Window);

            watch.Note("Prowler", "Endeavor", true, 40f, 0f, DamageKind.Energy, 100f);

            Assert.True(watch.Gathering);
            Assert.Null(watch.Due(100f));
            Assert.Null(watch.Due(104.9f));
            Assert.NotNull(watch.Due(105f));
        }

        [Fact]
        public void ShotsFromOnePairAtOneTargetBecomeOneVolley()
        {
            FireWatch watch = new FireWatch(Window);

            watch.Note("Prowler", "Endeavor", true, 40f, 0f, DamageKind.Energy, 0f);
            watch.Note("Prowler", "Endeavor", true, 20f, 5f, DamageKind.Energy, 1f);
            watch.Note("Prowler", "Endeavor", false, 0f, 0f, DamageKind.Unknown, 2f);

            IList<FireWatch.Volley> due = watch.Due(Window);

            Assert.Single(due);
            Assert.Equal("Prowler", due[0].Attacker);
            Assert.Equal("Endeavor", due[0].Target);
            Assert.Equal(2, due[0].Hits);
            Assert.Equal(1, due[0].Misses);
            Assert.Equal(60f, due[0].Energy);
            Assert.Equal(5f, due[0].Absorbed);
        }

        /// <summary>The two kinds are kept apart because the sentence names them: summing them would
        /// turn "40 energy and 20 projectile" into a number that describes neither.</summary>
        [Fact]
        public void TheTwoDamageKindsAreTalliedApart()
        {
            FireWatch watch = new FireWatch(Window);

            watch.Note("Patrol", "Prowler", true, 40f, 0f, DamageKind.Energy, 0f);
            watch.Note("Patrol", "Prowler", true, 20f, 0f, DamageKind.Projectile, 0f);
            watch.Note("Patrol", "Prowler", true, 7f, 0f, DamageKind.Unknown, 0f);

            FireWatch.Volley volley = watch.Due(Window)[0];

            Assert.Equal(40f, volley.Energy);
            Assert.Equal(20f, volley.Projectile);
            Assert.Equal(7f, volley.Untyped);
            Assert.Equal(67f, volley.Damage);
        }

        [Fact]
        public void EachPairIsItsOwnVolleyAndTheLoudestComesFirst()
        {
            FireWatch watch = new FireWatch(Window);

            watch.Note("Patrol", "Prowler", true, 10f, 0f, DamageKind.Energy, 0f);
            watch.Note("Prowler", "Endeavor", true, 90f, 0f, DamageKind.Energy, 0f);
            watch.Note("Prowler", "Patrol", true, 50f, 0f, DamageKind.Energy, 0f);

            IList<FireWatch.Volley> due = watch.Due(Window);

            Assert.Equal(3, due.Count);
            Assert.Equal("Endeavor", due[0].Target);
            Assert.Equal("Patrol", due[1].Target);
            Assert.Equal("Prowler", due[2].Target);
        }

        /// <summary>Two names that would run together into the same key if they were simply
        /// concatenated stay two exchanges.</summary>
        [Fact]
        public void PairsAreNotConfusedByWhereOneNameEnds()
        {
            FireWatch watch = new FireWatch(Window);

            watch.Note("AB", "C", true, 10f, 0f, DamageKind.Energy, 0f);
            watch.Note("A", "BC", true, 10f, 0f, DamageKind.Energy, 0f);

            Assert.Equal(2, watch.Due(Window).Count);
        }

        [Fact]
        public void AShotWithNothingToNameIsDropped()
        {
            FireWatch watch = new FireWatch(Window);

            watch.Note(null, "Endeavor", true, 40f, 0f, DamageKind.Energy, 0f);
            watch.Note("Prowler", null, true, 40f, 0f, DamageKind.Energy, 0f);
            watch.Note("", "", true, 40f, 0f, DamageKind.Energy, 0f);

            Assert.False(watch.Gathering);
            Assert.Null(watch.Due(Window));
        }

        /// <summary>Taking a window closes it: the next shot starts a fresh one rather than joining
        /// the one already reported.</summary>
        [Fact]
        public void TakingAWindowClosesIt()
        {
            FireWatch watch = new FireWatch(Window);

            watch.Note("Prowler", "Endeavor", true, 40f, 0f, DamageKind.Energy, 0f);
            Assert.Single(watch.Due(Window));
            Assert.Null(watch.Due(Window));

            watch.Note("Prowler", "Endeavor", true, 10f, 0f, DamageKind.Energy, 10f);
            IList<FireWatch.Volley> second = watch.Due(15f);

            Assert.Single(second);
            Assert.Equal(10f, second[0].Energy);
        }

        /// <summary>A re-watch replays the same stream against a reset model, so everything gathering
        /// belongs to a run that is no longer happening.</summary>
        [Fact]
        public void AResetForgetsWhatWasGathering()
        {
            FireWatch watch = new FireWatch(Window);

            watch.Note("Prowler", "Endeavor", true, 40f, 0f, DamageKind.Energy, 0f);
            watch.Reset();

            Assert.False(watch.Gathering);
            Assert.Null(watch.Due(Window));
        }
    }
}
