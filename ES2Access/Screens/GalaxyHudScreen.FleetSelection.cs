using System;
using System.Collections.ObjectModel;
using System.Reflection;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>Selecting a fleet the way the game's own "show me this fleet" does, including the
    /// next-idle-fleet cycle reached through the turn window's own handles.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>
        /// Take the camera to a fleet and select it.
        ///
        /// A fleet PARKED somewhere goes through the turn window's own routine for exactly that: it
        /// knows to wait for the galaxy view to finish coming back before it hands the fleet to the
        /// cursor, and getting that order wrong leaves the selection on nothing.
        ///
        /// A fleet UNDER WAY cannot go that way, and the game's routine says so by doing nothing at
        /// all: it finds the fleet's docking slot to aim the camera at, and a fleet between two systems
        /// has none, so it falls through to handing the fleet to a window that is not shown - which
        /// stashes it for the next time the window opens (<c>FleetsScreen.SelectIdleFleet</c> :672-682)
        /// and there never is one, because opening that window is what selecting a fleet does. So a
        /// moving fleet is selected the way every other "show me this fleet" in the game does it
        /// (<c>MilitaryScreen</c>, <c>NamedShipInfoPanel</c>): hand its cursor target to the selection,
        /// swap in the garrison cursor, and ask the camera for the fleet - in that order, because the
        /// panel's own visibility is gated on that cursor.
        /// </summary>
        /// <summary>The same selection a fleet's own node makes, for the one other thing on this page
        /// that names a fleet without walking to it (<see cref="GalaxyInspect"/>).</summary>
        internal static void SelectFleet(Fleet fleet)
        {
            Select(fleet);
        }

        /// <summary>
        /// NEXT IDLE FLEET, without the detour through the fleet's berth.
        ///
        /// The game's own button flies the camera to the fleet FIRST and selects afterwards
        /// (<c>EndTurnWindow.SelectIdleFleet</c> :1387-1411: a docked fleet gets
        /// <c>RequestGalaxyOverviewViewLevel(slot.position)</c> and then a coroutine that waits for the
        /// overview level before handing the fleet to the cursor). On this page that is one camera move
        /// too many: the flight lands on the docking slot, and then the cursor arriving on the fleet's
        /// row asks for the star's own framing and the picture moves a second time. Owner-reported
        /// 2026-08-26 as "the camera centres on the fleet, before the mod's reconciliation moves it to
        /// the star system's orbital view".
        ///
        /// So the node takes the route itself, in the order the page uses for everything else: the
        /// cursor is sent to the fleet's row through the page's ONE landing
        /// (<see cref="GoTo"/>) with the camera left to the row's own focus
        /// (<see cref="FollowPlace"/>) - a docked fleet's star framed, a fleet under way slid to its own
        /// point - and only then is the fleet selected, with no camera request of its own
        /// (<see cref="SelectSeated"/>). One move, and it is the move the row would have made anyway.
        ///
        /// The game's own cycle is still what picks the fleet (<c>GetNextIdleFleet</c>, which advances
        /// the window's counter), so a keyboard press and a mouse click walk the same fleets in the same
        /// order. Answers false only where that cycle cannot be reached at all, which is the caller's
        /// signal to press the button the old way.
        /// </summary>
        internal bool GoToNextIdleFleet()
        {
            EndTurnWindow window = TurnWindow();
            if (window == null || NextIdleFleetOf == null)
            {
                return false;
            }

            try
            {
                Fleet fleet = NextIdleFleetOf.Invoke(window, null) as Fleet;
                // Nothing to go to, a mode waiting for a target (a click selects nothing while one is
                // up), or a fleet the map refuses to select: consumed and silent, exactly as the fleet's
                // own row is (<see cref="Select"/>).
                if (fleet == null || CursorTargeting.Aiming || !FleetPresence.Selectable(fleet))
                {
                    return true;
                }

                MapTarget target;
                if (TargetFor(fleet, out target))
                {
                    GoTo(target, MapCamera.None);
                }

                SelectSeated(fleet);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: going to the next idle fleet threw: " + e);
                return true;
            }
        }

        /// <summary>
        /// Select a fleet where the camera is already showing it - the selection half of
        /// <see cref="GoToNextIdleFleet"/>, with every camera request taken out.
        ///
        /// A DOCKED fleet is the pair of calls the game's own coroutine makes once its flight has
        /// arrived (<c>EndTurnWindow.SelectFleetWhenViewReady</c>): hand the berth to the cursor, which
        /// is what opens the fleet panel, then tell the panel which of the berth's fleets was meant.
        /// Measured 2026-08-26 with the camera already framing the fleet's star: the panel opened, the
        /// docking cursor swapped in, and the camera stayed bit-identical for 1.2 s - the overview wait
        /// the game's coroutine exists for is a wait for the flight this route never starts.
        ///
        /// A fleet with no berth - one under way - has no docking slot to hand over, and the game's own
        /// routine falls through to a call that only STASHES the fleet for a window that is never
        /// shown. That case keeps the map's own selection (<see cref="SelectOnMap"/>), whose camera
        /// request is aimed at the fleet the landing is already going to.
        /// </summary>
        private static void SelectSeated(Fleet fleet)
        {
            try
            {
                // The map's own selection asks the camera for the fleet through the very call the mod
                // watches for the GAME sending the player somewhere - and the player is already being
                // sent there, by the landing above (<see cref="GalaxyLocate.Suppressed"/>).
                GalaxyLocate.Suppressed = true;
                if (FleetOrders.Orbit(fleet) != null)
                {
                    Amplitude.Unity.View.ICursorService cursors =
                        Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                    IVisibleDockingSlotRepositoryService slots =
                        Amplitude.Unity.Framework.Services.GetService<IVisibleDockingSlotRepositoryService>();
                    DockingSlotCursorTarget berth =
                        slots == null ? null : slots.GetDockingSlotWithFleet(fleet);
                    global::FleetsScreen panel = Gui.GuiServiceAvailable
                        ? Gui.GuiService.GetWindow<global::FleetsScreen>(false)
                        : null;
                    if (berth != null && cursors != null && panel != null)
                    {
                        cursors.Select(berth);
                        panel.SelectIdleFleet(fleet);
                        return;
                    }

                    if (panel != null)
                    {
                        // The game's own fall-back for a fleet it cannot find a berth for, kept so that
                        // an orbiting fleet the map is drawing no slot for behaves as the button does.
                        Log.Warn(
                            "galaxy: the next idle fleet is in orbit and the map draws no berth for it"
                                + " - falling back to the game's own hand-over"
                        );
                        panel.SelectIdleFleet(fleet);
                        return;
                    }
                }

                SelectOnMap(fleet);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting the fleet the camera is already showing threw: " + e);
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }
        }

        private static void Select(Fleet fleet)
        {
            try
            {
                // While the map is waiting for a target, a click selects NOTHING - every targeting
                // cursor turns selection off (<c>ValidateSelection</c> false) and keeps the click for
                // its own confirm. So this key is consumed and silent here rather than swapping in the
                // garrison cursor, which would cancel the mode the player is in the middle of
                // (<see cref="CursorTargeting"/>).
                if (CursorTargeting.Aiming)
                {
                    return;
                }

                // The map's own cursor target refuses an automated fleet outright
                // (<see cref="FleetPresence.Selectable"/>), so there is nothing to hand the selection
                // to. Said here as well as at the node, because the inspect cursor's Enter and the
                // scanner's fallback both come in through this door with no node in between.
                if (!FleetPresence.Selectable(fleet))
                {
                    return;
                }

                // Both routes below ask the camera for the fleet through the very call the mod watches
                // for the GAME sending the player somewhere (<see cref="GalaxyLocate"/>) - but the
                // cursor is already on this fleet, because this is the player's own key on its node.
                GalaxyLocate.Suppressed = true;
                if (FleetOrders.Orbit(fleet) != null)
                {
                    EndTurnWindow window = TurnWindow();
                    if (window != null && SelectIdleFleet != null)
                    {
                        SelectIdleFleet.Invoke(window, new object[] { fleet });
                        return;
                    }
                }

                SelectOnMap(fleet);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting a fleet threw: " + e);
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }
        }

        /// <summary>Select a fleet wherever it is standing on the map, and take the camera to it.
        /// </summary>
        private static void SelectOnMap(Fleet fleet)
        {
            GalaxyFleet galaxyFleet = OnMap(fleet);
            Amplitude.Unity.View.ICursorService cursors =
                Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
            if (galaxyFleet == null || galaxyFleet.CursorTarget == null || cursors == null)
            {
                return;
            }

            cursors.Select(galaxyFleet.CursorTarget);
            cursors.ChangeCursor(typeof(GalaxyGarrisonCursor), galaxyFleet);
            Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(fleet);
        }

        /// <summary>The map's own object for a fleet - the thing that carries its cursor target. The
        /// game keeps a repository of the ones it is drawing, which is the same list its other
        /// "show me this fleet" routes look through.</summary>
        private static GalaxyFleet OnMap(Fleet fleet)
        {
            IVisibleGalaxyFleetRepositoryService repository =
                Amplitude.Unity.Framework.Services.GetService<IVisibleGalaxyFleetRepositoryService>();
            if (repository == null || fleet == null)
            {
                return null;
            }

            ReadOnlyCollection<GalaxyFleet> fleets = repository.GalaxyFleets;
            for (int i = 0; i < fleets.Count; i++)
            {
                if (fleets[i] != null && fleets[i].Fleet != null && fleets[i].Fleet.GUID == fleet.GUID)
                {
                    return fleets[i];
                }
            }

            return null;
        }

        // The window keeps its "go to this fleet" routine to itself, and it is the only place the
        // whole recipe - dock slot, camera, view level, cursor, fleet panel - is written down.
        private static readonly MethodInfo SelectIdleFleet = GameHandlers.Method(
            typeof(EndTurnWindow),
            "SelectIdleFleet",
            new Type[] { typeof(Fleet) }
        );

        // The cycle itself - which fleet the button would have gone to, and the counter it advances, so
        // that the key and the click walk the same fleets in the same order
        // (<see cref="GoToNextIdleFleet"/>).
        private static readonly MethodInfo NextIdleFleetOf = GameHandlers.Method(
            typeof(EndTurnWindow),
            "GetNextIdleFleet",
            Type.EmptyTypes
        );
    }
}
