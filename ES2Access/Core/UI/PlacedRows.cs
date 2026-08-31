using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// ONE DECLARATION PER KIND OF ROW THAT STANDS SOMEWHERE ON THE MAP - what a probe's row, a
    /// bookmark's row or a star's row can DO, written down once instead of four times.
    ///
    /// Four surfaces used to ask the same question of the same rows and each kept its own list:
    /// whether the row can arm the inspect cell, whether Enter can land on it and in what order,
    /// whether a leap away from it is worth remembering, and whether it is somewhere the player can be
    /// put back. Every list was written by hand, and they drifted: a point bookmark answered three of
    /// them and was missing from Enter's, so a square that READ OUT "bookmark 1" did nothing when the
    /// player pressed Enter on it; probes, obliterator missiles and ally pins were missing from the
    /// same one. The lists are this table now, and a row kind that is not in it fails a build-time lint
    /// rather than working in three places and not the fourth.
    ///
    /// A row is identified by its KEY SEGMENT - the word its structural key names it with,
    /// <c>galaxy:probe/1621</c> being a <c>probe</c> and <c>.../system/162/planet/0</c> a
    /// <c>planet</c>. That is a fact about the tree the page builds, so it is the one thing this table
    /// needs from the engine and the only thing it borrows.
    ///
    /// GROUPINGS ARE DECLARED TOO, as REFUSALS. A constellation heading is not a place - it is a name
    /// written across a stretch of sky - and the entity behind it HAS a position, the centroid, which
    /// is exactly the trap: an arming that walked up from a row with no place of its own reached the
    /// heading and opened the cell a constellation away. Saying "refuses" out loud is what keeps that
    /// from being rediscovered.
    ///
    /// A row kind that is CARRIED by an ancestor - a planet, a lane, a dossier, the label's action row
    /// - is deliberately absent from this table: it stands nowhere itself and answers every one of the
    /// four questions through the star it hangs under. The lint keeps a list of those by name, so a
    /// NEW segment is neither silently positioned nor silently carried.
    /// </summary>
    public sealed class PlacedRow
    {
        /// <summary>The word this row's structural key names it with - <c>probe</c>, <c>bookmark</c>,
        /// <c>system</c>. Unique across the table.</summary>
        public readonly string Segment;

        /// <summary>Whether the inspect cell may be ARMED on this row: it stands somewhere, so there
        /// is a square to open on.</summary>
        public readonly bool Arms;

        /// <summary>Where this kind comes in Enter's order, or 0 for a row Enter never lands on.
        /// Lower wins; see <see cref="PlacedRows.Tiers"/> for what the numbers mean.</summary>
        public readonly int EnterTier;

        /// <summary>Whether a leap away from this row is worth remembering for Backspace.</summary>
        public readonly bool Leap;

        /// <summary>Whether the player may be PUT BACK here when the row they left has died.</summary>
        public readonly bool Restore;

        /// <summary>A heading rather than a place: it answers no to all four, and says so on purpose.
        /// </summary>
        public readonly bool Refuses;

        private PlacedRow(
            string segment,
            bool arms,
            int enterTier,
            bool leap,
            bool restore,
            bool refuses
        )
        {
            Segment = segment;
            Arms = arms;
            EnterTier = enterTier;
            Leap = leap;
            Restore = restore;
            Refuses = refuses;
        }

        /// <summary>A row that stands somewhere of its own.</summary>
        public static PlacedRow Placed(string segment, int enterTier, bool leap, bool restore)
        {
            return new PlacedRow(segment, true, enterTier, leap, restore, false);
        }

        /// <summary>A grouping: never a place, whatever the entity behind it happens to carry.
        /// </summary>
        public static PlacedRow Grouping(string segment)
        {
            return new PlacedRow(segment, false, 0, false, false, true);
        }
    }

    /// <summary>
    /// The table itself, and the one way to read a row's kind off its key.
    ///
    /// Off the engine on purpose: the ORDER Enter offers things in, and which kinds answer which
    /// question, are decisions rather than lookups, and they are the kind of decision that is wrong
    /// silently. Here they are a list a test can read.
    /// </summary>
    public static class PlacedRows
    {
        /// <summary>
        /// Enter's order, as names rather than numbers.
        ///
        /// A PLACE first: a star is what a square is usually about, and everything else in it is
        /// standing AT the star. Then the things that MOVE - a fleet, then the smaller movers a player
        /// sweeps the map looking for. Then the ANNOTATIONS: a quest marker the game planted, and last
        /// a bookmark, which is the player's own note and the one thing in a square that is not a fact
        /// about the galaxy at all.
        /// </summary>
        public const int TierPlace = 1;
        public const int TierFleet = 2;
        public const int TierMover = 3;
        public const int TierMarker = 4;
        public const int TierBookmark = 5;

        /// <summary>How many tiers Enter walks, so a caller can loop without knowing the names.
        /// </summary>
        public const int Tiers = 5;

        private static readonly PlacedRow[] Table = new PlacedRow[]
        {
            // A star system, and the special nodes the map strings the galaxy with - the same row
            // shape, the same key, and the cell reads them alike.
            PlacedRow.Placed("system", TierPlace, true, true),
            PlacedRow.Placed("fleet", TierFleet, true, true),
            PlacedRow.Placed("probe", TierMover, true, true),
            PlacedRow.Placed("projectile", TierMover, true, true),
            PlacedRow.Placed("pin", TierMover, true, true),
            PlacedRow.Placed("marker", TierMarker, true, true),
            PlacedRow.Placed("bookmark", TierBookmark, true, true),
            // The two groupings. Both are keyed under the same head as the systems they gather, and
            // neither is anywhere the player can be standing.
            PlacedRow.Grouping("constellation"),
            PlacedRow.Grouping("unexplored"),
        };

        /// <summary>Every declaration, in the order they are written.</summary>
        public static IList<PlacedRow> All
        {
            get { return new List<PlacedRow>(Table); }
        }

        /// <summary>What a structural key says its row IS, or null for a key this table does not
        /// name - a row carried by an ancestor, or a kind nobody has declared yet.</summary>
        public static PlacedRow Of(object structuralKey)
        {
            return Named(SegmentOf(structuralKey));
        }

        /// <summary>The declaration for a segment, or null.</summary>
        public static PlacedRow Named(string segment)
        {
            if (segment == null)
            {
                return null;
            }

            for (int i = 0; i < Table.Length; i++)
            {
                if (string.Equals(Table[i].Segment, segment, StringComparison.Ordinal))
                {
                    return Table[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The word a structural key names its row with.
        ///
        /// A key is a path of <c>name/id</c> pairs under a stop - <c>galaxy:constellation/1/system/162</c>
        /// - so the LAST token names the row where it is a word, and the one before it where the last
        /// token is the id. That covers both shapes the map builds: <c>.../bookmark/1</c> is a
        /// <c>bookmark</c>, and <c>galaxy:constellation/unexplored</c> is the <c>unexplored</c> bucket
        /// rather than another constellation.
        ///
        /// The stop's own name is stripped off the head, so <c>galaxy:probe/1621</c> is a
        /// <c>probe</c> and not a <c>galaxy:probe</c>.
        /// </summary>
        public static string SegmentOf(object structuralKey)
        {
            string path = structuralKey as string;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            int colon = path.IndexOf(':');
            if (colon >= 0)
            {
                path = path.Substring(colon + 1);
            }

            string[] parts = path.Split('/');
            if (parts.Length == 0)
            {
                return null;
            }

            string last = parts[parts.Length - 1];
            if (last.Length > 0 && !IsNumber(last))
            {
                return last;
            }

            return parts.Length >= 2 ? parts[parts.Length - 2] : null;
        }

        private static bool IsNumber(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9')
                {
                    return false;
                }
            }

            return text.Length > 0;
        }
    }
}
