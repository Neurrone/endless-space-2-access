using ES2Access.Core.UI.Buffers;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Which buffer the player is in, and how switching behaves when most of them are out of scope -
    /// the case that matters, because on most screens only the UI buffer exists.
    /// </summary>
    public class ReviewBufferManagerTests
    {
        private const string Ui = "ui";
        private const string Notifications = "notifications";
        private const string Combat = "combat";

        private static ReviewBufferManager Registered()
        {
            ReviewBufferManager buffers = new ReviewBufferManager();
            buffers.Register(Ui, () => "Details");
            buffers.Register(Notifications, () => "Notifications", true);
            buffers.Register(Combat, () => "Combat", true);
            return buffers;
        }

        [Fact]
        public void TheFirstBufferRegisteredIsTheStartingOne()
        {
            Assert.Equal(Ui, Registered().CurrentKey);
        }

        [Fact]
        public void OnlyTheFirstBufferIsVisibleUntilAScreenSaysOtherwise()
        {
            ReviewBufferManager buffers = Registered();

            Assert.True(buffers.Get(Ui).Visible);
            Assert.False(buffers.Get(Notifications).Visible);
            Assert.False(buffers.Get(Combat).Visible);
        }

        [Fact]
        public void TheFirstBufferStaysVisibleHoweverTheVisibleSetIsDeclared()
        {
            ReviewBufferManager buffers = Registered();

            buffers.SetVisible(new[] { Combat });

            Assert.True(buffers.Get(Ui).Visible);
            Assert.True(buffers.Get(Combat).Visible);
            Assert.False(buffers.Get(Notifications).Visible);
        }

        [Fact]
        public void ClearingTheVisibleSetLeavesTheFirstBuffer()
        {
            ReviewBufferManager buffers = Registered();
            buffers.SetVisible(new[] { Combat });

            buffers.SetVisible(null);

            Assert.True(buffers.Get(Ui).Visible);
            Assert.False(buffers.Get(Combat).Visible);
        }

        [Fact]
        public void SwitchingWithOneVisibleBufferStaysOnIt()
        {
            ReviewBufferManager buffers = Registered();

            Assert.Equal(Ui, buffers.MoveBuffer(1).Key);
            Assert.Equal(Ui, buffers.MoveBuffer(-1).Key);
            Assert.Equal(Ui, buffers.CurrentKey);
        }

        [Fact]
        public void SwitchingSkipsHiddenBuffersAndWraps()
        {
            ReviewBufferManager buffers = Registered();
            buffers.SetVisible(new[] { Combat });

            Assert.Equal(Combat, buffers.MoveBuffer(1).Key);
            Assert.Equal(Ui, buffers.MoveBuffer(1).Key);
            Assert.Equal(Combat, buffers.MoveBuffer(-1).Key);
        }

        [Fact]
        public void HidingTheCurrentBufferHandsOverToTheFirstVisibleOne()
        {
            ReviewBufferManager buffers = Registered();
            buffers.SetVisible(new[] { Combat });
            buffers.SetCurrent(Combat);

            buffers.SetVisible(null);

            Assert.Equal(Ui, buffers.CurrentKey);
        }

        [Fact]
        public void SwitchingToAFollowingBufferShowsItsNewestLine()
        {
            ReviewBufferManager buffers = Registered();
            buffers.SetVisible(new[] { Combat });
            buffers.ReplaceLines(Combat, new[] { "one", "two", "three" });

            Assert.Equal("three", buffers.MoveBuffer(1).CurrentLine);
        }

        [Fact]
        public void SwitchingToAStillBufferKeepsThePlayersPlace()
        {
            ReviewBufferManager buffers = Registered();
            buffers.SetVisible(new[] { Combat });
            buffers.ReplaceLines(Ui, new[] { "New Game", "Start a new game" });
            buffers.MoveNextLine();
            buffers.MoveBuffer(1);

            Assert.Equal(Ui, buffers.MoveBuffer(1).Key);
            Assert.Equal("Start a new game", buffers.Current.CurrentLine);
        }

        [Fact]
        public void SetCurrentIgnoresABufferThePlayerCannotReach()
        {
            ReviewBufferManager buffers = Registered();

            Assert.Null(buffers.SetCurrent(Combat));
            Assert.Equal(Ui, buffers.CurrentKey);
        }

        [Fact]
        public void SetCurrentSnapsAFollowingBufferToItsNewestLine()
        {
            ReviewBufferManager buffers = Registered();
            buffers.SetVisible(new[] { Notifications });
            buffers.ReplaceLines(Notifications, new[] { "one", "two" });
            buffers.SetCurrent(Notifications);
            buffers.MoveFirstLine();

            buffers.SetCurrent(Ui);
            buffers.SetCurrent(Notifications);

            Assert.Equal("two", buffers.Current.CurrentLine);
        }

        [Fact]
        public void LineMovesGoToTheCurrentBuffer()
        {
            ReviewBufferManager buffers = Registered();
            buffers.SetVisible(new[] { Combat });
            buffers.ReplaceLines(Ui, new[] { "one", "two" });
            buffers.ReplaceLines(Combat, new[] { "hit", "miss" });

            buffers.MoveNextLine();
            Assert.Equal("two", buffers.Current.CurrentLine);

            buffers.SetCurrent(Combat);
            buffers.MoveFirstLine();
            Assert.Equal("hit", buffers.Current.CurrentLine);
            Assert.Equal("two", buffers.Get(Ui).CurrentLine);
        }

        [Fact]
        public void AppendGoesToTheNamedBufferWhicheverThePlayerIsIn()
        {
            ReviewBufferManager buffers = Registered();
            buffers.SetVisible(new[] { Combat });
            buffers.AppendLine(Combat, "hit");

            Assert.Equal(Ui, buffers.CurrentKey);
            Assert.Equal("hit", buffers.Get(Combat).CurrentLine);
        }

        [Fact]
        public void ClearEmptiesTheNamedBuffer()
        {
            ReviewBufferManager buffers = Registered();
            buffers.ReplaceLines(Ui, new[] { "one" });

            buffers.Clear(Ui);

            Assert.Null(buffers.Current.CurrentLine);
        }

        [Fact]
        public void RegisteringTheSameKeyTwiceIsRefused()
        {
            ReviewBufferManager buffers = Registered();

            Assert.Null(buffers.Register(Ui, () => "Other"));
            Assert.Equal(3, buffers.Buffers.Count);
        }

        [Fact]
        public void AManagerWithNoBuffersIsHarmless()
        {
            ReviewBufferManager buffers = new ReviewBufferManager();

            Assert.Null(buffers.Current);
            Assert.Null(buffers.CurrentKey);
            Assert.Null(buffers.MoveBuffer(1));
            Assert.Equal(ReviewBufferMove.EndOfBuffer, buffers.MoveNextLine());
            Assert.Equal(ReviewBufferMove.BeginningOfBuffer, buffers.MovePreviousLine());
        }
    }
}
