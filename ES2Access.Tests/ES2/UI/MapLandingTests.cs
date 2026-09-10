using ES2Access.ES2.UI;
using Xunit;

namespace ES2Access.Tests.ES2.UI
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
            MapLanding plan = MapLandings.Decide(MapThing.Place, false, MapOrigin.GameLocate);
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
            MapLanding plan = MapLandings.Decide(MapThing.Point, false, MapOrigin.GameLocate);
            Assert.True(plan.FocusNode);
            Assert.True(plan.AnnounceNode);
            Assert.Equal(MapCameraMove.Slide, plan.Camera);
        }

        [Fact]
        public void AWorldLandsOnItsOwnNodeAndZoomsIn()
        {
            MapLanding plan = MapLandings.Decide(MapThing.PlanetBound, false, MapOrigin.GameLocate);
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
            MapLanding plan = MapLandings.Decide(MapThing.Place, true, MapOrigin.GameLocate);
            Assert.False(plan.ExitInspect);
            Assert.True(plan.MoveCell);
            // Owner ruling 2026-09-10: the cursor is seated on the row underneath, silently, and that
            // row is where leaving the mode puts the player.
            Assert.True(plan.FocusNode);
            Assert.False(plan.AnnounceNode);
            Assert.True(plan.RebaseEntry);
            // And the cell's own slide is the whole camera move: the scale stays where the player
            // put it.
            Assert.Equal(MapCameraMove.None, plan.Camera);
        }

        /// <summary>The ruling in one line: under the cell a place and a point do exactly the same
        /// thing - move the cell, seat the cursor silently under it where the GAME is the one
        /// pointing, touch the zoom not at all - so no gesture arrives differently from any other.
        /// </summary>
        [Fact]
        public void UnderTheCellTheCellMovesAndTheCursorFollowsSilently()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point })
            {
                MapLanding plan = MapLandings.Decide(thing, true, MapOrigin.GameLocate);
                Assert.True(plan.MoveCell);
                Assert.True(plan.FocusNode);
                Assert.False(plan.AnnounceNode);
                Assert.True(plan.RebaseEntry);
                Assert.Equal(MapCameraMove.None, plan.Camera);
            }

            // The one landing that still moves the cursor and zooms with the cursor up is the one that
            // TAKES IT DOWN first, and it is no longer reading the map through a square by then.
            MapLanding world = MapLandings.Decide(MapThing.PlanetBound, true, MapOrigin.GameLocate);
            Assert.True(world.ExitInspect);
            Assert.True(world.FocusNode);
            Assert.Equal(MapCameraMove.Zoom, world.Camera);
        }

        // ---- who asked (owner ruling 2026-09-10) ----

        /// <summary>One of the MOD's own jumps under the cell - the scanner's go-to, a bookmark, the
        /// next idle fleet, a starlane - moves the cell and nothing else, as it has since 2026-08-31:
        /// the player is driving the square, so leaving it puts them back on the row they armed it
        /// from rather than wherever their sweep took them.</summary>
        [Fact]
        public void OneOfTheModSOwnJumpsUnderTheCellMovesOnlyTheCell()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point })
            {
                MapLanding plan = MapLandings.Decide(thing, true, MapOrigin.ModJump);
                Assert.True(plan.MoveCell);
                Assert.False(plan.FocusNode);
                Assert.False(plan.AnnounceNode);
                Assert.False(plan.RebaseEntry);
                Assert.Equal(MapCameraMove.None, plan.Camera);
            }
        }

        /// <summary>Owner ruling 2026-09-10 (second pass): a landing the GAME made with the cell up
        /// puts the player back on the map widget, and that arrival is what speaks - the map names the
        /// mode as they land on it, and the square follows from the mode's own resume. Nobody else's
        /// landing says a word: the mod's own jumps keep their silent seat, and with the cell down the
        /// row landed on is the announcement.</summary>
        [Fact]
        public void BeingLedSomewhereUnderTheCellSaysWhereThePlayerNowIs()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point, MapThing.Nowhere })
            {
                Assert.True(MapLandings.Decide(thing, true, MapOrigin.GameLocate).AnnounceStop);
                Assert.False(MapLandings.Decide(thing, true, MapOrigin.ModJump).AnnounceStop);
                Assert.False(MapLandings.Decide(thing, false, MapOrigin.GameLocate).AnnounceStop);
            }

            // The one landing that takes the cell DOWN reads the row it lands on, so the map has
            // nothing of its own to say.
            Assert.False(
                MapLandings.Decide(MapThing.PlanetBound, true, MapOrigin.GameLocate).AnnounceStop
            );
        }

        /// <summary>Who asked changes nothing at all with the cell DOWN: the tree is what the player
        /// is reading either way, so every gesture lands on the row and says it.</summary>
        [Fact]
        public void OutOfTheCellItDoesNotMatterWhoAsked()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point, MapThing.PlanetBound })
            {
                Assert.Equal(
                    MapLandings.Decide(thing, false, MapOrigin.GameLocate),
                    MapLandings.Decide(thing, false, MapOrigin.ModJump)
                );
            }
        }

        [Fact]
        public void AThingAtABarePointKeepsTheCellUpAndLetsItSlide()
        {
            MapLanding plan = MapLandings.Decide(MapThing.Point, true, MapOrigin.GameLocate);
            Assert.True(plan.MoveCell);
            // The cursor follows underneath without a word (owner ruling 2026-09-10).
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
            MapLanding plan = MapLandings.Decide(MapThing.PlanetBound, true, MapOrigin.GameLocate);
            Assert.True(plan.ExitInspect);
            Assert.False(plan.MoveCell);
            Assert.True(plan.FocusNode);
            Assert.True(plan.AnnounceNode);
            Assert.Equal(MapCameraMove.Zoom, plan.Camera);
        }

        // ---- a point with nothing on it ----

        /// Owner ruling 2026-08-22: everything the game can point the player at is supposed to have a
        /// row, so this is still a defect to report and never a landing - the tree cursor does not
        /// move, because there is no row for it to move to.
        [Fact]
        public void APointWithNothingOnItIsADefectAndMovesNoCursor()
        {
            foreach (bool inspecting in new[] { false, true })
            {
                MapLanding plan = MapLandings.Decide(MapThing.Nowhere, inspecting, MapOrigin.GameLocate);
                Assert.True(plan.Unplaced);
                Assert.False(plan.ExitInspect);
                Assert.False(plan.FocusNode);
                Assert.False(plan.AnnounceNode);
                Assert.False(plan.RebaseEntry);
                Assert.Equal(MapCameraMove.None, plan.Camera);
            }
        }

        /// Owner ruling 2026-09-10: the player still gets to READ the place, through the one reader
        /// this map has for a bare coordinate - the cell is armed there where it is down and moved
        /// there where it is up, and it never does both.
        [Fact]
        public void APointWithNothingOnItIsReadThroughTheCell()
        {
            MapLanding down = MapLandings.Decide(MapThing.Nowhere, false, MapOrigin.GameLocate);
            Assert.True(down.ArmCell);
            Assert.False(down.MoveCell);

            MapLanding up = MapLandings.Decide(MapThing.Nowhere, true, MapOrigin.GameLocate);
            Assert.True(up.MoveCell);
            Assert.False(up.ArmCell);
        }

        /// Owner ruling 2026-09-10: reading a bare coordinate through the cell is the answer to the
        /// GAME pointing at one. The mod's own jumps move nothing - the only one of them that can
        /// reach a square of empty sky, the scanner, arms the cell on its own path.
        [Fact]
        public void OneOfTheModSOwnJumpsAtABareCoordinateTouchesNothing()
        {
            foreach (bool inspecting in new[] { false, true })
            {
                MapLanding plan = MapLandings.Decide(MapThing.Nowhere, inspecting, MapOrigin.ModJump);
                Assert.True(plan.Unplaced);
                Assert.False(plan.MoveCell);
                Assert.False(plan.ArmCell);
                Assert.False(plan.FocusNode);
            }
        }

        // ---- the invariants across the whole table ----

        [Fact]
        public void ALandingNeverBothSpeaksItselfAndHidesUnderTheCell()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point, MapThing.PlanetBound })
            {
                foreach (bool inspecting in new[] { false, true })
                foreach (MapOrigin origin in new[] { MapOrigin.GameLocate, MapOrigin.ModJump })
                {
                    MapLanding plan = MapLandings.Decide(thing, inspecting, origin);
                    Assert.False(plan.MoveCell && plan.AnnounceNode);
                    Assert.False(plan.MoveCell && plan.ExitInspect);
                    // A landing that is not a defect always moves the player: the cursor to its row,
                    // the cell to its square, or both. And the entry is re-based over exactly the
                    // cursor moves nobody hears, which is what makes leaving the cell end where the
                    // game led.
                    Assert.True(plan.FocusNode || plan.MoveCell);
                    Assert.NotEqual(plan.MoveCell, plan.AnnounceNode);
                    Assert.Equal(plan.FocusNode && plan.MoveCell, plan.RebaseEntry);
                    Assert.False(plan.ArmCell);
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
                foreach (MapOrigin origin in new[] { MapOrigin.GameLocate, MapOrigin.ModJump })
                {
                    MapLanding far = MapLandings.Decide(thing, inspecting, origin, MapReach.Elsewhere);
                    MapLanding near = MapLandings.Decide(thing, inspecting, origin, MapReach.Local);
                    Assert.True(far.Frame);
                    Assert.False(near.Frame);
                    Assert.Equal(far.Camera, near.Camera);
                    Assert.Equal(far.MoveCell, near.MoveCell);
                    Assert.Equal(far.FocusNode, near.FocusNode);
                    Assert.Equal(far.AnnounceNode, near.AnnounceNode);
                    Assert.Equal(far.ExitInspect, near.ExitInspect);
                    Assert.Equal(far.RebaseEntry, near.RebaseEntry);
                    Assert.Equal(far.AnnounceStop, near.AnnounceStop);
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
                Assert.True(MapLandings.Decide(thing, false, MapOrigin.GameLocate).Frame);
            }
        }

        [Fact]
        public void NothingTouchesTheFreeCursorWhileItIsDown()
        {
            foreach (MapThing thing in new[] { MapThing.Place, MapThing.Point, MapThing.PlanetBound })
            {
                MapLanding plan = MapLandings.Decide(thing, false, MapOrigin.GameLocate);
                Assert.False(plan.ExitInspect);
                Assert.False(plan.MoveCell);
                Assert.False(plan.ArmCell);
                Assert.False(plan.RebaseEntry);
                Assert.True(plan.AnnounceNode);
            }
        }
    }
}
