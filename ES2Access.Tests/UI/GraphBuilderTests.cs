using System;
using ES2Access.Core.UI.Graph;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The builder's wiring contract: what arrows do in menu mode, where Tab-stops cut the graph, and
    /// which nodes get an auto "n of m".
    /// </summary>
    public class GraphBuilderTests
    {
        private static GraphRender TwoByTwo(object rowKey)
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow(rowKey).AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).EndRow();
            b.StartRow(rowKey).AddItem(Id("c"), Vt("C")).AddItem(Id("d"), Vt("D")).EndRow();
            return b.Build();
        }

        [Fact]
        public void RowMembersAreWiredLeftAndRight()
        {
            GraphRender r = TwoByTwo(null);
            Assert.Equal("b", DestKey(Node(r, "a"), GraphDir.Right));
            Assert.Equal("a", DestKey(Node(r, "b"), GraphDir.Left));
            Assert.Null(DestKey(Node(r, "a"), GraphDir.Left));
            Assert.Null(DestKey(Node(r, "b"), GraphDir.Right));
        }

        [Fact]
        public void ConsecutiveRowsAreWiredUpAndDown()
        {
            GraphRender r = TwoByTwo(null);
            Assert.Equal("c", DestKey(Node(r, "a"), GraphDir.Down));
            Assert.Equal("a", DestKey(Node(r, "c"), GraphDir.Up));
        }

        [Fact]
        public void RowsWithoutAKeyLandVerticalNavigationOnTheFirstItem()
        {
            GraphRender r = TwoByTwo(null);
            Assert.Equal("c", DestKey(Node(r, "b"), GraphDir.Down));
            Assert.Equal("a", DestKey(Node(r, "d"), GraphDir.Up));
        }

        [Fact]
        public void RowsSharingARowKeyPreserveTheColumn()
        {
            GraphRender r = TwoByTwo("grid");
            Assert.Equal("d", DestKey(Node(r, "b"), GraphDir.Down));
            Assert.Equal("b", DestKey(Node(r, "d"), GraphDir.Up));
        }

        [Fact]
        public void ColumnIsPreservedOnlyBetweenRowsSharingTheSameKey()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow("one").AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).EndRow();
            b.StartRow("two").AddItem(Id("c"), Vt("C")).AddItem(Id("d"), Vt("D")).EndRow();
            GraphRender r = b.Build();
            Assert.Equal("c", DestKey(Node(r, "b"), GraphDir.Down));
        }

        [Fact]
        public void ColumnNavigationFallsBackWhenTheAdjacentRowIsShorter()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow("grid").AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).EndRow();
            b.StartRow("grid").AddItem(Id("c"), Vt("C")).EndRow();
            GraphRender r = b.Build();
            Assert.Equal("c", DestKey(Node(r, "b"), GraphDir.Down));
        }

        [Fact]
        public void ItemsOutsideARowFormAPlainVerticalMenu()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).AddItem(Id("c"), Vt("C"));
            GraphRender r = b.Build();
            Assert.Equal("b", DestKey(Node(r, "a"), GraphDir.Down));
            Assert.Equal("b", DestKey(Node(r, "c"), GraphDir.Up));
            Assert.Null(DestKey(Node(r, "a"), GraphDir.Right));
        }

        [Fact]
        public void ArrowsNeverCrossATabStop()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("s1").AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B"));
            b.BeginStop("s2").AddItem(Id("c"), Vt("C")).AddItem(Id("d"), Vt("D"));
            GraphRender r = b.Build();
            Assert.Null(DestKey(Node(r, "b"), GraphDir.Down));
            Assert.Null(DestKey(Node(r, "c"), GraphDir.Up));
            Assert.Equal("s1", Node(r, "b").StopKey);
            Assert.Equal("s2", Node(r, "c").StopKey);
        }

        [Fact]
        public void RegionsAreTaggedPerStopAndClearedByBeginStop()
        {
            GraphBuilder b = new GraphBuilder();
            b.SetRegion("r1").AddItem(Id("a"), Vt("A"));
            b.SetRegion("r2").AddItem(Id("b"), Vt("B"));
            b.BeginStop("s2").AddItem(Id("c"), Vt("C"));
            GraphRender r = b.Build();
            Assert.Equal("r1", Node(r, "a").RegionKey);
            Assert.Equal("r2", Node(r, "b").RegionKey);
            Assert.Null(Node(r, "c").RegionKey);
        }

        [Fact]
        public void SinglesInTheSameStopArePositionedAmongTheirSiblings()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).AddItem(Id("c"), Vt("C"));
            GraphRender r = b.Build();
            Assert.Equal(1, Node(r, "a").PositionIndex);
            Assert.Equal(3, Node(r, "a").PositionCount);
            Assert.Equal(2, Node(r, "b").PositionIndex);
            Assert.Equal(3, Node(r, "c").PositionCount);
        }

        [Fact]
        public void MultiItemRowMembersArePositionedWithinTheirRowNotTheStop()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).EndRow();
            b.AddItem(Id("c"), Vt("C"));
            b.AddItem(Id("d"), Vt("D"));
            GraphRender r = b.Build();
            Assert.Equal(2, Node(r, "a").PositionCount); // among its row
            Assert.Equal(2, Node(r, "b").PositionIndex);
            Assert.Equal(2, Node(r, "c").PositionCount); // among the single-item rows
            Assert.Equal(2, Node(r, "d").PositionIndex);
        }

        [Fact]
        public void ALoneSiblingGetsNoPosition()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(Id("a"), Vt("A"));
            GraphRender r = b.Build();
            Assert.Equal(0, Node(r, "a").PositionCount);
        }

        [Fact]
        public void PositionsAreCountedPerParentContext()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Outer");
            b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B"));
            b.PopContext();
            b.AddItem(Id("c"), Vt("C"));
            GraphRender r = b.Build();
            Assert.Equal(2, Node(r, "a").PositionCount);
            Assert.Equal(0, Node(r, "c").PositionCount); // its own (parent, stop) group has one member
        }

        [Fact]
        public void AnOnlyChildOfAnExpandableGroupStillGetsItsPosition()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(Id("before"), Vt("Before"));
            b.BeginGroup(Id("g"), Vt("G"), true);
            b.AddItem(Id("only"), Vt("Only"));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.Equal(1, Node(r, "only").PositionIndex);
            Assert.Equal(1, Node(r, "only").PositionCount);
        }

        [Fact]
        public void AnOnlyChildOfAPlainContextStillGetsNoPosition()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Outer");
            b.AddItem(Id("only"), Vt("Only"));
            b.PopContext();
            Assert.Equal(0, Node(b.Build(), "only").PositionCount);
        }

        [Fact]
        public void SuppressChildPositionsSilencesAContextsChildren()
        {
            GraphBuilder b = new GraphBuilder();
            b.PushContext("Log", null, false);
            b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).AddItem(Id("c"), Vt("C"));
            b.PopContext();
            GraphRender r = b.Build();
            Assert.Equal(0, Node(r, "a").PositionCount);
            Assert.Equal(0, Node(r, "c").PositionCount);
        }

        [Fact]
        public void RawNodesGetNoAutomaticWiringAndNoPositions()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(Id("a"), Vt("A")).AddNode(Id("b"), Vt("B"));
            b.Connect(Id("a"), GraphDir.Right, Id("b"), "over there");
            GraphRender r = b.Build();
            Assert.Equal("b", DestKey(Node(r, "a"), GraphDir.Right));
            Assert.Null(DestKey(Node(r, "b"), GraphDir.Left));
            Assert.Equal(0, Node(r, "a").PositionCount);
        }

        [Fact]
        public void EdgesToUndeclaredNodesAreDropped()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(Id("a"), Vt("A"));
            b.Connect(Id("a"), GraphDir.Right, Id("ghost"));
            GraphRender r = b.Build();
            Assert.Null(DestKey(Node(r, "a"), GraphDir.Right));
        }

        [Fact]
        public void MenuRowsAboveRawContentAreStitchedTogether()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(Id("f1"), Vt("Filter")).AddItem(Id("f2"), Vt("Sort")).EndRow();
            b.AddNode(Id("cell"), Vt("Cell"));
            GraphRender r = b.Build();
            Assert.Equal("cell", DestKey(Node(r, "f1"), GraphDir.Down));
            Assert.Equal("cell", DestKey(Node(r, "f2"), GraphDir.Down));
            Assert.Equal("f1", DestKey(Node(r, "cell"), GraphDir.Up));
        }

        /// <summary>A menu row, then a raw ROW of three cells wired only to each other — the shape a
        /// sheet's top row has under a strip of buttons.</summary>
        private static GraphRender StripOverRawRow()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(Id("f1"), Vt("Filter")).AddItem(Id("f2"), Vt("Sort")).EndRow();
            b.AddNode(Id("c0"), Vt("Alpha")).AddNode(Id("c1"), Vt("3")).AddNode(Id("c2"), Vt("5"));
            b.Connect(Id("c0"), GraphDir.Right, Id("c1"));
            b.Connect(Id("c1"), GraphDir.Left, Id("c0"));
            b.Connect(Id("c1"), GraphDir.Right, Id("c2"));
            b.Connect(Id("c2"), GraphDir.Left, Id("c1"));
            return b.Build();
        }

        [Fact]
        public void EveryCellOfARawTopRowReachesTheMenuRowAbove()
        {
            GraphRender r = StripOverRawRow();
            Assert.Equal("f1", DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.Equal("f1", DestKey(Node(r, "c1"), GraphDir.Up));
            Assert.Equal("f1", DestKey(Node(r, "c2"), GraphDir.Up));
            Assert.Equal("c0", DestKey(Node(r, "f1"), GraphDir.Down));
            Assert.Equal("c0", DestKey(Node(r, "f2"), GraphDir.Down));
        }

        [Fact]
        public void TheRawTopRunEndsAtTheFirstNodeThatAlreadyHasAnUp()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(Id("f1"), Vt("Filter")).EndRow();
            b.AddNode(Id("c0"), Vt("Alpha")).AddNode(Id("c1"), Vt("3"));
            b.AddNode(Id("d0"), Vt("Beta")).AddNode(Id("d1"), Vt("2"));
            b.Connect(Id("d0"), GraphDir.Up, Id("c0")); // a second table row wires itself
            GraphRender r = b.Build();
            Assert.Equal("f1", DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.Equal("f1", DestKey(Node(r, "c1"), GraphDir.Up));
            Assert.Equal("c0", DestKey(Node(r, "d0"), GraphDir.Up));
            Assert.Null(DestKey(Node(r, "d1"), GraphDir.Up));
        }

        [Fact]
        public void EveryCellOfARawBottomRowReachesTheMenuRowBelow()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(Id("c0"), Vt("Alpha")).AddNode(Id("c1"), Vt("3")).AddNode(Id("c2"), Vt("5"));
            b.StartRow().AddItem(Id("ok"), Vt("OK")).AddItem(Id("no"), Vt("Cancel")).EndRow();
            GraphRender r = b.Build();
            Assert.Equal("ok", DestKey(Node(r, "c0"), GraphDir.Down));
            Assert.Equal("ok", DestKey(Node(r, "c1"), GraphDir.Down));
            Assert.Equal("ok", DestKey(Node(r, "c2"), GraphDir.Down));

            // Back into the block lands on the run's FIRST node - a table row's primary cell, which
            // reads the whole row - not on whichever column happened to be declared last.
            Assert.Equal("c0", DestKey(Node(r, "ok"), GraphDir.Up));
            Assert.Equal("c0", DestKey(Node(r, "no"), GraphDir.Up));
        }

        [Fact]
        public void TheRawBottomRunEndsAtTheFirstNodeThatAlreadyHasADown()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(Id("c0"), Vt("Alpha")).AddNode(Id("c1"), Vt("3"));
            b.AddNode(Id("d0"), Vt("Beta")).AddNode(Id("d1"), Vt("2"));
            b.Connect(Id("c0"), GraphDir.Down, Id("d0")); // the row above wires itself
            b.StartRow().AddItem(Id("ok"), Vt("OK")).EndRow();
            GraphRender r = b.Build();
            Assert.Equal("d0", DestKey(Node(r, "c0"), GraphDir.Down));
            Assert.Equal("ok", DestKey(Node(r, "c1"), GraphDir.Down));
            Assert.Equal("ok", DestKey(Node(r, "d0"), GraphDir.Down));
            Assert.Equal("ok", DestKey(Node(r, "d1"), GraphDir.Down));
            Assert.Equal("c1", DestKey(Node(r, "ok"), GraphDir.Up));
        }

        [Fact]
        public void ASingleRawNodeBetweenTwoMenuRowsIsStitchedBothWays()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(Id("f1"), Vt("Filter")).EndRow();
            b.AddNode(Id("prose"), Vt("What happened"));
            b.StartRow().AddItem(Id("ok"), Vt("OK")).AddItem(Id("no"), Vt("Cancel")).EndRow();
            GraphRender r = b.Build();
            Assert.Equal("prose", DestKey(Node(r, "f1"), GraphDir.Down));
            Assert.Equal("f1", DestKey(Node(r, "prose"), GraphDir.Up));
            Assert.Equal("ok", DestKey(Node(r, "prose"), GraphDir.Down));
            Assert.Equal("prose", DestKey(Node(r, "ok"), GraphDir.Up));
            Assert.Equal("prose", DestKey(Node(r, "no"), GraphDir.Up));
        }

        [Fact]
        public void AStopOfMenuRowsOnlyIsLeftToItsOwnWiring()
        {
            GraphRender r = TwoByTwo(null);
            Assert.Equal("c", DestKey(Node(r, "a"), GraphDir.Down));
            Assert.Equal("a", DestKey(Node(r, "c"), GraphDir.Up));
            Assert.Null(DestKey(Node(r, "a"), GraphDir.Up));
            Assert.Null(DestKey(Node(r, "d"), GraphDir.Down));
        }

        [Fact]
        public void AStopOfRawNodesOnlyIsNeverStitched()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddNode(Id("c0"), Vt("Alpha")).AddNode(Id("c1"), Vt("Beta"));
            GraphRender r = b.Build();
            Assert.Null(DestKey(Node(r, "c0"), GraphDir.Up));
            Assert.Null(DestKey(Node(r, "c0"), GraphDir.Down));
            Assert.Null(DestKey(Node(r, "c1"), GraphDir.Up));
            Assert.Null(DestKey(Node(r, "c1"), GraphDir.Down));
        }

        [Fact]
        public void BuildReturnsNullWhenNothingWasDeclared()
        {
            Assert.Null(new GraphBuilder().Build());
        }

        [Fact]
        public void DuplicateControlIdsAreRejected()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(Id("a"), Vt("A"));
            Assert.Throws<InvalidOperationException>(() => b.AddItem(Id("a"), Vt("Again")));
        }

        [Fact]
        public void AControlWithoutAnnouncementsIsRejected()
        {
            GraphBuilder b = new GraphBuilder();
            Assert.Throws<ArgumentException>(() => b.AddItem(Id("a"), new NodeVtable()));
        }

        [Fact]
        public void AnUnclosedRowIsRejectedAtBuild()
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(Id("a"), Vt("A"));
            Assert.Throws<InvalidOperationException>(() => b.Build());
        }

        [Fact]
        public void SetStartOverridesTheDefaultStartNode()
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B"));
            b.SetStart(Id("b"));
            Assert.Equal("b", b.Build().StartKey.StructuralKey);
        }

        [Fact]
        public void CollapsedGroupChildrenAreNotDeclared()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(Id("g"), Vt("Group"));
            b.AddItem(Id("child"), Vt("Child"));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.NotNull(Node(r, "g"));
            Assert.Null(Node(r, "child"));
            Assert.True(Node(r, "g").Expandable);
            Assert.False(Node(r, "g").Expanded);
        }

        [Fact]
        public void ACollapsedAncestorSuppressesTheWholeSubtree()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(Id("outer"), Vt("Outer"));
            b.BeginGroup(Id("inner"), Vt("Inner"), true);
            b.AddItem(Id("leaf"), Vt("Leaf"));
            b.EndGroup();
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.Null(Node(r, "inner"));
            Assert.Null(Node(r, "leaf"));
        }

        [Fact]
        public void ExpandedGroupChildrenHangOffTheHeader()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginGroup(Id("g"), Vt("Group"), true);
            b.AddItem(Id("child"), Vt("Child"));
            b.EndGroup();
            GraphRender r = b.Build();
            Assert.Same(Node(r, "g"), Node(r, "child").Parent);
            Assert.Equal("child", DestKey(Node(r, "g"), GraphDir.Down));
        }
    }
}
