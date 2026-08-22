using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The one decision table behind every "go and look at this" on the galaxy map. Every failure
    /// here is inaudible - a cell left on a world it cannot read, a landing announced twice, a camera
    /// that never moved - so the rules are asserted off the engine.
    /// </summary>
    public class MapLandingTests
    {
        // ---- out of the free cursor: the tree lands and says so ----

        [Fact]
        public void APlaceLandsOnItsNodeAndZooms()
        {
            MapLanding plan = MapLandings.Decide(MapThing.Place, false);
            Assert.False(plan.ExitInspect);
            Assert.False(plan.MoveCell);
            Assert.True(plan.FocusNode);
            Assert.True(plan.AnnounceNode);
            Assert.Equal(MapCameraMove.Zoom, plan.Camera);
            Assert.False(plan.Unplaced);
        }

        [Fact]
        public void AThingAtABarePointLandsOnItsRowAndSlides()
        {
            MapLanding plan = MapLandings.Decide(MapThing.Point, false);
            Assert.True(plan.FocusNode);
            Assert.True(plan.AnnounceNode);
            Assert.Equal(MapCameraMove.Slide, plan.Camera);
        }

        [Fact]
        public void AWorldLandsOnItsOwnNodeAndZoomsIn()
        {
            MapLanding plan = MapLandings.Decide(MapThing.PlanetBound, false);
            Assert.False(plan.ExitInspect);
            Assert.False(plan.MoveCell);
            Assert.True(plan.FocusNode);
            Assert.True(plan.AnnounceNode);
            Assert.Equal(MapCameraMove.Zoom, plan.Camera);
        }

        // ---- with the free cursor up: the cell is what the player is reading ----

        [Fact]
        public void APlaceKeepsTheCellUpAndStillZooms()
        {
            MapLanding plan = MapLandings.Decide(MapThing.Place, true);
            Assert.False(plan.ExitInspect);
            Assert.True(plan.MoveCell);
            // The tree cursor follows underneath, to be felt when the mode ends - silently, or the
            // player hears the cell and the node for one press.
            Assert.True(plan.FocusNode);
            Assert.False(plan.AnnounceNode);
            // The place's own zoom overrides the cell's slide, so the picture is the same whichever
            // way the map is being read.
            Assert.Equal(MapCameraMove.Zoom, plan.Camera);
        }

        [Fact]
        public void AThingAtABarePointKeepsTheCellUpAndLetsItSlide()
        {
            MapLanding plan = MapLandings.Decide(MapThing.Point, true);
            Assert.True(plan.MoveCell);
            Assert.True(plan.FocusNode);
            Assert.False(plan.AnnounceNode);
            // Nothing on top of the cell's own slide.
            Assert.Equal(MapCameraMove.None, plan.Camera);
        }

        /// The correction batch 7 was for: the scanner used to jump the CELL onto a planet, which is
        /// a thing the cell cannot read.
        [Fact]
        public void AWorldENDSTheFreeCursorFirst()
        {
            MapLanding plan = MapLandings.Decide(MapThing.PlanetBound, true);
            Assert.True(plan.ExitInspect);
            Assert.False(plan.MoveCell);
            Assert.True(plan.FocusNode);
            Assert.True(plan.AnnounceNode);
            Assert.Equal(MapCameraMove.Zoom, plan.Camera);
        }

        // ---- a point with nothing on it ----

        /// Owner ruling 2026-08-22: everything the game can point the player at is supposed to have a
        /// row, so this is a defect to report and never a behaviour to fall back on - in particular it
        /// does NOT arm the free cursor, and it moves nothing.
        [Fact]
        public void APointWithNothingOnItIsADefectAndMovesNothing()
        {
            foreach (bool inspecting in new[] { false, true })
            {
                MapLanding plan = MapLandings.Decide(MapThing.Nowhere, inspecting);
                Assert.True(plan.Unplaced);
                Assert.False(plan.ExitInspect);
                Assert.False(plan.MoveCell);
                Assert.False(plan.FocusNode);
                Assert.Equal(MapCameraMove.None, plan.Camera);
            }
        }

        // ---- the invariants across the whole table ----

        [Fact]
        public void ALandingNeverBothSpeaksItselfAndHidesUnderTheCell()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point, MapThing.PlanetBound })
            {
                foreach (bool inspecting in new[] { false, true })
                {
                    MapLanding plan = MapLandings.Decide(thing, inspecting);
                    Assert.False(plan.MoveCell && plan.AnnounceNode);
                    Assert.False(plan.MoveCell && plan.ExitInspect);
                    Assert.True(plan.FocusNode);
                }
            }
        }

        [Fact]
        public void NothingTouchesTheFreeCursorWhileItIsDown()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point, MapThing.PlanetBound })
            {
                MapLanding plan = MapLandings.Decide(thing, false);
                Assert.False(plan.ExitInspect);
                Assert.False(plan.MoveCell);
                Assert.True(plan.AnnounceNode);
            }
        }
    }
}
