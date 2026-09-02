using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The count behind the builder's refusal: how many DIFFERENT hover surfaces a node declared.
    ///
    /// A game draws one tooltip at a time, so a node raises only the one it aims at and any other
    /// tooltip's words on it are a buffer promise nothing can keep. The rule has to tell that shape
    /// apart from the legal one it looks exactly like - a single tooltip split into a spoken half and a
    /// buffer-only half - which is why it counts sources rather than sections, and why every case here
    /// is about which sections share a source.
    /// </summary>
    public class OneTooltipRuleTests
    {
        private static readonly object Hero = new object();
        private static readonly object Refusal = new object();

        private static NodeSection Tooltip(object source)
        {
            return NodeSection.Derived(Words, TooltipMode.Announce, null, source);
        }

        [Fact]
        public void OneTooltipIsTheOrdinaryNode()
        {
            Assert.False(OneTooltipRule.Breached(Sections(Tooltip(Hero))));
            Assert.Equal(1, OneTooltipRule.Sources(Sections(Tooltip(Hero))));
        }

        [Fact]
        public void TwoDIFFERENTTooltipsAreTheRefusal()
        {
            Assert.True(OneTooltipRule.Breached(Sections(Tooltip(Hero), Tooltip(Refusal))));
            Assert.Equal(2, OneTooltipRule.Sources(Sections(Tooltip(Hero), Tooltip(Refusal))));
        }

        [Fact]
        public void TheHintSplitIsTwoSectionsOfONETooltip()
        {
            // What a hint-blocked button declares: its description speaks, the mouse instruction it ends
            // in is buffer-only, and both come off the one tooltip a hover would raise.
            IList<NodeSection> hint = Sections(
                Tooltip(Hero),
                NodeSection.Derived(Words, TooltipMode.None, null, Hero)
            );
            Assert.False(OneTooltipRule.Breached(hint));
            Assert.Equal(1, OneTooltipRule.Sources(hint));
        }

        [Fact]
        public void ComposedAndDrawnSectionsAreNotHoverSurfaces()
        {
            Assert.False(
                OneTooltipRule.Breached(
                    Sections(
                        NodeSection.Buffer(Words),
                        NodeSection.Composed(Words),
                        Tooltip(Hero),
                        NodeSection.Buffer(Words)
                    )
                )
            );
        }

        [Fact]
        public void AReviewedSecondaryIsCountedWhereverItNAMESItsTooltip()
        {
            // The reviewed-secondary shape came off NodeSection.Buffer, which names no tooltip, so the
            // rule cannot see it. Nothing here should pretend otherwise: the buffer half is not counted.
            Assert.False(
                OneTooltipRule.Breached(Sections(Tooltip(Hero), NodeSection.Buffer(Words)))
            );
        }

        [Fact]
        public void AnUnnamedSourceIsNeverCounted()
        {
            IList<NodeSection> unnamed = Sections(
                NodeSection.Derived(Words, TooltipMode.Announce, null),
                NodeSection.Derived(Words, TooltipMode.Announce, null)
            );
            Assert.False(OneTooltipRule.Breached(unnamed));
            Assert.Equal(0, OneTooltipRule.Sources(unnamed));
        }

        [Fact]
        public void ThreeSectionsOverTwoTooltipsStillBreaches()
        {
            Assert.True(
                OneTooltipRule.Breached(
                    Sections(Tooltip(Hero), Tooltip(Hero), Tooltip(Refusal))
                )
            );
        }

        [Fact]
        public void NothingDeclaredIsNotABreach()
        {
            Assert.False(OneTooltipRule.Breached(null));
            Assert.False(OneTooltipRule.Breached(new List<NodeSection>()));
            Assert.False(OneTooltipRule.Breached(Sections(null, Tooltip(Hero), null)));
        }
    }
}
