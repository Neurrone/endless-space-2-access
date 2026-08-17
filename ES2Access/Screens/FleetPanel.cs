using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

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

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>Whether the window has already been seen ready once this visit - see
        /// <see cref="Available"/>. Instance state, so a hot reload starts it over.</summary>
        private bool _arrived;

        /// <summary>Whether the panel was up on the last frame this was asked, so that its arrival and
        /// its departure are each announced once. Instance state, so it is reload-safe and each page
        /// keeps its own.</summary>
        private bool _up;

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
        }

        /// <summary>
        /// A fleet being selected, or let go of, is a thing that happens to the tab order - so it is
        /// said, and said queued: the player usually caused it, and interrupting the readout of the
        /// control they caused it from would take away the answer they asked for.
        /// </summary>
        public void Update()
        {
            try
            {
                bool up = Available();
                if (up == _up)
                {
                    return;
                }

                _up = up;
                Voice.Say(
                    up
                        ? ModStrings.Format(ModStrings.FleetsPanelOpened, SelectionText())
                        : ModStrings.Get(ModStrings.FleetsPanelClosed),
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("fleets: watching the selection threw: " + e);
            }
        }

        /// <summary>The fleets the panel has just been opened for, in the game's own names.</summary>
        private static string SelectionText()
        {
            List<Fleet> fleets = FleetOrders.Selected();
            MessageBuilder message = new MessageBuilder();
            for (int i = 0; i < fleets.Count; i++)
            {
                message.ListItem(fleets[i].LocalizedName);
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
        public void Build(GraphBuilder builder)
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
            BuildActions(builder, window);
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
        private void BuildActions(GraphBuilder builder, global::FleetsScreen window)
        {
            try
            {
                FleetActionsPanel panel = window.FleetActionsPanel;
                if (
                    panel == null
                    || panel.FleetActionsTable == null
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
                    AddAction(_cells, items[i]);
                }

                if (_cells.Count == 0)
                {
                    return;
                }

                builder.BeginStop(ActionsStop);
                builder.PushContext(ModStrings.Get(ModStrings.FleetsActionsPanel));
                Cells.Emit(builder, _cells);
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
        /// </summary>
        private static void AddAction(List<Cell> cells, FleetActionItem item)
        {
            if (item == null || !AgeWidgets.Visible(item.AgeTransform) || item.AgeTransform.Alpha == 0f)
            {
                return;
            }

            FleetActionItem it = item;
            AgeTooltip tooltip = AgeWidgets.Raw(item.AgeTransform);
            Func<string> label = () => ActionTitle(it.name);
            Func<bool> enabled = () => it.IsEnabled;

            NodeVtable vtable;
            if (item.Toggle != null && item.Toggle.Visible)
            {
                vtable = GraphNodes.Checkbox(
                    label,
                    () => it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    enabled,
                    tooltip
                );
                AgeWidgets.Point(vtable, item.Toggle, tooltip, item.AgeTransform);
            }
            else
            {
                vtable = GraphNodes.Button(
                    label,
                    () => AgeWidgets.Press(it.Button),
                    enabled,
                    tooltip
                );
                AgeWidgets.Point(vtable, item.Button, tooltip, item.AgeTransform);
            }

            AddRefusal(vtable, tooltip, enabled);
            Cells.Add(
                cells,
                item.AgeTransform,
                ControlId.Referenced(item, "fleets:action/" + item.name),
                vtable
            );
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

            string title = AgeText.Clean(Gui.GetTitle(definitionName));
            if (Unresolved(title))
            {
                title = AgeText.Clean("%" + definitionName + "Title");
            }

            return Unresolved(title) ? null : title;
        }

        private static bool Unresolved(string title)
        {
            return string.IsNullOrEmpty(title) || title[0] == '%';
        }

        // ---- the fleets parked here ----

        /// <summary>
        /// The list of garrisons at this place and the buttons that act on the selection, declared in
        /// the rows they are drawn in - the banner above, then one line per garrison.
        ///
        /// The hangar of a colonized system is one of those lines, which is why the list can hold
        /// something that is not a fleet at all: the game puts it first
        /// (<c>FleetsScreen.AddGarrison</c> :626-647) and draws it with a system name and no movement.
        /// </summary>
        private void BuildManagement(GraphBuilder builder, global::FleetsScreen window)
        {
            try
            {
                FleetsManagementPanel panel = window.FleetsManagementPanel;
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                _cells.Clear();
                AddBanner(_cells, panel);
                AddFleetLines(_cells, panel, window);
                if (_cells.Count == 0)
                {
                    return;
                }

                builder.BeginStop(ManagementStop);
                builder.PushContext(ModStrings.Get(ModStrings.FleetsFleetsPanel));
                Cells.Emit(builder, _cells);
                builder.PopContext();
            }
            catch (Exception e)
            {
                Log.Warn("fleets: reading the fleet list threw: " + e);
            }
        }

        /// <summary>The strip above the list: either the buttons, or - when the fleets belong to
        /// somebody else - the sentence the game writes there instead of them.</summary>
        private static void AddBanner(List<Cell> cells, FleetsManagementPanel panel)
        {
            if (panel.OtherEmpireBanner != null && AgeWidgets.Visible(panel.OtherEmpireBanner))
            {
                AgePrimitiveLabel content = panel.OtherEmpireContent;
                AgeTooltip tooltip = AgeWidgets.Raw(panel.OtherEmpireBanner);
                Cells.Add(
                    cells,
                    panel.OtherEmpireBanner,
                    ControlId.Structural("fleets:mgmt/other-empire"),
                    GraphNodes.Readout(() => AgeText.Label(content), null, null, tooltip)
                );
                return;
            }

            AddManagementButton(cells, panel.SelectAllButton, "%FleetSelectAllTitle", "select-all");
            AddManagementButton(cells, panel.CreateButton, "%FleetCreateFromHangarTitle", "create");
            AddManagementButton(cells, panel.MergeButton, "%FleetMergeTitle", "merge");
            AddManagementButton(cells, panel.DisbandButton, "%FleetDisbandTitle", "disband");
        }

        private static void AddManagementButton(
            List<Cell> cells,
            AgeControlButton button,
            string titleKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (button == null || !AgeWidgets.Visible(widget))
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
            AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.Referenced(button, "fleets:mgmt/" + key), vtable);
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
                    null,
                    () => FleetRoute.CommittedLines(going)
                );
                vtable.OnSelectToggle = () => AgeWidgets.Toggle(it.SelectionToggle);
                vtable.OnSelectRange = vtable.OnSelectToggle;
                vtable.Announcements.Add(
                    new NodeAnnouncement(
                        () => LineCells(it),
                        live: true,
                        kind: AnnouncementKinds.Value
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
                Cells.Add(
                    cells,
                    line.AgeTransform,
                    ControlId.Referenced(garrison, "fleets:line/" + garrison.GUID),
                    vtable
                );
            }
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
        private static readonly MethodInfo TransferShips = Transferer();

        private static MethodInfo Transferer()
        {
            try
            {
                return typeof(global::FleetsScreen).GetMethod(
                    "TransferShips",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
            }
            catch (Exception e)
            {
                Log.Warn("fleets: looking up TransferShips threw: " + e);
                return null;
            }
        }

        /// <summary>The line's name, from whichever of the two labels the game drew: a fleet's own
        /// name, or the system a hangar belongs to.</summary>
        private static string LineName(FleetLine line)
        {
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
        /// The numbers the line draws beside the name, each with the caption the game itself gives that
        /// column - which is an ICON in the game's fleet-list header, and so comes out as the icon's
        /// name. The values are the drawn strings, not the model behind them: "1/4" is what is on the
        /// screen.
        ///
        /// A hangar line draws no movement, so it says none: the game replaces the group with an empty
        /// one rather than a zero.
        /// </summary>
        private static string LineCells(FleetLine line)
        {
            MessageBuilder message = new MessageBuilder();
            AddCell(
                message,
                line.CommandPointsGroup,
                line.CommandPointsLabel,
                "%FleetListTableCommandPointsTitle"
            );
            AddCell(
                message,
                line.MovementPointsGroup,
                line.MovementPointsLabel,
                "%FleetListTableMovementPointsTitle"
            );
            return message.Build();
        }

        private static void AddCell(
            MessageBuilder message,
            AgeTransform group,
            AgePrimitiveLabel label,
            string captionKey
        )
        {
            if (group == null || !group.Visible)
            {
                return;
            }

            string value = AgeText.Label(label);
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            MessageBuilder cell = new MessageBuilder();
            cell.Fragment(AgeText.Clean(Gui.Localize(captionKey)));
            cell.Fragment(value);
            message.ListItem(cell.Build());
        }

        // ---- the hero and the ships ----

        /// <summary>The right-hand panel: the hero band, then the ships and the row of things that can
        /// be done to the ones picked out. Both halves are the same panel the star system page draws
        /// its hangar with, so both read them through <see cref="ShipRows"/>.</summary>
        private void BuildHeroAndShips(GraphBuilder builder, global::FleetsScreen window)
        {
            try
            {
                if (
                    window.HeroAndShipsPanel == null
                    || !AgeWidgets.Visible(window.HeroAndShipsPanel.AgeTransform)
                )
                {
                    return;
                }

                bool opened = BuildHero(builder, window, false);
                BuildShips(builder, window, opened);
            }
            catch (Exception e)
            {
                Log.Warn("fleets: reading the hero and ships panel threw: " + e);
            }
        }

        /// <summary>The hero band: who is aboard, the button that puts one aboard or takes them off,
        /// and the hero's own ship, which is a ship tile like any other.</summary>
        private bool BuildHero(GraphBuilder builder, global::FleetsScreen window, bool opened)
        {
            FleetHeroPanel panel = window.FleetHeroPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return opened;
            }

            _cells.Clear();
            FleetHeroPanel it = panel;
            if (
                panel.GuiHero != null
                && panel.HeroPortraitIcon != null
                && AgeWidgets.Visible(panel.HeroPortraitIcon.AgeTransform)
            )
            {
                Cells.Add(
                    _cells,
                    panel.HeroPortraitIcon.AgeTransform,
                    ControlId.Structural("fleets:hero/portrait"),
                    GraphNodes.Readout(
                        () => AgeText.Clean(it.GuiHero.Name),
                        null,
                        null,
                        panel.HeroTooltip
                    )
                );
            }

            AddHeroButton(_cells, panel);

            if (panel.HeroShipContainer != null)
            {
                ShipRows.Ship(
                    _cells,
                    panel.HeroShipContainer.GetComponentInChildren<ShipItem>(true),
                    window.ShipsManagementPanel,
                    "fleets:hero",
                    true
                );
            }

            if (_cells.Count == 0)
            {
                return opened;
            }

            opened = Open(builder, opened);
            builder.PushContext(ModStrings.Get(ModStrings.FleetsHeroPanel));
            Cells.Emit(builder, _cells);
            builder.PopContext();
            return opened;
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
            AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Structural("fleets:hero/assign"), vtable);
        }

        private static AgeTransform FirstDrawn(AgeTransform[] widgets)
        {
            for (int i = 0; widgets != null && i < widgets.Length; i++)
            {
                if (widgets[i] != null && AgeWidgets.Visible(widgets[i]))
                {
                    return widgets[i];
                }
            }

            return null;
        }

        private bool BuildShips(GraphBuilder builder, global::FleetsScreen window, bool opened)
        {
            ShipsManagementPanel panel = window.ShipsManagementPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return opened;
            }

            _cells.Clear();
            if (panel.OtherEmpireBanner != null && AgeWidgets.Visible(panel.OtherEmpireBanner))
            {
                AgePrimitiveLabel content = panel.OtherEmpireContent;
                Cells.Add(
                    _cells,
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
                ShipRows.Toolbar(_cells, panel, "fleets:ships");
            }

            ShipRows.Ships(_cells, panel, "fleets:ships", true);
            if (_cells.Count == 0)
            {
                return opened;
            }

            opened = Open(builder, opened);
            builder.PushContext(ModStrings.Get(ModStrings.FleetsShipsPanel));
            Cells.Emit(builder, _cells);
            builder.PopContext();
            return opened;
        }

        /// <summary>The hero band and the ships share one stop - they are one panel on screen - so
        /// whichever of the two has something to declare opens it.</summary>
        private static bool Open(GraphBuilder builder, bool opened)
        {
            if (!opened)
            {
                builder.BeginStop(ShipsStop);
            }

            return true;
        }

        // ---- shared ----

        private static void AddRefusal(NodeVtable vtable, AgeTooltip tooltip, Func<bool> enabled)
        {
            GraphNodes.AddRefusal(vtable, tooltip, enabled);
        }

        private static global::FleetsScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::FleetsScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
