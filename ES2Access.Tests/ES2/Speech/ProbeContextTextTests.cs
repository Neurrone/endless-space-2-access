using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.ES2.Map;
using ES2Access.ES2.Speech;
using ES2Access.Tests.Speech;
using Xunit;

namespace ES2Access.Tests.ES2.Speech
{
    /// <summary>
    /// What a player hears on each of the sixteen headings before spending a probe, and what they
    /// then read line by line on the one they stopped on. Every string asserted here was written
    /// down from the live galaxy; the point of holding them is that the failure is inaudible - a
    /// range spoken as a pair when it really runs off the map, or a list whose last item is not
    /// marked as the last, sounds like a perfectly good sentence and sends the probe somewhere else.
    ///
    /// The split itself is asserted too: the announcement carries the share and NOT the ranges, the
    /// buffer carries the share again and then a line per clause. Both halves come from the same
    /// reading, so a change that moved a clause between them would otherwise pass unseen.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class ProbeContextTextTests
    {
        public ProbeContextTextTests()
        {
            ModStrings.Reset();
        }

        /// <summary>A footprint that reads as the given percentage. What is counted into one is
        /// <see cref="ProbeFootprintTests"/>' business; here it is handed in ready-made, because these
        /// are tests of the sentence.</summary>
        private static ProbeFootprint Share(int percent)
        {
            return new ProbeFootprint(percent, 100);
        }

        private static ProbeCorridorReading Reading(int edge, params int[] bounds)
        {
            return new ProbeCorridorReading(edge, Spans(bounds));
        }

        /// <summary>A reading with fog beside the line as well as on it - the bearing given twice
        /// because the sentence names the heading from one and its two sides from the other.</summary>
        private static ProbeCorridorReading Beside(
            double bearing,
            int edge,
            int[] line,
            int[] clockwise,
            int[] counterClockwise
        )
        {
            return new ProbeCorridorReading(
                bearing,
                edge,
                Spans(line),
                Spans(clockwise),
                Spans(counterClockwise)
            );
        }

        private static List<UnexploredSpan> Spans(int[] bounds)
        {
            List<UnexploredSpan> spans = new List<UnexploredSpan>();
            for (int i = 0; bounds != null && i + 1 < bounds.Length; i += 2)
            {
                spans.Add(new UnexploredSpan(bounds[i], bounds[i + 1]));
            }

            return spans;
        }

        [Fact]
        public void SixStretchesOfFogAreAllSaidWithTheLastOneRunningOffTheMap()
        {
            Assert.Equal(
                new[]
                {
                    "35 percent explored",
                    "Unexplored 12-15, 17-18, 19-26, 31-35, 38-44, and 46 to the map edge at 58",
                },
                ProbeContextText.Lines(
                    Reading(58, 12, 15, 17, 18, 19, 26, 31, 35, 38, 44, 46, 58),
                    Share(35)
                )
            );
        }

        [Fact]
        public void OneStretchRunningOffTheMapIsSaidWithoutARangeAtAll()
        {
            Assert.Equal(
                new[] { "40 percent explored", "Unexplored 7 to the map edge at 51" },
                ProbeContextText.Lines(Reading(51, 7, 51), Share(40))
            );
            Assert.Equal(
                new[] { "40 percent explored", "Unexplored 8 to the map edge at 12" },
                ProbeContextText.Lines(Reading(12, 8, 12), Share(40))
            );
        }

        [Fact]
        public void TwoStretchesTakeTheConjunctionWithoutAComma()
        {
            Assert.Equal(
                new[] { "12 percent explored", "Unexplored 7-28 and 32 to the map edge at 42" },
                ProbeContextText.Lines(Reading(42, 7, 28, 32, 42), Share(12))
            );
        }

        [Fact]
        public void FogStoppingShortOfTheMapEdgeStillSaysWhereTheEdgeIs()
        {
            // Otherwise the heading's whole length goes unsaid: "unexplored 12-15" alone could be the
            // start of a long run out of the galaxy or the last thing in reach.
            Assert.Equal(
                new[] { "80 percent explored", "Unexplored 12-15 and 20-25, map edge at 58" },
                ProbeContextText.Lines(Reading(58, 12, 15, 20, 25), Share(80))
            );
            Assert.Equal(
                new[] { "80 percent explored", "Unexplored 12-15, map edge at 58" },
                ProbeContextText.Lines(Reading(58, 12, 15), Share(80))
            );
        }

        [Fact]
        public void AHeadingWithNoFogLeftSaysSoAndStillGivesTheDistance()
        {
            Assert.Equal(
                new[] { "100 percent explored", "Fully explored to the map edge at 34" },
                ProbeContextText.Lines(Reading(34), Share(100))
            );
        }

        [Fact]
        public void AClearFlightLineStillCarriesTheShareItsCorridorWouldFind()
        {
            // The two lines answer different questions - the flight line is what the probe flies
            // through, the share is everything its vision circle would sweep as far as it gets - so a
            // heading with nothing on the line can still be mostly dark, and the share is the only
            // warning of it. Silencing it on "fully explored" would silence exactly the surprising
            // case, and the announcement carries it whether or not the buffer is ever read.
            Assert.Equal(
                new[] { "18 percent explored", "Fully explored to the map edge at 34" },
                ProbeContextText.Lines(Reading(34), Share(18))
            );
            Assert.Equal(
                "Northwest: 18 percent explored.",
                ProbeContextText.Label(315.0, Share(18))
            );
        }

        [Fact]
        public void AHeadingWithNoMapDownItAtAllIsStillASentence()
        {
            Assert.Equal("East: 100 percent explored.", ProbeContextText.Label(90.0, new ProbeFootprint(0, 0)));
            Assert.Equal(
                new[] { "100 percent explored", "Fully explored to the map edge at 0" },
                ProbeContextText.Lines(Reading(0), new ProbeFootprint(0, 0))
            );
        }

        [Fact]
        public void TheAnnouncementIsTheHeadingAndTheShareAndNothingElse()
        {
            // The whole point of the split: the six ranges below are in the buffer and the player
            // walking sixteen bearings never has them read at them.
            Assert.Equal(
                "Southwest: 35 percent explored.",
                ProbeContextText.Label(225.0, Share(35))
            );
        }

        [Fact]
        public void AHalfWindHeadingIsNamedWithItsOwnWord()
        {
            Assert.Equal(
                "North-northeast: 12 percent explored.",
                ProbeContextText.Label(22.5, Share(12))
            );
        }

        [Fact]
        public void TheHeadingWordOpensTheAnnouncementAndIsCapitalizedWhereverItComesFrom()
        {
            Assert.Equal("Southwest: 50 percent explored.", ProbeContextText.Label("southwest", Share(50)));
            Assert.Equal("Southwest: 50 percent explored.", ProbeContextText.Label("Southwest", Share(50)));
        }

        [Fact]
        public void FogRunningBesideAClearFlightLineIsALineOfItsOwnAndNamesTheSide()
        {
            // The live southeast bearing. Said as one corridor it was "unexplored 0 to the map edge at
            // 40" - a heading the player would launch into believing the line ahead was dark.
            Assert.Equal(
                new[]
                {
                    "30 percent explored",
                    "Fully explored to the map edge at 40",
                    "Unexplored alongside to the southwest: 2-40",
                },
                ProbeContextText.Lines(Beside(135.0, 40, null, new[] { 2, 40 }, null), Share(30))
            );
        }

        [Fact]
        public void TheOtherSideOfTheSameHeadingIsNamedTheOtherWay()
        {
            Assert.Equal(
                new[]
                {
                    "30 percent explored",
                    "Fully explored to the map edge at 40",
                    "Unexplored alongside to the northeast: 2-40",
                },
                ProbeContextText.Lines(Beside(135.0, 40, null, null, new[] { 2, 40 }), Share(30))
            );
        }

        [Fact]
        public void TheSameStretchesOnBothSidesAreOneLine()
        {
            // A pocket of known map the width of the line: hearing the identical five numbers twice
            // tells the player nothing the word "both" does not.
            Assert.Equal(
                new[]
                {
                    "77 percent explored",
                    "Fully explored to the map edge at 40",
                    "Unexplored alongside to both sides: 5-9 and 12-14",
                },
                ProbeContextText.Lines(
                    Beside(0.0, 40, null, new[] { 5, 9, 12, 14 }, new[] { 5, 9, 12, 14 }),
                    Share(77)
                )
            );
        }

        [Fact]
        public void TwoSidesWithDifferentStretchesAreTwoLines()
        {
            Assert.Equal(
                new[]
                {
                    "77 percent explored",
                    "Fully explored to the map edge at 40",
                    "Unexplored alongside to the east: 5-9",
                    "Unexplored alongside to the west: 12-14",
                },
                ProbeContextText.Lines(
                    Beside(0.0, 40, null, new[] { 5, 9 }, new[] { 12, 14 }),
                    Share(77)
                )
            );
        }

        [Fact]
        public void FogOnTheLineAndFogBesideItAreBothSaid()
        {
            Assert.Equal(
                new[]
                {
                    "5 percent explored",
                    "Unexplored 12-15 and 20 to the map edge at 58",
                    "Unexplored alongside to the north: 0-12",
                },
                ProbeContextText.Lines(
                    Beside(270.0, 58, new[] { 12, 15, 20, 58 }, new[] { 0, 12 }, null),
                    Share(5)
                )
            );
        }

        [Fact]
        public void AnAlongsideStretchReachingTheRimIsStillJustARange()
        {
            // The map's edge has already been named by the line before; saying it again inside the
            // alongside list makes one number sound like two different rims.
            Assert.Equal(
                new[]
                {
                    "60 percent explored",
                    "Fully explored to the map edge at 40",
                    "Unexplored alongside to the east: 10-40",
                },
                ProbeContextText.Lines(Beside(0.0, 40, null, new[] { 10, 40 }, null), Share(60))
            );
        }

        [Fact]
        public void TheShareIsTheSameWordsInBothDeliveries()
        {
            // The buffer opens on the figure the announcement just gave, so the two are one template
            // and a translation cannot make them disagree.
            Assert.Equal("42 percent explored", ProbeContextText.Percent(Share(42)));
            Assert.Equal("North: 42 percent explored.", ProbeContextText.Label(0.0, Share(42)));
            Assert.Equal("42 percent explored", ProbeContextText.Lines(Reading(51, 7, 51), Share(42))[0]);
        }

        [Fact]
        public void ATranslationRewritesEveryPartOfTheReading()
        {
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.GalaxyProbeContext, "{0} - {1}" },
                    { ModStrings.GalaxyProbeContextPercentExplored, "{0}% bekannt" },
                    { ModStrings.GalaxyProbeContextUnexplored, "Fog at {0}" },
                    { ModStrings.GalaxyProbeContextRange, "{0} bis {1}" },
                    { ModStrings.GalaxyProbeContextToEdge, "{0} onwards, rim {1}" },
                    { ModStrings.GalaxyProbeContextExplored, "Frei bis zum Rand {0}" },
                    { ModStrings.GalaxyProbeContextAlongside, "Nebenan {0}: {1}" },
                    { ModStrings.ListFinal, "{0} und {1}" },
                    { ModStrings.DirectionSouthWest, "südwesten" },
                    { ModStrings.DirectionNorthWest, "nordwesten" },
                }
            );

            Assert.Equal("Südwesten - 35% bekannt", ProbeContextText.Label(225.0, Share(35)));
            Assert.Equal(
                new[]
                {
                    "35% bekannt",
                    "Fog at 12 bis 15, 17 bis 18 und 46 onwards, rim 58",
                },
                ProbeContextText.Lines(Reading(58, 12, 15, 17, 18, 46, 58), Share(35))
            );
            Assert.Equal(
                new[]
                {
                    "35% bekannt",
                    "Frei bis zum Rand 40",
                    "Nebenan nordwesten: 2 bis 40",
                },
                ProbeContextText.Lines(Beside(225.0, 40, null, new[] { 2, 40 }, null), Share(35))
            );
        }
    }
}
