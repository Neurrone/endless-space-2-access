using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The CLDR cardinal rules for the languages Endless Space 2 ships in. The teens are the whole
    /// point of the Slavic tables: 2 is paucal and 12 is not, 22 is paucal again.
    /// </summary>
    public class PluralRulesTests
    {
        [Theory]
        [InlineData(0, PluralForm.Many)]
        [InlineData(1, PluralForm.One)]
        [InlineData(2, PluralForm.Few)]
        [InlineData(4, PluralForm.Few)]
        [InlineData(5, PluralForm.Many)]
        [InlineData(11, PluralForm.Many)]
        [InlineData(12, PluralForm.Many)]
        [InlineData(14, PluralForm.Many)]
        [InlineData(21, PluralForm.Many)]
        [InlineData(22, PluralForm.Few)]
        [InlineData(25, PluralForm.Many)]
        [InlineData(101, PluralForm.Many)]
        [InlineData(111, PluralForm.Many)]
        [InlineData(112, PluralForm.Many)]
        [InlineData(122, PluralForm.Few)]
        public void PolishGivesOnlyOneItselfTheSingular(int count, PluralForm expected)
        {
            Assert.Equal(expected, PluralRules.For("polish", count));
        }

        [Theory]
        [InlineData(0, PluralForm.Many)]
        [InlineData(1, PluralForm.One)]
        [InlineData(2, PluralForm.Few)]
        [InlineData(4, PluralForm.Few)]
        [InlineData(5, PluralForm.Many)]
        [InlineData(11, PluralForm.Many)]
        [InlineData(12, PluralForm.Many)]
        [InlineData(14, PluralForm.Many)]
        [InlineData(21, PluralForm.One)]
        [InlineData(22, PluralForm.Few)]
        [InlineData(25, PluralForm.Many)]
        [InlineData(101, PluralForm.One)]
        [InlineData(111, PluralForm.Many)]
        [InlineData(112, PluralForm.Many)]
        [InlineData(122, PluralForm.Few)]
        public void RussianGivesEveryNumberEndingInOneTheSingular(int count, PluralForm expected)
        {
            Assert.Equal(expected, PluralRules.For("russian", count));
        }

        [Theory]
        [InlineData("french")]
        [InlineData("brazilian")]
        public void ZeroTakesTheSingularInFrenchAndBrazilian(string language)
        {
            Assert.Equal(PluralForm.One, PluralRules.For(language, 0));
            Assert.Equal(PluralForm.One, PluralRules.For(language, 1));
            Assert.Equal(PluralForm.Many, PluralRules.For(language, 2));
            Assert.Equal(PluralForm.Many, PluralRules.For(language, 21));
        }

        [Theory]
        [InlineData("english")]
        [InlineData("german")]
        [InlineData("spanish")]
        [InlineData("koreana")]
        [InlineData("schinese")]
        [InlineData("tchinese")]
        [InlineData("klingon")]
        [InlineData(null)]
        public void EveryOtherLanguageAndNoLanguageGetsTheTwoFormDefault(string language)
        {
            Assert.Equal(PluralForm.Many, PluralRules.For(language, 0));
            Assert.Equal(PluralForm.One, PluralRules.For(language, 1));
            Assert.Equal(PluralForm.Many, PluralRules.For(language, 2));
            Assert.Equal(PluralForm.Many, PluralRules.For(language, 22));
        }

        /// <summary>Nothing the mod counts is negative, but arriving there must still be a form
        /// rather than an exception out of <c>Math.Abs</c>.</summary>
        [Fact]
        public void NegativeCountsTakeTheirMagnitudesForm()
        {
            Assert.Equal(PluralForm.One, PluralRules.For("russian", -21));
            Assert.Equal(PluralForm.Few, PluralRules.For("polish", -3));
            Assert.Equal(PluralForm.One, PluralRules.For("english", -1));
            Assert.Equal(PluralForm.Many, PluralRules.For("polish", int.MinValue));
        }
    }
}
