using System;
using System.Collections.Generic;
using ES2Access.Core.Settings;
using ES2Access.ES2.Bookmarks;
using ES2Access.ES2.UI;
using Xunit;

namespace ES2Access.Tests.ES2.Bookmarks
{
    public class MapBookmarksTests
    {
        private static MapBookmarks Read(params string[] lines)
        {
            MapBookmarks bookmarks = new MapBookmarks();
            bookmarks.ReadFrom(SettingsFile.Parse(lines));
            return bookmarks;
        }

        private static SettingsFile Written(MapBookmarks bookmarks, params string[] existing)
        {
            SettingsFile file = SettingsFile.Parse(existing);
            bookmarks.WriteTo(file);
            return file;
        }

        private static MapBookmark Slot(MapBookmarks bookmarks, char digit)
        {
            MapBookmark bookmark;
            Assert.True(bookmarks.TryGet(digit, out bookmark), "slot " + digit + " is empty");
            return bookmark;
        }

        [Fact]
        public void ABookmarkComesBackWithTheSameSystemAndTheSamePosition()
        {
            MapBookmarks set = new MapBookmarks();
            set.Set('1', MapBookmark.AtPoint(-12.5f, 0.25f));
            set.Set('9', MapBookmark.OfSystem(ulong.MaxValue, -1234.5678f, 0.1f));
            set.Set('0', MapBookmark.OfSystem(1, 0f, -0f));

            MapBookmarks back = new MapBookmarks();
            back.ReadFrom(SettingsFile.Parse(Written(set).ToLines()));

            Assert.Equal(3, back.Count);
            Assert.Equal(0UL, Slot(back, '1').SystemGuid);
            Assert.Equal(-12.5f, Slot(back, '1').X);
            Assert.Equal(0.25f, Slot(back, '1').Y);
            Assert.Equal(ulong.MaxValue, Slot(back, '9').SystemGuid);
            Assert.Equal(-1234.5678f, Slot(back, '9').X);
            Assert.Equal(0.1f, Slot(back, '9').Y);
            Assert.Equal(1UL, Slot(back, '0').SystemGuid);
        }

        [Fact]
        public void APointBookmarkNamesNoSystemAndASystemOneDoes()
        {
            Assert.False(MapBookmark.AtPoint(3f, 4f).IsSystem);
            Assert.True(MapBookmark.OfSystem(7, 3f, 4f).IsSystem);
            Assert.False(Read("slot1 = 0,3,4").TryGet('2', out _));
            Assert.False(Slot(Read("slot1 = 0,3,4"), '1').IsSystem);
            Assert.True(Slot(Read("slot1 = 7,3,4"), '1').IsSystem);
        }

        [Theory]
        [InlineData("wat")]
        [InlineData("1,2")]
        [InlineData("1,2,3,4")]
        [InlineData("x,1,2")]
        [InlineData("-1,1,2")]
        [InlineData("1,NaN,2")]
        [InlineData("1,Infinity,2")]
        [InlineData("1,,2")]
        [InlineData("1;2;3")]
        public void AValueThatIsNotABookmarkIsRefused(string value)
        {
            Assert.False(MapBookmark.TryParse(value, out _));
        }

        [Fact]
        public void AnUnreadableSlotEmptiesOnlyItself()
        {
            MapBookmarks bookmarks = Read("slot1 = 5,1,2", "slot2 = wat", "slot3 = 1,2", "slot4 = 6,3,4");

            Assert.Equal(2, bookmarks.Count);
            Assert.Equal(5UL, Slot(bookmarks, '1').SystemGuid);
            Assert.Equal(6UL, Slot(bookmarks, '4').SystemGuid);
            Assert.False(bookmarks.TryGet('2', out _));
            Assert.False(bookmarks.TryGet('3', out _));
        }

        [Fact]
        public void AnUnreadableSlotIsGoneOnTheNextSave()
        {
            MapBookmarks bookmarks = Read("slot1 = 5,1,2", "slot2 = wat");
            SettingsFile file = Written(bookmarks, "slot1 = 5,1,2", "slot2 = wat");

            Assert.True(file.Has("slot1"));
            Assert.False(file.Has("slot2"));
        }

        [Fact]
        public void NoFileIsNoBookmarks()
        {
            Assert.Equal(0, new MapBookmarks().Count);
            Assert.Equal(0, Read().Count);
            Assert.Equal(0, Read("# nothing here", string.Empty).Count);
            Assert.Equal(0, Read("slot1 =").Count);
        }

