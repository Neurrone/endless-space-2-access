using ES2Access.Core.Map;

namespace ES2Access.Tests.Map
{
    /// <summary>Map fixtures written as flat coordinate lists, which is how a constellation reads on
    /// the page: <c>Places(0, 0, 10, 0, 10, 10)</c> is three stars, not nine lines of construction.
    /// </summary>
    internal static class MapFixtures
    {
        public static MapPoint[] Places(params double[] coordinates)
        {
            MapPoint[] places = new MapPoint[coordinates.Length / 2];
            for (int i = 0; i < places.Length; i++)
            {
                places[i] = new MapPoint(coordinates[2 * i], coordinates[2 * i + 1]);
            }

            return places;
        }

        public static ConvexHull Hull(params double[] coordinates)
        {
            return ConvexHull.Build(Places(coordinates));
        }
    }
}
