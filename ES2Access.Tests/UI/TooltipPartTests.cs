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
    /// What a control's tooltip does to its focus readout. The mode is the screen author's decision,
    /// so the two modes have to differ only in what is spoken - never in what the review buffer gets,
    /// which is the tooltip's lines either way.
    ///
    /// The buffer side itself lives in the engine-side navigator (it resolves live game text), so the
    /// rule that the tooltip part is left out of the buffer is covered by the live test rather than
    /// here; what is testable offline is that the part exists, reads, and carries the tooltip kind
    /// the buffer filters on.
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

        // A button carrying a tooltip in the given mode, in a list of three so it also reads a
        // position - the shape a menu entry has.
        private static string Readout(TooltipMode mode, Func<IList<string>> tooltip)
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            NodeAnnouncement part = TooltipParts.Part(mode, tooltip);
            List<NodeAnnouncement> parts = new List<NodeAnnouncement>
            {
                Part("New Game", AnnouncementKinds.Label),
            };
            if (part != null)
            {
                parts.Add(part);
            }

            GraphBuilder b = new GraphBuilder();
            b.AddItem(Id("a"), Vt("Before"));
            b.AddItem(Id("t"), new NodeVtable { ControlType = Type("button", "button"), Announcements = parts });
            b.AddItem(Id("c"), Vt("After"));
            return GraphAnnouncer.LeafText(Node(b.Build(), "t"));
        }

        [Fact]
        public void AnnounceModeSpeaksTheTooltipAfterTheControlAndBeforeThePosition()
        {
            Assert.Equal(
                "New Game, button, Start a new game, 2 of 3",
                Readout(TooltipMode.Announce, Tooltip("Start a new game"))
            );
        }

        [Fact]
        public void AnnounceModeJoinsTheTooltipsLinesIntoOneReadout()
        {
            Assert.Equal(
                "New Game, button, Quick start, Skips setup, Uses the last settings, 2 of 3",
                Readout(
                    TooltipMode.Announce,
                    Tooltip("Quick start", "", "Skips setup", "Uses the last settings")
                )
            );
        }

        [Fact]
        public void IndicateModeSpeaksThatThereIsATooltipInsteadOfItsText()
        {
            Assert.Equal(
                "New Game, button, has tooltip, 2 of 3",
                Readout(TooltipMode.Indicate, Tooltip("A long stat block", "line two"))
            );
        }

        [Fact]
        public void NoneModeContributesNoPart()
        {
            Assert.Null(TooltipParts.Part(TooltipMode.None, Tooltip("Start a new game")));
            Assert.Equal("New Game, button, 2 of 3", Readout(TooltipMode.None, Tooltip("Start a new game")));
        }

        [Fact]
        public void AControlWithNoTooltipContributesNoPart()
        {
            Assert.Null(TooltipParts.Part(TooltipMode.Announce, null));
            Assert.Null(TooltipParts.Part(TooltipMode.Indicate, null));
        }

        [Fact]
        public void AnEmptyTooltipIsSilentWhenItsTextIsWhatWouldBeSpoken()
        {
            Assert.Equal("New Game, button, 2 of 3", Readout(TooltipMode.Announce, Tooltip()));
        }

        /// <summary>
        /// The indication is about the tooltip EXISTING, and it exists whether or not it can be read
        /// yet. Every control in this mode carries a tooltip whose words are assembled by the tooltip
        /// window as it draws - a third of a second after focus arrives, and therefore never in time
        /// for the readout that mentions it. Reading the lines first and staying quiet when they came
        /// back empty silenced the indication on every one of them.
        /// </summary>
        [Fact]
        public void IndicateModeSpeaksEvenBeforeTheTooltipHasAnyDrawnLinesToRead()
        {
            Assert.Equal("New Game, button, has tooltip, 2 of 3", Readout(TooltipMode.Indicate, Tooltip()));
            Assert.Equal(
                "New Game, button, has tooltip, 2 of 3",
                Readout(TooltipMode.Indicate, Tooltip("", "   "))
            );
        }

        [Fact]
        public void TheTooltipIsReadAtSpeakTimeSoAnAppendedReasonStaysCurrent()
        {
            List<string> lines = new List<string> { "Join a multiplayer game" };
            NodeAnnouncement part = TooltipParts.Part(TooltipMode.Announce, () => lines);
            Assert.Equal("Join a multiplayer game", part.Text());

            lines.Add("Steam is not running");
            Assert.Equal("Join a multiplayer game, Steam is not running", part.Text());
        }

        [Fact]
        public void TheTooltipPartCarriesTheTooltipKind()
        {
            Assert.Equal(
                AnnouncementKinds.Tooltip,
                TooltipParts.Part(TooltipMode.Announce, Tooltip("Text")).Kind
            );
            Assert.Equal(
                AnnouncementKinds.Tooltip,
                TooltipParts.Part(TooltipMode.Indicate, Tooltip("Text")).Kind
            );
        }

        [Fact]
        public void TheTooltipsLinesAreJoinedWithTheTranslatedListSeparator()
        {
            ModStrings.Install(new Dictionary<string, string>
            {
                { ModStrings.ListSeparator, " / " },
            });
            Assert.Equal(
                "One / Two",
                TooltipParts.Part(TooltipMode.Announce, Tooltip("One", "Two")).Text()
            );
        }

        [Fact]
        public void IndicateModeSpeaksTheTranslatedWording()
        {
            ModStrings.Install(new Dictionary<string, string>
            {
                { ModStrings.NavHasTooltip, "info available" },
            });
            Assert.Equal(
                "info available",
                TooltipParts.Part(TooltipMode.Indicate, Tooltip("Text")).Text()
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
                    TooltipParts.Part(TooltipMode.Announce, Tooltip("Start a new game")),
                },
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
