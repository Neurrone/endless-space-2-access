using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Tests.Speech;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// What focus actually says. The announcer joins parts with the ModStrings list separator and reads
    /// injected delegates, so it shares the ModStrings collection: nothing here may run beside a test
    /// that installs a translation.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class GraphAnnouncerTests : IDisposable
    {
        public GraphAnnouncerTests()
        {
            ModStrings.Reset();
            GraphAnnouncer.Reset();
        }

        public void Dispose()
        {
            GraphAnnouncer.Reset();
        }

        // A context "Settings, list" wrapping two controls.
        private static GraphRender Contextual()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Settings", "list");
            b.AddItem(new SyntheticNode(Id("a"), Vt("Item A")));
            b.AddItem(new SyntheticNode(Id("b"), Vt("Item B")));
            b.PopContext();
            return b.Build();
        }

        [Fact]
        public void EnteringAContextReadsItsLevelsThenTheControl()
        {
            GraphRender r = Contextual();
            Assert.Equal("Settings, list, Item A", GraphAnnouncer.ComposeFull(Node(r, "a")));
        }

        [Fact]
        public void MovingWithinAContextReadsOnlyTheControl()
        {
            GraphRender r = Contextual();
            Assert.Equal("Item B", GraphAnnouncer.Compose(Node(r, "a"), Node(r, "b")));
        }

        [Fact]
        public void ATransitionLabelLeadsTheLine()
        {
            GraphRender r = Contextual();
            Assert.Equal("Ships, Item B", GraphAnnouncer.Compose(Node(r, "a"), Node(r, "b"), "Ships"));
        }

        [Fact]
        public void DescendingOntoAGroupsOwnChildReadsOnlyTheChild()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")), true);
            b.AddItem(new SyntheticNode(Id("c1"), Vt("Child 1")));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.Equal("Child 1", GraphAnnouncer.Compose(Node(r, "g"), Node(r, "c1")));
        }

        [Fact]
        public void AscendingReadsJustTheLandingNode()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")), true);
            b.AddItem(new SyntheticNode(Id("c1"), Vt("Child 1")));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.Equal("Group", GraphAnnouncer.Compose(Node(r, "c1"), Node(r, "g")));
        }

        [Fact]
        public void ALevelWhoseLabelDuplicatesTheNextIsDropped()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Game difficulty");
            b.AddItem(new SyntheticNode(Id("a"), Vt("Game difficulty", Part("combo box", AnnouncementKinds.Role))));
            b.PopContext();
            GraphRender r = b.Build();
            Assert.Equal("Game difficulty, combo box", GraphAnnouncer.ComposeFull(Node(r, "a")));
        }

        [Fact]
        public void ADistinctLevelLabelIsKept()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Game difficulty");
            b.AddItem(new SyntheticNode(Id("a"), Vt("Normal")));
            b.PopContext();
            GraphRender r = b.Build();
            Assert.Equal("Game difficulty, Normal", GraphAnnouncer.ComposeFull(Node(r, "a")));
        }

        [Fact]
        public void ComposeSaysNothingWhenThereIsNoNode()
        {
            Assert.Null(GraphAnnouncer.Compose(null, null));
        }

        // ---- control types ----

        [Fact]
        public void PartsSpeakInTheControlTypesKindOrder()
        {
            ControlType toggle = Type("toggle", "toggle");
            NodeVtable vt = new NodeVtable
            {
                ControlType = toggle,
                Announcements = new[]
                {
                    Part("on", AnnouncementKinds.Value),
                    Part("Auto explore", AnnouncementKinds.Label),
                },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            Assert.Equal("Auto explore, toggle, on", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [Fact]
        public void ANodePartOverridesTheTypesCommonPartOfTheSameKind()
        {
            ControlType button = Type("button", "button");
            NodeVtable vt = new NodeVtable
            {
                ControlType = button,
                Announcements = new[]
                {
                    Part("Colonize", AnnouncementKinds.Label),
                    Part("menu button", AnnouncementKinds.Role),
                },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            Assert.Equal("Colonize, menu button", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [Fact]
        public void KindlessPartsKeepTheirDeclarationOrderAfterTheOrderedOnes()
        {
            ControlType button = Type("button", "button");
            NodeVtable vt = new NodeVtable
            {
                ControlType = button,
                Announcements = new[]
                {
                    new NodeAnnouncement(() => "first extra"),
                    Part("Colonize", AnnouncementKinds.Label),
                    new NodeAnnouncement(() => "second extra"),
                },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            Assert.Equal("Colonize, button, first extra, second extra",
                GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        // ---- the drag ----
        //
        // No screen composes these words: the announcer derives them from the control's own pick-up and
        // drop declarations, so a screen written tomorrow says them too.

        [Fact]
        public void AControlThatCanBeDraggedSaysSoAtTheTailOfItsReadout()
        {
            CarryState carry = new CarryState();
            GraphAnnouncer.Carry = carry;
            ControlType button = Type("button", "button");
            NodeVtable vt = new NodeVtable
            {
                ControlType = button,
                Announcements = new[] { Part("Explorer", AnnouncementKinds.Label) },
                Sections = new[] { NodeSection.Composed(() => new[] { "A ship" }) },
                OnPickUp = () => new CarryItem(new object(), "Explorer", "ship"),
                DropKind = "ship",
                OnDrop = held => DropResult.Done(),
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            GraphNode node = Node(b.Build(), "a");

            Assert.Equal("Explorer, button, A ship, draggable", GraphAnnouncer.LeafText(node));

            // Mid-drag the useful fact is where the thing can GO, so the same control swaps one word for
            // the other rather than saying both.
            carry.PickUp(new CarryItem(new object(), "Hunter", "ship"), "fleets");
            Assert.Equal("Explorer, button, A ship, drop target", GraphAnnouncer.LeafText(node));
        }

        [Fact]
        public void AControlWithNoDragDeclarationsSaysNothingAboutDragging()
        {
            GraphAnnouncer.Carry = new CarryState();
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("Colonize")));
            Assert.Equal("Colonize", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [Fact]
        public void EmptyPartsAreSilent()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), new NodeVtable
            {
                Announcements = new[]
                {
                    NodeAnnouncement.Static("Label"),
                    new NodeAnnouncement(() => null),
                    NodeAnnouncement.Static(""),
                    NodeAnnouncement.Static("Tail"),
                },
            }));
            Assert.Equal("Label, Tail", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [Fact]
        public void ThePartFilterDropsPartsFromTheReadout()
        {
            ControlType button = Type("button", "button");
            GraphAnnouncer.PartFilter = (type, part) => part.Kind != AnnouncementKinds.Role;

            NodeVtable vt = new NodeVtable
            {
                ControlType = button,
                Announcements = new[] { Part("Colonize", AnnouncementKinds.Label) },
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), vt));
            Assert.Equal("Colonize", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [Fact]
        public void ThePartFilterSeesTheControlType()
        {
            List<string> seen = new List<string>();
            ControlType button = Type("button", "button");
            GraphAnnouncer.PartFilter = (type, part) =>
            {
                seen.Add(type == null ? "<untyped>" : type.Key);
                return true;
            };
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), new NodeVtable
            {
                ControlType = button,
                Announcements = new[] { Part("Colonize", AnnouncementKinds.Label) },
            }));
            GraphAnnouncer.LeafText(Node(b.Build(), "a"));
            Assert.Equal(new[] { "button", "button" }, seen); // the common role part and the label
        }

        // ---- expansion + position ----

        [Fact]
        public void AGroupReadsItsExpandedState()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("Group")));
            b.EndGroup();
            Assert.Equal("Group, collapsed", GraphAnnouncer.LeafText(Node(b.Build(), "g")));
        }

        [Fact]
        public void AGroupThatSpeaksItsOwnExpansionIsNotAnnotated()
        {
            GraphAnnouncer.ExpandedStateText = expanded => expanded ? "expanded" : "collapsed";
            NodeVtable vt = Vt("Group, open");
            vt.SpeaksOwnExpansion = true;
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), vt));
            b.EndGroup();
            Assert.Equal("Group, open", GraphAnnouncer.LeafText(Node(b.Build(), "g")));
        }

        [Fact]
        public void PositionIsStampedAmongRealSiblingsOnly()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B"))).AddItem(new SyntheticNode(Id("c"), Vt("C")));
            GraphRender r = b.Build();
            Assert.Equal("B, 2 of 3", GraphAnnouncer.LeafText(Node(r, "b")));
        }

        [Fact]
        public void ALoneControlReadsNoPosition()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A")));
            Assert.Equal("A", GraphAnnouncer.LeafText(Node(b.Build(), "a")));
        }

        [Fact]
        public void AGroupsOnlyChildStillReadsOneOfOne()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(new SyntheticNode(Id("g"), Vt("G")), true);
            b.AddItem(new SyntheticNode(Id("only"), Vt("Only")));
            b.EndGroup();
            Assert.Equal("Only, 1 of 1", GraphAnnouncer.LeafText(Node(b.Build(), "only")));
        }

        [Fact]
        public void SuppressChildPositionsSilencesThePositionReadout()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Log", null, false);
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")));
            b.PopContext();
            Assert.Equal("B", GraphAnnouncer.LeafText(Node(b.Build(), "b")));
        }

        [Fact]
        public void ANodeThatSpeaksItsOwnPositionIsNotStamped()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            NodeVtable vt = Vt("B, second of two");
            vt.SpeaksOwnPosition = true;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), vt));
            Assert.Equal("B, second of two", GraphAnnouncer.LeafText(Node(b.Build(), "b")));
        }

        [Fact]
        public void ThePositionKindFilterGovernsTheAutoStampedPosition()
        {
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            GraphAnnouncer.PartFilter = (type, part) => part.Kind != AnnouncementKinds.Position;
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), Vt("A"))).AddItem(new SyntheticNode(Id("b"), Vt("B")));
            Assert.Equal("B", GraphAnnouncer.LeafText(Node(b.Build(), "b")));
        }

        [Fact]
        public void PartsAreJoinedWithTheTranslatedListSeparator()
        {
            ModStrings.Install(new Dictionary<string, string>
            {
                { ModStrings.ListSeparator, " / " },
            });
            GraphRender r = Contextual();
            Assert.Equal("Settings / list / Item A", GraphAnnouncer.ComposeFull(Node(r, "a")));
        }

        // ---- the effective-parts memo ----

        [Fact]
        public void TheEffectivePartsAreComputedOncePerNodePerRender()
        {
            GraphRender r = Contextual();
            GraphNode node = Node(r, "a");
            Assert.Same(
                GraphAnnouncer.EffectiveAnnouncements(node),
                GraphAnnouncer.EffectiveAnnouncements(node));
        }

        [Fact]
        public void ANewRenderRecomputesTheEffectiveParts()
        {
            List<NodeAnnouncement> first = GraphAnnouncer.EffectiveAnnouncements(Node(Contextual(), "a"));
            List<NodeAnnouncement> second = GraphAnnouncer.EffectiveAnnouncements(Node(Contextual(), "a"));
            Assert.NotSame(first, second);
        }

        [Fact]
        public void InstallingAPartFilterReFiltersTheSameNode()
        {
            ControlType button = Type("button", "button");
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("a"), new NodeVtable
            {
                ControlType = button,
                Announcements = new[] { Part("Colonize", AnnouncementKinds.Label) },
            }));
            GraphNode node = Node(b.Build(), "a");

            Assert.Equal("Colonize, button", GraphAnnouncer.LeafText(node));
            GraphAnnouncer.PartFilter = (type, part) => part.Kind != AnnouncementKinds.Role;
            Assert.Equal("Colonize", GraphAnnouncer.LeafText(node));
        }
    }
}
