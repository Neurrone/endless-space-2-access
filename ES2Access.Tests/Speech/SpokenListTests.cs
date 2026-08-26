using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// Several things said as one enumeration. The join that matters is the LAST one: a list that
    /// ends on a comma leaves a listener waiting for the next item, and a list of two that takes the
    /// comma anyway sounds like a list of three with one missing.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class SpokenListTests
    {
        public SpokenListTests()
        {
            ModStrings.Reset();
        }

        private static string Join(params string[] items)
        {
            return SpokenList.Join(new List<string>(items));
        }

        [Fact]
        public void NothingIsNothingRatherThanAnEmptyString()
        {
            Assert.Null(SpokenList.Join(null));
            Assert.Null(Join());
        }

        [Fact]
        public void OneThingIsSaidOnItsOwnWithNoConjunction()
        {
            Assert.Equal("12-15", Join("12-15"));
        }

        [Fact]
        public void TwoThingsTakeTheConjunctionAndNoComma()
        {
            Assert.Equal("12-15 and 17-18", Join("12-15", "17-18"));
        }

        [Fact]
        public void MoreThanTwoTakeCommasAndThenTheConjunction()
        {
            Assert.Equal("12-15, 17-18, and 19-26", Join("12-15", "17-18", "19-26"));
            Assert.Equal("a, b, c, and d", Join("a", "b", "c", "d"));
        }

        [Fact]
        public void ATranslationOwnsBothJoinsAndTheirPunctuation()
        {
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.ListSeparator, "、" },
                    { ModStrings.ListPair, "{0}と{1}" },
                    { ModStrings.ListFinal, "{0}、および{1}" },
                }
            );

            Assert.Equal("aとb", Join("a", "b"));
            Assert.Equal("a、b、およびc", Join("a", "b", "c"));
        }
    }
}
