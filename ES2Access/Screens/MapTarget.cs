using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.ES2.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>What a caller wants done to the camera, where it knows better than the target's own
    /// kind does. <see cref="Auto"/> is the ordinary answer and the one every caller uses today.
    /// </summary>
    internal enum MapCamera
    {
        Auto,
        Zoom,
        Slide,
        None,
    }

    /// <summary>
    /// One thing on the galaxy map to send the player to, resolved: which node the tree has for it,
    /// which kind of thing it is (<see cref="MapThing"/>, the only thing the landing rules turn on),
    /// where it stands, and - for the one case the tree has no row for - the fleet the map itself can
    /// select instead.
    ///
    /// Built by the caller that KNOWS what it found (the scanner has the planet, the locate watcher
    /// has the entity the game named, a lane hop has the destination system), never re-derived inside
    /// the landing: the difference between "a planet" and "the system it is in" is exactly what
    /// decides whether the free cursor stays up, and a landing that had to guess got it wrong.
    /// </summary>
    internal struct MapTarget
    {
        /// <summary>Which of the four kinds this is.</summary>
        public MapThing Thing;

        /// <summary>The node the cursor should land on. Null only for <see cref="MapThing.Nowhere"/>.
        /// </summary>
        public ControlId Id;

        /// <summary>The system the camera zooms in on, for a place and for everything drawn at one.
        /// </summary>
        public StarSystemNode System;

        /// <summary>Where on the map it stands - what the camera slides to, and the tile the inspect
        /// cell opens on.</summary>
        public Vector3 At;

        /// <summary>A fleet the tree has no row for: the map's own selection is then the only "go to
        /// this fleet" this game has (<see cref="GalaxyHudScreen.SelectFleet"/>).</summary>
        public Fleet Select;

        /// <summary>
        /// The thing standing at <see cref="At"/>, where the landed row's own id does not carry it.
        ///
        /// A fleet's row is keyed STRUCTURALLY on purpose - the fleet panel's own line is keyed on the
        /// same garrison, and two nodes sharing a backing object would be one control to the cursor
        /// (<c>GalaxyHudScreen.FleetNode</c>) - so <see cref="Id"/><c>.Subject</c> is null for
        /// exactly the rows the camera most needs to name. Without this the landing could neither tell
        /// the camera which fleet it was arriving at nor write down that it had arrived, and the
        /// focus that followed made a second move (owner-reported 2026-08-26).
        /// </summary>
        public IGameEntityWithGalaxyPosition Standing;

        /// <summary>A place the map draws as one point: a star system, a special node.</summary>
        public static MapTarget Place(StarSystemNode system, ControlId id, Vector3 at)
        {
            return new MapTarget
            {
                Thing = MapThing.Place,
                Id = id,
                System = system,
                At = at,
            };
        }

        /// <summary>Something drawn AT a place - a planet, a resource or anomaly on one, a quest
        /// marker planted at a system - whose node hangs under the place's.</summary>
        public static MapTarget Under(StarSystemNode system, ControlId id, Vector3 at)
        {
            return new MapTarget
            {
                Thing = MapThing.PlanetBound,
                Id = id,
                System = system,
                At = at,
            };
        }

        /// <summary>Something standing at a bare point with a row of its own. <paramref name="standing"/>
        /// is the thing itself where its row's key does not carry it (<see cref="Standing"/>).</summary>
        public static MapTarget Point(
            ControlId id,
            Vector3 at,
            IGameEntityWithGalaxyPosition standing = null
        )
        {
            return new MapTarget
            {
                Thing = MapThing.Point,
                Id = id,
                At = at,
                Standing = standing,
            };
        }

        /// <summary>A fleet the map draws that the tree has no row for.</summary>
        public static MapTarget LooseFleet(Fleet fleet, Vector3 at)
        {
            return new MapTarget { Thing = MapThing.Point, At = at, Select = fleet };
        }

        /// <summary>A point with nothing on it - a request the tree cannot answer.</summary>
        public static MapTarget Nowhere(Vector3 at)
        {
            return new MapTarget { Thing = MapThing.Nowhere, At = at };
        }
    }
}
