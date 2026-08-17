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
    /// What a control's declared SECTIONS do to its focus readout. A section says what its lines are
    /// and how loud they should be; the engine derives the spoken tooltip part from the modes alone,
    /// and the review buffer from every section regardless of mode
    /// (<see cref="NodeBufferTests"/>). Nothing here is a screen's decision any more, which is the
    /// point: the two surfaces cannot drift apart if they come from one declaration.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class TooltipPartTests : IDisposable
    {
        public TooltipPartTests()
        {
            ModStrings.Reset();
            GraphAnnouncer.Reset();
        }

        public void Dispose()
        {
            GraphAnnouncer.Reset();
        }

        private static Func<IList<string>> Tooltip(params string[] lines)
        {
            return () => new List<string>(lines);
        }

        private static NodeSection Section(TooltipMode mode, params string[] lines)
        {
            return new NodeSection(Tooltip(lines), mode);
        }

        // A button carrying the given sections, in a list of three so it also reads a position - the
        // shape a menu entry has.
        private static string Readout(params NodeSection[] sections)
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(Id("a"), Vt("Before"));
            b.AddItem(
                Id("t"),
                new NodeVtable
                {
                    ControlType = Type("button", "button"),
                    Announcements = new List<NodeAnnouncement>
                    {
                        Part("New Game", AnnouncementKinds.Label),
                    },
                    Sections = sections,
                }
            );
            b.AddItem(Id("c"), Vt("After"));
            return GraphAnnouncer.LeafText(Node(b.Build(), "t"));
        }

        [Fact]
        public void AnnounceModeSpeaksTheTooltipAfterTheControlAndBeforeThePosition()
        {
            Assert.Equal(
                "New Game, button, Start a new game, 2 of 3",
                Readout(Section(TooltipMode.Announce, "Start a new game"))
            );
        }

        [Fact]
        public void AnnounceModeJoinsTheTooltipsLinesIntoOneReadout()
        {
            Assert.Equal(
                "New Game, button, Quick start Skips setup Uses the last settings, 2 of 3",
                Readout(
                    Section(
                        TooltipMode.Announce,
                        "Quick start",
                        "",
                        "Skips setup",
                        "Uses the last settings"
                    )
                )
            );
        }

        /// <summary>
        /// A long tooltip is READ, never announced - not its words, and not the fact that it is there.
        ///
        /// The mod used to say "has tooltip" here. On a live screen almost every control carries one,
        /// so the phrase arrived on nearly every readout and distinguished nothing; the convention that
        /// replaced it is simply that the player checks the review buffer, on any control, whenever
        /// they want more. Nothing was lost from the buffer - only from the announcement.
        /// </summary>
        [Fact]
        public void IndicateModeSaysNothingInTheReadout()
        {
            Assert.Equal(
                "New Game, button, 2 of 3",
                Readout(Section(TooltipMode.Indicate, "A long stat block", "line two"))
            );
            Assert.Null(TooltipParts.Part(new[] { Section(TooltipMode.Indicate, "A long stat block") }));
            Assert.Null(TooltipParts.Part(TooltipMode.Indicate, Tooltip("A long stat block")));
        }

        /// <summary>Whatever its state: with words, without them, and with the engine's own
        /// would-it-draw test answering either way. The test is still declared - the tooltip-parity
        /// audit and the focus pointer both ask it - it just no longer decides a spoken word.</summary>
        [Fact]
        public void AnIndicateSectionIsSilentWhateverItsStateIs()
        {
            Assert.Equal("New Game, button, 2 of 3", Readout(Section(TooltipMode.Indicate)));
            Assert.Equal("New Game, button, 2 of 3", Readout(Section(TooltipMode.Indicate, "", "   ")));
            Assert.Equal(
                "New Game, button, 2 of 3",
                Readout(new NodeSection(Tooltip(), TooltipMode.Indicate, () => false))
            );
            Assert.Equal(
                "New Game, button, 2 of 3",
                Readout(new NodeSection(Tooltip("A stat block"), TooltipMode.Indicate, () => true))
            );
        }

        [Fact]
        public void NoneModeContributesNoPart()
        {
            Assert.Null(TooltipParts.Part(new[] { Section(TooltipMode.None, "Start a new game") }));
            Assert.Equal(
                "New Game, button, 2 of 3",
                Readout(Section(TooltipMode.None, "Start a new game"))
            );
        }

        [Fact]
        public void AControlWithNoSectionsContributesNoPart()
        {
            Assert.Null(TooltipParts.Part(null));
            Assert.Null(TooltipParts.Part(new NodeSection[0]));
            Assert.Null(TooltipParts.Part(new NodeSection[] { null }));
            Assert.Null(TooltipParts.Part(new[] { new NodeSection(null, TooltipMode.Announce) }));
        }

        [Fact]
        public void AnEmptyTooltipIsSilentWhenItsTextIsWhatWouldBeSpoken()
        {
            Assert.Equal("New Game, button, 2 of 3", Readout(Section(TooltipMode.Announce)));
        }

        /// <summary>
        /// A row carries the heading's explanation of what the measure IS and the value's description
        /// of what it SAYS, in drawn order. The one the player asked for by landing there is the
        /// value's - the last one drawn - so that is the one spoken.
        /// </summary>
        [Fact]
        public void TheLastShortTooltipIsTheOneSpoken()
        {
            Assert.Equal(
                "New Game, button, Currently 8 empires, 2 of 3",
                Readout(
                    Section(TooltipMode.Announce, "How many empires play"),
                    Section(TooltipMode.Announce, "Currently 8 empires")
                )
            );
        }

        /// <summary>
        /// A long section beside a short one takes nothing away from it and adds nothing of its own.
        /// The short one's words are the sentence the game's author wrote for exactly this moment, and
        /// they are said wherever the long one sits in the row; the long one is in the buffer, which is
        /// where the player looks for it whether or not anything said so.
        /// </summary>
        [Fact]
        public void ALongTooltipLeavesTheShortOnesWordsAloneAndAddsNothing()
        {
            Assert.Equal(
                "New Game, button, What this measures, 2 of 3",
                Readout(
                    Section(TooltipMode.Announce, "What this measures"),
                    Section(TooltipMode.Indicate, "a stat block")
                )
            );
            Assert.Equal(
                "New Game, button, What it is set to, 2 of 3",
                Readout(
                    Section(TooltipMode.Indicate, "a stat block"),
                    Section(TooltipMode.Announce, "What it is set to")
                )
            );
        }

        /// <summary>A buffer-only section is the control's drawn face: reviewable, and never a word in
        /// the readout - not its text, and not an indication that it exists.</summary>
        [Fact]
        public void ABufferOnlySectionIsNeitherSpokenNorIndicated()
        {
            Assert.Equal(
                "New Game, button, The description, 2 of 3",
                Readout(
                    Section(TooltipMode.None, "Food 12", "Industry 8"),
                    Section(TooltipMode.Announce, "The description")
                )
            );
            Assert.Equal(
                "New Game, button, 2 of 3",
                Readout(Section(TooltipMode.None, "Food 12", "Industry 8"))
            );
        }

        [Fact]
        public void TheTooltipIsReadAtSpeakTimeSoAnAppendedReasonStaysCurrent()
        {
            List<string> lines = new List<string> { "Join a multiplayer game" };
            NodeAnnouncement part = TooltipParts.Part(
                new[] { new NodeSection(() => lines, TooltipMode.Announce) }
            );
            Assert.Equal("Join a multiplayer game", part.Text());

            lines.Add("Steam is not running");
            Assert.Equal("Join a multiplayer game Steam is not running", part.Text());
        }

        [Fact]
        public void TheTooltipPartCarriesTheTooltipKind()
        {
            Assert.Equal(
                AnnouncementKinds.Tooltip,
                TooltipParts.Part(new[] { Section(TooltipMode.Announce, "Text") }).Kind
            );
        }

        /// <summary>The lines are the game's prose - the translated LIST separator must never
        /// appear between them, whatever it is; the pause belongs to the game's own punctuation.</summary>
        [Fact]
        public void TheTooltipsLinesAreJoinedWithASpaceNeverTheListSeparator()
        {
            ModStrings.Install(new Dictionary<string, string>
            {
                { ModStrings.ListSeparator, " / " },
            });
            Assert.Equal(
                "One Two",
                TooltipParts.Part(new[] { Section(TooltipMode.Announce, "One", "Two") }).Text()
            );
        }

        /// <summary>A screen may still declare a tooltip-kind part of its own for something no section
        /// can express - a drop-list entry's live refusal - and the derived part must survive beside
        /// it, or a row the screen has one extra word about loses the tooltip it was reading.</summary>
        [Fact]
        public void ASectionStillSpeaksBesideAPartTheScreenDeclaredItself()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(
                Id("t"),
                new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        Part("Vaulters", AnnouncementKinds.Label),
                        Part("The content pack is not activated", AnnouncementKinds.Tooltip),
                    },
                    Sections = new[] { Section(TooltipMode.Announce, "A faction of exiles") },
                }
            );
            Assert.Equal(
                "Vaulters, The content pack is not activated, A faction of exiles",
                GraphAnnouncer.LeafText(Node(b.Build(), "t"))
            );
        }

        [Fact]
        public void AGroupReadsItsExpansionStateBeforeItsTooltip()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(Id("g"), new NodeVtable
            {
                ControlType = Type("button", "button"),
                Announcements = new List<NodeAnnouncement>
                {
                    Part("New Game", AnnouncementKinds.Label),
                },
                Sections = new[] { Section(TooltipMode.Announce, "Start a new game") },
            });
            b.EndGroup();
            Assert.Equal(
                "New Game, button, collapsed, Start a new game",
                GraphAnnouncer.LeafText(Node(b.Build(), "g"))
            );
        }

        [Fact]
        public void AGroupWithNoTooltipStillReadsItsExpansionStateLast()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(Id("g"), Vt("Options"));
            b.EndGroup();
            Assert.Equal("Options, collapsed", GraphAnnouncer.LeafText(Node(b.Build(), "g")));
        }
    }
}
