using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The engine: stepping, Tab-stop and region operations, tree semantics, and — the part that earns
    /// the two-tier identity — where focus lands after the world was rebuilt under it.
    /// </summary>
    public class KeyGraphTests
    {
        private static string Key(GraphNode node)
        {
            return node == null ? null : (string)node.Id.StructuralKey;
        }

        private static string Focused(KeyGraph g)
        {
            return Key(g.CurrentNode);
        }

        // ---- stepping ----

        [Fact]
        public void FocusStartsAtTheStartNode()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B"))), state);
            Assert.True(g.Rerender());
            Assert.Equal("a", Focused(g));
        }

        [Fact]
        public void MoveReportsTheCrossedEdgeLabel()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.AddNode(Id("a"), Vt("A")).AddNode(Id("b"), Vt("B"));
                b.Connect(Id("a"), GraphDir.Right, Id("b"), "Ships");
            }), state);
            g.Rerender();
            MoveResult r = g.Move(GraphDir.Right);
            Assert.True(r.Moved);
            Assert.Equal("Ships", r.TransitionLabel);
            Assert.Equal("b", Key(r.To));
            Assert.Equal("a", Key(r.From));
        }

        [Fact]
        public void MoveAtAnEdgeReportsNotMovedAndKeepsFocus()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B"))), state);
            g.Rerender();
            MoveResult r = g.Move(GraphDir.Up);
            Assert.False(r.Moved);
            Assert.Same(r.From, r.To);
            Assert.Equal("a", Focused(g));
        }

        [Fact]
        public void MoveToEdgeRunsToTheEndOfTheLine()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).AddItem(Id("c"), Vt("C"))), state);
            g.Rerender();
            Assert.True(g.MoveToEdge(GraphDir.Down).Moved);
            Assert.Equal("c", Focused(g));
        }

        // ---- tab stops ----

        private static KeyGraph TwoStops(GraphState state)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.BeginStop("s1").AddItem(Id("a1"), Vt("A1")).AddItem(Id("a2"), Vt("A2"));
                b.BeginStop("s2").AddItem(Id("b1"), Vt("B1")).AddItem(Id("b2"), Vt("B2"));
            }), state);
        }

        [Fact]
        public void MoveStopCyclesStopsInFirstAppearanceOrder()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            Assert.True(g.MoveStop(1, false).Moved);
            Assert.Equal("b1", Focused(g));
        }

        [Fact]
        public void MoveStopStopsAtTheLastStopWithoutWrap()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            g.MoveStop(1, false);
            Assert.False(g.MoveStop(1, false).Moved);
            Assert.Equal("b1", Focused(g));
        }

        [Fact]
        public void MoveStopWrapsWhenAsked()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            g.MoveStop(1, false);
            Assert.True(g.MoveStop(1, true).Moved);
            Assert.Equal("a1", Focused(g));
        }

        [Fact]
        public void MoveStopWrapsBackwardsFromTheFirstStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            Assert.True(g.MoveStop(-1, true).Moved);
            Assert.Equal("b1", Focused(g));
        }

        // What Tab does on a page with one panel: nothing. Wrapping round to the stop the player is
        // already on is not a move, so the key is consumed and says nothing rather than re-reading the
        // same control (GraphNavigator.Stop).
        [Fact]
        public void MoveStopWithOnlyOneStopNeverMoves()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.BeginStop("s1").AddItem(Id("a1"), Vt("A1")).AddItem(Id("a2"), Vt("A2"))), state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.Equal("a2", Focused(g));
            Assert.False(g.MoveStop(1, true).Moved);
            Assert.False(g.MoveStop(-1, true).Moved);
            Assert.Equal("a2", Focused(g));
        }

        [Fact]
        public void ReturningToAStopLandsOnItsRememberedPosition()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStops(state);
            g.Rerender();
            g.MoveStop(1, false);
            g.Move(GraphDir.Down);
            Assert.Equal("b2", Focused(g));
            g.MoveStop(-1, false);
            Assert.Equal("a1", Focused(g));
            g.MoveStop(1, false);
            Assert.Equal("b2", Focused(g));
        }

        [Fact]
        public void InitialFocusPrefersTheSelectedMemberOfTheStartStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.AddItem(Id("a"), Vt("A", Part(null, AnnouncementKinds.Selected)));
                b.AddItem(Id("b"), Vt("B", Part("selected", AnnouncementKinds.Selected)));
                b.AddItem(Id("c"), Vt("C", Part(null, AnnouncementKinds.Selected)));
            }), state);
            g.Rerender();
            Assert.Equal("b", Focused(g));
        }

        /// <summary>A start node that is not one of the alternatives keeps focus: a popup's block of
        /// text is where the screen wants reading to begin, and the dots marking which page it is on
        /// merely share its stop.</summary>
        [Fact]
        public void InitialFocusStaysOnAStartThatIsNotOneOfTheAlternatives()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.AddNode(Id("text"), Vt("Some words"));
                b.SetStart(Id("text"));
                b.AddItem(Id("dot1"), Vt("Page 1", Part("selected", AnnouncementKinds.Selected)));
                b.AddItem(Id("dot2"), Vt("Page 2", Part(null, AnnouncementKinds.Selected)));
            }), state);
            g.Rerender();
            Assert.Equal("text", Focused(g));
        }

        /// <summary>A stop whose first nodes are not what the player came for - a table's sort headings,
        /// where the SORTED column reads "selected" - says where Tab lands, and the
        /// land-on-the-selected-one rule runs from there rather than over the headings.</summary>
        private static KeyGraph TableUnderHeadings(GraphState state, bool rowSelected)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.BeginStop("elsewhere").AddItem(Id("away"), Vt("Away"));
                b.BeginStop("table");
                b.AddItem(Id("head1"), Vt("Name", Part("selected", AnnouncementKinds.Selected)));
                b.AddItem(Id("head2"), Vt("Ships", Part(null, AnnouncementKinds.Selected)));
                b.AddItem(Id("row1"), Vt("Alpha", Part(null, AnnouncementKinds.Selected)));
                b.AddItem(
                    Id("row2"),
                    Vt("Beta", Part(rowSelected ? "selected" : null, AnnouncementKinds.Selected))
                );
                b.LandStopOn(Id("row1"));
            }), state);
        }

        [Fact]
        public void ATabStopLandsWhereItSaidRatherThanOnItsFirstNode()
        {
            GraphState state = new GraphState();
            KeyGraph g = TableUnderHeadings(state, false);
            g.Rerender();
            Assert.True(g.MoveStop(1, true).Moved);
            Assert.Equal("row1", Focused(g));
        }

        [Fact]
        public void ATabStopWithADeclaredLandingStillPrefersASelectedNodeBelowIt()
        {
            GraphState state = new GraphState();
            KeyGraph g = TableUnderHeadings(state, true);
            g.Rerender();
            Assert.True(g.MoveStop(1, true).Moved);
            Assert.Equal("row2", Focused(g));
        }

        // ---- regions ----

        [Fact]
        public void MoveRegionJumpsToTheNextRegionOfTheSameStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.SetRegion("r1").AddItem(Id("a1"), Vt("A1")).AddItem(Id("a2"), Vt("A2"));
                b.SetRegion("r2").AddItem(Id("b1"), Vt("B1")).AddItem(Id("b2"), Vt("B2"));
            }), state);
            g.Rerender();
            Assert.True(g.MoveRegion(1).Moved);
            Assert.Equal("b1", Focused(g));
            Assert.False(g.MoveRegion(1).Moved);
            Assert.True(g.MoveRegion(-1).Moved);
            Assert.Equal("a1", Focused(g));
        }

        [Fact]
        public void MoveRegionNeverLeavesTheCurrentStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.SetRegion("r1").AddItem(Id("a1"), Vt("A1"));
                b.BeginStop("s2").SetRegion("r2").AddItem(Id("b1"), Vt("B1"));
            }), state);
            g.Rerender();
            Assert.False(g.MoveRegion(1).Moved);
            Assert.Equal("a1", Focused(g));
        }

        // ---- trees ----

        private static KeyGraph Tree(GraphState state, bool withChildren = true)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.AddItem(Id("top"), Vt("Top"));
                b.BeginGroup(Id("g"), Vt("Group"));
                if (withChildren)
                {
                    b.AddItem(Id("c1"), Vt("Child 1"));
                    b.AddItem(Id("c2"), Vt("Child 2"));
                }
                b.EndGroup();
                b.AddItem(Id("bottom"), Vt("Bottom"));
            }, state), state);
        }

        [Fact]
        public void TreeRightExpandsThenDescends()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.Equal("g", Focused(g));

            Assert.Equal(KeyGraph.TreeMove.Expanded, g.TreeRight().Kind);
            Assert.Contains(Id("g"), state.Expanded);
            Assert.Equal("g", Focused(g));

            KeyGraph.TreeResult descend = g.TreeRight();
            Assert.Equal(KeyGraph.TreeMove.Descended, descend.Kind);
            Assert.Equal("c1", Key(descend.Move.To));
            Assert.Equal("c1", Focused(g));
        }

        [Fact]
        public void TreeLeftAscendsThenCollapses()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            Assert.Equal("c1", Focused(g));

            KeyGraph.TreeResult up = g.TreeLeft();
            Assert.Equal(KeyGraph.TreeMove.Ascended, up.Kind);
            Assert.Equal("g", Focused(g));

            Assert.Equal(KeyGraph.TreeMove.Collapsed, g.TreeLeft().Kind);
            Assert.DoesNotContain(Id("g"), state.Expanded);
            Assert.Equal("g", Focused(g));
        }

        [Fact]
        public void ExpansionSurvivesRebuilds()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            Assert.True(g.Rerender());
            Assert.NotNull(g.Current.NodeAt(Id("c2")));
            Assert.True(g.Current.NodeAt(Id("g")).Expanded);
        }

        [Fact]
        public void ExpandingAnEmptyGroupRecollapsesIt()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state, false);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.Equal(KeyGraph.TreeMove.EmptyGroup, g.TreeRight().Kind);
            Assert.DoesNotContain(Id("g"), state.Expanded);
            Assert.False(g.Current.NodeAt(Id("g")).Expanded);
        }

        // A group whose children all sit under a NAMED SECTION - the shape the "Tooltips" region of a
        // dossier-bearing node has, and the one a technology dot has when everything it holds is a
        // dossier. The section is a context: non-focusable, so it is the children's parent and the
        // group is their grandparent. Comparing parents alone called the group empty and
        // auto-recollapsed it, which is "Nothing in here" said over a group full of nodes.
        private static KeyGraph Sectioned(GraphState state)
        {
            return new KeyGraph(
                Renderer(
                    b =>
                    {
                        b.BeginGroup(Id("g"), Vt("Group"));
                        b.PushContext("Tooltips");
                        b.AddItem(Id("t1"), Vt("Dossier 1"));
                        b.AddItem(Id("t2"), Vt("Dossier 2"));
                        b.PopContext();
                        b.EndGroup();
                    },
                    state
                ),
                state
            );
        }

        [Fact]
        public void AGroupWhoseChildrenAreAllInASectionIsNotEmpty()
        {
            GraphState state = new GraphState();
            KeyGraph g = Sectioned(state);
            g.Rerender();
            Assert.Equal("g", Focused(g));

            Assert.Equal(KeyGraph.TreeMove.Expanded, g.TreeRight().Kind);
            Assert.Contains(Id("g"), state.Expanded);

            KeyGraph.TreeResult descend = g.TreeRight();
            Assert.Equal(KeyGraph.TreeMove.Descended, descend.Kind);
            Assert.Equal("t1", Focused(g));
        }

        [Fact]
        public void DescendingSkipsPastANestedGroupsOwnChildren()
        {
            GraphState state = new GraphState();
            state.Expanded.Add(Id("outer"));
            state.Expanded.Add(Id("inner"));
            KeyGraph g = new KeyGraph(
                Renderer(
                    b =>
                    {
                        b.BeginGroup(Id("outer"), Vt("Outer"));
                        b.BeginGroup(Id("inner"), Vt("Inner"));
                        b.AddItem(Id("deep"), Vt("Deep"));
                        b.EndGroup();
                        b.EndGroup();
                    },
                    state
                ),
                state
            );
            g.Rerender();
            Assert.Equal("outer", Focused(g));
            Assert.Equal(KeyGraph.TreeMove.Descended, g.TreeRight().Kind);
            Assert.Equal("inner", Focused(g));
        }

        // ---- following a reference (a leaf that names somewhere else) ----

        /// <summary>A tree whose second child NAMES the top-level node rather than holding anything of
        /// its own - the shape a starlane has, pointing at the system it runs to.</summary>
        private static KeyGraph FollowTree(GraphState state, List<string> followed, bool expandable)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.AddItem(Id("top"), Vt("Top"));
                b.BeginGroup(Id("g"), Vt("Group"));
                b.AddItem(Id("c1"), Vt("Child 1"));
                NodeVtable lane = Vt("Lane");
                lane.OnFollow = () => followed.Add("followed");
                if (expandable)
                {
                    b.BeginGroup(Id("lane"), lane);
                    b.AddItem(Id("far"), Vt("Far"));
                    b.EndGroup();
                }
                else
                {
                    b.AddItem(Id("lane"), lane);
                }
                b.EndGroup();
            }, state), state);
        }

        // Right on the leaf runs the handler and reports Followed - the handler moves focus itself, so
        // the engine leaves the cursor exactly where it was and says nothing.
        [Fact]
        public void TreeRightFollowsALeafThatNamesSomewhereElse()
        {
            GraphState state = new GraphState();
            List<string> followed = new List<string>();
            KeyGraph g = FollowTree(state, followed, false);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            g.Move(GraphDir.Down);
            Assert.Equal("lane", Focused(g));

            Assert.Equal(KeyGraph.TreeMove.Followed, g.TreeRight().Kind);
            Assert.Single(followed);
            Assert.Equal("lane", Focused(g));
        }

        // The same leaf without a handler is an ordinary Leaf: consumed, nothing run.
        [Fact]
        public void TreeRightOnALeafWithoutOneStaysALeaf()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            Assert.Equal("c1", Focused(g));
            Assert.Equal(KeyGraph.TreeMove.Leaf, g.TreeRight().Kind);
        }

        // A node that has children of its own is not standing in for somewhere else: its own expansion
        // wins and the follow handler is never asked.
        [Fact]
        public void AnExpandableNodeIgnoresItsFollowHandler()
        {
            GraphState state = new GraphState();
            List<string> followed = new List<string>();
            KeyGraph g = FollowTree(state, followed, true);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            g.Move(GraphDir.Down);
            Assert.Equal("lane", Focused(g));

            Assert.Equal(KeyGraph.TreeMove.Expanded, g.TreeRight().Kind);
            Assert.Empty(followed);
            Assert.Equal(KeyGraph.TreeMove.Descended, g.TreeRight().Kind);
            Assert.Equal("far", Focused(g));
            Assert.Empty(followed);
        }

        /// <summary>Two panels, each with a group at the top level: Home and End inside one of them are
        /// about that panel and never reach into the other. The trap is that a top-level node has no
        /// parent, so "same parent" alone made every root-level node on the page a sibling.</summary>
        private static KeyGraph TwoStopTrees(GraphState state)
        {
            return new KeyGraph(Renderer(b =>
            {
                b.BeginStop("s1");
                b.AddItem(Id("a1"), Vt("A1"));
                b.BeginGroup(Id("g"), Vt("Group"));
                b.AddItem(Id("c1"), Vt("Child 1"));
                b.AddItem(Id("c2"), Vt("Child 2"));
                b.EndGroup();
                b.AddItem(Id("a2"), Vt("A2"));
                b.BeginStop("s2");
                b.AddItem(Id("b1"), Vt("B1"));
                b.AddItem(Id("b2"), Vt("B2"));
            }, state), state);
        }

        [Fact]
        public void EndOnAnExpandedGroupStaysInItsOwnStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStopTrees(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.Equal("g", Focused(g));
            Assert.Equal(KeyGraph.TreeMove.Expanded, g.TreeRight().Kind);

            MoveResult end = g.MoveToSiblingEdge(false);
            Assert.True(end.Moved);
            Assert.Equal("a2", Key(end.To));

            MoveResult home = g.MoveToSiblingEdge(true);
            Assert.True(home.Moved);
            Assert.Equal("a1", Key(home.To));
        }

        [Fact]
        public void EndOnAChildStaysAmongItsSiblings()
        {
            GraphState state = new GraphState();
            KeyGraph g = TwoStopTrees(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            Assert.Equal("c1", Focused(g));

            Assert.Equal("c2", Key(g.MoveToSiblingEdge(false).To));
            Assert.Equal("c1", Key(g.MoveToSiblingEdge(true).To));
        }

        [Fact]
        public void SiblingEdgesInAOneNodeStopMoveNothing()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.BeginStop("s1");
                b.BeginGroup(Id("g"), Vt("Group"));
                b.AddItem(Id("c1"), Vt("Child"));
                b.EndGroup();
                b.BeginStop("s2").AddItem(Id("z"), Vt("Z"));
            }, state), state);
            g.Rerender();
            Assert.Equal("g", Focused(g));
            Assert.False(g.MoveToSiblingEdge(false).Moved);
            Assert.False(g.MoveToSiblingEdge(true).Moved);
            Assert.Equal("g", Focused(g));
        }

        [Fact]
        public void TreeMovesOutsideATreeReportNone()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            Assert.Equal("top", Focused(g));
            Assert.Equal(KeyGraph.TreeMove.None, g.TreeRight().Kind);
            Assert.Equal(KeyGraph.TreeMove.None, g.TreeLeft().Kind);
        }

        [Fact]
        public void RightOnALeafInsideATreeIsConsumed()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.TreeRight();
            g.TreeRight();
            Assert.Equal(KeyGraph.TreeMove.Leaf, g.TreeRight().Kind);
        }

        [Fact]
        public void ExpansionGoesThroughTheVtableOverrideWhenDeclared()
        {
            GraphState state = new GraphState();
            bool expanded = false;
            NodeVtable header = Vt("Group");
            header.OnExpand = () => expanded = true;
            header.OnCollapse = () => expanded = false;

            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder(state.Expanded);
                b.BeginGroup(Id("g"), header, expanded);
                b.AddItem(Id("c1"), Vt("Child"));
                b.EndGroup();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.Equal(KeyGraph.TreeMove.Expanded, g.TreeRight().Kind);
            Assert.True(expanded);
            Assert.Empty(state.Expanded); // the persistent set stays out of it
        }

        // ---- reconciliation ----

        [Fact]
        public void FocusFollowsTheBackingObjectWhenItsStructuralKeyChanges()
        {
            GraphState state = new GraphState();
            object thing = new object();
            string key = "slot1";
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(Id("other"), Vt("Other"));
                b.AddItem(ControlId.Referenced(thing, key), Vt("Thing"));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.Equal("slot1", Focused(g));

            key = "slot9"; // the object moved
            g.Rerender();
            Assert.Equal("slot9", Focused(g));
        }

        [Fact]
        public void FocusFollowsTheStructuralKeyWhenTheBackingObjectIsRebuilt()
        {
            GraphState state = new GraphState();
            object thing = new object();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(Id("other"), Vt("Other"));
                b.AddItem(ControlId.Referenced(thing, "slot1"), Vt("Thing"));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);

            thing = new object(); // same logical control, fresh instance
            g.Rerender();
            Assert.Equal("slot1", Focused(g));
            Assert.Same(thing, g.CurrentNode.Id.Reference);
        }

        [Fact]
        public void TwoNodesSharingABackingObjectAreOneControlToTheCursor()
        {
            // The consequence of following the reference BEFORE the structural key, pinned here
            // because it is the trap adapters keep walking into: two surfaces that show the same
            // entity and both carry it as a reference are indistinguishable to reconciliation, so the
            // cursor lands on whichever one comes first and the player is teleported off the surface
            // they were reading. ES2 Access hit it twice - a research-queue row against its wheel
            // node, and the two ends of one starlane, each declared under its own system. Where two
            // nodes show one entity, at most one of them may carry the reference.
            GraphState state = new GraphState();
            object thing = new object();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(ControlId.Referenced(thing, "here"), Vt("Here"));
                b.AddItem(ControlId.Referenced(thing, "there"), Vt("There"));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.Equal("there", Focused(g));

            g.Rerender();
            Assert.Equal("here", Focused(g));
        }

        [Fact]
        public void AVanishedControlFallsBackToTheNearestSurvivor()
        {
            GraphState state = new GraphState();
            bool withC = true;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(Id("a"), Vt("A"));
                b.AddItem(Id("b"), Vt("B"));
                if (withC) b.AddItem(Id("c"), Vt("C"));
                b.AddItem(Id("d"), Vt("D"));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            g.Move(GraphDir.Down);
            Assert.Equal("c", Focused(g));

            withC = false;
            g.Rerender();
            Assert.Equal("b", Focused(g)); // the survivor before it in the previous order
        }

        [Fact]
        public void AnUnrecognizableRebuildFallsBackToTheStartNode()
        {
            GraphState state = new GraphState();
            bool second = false;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                if (second) b.AddItem(Id("x"), Vt("X")).AddItem(Id("y"), Vt("Y"));
                else b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B"));
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.Equal("b", Focused(g));

            second = true;
            g.Rerender();
            Assert.Equal("x", Focused(g));
        }

        [Fact]
        public void ASuggestedMoveIsHonoredOnceAndThenConsumed()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B"))), state);
            g.Rerender();
            state.NextSuggestedMove = Id("b");
            g.Rerender();
            Assert.Equal("b", Focused(g));
            Assert.Null(state.NextSuggestedMove);
            g.Rerender();
            Assert.Equal("b", Focused(g));
        }

        [Fact]
        public void RerenderReportsFalseWhenTheScreenProducesNothing()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() => null, state);
            Assert.False(g.Rerender());
            Assert.Null(g.Current);
            Assert.Null(g.CurrentNode);
        }

        [Fact]
        public void ComputeOrderVisitsAGridInReadingOrder()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow("g").AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).EndRow();
            b.StartRow("g").AddItem(Id("c"), Vt("C")).AddItem(Id("d"), Vt("D")).EndRow();
            GraphRender r = b.Build();
            List<ControlId> order = KeyGraph.ComputeOrder(r);
            Assert.Equal(new[] { "a", "b", "c", "d" }, order.ConvertAll(id => (string)id.StructuralKey));
        }

        [Fact]
        public void ComputeOrderAppendsNodesTheWalkCannotReach()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("s1").AddItem(Id("a"), Vt("A"));
            b.BeginStop("s2").AddItem(Id("z"), Vt("Z"));
            List<ControlId> order = KeyGraph.ComputeOrder(b.Build());
            Assert.Equal(new[] { "a", "z" }, order.ConvertAll(id => (string)id.StructuralKey));
        }

        // ---- behaviors ----

        [Fact]
        public void ActivateSecondaryAndTooltipReportWhetherTheControlHasThem()
        {
            GraphState state = new GraphState();
            int activated = 0, secondary = 0, tooltip = 0;
            NodeVtable rich = Vt("Rich");
            rich.OnActivate = () => activated++;
            rich.OnSecondary = () => secondary++;
            rich.OnTooltip = () => tooltip++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), rich).AddItem(Id("b"), Vt("Plain"))), state);
            g.Rerender();
            Assert.True(g.Activate());
            Assert.True(g.Secondary());
            Assert.True(g.Tooltip());
            Assert.Equal(1, activated);
            Assert.Equal(1, secondary);
            Assert.Equal(1, tooltip);

            g.Move(GraphDir.Down);
            Assert.False(g.Activate());
            Assert.False(g.Secondary());
            Assert.False(g.Tooltip());
        }

        /// <summary>
        /// The right click is a slot of its own and nothing else: a control without one answers false
        /// so the caller stays silent, and never borrows the plain click. A right click that does not
        /// exist has nothing to replay - unlike the modified LEFT clicks below.
        /// </summary>
        [Fact]
        public void TheContextualCommandRunsItsOwnSlotAndNothingWhereThereIsNone()
        {
            GraphState state = new GraphState();
            int activated = 0, contextual = 0;
            NodeVtable row = Vt("Ship");
            row.OnActivate = () => activated++;
            row.OnContextual = () => contextual++;
            NodeVtable plain = Vt("Plain");
            plain.OnActivate = () => activated++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), row).AddItem(Id("b"), plain)), state);
            g.Rerender();
            Assert.True(g.Contextual());
            Assert.Equal(0, activated);
            Assert.Equal(1, contextual);

            g.Move(GraphDir.Down);
            Assert.False(g.Contextual());
            Assert.Equal(0, activated);
        }

        /// <summary>
        /// The three modified LEFT clicks - Alt+click and the two selection chords. Where the control
        /// wires the slot, the slot runs and the plain click does not. Where it does not, the plain
        /// click is replayed instead: the player is physically holding the modifier, so the game's own
        /// handler is what branches on it (Ctrl+click to locate a technology), and that must work
        /// without every screen wiring a slot for behavior that is entirely the game's.
        /// </summary>
        [Fact]
        public void TheModifiedClicksRunTheirOwnSlotAndOtherwiseReplayThePlainClick()
        {
            GraphState state = new GraphState();
            int activated = 0, alternate = 0, toggled = 0, ranged = 0;
            NodeVtable row = Vt("Ship");
            row.OnActivate = () => activated++;
            row.OnAlternate = () => alternate++;
            row.OnSelectToggle = () => toggled++;
            row.OnSelectRange = () => ranged++;
            NodeVtable button = Vt("Behemoth");
            button.OnActivate = () => activated++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), row).AddItem(Id("b"), button).AddItem(Id("c"), Vt("Label"))), state);
            g.Rerender();
            Assert.True(g.Alternate());
            Assert.True(g.SelectToggle());
            Assert.True(g.SelectRange());
            Assert.Equal(0, activated);
            Assert.Equal(1, alternate);
            Assert.Equal(1, toggled);
            Assert.Equal(1, ranged);

            // No slot, but a click: each chord replays the click, once.
            g.Move(GraphDir.Down);
            Assert.True(g.Alternate());
            Assert.True(g.SelectToggle());
            Assert.True(g.SelectRange());
            Assert.Equal(3, activated);
            Assert.Equal(1, alternate);
            Assert.Equal(1, toggled);
            Assert.Equal(1, ranged);

            // Neither: nothing ran, and false is how the caller knows to stay silent.
            g.Move(GraphDir.Down);
            Assert.False(g.Alternate());
            Assert.False(g.SelectToggle());
            Assert.False(g.SelectRange());
            Assert.Equal(3, activated);
        }

        /// <summary>
        /// The double click is a slot of its own: a control that has one runs it and NOT its single
        /// click, and a control that has none answers false so the caller stays silent instead of
        /// clicking. The two are different commands wherever the game bothers to wire both - a module
        /// tile answers a single click with nothing at all and its double click fits the module - so a
        /// fall back to activation would do something the player never asked for.
        /// </summary>
        [Fact]
        public void TheDoubleClickRunsItsOwnSlotAndNothingWhereThereIsNone()
        {
            GraphState state = new GraphState();
            int activated = 0, doubled = 0, contextual = 0;
            NodeVtable tile = Vt("Kinetic module");
            tile.OnActivate = () => activated++;
            tile.OnDoubleClick = () => doubled++;
            NodeVtable other = Vt("Fleet");
            other.OnActivate = () => activated++;
            other.OnContextual = () => contextual++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), tile).AddItem(Id("b"), other)), state);
            g.Rerender();
            Assert.True(g.DoubleClick());
            Assert.Equal(1, doubled);
            Assert.Equal(0, activated);

            // A control with every other behavior and no double click: false, and nothing ran.
            g.Move(GraphDir.Down);
            Assert.False(g.DoubleClick());
            Assert.Equal(1, doubled);
            Assert.Equal(0, activated);
            Assert.Equal(0, contextual);
        }

        [Fact]
        public void TryAdjustPreemptsHorizontalNavigation()
        {
            GraphState state = new GraphState();
            List<string> adjustments = new List<string>();
            NodeVtable slider = Vt("Volume");
            slider.OnAdjust = (sign, large) => adjustments.Add(sign + (large ? " large" : " small"));

            KeyGraph g = new KeyGraph(Renderer(b =>
            {
                b.StartRow().AddItem(Id("s"), slider).AddItem(Id("n"), Vt("Next")).EndRow();
            }), state);
            g.Rerender();

            Assert.True(g.TryAdjust(1, false));
            Assert.True(g.TryAdjust(-1, true));
            Assert.Equal(new[] { "1 small", "-1 large" }, adjustments);
            Assert.Equal("s", Focused(g)); // adjusting never moves focus

            // The caller navigates only when the control declines to adjust.
            g.Move(GraphDir.Right);
            Assert.False(g.TryAdjust(1, false));
        }

        [Fact]
        public void FocusByReferenceSyncsFromTheGameSide()
        {
            GraphState state = new GraphState();
            object thing = new object();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                b.AddItem(Id("a"), Vt("A"));
                b.AddItem(ControlId.Referenced(thing, "b"), Vt("B"));
                return b.Build();
            }, state);
            g.Rerender();
            Assert.True(g.FocusByReference(thing));
            Assert.Equal("b", Focused(g));
            Assert.False(g.FocusByReference(thing)); // already there: no change
            Assert.False(g.FocusByReference(new object()));
        }
    }
}
