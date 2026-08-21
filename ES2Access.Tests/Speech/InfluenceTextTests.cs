using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The influence sentences. Two rules carry the weight: a radius is compared AS SPOKEN, so the
    /// tenth of a unit a colony creeps by between turns says "no change" rather than announcing growth
    /// nobody will hear arrive; and a crowd of contesters is one line, joined inside a translated
    /// sentence rather than by an English conjunction glued between names.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class InfluenceTextTests
    {
        public InfluenceTextTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void ARadiusGoingUpSaysWhereItIsGoing()
        {
            Assert.Equal(
                "Influence radius: 6.6, growing to 7.2 next turn",
                InfluenceText.Radius(6.56f, 7.23f)
            );
        }

        [Fact]
        public void ARadiusGoingDownSaysSoToo()
        {
            Assert.Equal(
                "Influence radius: 7.2, shrinking to 6.6 next turn",
                InfluenceText.Radius(7.23f, 6.56f)
            );
        }

        [Fact]
        public void ADriftTooSmallToHearIsNoChange()
        {
            Assert.Equal(
                "Influence radius: 6.6, no change next turn",
                InfluenceText.Radius(6.56f, 6.63f)
            );
            Assert.Equal(
                "Influence radius: 6.6, no change next turn",
                InfluenceText.Radius(6.63f, 6.56f)
            );
        }

        [Fact]
        public void ARoundNumberKeepsItsDecimal()
        {
            Assert.Equal(
                "Influence radius: 7.0, growing to 8.0 next turn",
                InfluenceText.Radius(7f, 8f)
            );
        }

        [Fact]
        public void AReachThatSpeaksAsNothingIsSilent()
        {
            Assert.Null(InfluenceText.Radius(1E-08f, 0f));
            Assert.Null(InfluenceText.Radius(0f, 0f));
        }

        [Fact]
        public void AReachGrowingOutOfNothingStillSpeaks()
        {
            Assert.Equal(
                "Influence radius: 0.0, growing to 0.6 next turn",
                InfluenceText.Radius(0.01f, 0.6f)
            );
        }

        [Fact]
        public void OneContesterIsNamedOnItsOwn()
        {
            Assert.Equal(
                "Influence contested by the Sophons",
                InfluenceText.Contested(new List<string> { "the Sophons" })
            );
        }

        [Fact]
        public void TwoContestersShareTheListSentence()
        {
            Assert.Equal(
                "Influence contested by the Sophons and the Lumeris",
                InfluenceText.Contested(new List<string> { "the Sophons", "the Lumeris" })
            );
        }

        [Fact]
        public void MoreThanTwoJoinWithTheListSeparatorUpToTheLast()
        {
            Assert.Equal(
                "Influence contested by the Sophons, the Lumeris and the Cravers",
                InfluenceText.Contested(
                    new List<string> { "the Sophons", "the Lumeris", "the Cravers" }
                )
            );
        }

        [Fact]
        public void NobodyContestingIsSilence()
        {
            Assert.Null(InfluenceText.Contested(null));
            Assert.Null(InfluenceText.Contested(new List<string>()));
        }
    }
}
