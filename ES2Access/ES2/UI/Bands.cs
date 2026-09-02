using System;

namespace ES2Access.ES2.UI
{
    /// <summary>How much of a kind the map is worth saying at a distance.</summary>
    public enum BandFidelity
    {
        /// <summary>The map draws nothing of this kind here, so the tree offers nothing.</summary>
        None = 0,

        /// <summary>What the picture NAMES and nothing else - a system's name and whose it is, the
        /// bar the map paints it on.</summary>
        Name = 1,

        /// <summary>A planet as the map's own dot: its name and its status, which is the whole of
        /// what the drawn circle and its hover carry.</summary>
        Dot = 2,

        /// <summary>Everything the mod reads of the kind - dossiers, children, actions.</summary>
        Full = 3,
    }

    /// <summary>The kinds of thing the map stop puts rows in. One entry per family of rows the
    /// galaxy tree builds, which is what makes this table the single place a band decision is
    /// written down.</summary>
    public enum BandKind
    {
        Constellations = 0,
        Systems = 1,
        Lanes = 2,
        Fleets = 3,
        Planets = 4,

        /// <summary>The empires the diplomacy lens draws - a kind that exists in the scan view and
        /// nowhere else, because nothing on the ordinary map is a row about an empire.</summary>
        Empires = 5,

        /// <summary>The things the picture draws out BETWEEN the stars: a probe on its way, an
        /// obliterator missile in flight, an ally's pin, a quest marker planted in open space. In the
        /// ordinary map they arrive with the full nameplate they are drawn beside; under a scan lens
        /// the game hides every one of their windows, at every lens
        /// (<c>GuiManager</c> :1555-1567), which is a fact the table has to be able to say in its own
        /// right - "the planets are dots" is not the same question, and standing in for it made the
        /// inspect cell name a probe under a lens that draws none.</summary>
        OpenSpace = 6,
    }

    /// <summary>Which scan overlay a spoken level sits under. <see cref="None"/> is the ordinary map:
    /// no lens is up.</summary>
    public enum ScanLens
    {
        None = 0,
        Diplomacy = 1,
        Trade = 2,
        Economy = 3,
        System = 4,

        /// <summary>The system page's own overlay (rung 14) and a planet's (rung 15). Neither draws a
        /// galaxy, so neither contributes map rows.</summary>
        SystemManagement = 5,
        Planet = 6,
    }

    /// <summary>
    /// WHAT THE MAP IS DRAWING AT EACH DISTANCE, as data.
    ///
    /// The galaxy is one page read at fifteen distances, and the game draws a different picture at
    /// each: from far off pure art with the stretches of sky named over it, closer the system names on
    /// owner-coloured bars, closer still fleet lozenges, then full nameplates with planet dots, and at
    /// the bottom of the ladder per-planet orbital cards. A tree that offers the same rows at every
    /// distance tells the player things the picture is not showing them, and one that offers fewer
    /// hides things it is - so which KINDS a band carries, and at what FIDELITY, is a fact about the
    /// game's own rendering rather than a preference, and it belongs in one table that the tree, the
    /// scanner, the inspect cursor and the zoom announcement all read.
    ///
    /// The levels here are the ones the player HEARS (1-15), not the camera steps the game counts
    /// (0-14). The scan lenses are bands of the same ladder: a lens is not chosen, it is what the
    /// overlay shows at that distance (<see cref="LensAt"/>).
    ///
    /// Engine-free on purpose: the table is the design, and a design is worth testing without a game
    /// running.
    /// </summary>
    public static class Bands
    {
        /// <summary>The first and last levels the player can be told they are at.</summary>
        public const int FirstLevel = 1;

        public const int LastLevel = 15;

        /// <summary>How many columns a row of the table has - one per <see cref="BandKind"/>, counted
        /// from the enum so that adding a kind cannot leave the table reading a column short.</summary>
        private static readonly int Kinds = Enum.GetValues(typeof(BandKind)).Length;

        // One row per level, one column per BandKind, in that enum's order.
        //
        // Normal view, measured: 1-2 the game draws pure art with the constellation names over it;
        // 3-4 adds system names on owner bars; 5-6 adds fleet lozenges; 7-12 is the full nameplate
        // with planet DOTS and nothing changes across those six but culling; 13 replaces the
        // nameplates with per-planet orbital cards, which is where a planet becomes a full reading.
        // 14-15 are the system and planet pages - the tree is left as 13 had it rather than re-gated,
        // because the player has gone INTO a place rather than further from the galaxy.
        private static readonly BandFidelity[][] Normal = Ladder(
            new Band[]
            {
                Row(1, 2, BandFidelity.Full, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None),
                Row(3, 4, BandFidelity.Full, BandFidelity.Name, BandFidelity.Full, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None),
                Row(5, 6, BandFidelity.Full, BandFidelity.Name, BandFidelity.Full, BandFidelity.Full, BandFidelity.None, BandFidelity.None, BandFidelity.None),
                Row(7, 12, BandFidelity.Full, BandFidelity.Full, BandFidelity.Full, BandFidelity.Full, BandFidelity.Dot, BandFidelity.None, BandFidelity.Full),
                Row(13, 15, BandFidelity.Full, BandFidelity.Full, BandFidelity.Full, BandFidelity.Full, BandFidelity.Full, BandFidelity.None, BandFidelity.Full),
            }
        );

