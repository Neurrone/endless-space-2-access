namespace ES2Access.ES2.UI
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

    /// <summary>WHO ASKED - the one thing that decides what a landing made under a live cell does
    /// beyond moving the cell (owner ruling 2026-09-10).</summary>
    public enum MapOrigin
    {
        /// <summary>The GAME is leading the player somewhere: a notification's show-location, a
        /// tutorial's, the quest journal's, the go-to-location key - everything that arrives through
        /// the game's own "show me this" call. Under a live cell such a landing seats the tree cursor
        /// on the row silently and re-bases the mode's exit on it, and a bare coordinate arms the
        /// cell.</summary>
        GameLocate,

        /// <summary>The PLAYER is driving one of the mod's own map keys: the scanner's go-to, a
        /// bookmark chord, the next-idle-fleet key, following a starlane. Under a live cell the cell
        /// is the only thing that moves - the tree cursor and the row the mode was armed from stay
        /// where the player left them, so leaving the square puts them back where they were.</summary>
        ModJump,
    }

    /// <summary>What the camera has to do beyond whatever the cell cursor already does.</summary>
    public enum MapCameraMove
    {
        None,
        Zoom,
        Slide,
    }

    /// <summary>How far a landing reaches - the one thing that decides whether it FRAMES what it
    /// lands on.</summary>
    public enum MapReach
    {
        /// <summary>Somewhere else on the map: a go-to, a bookmark jump, a quest pin, the game's own
        /// show-location. The player asked to be taken there, so the picture is composed around the
        /// destination.</summary>
        Elsewhere,

        /// <summary>One step from where the player already stands - following a starlane to the star
        /// at its far end, and backing up the lane again. It is a walk and not a journey: the
        /// destination is a neighbour of the row the cursor was already on, so the picture stays at
        /// the distance the player put it and moves only as far as reading the next row moves it.
        /// </summary>
        Local,
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

        /// <summary>Make the row landed on the one LEAVING the free cursor puts the player back on,
        /// and its place the one the camera comes back to (owner ruling 2026-09-10). Set exactly where
        /// the cursor is seated silently under a live cell on a landing the GAME asked for
        /// (<see cref="MapOrigin.GameLocate"/>): what the player was last SHOWN through the square is
        /// where the mode ends, rather than the row they armed it from.</summary>
        public bool RebaseEntry;

        /// <summary>Say what the map widget is as the cursor is put back on it - the arrival's own
        /// line, and the whole of it: under a live cell the stop is named after the mode rather than
        /// after the map (<c>GalaxyHudScreen.MapContext</c>) and the row landed on is not read at all,
        /// the cell's own resume being the second half of the arrival (owner rulings 2026-09-10).
        ///
        /// Set exactly where the GAME led the player somewhere with the cell up
        /// (<see cref="MapOrigin.GameLocate"/>). The mod's own jumps keep the silent seat they have
        /// had since 2026-08-31: the player driving a square about the map never left the map in the
        /// first place as far as they are concerned, so the cell's reading is their whole arrival.
        /// </summary>
        public bool AnnounceStop;

        /// <summary>Turn the free cursor ON at the point. The answer to a place the tree has no row
        /// for when the GAME is the one pointing (owner ruling 2026-09-10): the cell is the only
        /// reader this map has for a bare coordinate, so where it is down it is armed there rather
        /// than the request being answered with a word.</summary>
        public bool ArmCell;

        /// <summary>What the caller must do to the camera. <see cref="MapCameraMove.None"/> with
        /// <see cref="MoveCell"/> set means the cell's own slide is the whole camera move.</summary>
        public MapCameraMove Camera;

        /// <summary>Nothing on the map answers for the point: say so and leave the cursor alone.
        /// </summary>
        public bool Unplaced;

        /// <summary>The camera move FRAMES the destination - it is made as a thing asked for out loud
        /// rather than as the camera following the cursor, so it overrides whatever the player had
        /// set the picture to. False for a local hop, whose camera does exactly what walking into
        /// that place would have done at this distance and no more.</summary>
        public bool Frame;
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
    /// <item>OUT of the cell, a place is ZOOMED to and a point is SLID to.</item>
    /// <item>UNDER THE CELL, NOTHING CHANGES THE ZOOM (owner ruling 2026-08-31, reversing the
    /// 2026-08-22 line that had a place's zoom override the cell's own slide). While the player is
    /// driving a square about the map, that square is the only thing moving the camera: it centres
    /// what was landed on and the picture stays at the scale the player chose. A landing that dived
    /// in was answering a question they had not asked - they asked where a thing IS, not to be taken
    /// down to it - and it disagreed with the map's other go-to gestures, which leave the zoom alone.
    /// So a place and a point are the same plan in the cell: move the cell, seat the cursor silently,
    /// and let the cell's slide be the whole camera move. Leaving the mode then puts the cursor on
    /// what was landed on, and stepping INSIDE it zooms as any tree walk does - the ordinary machinery,
    /// unchanged.</item>
    /// <item>WHAT THE GAME SHOWED THROUGH THE CELL IS WHERE LEAVING IT PUTS THE PLAYER (owner ruling
    /// 2026-09-10, reversing the second 2026-08-31 ruling for one caller only). The row is seated
    /// SILENTLY - the cell's own arrival line is the whole announcement, and nothing extra is said -
    /// and it becomes the row Escape restores to, with the camera recentred there
    /// (<see cref="RebaseEntry"/>). The game led the player to a place; leaving the square leaves them
    /// standing on it rather than back where they armed the mode. It is exactly the landings the GAME
    /// asks for that do this (<see cref="MapOrigin.GameLocate"/>): the mod's own jumps - the scanner,
    /// a bookmark, the next idle fleet, a starlane - keep the 2026-08-31 line, where the cell is the
    /// only thing that moves and the mode ends where it was armed. The player driving a square about
    /// the map has not asked to be moved; the game pointing them at a place has.</item>
    /// <item>AND IT PUTS THEM ON THE MAP, WHICH SAYS SO (owner rulings 2026-09-10, the second
    /// pass). The silent row seat above is a REQUEST - it lands whenever the tree next draws that
    /// row, which is frames later where its branch has to open and never where the branch never
    /// does - so on its own it left the player standing on the notification strip with the square
    /// moved underneath them and nothing said at all (measured: the cursor was still on the strip
    /// row 0.4 s after the press, and the cell's reading only came when the request landed). So a
    /// landing the GAME asked for seats the MAP WIDGET too, on the frame of the press, and that
    /// arrival is what speaks: the stop is named after the mode while the cell is up
    /// (<c>GalaxyHudScreen.MapContext</c>), the row landed on is not read
    /// (<see cref="AnnounceStop"/>), and the mode's own resume reads the square a fifth of a second
    /// later. Two lines, neither of them dependent on the row seat arriving.</item>
    /// <item>A LOCAL HOP DOES NOT FRAME (owner ruling 2026-09-02, <see cref="MapReach.Local"/>).
    /// Following a starlane is a walk to the next row along, not a request to be shown a place, so
    /// its camera is the camera an in-place expansion of that system would have given: at the far
    /// bands, where the map draws no inside for a system, a slide and nothing more; at the detail
    /// bands, the same coming-in that walking into the place makes. Measured at spoken level 5, where
    /// expanding a system correctly stayed put and following a lane out of it dived to 13 - the same
    /// gesture, one row apart, answering two different questions. The MINIMUM BAND is untouched: a
    /// landing still forces the band its target needs a row at (<c>GalaxyHudScreen.EnsureBand</c>),
    /// which for a system is the band that names the systems, so a hop can never land where there is
    /// no row.</item>
    /// <item>Out of the free cursor the landing's own announcement is the whole utterance, once.
    /// </item>
    /// <item>A point with NOTHING on it is still a defect to LOG (owner ruling, 2026-08-22):
    /// everything the game can point the player at is supposed to have a row, and the caller logs the
    /// request so the sweep can find it. What the PLAYER gets is no longer a word and a shrug (owner
    /// ruling 2026-09-10) but the CELL: a square of bare map is exactly what it reads, so it is ARMED
    /// on the point where it is down and MOVED there where it is up, and the cell's own entry or
    /// arrival line is the news. The tree cursor is not moved - there is no row to move it to. Again
    /// only where the GAME is the one pointing: the mod's own jumps that can reach a bare square
    /// already arm the cell on their own path (the scanner's square result), and the rest move
    /// nothing.</item>
    /// </list>
    /// </summary>
    public static class MapLandings
    {
        public static MapLanding Decide(
            MapThing thing,
            bool inspectLive,
            MapOrigin origin,
            MapReach reach = MapReach.Elsewhere
        )
        {
            bool frame = reach == MapReach.Elsewhere;
            // Under the cell, being LED is what moves anything but the cell.
            bool led = origin == MapOrigin.GameLocate;
            switch (thing)
            {
                case MapThing.Place:
                    return new MapLanding
                    {
                        Frame = frame,
                        MoveCell = inspectLive,
                        // Under the cell the cursor goes to the row too, SILENTLY, and that row is
                        // where leaving the mode puts the player - for a landing the GAME asked for
                        // (owner ruling 2026-09-10). Under one of the mod's own jumps nothing but the
                        // cell moves, as it has since 2026-08-31.
                        FocusNode = !inspectLive || led,
                        AnnounceNode = !inspectLive,
                        AnnounceStop = inspectLive && led,
                        RebaseEntry = inspectLive && led,
                        // Out of the cell a place is zoomed to. UNDER the cell nothing is: the cell's
                        // own slide is the whole camera move, exactly as it is for a point.
                        Camera = inspectLive ? MapCameraMove.None : MapCameraMove.Zoom,
                    };

                case MapThing.Point:
                    return new MapLanding
                    {
                        Frame = frame,
                        MoveCell = inspectLive,
                        FocusNode = !inspectLive || led,
                        AnnounceNode = !inspectLive,
                        AnnounceStop = inspectLive && led,
                        RebaseEntry = inspectLive && led,
                        // The cell slides itself; out of the mode the caller does it.
                        Camera = inspectLive ? MapCameraMove.None : MapCameraMove.Slide,
                    };

                case MapThing.PlanetBound:
                    return new MapLanding
                    {
                        Frame = frame,
                        ExitInspect = inspectLive,
                        FocusNode = true,
                        AnnounceNode = true,
                        Camera = MapCameraMove.Zoom,
                    };

                default:
                    // Nothing for the tree to land on, so where the GAME is pointing the cell is the
                    // reading: armed on the point where it is down, moved there where it is up (owner
                    // ruling 2026-09-10). One of the mod's own jumps moves nothing at all - the only
                    // one that can reach a bare square arms the cell on its own path.
                    return new MapLanding
                    {
                        Unplaced = true,
                        MoveCell = inspectLive && led,
                        AnnounceStop = inspectLive && led,
                        ArmCell = !inspectLive && led,
                    };
            }
        }
    }
}
