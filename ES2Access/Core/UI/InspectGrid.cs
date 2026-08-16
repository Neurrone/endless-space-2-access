namespace ES2Access.Core.UI
{
    /// <summary>
    /// The geometry of the map's inspect cursor: a square cell of whole galaxy units, centred on a
    /// whole-unit coordinate pair, which the player moves a cell at a time to hear what is inside it.
    ///
    /// Everything here is engine-free arithmetic on the map's own units, which is what makes the two
    /// rules that decide what the player hears testable off the game:
    ///
    /// - SIZES ARE ODD (1, 3, 5, 7, 9, 11). An odd square has a centre square, so the cell's centre is
    ///   a whole coordinate pair and the pair the player is told is a place they can hold against every
    ///   other pair the mod says. An even size would centre on a half unit and every announcement would
    ///   carry a fraction nothing else in the game ever says.
    /// - THE CELL IS HALF-OPEN, and a move steps by exactly the cursor's size. Together those make the
    ///   cells TILE: every point in the galaxy belongs to exactly one cell of a given size, so a sweep
    ///   with the arrows can neither skip a system nor hear one twice. A closed cell would report a
    ///   star sitting on a boundary from both sides of it, which reads as two stars.
    ///
    /// Distances are doubles because the caller hands in positions already measured from the empire's
    /// home system (<see cref="ES2Access.Core.Speech.MapCoordinates"/>), and that subtraction is done
    /// before any rounding.
    /// </summary>
    public static class InspectGrid
    {
        /// <summary>One galaxy unit across - the cursor pinned to a single square.</summary>
        public const int SmallestSize = 1;

        /// <summary>The widest sweep, eleven units across. Past this a cell holds so much that the
        /// reading stops being a place and becomes a list.</summary>
        public const int LargestSize = 11;

        /// <summary>What the cursor is when the player has never resized it.</summary>
        public const int DefaultSize = 3;

        /// <summary>The next size up, or the same size at the top of the ladder.</summary>
        public static int Grow(int size)
        {
            return size >= LargestSize ? LargestSize : Clamp(size) + 2;
        }

        /// <summary>The next size down, or the same size at the bottom.</summary>
        public static int Shrink(int size)
        {
            return size <= SmallestSize ? SmallestSize : Clamp(size) - 2;
        }

        /// <summary>The nearest legal size to <paramref name="size"/> - odd, and within the ladder.
        /// </summary>
        public static int Clamp(int size)
        {
            if (size <= SmallestSize)
            {
                return SmallestSize;
            }

            if (size >= LargestSize)
            {
                return LargestSize;
            }

            return (size % 2) == 0 ? size - 1 : size;
        }

        /// <summary>The cell's western/southern edge, which belongs to the cell.</summary>
        public static double Low(int centre, int size)
        {
            return centre - Clamp(size) / 2.0;
        }

        /// <summary>The cell's eastern/northern edge, which belongs to the NEXT cell - the half-open
        /// end that makes the cells tile.</summary>
        public static double High(int centre, int size)
        {
            return centre + Clamp(size) / 2.0;
        }

        /// <summary>Whether a point is in this cell, on the half-open rule: a point exactly on the low
        /// edge is in, one exactly on the high edge belongs to the neighbour.</summary>
        public static bool Holds(int centreX, int centreY, int size, double x, double y)
        {
            return x >= Low(centreX, size)
                && x < High(centreX, size)
                && y >= Low(centreY, size)
                && y < High(centreY, size);
        }

        /// <summary>Where the cursor lands after one press of an arrow: a whole cell along, so the
        /// cells the player walks through tile the map with no gap and no overlap.</summary>
        public static int Step(int centre, int size, int direction)
        {
            return centre + direction * Clamp(size);
        }

        /// <summary>Whether a straight line between two points passes through the cell - the question a
        /// starlane asks, since a lane can cross a cell holding neither of its ends.
        ///
        /// Liang-Barsky against the CLOSED rectangle, deliberately: a line is not a thing that can be
        /// double-counted the way a star can, and a lane running exactly along a boundary really does
        /// touch both cells.</summary>
        public static bool Crosses(
            int centreX,
            int centreY,
            int size,
            double x0,
            double y0,
            double x1,
            double y1
        )
        {
            double lowX = Low(centreX, size);
            double highX = High(centreX, size);
            double lowY = Low(centreY, size);
            double highY = High(centreY, size);
            double dx = x1 - x0;
            double dy = y1 - y0;
            double enter = 0.0;
            double leave = 1.0;
            return Clip(-dx, x0 - lowX, ref enter, ref leave)
                && Clip(dx, highX - x0, ref enter, ref leave)
                && Clip(-dy, y0 - lowY, ref enter, ref leave)
                && Clip(dy, highY - y0, ref enter, ref leave);
        }

        /// <summary>One slab of the clip: false the moment the segment is wholly outside it.</summary>
        private static bool Clip(double p, double q, ref double enter, ref double leave)
        {
            if (p == 0.0)
            {
                return q >= 0.0;
            }

            double at = q / p;
            if (p < 0.0)
            {
                if (at > leave)
                {
                    return false;
                }

                if (at > enter)
                {
                    enter = at;
                }

                return true;
            }

            if (at < enter)
            {
                return false;
            }

            if (at < leave)
            {
                leave = at;
            }

            return true;
        }

        /// <summary>Which end of a line is named FIRST - the westmost, and where two ends stand at the
        /// same longitude the southern one. A lane is one thing whichever end the reading reached it
        /// from, so it has to be said the same way round every time or the same lane heard from two
        /// cells sounds like two lanes.</summary>
        public static bool WestmostFirst(double ax, double ay, double bx, double by)
        {
            return ax != bx ? ax < bx : ay <= by;
        }

        /// <summary>Whether a cell centre is still inside the galaxy - the test a move makes before it
        /// happens, so the player is told they are at the edge rather than being taken out into
        /// nothing.</summary>
        public static bool InBounds(
            int x,
            int y,
            double lowX,
            double highX,
            double lowY,
            double highY
        )
        {
            return x >= lowX && x <= highX && y >= lowY && y <= highY;
        }

        /// <summary>The whole-unit squares a cell covers, as offsets from its centre: -n..n where the
        /// cursor is 2n+1 across. What a per-square question (is this square under fog?) is asked
        /// over.</summary>
        public static int HalfWidth(int size)
        {
            return Clamp(size) / 2;
        }

        /// <summary>How many whole-unit squares the cell covers.</summary>
        public static int Squares(int size)
        {
            int side = Clamp(size);
            return side * side;
        }
    }
}
