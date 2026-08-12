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
        /// The three keys that are not activation: the right-click command and the two selection
        /// chords. Each runs its own slot and nothing else - a chord must never fall back to plain
        /// activation on a control that does not have it, which would do something the player never
        /// asked for. False is how the caller knows to say "nothing to do here" instead.
        /// </summary>
        [Fact]
        public void TheContextualCommandAndTheSelectionChordsEachRunTheirOwnSlot()
        {
            GraphState state = new GraphState();
            int activated = 0, contextual = 0, toggled = 0, ranged = 0;
            NodeVtable row = Vt("Ship");
            row.OnActivate = () => activated++;
            row.OnContextual = () => contextual++;
            row.OnSelectToggle = () => toggled++;
            row.OnSelectRange = () => ranged++;

            KeyGraph g = new KeyGraph(Renderer(b =>
                b.AddItem(Id("a"), row).AddItem(Id("b"), Vt("Plain"))), state);
            g.Rerender();
            Assert.True(g.Contextual());
            Assert.True(g.SelectToggle());
            Assert.True(g.SelectRange());
            Assert.Equal(0, activated);
            Assert.Equal(1, contextual);
            Assert.Equal(1, toggled);
            Assert.Equal(1, ranged);

            g.Move(GraphDir.Down);
            Assert.False(g.Contextual());
            Assert.False(g.SelectToggle());
            Assert.False(g.SelectRange());
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
