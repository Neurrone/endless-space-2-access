using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// How a panel's fields become one spoken line. The behaviour the system-discovery announcer
    /// leans on is the last one: nothing to say is null, which is how it tells "this card has not
    /// been filled in yet" from "this card says something".
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class FieldReadoutTests
    {
        public FieldReadoutTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void FieldsAreReadInTheOrderTheyWereCollected()
        {
            Assert.Equal(
                "Cravings, Unique planet, Medium Terran",
                FieldReadout.Compose(
                    new List<string> { "Cravings", "Unique planet", "Medium Terran" }
                )
            );
        }

        [Fact]
        public void AFieldThePanelIsNotShowingIsLeftOutWithoutLeavingAGap()
        {
            Assert.Equal(
                "Cravings, Colonized",
                FieldReadout.Compose(new List<string> { "Cravings", null, "", "Colonized" })
            );
        }

        [Fact]
        public void ABlankedLabelIsNotAListItemOfItsOwn()
        {
            Assert.Equal(
                "Cravings, Colonized",
                FieldReadout.Compose(new List<string> { "Cravings", "   ", "Colonized" })
            );
        }

        [Fact]
        public void SurroundingSpaceIsTrimmedOffAFieldThatSurvives()
        {
            Assert.Equal(
                "Cravings, Colonized",
                FieldReadout.Compose(new List<string> { " Cravings ", "Colonized" })
            );
        }

        [Fact]
        public void ALoneFieldIsSpokenWithNoSeparators()
        {
            Assert.Equal("Cravings", FieldReadout.Compose(new List<string> { "Cravings" }));
        }

        [Fact]
        public void RepeatedFieldsAreBothSpoken()
        {
            Assert.Equal(
                "Poor, Poor",
                FieldReadout.Compose(new List<string> { "Poor", "Poor" })
            );
        }

        [Fact]
        public void NothingToSayIsNullRatherThanAnEmptyLine()
        {
            Assert.Null(FieldReadout.Compose(new List<string>()));
            Assert.Null(FieldReadout.Compose(new List<string> { null, "", "  " }));
            Assert.Null(FieldReadout.Compose(null));
        }
    }
}
