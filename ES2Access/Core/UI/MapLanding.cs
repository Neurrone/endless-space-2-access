namespace ES2Access.Core.UI
{
    /// <summary>What kind of thing the player is being sent to on a world map - the only distinction
    /// the landing rules turn on.</summary>
    public enum MapThing
    {
        /// <summary>A place the map draws as one point and the tree declares as one node: a star
        /// system, a special node. The camera goes IN on it.</summary>
        Place,

        /// <summary>Something standing at a bare point of the map with a row of its own: a fleet under
        /// way, a probe, an ally's pin, a missile in flight, a quest marker planted out in the open.
        /// The camera slides onto the point.</summary>
        Point,

        /// <summary>Something the map draws AT a place rather than as a point of its own: a planet, an
        /// anomaly or curiosity or deposit on one, a quest marker planted at a system. Its node hangs
        /// under the place's, and reading it needs the close-up view the camera has to fly to.
        /// </summary>
        PlanetBound,

        /// <summary>A point with nothing on it. Not a landing: the map has been moved and the tree has
        /// nowhere to put the player.</summary>
        Nowhere,
    }

    /// <summary>What the camera has to do beyond whatever the cell cursor already does.</summary>
    public enum MapCameraMove
    {
        None,
        Zoom,
        Slide,
    }

    /// <summary>The plan for one "go and look at this" - what to do to the inspect cell, the tree
    /// cursor and the camera.</summary>
    public struct MapLanding
    {
        /// <summary>Take the inspect cursor down first: what is being landed on is read from the tree
        /// and not from a square of sky.</summary>
        public bool ExitInspect;

        /// <summary>Put the inspect cell on the thing's own tile.</summary>
        public bool MoveCell;

        /// <summary>Send the tree cursor to the thing's node.</summary>
        public bool FocusNode;

        /// <summary>...and let that landing announce itself. False where the cell is the thing the
        /// player is reading, so the tree move is felt only when the mode ends.</summary>
        public bool AnnounceNode;

        /// <summary>What the caller must do to the camera. <see cref="MapCameraMove.None"/> with
        /// <see cref="MoveCell"/> set means the cell's own slide is the whole camera move.</summary>
        public MapCameraMove Camera;

        /// <summary>Nothing on the map answers for the point: say so and leave the cursor alone.
        /// </summary>
        public bool Unplaced;
    }

    /// <summary>
    /// The one decision table behind every "go and look at this" on a world map: a notification's
    /// show-location, a scanner's go-to, travelling a road, a global go-to key.
    ///
    /// It exists apart from the game because the thing that goes wrong here is inaudible. Each caller
    /// used to answer these three questions for itself - does the free cursor stay up, does the tree
    /// cursor move, does the camera zoom or slide - and the copies disagreed: one of them jumped the
    /// cell onto a PLANET, which is a thing the cell cannot read, and the player was left standing on
    /// a square of sky next to the world they had asked for.
    ///
    /// The rules, owner-ruled 2026-08-22:
    /// <list type="bullet">
    /// <item>A PLACE and a POINT keep the free cursor up where it is up: both are things the cell can
    /// read, so the cell goes to them and the tree cursor follows silently underneath, to be felt when
    /// the mode ends.</item>
    /// <item>A PLANET-BOUND thing ENDS the free cursor first: it is read from the tree and from the
    /// close-up view, neither of which the cell can show.</item>
    /// <item>A place is ZOOMED to, a point is SLID to - even in the cell, where the place's zoom
    /// overrides the cell's own slide, so the picture is the same whichever way the player is
    /// reading.</item>
    /// <item>Out of the free cursor the landing's own announcement is the whole utterance, once.
    /// </item>
    /// <item>A point with NOTHING on it is a defect, not a behaviour (owner ruling, 2026-08-22):
    /// everything the game can point the player at is supposed to have a row. The caller says the
    /// "shown on the map" line, logs the request so the sweep can find it, and moves nothing.</item>
    /// </list>
    /// </summary>
    public static class MapLandings
    {
        public static MapLanding Decide(MapThing thing, bool inspectLive)
        {
            switch (thing)
            {
                case MapThing.Place:
                    return new MapLanding
                    {
                        MoveCell = inspectLive,
                        FocusNode = true,
                        AnnounceNode = !inspectLive,
                        Camera = MapCameraMove.Zoom,
                    };

                case MapThing.Point:
                    return new MapLanding
                    {
                        MoveCell = inspectLive,
                        FocusNode = true,
                        AnnounceNode = !inspectLive,
                        // The cell slides itself; out of the mode the caller does it.
                        Camera = inspectLive ? MapCameraMove.None : MapCameraMove.Slide,
                    };

                case MapThing.PlanetBound:
                    return new MapLanding
                    {
                        ExitInspect = inspectLive,
                        FocusNode = true,
                        AnnounceNode = true,
                        Camera = MapCameraMove.Zoom,
                    };

                default:
                    return new MapLanding { Unplaced = true };
            }
        }
    }
}
