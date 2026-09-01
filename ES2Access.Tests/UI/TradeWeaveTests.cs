using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Tests.Speech;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The trade routes woven into the map. Every rule here is a way the reading stops agreeing with
    /// the picture: a route that stops being one route the moment two of them share a hop, a blockade
    /// that fails to travel down the rest of its own line the way the renderer's accumulator makes it,
    /// or a lane carrying both kinds of traffic reported as simply blockaded when the game paints it a
    /// third colour.
    ///
    /// The fixture the mod is developed against has no trading company at all, so these are the whole
    /// of the proof that the weave is right until a save with one exists.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class TradeWeaveTests
    {
        public TradeWeaveTests()
        {
            ModStrings.Reset();
        }

        /// <summary>Dusay(1) → Heka(2) → Leo(3): the ends know each other, the middle knows both, and
        /// each hop is a lane carrying the one route.</summary>
        [Fact]
        public void OneRouteTiesItsEndsItsWaypointsAndItsLanes()
        {
            TradeWeave weave = new TradeWeave();
            int route = weave.Add(new[] { 1, 2, 3 }, false, null);

            Assert.Equal(0, route);
            Assert.Equal(1, weave.Count);
            Assert.Equal(1, weave[route].Start);
            Assert.Equal(3, weave[route].End);
            Assert.False(weave[route].Blockaded);

            IList<TradeWeave.Tie> start = weave.At(1);
            Assert.Single(start);
            Assert.True(start[0].Endpoint);
            Assert.Equal(3, start[0].Other);

            IList<TradeWeave.Tie> end = weave.At(3);
            Assert.Single(end);
            Assert.True(end[0].Endpoint);
            Assert.Equal(1, end[0].Other);

            IList<TradeWeave.Tie> middle = weave.At(2);
            Assert.Single(middle);
            Assert.False(middle[0].Endpoint);
            Assert.Equal(route, middle[0].Route);

            Assert.Single(weave.On(1, 2));
            Assert.Single(weave.On(2, 3));
            // The lane has no direction, and no route runs where none was laid.
            Assert.Single(weave.On(2, 1));
            Assert.Null(weave.On(1, 3));
        }

        /// <summary>Two routes over one hop are TWO lines to say and one line on the screen - the
        /// multiplicity ruling and the renderer's merge at once.</summary>
        [Fact]
        public void TwoRoutesSharingALaneAreNamedSeparatelyOnIt()
        {
            TradeWeave weave = new TradeWeave();
            weave.Add(new[] { 1, 2, 3 }, false, null);
            weave.Add(new[] { 1, 2, 4 }, false, null);

            IList<TradeWeave.Ride> shared = weave.On(1, 2);
            Assert.Equal(2, shared.Count);
            Assert.Equal(0, shared[0].Route);
            Assert.Equal(1, shared[1].Route);
            Assert.Equal(TradeWeave.Traffic.Open, shared[0].State);
            Assert.Equal(TradeWeave.Traffic.Open, shared[1].State);

            // ...and the system they both leave says both, one line each, never a count.
            Assert.Equal(2, weave.At(1).Count);
            Assert.Equal(3, weave.At(1)[0].Other);
            Assert.Equal(4, weave.At(1)[1].Other);
        }

        /// <summary>A system can be the end of one route and the middle of another, and says both
        /// things.</summary>
        [Fact]
        public void APlaceCanBeAnEndAndAWaypointAtOnce()
        {
            TradeWeave weave = new TradeWeave();
            weave.Add(new[] { 5, 6 }, false, null);
            weave.Add(new[] { 1, 5, 9 }, false, null);

            IList<TradeWeave.Tie> ties = weave.At(5);
            Assert.Equal(2, ties.Count);
            Assert.True(ties[0].Endpoint);
            Assert.Equal(6, ties[0].Other);
            Assert.False(ties[1].Endpoint);
        }

        /// <summary>The renderer's accumulator: a blockade at the SOURCE is set before the first leg,
        /// so every leg of that route - and the route itself - is painted blockaded.</summary>
        [Fact]
        public void ABlockadeAtTheSourceColoursTheWholeRoute()
        {
            TradeWeave weave = new TradeWeave();
            int route = weave.Add(new[] { 1, 2, 3 }, true, null);

            Assert.True(weave[route].Blockaded);
            Assert.True(weave.At(1)[0].Blockaded);
            Assert.True(weave.At(3)[0].Blockaded);
            Assert.Equal(TradeWeave.Traffic.Blockaded, weave.On(1, 2)[0].State);
            Assert.Equal(TradeWeave.Traffic.Blockaded, weave.On(2, 3)[0].State);
        }

        /// <summary>The same accumulation partway along: the legs BEFORE the blockaded node stay open
        /// and everything after it is blockaded, which is the picture the renderer paints.</summary>
        [Fact]
        public void ABlockadeMidRouteTravelsDownTheRestOfIt()
        {
            TradeWeave weave = new TradeWeave();
            int route = weave.Add(new[] { 1, 2, 3, 4 }, false, new[] { false, true, false, false });

            Assert.True(weave[route].Blockaded);
            Assert.Equal(TradeWeave.Traffic.Open, weave.On(1, 2)[0].State);
            Assert.Equal(TradeWeave.Traffic.Blockaded, weave.On(2, 3)[0].State);
            Assert.Equal(TradeWeave.Traffic.Blockaded, weave.On(3, 4)[0].State);
        }

        /// <summary>A lane carrying one open route and one blockaded one is the third material, and
        /// both of its lines say so - it is one line on the screen and one colour.</summary>
        [Fact]
        public void ALaneCarryingBothIsMixedForEveryRouteOnIt()
        {
            TradeWeave weave = new TradeWeave();
            weave.Add(new[] { 1, 2 }, false, null);
            weave.Add(new[] { 1, 2 }, true, null);

            IList<TradeWeave.Ride> rides = weave.On(1, 2);
            Assert.Equal(2, rides.Count);
            Assert.Equal(TradeWeave.Traffic.Mixed, rides[0].State);
            Assert.Equal(TradeWeave.Traffic.Mixed, rides[1].State);
            // The route's own state is still its own: one of them is the blockaded one.
            Assert.False(weave[0].Blockaded);
            Assert.True(weave[1].Blockaded);
        }

        /// <summary>A path with no leg in it draws no line and is not a route.</summary>
        [Fact]
        public void APathWithNoLegIsNoRoute()
        {
            TradeWeave weave = new TradeWeave();

            Assert.Equal(-1, weave.Add(new[] { 7 }, false, null));
            Assert.Equal(-1, weave.Add(null, false, null));
            Assert.Equal(0, weave.Count);
            Assert.Null(weave.At(7));
        }

        /// <summary>Starting again empties every tie, so a map built after the routes changed is not
        /// speaking about the last build's.</summary>
        [Fact]
        public void ClearingStartsTheWalkAgain()
        {
            TradeWeave weave = new TradeWeave();
            weave.Add(new[] { 1, 2 }, false, null);
            weave.Clear();

            Assert.Equal(0, weave.Count);
            Assert.Null(weave.At(1));
            Assert.Null(weave.On(1, 2));
        }

        [Fact]
        public void AnEndSaysWhereTheRouteGoes()
        {
            Assert.Equal("Trade route to Heka", TradeWeave.EndText("Heka", false));
            Assert.Equal(
                "Trade route to Heka, blockaded",
                TradeWeave.EndText("Heka", true)
            );
            Assert.Null(TradeWeave.EndText(null, false));
        }

        [Fact]
        public void AWaypointSaysWhoseRoadIsPassingThrough()
        {
            Assert.Equal(
                "along trade route from Dusay to Leo",
                TradeWeave.ThroughText("Dusay", "Leo")
            );
            Assert.Null(TradeWeave.ThroughText("Dusay", null));
        }

        [Fact]
        public void ALaneSaysWhatItCarriesAndInWhichColour()
        {
            Assert.Equal(
                "carries trade route Dusay to Leo, open",
                TradeWeave.LaneText("Dusay", "Leo", TradeWeave.Traffic.Open)
            );
            Assert.Equal(
                "carries trade route Dusay to Leo, blockaded",
                TradeWeave.LaneText("Dusay", "Leo", TradeWeave.Traffic.Blockaded)
            );
            Assert.Equal(
                "carries trade route Dusay to Leo, mixed",
                TradeWeave.LaneText("Dusay", "Leo", TradeWeave.Traffic.Mixed)
            );
            Assert.Null(TradeWeave.LaneText(null, "Leo", TradeWeave.Traffic.Open));
        }
    }
}