        // The same ladder under a scan overlay. The constellation window is hidden at every lens
        // (measured), so the in-mode tree is flat system rows and Constellations is None throughout.
        // Diplomacy draws no systems at all - it draws EMPIRES, their centres and their spokes.
        // Trade and Economy draw the same labels (Economy simply drops Trade's importance filter),
        // so they carry the same kinds; System turns the node labels off and leaves discs and lanes,
        // which is a name and nothing more.
        private static readonly BandFidelity[][] Scan = Ladder(
            new Band[]
            {
                Row(1, 2, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.Full, BandFidelity.None),
                Row(3, 10, BandFidelity.None, BandFidelity.Full, BandFidelity.Full, BandFidelity.None, BandFidelity.Dot, BandFidelity.None, BandFidelity.None),
                Row(11, 13, BandFidelity.None, BandFidelity.Name, BandFidelity.Full, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None),
                Row(14, 15, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None, BandFidelity.None),
            }
        );

        // The lens each level sits under, indexed the same way.
        private static readonly ScanLens[] Lenses = LensLadder();

        // The scanner's categories per band, as the keys a saved selector is written in
        // (ScannerKeys). Normal view: nothing is near enough to list until the map names the systems
        // (3), fleets arrive with the lozenges (5), and everything else waits for the full nameplate
        // and its planet dots (7). Under a lens the categories follow what the lens itself draws -
        // and CURIOSITIES appear nowhere in scan, because the scan dot prefab does not wire the
        // curiosity circle (measured live: the field is null; the normal-view dot prefab does wire
        // it).
        private static readonly int[] ScannedFromInNormal = NormalScannerLevels();

        /// <summary>What the band at <paramref name="level"/> offers of <paramref name="kind"/>, in
        /// the ordinary map (<paramref name="scanning"/> false) or under whichever lens that level
        /// carries. A level off either end of the ladder answers as its nearest band.</summary>
        public static BandFidelity Shows(int level, bool scanning, BandKind kind)
        {
            BandFidelity[] row = (scanning ? Scan : Normal)[Clamp(level) - FirstLevel];
            int column = (int)kind;
            return column < 0 || column >= Kinds ? BandFidelity.None : row[column];
        }

