using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>Where the cursor is PUT after the game moves on the player's behalf - a probe mode
    /// arming, a fleet action finishing, an arrival or a pointer's pick taking the map in on a
    /// star, a bookmark jump landing inside a system.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>
        /// Where the player is put when the launch-probe mode is armed: on the first of the sixteen
        /// bearings that mode offers (<see cref="AddProbeDirections"/>), with the acting fleet's system
        /// and that group both opened to get there.
        ///
        /// The button that arms the mode is on the fleet panel, and arming takes the whole panel off the
        /// screen (the game draws it for the garrison cursor alone) - so the control the player pressed
        /// is gone and the cursor is left wherever the rebuild's reconciliation puts it. That was the
        /// acting fleet's own system only when the player happened to be standing in that branch;
        /// having walked anywhere else in between, they were left with the mode up, its one keyboard
        /// control several stops away, and nothing saying where (owner-reported). So the mode seats the
        /// cursor itself, from wherever it was.
        ///
        /// Nothing is spoken here. The landing announces itself through the same path every focus
        /// change goes through, naming the group it entered and the bearing it is on, which is the
        /// whole of what there is to say.
        ///
        /// Watched by the FLEET the mode is armed for rather than by a bare flag, so re-arming - the
        /// same fleet after a cancel, or a second fleet - seats again, and a mode that simply goes on
        /// being up seats nothing.
        /// </summary>
        private void FollowProbeArming()
        {
            try
            {
                Fleet fleet = ArmedProbeFleet();
                if (ReferenceEquals(fleet, _armedProbe))
                {
                    return;
                }

                Fleet was = _armedProbe;
                StarSystemNode wasAt = _armedProbeAt;
                string wasGroup = _armedProbeGroup;
                _armedProbe = fleet;
                _armedProbeAt = null;
                _armedProbeGroup = null;
                if (fleet == null)
                {
                    SeatAfterProbeMode(was, wasAt, wasGroup);
                    return;
                }

                StarSystemNode node = FleetOrders.Orbit(fleet) as StarSystemNode;
                if (node == null)
                {
                    return;
                }

                // The branch first, the group inside it second, the cursor last - the order the frame
                // applies them in (<see cref="Arrive"/>): both expansions belong to the build that
                // declares the bearing the cursor is being sent to.
                string place = SystemKey(node);
                _armedProbeAt = node;
                _armedProbeGroup = place + "/launch";
                OpenPlace(node);
                _pendingExpand.Add(ControlId.Structural(place + "/launch"));
                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.FocusNode(ControlId.Structural(place + "/launch/0"));
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: seating the probe's launch directions threw: " + e);
            }
        }

        /// <summary>The fleet the launch-probe mode is waiting on, or null while no probe mode is up.
        /// </summary>
        private static Fleet ArmedProbeFleet()
        {
            ProbeLaunchingCursor cursor = CursorTargeting.ArmedProbe;
            return cursor == null ? null : cursor.ProbeOriginFleet;
        }

        /// <summary>
        /// Where the player is put when letting go of the selection closes the panel they were
        /// standing in: the fleet's own row in the tree, the place the selection was ABOUT.
        ///
        /// Escape with a fleet selected deselects it, and the panel's three stops vanish with the
        /// selection - so a cursor on a fleet line, a ship or an action is on a node the next build no
        /// longer declares, and reconciliation's nearest-survivor fallback walks the old order backward
        /// onto whatever row happened to precede the panel (measured 2026-08-25: the map stop's last
        /// drifting probe, a place the player never was). The fleet still has a row, so the cursor goes
        /// there - or to its system's when the fleet itself is gone (a disband).
        ///
        /// Only when the panel closed into the PLAIN map cursor, because that is what tells "the
        /// selection was let go" from the other ways the panel leaves: a targeting mode closing it
        /// seats its own cursor (<see cref="FollowProbeArming"/>), a zoom-in action flies into the
        /// system (<see cref="SeatAfterFleetAction"/>), and this must overwrite neither landing.
        ///
        /// Made from the panel OR from the fleet's own row, and from nowhere else. Selecting from the
        /// row never moved the cursor, so there is nothing to hand back - but the handover is still
        /// made, onto the row the player is already standing on, because it is how this page says "the
        /// cursor is placed here" and the camera follows a placement wherever it is made
        /// (<see cref="OnFocusVisual"/>). Without it the Escape that closed the panel left the camera
        /// on the docking slot the game had framed for the selection, with the system unfocused and
        /// its orbital cards gone, until the player pressed an arrow (owner-reported 2026-08-26). A
        /// player reading the HUD when they let go has lost nothing, is reading nothing on the map,
        /// and is left exactly where they are.
        /// </summary>
        private void FollowSelectionEnd(Fleet released)
        {
            if (released == null
                || !(Gui.GetCursor() is GalaxyCursor)
                || !(CursorInFleetPanel() || CursorAtFleetRow(released)))
            {
                return;
            }

            SeatOnFleet(released, null);
        }

        /// <summary>Whether the cursor is already standing exactly where a seat would put it - the
        /// "never left it" half of the question above, asked of the same index the seat aims with
        /// (<see cref="SeatOnFleet"/>) so the two cannot disagree about which row is the fleet's.
        /// </summary>
        private bool CursorAtFleetRow(Fleet fleet)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphNode standing = navigator == null ? null : navigator.CurrentNode;
            if (standing == null || standing.Id == null)
            {
                return false;
            }

            try
            {
                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (ReferenceEquals(sites[i].Fleet, fleet))
                    {
                        return standing.Id.Equals(sites[i].Node);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking whether the cursor is on a fleet's own row threw: " + e);
            }

            return false;
        }

        /// <summary>Whether the cursor is standing in one of the panel's three stops - the question
        /// every one of these handovers asks, because a player who was reading the map or the HUD when
        /// the panel went has lost nothing and must be left where they are.</summary>
        private static bool CursorInFleetPanel()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphNode standing = navigator == null ? null : navigator.CurrentNode;
            object stop = standing == null ? null : standing.StopKey;
            return FleetPanel.ManagementStop.Equals(stop)
                || FleetPanel.ShipsStop.Equals(stop)
                || FleetPanel.ActionsStop.Equals(stop);
        }

        /// <summary>
        /// Put the cursor on a fleet's own row on the map - the place the panel that has just gone was
        /// ABOUT - or on its system's row when the fleet itself is no longer drawn (a disband, a fleet
        /// lost). Answers whether it aimed at anything.
        ///
        /// The one seat every fleet-panel handover shares: the selection being let go
        /// (<see cref="FollowSelectionEnd"/>), a targeting mode ending under the cursor
        /// (<see cref="FollowProbeArming"/>), and the panel being taken away by a screen drawn over
        /// the map (<see cref="_releasedAcross"/>).
        ///
        /// It says nothing about the camera, and none of the handovers do: seating the cursor IS a
        /// placement, and the page's one camera rule answers every placement alike
        /// (<see cref="OnFocusVisual"/>). So the camera comes back in on the place the player is left
        /// reading - or stays where the player put it by hand, which is the record's decision to make
        /// (<see cref="Showing"/>) and not a handover's.
        ///
        /// <paramref name="home"/> is the system to fall back to where the caller knows it from
        /// before the fleet went - the fleet's own orbit answers it in every other case.
        /// </summary>
        private bool SeatOnFleet(Fleet fleet, StarSystemNode home)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null || fleet == null)
            {
                return false;
            }

            try
            {
                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (ReferenceEquals(sites[i].Fleet, fleet))
                    {
                        navigator.FocusNode(Reveal(sites[i]));
                        return true;
                    }
                }

                StarSystemNode at = home != null ? home : FleetOrders.Orbit(fleet) as StarSystemNode;
                ControlId id = SystemId(at);
                if (id == null)
                {
                    return false;
                }

                navigator.FocusNode(id);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: seating the cursor on a fleet's own row threw: " + e);
                return false;
            }
        }

        /// <summary>
        /// The fleet the panel was up for when this page was taken away with the panel still open, or
        /// null the rest of the time.
        ///
        /// A full screen drawn over the map - the military screen, the academy, anything the player
        /// opens from the HUD - pops this page, and opening one also force-swaps the map to the plain
        /// cursor and clears the fleet selection (<c>GuiManager.cs:1783-1795</c>). So the panel closes
        /// while nobody is watching: the close frame the watch would have handed the fleet over on
        /// happens with this screen off the stack, and coming back left the cursor wherever
        /// reconciliation put it - measured 2026-08-26 on an unrelated fleet's row three stops away.
        /// The release is therefore CAUGHT at the pop and answered on the first frame this page is the
        /// focused one again, which makes the trip through the screen invisible: the player comes back
        /// where letting the fleet go would have put them anyway. It is dropped unanswered when the
        /// panel came back up with the page - then nothing was taken from under the cursor.
        /// </summary>
        private Fleet _releasedAcross;

        /// <summary>Answer a release caught at the pop, on the first frame back on the page - not at
        /// the push itself, where the navigator has not yet been handed this screen and a landing
        /// asked for there would belong to the screen the player was leaving.</summary>
        private void FollowSelectionEndAcross()
        {
            Fleet released = _releasedAcross;
            _releasedAcross = null;
            if (released != null && !_fleetPanel.Available())
            {
                SeatOnFleet(released, null);
            }
        }

        /// <summary>
        /// Where the player is put when the launch-probe mode ENDS - cancelled, or spent on the last
        /// charge: back on the acting fleet's own row, the same place letting go of the selection puts
        /// them (<see cref="SeatOnFleet"/>).
        ///
        /// Only while the cursor is standing among the bearings themselves, because those are the
        /// nodes the mode's end takes away: the group is declared for as long as the mode is up and
        /// for no longer (<see cref="AddProbeDirections"/>), so a cursor inside it is about to be left
        /// on nothing and reconciliation would walk it backwards onto whatever row happened to be
        /// drawn last at that system. A cursor anywhere else is on a node that SURVIVES the mode
        /// ending, and nothing may move it - the same limit the selection-end seat keeps.
        ///
        /// Both the fleet and the group are read from what was remembered at ARMING time: by the frame
        /// the mode ends the cursor object is gone, and with it the only live route to either.
        /// </summary>
        private void SeatAfterProbeMode(Fleet was, StarSystemNode at, string group)
        {
            if (was == null || group == null)
            {
                return;
            }

            GraphNavigator navigator = ModEntry.Navigator;
            GraphNode standing = navigator == null ? null : navigator.CurrentNode;
            ControlId id = standing == null ? null : standing.Id;
            string key = id == null ? null : id.StructuralKey as string;
            if (key == null || (key != group && !key.StartsWith(group + "/")))
            {
                return;
            }

            SeatOnFleet(was, at);
        }

        /// <summary>The fleet the probe mode was armed for when it was last looked at - instance state,
        /// so it is reload-safe and each page keeps its own.</summary>
        private Fleet _armedProbe;

        /// <summary>The system that fleet was launching from, and the key of the group of bearings
        /// offered there - both remembered from the frame the mode was armed, for the frame it ends
        /// (<see cref="SeatAfterProbeMode"/>).</summary>
        private StarSystemNode _armedProbeAt;

        private string _armedProbeGroup;

        // ---- the fleet actions that only bring the camera in ----

        /// <summary>
        /// Which control INSIDE the fleet's own system a fleet action's button is really asking for.
        ///
        /// Nine of the game's fleet actions order nothing when they are pressed: Colonize, Super
        /// Colonize, Destroy Planet, Expedition, Launch Mining Probe and Reclaim Mothership all just
        /// select the fleet's system and fly the camera in (<c>FleetActionButtonColonize.OnClick</c>
        /// and its four siblings; <c>FleetActionToggleReclaimMothership.OnToggle</c>), and so do the
        /// juggernaut's three planet-construction toggles - Terraform, Restore and Reduce Anomaly,
        /// which share one <c>OnToggle</c> on <c>EmpireLocalActionTogglePlanetConstruction</c>
        /// (:23-38) and are told apart by the action DEFINITION each was loaded with. The reason is
        /// the same for all nine: the real order is a control the map draws once it is there - a
        /// planet's own colonize, destroy, terraform, restore or reduce-anomaly button, a curiosity in
        /// orbit, a probe site, the wreck. <see cref="None"/> is every other action: the ones that
        /// post an order themselves and the ones that arm a targeting cursor.
        ///
        /// A toggle whose work is ALREADY under way cancels it instead of zooming (the same branch in
        /// both <c>OnToggle</c>s, and for the three juggernaut actions the cancel raises a
        /// confirmation box). The seat is armed either way, exactly as it already is for Reclaim
        /// Mothership: the cancel simply leaves a target that is never drawn and the wait runs out.
        /// </summary>
        public enum SeatTarget
        {
            None,
            Colonize,
            Destroy,
            Expedition,
            MiningProbe,
            Wreck,
            Terraform,
            Restore,
            ReduceAnomaly,
        }

        /// <summary>Which of the six, if any, this action button is - asked of the GAME's own control
        /// class rather than of the definition name, because that is what decides the click's
        /// behaviour. Super Colonize is a subclass of Colonize and lands on the same card button, which
        /// is the game's own arrangement (<c>PlanetLabel_SystemOrbital.RefreshColonizationButton</c>
        /// drives one button from both).</summary>
        public static SeatTarget SeatTargetOf(FleetActionItem item)
        {
            try
            {
                FleetActionControl control =
                    item == null ? null : item.GetComponent<FleetActionControl>();
                if (control is FleetActionButtonColonize)
                {
                    return SeatTarget.Colonize;
                }

                if (control is FleetActionButtonDestroyPlanet)
                {
                    return SeatTarget.Destroy;
                }

                if (control is FleetActionButtonExpedition)
                {
                    return SeatTarget.Expedition;
                }

                if (control is FleetActionButtonLaunchMiningProbe)
                {
                    return SeatTarget.MiningProbe;
                }

                if (control is FleetActionToggleReclaimMothership)
                {
                    return SeatTarget.Wreck;
                }

                if (control is EmpireLocalActionTogglePlanetConstruction)
                {
                    return PlanetConstruction(control);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a fleet action's control class threw: " + e);
            }

            return SeatTarget.None;
        }

        /// <summary>
        /// Which of the juggernaut's three planet-construction actions a toggle is, asked of the
        /// action DEFINITION rather than of the control class - because the class does not answer it.
        ///
        /// Terraform and Restore share one control
        /// (<c>EmpireLocalActionTogglePlanetTerraformation</c>, whose only override is the wording of
        /// the cancel confirmation), so what tells them apart is the definition each item was loaded
        /// with - and that is the same question the CARD asks to decide which of its buttons to draw
        /// (<c>PlanetLabel_SystemOrbital.RefreshTerraformationStatus</c> /
        /// <c>RefreshRestorationStatus</c> / <c>RefreshAnomalyReductionStatus</c>, each fetching its
        /// own <c>Initiateâ€¦EmpireActionFleetActionDefinition</c>). Restoration's definition DERIVES
        /// from terraformation's, so it is tested first or every restore would read as a terraform.
        ///
        /// An unrecognised planet-construction action is <see cref="SeatTarget.None"/>: nothing is
        /// invented about which button it wants, and the camera move it makes is still followed by the
        /// page's own answer to a picked node (<see cref="GalaxyPick"/>).
        /// </summary>
        private static SeatTarget PlanetConstruction(FleetActionControl control)
        {
            EntityActionDefinition definition = control.EntityActionDefinition;
            if (definition is InitiateRestorationEmpireActionFleetActionDefinition)
            {
                return SeatTarget.Restore;
            }

            if (definition is InitiateTerraformationEmpireActionFleetActionDefinition)
            {
                return SeatTarget.Terraform;
            }

            if (definition is InitiateAnomalyReductionEmpireActionFleetActionDefinition)
            {
                return SeatTarget.ReduceAnomaly;
            }

            return SeatTarget.None;
        }

        /// <summary>The phrase such a button appends to its own announcement, or null where it has
        /// nothing to add.</summary>
        public static string SeatPhrase(SeatTarget seat)
        {
            switch (seat)
            {
                case SeatTarget.Colonize:
                    return ModStrings.FleetsActionSeatsColonize;
                case SeatTarget.Destroy:
                    return ModStrings.FleetsActionSeatsDestroy;
                case SeatTarget.Expedition:
                    return ModStrings.FleetsActionSeatsExpedition;
                case SeatTarget.MiningProbe:
                    return ModStrings.FleetsActionSeatsProbeSite;
                case SeatTarget.Wreck:
                    return ModStrings.FleetsActionSeatsWreck;
                case SeatTarget.Terraform:
                    return ModStrings.FleetsActionSeatsTerraform;
                case SeatTarget.Restore:
                    return ModStrings.FleetsActionSeatsRestore;
                case SeatTarget.ReduceAnomaly:
                    return ModStrings.FleetsActionSeatsReduceAnomaly;
            }

            return null;
        }

        /// <summary>
        /// One of the six has just been pressed: open the acting fleet's system and put the cursor on
        /// the control that gives the order.
        ///
        /// Asked for rather than done, and asked for over several frames: the game answers the click by
        /// flying the camera in, and the cards, the curiosities and the wrecks are all drawn by windows
        /// that bind to the system the camera ARRIVES at - none of them exists on the frame the button
        /// was pressed. So the target is looked for every frame until it is there, and the seat itself
        /// goes through the same pending-focus path every other screen-driven landing uses, because the
        /// tree re-declares itself each frame and the row only exists in the build that follows the
        /// branch being opened.
        ///
        /// The branch is opened straight away whatever happens next, so an action whose target the
        /// fixture has nothing to offer still leaves the player in the system the game flew them to.
        /// Nothing is spoken here: the landing announces itself.
        /// </summary>
        public void SeatAfterFleetAction(StarSystemNode system, SeatTarget seat)
        {
            if (system == null || seat == SeatTarget.None)
            {
                return;
            }

            // The camera move this action just made was seen by the map's own watch a moment ago
            // (<see cref="GalaxyPick"/>, the same GalaxyView call a click makes). This seat names a
            // control INSIDE the system, which is the finer answer, so the pick is dropped here rather
            // than left to be stood down frame by frame - the wait below clears _seatTarget on the very
            // frame it lands, and a pick still standing would then take the cursor off it.
            GalaxyPick.Forget();
            _seatSystem = system;
            _seatTarget = seat;
            _seatFrames = SeatWaitFrames;
            OpenPlace(system);
        }

        /// <summary>About five seconds of frames - several times the camera's own flight into a system,
        /// and short enough that a target the game never draws stops being looked for.</summary>
        private const int SeatWaitFrames = 300;

        /// <summary>
        /// How long the answer has to STOP CHANGING before the cursor is sent to it - a third of a
        /// second.
        ///
        /// A card's buttons do not all appear on one frame: the window blanks every one of them when it
        /// binds a planet and its refresh turns back on the ones that apply, so a card that ends up
        /// drawing Colonize and a curiosity draws the curiosity alone for a frame or two first. The row
        /// id is the button's POSITION in the card's action list, so seating on that frame put the
        /// cursor on the curiosity's id - which the very next build handed to Colonize (measured
        /// 2026-08-20: the seat spoke "Signal" and the cursor was reading "Colonize" a frame later).
        /// </summary>
        private const int SeatSteadyFrames = 20;

        private StarSystemNode _seatSystem;
        private SeatTarget _seatTarget;
        private int _seatFrames;
        private ControlId _seatRow;
        private ControlId _seatGroup;
        private int _seatSteady;

        /// <summary>Per frame while a seat is outstanding: the target once the map has settled on
        /// drawing it, the planet branch it hangs in opened in the same breath (the order the build
        /// applies them in), and nothing at all once the wait has run out.</summary>
        private void FollowActionSeat()
        {
            if (_seatTarget == SeatTarget.None)
            {
                return;
            }

            try
            {
                if (GalaxyViewLevels.ChangingLevel)
                {
                    // The camera is still on its way. Nothing the map draws mid-flight is the answer -
                    // the cards belong to wherever the view is leaving - so the frames a flight takes
                    // are not the wait's to spend, and whatever the last frame settled on is dropped
                    // rather than counted towards the steady run.
                    _seatRow = null;
                    _seatSteady = 0;
                    return;
                }

                if (--_seatFrames <= 0)
                {
                    ForgetActionSeat();
                    return;
                }

                ControlId group;
                ControlId row = SeatRow(_seatSystem, _seatTarget, out group);
                if (row == null)
                {
                    _seatRow = null;
                    _seatSteady = 0;
                    return;
                }

                if (row.Equals(_seatRow))
                {
                    _seatSteady++;
                }
                else
                {
                    _seatRow = row;
                    _seatGroup = group;
                    _seatSteady = 1;
                }

                if (_seatSteady < SeatSteadyFrames)
                {
                    return;
                }

                if (_seatGroup != null)
                {
                    _pendingExpand.Add(_seatGroup);
                }

                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.FocusNode(_seatRow);
                }

                ForgetActionSeat();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: seating a fleet action's target threw: " + e);
                ForgetActionSeat();
            }
        }

        private void ForgetActionSeat()
        {
            _seatTarget = SeatTarget.None;
            _seatSystem = null;
            _seatFrames = 0;
            SuspendActionSeat();
        }

        /// <summary>Keep the wait but throw away everything it had settled on: the row it was closing in
        /// on was an index into a card the map is no longer drawing, and the run of frames it had held
        /// steady for says nothing about the map the player will come back to. What survives is the
        /// action, the system and the budget left.</summary>
        private void SuspendActionSeat()
        {
            _seatRow = null;
            _seatGroup = null;
            _seatSteady = 0;
        }

        /// <summary>
        /// The row the map is drawing for this action's target, and the branch it hangs in - both null
        /// until the game has drawn it.
        ///
        /// The index is worked out from the very list the tree builds the row from
        /// (<see cref="OrbitalActions"/>), never guessed from the order the card's buttons are
        /// declared in: which of them are drawn changes with the planet, so a fixed index would name a
        /// different button on the next world.
        /// </summary>
        private static ControlId SeatRow(StarSystemNode node, SeatTarget seat, out ControlId group)
        {
            group = null;
            if (node == null)
            {
                return null;
            }

            string place = SystemKey(node);
            if (seat == SeatTarget.Wreck)
            {
                return FirstWreckRow(node, place);
            }

            PlanetLabel_SystemOrbital[] cards = OrbitalLabels(node);
            if (cards.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < node.Planets.Count; i++)
            {
                PlanetLabel_SystemOrbital card = CardFor(node.Planets[i], cards);
                AgeTransform want = card == null ? null : SeatWidget(card, seat);
                if (want == null)
                {
                    continue;
                }

                List<CardActions.CardAction> actions = OrbitalActions(card);
                for (int j = 0; j < actions.Count; j++)
                {
                    if (!ReferenceEquals(actions[j].Widget, want))
                    {
                        continue;
                    }

                    string key = place + "/planet/" + i;
                    group = ControlId.Structural(key);
                    return ControlId.Structural(key + "/action/" + j);
                }
            }

            return null;
        }

        /// <summary>Which of the card's own controls this action is really after. A button the game is
        /// not drawing is simply not in the card's action list, so no drawn-ness test is needed here -
        /// the search below fails and the next planet is tried.</summary>
        private static AgeTransform SeatWidget(PlanetLabel_SystemOrbital card, SeatTarget seat)
        {
            switch (seat)
            {
                case SeatTarget.Colonize:
                    return AgeWidgets.Transform(card.ColonizeButton);
                case SeatTarget.Destroy:
                    return AgeWidgets.Transform(card.DestroyButton);
                case SeatTarget.MiningProbe:
                    return AgeWidgets.Transform(card.MiningProbeButton);
                case SeatTarget.Expedition:
                    return FirstCuriosity(card);
                // The juggernaut's three. Each card draws its own button for the action it can take
                // on that world, and the card's IN-PROGRESS button is deliberately not offered here:
                // the toggle only zooms when there is no action running, and where one IS running the
                // toggle cancels it and moves no camera at all.
                case SeatTarget.Terraform:
                    return AgeWidgets.Transform(card.TerraformationButton);
                case SeatTarget.Restore:
                    return AgeWidgets.Transform(card.RestorationButton);
                case SeatTarget.ReduceAnomaly:
                    return AgeWidgets.Transform(card.AnomalyReductionButton);
            }

            return null;
        }

        /// <summary>The first curiosity the card is drawing - PAINTED, the same gate
        /// <see cref="AddCuriosities"/> declares them by, because the ring pools its items and retires
        /// a surplus one by fading it rather than hiding it.</summary>
        private static AgeTransform FirstCuriosity(PlanetLabel_SystemOrbital card)
        {
            AgeTransform table = card.PlanetCuriositiesTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return null;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                // Different widget: the first curiosity the card is actually drawing, which is where the pointer goes.
                if (AgeWidgets.Painted(items[i]))
                {
                    return items[i];
                }
            }

            return null;
        }

        /// <summary>The first wreck row this system has, which is always index 0 of the group
        /// <see cref="AddWrecks"/> emits - that list holds the visible items alone, in order.</summary>
        private static ControlId FirstWreckRow(StarSystemNode node, string place)
        {
            WreckedMothershipLabelWindow window = WreckWindow(node);
            AgeTransform table = window == null ? null : window.CuriositiesTable;
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] != null && AgeWidgets.Visible(items[i]))
                {
                    return ControlId.Structural(place + "/wreck/action/0");
                }
            }

            return null;
        }

        // ---- the map taken in on a star by a pointer ----

        /// <summary>Drop a pointer's pick and any seat it armed - the page it was meant for has gone
        /// away, the player has moved the cursor themselves, or something that names a finer place
        /// inside the system has taken the move over. A seat an ARRIVAL armed is left alone: only a
        /// pick's own is cancelled here.</summary>
        private void ForgetPick()
        {
            GalaxyPick.Forget();
            if (_centrePick == null)
            {
                return;
            }

            _centrePick = null;
            _centreSeat = 0;
            _centreSettle = 0;
        }

        // ---- the system the picture is of ----

        /// <summary>Frames the arrival seat is given to find its answer, after the settle above: the
        /// galaxy camera has to be the live one and to have stopped. Also the CAP on how long the
        /// page's arrival announcement is held for it (<see cref="BetweenViews"/>), which is why it is
        /// not the minute-long budget a locate gets - an answer that never comes must cost the player
        /// a moment, not a silence.</summary>
        private const int CentreSeatFrames = 30;

        /// <summary>How long after the page is pushed an activation still counts as this arrival's.
        /// The view level is made current before its own activation runs, so the page can be up a
        /// frame or two before the notice arrives; past that a notice is somebody else's and is
        /// dropped rather than kept for the next visit.</summary>
        private const int ArrivalWindowFrames = 20;

        /// <summary>Frames waited before the camera is asked where it is looking. The game places it
        /// one frame AFTER the page is pushed (measured 2026-08-28: on the frame the page arrives the
        /// camera still reads the position the map had before the system's page was opened, and from
        /// the next frame on it reads the new one), so an answer taken on the arrival frame is the
        /// picture that has just been left. The page's own <see cref="ViewBindFrames"/> is the wait,
        /// and the announcement is held over it - so an arrival names the system the map is showing
        /// once, rather than the one it was showing and then this one.</summary>
        private const int ArrivalSettleFrames = ViewBindFrames;

        private int _centreSeat;

        private int _centreSettle;

        private int _arrivalWindow;

        /// <summary>Start looking for the system the map is showing, and hold what the page is about to
        /// say until the answer is in.</summary>
        private void ArmCentreSeat()
        {
            // The hold itself is <see cref="BetweenViews"/> reading _centreSeat: the page has not yet
            // decided which system it is showing, so it has nothing to say, and the hold ends on the
            // frame the answer arrives - the same frame the seat is asked for, so the arrival announces
            // the seated row once instead of the row it was restored to and then this one.
            _centreSeat = CentreSeatFrames;
            _centreSettle = ArrivalSettleFrames;
        }

        // A "has the player moved the cursor since the page arrived?" stand-down was tried here and
        // taken out again (2026-08-28). It cannot be asked at this level: loading a save while the
        // cursor stands on the map RECONCILES it - the row it was on no longer exists, so the engine
        // walks it up to a survivor - and that is a cursor move nobody made, indistinguishable from a
        // keypress. It stood the seat down on exactly the arrival that needs it most. What legitimately
        // owns the cursor on an arrival is named instead, above: a locate, a fleet-action seat, a fleet
        // panel let go. The window it would have guarded is the twelve frames of
        // <see cref="ArrivalSettleFrames"/>, during which the page is holding its own announcement
        // anyway.

        /// <summary>
        /// Make the tree's cursor describe the system the map is SHOWING, whenever the map came to be
        /// showing it for a reason of the game's own. ONE rule, two triggers: an arrival nobody asked
        /// for (<see cref="GalaxyOverviewEntry"/>) - a save being loaded, coming back out of a system's
        /// management page - and the map being taken in on a star by a POINTER
        /// (<see cref="GalaxyPick"/>, <see cref="ArmPickSeat"/>) - a click, or the wheel past its
        /// deepest step. The two differ only in how the system is NAMED: an arrival has to be asked of
        /// the camera, a pick says so itself.
        ///
        /// PASSIVE where the player is reading something else. A cursor on the HUD is left exactly
        /// where it is and the map stop's remembered position is written instead
        /// (<see cref="GraphNavigator.SeatStop"/>), so a save loads reading the empire's own summary
        /// as it always has and the FIRST landing on the map - Ctrl+G, or Tab round to it - is the
        /// centred system rather than whichever row happens to be declared first. A cursor already
        /// standing on the map is a different question: the page is arriving, so whatever it is
        /// standing on is about to be read out, and reading out a system the map is not showing is the
        /// defect. It follows, and the arrival announces it the ordinary way.
        ///
        /// Either way "already right" means the PLACE agrees, not the row: a cursor inside the centred
        /// system - on one of its planets, its lanes, a fleet parked at it - is reading that system
        /// and is left alone, which is what keeps an excursion to another screen and back from
        /// bouncing the cursor up to the star it was under.
        ///
        /// The picture is asked of the CAMERA and never of the activation's arguments, which do not
        /// answer it: the way out of a management page names the system whose page was open and then
        /// sends the camera to where it was before the page opened (<see cref="GalaxyOverviewEntry"/>
        /// has the measurements). The nearest declared system to the camera's own target is what a
        /// sighted player reads as the centre of the picture, the same rule every other place-naming
        /// on this page uses (<see cref="CentredSystem"/>).
        /// </summary>
        private void FollowCentredSystem()
        {
            if (_arrivalWindow > 0)
            {
                _arrivalWindow--;
                if (_centreSeat <= 0 && GalaxyOverviewEntry.Take())
                {
                    ArmCentreSeat();
                }
            }
            else
            {
                // An activation that reached a page which has been up all along is not an arrival.
                GalaxyOverviewEntry.Forget();
            }

            // The OTHER trigger of the same rule (owner ruling 2026-08-29): the map taken in on a star
            // by a POINTER - a left click, a click on a wreck, or the wheel scrolled in past the
            // deepest step (<see cref="GalaxyPick"/>). No page change, so no arrival window to sit
            // inside; and nothing to ask the camera either, because unlike an activation this one
            // NAMES the system it is sending the camera to. An arrival already being answered wins -
            // it is the bigger change, and it will have moved the cursor to the same kind of place.
            if (_centreSeat <= 0)
            {
                ArmPickSeat();
            }

            if (_centreSeat <= 0)
            {
                return;
            }

            // Everything that names a place of its own beats a picture that merely became true: a
            // "go and look at this" (which lands announced), the seat one of the zoom-in fleet actions
            // is owed across the page change, and a fleet panel let go across it.
            if (
                _locating != null
                || GalaxyLocate.Peek() != null
                || _seatTarget != SeatTarget.None
                || _releasedAcross != null
            )
            {
                _centreSeat = 0;
                _centrePick = null;
                return;
            }

            // The camera is placed the frame AFTER the page arrives (<see cref="ArrivalSettleFrames"/>).
            if (_centreSettle > 0)
            {
                _centreSettle--;
                return;
            }

            // Only the camera is waited for, and only until it has stopped and can say where it is
            // looking. What the TREE has declared deliberately is not waited for: a page arrived at
            // from a save being loaded has not built once - the tutorial popup has the keyboard on the
            // frames that would have built it - and the answer does not need it
            // (<see cref="CentredSystem"/>).
            // ...unless the trigger already said which system, which a pointer's pick does.
            Vector3 at;
            StarSystemNode centred = _centrePick;
            if (centred == null)
            {
                centred = GalaxyViewLevels.CameraSettling
                    || !GalaxyViewLevels.CameraTarget(out at)
                    ? null
                    : CentredSystem(at);
            }

            if (centred == null)
            {
                if (--_centreSeat <= 0)
                {
                    Vector3 last;
                    Log.Warn(
                        "galaxy: the map was arrived at and never said which system it is showing"
                            + " (settling="
                            + GalaxyViewLevels.CameraSettling
                            + " camera="
                            + GalaxyViewLevels.CameraTarget(out last)
                            + ")"
                    );
                }

                return;
            }

            _centreSeat = 0;
            _centrePick = null;
            try
            {
                SeatOnCentredSystem(centred);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: seating the tree on the system the map shows threw: " + e);
            }
        }

        /// <summary>
        /// Take a pointer's pick, if there is one, and arm the same seat on the system it names.
        ///
        /// No settle is waited out: the settle exists to let the camera be PLACED before it is asked
        /// where it is looking, and nothing is asked of the camera here. Anything the map lets a
        /// pointer zoom at that is not a star is counted and no more - there is no other row for the
        /// cursor to stand on.
        /// </summary>
        private void ArmPickSeat()
        {
            GameNode picked = GalaxyPick.Take();
            if (picked == null)
            {
                return;
            }

            _centrePick = picked as StarSystemNode;
            if (_centrePick == null)
            {
                return;
            }

            ArmCentreSeat();
            _centreSettle = 0;
        }

        /// <summary>The system a pointer's pick named, while its seat is outstanding - what makes the
        /// answer below the trigger's own rather than the camera's.</summary>
        private StarSystemNode _centrePick;

        private void SeatOnCentredSystem(StarSystemNode centred)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null)
            {
                return;
            }

            ControlId id = ControlId.For(centred, SystemKey(centred));

            GraphNode standing = navigator.CurrentNode;
            if (standing != null && IsMapStop(standing.StopKey))
            {
                object place;
                bool inside;
                if (Place(standing, out place, out inside) && ReferenceEquals(place, centred))
                {
                    return;
                }

                navigator.FocusNode(id);
                return;
            }

            // Left alone only where the remembered row is BOTH a reading of this system and a row that
            // still exists. A save being loaded takes fleets and planets away under a memory that is
            // still a path into the right system - and a stop whose memory names a row nothing declares
            // falls back to the FIRST row of the whole stop, which is how a correct-looking memory
            // still lands the player in another constellation.
            ControlId remembered = navigator.RememberedStop(SystemStop);
            GraphRender render = navigator.Render;
            bool alive = remembered != null && render != null && render.NodeAt(remembered) != null;
            if (!alive || !Reads(remembered, centred))
            {
                navigator.SeatStop(SystemStop, id);
            }
        }

        /// <summary>
        /// The system a point on the map is a picture OF - the nearest one the tree gives a row to.
        ///
        /// Asked of the galaxy rather than of <see cref="_systems"/>, and by the same gate that list is
        /// built by (a colony of the empire's, or a perceived star), because the one arrival that needs
        /// this most is the one where the page has never been built: a save being loaded pushes the
        /// page under the tutorial popup, which holds the keyboard, so nothing has declared a row yet
        /// when the picture is already on the screen.
        /// </summary>
        private static StarSystemNode CentredSystem(GalaxyPosition at)
        {
            Empire empire = PlayerEmpire();
            if (empire == null || !GameGalaxy.Present())
            {
                return null;
            }

            StarSystemNode nearest = null;
            float best = float.PositiveInfinity;
            foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
            {
                if (!Perceived(node, empire) && !Colonized(node, empire))
                {
                    continue;
                }

                float distance = GalaxyPosition.SqrDistance(node.GalaxyPosition, at);
                if (distance < best)
                {
                    best = distance;
                    nearest = node;
                }
            }

            return nearest;
        }

        /// <summary>Whether the empire holds this system - the half of the tree's own list that is not
        /// the perception gate (an outpost of ours in a system we could not otherwise see is still a
        /// row).</summary>
        private static bool Colonized(StarSystemNode node, Empire empire)
        {
            IColonizedStarSystemRepositoryService colonies =
                Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null || empire == null || node == null)
            {
                return false;
            }

            // The repository's own index rather than a walk of the empire's colony list: it answers
            // this in one lookup, and this is asked per system per build.
            ColonizedStarSystem held;
            return colonies.TryGetValue(empire, node.NodePosition, out held);
        }

        /// <summary>Whether a remembered map position is already a reading of this system - its own row
        /// or anything the tree files under it. Asked of the KEY because the row it names need not be
        /// in the render at all: the memory outlives the build, and on a page arrived at cold there is
        /// no node to walk up from.</summary>
        private static bool Reads(ControlId remembered, StarSystemNode system)
        {
            string key = remembered == null ? null : remembered.StructuralKey as string;
            if (key == null)
            {
                return false;
            }

            string place = SystemKey(system);
            return key == place || key.StartsWith(place + "/", StringComparison.Ordinal);
        }

        // ---- bookmarks (GalaxyBookmarks owns the keys; these are the map's half) ----

        /// <summary>
        /// Take the player INSIDE a system - the landing a bookmark jump makes.
        ///
        /// The cursor goes to the system's FIRST CHILD rather than to its row, which is what brings
        /// the camera all the way in: the page's one camera rule reads a row inside a system as being
        /// in that place and snaps to it, where the system's own row is a place being looked AT from
        /// wherever the camera stands (<see cref="FollowPlace"/>). So a jump leaves the player exactly
        /// where walking in with Right would have.
        ///
        /// It cannot be done on the press. The branch is shut - possibly inside a shut constellation -
        /// so the child does not exist yet and its key is not something this page can compose: what a
        /// system's first child IS depends on what the map is drawing there
        /// (<see cref="AddInside"/>). The branch is asked to open and the landing waits for the build
        /// that opens it (<see cref="FollowBookmarkLanding"/>).
        /// </summary>
        internal void LandInside(StarSystemNode node)
        {
            if (node == null)
            {
                return;
            }

            // The band first, for the reason every other landing forces one
            // (<see cref="EnsureBand"/>): from the two furthest-out levels the map names no system, so
            // the branch this asks for would never open and the jump would move the camera and say
            // nothing - measured. Beyond that the landing keeps its own framing, which is whatever
            // walking in with Right would have given at this distance.
            MapTarget place = MapTarget.Place(node, SystemRow(node), node.GalaxyPosition);
            // And, under a lens that draws no star at all, out of the lens first - the same rule the
            // one landing follows, for the same reason (<see cref="DrawnByTheLens"/>).
            bool leaving = Scanning && !DrawnByTheLens(place);
            if (leaving)
            {
                LeaveTheLens();
            }

            EnsureBand(place, leaving);
            OpenPlace(node);
            _bookmarkLanding = SystemRow(node);
            _bookmarkLandingFrames = BookmarkLandingFrames;
        }

        /// <summary>Put the cursor on the first thing inside the system a jump named, once the build
        /// that opened the branch has declared it. A system with nothing in it - and a branch that
        /// never opens, which is the budget running out - lands on the system's own row instead, which
        /// is the honest answer to "go inside" where there is no inside.</summary>
        private void FollowBookmarkLanding()
        {
            if (_bookmarkLanding == null)
            {
                return;
            }

            GraphNavigator navigator = ModEntry.Navigator;
            GraphRender render = navigator == null ? null : navigator.Render;
            GraphNode node = render == null ? null : render.NodeAt(_bookmarkLanding);
            bool open = node != null && node.Expanded;
            bool spent = --_bookmarkLandingFrames <= 0;
            if (!open && !spent)
            {
                // The branch was asked for on the press and opens on a later build: a system that is
                // merely DECLARED is the shut row the jump was made from, and landing on it would be
                // the jump giving up one frame before it could have gone inside.
                return;
            }

            ControlId landing = _bookmarkLanding;
            _bookmarkLanding = null;
            ControlId child = open ? FirstChild(render, node) : null;
            if (navigator != null)
            {
                navigator.FocusNode(child ?? landing);
            }
        }

        /// <summary>The first thing declared inside a group in this render - declaration order is the
        /// reading order, so the first is the one an arrow key would reach first.
        ///
        /// Asked of ANCESTRY rather than of the direct parent, because a group is free to sort its
        /// children under named levels of its own - a system's are seven regions
        /// (<see cref="AddInside"/>), and a region is a pushed context, so nothing inside an opened
        /// system has the system itself for a parent any more. The direct-parent test answered
        /// nothing there, which left the one landing that reads this - the bookmark jump - on the
        /// system's row instead of inside it, and so left the camera outside.</summary>
        private static ControlId FirstChild(GraphRender render, GraphNode group)
        {
            for (int i = 0; i < render.Order.Count; i++)
            {
                GraphNode node = render.Order[i];
                for (GraphNode walk = node.Parent; walk != null; walk = walk.Parent)
                {
                    if (ReferenceEquals(walk, group))
                    {
                        return node.Id;
                    }
                }
            }

            return null;
        }

        private ControlId _bookmarkLanding;
        private int _bookmarkLandingFrames;

        /// <summary>How long a bookmark landing waits for the branch it asked for. Two builds is
        /// enough - the constellation on one, the system on the next - and twelve is the same generous
        /// count every other wait on this page uses.</summary>
        private const int BookmarkLandingFrames = 12;
    }
}
