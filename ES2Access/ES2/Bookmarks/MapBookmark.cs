using System.Globalization;

namespace ES2Access.ES2.Bookmarks
{
    /// <summary>
    /// One place on the galaxy map the player put a bookmark on: a star system, or a bare point of
    /// space.
    ///
    /// Both forms carry the REAL galaxy position, never the rounded pair the player hears - that
    /// pair is rendered from this when the bookmark is spoken, so a jump lands exactly where the
    /// set did. A system bookmark carries the system's entity GUID as well and is resolved through
    /// it while the system is still on the map; the position is what remains when it is not.
    /// A GUID of 0 is the point form - the game mints no entity 0.
    /// </summary>
    public struct MapBookmark
    {
        public readonly ulong SystemGuid;
        public readonly float X;
        public readonly float Y;

        public MapBookmark(ulong systemGuid, float x, float y)
        {
            SystemGuid = systemGuid;
            X = x;
            Y = y;
        }

        /// <summary>A bookmark on the system with this GUID, at the position the system sits at.
        /// </summary>
        public static MapBookmark OfSystem(ulong systemGuid, float x, float y)
        {
            return new MapBookmark(systemGuid, x, y);
        }

        /// <summary>A bookmark on a point of space and nothing else - what a fleet's position, or a
        /// cell with no system in it, leaves behind.</summary>
        public static MapBookmark AtPoint(float x, float y)
        {
            return new MapBookmark(0, x, y);
        }

        /// <summary>Whether this bookmark names a system, as opposed to a point of space.</summary>
        public bool IsSystem
        {
            get { return SystemGuid != 0; }
        }

        /// <summary>The bookmark as one settings value - <c>guid,x,y</c>, invariant and
        /// round-trippable, so the file reads back the same numbers whatever the player's
        /// language settings do to decimal points. "G9" rather than "R": nine significant
        /// digits round-trip every float unconditionally, where the old runtime's "R" has a
        /// seven-digit fast path that can land on a neighbouring value.</summary>
        public string ToValue()
        {
            return SystemGuid.ToString(CultureInfo.InvariantCulture)
                + "," + X.ToString("G9", CultureInfo.InvariantCulture)
                + "," + Y.ToString("G9", CultureInfo.InvariantCulture);
        }

        /// <summary>Read one back. Anything that is not exactly a GUID and two finite coordinates -
        /// a truncated line, a number that is not one, a hand-edit gone wrong - answers false, and
        /// the caller drops that ONE bookmark rather than the file.</summary>
        public static bool TryParse(string value, out MapBookmark bookmark)
        {
            bookmark = default(MapBookmark);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split(',');
            ulong systemGuid;
            float x;
            float y;
            if (parts.Length != 3
                || !ulong.TryParse(
                    parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out systemGuid)
                || !TryParseCoordinate(parts[1], out x)
                || !TryParseCoordinate(parts[2], out y))
            {
                return false;
            }

            bookmark = new MapBookmark(systemGuid, x, y);
            return true;
        }

        private static bool TryParseCoordinate(string text, out float coordinate)
        {
            return float.TryParse(
                       text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                       out coordinate)
                   && !float.IsNaN(coordinate)
                   && !float.IsInfinity(coordinate);
        }
    }
}
