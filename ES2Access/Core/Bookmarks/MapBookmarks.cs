using System;
using System.Collections.Generic;
using ES2Access.Core.Settings;
using ES2Access.Core.Util;

namespace ES2Access.Core.Bookmarks
{
    /// <summary>
    /// The map bookmarks of one campaign, in the ten slots the player sets and jumps to them by:
    /// the digit keys 1 to 9 and 0, in that order. A slot is either empty or holds one
    /// <see cref="MapBookmark"/>, setting it silently overwrites whatever was there, and no slot is
    /// special - the home system is its own gesture, not a default bookmark.
    ///
    /// The store reads and writes itself through a <see cref="SettingsFile"/>, one
    /// <c>slot&lt;digit&gt;</c> key each, and touches nothing else in that file: a comment, a blank
    /// line, or a key a newer build of the mod wrote survives a load-modify-save where it was. A
    /// value it cannot read empties THAT slot and is logged - one hand-edit gone wrong costs the
    /// player one bookmark, never the file.
    ///
    /// WHICH file a campaign's bookmarks live in, and when they are written, belong to the caller:
    /// this store never sees a path.
    /// </summary>
    public sealed class MapBookmarks
    {
        /// <summary>The slot digits, in the order the player reaches them.</summary>
        public const string Digits = "1234567890";

        private const string KeyPrefix = "slot";

        private readonly Dictionary<char, MapBookmark> _slots = new Dictionary<char, MapBookmark>();

        /// <summary>Whether <paramref name="digit"/> is one of the ten slots.</summary>
        public static bool IsSlot(char digit)
        {
            return Digits.IndexOf(digit) >= 0;
        }

        /// <summary>How many slots are filled - 0 for a campaign the player has bookmarked nothing
        /// in.</summary>
        public int Count
        {
            get { return _slots.Count; }
        }

        /// <summary>What slot <paramref name="digit"/> holds, or false where it is empty.</summary>
        public bool TryGet(char digit, out MapBookmark bookmark)
        {
            return _slots.TryGetValue(digit, out bookmark);
        }

        /// <summary>Put a bookmark in a slot, over whatever was there.</summary>
        public void Set(char digit, MapBookmark bookmark)
        {
            if (!IsSlot(digit))
            {
                throw new ArgumentException("not a bookmark slot: '" + digit + "'", "digit");
            }

            _slots[digit] = bookmark;
        }

        /// <summary>Empty a slot.</summary>
        public void Clear(char digit)
        {
            _slots.Remove(digit);
        }

        /// <summary>Take the bookmarks the file holds, forgetting whatever was in the store - the
        /// campaign being loaded is the one whose bookmarks these are.</summary>
        public void ReadFrom(SettingsFile file)
        {
            _slots.Clear();
            if (file == null)
            {
                return;
            }

            foreach (char digit in Digits)
            {
                string key = KeyFor(digit);
                string value = file.Get(key);
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                MapBookmark bookmark;
                if (MapBookmark.TryParse(value, out bookmark))
                {
                    _slots[digit] = bookmark;
                }
                else
                {
                    Log.Warn("bookmarks: " + key + " is not a bookmark, emptying it: " + value);
                }
            }
        }

        /// <summary>Write the bookmarks into the file, an empty slot removing its key so the file
        /// says what the player's slots say and nothing more.</summary>
        public void WriteTo(SettingsFile file)
        {
            foreach (char digit in Digits)
            {
                string key = KeyFor(digit);
                MapBookmark bookmark;
                if (_slots.TryGetValue(digit, out bookmark))
                {
                    file.Set(key, bookmark.ToValue());
                }
                else
                {
                    file.Remove(key);
                }
            }
        }

        private static string KeyFor(char digit)
        {
            return KeyPrefix + digit;
        }
    }
}
