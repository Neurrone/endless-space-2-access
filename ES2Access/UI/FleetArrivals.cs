using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Util;
using HarmonyLib;

namespace ES2Access.UI
{
    /// <summary>
    /// A fleet of the player's reaching where it was sent, said out loud - because the game says
    /// nothing at all. The map shows the lozenge at rest and the fleet list drops its "arrives in N
    /// turns" clause, and that is the whole of the game's announcement.
    ///
    /// The one moment every journey passes through is <c>Fleet.OnGoToEnd</c> (<c>Fleet</c>
    /// :1587-1590), reached from <c>GoToFleetAction.ClientFinalize</c> (:258-268). EVERY ending goes
    /// through it, not just arrival: <c>EntityAction.ClientExecute</c> (:467-492) runs
    /// <c>ClientCancel</c> into <c>WaitingForFinalization</c> and then <c>ClientFinalize</c>, so a
    /// cancelled route, a route replaced under the fleet, and an interception all finalize the same
    /// way. So arriving is told from the rest exactly as the game itself tells it
    /// (<c>GoToFleetAction.ClientUpdate</c> :307-311): the fleet is standing ON the path's
    /// destination.
    ///
    /// That test has to be made BEFORE the method runs, because the first thing the method does is
    /// throw the path away (<c>SetPath(null)</c>) - hence a prefix, not a postfix. The interception
    /// flag is safe either way and was measured to be: <c>HasBeenIntercepted</c> is cleared in
    /// <c>ClientInitialize</c> (:271-279), which is the START of the NEXT journey, so at the end of
    /// this one it still says what happened during it.
    ///
    /// The hook only remembers; the raising happens from the pump (<see cref="Tick"/>), which is
    /// where the mod's own event goes on the game's bus and comes back as a notification the player
    /// hears. A fleet stopped short is NOT raised here - <see cref="FleetRouteWatch"/> already
    /// watches the interception flag and catches it whether a move order was running or not.
    /// </summary>
    internal static class FleetArrivals
    {
        private struct Arrival
        {
            public Fleet Fleet;

            public GameNode At;
        }

        private static readonly ModPatch Patches = new ModPatch("fleetarrivals", "fleet arrivals");
        private static readonly List<Arrival> _arrived = new List<Arrival>(2);

        /// <summary>Whether the patch is in place - what the teardown check reads.</summary>
        public static bool Installed
        {
            get { return Patches.Installed; }
        }

        /// <summary>Who is patching the journey's end right now (see <see cref="ModPatches"/>).
        /// </summary>
        internal static string[] Owners()
        {
            return ModPatches.Owners(Ending(), true);
        }

        private static MethodInfo Ending()
        {
            return AccessTools.Method(typeof(Fleet), "OnGoToEnd", Type.EmptyTypes);
        }

        public static void Install()
        {
            Patches.Install(
                patch => patch.Prefix(Ending(), typeof(FleetArrivals), "JourneyEnding")
            );
        }

        public static void Remove()
        {
            Patches.Remove();
            _arrived.Clear();
        }

        /// <summary>Put whatever arrived since the last frame on the game's bus. From the pump, so
        /// the notification it becomes is spoken in the same frame.</summary>
        public static void Tick()
        {
            if (_arrived.Count == 0)
            {
                return;
            }

            List<Arrival> landed = new List<Arrival>(_arrived);
            _arrived.Clear();
            for (int i = 0; i < landed.Count; i++)
            {
                try
                {
                    Fleet fleet = landed[i].Fleet;
                    if (fleet == null || fleet.IsDestroyed)
                    {
                        continue;
                    }

                    Empire empire = Gui.PlayerEmpire;
                    if (empire == null || !ReferenceEquals(fleet.Empire, empire))
                    {
                        continue;
                    }

                    ModNotifications.Raise(
                        new EventModFleetArrived(empire, fleet, landed[i].At)
                    );
                }
                catch (Exception e)
                {
                    Log.Warn("fleets: announcing an arrival threw: " + e);
                }
            }
        }

        /// <summary>Runs inside the game's own action pipeline: decides and remembers, and does
        /// nothing else.</summary>
        private static void JourneyEnding(Fleet __instance)
        {
            try
            {
                Fleet fleet = __instance;
                if (fleet == null || fleet.IsDestroyed || fleet.HasBeenIntercepted)
                {
                    return;
                }

                Empire empire = Gui.PlayerEmpire;
                if (empire == null || !ReferenceEquals(fleet.Empire, empire))
                {
                    return;
                }

                GalaxyPath path = fleet.Path;
                if (path == null || fleet.Position.NodePosition != path.Destination)
                {
                    // A route cancelled, replaced, or ended anywhere but at its far end. Both of
                    // those already have words of their own (FleetRouteWatch), and neither is an
                    // arrival.
                    return;
                }

                _arrived.Add(new Arrival { Fleet = fleet, At = fleet.GetGameNode() });
            }
            catch (Exception e)
            {
                Log.Warn("fleets: noticing an arrival threw: " + e);
            }
        }
    }
}
