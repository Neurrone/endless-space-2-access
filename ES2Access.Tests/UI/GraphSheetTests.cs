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
                s.Region("Fleets", new[] { "Name", "Ships", "Move" });
                s.Row(Vt("Alpha"), _rowA, null, () => "3", () => "5");
                if (raggedSecondRow) s.Row(Vt("Beta"), _rowB, null);
                else s.Row(Vt("Beta"), _rowB, null, () => "2", () => "4");
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

        /// <summary>The primary is a column like any other: crossing back into it says its caption, so a
        /// player walking a row hears the same shape of line in both directions.</summary>
        [Fact]
        public void ReturningToThePrimaryCellSpeaksItsColumnCaption()
        {
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            g.Move(GraphDir.Right);
            MoveResult back = g.Move(GraphDir.Left);
            Assert.True(back.Moved);
            Assert.Equal("Name", back.TransitionLabel);
            Assert.Equal("Alpha", GraphAnnouncer.LeafText(back.To));
        }

        /// <summary>A table whose primary column the game drew no caption over: the entry is null and
        /// the crossing stays label-free rather than inventing a word for it.</summary>
        [Fact]
        public void APrimaryWithNoCaptionCrossesUnlabeled()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { null, "Ships" });
                s.Row(Vt("Alpha"), _rowA, null, () => "3");
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();

            Assert.Equal("Ships", g.Move(GraphDir.Right).TransitionLabel);
            MoveResult back = g.Move(GraphDir.Left);
            Assert.True(back.Moved);
            Assert.Null(back.TransitionLabel);
            Assert.Equal("Alpha", GraphAnnouncer.LeafText(back.To));
        }

        /// <summary>A plain list region has no captions at all, and gains none: neither direction is
        /// labeled and the region is not called a table.</summary>
        [Fact]
        public void APlainListRegionLabelsNothingAndIsNoTable()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets");
                s.Row(Vt("Alpha"), _rowA, null, () => "3");
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.Equal("Fleets, Alpha", GraphAnnouncer.ComposeFull(g.CurrentNode));

            Assert.Null(g.Move(GraphDir.Right).TransitionLabel);
            Assert.Null(g.Move(GraphDir.Left).TransitionLabel);
        }

        /// <summary>One captioned column and nothing beside it is the list it looks like: the header
        /// array counts the primary, so a lone entry does not make a region a table.</summary>
        [Fact]
        public void APrimaryCaptionAloneDoesNotMakeARegionATable()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { "Name" });
                s.Row(Vt("Alpha"), _rowA, null);
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            Assert.Equal("Fleets, Alpha", GraphAnnouncer.ComposeFull(g.CurrentNode));
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

        /// <summary>A grid whose rows are only the lines the game wrapped one lattice onto: column 0 is
        /// a cell like any other, so a vertical crossing names no row - saying one would announce a
        /// NEIGHBOURING cell's words in front of the cell landed on.</summary>
        [Fact]
        public void UnnamedRowsLabelNoVerticalCrossing()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.NamedRows = false;
                s.Region("Luxuries", new[] { "Food", "Industry" });
                s.Row(Vt("Alpha"), null, null, () => "3");
                s.Row(Vt("Beta"), null, null, () => "2");
                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();
            g.Move(GraphDir.Right);

            MoveResult down = g.Move(GraphDir.Down);
            Assert.True(down.Moved);
            Assert.Null(down.TransitionLabel);
            Assert.Equal("2", GraphAnnouncer.LeafText(down.To));
            Assert.Null(g.Move(GraphDir.Up).TransitionLabel);
        }

        /// <summary>And every cell of such a grid is its own search result: the one-result-per-row filter
        /// exists because a named row's cells all search as that row, which here would make seven columns
        /// of eight unreachable by typing.</summary>
        [Fact]
        public void UnnamedRowsMakeEveryCellSearchable()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("lux");
            GraphSheet s = new GraphSheet(b, "t:");
            s.NamedRows = false;
            s.Region("Luxuries", new[] { "Food", "Industry" });
            s.Row(Vt("Alpha"), null, null, () => "Transvine");
            s.Finish();
            GraphRender render = b.Build();

            SearchScope scope = SearchScope.OverStop(render, "lux");
            Assert.Equal(2, scope.Count);
            Assert.Equal("Transvine", scope.TextOf(1));
        }

        /// <summary>The primary included. A search made from a metadata column steps back into that
        /// column after landing, because a named row's cells all matched by the row's NAME and the
        /// player was reading a column; a cell that matched by its own words is already the thing asked
        /// for, so the stamp has to be on column 0 too or the landing walks one cell past it.</summary>
        [Fact]
        public void UnnamedRowsMatchByTheirOwnWordsInEveryColumnIncludingTheFirst()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("lux");
            GraphSheet s = new GraphSheet(b, "t:");
            s.NamedRows = false;
            s.Region("Luxuries", new[] { "Food", "Industry" });
            s.Row(Vt("Transvine"), null, null, () => "3");
            s.Finish();
            foreach (GraphNode node in b.Build().Order)
                Assert.True(node.Vtable.SearchesAsItself, "column " + node.Vtable.Column);

            GraphBuilder named = new GraphBuilder();
            named.BeginStop("fleets");
            GraphSheet n = new GraphSheet(named, "t:");
            n.Region("Fleets", new[] { "Name", "Ships" });
            n.Row(Vt("Alpha"), _rowA, null, () => "3");
            n.Finish();
            foreach (GraphNode node in named.Build().Order)
                Assert.False(node.Vtable.SearchesAsItself);
        }

        /// <summary>The default is unchanged: a table whose rows are things offers one result per row.
        /// </summary>
        [Fact]
        public void NamedRowsStillOfferOneSearchResultPerRow()
        {
            GraphBuilder b = new GraphBuilder();
            b.BeginStop("fleets");
            GraphSheet s = new GraphSheet(b, "t:");
            s.Region("Fleets", new[] { "Name", "Ships" });
            s.Row(Vt("Alpha"), _rowA, null, () => "3");
            s.Finish();

            Assert.Equal(1, SearchScope.OverStop(b.Build(), "fleets").Count);
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
                s.Region("Fleets", new[] { "Name", "Ships" });
                s.Row(Vt("Alpha"), _rowA, null, () => "   ");
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
                s.Row(Vt("Alpha"), _rowA, null);
                s.Region("Systems");
                s.Row(Vt("Sol"), _rowB, null);
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

        /// <summary>A popup's shape: a strip of buttons, the paragraph it opened with, its table, and
        /// the buttons along the bottom - all in one Tab stop, so every seam is the builder's.</summary>
        private GraphRender ProseOverTable(bool prose)
        {
            GraphBuilder b = new GraphBuilder();
            b.StartRow().AddItem(Id("next"), Vt("Next")).AddItem(Id("prev"), Vt("Previous")).EndRow();
            if (prose) b.AddNode(Id("words"), Vt("Something happened."));

            GraphSheet s = new GraphSheet(b, "t:");
            s.Region("Report", new[] { "Name", "Ships", "Move" });
            if (prose) s.Follows(Id("words"));
            s.Row(Vt("Alpha"), _rowA, null, () => "3", () => "5");
            s.Row(Vt("Beta"), _rowB, null, () => "2", () => "4");
            s.Finish();

            b.StartRow().AddItem(Id("done"), Vt("Done")).EndRow();
            return b.Build();
        }

        [Fact]
        public void EveryCellOfTheTopRowReachesTheStripAboveIt()
        {
            GraphRender r = ProseOverTable(false);
            Assert.Equal("next", DestKey(Node(r, "t:row" + _rowA.GetHashCode() + "c0"), GraphDir.Up));
            Assert.Equal("next", DestKey(Node(r, "t:row" + _rowA.GetHashCode() + "c1"), GraphDir.Up));
            Assert.Equal("next", DestKey(Node(r, "t:row" + _rowA.GetHashCode() + "c2"), GraphDir.Up));
            Assert.Equal("t:row" + _rowA.GetHashCode() + "c0", DestKey(Node(r, "next"), GraphDir.Down));
        }

        [Fact]
        public void EveryCellOfTheBottomRowReachesTheStripBelowIt()
        {
            GraphRender r = ProseOverTable(false);
            Assert.Equal("done", DestKey(Node(r, "t:row" + _rowB.GetHashCode() + "c0"), GraphDir.Down));
            Assert.Equal("done", DestKey(Node(r, "t:row" + _rowB.GetHashCode() + "c1"), GraphDir.Down));
            Assert.Equal("done", DestKey(Node(r, "t:row" + _rowB.GetHashCode() + "c2"), GraphDir.Down));
            Assert.Equal("t:row" + _rowB.GetHashCode() + "c0", DestKey(Node(r, "done"), GraphDir.Up));
        }

        [Fact]
        public void ATableToldToFollowANodeMeetsItAsThoughItWereTheRowAbove()
        {
            GraphRender r = ProseOverTable(true);
            string a0 = "t:row" + _rowA.GetHashCode() + "c0";
            Assert.Equal("words", DestKey(Node(r, a0), GraphDir.Up));
            Assert.Equal("words", DestKey(Node(r, "t:row" + _rowA.GetHashCode() + "c1"), GraphDir.Up));
            Assert.Equal("words", DestKey(Node(r, "t:row" + _rowA.GetHashCode() + "c2"), GraphDir.Up));
            Assert.Equal(a0, DestKey(Node(r, "words"), GraphDir.Down));

            // and the strip above stops at the words rather than reaching over them into the table
            Assert.Equal("words", DestKey(Node(r, "next"), GraphDir.Down));
            Assert.Equal("next", DestKey(Node(r, "words"), GraphDir.Up));
        }

        [Fact]
        public void CrossingUpOntoAFollowedNodeIsUnlabeled()
        {
            GraphState state = new GraphState();
            KeyGraph g = new KeyGraph(() => ProseOverTable(true), state);
            g.Rerender();
            g.Move(GraphDir.Down); // the strip -> the words
            g.Move(GraphDir.Down); // the words -> Alpha
            g.Move(GraphDir.Right); // Alpha / Ships
            MoveResult up = g.Move(GraphDir.Up);
            Assert.True(up.Moved);
            Assert.Null(up.TransitionLabel); // a followed node is nobody's row name
            Assert.Equal("Something happened.", GraphAnnouncer.LeafText(up.To));
        }

        [Fact]
        public void TheSheetNamesItsFirstRowSoAScreenNeverRebuildsAKey()
        {
            GraphBuilder b = new GraphBuilder();
            GraphSheet s = new GraphSheet(b, "t:");
            Assert.Null(s.FirstRow);
            s.Region("Report");
            s.Row(Vt("Alpha"), _rowA, null);
            s.Row(Vt("Beta"), _rowB, null);
            s.Finish();
            Assert.Equal("t:row" + _rowA.GetHashCode() + "c0", s.FirstRow.StructuralKey);
            Assert.Same(_rowA, s.FirstRow.Reference);

            b.SetStart(s.FirstRow);
            Assert.Equal(s.FirstRow.StructuralKey, b.Build().StartKey.StructuralKey);
        }

        private static string Say(MoveResult move)
        {
            return GraphAnnouncer.Compose(move.From, move.To, move.TransitionLabel);
        }

        [Fact]
        public void TheRowPositionIsSaidOnArrivalAndOnRowChangesOnly()
        {
            GraphAnnouncer.PositionText = (index, count) => index + " of " + count;
            GraphState state = new GraphState();
            KeyGraph g = Table(state);
            g.Rerender();
            Assert.Equal("Fleets, table, Alpha, 1 of 2", GraphAnnouncer.ComposeFull(g.CurrentNode));

            // Along the row: the row has not changed, so its position is not said again.
            Assert.Equal("Ships, 3", Say(g.Move(GraphDir.Right)));

            // A different row, reached off-primary: said.
            Assert.Equal("Beta, 2, 2 of 2", Say(g.Move(GraphDir.Down)));

            // Back onto column 0 of the row we are already in: the column is named, the position is
            // not - the row has not changed.
            Assert.Equal("Name, Beta", Say(g.Move(GraphDir.Left)));

            Assert.Equal("Alpha, 1 of 2", Say(g.Move(GraphDir.Up)));
        }

        [Fact]
        public void ACellSaysNoPositionOfItsOwn()
        {
            GraphBuilder b = new GraphBuilder();
            GraphSheet s = new GraphSheet(b, "t:");
            s.Region("Fleets", new[] { "Name", "Ships", "Move" });
            s.Row(Vt("Alpha"), _rowA, null, () => "3", () => "5");
            s.Row(Vt("Beta"), _rowB, null, () => "2", () => "4");
            s.Finish();
            GraphRender render = b.Build();
            foreach (GraphNode node in render.Order)
            {
                Assert.Equal(0, node.PositionCount);
            }
        }

        [Fact]
        public void EachRegionCountsItsOwnRows()
        {
            GraphAnnouncer.PositionText = (index, count) => index + " of " + count;
            object rowC = new object();
            GraphBuilder b = new GraphBuilder();
            GraphSheet s = new GraphSheet(b, "t:");
            s.Region("Fleets");
            s.Row(Vt("Alpha"), _rowA, null);
            s.Row(Vt("Beta"), _rowB, null);
            s.Region("Ships");
            s.Row(Vt("Gamma"), rowC, null);
            s.Finish();
            GraphRender render = b.Build();

            Assert.Equal(
                "Fleets, Alpha, 1 of 2",
                GraphAnnouncer.ComposeFull(Node(render, "t:row" + _rowA.GetHashCode() + "c0"))
            );
            Assert.Equal(
                "Ships, Gamma, 1 of 1",
                GraphAnnouncer.ComposeFull(Node(render, "t:row" + rowC.GetHashCode() + "c0"))
            );
        }

        [Fact]
        public void ARowThatHasMovedStillReadsAsTheSameRow()
        {
            GraphAnnouncer.PositionText = (index, count) => index + " of " + count;
            GraphState state = new GraphState();
            bool swapped = false;
            KeyGraph g = new KeyGraph(() =>
            {
                GraphBuilder b = new GraphBuilder();
                GraphSheet s = new GraphSheet(b, "t:");
                s.Region("Fleets", new[] { "Name", "Ships" });
                if (swapped)
                {
                    s.Row(Vt("Beta"), _rowB, null, () => "2");
                    s.Row(Vt("Alpha"), _rowA, null, () => "3");
                }
                else
                {
                    s.Row(Vt("Alpha"), _rowA, null, () => "3");
                    s.Row(Vt("Beta"), _rowB, null, () => "2");
                }

                s.Finish();
                return b.Build();
            }, state);
            g.Rerender();

            // A re-sort while the cursor stands still: stepping across the row afterwards is still a
            // step within ONE row, because the row is identified by what it stands for.
            swapped = true;
            g.Rerender();
            Assert.Equal("Ships, 3", Say(g.Move(GraphDir.Right)));
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
                if (swapped) { s.Row(Vt("Beta"), _rowB, null); s.Row(Vt("Alpha"), _rowA, null); }
                else { s.Row(Vt("Alpha"), _rowA, null); s.Row(Vt("Beta"), _rowB, null); }
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
