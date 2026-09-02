using System;
using System.Collections.Generic;

namespace ES2Access.ES2.UI
{
    /// <summary>
    /// How much of a cell is under the fog - the one part of a cell's identity that is not a thing
    /// with a name, and the one a sighted player reads from an absence rather than from a drawing.
    /// Three states and not a count: a sweep along the edge of what the empire has explored crosses
    /// cells whose fogged-square counts differ by one all the way, and a signature carrying the count
    /// would stop the cursor on every one of them.
    /// </summary>
    public enum CellFog
    {
        Clear,
        Partly,
        Wholly,
    }

    /// <summary>
    /// WHAT IS IN A CELL, reduced to the one thing a skip can compare: the identity of everything the
    /// cell's reading names, plus how much of it is fogged.
    ///
    /// Identities and not descriptions, because the question the skip answers is "is this the same
    /// stretch of map as the one I am standing on" - two different stars with the same name are two
    /// stops, and one star heard from two neighbouring cells is one stop. Coordinates are deliberately
    /// NOT part of it: every cell has its own pair, so a signature carrying it would differ from its
    /// neighbour's always and the key would degenerate into an ordinary arrow.
    ///
    /// The set is unordered - the caller hands in the tokens in whatever order it gathered them and
    /// they are sorted here - so a lane that changes which end names it first cannot read as a change.
    /// </summary>
    public sealed class CellSignature : IEquatable<CellSignature>
    {
        private readonly string[] _things;

        public CellSignature(IList<string> things, CellFog fog)
        {
            Fog = fog;
            if (things == null || things.Count == 0)
            {
                _things = new string[0];
                return;
            }

            List<string> copy = new List<string>(things.Count);
            for (int i = 0; i < things.Count; i++)
            {
                if (!string.IsNullOrEmpty(things[i]))
                {
                    copy.Add(things[i]);
                }
            }

            copy.Sort(StringComparer.Ordinal);
            _things = copy.ToArray();
        }

        public CellFog Fog { get; private set; }

        /// <summary>How many things the cell names - the count the identity set was built from.
        /// </summary>
        public int Count
        {
            get { return _things.Length; }
        }

        public bool Equals(CellSignature other)
        {
            if (other == null || Fog != other.Fog || _things.Length != other._things.Length)
            {
                return false;
            }

            for (int i = 0; i < _things.Length; i++)
            {
                if (!string.Equals(_things[i], other._things[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CellSignature);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)Fog;
                for (int i = 0; i < _things.Length; i++)
                {
                    hash = hash * 31 + _things[i].GetHashCode();
                }

                return hash;
            }
        }
    }

    /// <summary>
    /// MOVE TO THE NEXT INTERESTING CELL: the walk behind the modified arrows, which cross a stretch
    /// of map that is all the same in one press instead of one cell per press.
    ///
    /// The rule is the one thing that makes it predictable: the cursor stops at the first cell that is
    /// NOT what the player is standing on. It is compared against the ORIGIN and never against the
    /// cell before it, so a run of empty cells is one stretch however long it is, and a cell holding
    /// the very star the cursor started on is not a stop - which is what lets a big cursor step off
    /// the far side of a system it overlaps rather than stopping on its own contents.
    ///
    /// Running out of the galaxy is not a refusal: the walk lands on the last cell that was still on
    /// the map, so the key sweeps to the edge and stops there. It refuses only when not even one step
    /// is possible, which is the same answer the plain arrow gives in that position.
    /// (Modelled on songs-of-conquest-access's TileSkipNavigator, the same rule for a square map.)
    /// </summary>
    public static class CellSkip
    {
        /// <summary>
        /// Where a skip in <paramref name="east"/>/<paramref name="north"/> lands, and how many
        /// matching cells it passed over on the way. False when the first step is already off the map
        /// - the caller says what an arrow at the edge says.
        /// </summary>
        public static bool Find(
            int x,
            int y,
            int size,
            int east,
            int north,
            Func<int, int, bool> inBounds,
            Func<int, int, CellSignature> signatureAt,
            out int toX,
            out int toY,
            out int skipped
        )
        {
            toX = x;
            toY = y;
            skipped = 0;
            if (inBounds == null || signatureAt == null || (east == 0 && north == 0))
            {
                return false;
            }

            CellSignature origin = signatureAt(x, y);
            int candidateX = InspectGrid.Step(x, size, east);
            int candidateY = InspectGrid.Step(y, size, north);
            int lastX = x;
            int lastY = y;
            int matching = 0;
            while (inBounds(candidateX, candidateY))
            {
                CellSignature here = signatureAt(candidateX, candidateY);
                if (!Same(origin, here))
                {
                    toX = candidateX;
                    toY = candidateY;
                    skipped = matching;
                    return true;
                }

                matching++;
                lastX = candidateX;
                lastY = candidateY;
                candidateX = InspectGrid.Step(candidateX, size, east);
                candidateY = InspectGrid.Step(candidateY, size, north);
            }

            if (matching == 0)
            {
                return false;
            }

            // The map ran out with nothing different found. The cursor goes as far as it could, and
            // the cell it lands on is not one of the ones it skipped OVER - hence the one less.
            toX = lastX;
            toY = lastY;
            skipped = matching - 1;
            return true;
        }

        private static bool Same(CellSignature one, CellSignature two)
        {
            return one == null ? two == null : one.Equals(two);
        }
    }
}
