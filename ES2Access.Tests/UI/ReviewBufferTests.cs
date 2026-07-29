using ES2Access.Core.UI.Buffers;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The reading cursor: what the player hears when they walk a buffer, and what happens at its
    /// edges. Clamping is the interesting part - a review key must never fail, only repeat itself.
    /// </summary>
    public class ReviewBufferTests
    {
        private static ReviewBuffer Ui(params string[] lines)
        {
            ReviewBuffer buffer = new ReviewBuffer("ui", () => "Details", false);
            buffer.ReplaceLines(lines);
            return buffer;
        }

        [Fact]
        public void EmptyBufferHasNoCurrentLine()
        {
            ReviewBuffer buffer = Ui();

            Assert.Null(buffer.CurrentLine);
            Assert.Equal(0, buffer.Count);
            Assert.Equal(0, buffer.CurrentLineIndex);
        }

        [Fact]
        public void LinesAreTrimmedAndBlanksDropped()
        {
            ReviewBuffer buffer = Ui("  New Game  ", "", "   ", null, "Start a new game");

            Assert.Equal(new[] { "New Game", "Start a new game" }, buffer.Lines);
        }

        [Fact]
        public void ReplaceLinesStartsAtTheTop()
        {
            ReviewBuffer buffer = Ui("one", "two", "three");
            buffer.MoveLast();

            buffer.ReplaceLines(new[] { "alpha", "beta" });

            Assert.Equal(0, buffer.CurrentLineIndex);
            Assert.Equal("alpha", buffer.CurrentLine);
        }

        [Fact]
        public void ReplaceLinesWithNothingEmptiesTheBuffer()
        {
            ReviewBuffer buffer = Ui("one", "two");

            buffer.ReplaceLines(null);

            Assert.Equal(0, buffer.Count);
            Assert.Null(buffer.CurrentLine);
        }

        [Fact]
        public void SteppingWalksTheLines()
        {
            ReviewBuffer buffer = Ui("one", "two", "three");

            Assert.Equal(ReviewBufferMove.Moved, buffer.MoveNext());
            Assert.Equal("two", buffer.CurrentLine);
            Assert.Equal(ReviewBufferMove.Moved, buffer.MovePrevious());
            Assert.Equal("one", buffer.CurrentLine);
        }

        [Fact]
        public void SteppingPastTheEndClampsAndReportsTheEdge()
        {
            ReviewBuffer buffer = Ui("one", "two");
            buffer.MoveLast();

            Assert.Equal(ReviewBufferMove.EndOfBuffer, buffer.MoveNext());
            Assert.Equal("two", buffer.CurrentLine);
            Assert.Equal(1, buffer.CurrentLineIndex);
        }

        [Fact]
        public void SteppingBeforeTheStartClampsAndReportsTheEdge()
        {
            ReviewBuffer buffer = Ui("one", "two");

            Assert.Equal(ReviewBufferMove.BeginningOfBuffer, buffer.MovePrevious());
            Assert.Equal("one", buffer.CurrentLine);
            Assert.Equal(0, buffer.CurrentLineIndex);
        }

        [Fact]
        public void EveryMoveOnAnEmptyBufferReportsAnEdge()
        {
            ReviewBuffer buffer = Ui();

            Assert.Equal(ReviewBufferMove.EndOfBuffer, buffer.MoveNext());
            Assert.Equal(ReviewBufferMove.BeginningOfBuffer, buffer.MovePrevious());
            Assert.Equal(ReviewBufferMove.BeginningOfBuffer, buffer.MoveFirst());
            Assert.Equal(ReviewBufferMove.EndOfBuffer, buffer.MoveLast());
            Assert.Null(buffer.CurrentLine);
        }

        [Fact]
        public void FirstAndLastJumpToTheEdges()
        {
            ReviewBuffer buffer = Ui("one", "two", "three");

            Assert.Equal(ReviewBufferMove.Moved, buffer.MoveLast());
            Assert.Equal("three", buffer.CurrentLine);
            Assert.Equal(ReviewBufferMove.Moved, buffer.MoveFirst());
            Assert.Equal("one", buffer.CurrentLine);
        }

        [Fact]
        public void AppendLeavesAStillBufferWhereThePlayerLeftIt()
        {
            ReviewBuffer buffer = Ui("one", "two");

            buffer.AppendLine("three");

            Assert.Equal(0, buffer.CurrentLineIndex);
            Assert.Equal("one", buffer.CurrentLine);
        }

        [Fact]
        public void AppendCarriesTheCursorOnAFollowingBuffer()
        {
            ReviewBuffer log = new ReviewBuffer("log", () => "Log", true);
            log.ReplaceLines(new[] { "one", "two" });

            log.AppendLine("three");

            Assert.Equal(2, log.CurrentLineIndex);
            Assert.Equal("three", log.CurrentLine);
        }

        [Fact]
        public void AppendingNothingDoesNotMoveAFollowingCursor()
        {
            ReviewBuffer log = new ReviewBuffer("log", () => "Log", true);
            log.ReplaceLines(new[] { "one", "two" });
            log.MoveFirst();

            log.AppendLine("   ");

            Assert.Equal(2, log.Count);
            Assert.Equal("one", log.CurrentLine);
        }

        [Fact]
        public void ClearEmptiesTheBufferAndTheCursor()
        {
            ReviewBuffer buffer = Ui("one", "two");
            buffer.MoveLast();

            buffer.Clear();

            Assert.Equal(0, buffer.Count);
            Assert.Equal(0, buffer.CurrentLineIndex);
            Assert.Null(buffer.CurrentLine);
        }

        [Fact]
        public void ShrinkingTheBufferPullsTheCursorBackInside()
        {
            ReviewBuffer log = new ReviewBuffer("log", () => "Log", true);
            log.ReplaceLines(new[] { "one", "two", "three" });
            log.MoveLast();

            log.ReplaceLines(new[] { "only" });

            Assert.Equal(0, log.CurrentLineIndex);
            Assert.Equal("only", log.CurrentLine);
        }

        [Fact]
        public void LabelFallsBackToTheKeyWhenThereIsNoName()
        {
            Assert.Equal("ui", new ReviewBuffer("ui", null, false).LabelText);
            Assert.Equal("ui", new ReviewBuffer("ui", () => "", false).LabelText);
            Assert.Equal("Details", new ReviewBuffer("ui", () => "Details", false).LabelText);
        }
    }
}
