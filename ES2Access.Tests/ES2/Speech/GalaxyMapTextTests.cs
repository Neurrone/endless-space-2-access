using ES2Access.Core.Map;
using ES2Access.Core.Speech;
using ES2Access.ES2.Speech;
using ES2Access.Tests.Speech;
using Xunit;

namespace ES2Access.Tests.ES2.Speech
{
    /// <summary>
    /// The one sentence that gives a listener the scale of the galaxy every later distance is
    /// measured against.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class GalaxyMapTextTests
    {
        public GalaxyMapTextTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void TheGalaxyIsSaidAsItsShapeItsSizeItsSpanAndWhereItsMiddleIs()
        {
            Assert.Equal(
                "Spiral 2 galaxy, normal size, 187 by 130 units; galactic center 71 west, 23 north of Dusay.",
                GalaxyMapText.Summary("Spiral 2", "normal", 187, 130, "71 west, 23 north", "Dusay")
            );
        }

        [Fact]
        public void MeasuredOffTheGalaxysOwnOutlineTheOffsetIsSaidEastWestFirst()
        {
            // Every offset the mod speaks leads with the east/west component (CompassDirections
            // .Offsets), and this one is composed from it rather than being punctuated here.
            ConvexHull galaxy = ConvexHull.Build(
                new[]
                {
                    new MapPoint(0, 0),
                    new MapPoint(187, 0),
                    new MapPoint(187, 130),
                    new MapPoint(0, 130),
                }
            );

            Assert.Equal(
                "Spiral 2 galaxy, normal size, 187 by 130 units; "
                    + "galactic center 71 west, 23 north of Dusay.",
                GalaxyMapText.Summary(
                    "Spiral 2",
                    "normal",
                    galaxy,
                    new MapPoint(164.5, 42),
                    "Dusay"
                )
            );
        }

        [Fact]
        public void TheSpanIsRoundedToWholeUnits()
        {
            ConvexHull galaxy = ConvexHull.Build(
                new[]
                {
                    new MapPoint(-0.4, -0.5),
                    new MapPoint(186.2, -0.5),
                    new MapPoint(186.2, 129.6),
                    new MapPoint(-0.4, 129.6),
                }
            );

            Assert.Equal(187, MapCoordinates.Round(galaxy.Width));
            Assert.Equal(130, MapCoordinates.Round(galaxy.Height));
            Assert.Contains(
                "187 by 130 units",
                GalaxyMapText.Summary("Spiral 2", "normal", galaxy, new MapPoint(0, 0), "Dusay")
            );
        }

        [Fact]
        public void AHomeSystemStandingOnTheGalacticCentreIsNotSaidAtAll()
        {
            // Both components round to zero, so there is no offset left to say and the sentence would
            // end on a blank. No wording has been chosen for a galaxy centred on home, so the whole
            // sentence is withheld rather than half of one being spoken.
            ConvexHull galaxy = ConvexHull.Build(
                new[]
                {
                    new MapPoint(-10, -10),
                    new MapPoint(10, -10),
                    new MapPoint(10, 10),
                    new MapPoint(-10, 10),
                }
            );

            Assert.Null(
                GalaxyMapText.Summary("Spiral 2", "small", galaxy, new MapPoint(0, 0), "Dusay")
            );
        }
    }
}