        /// <summary>Whether the scanner lists <paramref name="categoryKey"/> - a
        /// <see cref="ScannerKeys"/> category - at this band. A category the picture is not drawing is
        /// not a short list, it is a list of things the player has not been shown.</summary>
        public static bool Scans(int level, bool scanning, string categoryKey)
        {
            int category = ScannerKeys.Category(categoryKey);
            if (category < 0)
            {
                return false;
            }

            int at = Clamp(level);
            if (!scanning)
            {
                return at >= ScannedFromInNormal[category];
            }

            switch (Lenses[at - FirstLevel])
            {
                case ScanLens.Trade:
                    return categoryKey == ScannerKeys.Systems
                        || categoryKey == ScannerKeys.Colonizable
                        || categoryKey == ScannerKeys.Unexplored;
                case ScanLens.Economy:
                    return categoryKey == ScannerKeys.Systems
                        || categoryKey == ScannerKeys.Colonizable
                        || categoryKey == ScannerKeys.Unexplored
                        // PROVISIONAL: contest rendering is unmeasured anywhere, and the economy lens
                        // is where a contested circle would be drawn if it is drawn at all.
                        || categoryKey == ScannerKeys.Contested;
                case ScanLens.System:
                    return categoryKey == ScannerKeys.Systems
                        || categoryKey == ScannerKeys.Unexplored;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The nearest-out level at which the picture holds <paramref name="kind"/> at
        /// <paramref name="least"/> or better - the MINIMUM BAND a thing of that kind needs before the
        /// tree has a row for it and the player can be put on one.
        ///
        /// This is what a snap landing forces (owner ruling 2026-09-01). Being sent to a place is a
        /// promise that the player arrives standing on it, and a band that draws nothing of the kind
        /// has no row to stand on - so the landing takes the camera to the nearest distance at which
        /// the thing it is about is part of the picture, and no closer. "No closer" is the whole point
        /// of asking the table rather than writing a number down: a fleet needs the lozenges and
        /// nothing more, a planet's own card needs the orbital view, and the two answers move with the
        /// table if it is ever re-cut.
        ///
        /// <see cref="BandFidelity.None"/> asks for nothing and answers <see cref="FirstLevel"/>. -1
        /// where no level of the ladder shows that much of the kind, which is the caller's signal that
        /// there is no band to force.
        /// </summary>
        public static int LowestLevel(BandKind kind, bool scanning, BandFidelity least)
        {
            if (least == BandFidelity.None)
            {
                return FirstLevel;
            }

            for (int level = FirstLevel; level <= LastLevel; level++)
            {
                if (Shows(level, scanning, kind) >= least)
                {
                    return level;
                }
            }

            return -1;
        }

        /// <summary>
        /// Whether two levels draw the same picture as far as this table is concerned - every kind at
        /// the same fidelity.
        ///
        /// The question a tree asks itself on the build after the camera moved: a step WITHIN a band
        /// changes nothing it offers, and a step ACROSS one takes whole families of rows away, which
        /// is a different thing to recover a cursor from than one row going out of existence.
        /// </summary>
        public static bool SameShape(int left, int right, bool scanning)
        {
            BandFidelity[][] ladder = scanning ? Scan : Normal;
            return ReferenceEquals(
                ladder[Clamp(left) - FirstLevel],
                ladder[Clamp(right) - FirstLevel]
            );
        }

        /// <summary>Which overlay a spoken level shows while the scan view is up. Never
        /// <see cref="ScanLens.None"/>: every rung of the ladder has a lens under it, and the ordinary
        /// map is said by passing <c>scanning: false</c> to the queries above rather than by asking
        /// this.</summary>
        public static ScanLens LensAt(int level)
        {
            return Lenses[Clamp(level) - FirstLevel];
        }

        private static int Clamp(int level)
        {
            return level < FirstLevel ? FirstLevel : (level > LastLevel ? LastLevel : level);
        }

        // ---- the tables, written as bands and expanded to one row per level ----

        /// <summary>One band of the ladder: the levels it covers and what it offers of each kind, in
        /// <see cref="BandKind"/> order.</summary>
        private sealed class Band
        {
            public readonly int From;

            public readonly int To;

            public readonly BandFidelity[] Fidelities;

            public Band(int from, int to, params BandFidelity[] fidelities)
            {
                From = from;
                To = to;
                Fidelities = fidelities;
            }
        }

        private static Band Row(
            int from,
            int to,
            BandFidelity constellations,
            BandFidelity systems,
            BandFidelity lanes,
            BandFidelity fleets,
            BandFidelity planets,
            BandFidelity empires,
            BandFidelity openSpace
        )
        {
            return new Band(
                from,
                to,
                constellations,
                systems,
                lanes,
                fleets,
                planets,
                empires,
                openSpace
            );
        }

        private static BandFidelity[][] Ladder(Band[] bands)
        {
            BandFidelity[][] levels = new BandFidelity[LastLevel - FirstLevel + 1][];
            for (int i = 0; i < bands.Length; i++)
            {
                Band band = bands[i];
                for (int level = band.From; level <= band.To; level++)
                {
                    levels[level - FirstLevel] = band.Fidelities;
                }
            }

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null)
                {
                    throw new System.InvalidOperationException(
                        "Bands: level " + (i + FirstLevel) + " is in no band"
                    );
                }
            }

            return levels;
        }

        private static ScanLens[] LensLadder()
        {
            ScanLens[] lenses = new ScanLens[LastLevel - FirstLevel + 1];
            for (int level = FirstLevel; level <= LastLevel; level++)
            {
                ScanLens lens;
                if (level <= 2)
                {
                    lens = ScanLens.Diplomacy;
                }
                else if (level <= 6)
                {
                    lens = ScanLens.Trade;
                }
                else if (level <= 10)
                {
                    lens = ScanLens.Economy;
                }
                else if (level <= 13)
                {
                    lens = ScanLens.System;
                }
                else if (level == 14)
                {
                    lens = ScanLens.SystemManagement;
                }
                else
                {
                    lens = ScanLens.Planet;
                }

                lenses[level - FirstLevel] = lens;
            }

            return lenses;
        }

        private static int[] NormalScannerLevels()
        {
            int[] from = new int[ScannerKeys.Categories.Length];
            for (int i = 0; i < from.Length; i++)
            {
                string key = ScannerKeys.Categories[i];
                if (key == ScannerKeys.Systems || key == ScannerKeys.Unexplored)
                {
                    // The map names the systems at 3, and an unexplored lane is a property of a named
                    // system's own lanes.
                    from[i] = 3;
                }
                else if (key == ScannerKeys.Fleets)
                {
                    from[i] = 5;
                }
                else
                {
                    // Everything else is drawn as part of the full nameplate or beside it - deposit
                    // icons, planet dots and what hangs off them, the things out in open space.
                    from[i] = 7;
                }
            }

            return from;
        }
    }
}
