using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>
    /// The panel the game slides across the bottom of the map the moment a fleet is selected: what this
    /// fleet can do, which fleets are parked here, and the ships they are made of.
    ///
    /// It is not a page, and it must not be a screen. Selecting a fleet changes nothing about where the
    /// player is: the map is still drawn, still live, still the thing the fleet is going to be sent
    /// across - the game merely swaps the cursor and slides a strip of controls over the bottom of the
    /// screen (<c>GuiManager.UpdateGameWindowsVisibility</c> :1543 shows the window for a garrison
    /// cursor and nothing else). Declaring it as a screen of its own put it OVER the galaxy in the
    /// layer stack, which took the systems, the starlanes and the whole HUD out of the tab order at
    /// exactly the moment the player needed them - a fleet is selected in order to send it somewhere.
    /// So this is a CONTRIBUTOR in the shape of <see cref="GlobalHud"/>: the page underneath asks for
    /// its stops, in the order they are drawn relative to that page's own content, and Tab walks the
    /// map and the panel as one screen.
    ///
    /// Three panels, and each is a Tab stop:
    ///
    /// - the FLEETS parked at this place, one line each, plus the buttons that act on the selection.
    ///   The lines are pool slots the game rebinds on every refresh, so each one is keyed on the
    ///   GARRISON it is bound to and asked about the same way - a slot whose data is gone is not a row,
    ///   whatever its Visible flag says.
    /// - the HERO and the SHIPS, the game's own select-then-act model kept intact: the ships are picked
    ///   out and the toolbar above acts on whatever is picked.
    ///
    /// The first two are the shape the constructibles and the hangar already have: a band of things to
    /// DO and the list they are done to, as REGIONS of one stop, so Alt and an arrow steps between the
    /// commands and the list without walking through either. A half the game is not drawing declares no
    /// region, and a stop left with one declares none - a lone region is a jump that swallows the key
    /// silently. What the player came to this panel for is the LIST, so that is where Tab lands
    /// (<c>GraphBuilder.LandStopOn</c>): the first fleet line, the first ship, with the buttons one jump
    /// away. Only the bands are named - "Actions", "Hero" - because the list is what the stop is already
    /// called, and naming it would say "Fleets" or "Ships" twice on the way in.
    ///
    /// The sentence the game writes in place of those buttons for somebody ELSE's fleet is not a command
    /// and so is not a band: it goes in with the list it heads, in the place the game drew it.
    /// - the fleet ACTIONS, one button per thing this fleet could be ordered to do. The roster is closed
    ///   and data-driven (<c>Public/Gui/Screens/GuiElements[FleetsScreen].xml</c>, read once at load), and
    ///   the game hides every button whose failure is flagged "Discard" - so what is drawn IS the answer
    ///   to "what could this fleet do here", and this declares exactly that. A button the game
    ///   leaves drawn but blocked keeps its refusal, in the game's own sentence.
    ///
    /// APPROVED DEVIATION from drawn order. The game draws the actions on the left (x262), the fleet
    /// lines in the middle (x486) and the ships on the right (x776), and reading order would put the
    /// actions first. Tab walks them fleets - ships - actions instead, by the owner's decision: what a
    /// player does on this panel is decide WHICH fleets and WHICH ships they mean and only then order
    /// something, and the actions panel disappears outright unless exactly one fleet is selected, so
    /// leading with a stop that comes and goes under the player reads as the panel breaking. Everything
    /// else on this panel follows the game's own drawn order.
    ///
    /// Selection is the game's own three clicks, and the keyboard now has all three. A fleet line's
    /// handler branches on the modifier the REAL keyboard is holding
    /// (<c>FleetsManagementPanel.OnToggleFleetLine</c> :277-299): none replaces the selection with this
    /// fleet, and Control or Shift - which that handler treats identically, so this panel has no range
    /// gesture of its own - puts one line in or out. All three keys therefore replay the line's own
    /// click and let the game read the player's own fingers, rather than calling
    /// <c>SelectGarrison</c>/<c>UnselectGarrison</c> and having to remember that a selection lives in
    /// two stores at once. "Select all" is still the game's own control for the whole list.
    ///
    /// A ship is moved between fleets by CARRYING it: Space on a ship tile picks it up, Enter on a
    /// fleet line - including the system's hangar line - puts it down there, through
    /// <c>DepartmentOfDefense.CanTransferShips</c> and the drag's own <c>TransferShips</c>. The drag's
    /// other ending, dropping onto empty space to make a NEW fleet, is deliberately not modelled: that
    /// is what selecting ships and pressing the game's own Create button does.
    ///
    /// The panel arriving and leaving is announced, because with no screen change to speak for it
    /// nothing else would: three Tab stops appear under the player and the only sign of it would be Tab
    /// taking longer to come round.
    /// </summary>
    public sealed class FleetPanel
    {
        public static readonly object ActionsStop = "fleets:actions";
        public static readonly object ManagementStop = "fleets:management";
        public static readonly object ShipsStop = "fleets:ships";

        // The regions inside those stops: the band of commands, and the list it acts on.
        private static readonly object ManagementActionsRegion = "fleets:mgmt/actions";
        private static readonly object ManagementListRegion = "fleets:mgmt/list";
        private static readonly object HeroRegion = "fleets:ships/hero";
        private static readonly object ShipsActionsRegion = "fleets:ships/toolbar";
        private static readonly object ShipsListRegion = "fleets:ships/list";

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick. Three
        /// of them, because a stop's halves are gathered separately to be declared as separate regions -
        /// <see cref="_bar"/> is the band of things to do, <see cref="_cells"/> the list they are done
        /// to, and <see cref="_hero"/> the band above the ships.</summary>
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<Cell> _bar = new List<Cell>();
        private readonly List<Cell> _hero = new List<Cell>();

        /// <summary>Whether the window has already been seen ready once this visit - see
        /// <see cref="Available"/>. Instance state, so a hot reload starts it over.</summary>
        private bool _arrived;

        /// <summary>Whether the panel was up on the last frame this was asked, so that its arrival and
        /// its departure are each announced once. Instance state, so it is reload-safe and each page
        /// keeps its own.</summary>
        private bool _up;

        /// <summary>The fleet the panel was last up FOR, so that closing can say which fleet was let
        /// go of. Tracked while the panel is up because the moment it closes the game's selection is
        /// already empty - the cursor swap that closes it is the same thing that clears it.</summary>
        private Fleet _held;

        /// <summary>The fleet the panel is up FOR right now, or null when it is not up - for a page
        /// about to be POPPED with the panel still open, which is the one case where the close frame
        /// never arrives and <see cref="Update"/> can never hand the fleet over
        /// (<see cref="GalaxyHudScreen.OnPop"/>).</summary>
        public Fleet Held
        {
            get { return _held; }
        }

        // ---- the passive watch ----

        /// <summary>Start the watch from what is on the screen now, so arriving on a page with a fleet
        /// already selected never announces a selection nobody just made.</summary>
        public void Baseline()
        {
            _up = Available();
        }

        /// <summary>Stop watching. The next arrival baselines afresh rather than comparing against
        /// whatever the player did while they were somewhere else.</summary>
        public void Forget()
        {
            _up = false;
            _arrived = false;
            _held = null;
        }

        /// <summary>
        /// A fleet being selected, or let go of, is a thing that happens to the tab order - so it is
        /// said, and said queued: the player usually caused it, and interrupting the readout of the
        /// control they caused it from would take away the answer they asked for.
        ///
        /// Answers with the fleet that was LET GO when the panel has just closed, and null every other
        /// frame - for the page to put the cursor somewhere sane when the close took the stops out
        /// from under it (<see cref="GalaxyHudScreen.FollowSelectionEnd"/>).
        /// </summary>
        public Fleet Update()
        {
            try
            {
                bool up = Available();
                if (up)
                {
                    Fleet first = FleetOrders.FirstSelected();
                    if (first != null)
                    {
                        _held = first;
                    }
                }

                if (up == _up)
                {
                    return null;
                }

                _up = up;
                Voice.Say(
                    up
                        ? ModStrings.Format(ModStrings.FleetsPanelOpened, SelectionText())
                        : ModStrings.Get(ModStrings.FleetsPanelClosed),
                    false
                );
                if (up)
                {
                    return null;
                }

                Fleet released = _held;
                _held = null;
                return released;
            }
            catch (Exception e)
            {
                Log.Warn("fleets: watching the selection threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// What the panel has just been opened for, in the game's own names.
        ///
        /// Everything the cursor is holding, not just what an order would move: the panel opens for a
        /// foreign empire's fleet and for a system's hangar exactly as it does for the player's own
        /// ships, and naming only the movable ones left those two openings announced with no name at
        /// all. A hangar names itself after the system it belongs to (<c>Hangar.LocalizedName</c>,
        /// "Hangar (Sabel)"), which is the same system name the panel's own line draws.
        /// </summary>
        private static string SelectionText()
        {
            List<Garrison> garrisons = FleetOrders.SelectedGarrisons();
            MessageBuilder message = new MessageBuilder();
            for (int i = 0; i < garrisons.Count; i++)
            {
                message.ListItem(garrisons[i].LocalizedName);
            }

            return message.Build();
        }

        /// <summary>
        /// Whether the game is drawing the panel and it is worth declaring.
        ///
        /// Arriving and staying are different questions here, because posting one of this panel's own
        /// orders makes the window rebuild itself: merging two fleets destroys both and builds a third,
        /// and the window goes not-ready for a frame or two in the middle of it. Arriving on that would
        /// declare a half-built panel, so arrival waits for ready - but standing down on it would take
        /// three stops out from under the player in the middle of an action they took THERE. So
        /// readiness is asked once, on the way in, and after that only the window being shown at all
        /// keeps the stops up.
        /// </summary>
        public bool Available()
        {
            try
            {
                global::FleetsScreen window = Window();
                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                if (
                    window == null
                    || !window.Shown
                    || gui == null
                    || gui.IsAnyModalVisible
                    || gui.IsInLoadingWindow
                )
                {
                    _arrived = false;
                    return false;
                }

                if (!_arrived && !window.IsReady)
                {
                    return false;
                }

                _arrived = true;
                return true;
            }
            catch (Exception)
            {
                _arrived = false;
                return false;
            }
        }

        /// <summary>The panel's three stops - fleets, ships, actions, the approved deviation from drawn
        /// order explained on the class - or nothing at all while no fleet is selected.</summary>
        public void Build(GraphBuilder builder, GalaxyHudScreen page)
        {
            if (!Available())
            {
                return;
            }

            global::FleetsScreen window = Window();
            if (window == null)
            {
                return;
            }

            BuildManagement(builder, window);
            BuildHeroAndShips(builder, window);
            BuildActions(builder, window, page);
        }

        // ---- what this fleet can do ----

        /// <summary>
        /// One node per action button the game is DRAWING.
        ///
        /// The panel builds all thirty-odd buttons once and hides the ones that do not apply: a failure
        /// the definition flags "Discard" takes the button off the screen entirely
        /// (<c>FleetActionControl.DoRefresh</c> :109-125), and the panel hides the lot whenever the
        /// selection is not exactly one fleet of the player's (<c>FleetActionsPanel.Refresh</c>
        /// :80-104). Both of those are the game answering "is this offerable at all", so the drawn set
        /// is the whole model and there is no separate predicate to ask.
        ///
        /// A button carries no words - it is an icon - and the game names it under the action it
        /// carries out, not under the widget.
        /// </summary>
        private void BuildActions(
            GraphBuilder builder,
            global::FleetsScreen window,
            GalaxyHudScreen page
        )
        {
            try
            {
                FleetActionsPanel panel = window.FleetActionsPanel;
                if (
                    panel == null
                    || panel.FleetActionsTable == null
                    // Flow control: the actions are found by a component scrape and each read for its
                    // badges before anything is declared.
                    || !AgeWidgets.Visible(panel.AgeTransform)
                )
                {
                    return;
                }

                _cells.Clear();
                FleetActionItem[] items =
                    panel.FleetActionsTable.GetComponentsInChildren<FleetActionItem>(true);
                for (int i = 0; i < items.Length; i++)
                {
                    AddAction(_cells, items[i], page);
                }

                if (_cells.Count == 0)
                {
                    return;
                }

                builder.BeginStop(ActionsStop);
                builder.PushContext(ModStrings.Get(ModStrings.FleetsActionsPanel));
                Cells.EmitLinear(builder, _cells);
                builder.PopContext();
            }
            catch (Exception e)
            {
                Log.Warn("fleets: reading the action buttons threw: " + e);
            }
        }

        /// <summary>
        /// One action. The item holds both a button and a toggle and shows whichever its control class
        /// chose (<c>FleetActionButton</c>/<c>FleetActionToggle</c>), so which of the two is drawn is
        /// also which kind of control this is: an order you give once, or a standing state the fleet is
        /// in - auto-explore, sleep, privateering - whose tick is the game's own
        /// "is there an action of mine running".
        ///
        /// The tooltip hangs on the ITEM, not on either control, so the pointer has to be told both
        /// which tooltip to show and what to draw it under. Pointing at the button alone re-derives the
        /// tooltip from the button's own transform, which every one of these leaves empty - and the
        /// row's review buffer then waits on a window that never draws.
        ///
        /// Six of these buttons order nothing at all when they are pressed - they select the fleet's
        /// system and fly the camera in, and the real order is a control drawn inside it
        /// (<see cref="GalaxyHudScreen.SeatTarget"/>). Those six say where pressing them puts the
        /// cursor, and the page then puts it there. Every other action is untouched.
        ///
        /// The item also draws four badges over the icon, each with a sentence of its own
        /// (<see cref="Badges"/>), and two of them draw a figure as well
        /// (<see cref="BadgeText"/>).
        /// </summary>
        private static void AddAction(List<Cell> cells, FleetActionItem item, GalaxyHudScreen page)
        {
            // Kept as flow control: the caller scrapes every FleetActionItem under the panel with
            // includeInactive, and the fixture holds about thirty of them against a handful the game
            // is drawing. Without this each of the rest would build a whole vtable, its tooltips and
            // its badges every frame for the gate to throw away (measured: 30 distinct
            // fleets:action/* drops in one walk).
            if (item == null || !AgeWidgets.Paints(item.AgeTransform))
            {
                return;
            }

            FleetActionItem it = item;
            GalaxyHudScreen owner = page;
            GalaxyHudScreen.SeatTarget seat = GalaxyHudScreen.SeatTargetOf(item);
            AgeTooltip tooltip = AgeWidgets.Raw(item.AgeTransform);
            Func<string> label = () => ActionTitle(it.name);
            Func<bool> enabled = () => it.IsEnabled;
            Func<string> badge = () => BadgeText(it);

            NodeVtable vtable;
            // Which SHAPE the node takes - a tick or a button - not whether it exists.
            if (item.Toggle != null && item.Toggle.Visible)
            {
                vtable = GraphNodes.Checkbox(
                    label,
                    () => it.Toggle.State,
                    () => Act(owner, it, seat, true),
                    enabled,
                    tooltip,
                    value: badge
                );
                AgeWidgets.Point(vtable, item.Toggle, tooltip, item.AgeTransform);
            }
            else
            {
                vtable = GraphNodes.Button(
                    label,
                    () => Act(owner, it, seat, false),
                    enabled,
                    tooltip
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(badge));
                AgeWidgets.Point(vtable, item.Button, tooltip, item.AgeTransform);
            }

            AddSeatPhrase(vtable, seat);
            Cell cell = Cells.Add(
                cells,
                item.AgeTransform,
                ControlId.For(item, "fleets:action/" + item.name),
                vtable
            );

            List<TooltipChildren.Dossier> badges = Badges(item);
            if (cell != null && badges.Count > 0)
            {
                cell.Dossiers = badges;
                cell.Key = "fleets:action/" + item.name;
            }
        }

        /// <summary>
        /// The figures the game draws ON the icon: how many charges the action has left ("0/2", the
        /// probe stock) and how many turns the one already running has to go. Drawn words a sighted
        /// player reads off the button, so they are read with it.
        ///
        /// What either figure MEANS is on the badge's own tooltip, which is a node of its own
        /// (<see cref="Badges"/>) rather than a caption here - the game writes no caption for them.
        /// </summary>
        private static string BadgeText(FleetActionItem item)
        {
            MessageBuilder message = new MessageBuilder();
            message.Fragment(AgeWidgets.TextOf(item.ExecutionStockGroup, 2));
            message.Fragment(AgeWidgets.TextOf(item.DurationGroup, 2));
            return message.Build();
        }

        /// <summary>
        /// The four badges the prefab hangs over an action's icon, in the order it declares them:
        /// this action is already running, it spends the fleet's action point, it has a stock of
        /// charges, it has turns left to go. Each is a plain sentence the game authored
        /// (<c>FleetActionsPanel.CreateFleetActionButtons</c> :117-120 anchors all four), and a mouse
        /// hovering a corner of the icon is the only thing that has ever read one.
        ///
        /// Nodes rather than buffer lines: four explanations merged into one paragraph is what a
        /// player cannot step through, and the badge that says whether this action costs the fleet's
        /// action point is the one the rest of the panel never mentions.
        /// </summary>
        private static List<TooltipChildren.Dossier> Badges(FleetActionItem item)
        {
            List<TooltipChildren.Dossier> badges = new List<TooltipChildren.Dossier>(4);
            TooltipChildren.AddPlainInside(badges, item.OnGoingGroup);
            TooltipChildren.AddPlainInside(badges, item.ActionPointGroup);
            TooltipChildren.AddPlainInside(badges, item.ExecutionStockGroup);
            TooltipChildren.AddPlainInside(badges, item.DurationGroup);
            return badges;
        }

        /// <summary>
        /// The action's own gesture - and, for the six whose gesture only brings the camera in, the
        /// seat that follows it.
        ///
        /// Which system the fleet is acting at is read BEFORE the click, because the click closes this
        /// panel and takes the selection the fleet was read from with it; the seat is asked for AFTER
        /// it, because the click is what opens the system the target is drawn in. The inspect cell goes
        /// down first of all: it is a mode of the map, and the map is about to be driven somewhere else
        /// (owner ruling 2026-08-20).
        /// </summary>
        private static void Act(
            GalaxyHudScreen page,
            FleetActionItem item,
            GalaxyHudScreen.SeatTarget seat,
            bool toggle
        )
        {
            StarSystemNode system =
                seat == GalaxyHudScreen.SeatTarget.None ? null : ActingSystem();
            if (seat != GalaxyHudScreen.SeatTarget.None)
            {
                GalaxyInspect.Dismiss();
            }

            if (toggle)
            {
                AgeWidgets.Toggle(item.Toggle);
            }
            else
            {
                AgeWidgets.Press(item.Button);
            }

            if (page != null && system != null)
            {
                page.SeatAfterFleetAction(system, seat);
            }
        }

        /// <summary>The star system the fleet this panel is acting for is orbiting. The actions panel is
        /// drawn for exactly one fleet of the player's (<c>FleetActionsPanel.Refresh</c>), and all six
        /// of the seating actions require an orbit, so anything else is no answer at all.</summary>
        private static StarSystemNode ActingSystem()
        {
            try
            {
                List<Fleet> fleets = FleetOrders.Selected();
                return fleets.Count == 1
                    ? FleetOrders.Orbit(fleets[0]) as StarSystemNode
                    : null;
            }
            catch (Exception e)
            {
                Log.Warn("fleets: finding the acting fleet's system threw: " + e);
                return null;
            }
        }

        /// <summary>What one of the six adds to its own announcement: where pressing it puts the cursor.
        /// Kindless, so it sits at the tail of the readout - what the control has to SAY about itself,
        /// after everything it IS and after the game's own reason for refusing it.</summary>
        private static void AddSeatPhrase(NodeVtable vtable, GalaxyHudScreen.SeatTarget seat)
        {
            string key = GalaxyHudScreen.SeatPhrase(seat);
            if (key == null || vtable.Announcements == null)
            {
                return;
            }

            vtable.Announcements.Add(new NodeAnnouncement(() => ModStrings.Get(key)));
        }

        /// <summary>What the game calls a fleet action. The element database holds the title as a key
        /// rather than as words, and a definition the database does not know at all is answered by the
        /// engine's own naming convention; silence is the last resort, because an action announced as
        /// "%SomethingTitle" would be worse than one announced by its tooltip alone.</summary>
        private static string ActionTitle(string definitionName)
        {
            if (string.IsNullOrEmpty(definitionName))
            {
                return null;
            }

            return AgeText.Title(Gui.GetTitle(definitionName))
                ?? AgeText.Title("%" + definitionName + "Title");
        }

        // ---- the fleets parked here ----

        /// <summary>
        /// The list of garrisons at this place and the buttons that act on the selection - the band of
        /// buttons the game draws above, then one line per garrison, as the stop's two regions.
        ///
        /// The hangar of a colonized system is one of those lines, which is why the list can hold
        /// something that is not a fleet at all: the game puts it first
        /// (<c>FleetsScreen.AddGarrison</c> :626-647) and draws it with a system name and no movement.
        /// It is a line like any other, so it is also where Tab lands when it is the first of them.
        /// </summary>
        private void BuildManagement(GraphBuilder builder, global::FleetsScreen window)
        {
            try
            {
                FleetsManagementPanel panel = window.FleetsManagementPanel;
                // Flow control: the whole management reading walks the panel.
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                _bar.Clear();
                _cells.Clear();
                AddFleetLines(_cells, panel, window);
                // The line the stop opens on, read before the banner is gathered: the sentence the game
                // writes for somebody else's fleets goes into the same list, and it is not what the
                // player came for.
                ControlId landing = _cells.Count == 0 ? null : _cells[0].Id;
                AddBanner(_bar, _cells, panel);
                if (_bar.Count == 0 && _cells.Count == 0)
                {
                    return;
                }

                bool regions = _bar.Count > 0 && _cells.Count > 0;
                builder.BeginStop(ManagementStop);
                builder.PushContext(ModStrings.Get(ModStrings.FleetsFleetsPanel));
                Cells.EmitRegion(
                    builder,
                    ManagementActionsRegion,
                    ModStrings.DiplomacyActionsBand,
                    regions,
                    _bar,
                    regions ? Cells.AsDrawnRows : Cells.OnePerRow
                );
                // The list takes no word of its own - "Fleets" is what the stop is already called.
                if (regions)
                {
                    builder.SetRegion(ManagementListRegion);
                }

                Cells.EmitLinear(builder, _cells);
                builder.LandStopOn(landing);
                builder.PopContext();
            }
            catch (Exception e)
            {
                Log.Warn("fleets: reading the fleet list threw: " + e);
            }
        }

        /// <summary>
        /// The strip above the list: either the buttons, or - when the fleets belong to somebody else -
        /// the sentence the game writes there instead of them.
        ///
        /// The two go to different places. The buttons are the band the list's region is paired with;
        /// the sentence is not a command at all, so it joins the LIST rather than standing as a region
        /// of commands with no command in it - and the drawn order puts it back on top of the lines
        /// where the game wrote it.
        /// </summary>
        private static void AddBanner(
            List<Cell> bar,
            List<Cell> lines,
            FleetsManagementPanel panel
        )
        {
            // Which BRANCH the strip is: the game draws the banner INSTEAD of the buttons, so this
            // chooses between two readings rather than gating one node.
            if (panel.OtherEmpireBanner != null && AgeWidgets.Visible(panel.OtherEmpireBanner))
            {
                AgePrimitiveLabel content = panel.OtherEmpireContent;
                AgeTooltip tooltip = AgeWidgets.Raw(panel.OtherEmpireBanner);
                Cells.Add(
                    lines,
                    panel.OtherEmpireBanner,
                    ControlId.Structural("fleets:mgmt/other-empire"),
                    GraphNodes.Readout(() => AgeText.Label(content), null, null, tooltip)
                );
                return;
            }

            AddManagementButton(bar, panel.SelectAllButton, "%FleetSelectAllTitle", "select-all");
            AddManagementButton(bar, panel.CreateButton, "%FleetCreateFromHangarTitle", "create");
            AddManagementButton(bar, panel.MergeButton, "%FleetMergeTitle", "merge");
            AddManagementButton(bar, panel.DisbandButton, "%FleetDisbandTitle", "disband");
        }

        private static void AddManagementButton(
            List<Cell> cells,
            AgeControlButton button,
            string titleKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (button == null)
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Operable(AgeWidgets.Transform(it));
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Clean(titleKey),
                () => AgeWidgets.Press(it),
                enabled,
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.For(button, "fleets:mgmt/" + key), vtable);
        }

        /// <summary>
        /// Each line is one of the garrisons the player is choosing between, and membership is the
        /// thing being read: a line says "selected" or "not selected" every time, because with three
        /// selection keys on the panel the absence has to be audible.
        ///
        /// All three keys replay the line's own click, and the game decides which of its rules to run
        /// from the modifier the player is physically holding
        /// (<c>FleetsManagementPanel.OnToggleFleetLine</c> :277-299). Control and Shift reach the same
        /// branch there, so this panel has no range gesture - Shift and Enter puts one line in or out,
        /// exactly as Control and Enter does, which is what the mouse does too.
        ///
        /// A line is also where a carried ship is put down, with Enter - the same key that selects the
        /// line when nothing is being carried. The line the ship came from takes it back; any other
        /// line - including the system's hangar, which the game draws as the first line of this same
        /// list - takes it if the game's own check says so, and says the game's reason if not.
        ///
        /// One line per garrison the window is showing, keyed on the garrison rather than on the slot
        /// drawing it: the table pools its lines and rebinds them on every refresh, so a cursor keyed on
        /// <c>FleetLine007</c> would be sitting on a different fleet a frame later. Existence is the
        /// same question - a slot the game has unbound is not a row, whatever its Visible flag says -
        /// and the window's own garrison list is the second half of it, so a line left bound to
        /// something the window has dropped is not offered either.
        /// </summary>
        private static void AddFleetLines(
            List<Cell> cells,
            FleetsManagementPanel panel,
            global::FleetsScreen window
        )
        {
            if (panel.FleetLinesTable == null)
            {
                return;
            }

            FleetLine[] lines = panel.FleetLinesTable.GetComponentsInChildren<FleetLine>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                FleetLine line = lines[i];
                if (line == null || line.GuiGarrison == null || line.GuiGarrison.Garrison == null)
                {
                    continue;
                }

                Garrison garrison = line.GuiGarrison.Garrison;
                if (!window.Garrisons.Contains(garrison))
                {
                    continue;
                }

                FleetLine it = line;
                Garrison held = garrison;
                global::FleetsScreen screen = window;
                AgeTooltip tooltip = line.FleetTooltip;
                // A hangar is a line here too, and a hangar goes nowhere.
                Fleet going = garrison as Fleet;
                NodeVtable vtable = GraphNodes.SelectionItem(
                    () => LineName(it),
                    () => Selected(screen, held),
                    null,
                    () => AgeWidgets.Toggle(it.SelectionToggle),
                    () => AgeWidgets.Operable(it.AgeTransform),
                    tooltip,
                    () => FleetRoute.CommittedLines(going)
                );
                vtable.OnSelectToggle = () => AgeWidgets.Toggle(it.SelectionToggle);
                vtable.OnSelectRange = vtable.OnSelectToggle;
                // Both chords replay the line's own click and the GAME reads the modifier the player
                // is still holding (<c>FleetsManagementPanel</c> :280), so the two do different things
                // through one handler - and nothing on the list says either exists.
                NodeHints.Add(vtable, ModStrings.HintAddToSelection, UiActions.SelectToggle);
                NodeHints.Add(vtable, ModStrings.HintSelectUpToHere, UiActions.SelectRange);
                // A part per COLUMN rather than one composed sentence, the shape the battle screens'
                // figures already have: the announcer joins the parts into the one line the row
                // always spoke, and the review buffer gives each column a line of its own to step
                // through. A column the line is not drawing answers null and contributes to neither.
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(
                        () =>
                            CellText(
                                it.CommandPointsGroup,
                                it.CommandPointsLabel,
                                "%FleetListTableCommandPointsTitle"
                            )
                    )
                );
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(
                        () =>
                            CellText(
                                it.MovementPointsGroup,
                                it.MovementPointsLabel,
                                "%FleetListTableMovementPointsTitle"
                            )
                    )
                );
                // After the numbers the line draws, because that is where it would be written if the
                // game wrote it anywhere: the line names no destination of its own. Not watched - the
                // answer is a walk of the fleet's path, and this list is rebuilt every frame.
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(() => FleetRoute.Committed(going), false)
                );
                vtable.DropKind = ShipRows.ShipKind;
                vtable.OnDrop = item => Transfer(screen, held, LineName(it), item);
                AgeWidgets.PointAt(vtable, it.AgeTransform);
                Cell row = Cells.Add(
                    cells,
                    line.AgeTransform,
                    // STRUCTURAL, not anchored on the garrison: the galaxy tree's own fleet row carries
                    // that same Fleet object as its subject (<c>PlacedRows.Anchor</c>), and a subject
                    // on two nodes is one control to the cursor - reconciliation searches subjects
                    // before keys, so this line teleported the player back out to the map on the next
                    // rebuild. A subject buys nothing here anyway: the GUID key is stable across the
                    // pool's rebinds, which is the only change this row ever sees.
                    ControlId.Structural("fleets:line/" + garrison.GUID),
                    vtable
                );
                AddLineBadge(row, line, "fleets:line/" + garrison.GUID);
            }
        }

        /// <summary>The badge the list draws on a fleet's own line: whether the fleet still has an
        /// action point to spend this turn. The game writes a different sentence for each state onto
        /// the same icon (<c>FleetLine.cs</c> :270-279) and dims it rather than hiding it when there
        /// is none, so both states are drawn and both read - and nothing else on the line says the
        /// fleet has spent its turn. A line the game draws no badge on stays the leaf it was.</summary>
        private static void AddLineBadge(Cell owner, FleetLine line, string key)
        {
            if (owner == null)
            {
                return;
            }

            List<TooltipChildren.Dossier> badge = new List<TooltipChildren.Dossier>(1);
            TooltipChildren.AddPlain(
                badge,
                line.ActionPointIcon == null ? null : line.ActionPointIcon.AgeTransform
            );
            if (badge.Count == 0)
            {
                return;
            }

            owner.Dossiers = badge;
            owner.Key = key;
        }

        /// <summary>Whether this garrison is in the game's own selection. The window's list rather than
        /// the line's drawn tick: the tick is rewritten on the panel's next refresh, so straight after a
        /// selection key it is still showing the state before it.</summary>
        private static bool Selected(global::FleetsScreen window, Garrison garrison)
        {
            try
            {
                return window.SelectedGarrisons != null
                    && window.SelectedGarrisons.Contains(garrison);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Put a carried ship into this garrison, the way the drag does it: the game's own
        /// <c>CanTransferShips</c> decides, and the game's own <c>TransferShips</c> posts the order -
        /// which is also what keeps the confirmation box the game raises when a transfer would cost a
        /// fleet its invisibility.
        ///
        /// A refusal is the game's sentence for its own failure flag, never a rule written here, and the
        /// player goes on carrying the ship.
        /// </summary>
        private static DropResult Transfer(
            global::FleetsScreen window,
            Garrison destination,
            string destinationName,
            CarryItem item
        )
        {
            try
            {
                Ship ship = item == null ? null : item.Cargo as Ship;
                if (ship == null || destination == null || TransferShips == null)
                {
                    return DropResult.Refused(null);
                }

                List<Ship> ships = new List<Ship> { ship };
                FailureInfo failure;
                if (!DepartmentOfDefense.CanTransferShips(destination, ships, null, out failure))
                {
                    return DropResult.Refused(
                        AgeText.Clean(Gui.FormatFailureInfo(string.Empty, failure))
                    );
                }

                TransferShips.Invoke(
                    window,
                    new object[] { ships, null, destination.GUID, NodePosition.Invalid }
                );
                return DropResult.Done(
                    ModStrings.Format(ModStrings.FleetsShipMoved, item.Name, destinationName)
                );
            }
            catch (Exception e)
            {
                Log.Warn("fleets: transferring a ship threw: " + e);
                return DropResult.Refused(null);
            }
        }

        // The window keeps the transfer to itself; it is the drag's own call, and going round it would
        // lose the confirmation the game raises for an invisibility-breaking move.
        private static readonly MethodInfo TransferShips = GameHandlers.Method(
            typeof(global::FleetsScreen),
            "TransferShips"
        );

        /// <summary>The line's name, from whichever of the two labels the game drew: a fleet's own
        /// name, or the system a hangar belongs to.</summary>
        private static string LineName(FleetLine line)
        {
            // Content: which drawn label supplies the line's name.
            if (line.FleetNameLabel != null && line.FleetNameLabel.AgeTransform.Visible)
            {
                return AgeText.Label(line.FleetNameLabel);
            }

            if (line.SystemNameLabel != null && line.SystemNameLabel.AgeTransform.Visible)
            {
                return AgeText.Label(line.SystemNameLabel);
            }

            return line.GuiGarrison == null ? null : AgeText.Clean(line.GuiGarrison.LocalizedName);
        }

        /// <summary>
        /// ONE of the numbers the line draws beside the name, with the caption the game itself gives
        /// that column - which is an ICON in the game's fleet-list header, and so comes out as the
        /// icon's name. The value is the drawn string, not the model behind it: "1/4" is what is on
        /// the screen.
        ///
        /// Null where the line is not drawing that column, so the part it belongs to disappears: a
        /// hangar line draws no movement, and the game replaces the group with an empty one rather
        /// than a zero.
        /// </summary>
        private static string CellText(
            AgeTransform group,
            AgePrimitiveLabel label,
            string captionKey
        )
        {
            // Content: whether this column is drawn on the line at all.
            if (group == null || !group.Visible)
            {
                return null;
            }

            string value = AgeText.Label(label);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            MessageBuilder cell = new MessageBuilder();
            cell.Fragment(AgeText.Clean(Gui.Localize(captionKey)));
            cell.Fragment(value);
            return cell.Build();
        }

        // ---- the hero and the ships ----

        /// <summary>
        /// The right-hand panel: the hero band, then the ships and the row of things that can be done to
        /// the ones picked out. Both halves are the same panel the star system page draws its hangar
        /// with, so both read them through <see cref="ShipRows"/>.
        ///
        /// One stop with up to three regions - the hero band, the commands, the ships - each named for
        /// itself, the ships as "Ships" (owner ruling 2026-08-24, superseding the stop-wide "Ships" of
        /// 2026-08-20: the stop carries no name of its own, so a landing in the hero band no longer
        /// says "Ships" first). Regions at all only where two of the three are drawn: one region is a
        /// jump that goes nowhere.
        /// </summary>
        private void BuildHeroAndShips(GraphBuilder builder, global::FleetsScreen window)
        {
            try
            {
                if (
                    window.HeroAndShipsPanel == null
                    // Flow control: both readings under it walk panels of their own.
                    || !AgeWidgets.Visible(window.HeroAndShipsPanel.AgeTransform)
                )
                {
                    return;
                }

                _hero.Clear();
                _bar.Clear();
                _cells.Clear();
                AddHero(_hero, window);
                ControlId landing = AddShips(_bar, _cells, window.ShipsManagementPanel);
                if (_hero.Count == 0 && _bar.Count == 0 && _cells.Count == 0)
                {
                    return;
                }

                int halves =
                    (_hero.Count > 0 ? 1 : 0)
                    + (_bar.Count > 0 ? 1 : 0)
                    + (_cells.Count > 0 ? 1 : 0);
                bool regions = halves > 1;

                builder.BeginStop(ShipsStop);
                // The hero band reads linearly: its ship tile carries a dossier group, which a row
                // cannot host.
                Cells.EmitRegion(
                    builder,
                    HeroRegion,
                    ModStrings.FleetsHeroPanel,
                    regions,
                    _hero,
                    Cells.OnePerRow
                );
                // The toolbar is plain buttons, so it stands as the one row it is drawn as.
                Cells.EmitRegion(
                    builder,
                    ShipsActionsRegion,
                    ModStrings.DiplomacyActionsBand,
                    regions,
                    _bar,
                    regions ? Cells.AsDrawnRows : Cells.OnePerRow
                );
                Cells.EmitRegion(
                    builder,
                    ShipsListRegion,
                    ModStrings.FleetsShipsPanel,
                    regions,
                    _cells,
                    Cells.OnePerRow
                );
                builder.LandStopOn(landing);
            }
            catch (Exception e)
            {
                Log.Warn("fleets: reading the hero and ships panel threw: " + e);
            }
        }

        /// <summary>The hero band: who is aboard, the button that puts one aboard or takes them off,
        /// and the hero's own ship, which is a ship tile like any other.</summary>
        private static void AddHero(List<Cell> cells, global::FleetsScreen window)
        {
            FleetHeroPanel panel = window.FleetHeroPanel;
            // Flow control: the hero's ship row under it is a walk of its own.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            FleetHeroPanel it = panel;
            if (
                panel.GuiHero != null
                && panel.HeroPortraitIcon != null
            )
            {
                NodeVtable portrait = GraphNodes.Readout(
                    () => AgeText.Clean(it.GuiHero.Title),
                    null,
                    null,
                    panel.HeroTooltip
                );
                AgeWidgets.PointAt(
                    portrait,
                    panel.HeroPortraitIcon.AgeTransform,
                    panel.HeroTooltip
                );
                Cells.Add(
                    cells,
                    panel.HeroPortraitIcon.AgeTransform,
                    ControlId.Structural("fleets:hero/portrait"),
                    portrait
                );
            }

            AddHeroButton(cells, panel);

            if (panel.HeroShipContainer != null)
            {
                ShipRows.Ship(
                    cells,
                    panel.HeroShipContainer.GetComponentInChildren<ShipItem>(true),
                    window.ShipsManagementPanel,
                    "fleets:hero",
                    true
                );
            }
        }

        /// <summary>
        /// The one button that puts a hero aboard or takes them off again.
        ///
        /// The panel keeps TWO transforms for it and shows whichever fits the band it is drawing, and
        /// they share one tooltip - so which of them is up says nothing about which of the two actions
        /// this is. What does say it is the ICON: the panel draws either <c>AssignIcon</c> or
        /// <c>UnassignIcon</c> and never both (:210-211, :251-252, :271-272). Measured, after declaring
        /// it the other way round left an "Unassign" button carrying the assign tooltip.
        /// </summary>
        private static void AddHeroButton(List<Cell> cells, FleetHeroPanel panel)
        {
            AgeTransform widget = FirstDrawn(panel.AssignUnassignButtons);
            if (widget == null)
            {
                return;
            }

            AgeTransform it = widget;
            FleetHeroPanel owner = panel;
            AgeTooltip tooltip = panel.AssignUnassignButtonTooltip;
            Func<bool> enabled = () => AgeWidgets.Operable(it);
            // Content: which of the game's two titles the button is called by.
            NodeVtable vtable = GraphNodes.Button(
                () =>
                    AgeText.Clean(
                        owner.AssignIcon != null && owner.AssignIcon.AgeTransform.Visible
                            ? "%HeroAssignTitle"
                            : "%HeroUnassignTitle"
                    ),
                () => AgeWidgets.Press(it),
                enabled,
                tooltip
            );
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Structural("fleets:hero/assign"), vtable);
        }

        private static AgeTransform FirstDrawn(AgeTransform[] widgets)
        {
            for (int i = 0; widgets != null && i < widgets.Length; i++)
            {
                // Candidate choice: the first drawn of several alternatives, which the gate cannot make.
                if (widgets[i] != null && AgeWidgets.Visible(widgets[i]))
                {
                    return widgets[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The ships and the row of commands above them - <paramref name="bar"/> the commands,
        /// <paramref name="cells"/> the ships, so the two can be declared as regions of their own.
        ///
        /// Somebody else's fleet gets a sentence where the commands would be, and that sentence is not a
        /// command: it joins the SHIPS rather than making a band of its own with nothing to do in it.
        /// The ships are gathered first so the tile Tab lands on is the first SHIP, never that sentence.
        /// </summary>
        private static ControlId AddShips(
            List<Cell> bar,
            List<Cell> cells,
            ShipsManagementPanel panel
        )
        {
            // Flow control: the shared ship reader walks the whole panel.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return null;
            }

            ShipRows.Ships(cells, panel, "fleets:ships", true);
            ControlId landing = cells.Count == 0 ? null : cells[0].Id;
            // Which BRANCH again: the banner is drawn INSTEAD of the toolbar.
            if (panel.OtherEmpireBanner != null && AgeWidgets.Visible(panel.OtherEmpireBanner))
            {
                AgePrimitiveLabel content = panel.OtherEmpireContent;
                Cells.Add(
                    cells,
                    panel.OtherEmpireBanner,
                    ControlId.Structural("fleets:ships/other-empire"),
                    GraphNodes.Readout(
                        () => AgeText.Label(content),
                        null,
                        null,
                        AgeWidgets.Raw(panel.OtherEmpireBanner)
                    )
                );
            }
            else
            {
                ShipRows.Toolbar(bar, panel, "fleets:ships");
            }

            return landing;
        }

        // ---- shared ----

        private static global::FleetsScreen Window()
        {
            return GameWindows.Of<global::FleetsScreen>();
        }
    }
}
