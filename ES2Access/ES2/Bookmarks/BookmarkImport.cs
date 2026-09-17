using System;
using System.Collections.Generic;
using ES2Access.Core.Settings;

namespace ES2Access.ES2.Bookmarks
{
    /// <summary>What pasted text turned out to be.</summary>
    public enum BookmarkImportReading
    {
        /// <summary>Nothing was pasted - an empty clipboard, or one holding only whitespace.
        /// </summary>
        Empty,

        /// <summary>Something was pasted, and it is not a bookmarks file: it says nothing about
        /// which campaign it belongs to, so there is no file it could be filed as.</summary>
        NotBookmarks,

        /// <summary>A bookmarks file, naming its campaign. How many bookmarks it carries is a
        /// separate question - a file with none is still a bookmarks file.</summary>
        Bookmarks,
    }

    /// <summary>
    /// SOMEBODY ELSE'S BOOKMARKS, AS PASTED TEXT - read, identified, and merged into the file they
    /// belong in.
    ///
    /// The receiving half of the copy button (<c>BookmarkRows</c>): a save changes hands, the
    /// bookmarks that go with it arrive as a paste in a chat window, and this is what turns that
    /// back into the file the mod reads. The text carries the campaign and the faction part
    /// (<see cref="BookmarkFile"/>), which is what says WHICH file - the one the campaign's owner
    /// would have written themselves, whether or not that campaign is the one being played, or one
    /// this machine has ever seen.
    ///
    /// MERGING RATHER THAN REPLACING (owner ruling 2026-09-17): a slot the pasted text says nothing
    /// about is left exactly as it was, so a player who receives two people's bookmarks keeps both.
    /// What a pasted slot DOES take with it is any other slot holding the same place, by the store's
    /// own one-place-one-slot rule (<see cref="MapBookmarks.SetAlone"/>), so an import cannot leave
    /// the file saying the same thing twice.
    ///
    /// Engine-free on purpose: the parsing and the merge are the half that can be tested with no
    /// game running, and the clipboard, the folder and the message box are the caller's.
    /// </summary>
    public sealed class BookmarkImport
    {
        private readonly MapBookmarks _pasted = new MapBookmarks();
        private string _campaign;
        private string _faction;

        /// <summary>
        /// Read pasted text. Answers what it turned out to be; <paramref name="import"/> is null for
        /// anything but <see cref="BookmarkImportReading.Bookmarks"/>.
        ///
        /// A slot whose value is not a bookmark is dropped rather than failing the import, which is
        /// the same answer the store gives a hand-edited file: one bad line costs one bookmark, not
        /// the paste.
        /// </summary>
        public static BookmarkImportReading Read(string text, out BookmarkImport import)
        {
            import = null;
            if (text == null || text.Trim().Length == 0)
            {
                return BookmarkImportReading.Empty;
            }

            SettingsFile file = SettingsFile.Parse(Lines(text));
            string campaign = BookmarkFile.CampaignOf(file);
            if (campaign == null)
            {
                return BookmarkImportReading.NotBookmarks;
            }

            import = new BookmarkImport();
            import._campaign = campaign;
            import._faction = BookmarkFile.FactionOf(file);
            import._pasted.ReadFrom(file);
            return BookmarkImportReading.Bookmarks;
        }

        /// <summary>Which campaign these bookmarks belong to, as the file's own name spells it.
        /// </summary>
        public string Campaign
        {
            get { return _campaign; }
        }

        /// <summary>What the file this belongs in is called - folder excluded, since where the
        /// folder is belongs to the running mod.</summary>
        public string FileName
        {
            get { return BookmarkFile.NameOf(_campaign, _faction); }
        }

        /// <summary>How many bookmarks the paste carries.</summary>
        public int Count
        {
            get { return _pasted.Count; }
        }

        /// <summary>
        /// Put the pasted bookmarks into the campaign's own file, and answer how many went in.
        ///
        /// <paramref name="tiles"/> says whether "the same place" may be judged on the spoken tile,
        /// which needs the empire's home: true with <paramref name="originX"/> and
        /// <paramref name="originY"/> for the campaign being played, false for one that is not, where
        /// the question falls back to exact positions (<see cref="MapBookmarks.SetAloneExactly"/>).
        ///
        /// The file is left identified, so a file this import CREATES says whose it is from its first
        /// line onwards rather than waiting for the campaign to be played again.
        /// </summary>
        public int MergeInto(SettingsFile target, bool tiles, float originX, float originY)
        {
            if (target == null)
            {
                return 0;
            }

            MapBookmarks slots = new MapBookmarks();
            slots.ReadFrom(target);

            for (int i = 0; i < MapBookmarks.Digits.Length; i++)
            {
                char digit = MapBookmarks.Digits[i];
                MapBookmark bookmark;
                if (!_pasted.TryGet(digit, out bookmark))
                {
                    continue;
                }

                if (tiles)
                {
                    slots.SetAlone(digit, bookmark, originX, originY);
                }
                else
                {
                    slots.SetAloneExactly(digit, bookmark);
                }
            }

            // Counted at the END, not as each slot goes in: a paste that names one place twice
            // loses the earlier of the two to the same rule everything else is held to, and the
            // player is told what their file now holds rather than how many lines were read.
            int imported = 0;
            for (int i = 0; i < MapBookmarks.Digits.Length; i++)
            {
                char digit = MapBookmarks.Digits[i];
                MapBookmark held;
                if (_pasted.TryGet(digit, out held) && slots.TryGet(digit, out held))
                {
                    imported++;
                }
            }

            slots.WriteTo(target);
            BookmarkFile.Identify(target, _campaign, _faction);
            return imported;
        }

        /// <summary>The pasted text as lines, however the machine it was written on ends one.
        /// </summary>
        private static IEnumerable<string> Lines(string text)
        {
            string[] split = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return split;
        }
    }
}
