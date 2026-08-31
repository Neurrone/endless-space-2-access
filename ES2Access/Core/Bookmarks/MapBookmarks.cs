using System;
using System.Collections.Generic;
using ES2Access.Core.Settings;
using ES2Access.Core.Speech;
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

        /// <summary>
        /// Put a bookmark in a slot, and take that same PLACE out of every other slot: one place, one
        /// slot (owner ruling 2026-08-31). Answers the first slot emptied, or <c>'\0'</c>.
        ///
        /// A player who bookmarks somewhere they have already bookmarked meant to MOVE it there, not
        /// to own it twice - two digits for one place is two ways to say the same thing and one slot
        /// wasted out of ten. Which slot went is ANSWERED rather than swallowed, because a slot the
        /// player set must never vanish without their hearing it; the caller is the one that says so
        /// (<c>GalaxyBookmarks.Say</c>).
        ///
        /// WHAT COUNTS AS THE SAME PLACE has to be asked kind by kind, because the two kinds are not
        /// the same sort of thing. Two SYSTEM bookmarks are the same place when they are the same
        /// system - by GUID, so two different stars that happen to round into one spoken tile are two
        /// places and both keep their slots, which is the case a tile test would get wrong. Everything
        /// else is judged on the TILE, the rounded pair the player hears: two points the player cannot
        /// tell apart when they are read out are one place to them, and a point set on a system's tile
        /// is the player saying "here" about somewhere they have already named.
        ///
        /// The origin is the caller's because the tile is measured from the empire's home and this
        /// store has no way to ask where that is.
        ///
        /// Dedupe happens ON SET and never on load: a file that already holds two slots for one place
        /// keeps them until a set touches that place, because rewriting a player's file behind their
        /// back on the strength of a rule they have not invoked is not this store's business.
        /// </summary>
        public char SetAlone(char digit, MapBookmark bookmark, float originX, float originY)
        {
            char emptied = '\0';
            for (int i = 0; i < Digits.Length; i++)
            {
                char other = Digits[i];
                MapBookmark held;
                if (other == digit || !_slots.TryGetValue(other, out held))
                {
                    continue;
                }

                if (!SamePlace(held, bookmark, originX, originY))
                {
                    continue;
                }

                _slots.Remove(other);
                if (emptied == '\0')
                {
                    emptied = other;
                }
            }

            Set(digit, bookmark);
            return emptied;
        }

        /// <summary>Whether two bookmarks name the same place - see <see cref="SetAlone"/> for why the
        /// question is asked differently of two systems than of anything else.</summary>
        private static bool SamePlace(
            MapBookmark one,
            MapBookmark two,
            float originX,
            float originY
        )
        {
            if (one.IsSystem && two.IsSystem)
            {
                return one.SystemGuid == two.SystemGuid;
            }

            return MapCoordinates.Round(one.X - originX) == MapCoordinates.Round(two.X - originX)
                && MapCoordinates.Round(one.Y - originY) == MapCoordinates.Round(two.Y - originY);
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
