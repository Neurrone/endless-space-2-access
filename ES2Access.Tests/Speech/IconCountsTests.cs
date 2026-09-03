using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Speech;
using ES2Access.ES2.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The turn icon's count rule: when a number written hard against the turn icon becomes a
    /// counted phrase, and when the icon is left to be named the ordinary way.
    ///
    /// The phrases are checked in three languages because the whole point of routing this through
    /// the plural machinery is that a translator moves the words: English needs two forms, Polish
    /// three, and Russian puts 21 back in the singular.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class IconCountsTests
    {
        public IconCountsTests()
        {
            ModStrings.Reset();
        }

        private static string Phrase(string before, out int consumed)
        {
            return IconCounts.Phrase(new StringBuilder(before), "TURNCOLORED", out consumed);
        }

        private static string Phrase(string before)
        {
            int consumed;
            return Phrase(before, out consumed);
        }

        /// <summary>Speak as <paramref name="language"/>, with only the counted turn pair
        /// translated - the rest of the table is beside the point here.</summary>
        private static void Speak(string language, string one, string many, string few)
        {
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.IconTurnCount, one },
                    { ModStrings.IconTurnsCount, many },
                    { ModStrings.IconTurnsCount + PluralRules.FewSuffix, few },
                },
                language
            );
        }

        [Fact]
        public void ACountBeforeTheTurnIconIsSpokenAsCountedTurns()
        {
            Assert.Equal("8 turns", Phrase("(8"));
            Assert.Equal("1 turn", Phrase("1"));
            Assert.Equal("0 turns", Phrase("0"));
        }

        /// <summary>The cost panel writes "{0} ({1}[turnColored])" - the digits butt up against the
        /// bracket - and the remaining-turns panel "{0} [turnColored]", with a blank between. Both
        /// hand the whole number and its spacing to the phrase.</summary>
        [Fact]
        public void ThePhraseTakesBackTheDigitsAndTheBlanksBetweenThemAndTheIcon()
        {
            int consumed;

            Assert.Equal("8 turns", Phrase("(8", out consumed));
            Assert.Equal(1, consumed);

            Assert.Equal("12 turns", Phrase("Cost: 12  ", out consumed));
            Assert.Equal(4, consumed);

            Assert.Equal("3 turns", Phrase("3", out consumed));
            Assert.Equal(1, consumed);
        }

        [Fact]
        public void NoOtherIconCounts()
        {
            int consumed;
            Assert.Null(IconCounts.Phrase(new StringBuilder("60"), "INDUSTRY", out consumed));
            Assert.Equal(0, consumed);
        }

        [Fact]
        public void TheIconOnItsOwnKeepsItsName()
        {
            Assert.Null(Phrase(string.Empty));
            Assert.Null(Phrase("Cost "));
        }

        [Fact]
        public void SomethingThatIsNotAWholeNumberKeepsThePlainReading()
        {
            Assert.Null(Phrase("1.5"));
            Assert.Null(Phrase("1,5"));
            Assert.Null(Phrase("-3"));
            Assert.Null(Phrase("+3"));
            Assert.Null(Phrase("Mark2"));
            Assert.Null(Phrase("1234567890"));
        }

        /// <summary>A number on the line above is a different sentence, not this icon's count.</summary>
        [Fact]
        public void ANumberOnTheLineAboveIsNotTheCount()
        {
            Assert.Null(Phrase("8\n"));
        }

        [Fact]
        public void PolishTakesThePaucalForTwoThroughFour()
        {
            Speak("polish", "{0} tura", "{0} tur", "{0} tury");

            Assert.Equal("1 tura", Phrase("1"));
            Assert.Equal("3 tury", Phrase("3"));
            Assert.Equal("12 tur", Phrase("12"));
            Assert.Equal("21 tur", Phrase("21"));
        }

        [Fact]
        public void RussianPutsEveryNumberEndingInOneBackInTheSingular()
        {
            Speak("russian", "{0} ход", "{0} ходов", "{0} хода");

            Assert.Equal("1 ход", Phrase("1"));
            Assert.Equal("21 ход", Phrase("21"));
            Assert.Equal("3 хода", Phrase("3"));
            Assert.Equal("8 ходов", Phrase("8"));
        }
    }
}
