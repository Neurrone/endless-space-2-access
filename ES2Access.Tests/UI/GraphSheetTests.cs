using System;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Tests.Speech;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The sheet emitter: a table whose framing rules are ordinary graph edges. Shares the ModStrings
    /// collection because the readouts go through the announcer.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class GraphSheetTests : IDisposable
    {
        private readonly object _rowA = new object();
        private readonly object _rowB = new object();

        public GraphSheetTests()
        {
            ModStrings.Reset();
            GraphAnnouncer.Reset();
            GraphSheet.Reset();
            GraphSheet.TableRoleText = () => "table";
            GraphSheet.BlankText = () => "blank";
        }

        public void Dispose()
        {
            GraphAnnouncer.Reset();
            GraphSheet.Reset();
        }

        private KeyGraph Table(GraphState state, bool raggedSecondRow = false)
        {
            return new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { "Ships", "Move" });
                s.Row(Vt("Alpha"), _rowA, () => "3", () => "5");
                if (raggedSecondRow) s.Row(Vt("Beta"), _rowB);
                else s.Row(Vt("Beta"), _rowB, () => "2", () => "4");
                s.Finish();
                return b.Build();
            }, state);
        }

        [Fact]
        public void FocusStartsOnTheFirstRowsPrimaryCellAndReadsTheRegionOnce()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            Assert.Equal("Fleets, table, Alpha", GraphAnnouncer.ComposeFull(g.CurrentNode));
        }

        [Fact]
        public void CrossingIntoAColumnSpeaksItsHeader()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();

            MoveResult right = g.Move(GraphDir.Right);
            Assert.True(right.Moved);
            Assert.Equal("Ships", right.TransitionLabel);
            Assert.Equal("3", GraphAnnouncer.LeafText(right.To));

            MoveResult right2 = g.Move(GraphDir.Right);
            Assert.Equal("Move", right2.TransitionLabel);
            Assert.Equal("5", GraphAnnouncer.LeafText(right2.To));

            MoveResult back = g.Move(GraphDir.Left);
            Assert.Equal("Ships", back.TransitionLabel);
        }

        [Fact]
        public void ReturningToThePrimaryCellCrossesAnUnlabeledEdge()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            g.Move(GraphDir.Right);
            MoveResult back = g.Move(GraphDir.Left);
            Assert.True(back.Moved);
            Assert.Null(back.TransitionLabel);
            Assert.Equal("Alpha", GraphAnnouncer.LeafText(back.To));
        }

        [Fact]
        public void VerticalNavigationPreservesTheColumnAndNamesTheRow()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            g.Move(GraphDir.Right); // Alpha / Ships

            MoveResult down = g.Move(GraphDir.Down);
            Assert.True(down.Moved);
            Assert.Equal("Beta", down.TransitionLabel);
            Assert.Equal("2", GraphAnnouncer.LeafText(down.To)); // still the Ships column

            MoveResult up = g.Move(GraphDir.Up);
            Assert.Equal("Alpha", up.TransitionLabel);
            Assert.Equal("3", GraphAnnouncer.LeafText(up.To));
        }

        [Fact]
        public void MovingDownThePrimaryColumnIsUnlabeled()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            MoveResult down = g.Move(GraphDir.Down);
            Assert.True(down.Moved);
            Assert.Null(down.TransitionLabel);
            Assert.Equal("Beta", GraphAnnouncer.LeafText(down.To));
        }

        [Fact]
        public void ARaggedRowFallsBackToItsPrimaryCell()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state, true);
            g.Rerender();
            g.Move(GraphDir.Right); // Alpha / Ships

            MoveResult down = g.Move(GraphDir.Down);
            Assert.True(down.Moved);
            Assert.Null(down.TransitionLabel); // landing on a primary is never row-labeled
            Assert.Equal("Beta", GraphAnnouncer.LeafText(down.To));
        }

        [Fact]
        public void AnEmptyCellReadsBlank()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { "Ships" });
                s.Row(Vt("Alpha"), _rowA, () => "   ");
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.Equal("blank", GraphAnnouncer.LeafText(g.Move(GraphDir.Right).To));
        }

        [Fact]
        public void SheetCellsAreRawNodesSoTheyCarryNoAutoPosition()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            foreach (GraphNode n in g.Current.Order) Assert.Equal(0, n.PositionCount);
        }

        [Fact]
        public void RowsAreTaggedWithTheirRegionAndStayInOneStop()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets");
                s.Row(Vt("Alpha"), _rowA);
                s.Region("Systems");
                s.Row(Vt("Sol"), _rowB);
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.Equal("t:reg:0", g.CurrentNode.RegionKey);

            MoveResult jump = g.MoveRegion(1);
            Assert.True(jump.Moved);
            Assert.Equal("t:reg:1", jump.To.RegionKey);
            Assert.Equal("Systems, Sol", GraphAnnouncer.Compose(g.Current.Order[0], jump.To));
        }

        [Fact]
        public void ThePrimaryCellCarriesTheRowObjectSoFocusFollowsAReorder()
        {
            GraphState state = new GraphState();
            bool swapped = false;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets");
                if (swapped) { s.Row(Vt("Beta"), _rowB); s.Row(Vt("Alpha"), _rowA); }
                else { s.Row(Vt("Alpha"), _rowA); s.Row(Vt("Beta"), _rowB); }
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Down);
            Assert.Equal("Beta", GraphAnnouncer.LeafText(g.CurrentNode));

            swapped = true;
            g.Rerender();
            Assert.Equal("Beta", GraphAnnouncer.LeafText(g.CurrentNode));
            Assert.Same(_rowB, g.CurrentNode.Id.Reference);
        }
    }
}
