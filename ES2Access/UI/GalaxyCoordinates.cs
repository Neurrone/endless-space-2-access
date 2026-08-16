using System;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The coordinate pair every place on the MAP says after its name, measured from the empire's home
    /// system. The wording and the rounding are <see cref="MapCoordinates"/>'s; what lives here is the
    /// one thing that needs the game: where home is.
    ///
    /// The origin is cached because it is a two-service lookup and an agency walk, and a coordinate
    /// part is asked of the focused node on EVERY frame (an un-watched part is still asked - see
    /// dev-loop). It is re-derived whenever the player empire changes IDENTITY, which is what loading
    /// another save looks like from here, and retried for as long as the empire has no home system yet
    /// (a game still starting up). At most one attempt per frame, so the fallback case costs no more
    /// than the settled one.
    ///
    /// With no home to measure from, the pair is the place's own unshifted position rather than
    /// silence: the numbers are still a consistent map, only with the game's own origin at their
    /// centre instead of the player's.
    ///
    /// Only the map says these numbers. A system named anywhere else - a construction queue, an
    /// economy table, the title of its own page - is being named as a thing to work on rather than as
    /// a place to steer towards, and a coordinate there is noise in front of every other word.
    /// </summary>
    public static class GalaxyCoordinates
    {
        /// <summary>The pair for a place that never moves - a star, a special node, a pin.</summary>
        public static NodeAnnouncement Part(GalaxyPosition position)
        {
            GalaxyPosition it = position;
            return GraphNodes.LabelPart(() => Text(it));
        }

        /// <summary>The pair for something that MOVES - a fleet under way, a probe, a missile - asked
        /// afresh each time the node is read. Not watched: a fleet crossing a lane changes its pair
        /// every few frames, and a cursor resting on it would be told so over and over.</summary>
        public static NodeAnnouncement Part(Func<GalaxyPosition> position)
        {
            return GraphNodes.LabelPart(
                () => position == null ? null : Text(position())
            );
        }

        /// <summary>Where a place is, in the pair of numbers the map says it in.</summary>
        public static string Text(GalaxyPosition position)
        {
            try
            {
                Resolve();
                return MapCoordinates.Text(position.X, position.Y, _origin.X, _origin.Y);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Where the pair "0, 0" is on the map - the empire's home system, or the game's own origin
        /// while there is no home yet.
        ///
        /// For the one caller that has to go the other way: the inspect cursor is held in the spoken
        /// pair, and putting the camera on it means turning that pair back into a place
        /// (<see cref="Screens.GalaxyInspect"/>). Everything else measures FROM here and never needs
        /// to say where here is.
        /// </summary>
        public static GalaxyPosition Origin()
        {
            try
            {
                Resolve();
            }
            catch (Exception)
            {
                // The unshifted origin is the same answer Text falls back to, so the two agree.
            }

            return _origin;
        }

        /// <summary>How far a place is from home along each axis, BEFORE any rounding - what a caller
        /// asking which cell of the map something is in needs, since rounding first would move a thing
        /// standing near a cell boundary into its neighbour.</summary>
        public static void Offsets(GalaxyPosition position, out double east, out double north)
        {
            GalaxyPosition origin = Origin();
            east = position.X - origin.X;
            north = position.Y - origin.Y;
        }

        /// <summary>Let go of the empire the origin was taken from - mod teardown.</summary>
        public static void Forget()
        {
            _empire = null;
            _origin = default(GalaxyPosition);
            _settled = false;
            _frame = -1;
        }

        private static Empire _empire;
        private static GalaxyPosition _origin;
        private static bool _settled;
        private static int _frame = -1;

        private static void Resolve()
        {
            int frame = UnityEngine.Time.frameCount;
            if (frame == _frame)
            {
                return;
            }

            _frame = frame;
            Empire empire = Gui.PlayerEmpire;
            if (!ReferenceEquals(empire, _empire))
            {
                _empire = empire;
                _origin = default(GalaxyPosition);
                _settled = false;
            }

            if (_settled || empire == null)
            {
                return;
            }

            DepartmentOfTheInterior interior = empire.GetAgency<DepartmentOfTheInterior>();
            StarSystemNode home = interior == null ? null : interior.HomeSystemNode;
            if (home != null)
            {
                _origin = home.GalaxyPosition;
                _settled = true;
            }
        }
    }
}
