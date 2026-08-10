using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// What a drawn tooltip's parts SAY once something else has worked out where they are. The
    /// rules here are the ones that were wrong in the geometric reader: when a picture is a column
    /// heading and when it is decoration, when a caption and a value are one fact, and how a strip
    /// of repeated items differs from a stack of them.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class TooltipTextTests
    {
        public TooltipTextTests()
        {
            ModStrings.Reset();
        }

        private static List<string> Row(params TooltipPart[] parts)
        {
            List<string> lines = new List<string>();
            TooltipText.AddRow(lines, parts);
            return lines;
        }

        private static TooltipPart Words(string text)
        {
            return new TooltipPart(text, false);
        }

        private static TooltipPart Picture(string name)
        {
            return new TooltipPart(name, true);
        }

        private static TooltipPart Elsewhere(string text)
        {
            return new TooltipPart(text, false, true);
        }

        [Fact]
        public void AFactThatOnlyLandedInThisRowIsNotPartOfItsSentence()
        {
            Assert.Equal(
                new[] { "Counselor", "Level 1" },
                Row(Words("Counselor"), Elsewhere("Level 1"))
            );
        }

        [Fact]
        public void SuchAFactKeepsItsPlaceInTheRowItLandedIn()
        {
            Assert.Equal(
                new[] { "Level 1", "Counselor" },
                Row(Elsewhere("Level 1"), Words("Counselor"))
            );
        }

        [Fact]
        public void APictureIsWeighedAgainstTheWordsOfItsOwnLineOnly()
        {
            Assert.Equal(
                new[] { "Level 1", "Food 36" },
                Row(Elsewhere("Level 1"), Picture("Food"), Words("36"))
            );
        }

        [Fact]
        public void ARowThatIsNothingButSuchAFactIsStillOneLine()
        {
            Assert.Equal(new[] { "Level 1" }, Row(Elsewhere("Level 1")));
        }

        [Fact]
        public void ACaptionAndItsValueAreOneLineJoinedAsProse()
        {
            Assert.Equal(
                new[] { "Current Stock: 0/300" },
                Row(Words("Current Stock:"), Words("0/300"))
            );
        }

        [Fact]
        public void APictureBesideANumberIsWhatTheNumberMeansSoItIsRead()
        {
            Assert.Equal(
                new[] { "Food 36 Industry 37" },
                Row(Picture("Food"), Words("36"), Picture("Industry"), Words("37"))
            );
        }

        [Fact]
        public void APictureWithNoWordsToCompleteIsNotALineOfItsOwn()
        {
            Assert.Empty(Row(Picture("Blue Star")));
        }

        [Fact]
        public void APictureTheRowAlreadySaysInWordsIsNotSaidTwice()
        {
            Assert.Equal(
                new[] { "Star System (Blue Star)" },
                Row(Picture("System"), Words("Star System (Blue Star)"))
            );
        }

        [Fact]
        public void AParagraphTheWindowWrappedKeepsItsOwnBreaks()
        {
            Assert.Equal(
                new[] { "This planet is too hostile", "Missing technology" },
                Row(Words("This planet is too hostile\n\nMissing technology"))
            );
        }

        [Fact]
        public void ASubstitutedPartThatCameOutEmptyLeavesNoGap()
        {
            Assert.Equal(new[] { "Health 1500/1500" }, Row(Words("Health 1500/1500"), Words(null)));
        }

        [Fact]
        public void AStatWithNoValueDrawnIsNotAStatWithAnEmptyAnswer()
        {
            Assert.Null(TooltipText.Captioned("Health", null));
            Assert.Equal("Health 1500/1500", TooltipText.Captioned("Health", "1500/1500"));
        }

        [Fact]
        public void AValueTheGameNeverNamedIsStillTheValue()
        {
            Assert.Equal("1500/1500", TooltipText.Captioned(null, "1500/1500"));
        }

        [Fact]
        public void ARepeatedStripReadsAsOneFactWithItsPartsSeparated()
        {
            Assert.Equal(
                "Long 10%, Medium 50%, Short 95%",
                TooltipText.Items(new List<string> { "Long 10%", "Medium 50%", "Short 95%" })
            );
        }

        [Fact]
        public void OneItemsPartsAreOnePhraseWithNoPauseInIt()
        {
            Assert.Equal("Long 10%", TooltipText.Phrase(new List<string> { "Long", "10%" }));
        }
    }
}
