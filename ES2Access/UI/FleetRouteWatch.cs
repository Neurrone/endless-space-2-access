using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// A journey the game called off, said out loud - because the game says nothing at all.
    ///
    /// A route given to a fleet can be destroyed without the player doing anything. Flying into a
    /// system somebody has a citadel in INTERCEPTS the fleet: its movement is set to spent and its move
    /// order is terminated where it stands (<c>Citadel</c> :195-222 sets <c>HasBeenIntercepted</c> and
    /// <c>DepartmentOfLabour.OnFleetIntercepted</c> terminates every action that says
    /// <c>TerminateOnInterception</c>, which a GoTo does - <c>GoToFleetAction</c> :44). And a route
    /// whose next hop has become impossible - a border closed, a lane lost - is cancelled outright
    /// mid-flight (<c>GoToFleetAction.TryToMoveFleet</c> :352-360 posts
    /// <c>OrderCancelEntityAction</c>). Both leave the fleet sitting still with no path, and the only
    /// sign of either is a status ICON on the fleet's own lozenge (<c>GuiFleetStatus</c>) - a picture,
    /// on a map the player may not be looking at.
    ///
    /// The game's own event stream is NOT the watch here, and that is measured rather than assumed:
    /// <c>EventFleetGotInterceptedByAnEnemy</c> is raised by <c>GuardEmpireLocalAction</c> :605 alone,
    /// so a citadel interception - the common one - raises nothing at all, and there is no cancelled-
    /// route event of any kind. What both endings DO have in common is the fleet's path going away
    /// (<c>Fleet.OnGoToEnd</c>), which is what is watched.
    ///
    /// The whole difficulty is that ARRIVING ends a path too, and an arrival is not news. So the
    /// destination is remembered while the path is still there, and a path that goes away with the
    /// fleet standing at that destination is a journey that finished. The remaining case is a route
    /// REPLACED - the player sending the same fleet somewhere else, which clears the old path before
    /// setting the new one - so a vanished path is held for a moment before it is believed.
    ///
    /// Everything here is rebuilt from the live game (<see cref="Baseline"/>), so a hot reload or a
    /// loaded save starts from what is on the screen and announces nothing about state it was not
    /// there for.
    /// </summary>
    public sealed class FleetRouteWatch
    {
        /// <summary>How long a fleet must stand with no path before the path is believed to be gone.
        /// Replacing a route clears the old path and sets the new one, and the two do not always land in
        /// the same frame; a third of a second is far longer than that gap and far shorter than anything
        /// a player would notice.</summary>
        private const int SettleFrames = 20;

        private sealed class Watched
        {
            public bool HadPath;

            public NodePosition Destination;

            public bool Intercepted;

            /// <summary>Frames the path has been gone for, or -1 while there is nothing to settle.
            /// </summary>
            public int Missing;

            /// <summary>Seen on this pass - anything not seen has left the game and is dropped without a
            /// word, because a fleet that is gone is somebody else's news.</summary>
            public bool Present;
        }

        private readonly Dictionary<ulong, Watched> _fleets = new Dictionary<ulong, Watched>();

        private readonly List<string> _said = new List<string>(2);

        /// <summary>Start from what the game is holding now, saying nothing about any of it.</summary>
        public void Baseline()
        {
            _fleets.Clear();
            Scan(false);
        }

        /// <summary>Let go of everything. The next baseline starts over rather than comparing against a
        /// game the mod was not watching.</summary>
        public void Forget()
        {
            _fleets.Clear();
        }

        /// <summary>
        /// Queued, never interrupting: this is news the player did not ask for, and it arrives while
        /// they are reading something they did.
        /// </summary>
        public void Tick()
        {
            try
            {
                _said.Clear();
                Scan(true);
                for (int i = 0; i < _said.Count; i++)
                {
                    Voice.Say(_said[i], false);
                }
            }
            catch (Exception e)
            {
                Log.Warn("fleets: watching the routes threw: " + e);
            }
        }

        private void Scan(bool announce)
        {
            Empire empire = Gui.PlayerEmpire;
            DepartmentOfDefense defense =
                empire == null ? null : empire.GetAgency<DepartmentOfDefense>();
            IList<Fleet> fleets = defense == null ? null : defense.Fleets;
            if (fleets == null)
            {
                _fleets.Clear();
                return;
            }

            foreach (KeyValuePair<ulong, Watched> pair in _fleets)
            {
                pair.Value.Present = false;
            }

            for (int i = 0; i < fleets.Count; i++)
            {
                Fleet fleet = fleets[i];
                if (fleet == null || fleet.IsDestroyed)
                {
                    continue;
                }

                ulong key = fleet.GUID.ToUInt64(null);
                Watched watched;
                bool known = _fleets.TryGetValue(key, out watched);
                if (!known)
                {
                    watched = new Watched { Missing = -1 };
                    _fleets[key] = watched;
                }

                watched.Present = true;
                Look(fleet, watched, announce && known);
            }

            Drop();
        }

        private void Look(Fleet fleet, Watched watched, bool announce)
        {
            GalaxyPath path = fleet.Path;
            bool intercepted = fleet.HasBeenIntercepted;
            if (intercepted && !watched.Intercepted && announce)
            {
                Say(
                    fleet,
                    ModStrings.FleetInterceptedAt,
                    ModStrings.FleetIntercepted,
                    FleetRoute.Named(FleetOrders.Orbit(fleet))
                );
            }

            watched.Intercepted = intercepted;

            if (path != null)
            {
                watched.HadPath = true;
                watched.Destination = path.Destination;
                watched.Missing = -1;
                return;
            }

            if (!watched.HadPath)
            {
                return;
            }

            // The journey finished: the fleet is standing where it was going. Nothing to say - the
            // arrival is what the player asked for, and the game shows them the fleet at rest.
            if (fleet.Position.NodePosition == watched.Destination)
            {
                watched.HadPath = false;
                watched.Missing = -1;
                return;
            }

            // Interception has already been said for what it is; the route dying is what interception
            // MEANS, not a second thing that happened.
            if (watched.Intercepted)
            {
                watched.HadPath = false;
                watched.Missing = -1;
                return;
            }

            watched.Missing = watched.Missing < 0 ? 1 : watched.Missing + 1;
            if (watched.Missing < SettleFrames)
            {
                return;
            }

            watched.HadPath = false;
            watched.Missing = -1;
            if (!announce)
            {
                return;
            }

            Say(
                fleet,
                ModStrings.FleetRouteCancelledTo,
                ModStrings.FleetRouteCancelled,
                FleetRoute.Named(Place(watched.Destination))
            );
        }

        /// <summary>The sentence that names the place, or the one that does not - a place the map has not
        /// named is not named here either, exactly as a starlane running into the dark is not.</summary>
        private void Say(Fleet fleet, string withPlace, string without, string place)
        {
            _said.Add(
                place == null
                    ? ModStrings.Format(without, fleet.LocalizedName)
                    : ModStrings.Format(withPlace, fleet.LocalizedName, place)
            );
        }

        private static GameNode Place(NodePosition position)
        {
            try
            {
                IPositioningService positioning =
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
                return positioning == null ? null : positioning.GetGameNode(position);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Drop()
        {
            List<ulong> gone = null;
            foreach (KeyValuePair<ulong, Watched> pair in _fleets)
            {
                if (!pair.Value.Present)
                {
                    if (gone == null)
                    {
                        gone = new List<ulong>(2);
                    }

                    gone.Add(pair.Key);
                }
            }

            for (int i = 0; gone != null && i < gone.Count; i++)
            {
                _fleets.Remove(gone[i]);
            }
        }
    }
}
