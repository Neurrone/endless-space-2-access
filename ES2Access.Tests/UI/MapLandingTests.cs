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
        public void APlaceKeepsTheCellUpAndDoesNotZoom()
        {
            MapLanding plan = MapLandings.Decide(MapThing.Place, true);
            Assert.False(plan.ExitInspect);
            Assert.True(plan.MoveCell);
            // Owner ruling 2026-08-31 (the later of the two that day, reversing the reseat): under the
            // cell the tree cursor does not move at all, so leaving the mode puts the player back
            // where they armed it.
            Assert.False(plan.FocusNode);
            Assert.False(plan.AnnounceNode);
            // And the cell's own slide is the whole camera move: the scale stays where the player
            // put it.
            Assert.Equal(MapCameraMove.None, plan.Camera);
        }

        /// <summary>The ruling in one line: under the cell a place and a point do exactly the same
        /// thing - move the cell, touch neither the cursor nor the zoom - so no gesture arrives
        /// differently from any other.</summary>
        [Fact]
        public void UnderTheCellOnlyTheCellMoves()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point })
            {
                MapLanding plan = MapLandings.Decide(thing, true);
                Assert.True(plan.MoveCell);
                Assert.False(plan.FocusNode);
                Assert.False(plan.AnnounceNode);
                Assert.Equal(MapCameraMove.None, plan.Camera);
            }

            // The one landing that still moves the cursor and zooms with the cursor up is the one that
            // TAKES IT DOWN first, and it is no longer reading the map through a square by then.
            MapLanding world = MapLandings.Decide(MapThing.PlanetBound, true);
            Assert.True(world.ExitInspect);
            Assert.True(world.FocusNode);
            Assert.Equal(MapCameraMove.Zoom, world.Camera);
        }

        [Fact]
        public void AThingAtABarePointKeepsTheCellUpAndLetsItSlide()
        {
            MapLanding plan = MapLandings.Decide(MapThing.Point, true);
            Assert.True(plan.MoveCell);
            // The cursor stays where the mode was armed (owner ruling 2026-08-31).
            Assert.False(plan.FocusNode);
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
                    // Moving the CELL and moving the CURSOR are now the two halves of one choice:
                    // exactly one of them happens on every landing that is not a defect.
                    Assert.NotEqual(plan.MoveCell, plan.FocusNode);
                }
            }
        }

        // ---- how far the landing reaches ----

        /// Owner ruling 2026-09-02: a LOCAL hop - travelling a starlane to the star at its far end -
        /// does not frame what it lands on. Everything else about the plan is unchanged: the reach
        /// decides the camera's authority over the picture and nothing else.
        [Fact]
        public void ALocalHopDoesNotFrameWhatItLandsOn()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point, MapThing.PlanetBound })
            {
                foreach (bool inspecting in new[] { false, true })
                {
                    MapLanding far = MapLandings.Decide(thing, inspecting, MapReach.Elsewhere);
                    MapLanding near = MapLandings.Decide(thing, inspecting, MapReach.Local);
                    Assert.True(far.Frame);
                    Assert.False(near.Frame);
                    Assert.Equal(far.Camera, near.Camera);
                    Assert.Equal(far.MoveCell, near.MoveCell);
                    Assert.Equal(far.FocusNode, near.FocusNode);
                    Assert.Equal(far.AnnounceNode, near.AnnounceNode);
                    Assert.Equal(far.ExitInspect, near.ExitInspect);
                }
            }
        }

        /// A landing whose reach nobody states frames, which is what every caller but the lane hop
        /// wants and what each of them did before the reach existed.
        [Fact]
        public void ALandingFramesUnlessItSaysOtherwise()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point, MapThing.PlanetBound })
            {
                Assert.True(MapLandings.Decide(thing, false).Frame);
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
