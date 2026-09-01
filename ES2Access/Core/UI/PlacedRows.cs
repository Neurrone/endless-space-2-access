using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;

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
    ///
    /// THE FIFTH QUESTION IS IDENTITY, and it is asked of the kinds that MOVE. A row's structural key
    /// says where in the tree it hangs, so a thing that travels gets a NEW key the moment it re-files -
    /// a fleet leaving its berth is keyed under the system it is heading for, and a star's own key
    /// changes the turn the player learns which constellation it is in. The cursor standing on that row
    /// is reconciled by key, so a key that changed reads as a row that DIED and the player is put back
    /// on a neighbour. <see cref="Anchored"/> is the answer: the row's id carries the game entity as
    /// its <c>Subject</c>, and tier-1 reconciliation follows the object to wherever its new key is
    /// (<c>ControlId</c>, <c>KeyGraph.Reconcile</c>).
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

        /// <summary>Whether this row's id must ANCHOR ON THE ENTITY it is about - carry it as the
        /// <c>Subject</c> - so that the cursor follows the thing when its structural key changes under
        /// it. True for the kinds that travel; false for a row whose key only ever changes because the
        /// row itself was replaced, where following would be following the wrong thing.</summary>
        public readonly bool Anchored;

        private PlacedRow(
            string segment,
            bool arms,
            int enterTier,
            bool leap,
            bool restore,
            bool refuses,
            bool anchored
        )
        {
            Segment = segment;
            Arms = arms;
            EnterTier = enterTier;
            Leap = leap;
            Restore = restore;
            Refuses = refuses;
            Anchored = anchored;
        }

        /// <summary>A row that stands somewhere of its own.</summary>
        public static PlacedRow Placed(
            string segment,
            int enterTier,
            bool leap,
            bool restore,
            bool anchored
        )
        {
            return new PlacedRow(segment, true, enterTier, leap, restore, false, anchored);
        }

        /// <summary>A grouping: never a place, whatever the entity behind it happens to carry. Its key
        /// is a name the map wrote across the sky and nothing travels between two of them, so it is
        /// never anchored - the entity a heading happens to hold is not identity here.</summary>
        public static PlacedRow Grouping(string segment)
        {
            return new PlacedRow(segment, false, 0, false, false, true, false);
        }

        /// <summary>
        /// A row a LENS draws that is not somewhere the player stands: the diplomacy band's battle
        /// line, which is two tinted emblems planted over a node, and nothing the tree can be sent to.
        ///
        /// It answers no to all four on purpose. Whether the inspect cell may be armed from a row at
        /// the two furthest-out rungs is an open owner question (roadmap), and the inert answer is the
        /// one to ship until it is settled - a refusal is silent and costs the player nothing, while
        /// an invented arming would put them on a square nobody chose.
        /// </summary>
        public static PlacedRow Drawing(string segment)
        {
            return new PlacedRow(segment, false, 0, false, false, true, false);
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
            // shape, the same key, and the cell reads them alike. A star does not travel, but its KEY
            // does: the head of it is the constellation, and the turn the player is first shown which
            // stretch of sky it belongs to the whole branch moves out of the unexplored bucket. So it
            // is anchored, and has been since before this column named the reason.
            PlacedRow.Placed("system", TierPlace, true, true, true),
            // The four that TRAVEL. A fleet is re-keyed under the system it is heading for the moment
            // it leaves its berth; a probe, a missile and an ally's pin are keyed at the top of the
            // stop and change nothing as they cross - but they are the same kind of thing, and a key
            // shape is not a promise. All four carry their entity (owner-approved 2026-08-31).
            PlacedRow.Placed("fleet", TierFleet, true, true, true),
            PlacedRow.Placed("probe", TierMover, true, true, true),
            PlacedRow.Placed("projectile", TierMover, true, true, true),
            PlacedRow.Placed("pin", TierMover, true, true, true),
            PlacedRow.Placed("marker", TierMarker, true, true, false),
            // A bookmark is the one row that is the PLAYER'S note rather than a thing in the galaxy,
            // and it is keyed by the slot digit. Deliberately unanchored: when one place, one slot
            // empties a slot, that row is meant to die under the cursor and the restore is meant to
            // find the replacing slot's row - an anchor would be a thing to follow where nothing moved.
            PlacedRow.Placed("bookmark", TierBookmark, true, true, false),
            // The three groupings. None of them is anywhere the player can be standing.
            //
            // The first two are keyed under the same head as the systems they gather. The THIRD is
            // not, and deliberately: an owner heading is what the scan lens groups the map by (owner
            // ruling 2026-09-01), and it stands over stars whose keys are unchanged from the ordinary
            // view - so entering and leaving the mode costs the cursor nothing, since every row it
            // could be standing on is the same row. An empire is not a place either: the position the
            // watching empire's intelligence has for it is where a CIRCLE is drawn, in the same way a
            // constellation's centroid is where its name is written.
            PlacedRow.Grouping("constellation"),
            PlacedRow.Grouping("unexplored"),
            PlacedRow.Grouping("owner"),
            // The one row the diplomacy lens draws that is neither a heading nor a place.
            PlacedRow.Drawing("battle"),
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

        /// <summary>
        /// The id a placed row is declared with: the entity carried as the subject where the table
        /// says this kind is <see cref="PlacedRow.Anchored"/>, and the bare structural key where it
        /// does not. The one place the column is spent, so a kind cannot be declared anchored and then
        /// built without its anchor.
        ///
        /// ACROSS A SAVE AND A LOAD the anchor is worth nothing: the game builds new instances for
        /// everything, so no subject from the session before matches. That is what the GUID in the
        /// structural key is for - tier 2 catches the rebuilt object under the same key, and between
        /// them a row is followed whether it moved or was remade. Neither survives BOTH at once, and
        /// nothing needs it to: focus does not outlive a load.
        ///
        /// A row this table does not name - a segment carried by an ancestor, or one nobody has
        /// declared yet - gets the bare key. A carried row that wants a subject passes
        /// <c>ControlId.For</c> itself; it is not a placed row and this is not its question.
        /// </summary>
        public static ControlId Anchor(object subject, string structuralKey)
        {
            PlacedRow row = Named(SegmentOf(structuralKey));
            return subject != null && row != null && row.Anchored
                ? ControlId.For(subject, structuralKey)
                : ControlId.Structural(structuralKey);
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
