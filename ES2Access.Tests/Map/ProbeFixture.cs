using System;
using ES2Access.Core.Map;

namespace ES2Access.Tests.Map
{
    /// <summary>The galaxy a probe reading is measured in: a wide square with the anchor in the
    /// middle, so a heading out of it has room to run before it meets a rim. Shared by the corridor
    /// and the footprint because the two are readings of the same flight and a fixture that drifted
    /// between them would make their numbers incomparable.</summary>
    internal static class ProbeFixture
    {
        public static readonly MapPoint Home = new MapPoint(0, 0);

        public static ConvexHull Galaxy()
        {
            return Galaxy(100);
        }

        public static ConvexHull Galaxy(double half)
        {
            return ConvexHull.Build(
                new[]
                {
                    new MapPoint(-half, -half),
                    new MapPoint(half, -half),
                    new MapPoint(half, half),
                    new MapPoint(-half, half),
                }
            );
        }

        /// <summary>The fog may only ever be asked about a whole-numbered offset from the anchor: the
        /// tiles are the anchor's own lattice, and a fractional ask is a reading of a tile that does
        /// not exist.</summary>
        public static void Lattice(double offset, string axis)
        {
            if (Math.Abs(offset - Math.Round(offset)) > 1e-9)
            {
                throw new InvalidOperationException(
                    "the fog was asked at " + offset + " " + axis + " of the anchor"
                );
            }
        }
    }
}
