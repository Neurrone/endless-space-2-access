using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The two promises read back off a node's own declaration - the comparison behind the tooltip
    /// audit's <c>unaimed</c> bucket. A node that wrote a tooltip's words down and aims at nothing
    /// promises a review buffer the game will never fill, and it does it invisibly: no other bucket
    /// there has an aim to judge or a promise to check, so the screen reads clean.
    ///
    /// The engine half - what a widget is, whether the game would draw anything - needs the game. This
    /// half does not: it is the sections the node declared and the two booleans, and every rule that
    /// keeps an honest node out of the bucket lives here.
    /// </summary>
    public class TooltipAimRuleTests
    {
        private static readonly System.Func<IList<string>> Words = () =>
            new List<string> { "Click to consult the empire summary" };

        private static IList<NodeSection> Sections(params NodeSection[] sections)
        {
            return new List<NodeSection>(sections);
        }

        private static NodeSection Tooltip()
        {
            return NodeSection.Derived(Words, TooltipMode.Announce, null);
        }

        [Fact]
        public void ATooltipSectionWithNoAimAndNoPointerIsTheFinding()
        {
            Assert.True(TooltipAimRule.Unraisable(Sections(Tooltip()), false, false));
        }

        [Fact]
        public void AimingIsEnoughToLeaveTheBucket()
        {
            // Aiming without raising is its own finding (unraised); this one must not double-report it.
            Assert.False(TooltipAimRule.Unraisable(Sections(Tooltip()), true, false));
        }

        [Fact]
        public void RaisingIsEnoughToLeaveTheBucket()
        {
            // The player gets the tooltip: the pointer goes there on focus, whatever the node named.
            Assert.False(TooltipAimRule.Unraisable(Sections(Tooltip()), false, true));
        }

        [Fact]
        public void WordsTheModComposedAreNotATooltipPromise()
        {
            Assert.False(
                TooltipAimRule.Unraisable(Sections(NodeSection.Composed(Words)), false, false)
            );
        }

        [Fact]
        public void ContentTheControlDrawsIsNotATooltipPromise()
        {
            Assert.False(
                TooltipAimRule.Unraisable(Sections(NodeSection.Buffer(Words)), false, false)
            );
        }

        [Fact]
        public void OneTooltipSectionAmongDrawnOnesIsStillTheFinding()
        {
            Assert.True(
                TooltipAimRule.Unraisable(
                    Sections(NodeSection.Buffer(Words), Tooltip()),
                    false,
                    false
                )
            );
        }

        [Fact]
        public void ANodeThatDeclaredNoSectionsAtAllIsNotAFinding()
        {
            Assert.False(TooltipAimRule.Unraisable(null, false, false));
            Assert.False(TooltipAimRule.Unraisable(Sections(), false, false));
            Assert.False(TooltipAimRule.Unraisable(Sections(null, null), false, false));
        }

        [Fact]
        public void AReviewedSecondTooltipIsDeclaredAsDrawnContentAndSaysNothingHere()
        {
            // GraphNodes.ReviewedTooltipSection builds a Buffer section, deliberately: the node is not
            // pointing at that tooltip and never claimed to. Only the tooltip the door DERIVED counts.
            Assert.False(
                TooltipAimRule.Unraisable(Sections(NodeSection.Buffer(Words)), false, false)
            );
        }
    }
}
