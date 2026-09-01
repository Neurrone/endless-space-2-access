using ES2Access.Core.UI;

namespace ES2Access.UI
{
    /// <summary>
    /// What the map is DRAWING right now, asked of the game and answered out of
    /// <see cref="Bands"/>.
    ///
    /// The table is the design and is engine-free; this is the two readings it needs from a running
    /// game - how far out the camera is, and whether a scan lens is over it - turned into the one
    /// question every consumer asks: is this kind of thing part of the picture at the moment, and how
    /// much of it. Nothing is remembered: both readings are the game's own fields, kept up to date on
    /// its schedule, so a cached copy could only be a stale one.
    ///
    /// The spoken LEVEL is the rung the player hears (<c>GalaxyViewLevels.ZoomRung</c> counts from
    /// zero, and every number the mod says counts from one), so the table and the announcement can
    /// never be off by one from each other.
    ///
    /// Where there is no rung to read - a battle, the system-discovery view, no game at all - every
    /// question answers as though the whole picture were being drawn. A band filter exists to withhold
    /// what the player is not being shown; a filter that cannot tell what is being shown must withhold
    /// nothing.
    /// </summary>
    public static class ZoomBands
    {
        /// <summary>The rung the player hears, 1-15, or -1 where the question has no answer.</summary>
        public static int Level
        {
            get
            {
                int rung = GalaxyViewLevels.ZoomRung;
                return rung < 0 ? -1 : rung + 1;
            }
        }

        /// <summary>Whether the game's own scan overlay is up, which makes the ladder a ladder of
        /// LENSES rather than of distances.</summary>
        public static bool Scanning
        {
            get { return GalaxyViewLevels.Scanning; }
        }

        /// <summary>How much of <paramref name="kind"/> the picture is worth saying at this
        /// distance.</summary>
        public static BandFidelity Fidelity(BandKind kind)
        {
            int level = Level;
            return level < 0 ? BandFidelity.Full : Bands.Shows(level, Scanning, kind);
        }

        /// <summary>Whether the picture holds this kind of thing at all here.</summary>
        public static bool Shows(BandKind kind)
        {
            return Fidelity(kind) != BandFidelity.None;
        }

        /// <summary>
        /// Whether the map is drawing a system in FULL - the nameplate with its planet dots, and with
        /// it everything the picture only draws beside a nameplate: the docks and hangars, the probes
        /// and missiles out in the open, the quest pins, the deposit icons, the buttons on the label.
        ///
        /// Read off the planets' own band rather than written down as a number, because "the planet
        /// dots are drawn" is exactly the boundary the game crosses when the label stops being a name
        /// on a bar and becomes a nameplate.
        /// </summary>
        public static bool MapDetail
        {
            get { return Shows(BandKind.Planets); }
        }

        /// <summary>Whether the scanner lists a category here - the same table, so the list the player
        /// walks and the tree they browse cannot disagree about what the map is showing.</summary>
        public static bool Scans(string categoryKey)
        {
            int level = Level;
            return level < 0 || Bands.Scans(level, Scanning, categoryKey);
        }
    }
}
