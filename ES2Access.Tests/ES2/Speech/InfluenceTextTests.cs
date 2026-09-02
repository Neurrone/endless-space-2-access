using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.ES2.Speech;
using ES2Access.ES2.UI;
using ES2Access.Tests.Speech;
using Xunit;

namespace ES2Access.Tests.ES2.Speech
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

        [Fact]
        public void ACellProvedToBeOneEmpiresSaysSoWithoutHedging()
        {
            Assert.Equal(
                "In the Sophons's influence",
                InfluenceText.Cell(InfluenceCover.Whole, One("the Sophons"), false)
            );
            Assert.Equal(
                "In your influence",
                InfluenceText.Cell(InfluenceCover.Whole, One("Neurrone"), true)
            );
        }

        [Fact]
        public void ACellTheBorderRunsThroughIsTheEdgeOfIt()
        {
            Assert.Equal(
                "Edge of the Sophons's influence",
                InfluenceText.Cell(InfluenceCover.Edge, One("the Sophons"), false)
            );
            Assert.Equal(
                "Edge of your influence",
                InfluenceText.Cell(InfluenceCover.Edge, One("Neurrone"), true)
            );
        }

        [Fact]
        public void SeveralEmpiresSharingACellAreOneLine()
        {
            Assert.Equal(
                "Edge of the Sophons's and the Lumeris's influence",
                InfluenceText.Cell(
                    InfluenceCover.Edge,
                    new List<string> { "the Sophons", "the Lumeris" },
                    false
                )
            );
            // Past two the possessive can only sit on the two slots the template has, because a
            // language's own inflection belongs inside the translated sentence and never glued onto a
            // name by the mod. Three empires overlapping one square is a wording the owner has not
            // ruled on; this records what it says today.
            Assert.Equal(
                "Edge of the Sophons, the Lumeris's and the Cravers's influence",
                InfluenceText.Cell(
                    InfluenceCover.Edge,
                    new List<string> { "the Sophons", "the Lumeris", "the Cravers" },
                    false
                )
            );
        }

        [Fact]
        public void ThePlayerInACrowdIsNamedLikeAnybodyElse()
        {
            Assert.Equal(
                "Edge of Neurrone's and Niris's influence",
                InfluenceText.Cell(
                    InfluenceCover.Edge,
                    new List<string> { "Neurrone", "Niris" },
                    true
                )
            );
        }

        [Fact]
        public void SteppingOutNamesWhatWasLeft()
        {
            Assert.Equal("Out of your influence", InfluenceText.Left(One("Neurrone"), true));
            Assert.Equal("Out of Niris's influence", InfluenceText.Left(One("Niris"), false));
            Assert.Equal(
                "Out of Neurrone's and Niris's influence",
                InfluenceText.Left(new List<string> { "Neurrone", "Niris" }, false)
            );
        }

        [Fact]
        public void NobodysInfluenceIsSilence()
        {
            Assert.Null(InfluenceText.Cell(InfluenceCover.None, One("Niris"), false));
            Assert.Null(InfluenceText.Cell(InfluenceCover.Edge, new List<string>(), false));
            Assert.Null(InfluenceText.Left(null, false));
        }

        private static List<string> One(string name)
        {
            return new List<string> { name };
        }
    }
}