        [Fact]
        public void WhatTheFileSaysBesidesBookmarksSurvivesALoadAndSave()
        {
            string[] existing =
            {
                "# my bookmarks",
                string.Empty,
                "future.thing = 7",
                "slot2 = 5,1,2",
            };

            MapBookmarks bookmarks = Read(existing);
            bookmarks.Set('7', MapBookmark.AtPoint(8.5f, -9f));
            SettingsFile file = Written(bookmarks, existing);
            IList<string> lines = file.ToLines();

            Assert.Equal("# my bookmarks", lines[0]);
            Assert.Equal(string.Empty, lines[1]);
            Assert.Equal("future.thing = 7", lines[2]);
            Assert.Equal("7", file.Get("future.thing"));
            Assert.Equal("5,1,2", file.Get("slot2"));
            Assert.Equal("0,8.5,-9", file.Get("slot7"));
        }

        [Fact]
        public void SettingASlotOverwritesWhateverWasThere()
        {
            MapBookmarks bookmarks = Read("slot1 = 5,1,2");
            bookmarks.Set('1', MapBookmark.AtPoint(3f, 4f));

            Assert.Equal(1, bookmarks.Count);
            Assert.Equal(0UL, Slot(bookmarks, '1').SystemGuid);
            Assert.Equal("0,3,4", Written(bookmarks).Get("slot1"));
        }

        [Fact]
        public void ClearingASlotTakesItsKeyOutOfTheFile()
        {
            MapBookmarks bookmarks = Read("slot1 = 5,1,2", "slot2 = 6,3,4");
            bookmarks.Clear('1');

            Assert.Equal(1, bookmarks.Count);
            SettingsFile file = Written(bookmarks, "slot1 = 5,1,2", "slot2 = 6,3,4");
            Assert.False(file.Has("slot1"));
            Assert.True(file.Has("slot2"));
        }

        [Fact]
        public void ReadingACampaignForgetsTheOneBefore()
        {
            MapBookmarks bookmarks = Read("slot1 = 5,1,2", "slot2 = 6,3,4");
            bookmarks.ReadFrom(SettingsFile.Parse(new[] { "slot3 = 7,5,6" }));

            Assert.Equal(1, bookmarks.Count);
            Assert.True(bookmarks.TryGet('3', out _));
            Assert.False(bookmarks.TryGet('1', out _));
        }

        // ---- one place, one slot (owner ruling 2026-08-31) ----

        // Home is deliberately NOT at the origin in these tests: the tile is measured from the
        // empire's home, and a test that put home at (0,0) would pass for an implementation that
        // forgot to subtract it.
        private const float HomeX = 68.884f;
        private const float HomeY = -22.45f;

        private static char SetAlone(MapBookmarks bookmarks, char digit, MapBookmark bookmark)
        {
            return bookmarks.SetAlone(digit, bookmark, HomeX, HomeY);
        }

        /// <summary>Two SYSTEM bookmarks on the same system are one place: the older slot goes.
        /// </summary>
        [Fact]
        public void SettingASystemAlreadyBookmarkedEmptiesTheOldSlot()
        {
            MapBookmarks bookmarks = new MapBookmarks();
            SetAlone(bookmarks, '2', MapBookmark.OfSystem(162, 2.4f, -48.6f));
            SetAlone(bookmarks, '7', MapBookmark.OfSystem(505, 53.0f, -27.0f));

            Assert.Equal('2', SetAlone(bookmarks, '9', MapBookmark.OfSystem(162, 2.4f, -48.6f)));
            Assert.False(bookmarks.TryGet('2', out _));
            Assert.True(bookmarks.TryGet('9', out _));
            // Nothing else is touched.
            Assert.True(bookmarks.TryGet('7', out _));
            Assert.Equal(2, bookmarks.Count);
        }

        /// <summary>Two POINT bookmarks the player cannot tell apart when they are read out - the same
        /// rounded pair - are one place, however far apart the raw floats are inside the tile.
        /// </summary>
        [Fact]
        public void SettingAPointOnABookmarkedTileEmptiesTheOldSlot()
        {
            MapBookmarks bookmarks = new MapBookmarks();
            SetAlone(bookmarks, '3', MapBookmark.AtPoint(HomeX + 4.4f, HomeY - 2.4f));

            Assert.Equal(
                '3',
                SetAlone(bookmarks, '8', MapBookmark.AtPoint(HomeX + 3.6f, HomeY - 1.6f))
            );
            Assert.False(bookmarks.TryGet('3', out _));
            Assert.Equal(1, bookmarks.Count);
        }

