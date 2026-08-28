using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The class boundary read back off a finished readout - the comparison behind the tooltip
    /// audit's <c>misclassed</c> bucket. A tooltip the game wrote words for is announced whole; one
    /// the game assembles on hover is reviewed and never announced, and these two answers are what
    /// says whether that came out true for a node.
    ///
    /// The engine half (which tooltip is the node's, whether the words exist yet) needs the game.
    /// This half does not: it is two string comparisons, and every rule that makes them fair - the
    /// dedupe's alibi, a caption's coincidences, a control naming itself - lives here.
    /// </summary>
    public class TooltipKindRuleTests
    {
        private static IList<string> Lines(params string[] lines)
        {
            return new List<string>(lines);
        }

        [Fact]
        public void ATooltipTheReadoutSaysIsAccountedFor()
        {
            Assert.Null(
                TooltipKindRule.Unspoken(
                    Lines("Ends the turn", "and starts the next"),
                    "End Turn, button, Ends the turn and starts the next"
                )
            );
        }

        [Fact]
        public void TheFirstLineTheReadoutNeverSaysIsTheAnswer()
        {
            Assert.Equal(
                "and starts the next",
                TooltipKindRule.Unspoken(
                    Lines("Ends the turn", "and starts the next"),
                    "End Turn, button, Ends the turn"
                )
            );
        }

        // The dedupe drops a tooltip line the LABEL already speaks, which is why the question is asked
        // of the whole readout: the words are still there, in the label's voice.
        [Fact]
        public void ALineTheLabelSpeaksIsNotMissing()
        {
            Assert.Null(
                TooltipKindRule.Unspoken(
                    Lines("Empire Summary", "Click to consult your empire"),
                    "Empire Summary, button, Click to consult your empire"
                )
            );
        }

        // The shape the ruling outlawed: the label was read off the tooltip's first line and the rest
        // of the tooltip was declared buffer-only, so the readout is complete-looking and short.
        [Fact]
        public void ANamedControlStillOwesTheRestOfItsTooltip()
        {
            Assert.Equal(
                "Click to consult your empire",
                TooltipKindRule.Unspoken(
                    Lines("Empire Summary", "Click to consult your empire"),
                    "Empire Summary, button"
                )
            );
        }

        [Fact]
        public void PunctuationCaseAndMarkupAreNotDifferences()
        {
            Assert.Null(
                TooltipKindRule.Unspoken(
                    Lines("Over-colonization penalty"),
                    "over colonization Penalty"
                )
            );
        }

        [Fact]
        public void ALineWithNoWordsInItIsNotOwedAReading()
        {
            Assert.Null(TooltipKindRule.Unspoken(Lines("---", "  ", ""), "Anything"));
        }

        [Fact]
        public void NoLinesAtAllIsNothingToAnswerFor()
        {
            Assert.Null(TooltipKindRule.Unspoken(null, "Anything"));
            Assert.Null(TooltipKindRule.Leaked(null, "Anything", null));
        }

        [Fact]
        public void AnAssembledTooltipsSentenceInTheReadoutIsALeak()
        {
            Assert.Equal(
                "Produces 12 food per turn on this planet",
                TooltipKindRule.Leaked(
                    Lines("Food", "Produces 12 food per turn on this planet"),
                    "Food, Produces 12 food per turn on this planet",
                    Lines("Food")
                )
            );
        }

        [Fact]
        public void ACaptionIsTooShortToBeEvidence()
        {
            Assert.Null(
                TooltipKindRule.Leaked(Lines("Food", "12/20"), "Food, 12/20, slider", null)
            );
        }

        // A control whose drawn name is the panel's own first line is naming itself. The words are in
        // the readout because the LABEL says them, and the tooltip is still reviewed-only.
        [Fact]
        public void AControlNamingItselfIsNotALeak()
        {
            Assert.Null(
                TooltipKindRule.Leaked(
                    Lines("Colonization Ships"),
                    "Colonization Ships, button",
                    Lines("Colonization Ships")
                )
            );
        }

        [Fact]
        public void WordsTheReadoutNeverSaysAreNoLeak()
        {
            Assert.Null(
                TooltipKindRule.Leaked(
                    Lines("Produces 12 food per turn on this planet"),
                    "Food, slider, 12 of 20",
                    null
                )
            );
        }
    }
}
