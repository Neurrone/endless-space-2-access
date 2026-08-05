using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// Turning a blocked button's tooltip into the one sentence a player who just asked for that
    /// action needs to hear. The shapes below are ES2's orbital planet cards as measured on turn 1 -
    /// the colonize button that the game leaves clickable and turns into a signpost to the missing
    /// technology.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class RefusalTextTests
    {
        private const string Mouse =
            "Hold Control+Click to locate this technology in the technology tree";

        public RefusalTextTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void TheRefusalIsWhatIsLeftAfterTheDescriptionAndTheMouseInstruction()
        {
            Assert.Equal(
                "Missing technology Maximized Exploitation",
                RefusalText.Compose(
                    new List<string>
                    {
                        "Colonize the planet. This will consume your Colonization ship and create an Outpost on the System.",
                        "Missing technology Maximized Exploitation",
                        Mouse,
                    },
                    Mouse
                )
            );
        }

        [Fact]
        public void ARefusalTheGameWroteOverSeveralLinesIsReadAsOnePhrase()
        {
            Assert.Equal(
                "Missing technology Evaporation Inhibitors or Xenobotany",
                RefusalText.Compose(
                    new List<string>
                    {
                        "Colonize the planet.",
                        "Missing technology Evaporation Inhibitors",
                        "or Xenobotany",
                    },
                    Mouse
                )
            );
        }

        [Fact]
        public void AGameWithNoMouseInstructionKeepsEveryLineAfterTheDescription()
        {
            Assert.Equal(
                "You need a colony ship in this system",
                RefusalText.Compose(
                    new List<string> { "Colonize the planet.", "You need a colony ship in this system" },
                    null
                )
            );
        }

        [Fact]
        public void ASingleLineIsTheWholeOfWhatTheGameSaid()
        {
            Assert.Equal(
                "Missing technology Maximized Exploitation",
                RefusalText.Compose(
                    new List<string> { "Missing technology Maximized Exploitation" },
                    Mouse
                )
            );
        }

        [Fact]
        public void BlankLinesTheGameLeftBetweenThePartsAreNotSpoken()
        {
            Assert.Equal(
                "Missing technology Maximized Exploitation",
                RefusalText.Compose(
                    new List<string>
                    {
                        "Colonize the planet.",
                        "",
                        "   ",
                        "Missing technology Maximized Exploitation",
                    },
                    Mouse
                )
            );
        }

        [Fact]
        public void ATooltipThatOnlyDescribesTheButtonHasNoRefusalToReport()
        {
            Assert.Null(
                RefusalText.Compose(new List<string> { "Colonize the planet.", Mouse }, Mouse)
            );
            Assert.Null(RefusalText.Compose(new List<string>(), Mouse));
            Assert.Null(RefusalText.Compose(null, Mouse));
        }
    }
}
