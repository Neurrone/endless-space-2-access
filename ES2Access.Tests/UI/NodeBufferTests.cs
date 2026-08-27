using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Tests.Speech;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The BUFFER half of the same declaration <see cref="TooltipPartTests"/> covers the spoken half
    /// of. One list of sections, two surfaces, derived - the pairing these tests exist to hold, since
    /// wiring them separately is what let a row announce a tooltip it could not review three times
    /// over.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class NodeBufferTests : IDisposable
    {
        public NodeBufferTests()
        {
            ModStrings.Reset();
            GraphAnnouncer.Reset();
        }

        public void Dispose()
        {
            GraphAnnouncer.Reset();
        }

        private static NodeSection Section(TooltipMode mode, params string[] lines)
        {
            List<string> list = new List<string>(lines);
            return new NodeSection(() => list, mode);
        }

        private static List<string> Buffer(NodeVtable vtable)
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("Before")));
            b.AddItem(new SyntheticNode(Id("t"), vtable));
            b.AddItem(new SyntheticNode(Id("c"), Vt("After")));
            return NodeBuffer.Lines(Node(b.Build(), "t"));
        }

        private static NodeVtable Control(params NodeSection[] sections)
        {
            return new NodeVtable
            {
                ControlType = Type("button", "button"),
                Announcements = new List<NodeAnnouncement>
                {
                    Part("Difficulty", AnnouncementKinds.Label),
                    Part("Normal", AnnouncementKinds.Value),
                    Part("unavailable", AnnouncementKinds.Enabled),
                },
                Sections = sections,
            };
        }

        /// <summary>The head is automatic: the control's name and the state words its readout appends,
        /// never its role word and never the auto-stamped position, which describe the control rather
        /// than being anything it has to say.</summary>
        [Fact]
        public void TheHeadIsTheControlsOwnNameAndStateWithoutItsRoleOrPosition()
        {
            Assert.Equal(new[] { "Difficulty", "Normal", "unavailable" }, Buffer(Control()));
        }

        /// <summary>A control that declares NO sections still reviews correctly - which is what lets a
        /// paragraph of lore be declared as nothing but a label and still be walkable.</summary>
        [Fact]
        public void AControlWithNoSectionsBuffersItsOwnReadout()
        {
            Assert.Equal(
                new[] { "The Empire is under a central monarchy." },
                Buffer(
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            Part("The Empire is under a central monarchy.", AnnouncementKinds.Label),
                        },
                    }
                )
            );
        }

        /// <summary>A control whose first part is not a label - a table cell, which leads with its
        /// value because the column's caption is the edge the player crossed to reach it - opens its
        /// buffer with that value ONCE.</summary>
        [Fact]
        public void AControlLeadingWithItsValueDoesNotBufferItTwice()
        {
            Assert.Equal(
                new[] { "37", "selected" },
                Buffer(
                    new NodeVtable
                    {
                        ControlType = Type("text", null),
                        Announcements = new List<NodeAnnouncement>
                        {
                            Part("37", AnnouncementKinds.Value),
                            Part("selected", AnnouncementKinds.Selected),
                        },
                    }
                )
            );
        }

        /// <summary>A control whose readout leaves out a word the buffer needs declares its own head -
        /// a table cell, whose column caption is spoken as the crossed edge and so is not in the
        /// readout. The declared head replaces the readout's, is what the first content line is tested
        /// against, and does not make the readout's own first part read again.</summary>
        [Fact]
        public void ADeclaredHeadOpensTheBufferAndTheCellsOwnFirstLineIsThenTheSame()
        {
            Assert.Equal(
                new[] { "Mods, Valid", "selected", "Requires: Vanilla 1.5" },
                Buffer(
                    new NodeVtable
                    {
                        ControlType = Type("text", null),
                        Announcements = new List<NodeAnnouncement>
                        {
                            Part("Valid", AnnouncementKinds.Value),
                            Part("selected", AnnouncementKinds.Selected),
                        },
                        BufferHead = () => "Mods, Valid",
                        Sections = new List<NodeSection>
                        {
                            Section(TooltipMode.None, "Mods, Valid"),
                            Section(TooltipMode.Indicate, "Requires: Vanilla 1.5"),
                        },
                    }
                )
            );
        }

        /// <summary>Every section is reviewable whatever its mode: that is what makes "announce and
        /// review" and "indicate and review" the same promise.</summary>
        [Fact]
        public void EverySectionReachesTheBufferInDeclaredOrderWhateverItsMode()
        {
            Assert.Equal(
                new[]
                {
                    "Difficulty",
                    "Normal",
                    "unavailable",
                    "What this measures",
                    "Food 12",
                    "A stat block",
                },
                Buffer(
                    Control(
                        Section(TooltipMode.Announce, "What this measures"),
                        Section(TooltipMode.None, "Food 12"),
                        Section(TooltipMode.Indicate, "A stat block")
                    )
                )
            );
        }

        /// <summary>An indicated tooltip must be readable from the buffer - the indication is a promise
        /// that there is something there.</summary>
        [Fact]
        public void AnIndicatedTooltipIsAlwaysInTheBuffer()
        {
            NodeVtable vtable = Control(Section(TooltipMode.Indicate, "Range 4", "Damage 12"));
            Assert.Contains("Range 4", Buffer(vtable));
            Assert.Contains("Damage 12", Buffer(vtable));
        }

        /// <summary>Native tooltips routinely open by repeating the control's name; the buffer already
        /// opened with it.</summary>
        [Fact]
        public void AFirstLineThatOnlyRepeatsTheLabelIsDropped()
        {
            Assert.Equal(
                new[] { "Difficulty", "Normal", "unavailable", "How hard the game is" },
                Buffer(
                    Control(Section(TooltipMode.Announce, " difficulty ", "How hard the game is"))
                )
            );
        }

        /// <summary>Only the FIRST line of the whole list, and only an exact repeat: a later line that
        /// happens to match, or a heading that adds anything, still reads.</summary>
        [Fact]
        public void OnlyTheVeryFirstLineIsTestedAgainstTheLabel()
        {
            Assert.Equal(
                new[] { "Difficulty", "Normal", "unavailable", "How hard", "Difficulty" },
                Buffer(Control(Section(TooltipMode.Announce, "How hard", "Difficulty")))
            );
            Assert.Equal(
                new[] { "Difficulty", "Normal", "unavailable", "Difficulty settings" },
                Buffer(Control(Section(TooltipMode.Announce, "Difficulty settings")))
            );
        }

        /// <summary>The dedupe applies across the section boundary too: the first line that exists is
        /// the one tested, whichever section it came out of.</summary>
        [Fact]
        public void TheDedupeLooksAtTheFirstLineOfTheFirstSectionThatHasOne()
        {
            Assert.Equal(
                new[] { "Difficulty", "Normal", "unavailable", "How hard the game is" },
                Buffer(
                    Control(
                        Section(TooltipMode.None),
                        Section(TooltipMode.Announce, "Difficulty", "How hard the game is")
                    )
                )
            );
        }

        /// <summary>A group's expanded state is part of what the readout says, so it is part of the
        /// head - and it comes before the sections, being state rather than content.</summary>
        [Fact]
        public void AGroupsExpansionStateIsPartOfTheHead()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(
                Id("g"),
                new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        Part("Load Game", AnnouncementKinds.Label),
                    },
                    Sections = new[] { Section(TooltipMode.Announce, "Load a saved game") },
                }
            ));
            b.EndGroup();
            Assert.Equal(
                new[] { "Load Game", "collapsed", "Load a saved game" },
                NodeBuffer.Lines(Node(b.Build(), "g"))
            );
        }

        /// <summary>A section that throws while resolving is a section with nothing to say, not a
        /// screen with an empty buffer: the game's own readers throw on half-torn-down widgets.</summary>
        [Fact]
        public void ASectionThatThrowsIsSkippedAndTheRestStillRead()
        {
            Assert.Equal(
                new[] { "Difficulty", "Normal", "unavailable", "Still here" },
                Buffer(
                    Control(
                        new NodeSection(
                            () => { throw new InvalidOperationException(); },
                            TooltipMode.Announce
                        ),
                        Section(TooltipMode.None, "Still here")
                    )
                )
            );
        }
    }
}
