using ES2Access.Core.Settings;
using ES2Access.ES2.Bookmarks;
using Xunit;

namespace ES2Access.Tests.ES2.Bookmarks
{
    /// <summary>What pasted text is read as, and what merging it into a campaign's file does to the
    /// slots already there.</summary>
    public class BookmarkImportTests
    {
        private const string Campaign = "3f8a1c0d9b7e4a2f8c6d5e4b3a291807";

        // Home is deliberately not at the origin: the tile is measured from the empire's home.
        private const float HomeX = 68.884f;
        private const float HomeY = -22.45f;

        private static string Pasted(params string[] lines)
        {
            return string.Join("\r\n", lines);
        }

        private static BookmarkImport Read(string text)
        {
            BookmarkImport import;
            Assert.Equal(BookmarkImportReading.Bookmarks, BookmarkImport.Read(text, out import));
            return import;
        }

        private static MapBookmarks SlotsOf(SettingsFile file)
        {
            MapBookmarks slots = new MapBookmarks();
            slots.ReadFrom(file);
            return slots;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   \r\n\t  \r\n")]
        public void NothingOnTheClipboardIsAnEmptyClipboard(string text)
        {
            BookmarkImport import;
            Assert.Equal(BookmarkImportReading.Empty, BookmarkImport.Read(text, out import));
            Assert.Null(import);
        }

        /// <summary>Text saying nothing a campaign could be named by is not a bookmarks file - a
        /// chat message, and equally a bookmarks-shaped file whose campaign line is a hand-edit gone
        /// wrong.</summary>
        [Theory]
        [InlineData("what is this even")]
        [InlineData("slot1 = 5,1,2")]
        [InlineData("campaign = \r\nslot1 = 5,1,2")]
        [InlineData("campaign = not-a-guid\r\nslot1 = 5,1,2")]
        public void TextThatNamesNoCampaignIsNotABookmarksFile(string text)
        {
            BookmarkImport import;
            Assert.Equal(BookmarkImportReading.NotBookmarks, BookmarkImport.Read(text, out import));
            Assert.Null(import);
        }

        /// <summary>The file the paste belongs in is the one the campaign's own machine would have
        /// written: the faction in front of the GUID, and the bare GUID where the paste names no
        /// faction.</summary>
        [Fact]
        public void ThePasteNamesTheFileItBelongsIn()
        {
            Assert.Equal(
                "Horatio-" + Campaign + ".cfg",
                Read(Pasted("campaign = " + Campaign, "faction = Horatio")).FileName
            );
            Assert.Equal(Campaign + ".cfg", Read(Pasted("campaign = " + Campaign)).FileName);
            Assert.Equal(Campaign, Read(Pasted("campaign = " + Campaign.ToUpperInvariant())).Campaign);
        }

        /// <summary>A slot the paste says nothing about is left exactly as it was; a slot it does
        /// carry is written over. The answer is how many slots the paste put in, not how many the
        /// file ends with.</summary>
        [Fact]
        public void OnlyThePastedSlotsAreWritten()
        {
            SettingsFile target = SettingsFile.Parse(
                new[] { "slot1 = 0,10,10", "slot5 = 0,50,50" }
            );

            int imported = Read(
                Pasted("campaign = " + Campaign, "slot1 = 0,11,11", "slot9 = 0,90,90")
            ).MergeInto(target, true, HomeX, HomeY);

            Assert.Equal(2, imported);
            MapBookmarks slots = SlotsOf(target);
            MapBookmark bookmark;
            Assert.True(slots.TryGet('1', out bookmark));
            Assert.Equal(11f, bookmark.X);
            Assert.True(slots.TryGet('5', out bookmark));
            Assert.Equal(50f, bookmark.X);
            Assert.True(slots.TryGet('9', out bookmark));
            Assert.Equal(3, slots.Count);
        }

        /// <summary>A slot already holding a place the paste puts somewhere else is emptied, so the
        /// file never says the same thing twice. For the campaign being played the question is the
        /// spoken tile, which is why the duplicate here is a point a tile away from the pasted one.
        /// </summary>
        [Fact]
        public void ASlotAlreadyHoldingThePastedPlaceIsEmptied()
        {
            // 73.284,-24.85 and 72.484,-24.05 are 4.4 and 3.6 tiles east of home, 2.4 and 1.6
            // north of it: two positions the player hears as the same pair.
            SettingsFile target = SettingsFile.Parse(
                new[] { "slot4 = 162,2.4,-48.6", "slot6 = 0,73.284,-24.85" }
            );

            int imported = Read(
                Pasted(
                    "campaign = " + Campaign,
                    "slot2 = 162,2.4,-48.6",
                    "slot3 = 0,72.484,-24.05"
                )
            ).MergeInto(target, true, HomeX, HomeY);

            Assert.Equal(2, imported);
            MapBookmarks slots = SlotsOf(target);
            Assert.False(slots.TryGet('4', out _));
            Assert.False(slots.TryGet('6', out _));
            Assert.True(slots.TryGet('2', out _));
            Assert.True(slots.TryGet('3', out _));
            Assert.Equal(2, slots.Count);
        }

        /// <summary>With no empire whose home the tiles could be counted from - a campaign that is
        /// not the one being played - two points are the same place only when they are the very same
        /// point, while two bookmarks on one system still are.</summary>
        [Fact]
        public void WithNoHomeOnlyTheExactSamePointIsTheSamePlace()
        {
            SettingsFile target = SettingsFile.Parse(
                new[] { "slot4 = 162,2.4,-48.6", "slot6 = 0,73.284,-24.85", "slot7 = 0,73.3,-24.9" }
            );

            int imported = Read(
                Pasted(
                    "campaign = " + Campaign,
                    "slot2 = 162,9.9,-9.9",
                    "slot3 = 0,73.284,-24.85"
                )
            ).MergeInto(target, false, 0f, 0f);

            Assert.Equal(2, imported);
            MapBookmarks slots = SlotsOf(target);
            Assert.False(slots.TryGet('4', out _));
            Assert.False(slots.TryGet('6', out _));
            // A point a tile away from the pasted one is left alone: without a home there is no
            // tile to judge it by.
            Assert.True(slots.TryGet('7', out _));
            Assert.Equal(3, slots.Count);
        }

        /// <summary>A paste naming one place in two slots keeps the later slot, like any other
        /// pair of slots on one place - and the count is what the file ends up holding, not how
        /// many lines were read.</summary>
        [Fact]
        public void APasteNamingOnePlaceTwiceKeepsOneSlot()
        {
            SettingsFile target = new SettingsFile();
            int imported = Read(
                Pasted(
                    "campaign = " + Campaign,
                    "slot1 = 0,1,2",
                    "slot2 = 900,3,4",
                    "slot9 = 0,1,2"
                )
            ).MergeInto(target, false, 0f, 0f);

            Assert.Equal(2, imported);
            MapBookmarks slots = SlotsOf(target);
            Assert.False(slots.TryGet('1', out _));
            Assert.True(slots.TryGet('9', out _));
            Assert.Equal(2, slots.Count);
        }

        /// <summary>The merged file says whose it is, so a file the import CREATED can be read back
        /// by the same import on the next machine.</summary>
        [Fact]
        public void TheMergedFileSaysWhichCampaignItBelongsTo()
        {
            SettingsFile target = new SettingsFile();
            Read(Pasted("campaign = " + Campaign, "faction = Horatio", "slot1 = 0,1,2")).MergeInto(
                target,
                false,
                0f,
                0f
            );

            Assert.Equal(Campaign, BookmarkFile.CampaignOf(target));
            Assert.Equal("Horatio", BookmarkFile.FactionOf(target));
        }
    }
}