        /// <summary>Mixed kinds collide on the tile, both ways round: a point set on a bookmarked
        /// system's tile takes the system's slot, and a system set on a bookmarked point's tile takes
        /// the point's.</summary>
        [Fact]
        public void APointAndASystemOnOneTileAreOnePlace()
        {
            MapBookmarks one = new MapBookmarks();
            SetAlone(one, '1', MapBookmark.OfSystem(162, HomeX - 66.2f, HomeY - 26.2f));
            Assert.Equal(
                '1',
                SetAlone(one, '4', MapBookmark.AtPoint(HomeX - 66.0f, HomeY - 26.0f))
            );
            Assert.False(one.TryGet('1', out _));

            MapBookmarks two = new MapBookmarks();
            SetAlone(two, '1', MapBookmark.AtPoint(HomeX - 66.0f, HomeY - 26.0f));
            Assert.Equal(
                '1',
                SetAlone(two, '4', MapBookmark.OfSystem(162, HomeX - 66.2f, HomeY - 26.2f))
            );
            Assert.False(two.TryGet('1', out _));
        }

        /// <summary>The case a tile test alone would get wrong: two DIFFERENT systems close enough to
        /// round into one spoken tile are two places, and both keep their slots.</summary>
        [Fact]
        public void TwoSystemsSharingATileDoNotCollide()
        {
            MapBookmarks bookmarks = new MapBookmarks();
            SetAlone(bookmarks, '1', MapBookmark.OfSystem(162, HomeX + 4.4f, HomeY + 4.4f));

            Assert.Equal(
                '\0',
                SetAlone(bookmarks, '5', MapBookmark.OfSystem(505, HomeX + 3.6f, HomeY + 3.6f))
            );
            Assert.True(bookmarks.TryGet('1', out _));
            Assert.True(bookmarks.TryGet('5', out _));
            Assert.Equal(2, bookmarks.Count);
        }

        /// <summary>Re-setting the SAME slot is the plain overwrite it always was - a slot does not
        /// collide with itself, and nothing is reported as emptied.</summary>
        [Fact]
        public void ReSettingTheSameSlotIsAPlainOverwrite()
        {
            MapBookmarks bookmarks = new MapBookmarks();
            SetAlone(bookmarks, '6', MapBookmark.OfSystem(162, 2.4f, -48.6f));

            Assert.Equal('\0', SetAlone(bookmarks, '6', MapBookmark.OfSystem(162, 2.4f, -48.6f)));
            Assert.True(bookmarks.TryGet('6', out _));
            Assert.Equal(1, bookmarks.Count);

            Assert.Equal('\0', SetAlone(bookmarks, '6', MapBookmark.AtPoint(HomeX, HomeY)));
            Assert.Equal(1, bookmarks.Count);
        }

        /// <summary>A place nobody has bookmarked takes its slot and empties nothing.</summary>
        [Fact]
        public void SettingSomewhereNewEmptiesNothing()
        {
            MapBookmarks bookmarks = new MapBookmarks();
            SetAlone(bookmarks, '1', MapBookmark.OfSystem(162, 2.4f, -48.6f));

            Assert.Equal('\0', SetAlone(bookmarks, '2', MapBookmark.AtPoint(HomeX + 9f, HomeY)));
            Assert.Equal(2, bookmarks.Count);
        }

        /// <summary>A file that already holds a duplicate keeps it until a set touches that place -
        /// dedupe is a rule about SETTING, not a rewrite of the player's file on load. And when a set
        /// does touch it, the place is left to one slot.</summary>
        [Fact]
        public void ExistingDuplicatesSurviveALoadAndGoOnTheNextSetThere()
        {
            MapBookmarks bookmarks = Read("slot2 = 0,1,2", "slot4 = 0,1,2");
            Assert.Equal(2, bookmarks.Count);

            Assert.Equal('2', SetAlone(bookmarks, '9', MapBookmark.AtPoint(1f, 2f)));
            Assert.False(bookmarks.TryGet('2', out _));
            Assert.False(bookmarks.TryGet('4', out _));
            Assert.Equal(1, bookmarks.Count);
        }

        [Fact]
        public void OnlyTheTenDigitKeysAreSlots()
        {
            Assert.Equal("1234567890", MapBookmarks.Digits);
            Assert.True(MapBookmarks.IsSlot('0'));
            Assert.False(MapBookmarks.IsSlot('a'));
            Assert.Throws<ArgumentException>(
                () => new MapBookmarks().Set('a', MapBookmark.AtPoint(1f, 2f)));
        }
    }
}
