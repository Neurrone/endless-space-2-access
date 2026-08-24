using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The split behind the two "throw them all away" buttons. A failure here is a button that
    /// empties somebody else's list - the loudest defect this mod can ship, because the news it
    /// throws away is gone and no key brings it back.
    /// </summary>
    public class OwnedNotificationsTests
    {
        private class Entry
        {
            public string Text;
        }

        private sealed class MyEntry : Entry { }

        private static readonly Converter<Entry, MyEntry> Mine = entry => entry as MyEntry;

        private static List<Entry> Mixed()
        {
            return new List<Entry>
            {
                new Entry { Text = "game 1" },
                new MyEntry { Text = "mod 1" },
                new Entry { Text = "game 2" },
                new MyEntry { Text = "mod 2" },
            };
        }

        [Fact]
        public void MineIsTheModsOwnInListOrder()
        {
            List<MyEntry> mine = OwnedNotifications.Mine(Mixed(), Mine);

            Assert.Equal(new[] { "mod 1", "mod 2" }, mine.ConvertAll(e => e.Text));
        }

        [Fact]
        public void TheirsIsEverythingTheModDidNotRaise()
        {
            List<Entry> theirs = OwnedNotifications.Theirs(Mixed(), Mine);

            Assert.Equal(new[] { "game 1", "game 2" }, theirs.ConvertAll(e => e.Text));
        }

        [Fact]
        public void TheTwoSidesShareNothingAndLoseNothing()
        {
            List<Entry> all = Mixed();

            List<MyEntry> mine = OwnedNotifications.Mine(all, Mine);
            List<Entry> theirs = OwnedNotifications.Theirs(all, Mine);

            Assert.Equal(all.Count, mine.Count + theirs.Count);
            foreach (Entry entry in all)
            {
                Assert.True(
                    mine.Contains(entry as MyEntry) ^ theirs.Contains(entry),
                    "every entry belongs to exactly one side"
                );
            }
        }

        [Fact]
        public void AListOfOnlyOneOwnerLeavesTheOtherSideEmpty()
        {
            List<Entry> onlyMine = new List<Entry> { new MyEntry(), new MyEntry() };
            List<Entry> onlyTheirs = new List<Entry> { new Entry(), new Entry() };

            Assert.Equal(2, OwnedNotifications.Mine(onlyMine, Mine).Count);
            Assert.Empty(OwnedNotifications.Theirs(onlyMine, Mine));
            Assert.Empty(OwnedNotifications.Mine(onlyTheirs, Mine));
            Assert.Equal(2, OwnedNotifications.Theirs(onlyTheirs, Mine).Count);
        }

        [Fact]
        public void AHoleInTheListBelongsToNeitherSide()
        {
            List<Entry> withHole = new List<Entry> { null, new Entry(), null, new MyEntry() };

            Assert.Single(OwnedNotifications.Mine(withHole, Mine));
            Assert.Single(OwnedNotifications.Theirs(withHole, Mine));
        }

        [Fact]
        public void NothingToSplitAnswersEmptyLists()
        {
            Assert.Empty(OwnedNotifications.Mine(null, Mine));
            Assert.Empty(OwnedNotifications.Theirs(null, Mine));
            Assert.Empty(OwnedNotifications.Mine(Mixed(), (Converter<Entry, MyEntry>)null));
            Assert.Empty(OwnedNotifications.Theirs(Mixed(), (Converter<Entry, MyEntry>)null));
        }
    }
}
