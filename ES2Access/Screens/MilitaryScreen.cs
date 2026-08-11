using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The fleets and ship designs page - what F5 opens: every fleet the empire is flying as a table,
    /// the ships of whichever one is picked, and the templates those ships are built from.
    ///
    /// The page can be LOCKED OUT entirely. It needs a military technology to exist at all
    /// (<c>MilitaryScreen.IsAccessible</c> :76 -&gt; <c>DepartmentOfDefense.MilitaryScreenGameplayUnlocked</c>),
    /// and until then the icon that opens it is drawn disabled with the game's own sentence for what is
    /// missing - so the refusal is already read where the player presses, by the icon strip, and this
    /// screen simply never becomes active. Nothing here reproduces that test.
    ///
    /// Two things about the fleets table are this screen's own; everything else about the reading of a
    /// <c>GuiTable</c> is inherited (<see cref="TableSheet"/>).
    ///
    /// - The table is SINGLE-select (<c>GuiTable.EnforceRadio</c> :309-316), so there are no selection
    ///   chords on a row: Enter is the row's own click, which picks the fleet, enables the three fleet
    ///   buttons and slides the ship list out underneath.
    /// - Backslash on a row is the table's own DOUBLE click, which is how the mouse gets from a fleet in
    ///   this list to that fleet on the galaxy map (<c>OnLineDoubleClick</c> :511-560: it selects the
    ///   fleet's cursor target, swaps in the garrison cursor and asks for the galaxy view). It reads the
    ///   SELECTED fleet rather than the line it was handed, so the row is picked first - which is what
    ///   the mouse's own first click did.
    ///
    /// The Hero column is a control rather than a figure (<c>interactiveCells: true</c>, :252, which is
    /// what draws <c>GuiTableCellAssignedHero.SelectionButton</c>): pressing it opens the hero picker for
    /// a fleet with no hero and the hero's dossier for one that has one (:492-507). That is the game's
    /// two-step - the cell's button records which cell was hit and the ROW's toggle then acts on it - so
    /// it is replayed as one gesture with <see cref="AgeWidgets.PressPropagating"/>. The cell is read
    /// here rather than by the sheet's ordinary value cell because a REFUSAL lives on it: a fleet with a
    /// juggernaut in it has the cell switched off (<c>GuiTableCellAssignedHero.Refresh</c> :31) while the
    /// row itself is perfectly available.
    ///
    /// The ships half is the shared garrison reading (<see cref="ShipRows"/>) - <c>MilitaryShipsListPanel</c>
    /// IS <c>ShipsManagementPanel</c>, with no members of its own - with two things this host settles: a
    /// ship cannot be picked UP here, because this page draws nowhere to put one down (its
    /// <c>IGuiGarrisonsHandler</c> hooks throw or do nothing, :168-200), and the panel's Create-fleet
    /// button is not declared at all, because the method it calls on this host is empty.
    ///
    /// The two boxes down the left edge each hand the shared side-panel reader one correction of their
    /// own. The battle-tactics box draws its six deck slots as wordless artwork with the title label
    /// hidden, so the shape walk finds nothing to say in them and they are read off the wrapper the panel
    /// binds (<see cref="DeckSlot"/>); the box's Open button raises the window where the set is actually
    /// changed (<see cref="BattleTacticsScreen"/>), and its Manage sibling in the manpower box raises
    /// <see cref="TroopManagementScreen"/>.
    ///
    /// The manpower box down the left edge is read by the shared side-panel reader, with one correction:
    /// seven of its groups are wired to a click handler that only does anything in the developers' god
    /// mode (<c>MilitaryManPowerSidePanel.OnClickManPowerCb</c> :225-232), and a click the game answers
    /// with silence is not a control. Declared as the bands of readouts they are, which also lets the
    /// walk reach the three troop percentages inside them.
    ///
    /// Escape and F5 stay the game's. This is one of the icon strip's screens, drawn in an exclusive
    /// window stack - opening any other one hides this instantly - which is why they share a layer.
    /// </summary>
    public sealed class MilitaryScreen : Screen
    {
        private static readonly object HeadersStop = "military:headers";
        private static readonly object FleetsStop = "military:fleets";
        private static readonly object FleetActionsStop = "military:fleet-actions";
        private static readonly object ShipsStop = "military:ships";
        private static readonly object DesignsStop = "military:designs";
        private static readonly object DesignActionsStop = "military:design-actions";
        private static readonly object OverviewStop = "military:overview";

        /// <summary>The prefix the shared readers key this page's ids under.</summary>
        private const string Keys = "military:";

        /// <summary>The one god-mode-only handler the manpower box wires seven of its groups to.
        /// </summary>
        private const string GodModeOnly = "OnClickManPowerCb";

        private readonly GlobalHud _hud = new GlobalHud();
        private readonly TableSheet _table;

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<Cell> _cells = new List<Cell>();

        public MilitaryScreen()
        {
            _table = new TableSheet(Keys, FleetOf);
            _table.RowName = FleetName;
            _table.ReadCell = HeroCell;
            _table.Decorate = FocusOnMap;
        }

        public override string Key
        {
            get { return "screen.military"; }
        }

        /// <summary>Above the view levels it is drawn over, beside the empire summary and the senate: the
        /// strip of icons in the corner opens all of them and the engine's window stack lets only one be
        /// up at a time.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = ScreenTitle();
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenMilitary)
                    : title;
            }
        }

        /// <summary>The fleets, because that is what the page is for; the boxes down the left edge and
        /// the empire's banners are a Shift+Tab away.</summary>
        public override object InitialFocusStop
        {
            get { return FleetsStop; }
        }

        /// <summary>A page the player closes and comes straight back to, with the cursor where they left
        /// it.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape is the game's: it closes the screen, which is what the page's own exit does
        /// too.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>Arrival gates on the page being WORKABLE, not just on no modal being up: the renderer
        /// switches the whole background stack off while a modal is showing and back on a frame or more
        /// AFTER the modal reports itself gone, so coming back on "no modal" alone lands the cursor on a
        /// page whose every control is still switched off and reads one passing "unavailable".</summary>
        public override bool IsActive()
        {
            try
            {
                global::MilitaryScreen window = Window();
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow
                    && AgeWidgets.Operable(window.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void OnPush()
        {
            _hud.Baseline();
        }

        public override void OnPop()
        {
            _hud.Forget();
        }

        public override void OnUpdate()
        {
            _hud.Update();
        }

        public override void Build(GraphBuilder builder)
        {
            global::MilitaryScreen window = Window();
            if (window == null)
            {
                return;
            }

            // Down the screen and then across it: the empire's banners along the top, the boxes down the
            // left edge, the fleets and what can be done to them, the ships of the picked fleet, and then
            // the right-hand column - the designs and whichever one of them is written out below.
            _hud.Top(builder);
            BuildSidePanels(builder);
            BuildFleets(builder, window);
            BuildFleetActions(builder, window);
            BuildShips(builder, window);
            BuildDesigns(builder, window);
            BuildOverview(builder, window);
            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.Turn(builder);
        }

        // ---- the boxes down the left edge ----

        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop("military:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    _cells.Clear();
                    SidePanels.Readouts(
                        _cells,
                        panel,
                        "military:side/" + i + "/",
                        PanelCell,
                        GodModeClick
                    );
                    Cells.Emit(builder, _cells);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("military: reading the side panels threw: " + e);
            }
        }

        /// <summary>What a box down the left edge is called. The battle-tactics deck writes a heading
        /// across its own top but not in the field the shared reader looks in, so it is taken from where
        /// it is drawn; the manpower box writes none at all and gets a word of the mod's.</summary>
        private static string PanelName(SidePanel panel)
        {
            if (panel is MilitaryManPowerSidePanel)
            {
                return ModStrings.Get(ModStrings.MilitaryManpowerPanel);
            }

            string drawn = DeckHeading(panel);
            return string.IsNullOrEmpty(drawn) ? SidePanels.Name(panel) : drawn;
        }

        /// <summary>The label the battle-tactics box writes its heading into, which the shared reader
        /// cannot find: that reader looks up a public <c>PanelTitle</c> field and this panel declares
        /// none, keeping its heading in an ordinary label inside its contents instead. Looked for in the
        /// CONTENTS rather than on the panel, because the panel's own first child is the group holding
        /// its explaining icon and wears the same name.</summary>
        private static AgeTransform DeckHeadingLabel(SidePanel panel)
        {
            if (!(panel is MilitaryPlayCardDeckSidePanel))
            {
                return null;
            }

            return AgeWidgets.ChildNamed(panel.ContentGroup, "Header", 0);
        }

        private static string DeckHeading(SidePanel panel)
        {
            AgeTransform label = DeckHeadingLabel(panel);
            return label == null ? null : AgeWidgets.TextOf(label);
        }

        /// <summary>What the shape of these boxes cannot answer: the heading the battle-tactics box
        /// draws, which is already the name of the stop the player just entered, and the six deck slots
        /// it draws as wordless artwork.</summary>
        private static bool PanelCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            AgeTransform heading = DeckHeadingLabel(panel);
            if (
                heading != null
                && ReferenceEquals(heading, widget)
                && !string.IsNullOrEmpty(AgeWidgets.TextOf(heading))
            )
            {
                return true;
            }

            return DeckSlot(cells, widget, keyPrefix);
        }

        /// <summary>
        /// One of the six battle-tactics slots the box draws along its own top.
        ///
        /// The mini card is ARTWORK and nothing else: the game hides the title label the full-size card
        /// draws (measured - <c>PlayTitle</c> is invisible in this prefab and still holds the words), so
        /// the shape walk finds a drag area with no text in it and declares nothing. What the slot holds
        /// is on the wrapper the game binds to it (<c>MilitaryPlayCardDeckSidePanel.Refresh</c> :168-190),
        /// which is the same title the drawn tooltip's own header shows.
        ///
        /// A readout, because there is nothing to press: the panel binds these cards with no client and
        /// no drag (<c>BindDeckBattlePlayCard</c> :233-241), so a click reaches
        /// <c>BattlePlayCard.OnClickCb</c> and does nothing at all. The set is CHANGED in the window the
        /// Open button below raises (<see cref="BattleTacticsScreen"/>).
        /// </summary>
        private static bool DeckSlot(List<Cell> cells, AgeTransform widget, string keyPrefix)
        {
            BattlePlayCard card = widget == null ? null : widget.GetComponent<BattlePlayCard>();
            if (card == null || !card.IsBound || card.GuiBattlePlaySlot == null)
            {
                return false;
            }

            BattlePlayCard it = card;
            AgeTooltip tooltip = card.Tooltip ?? AgeWidgets.Raw(widget);
            // A slot with no tactic in it has no name of its own anywhere, so the sentence the game
            // explains it with becomes its name - and is then not announced as a tooltip as well, which
            // is the same trade every bare-icon control in the mod makes. The buffer still holds all of
            // it.
            bool named = !string.IsNullOrEmpty(Title(card));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => DeckSlotName(it, tooltip)),
                },
                Sections = GraphNodes.Sections(
                    null,
                    tooltip,
                    named ? GraphNodes.ModeFor(tooltip) : TooltipMode.None
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(
                cells,
                widget,
                ControlId.Structural(keyPrefix + widget.name),
                vtable
            );
            return true;
        }

        /// <summary>The tactic in a slot, in the game's own words - and for a slot with nothing in it, the
        /// sentence the game explains the empty or locked slot with, because the wrapper for those has no
        /// title at all.</summary>
        private static string DeckSlotName(BattlePlayCard card, AgeTooltip tooltip)
        {
            string title = Title(card);
            return string.IsNullOrEmpty(title) ? CardActions.FirstLine(tooltip) : title;
        }

        /// <summary>The tactic a slot is holding, in the game's own words - the same title the drawn
        /// tooltip writes across its own top. Empty for a slot with nothing in it.</summary>
        private static string Title(BattlePlayCard card)
        {
            try
            {
                GuiBattlePlaySlot slot = card.GuiBattlePlaySlot;
                return slot == null ? null : AgeText.Clean(slot.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether a group the game made clickable is really a band of readouts.
        ///
        /// The manpower box wires seven of its groups - the four figures across its top, the troop
        /// breakdown and the two halves holding it - to <c>OnClickManPowerCb</c>, whose whole body is
        /// guarded by <c>GodGalaxyCursor.IsGuiInGodMode()</c> (:225-232). For a player, clicking any of
        /// them does nothing and says nothing, so they are declared as the readouts they are - and that
        /// is also what lets the walk descend PAST the outer group to the infantry, armour and air
        /// percentages drawn inside it.
        /// </summary>
        private static bool GodModeClick(AgeTransform widget, SidePanel panel)
        {
            AgeControlButton button = AgeWidgets.Button(widget);
            return button != null && button.OnActivateMethod == GodModeOnly;
        }

        // ---- the fleets ----

        /// <summary>The sort headers, then the fleets themselves.</summary>
        private void BuildFleets(GraphBuilder builder, global::MilitaryScreen window)
        {
            GuiTable table = Table(window);
            if (table == null)
            {
                return;
            }

            builder.BeginStop(HeadersStop);
            _table.Headers(builder, table);

            builder.BeginStop(FleetsStop);
            _table.Rows(builder, table, GroupTitle(table));
        }

        /// <summary>
        /// The Hero column, where the game draws a BUTTON rather than a figure - the one cell of this
        /// table that is a control.
        ///
        /// Null for every other column, which is the shared value cell. Enter here is the cell's own
        /// click carried on to the row's toggle, which is the two-step the mouse makes: the cell button
        /// records which cell was hit and the row's own handler reads that and opens either the hero
        /// picker or the assigned hero's dossier.
        ///
        /// The cell is still a cell - it says the hero it is showing, or the word for showing nothing,
        /// and not its heading, which the sheet speaks as the edge crossed to reach it.
        /// </summary>
        private NodeVtable HeroCell(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        )
        {
            AgeControlButton button = CellButton(cell);
            if (button == null)
            {
                return null;
            }

            AgeTransform it = cell;
            AgeControlButton press = button;
            GuiTableHeader heading = header;
            Func<bool> rowEnabled = enabled;
            Func<bool> operable = () =>
                rowEnabled() && AgeWidgets.Operable(press.AgeTransform) && AgeWidgets.Enabled(it);
            AgeTooltip tooltip = TableSheet.TooltipOf(cell);
            NodeVtable vtable = new NodeVtable
            {
                // Named as the button it is, unlike the figures beside it: the whole point of the column
                // is what pressing it opens, and the role word is the only thing that says it can be
                // pressed at all.
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => _table.CellText(it)),
                    GraphNodes.DisabledPart(operable),
                },
                Sections = GraphNodes.Sections(() => _table.CellFacts(heading, it), tooltip),
                OnActivate = () =>
                {
                    if (operable())
                    {
                        AgeWidgets.PressPropagating(press);
                    }
                },
            };
            GraphNodes.AddRefusal(vtable, tooltip, operable);

            return vtable;
        }

        private static AgeControlButton CellButton(AgeTransform cell)
        {
            try
            {
                if (cell == null)
                {
                    return null;
                }

                AgeControlButton button = cell.GetComponentInChildren<AgeControlButton>(true);
                return button != null
                    && !string.IsNullOrEmpty(button.OnActivateMethod)
                    && button.AgeTransform.Visible
                    ? button
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The gesture a fleet row has beyond its click: the table's own double click, which takes the
        /// player to that fleet on the galaxy map.
        ///
        /// The game's handler acts on the SELECTED fleet and returns at once when there is none
        /// (:513-516), so the row is picked first - which is exactly what the mouse's first click did
        /// before its second one arrived. Picking a row that is already picked is not repeated: the
        /// game's own selection handler slides the ship list out and plays a sound, and neither belongs
        /// to a request to look at the map.
        /// </summary>
        private static void FocusOnMap(GuiTableLine line, NodeVtable vtable)
        {
            GuiTableLine row = line;
            if (row.DoubleClickButton == null)
            {
                return;
            }

            vtable.OnContextual = () =>
            {
                try
                {
                    if (!AgeWidgets.Enabled(row.AgeTransform))
                    {
                        return;
                    }

                    if (row.SelectionToggle != null && !row.SelectionToggle.State)
                    {
                        AgeWidgets.Toggle(row.SelectionToggle);
                    }

                    AgeWidgets.DoubleClick(row.DoubleClickButton);
                }
                catch (Exception e)
                {
                    Log.Warn("military: showing a fleet on the map threw: " + e);
                }
            };
        }

        // ---- what can be done to the picked fleet ----

        /// <summary>Rename, Retrofit and Disband, in the order the band draws them. Each carries the
        /// game's own sentence for why it cannot be used - written onto its tooltip by
        /// <c>RefreshFleetButtons</c> (:443-473) - so a refusal needs nothing of the mod's.</summary>
        private void BuildFleetActions(GraphBuilder builder, global::MilitaryScreen window)
        {
            AgeTransform band = window.FleetButtonsGroup;
            if (band == null || !AgeWidgets.Visible(band))
            {
                return;
            }

            builder.BeginStop(FleetActionsStop);
            builder.PushContext(ModStrings.Get(ModStrings.MilitaryFleetActions));
            _cells.Clear();
            AddButton(window.RenameFleetButton, "rename");
            AddButton(window.RetrofitFleetButton, "retrofit");
            AddButton(window.DisbandFleetButton, "disband");
            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        private void AddButton(AgeControlButton button, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            _cells.Add(
                Cells.Control(
                    widget,
                    button,
                    AgeWidgets.Raw(widget),
                    AgeWidgets.TextOf(widget),
                    Keys + "button/" + key
                )
            );
        }

        // ---- the ships of the picked fleet ----

        /// <summary>The ship list the game slides out under the fleets once one is picked, read by the
        /// shared garrison reader. Declared only while it is drawn, which is the game's own answer to
        /// whether a fleet has been picked.</summary>
        private void BuildShips(GraphBuilder builder, global::MilitaryScreen window)
        {
            MilitaryShipsListPanel panel = window.ShipsListPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(ShipsStop);
            builder.PushContext(ModStrings.Get(ModStrings.MilitaryShipsPanel));
            _cells.Clear();
            ShipRows.Toolbar(_cells, panel, Keys + "ships", false);
            ShipRows.Ships(_cells, panel, Keys + "ships", false);
            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        // ---- the ship designs ----

        /// <summary>The templates the empire builds ships from, one row each, then the four things that
        /// can be done to the picked one. A row is a radio: the panel keeps exactly one design picked
        /// (<c>ShipDesignsListPanel.OnToggleShipDesignItem</c> :121-138) and the buttons act on it.
        /// </summary>
        private void BuildDesigns(GraphBuilder builder, global::MilitaryScreen window)
        {
            ShipDesignsListPanel panel = window.ShipDesignsPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(DesignsStop);
            string title = GroupTitle(panel.AgeTransform);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            _cells.Clear();
            AddDesigns(panel);
            Cells.Emit(builder, _cells);
            if (named)
            {
                builder.PopContext();
            }

            builder.BeginStop(DesignActionsStop);
            builder.PushContext(ModStrings.Get(ModStrings.MilitaryDesignActions));
            _cells.Clear();
            AddButton(panel.CreateShipDesignButton, "design-create");
            AddButton(panel.EditShipDesignButton, "design-edit");
            AddButton(panel.DeleteShipDesignButton, "design-delete");
            AddButton(panel.AutoUpgradeShipDesignButton, "design-auto-upgrade");
            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        /// <summary>The designs the panel is drawing. They are a wrapping STRIP rather than a column -
        /// tiles laid out left to right - so they go through the shared layout reader and are walked the
        /// way they are drawn.</summary>
        private void AddDesigns(ShipDesignsListPanel panel)
        {
            try
            {
                AgeTransform table = panel.ShipDesignItemsTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AddDesign(items[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("military: reading the ship designs threw: " + e);
            }
        }

        /// <summary>One design. Its own tooltip is the game's ship-design dossier, drawn by the tooltip
        /// window rather than written into the widget, and what the tile draws BESIDE the name is the
        /// sentence explaining the role icon - so that is a line of the tile's own.
        ///
        /// The name does NOT come off the drawn label: the tile is 64 pixels wide and the game writes
        /// an already-truncated string into it (<c>ShipDesignItem.Bind</c> :37 calls
        /// <c>GuiShipDesign.GetFullTitle(Title)</c>, whose label overload runs the text through
        /// <c>AgeUtils.TruncateString</c> with '.' as the ellipsis) - so a design the player named
        /// "Accessible Colony" is drawn, and would be read, as "Accessible .". The same overload glues
        /// the revision number on with no separator ("Settler2"). Passing a null label asks the game
        /// for the same title untruncated and spaced, which is what <see cref="DesignName"/> does;
        /// <see cref="AgeText.FullLabel"/> cannot help here because there is no key left in the label
        /// to re-resolve.</summary>
        private void AddDesign(AgeTransform widget)
        {
            ShipDesignItem item =
                widget == null ? null : widget.GetComponent<ShipDesignItem>();
            if (item == null || !AgeWidgets.Visible(widget) || item.GuiShipDesign == null)
            {
                return;
            }

            ShipDesignItem it = item;
            NodeVtable vtable = GraphNodes.Radio(
                () => DesignName(it),
                () => it.SelectionToggle != null && it.SelectionToggle.State,
                () => AgeWidgets.Toggle(it.SelectionToggle),
                () => AgeWidgets.Operable(it.AgeTransform),
                () => DesignDetails(it),
                it.Tooltip
            );
            AgeWidgets.Point(vtable, it.SelectionToggle, it.Tooltip, widget);
            Cells.Add(
                _cells,
                widget,
                ControlId.Referenced(item, Keys + "design/" + item.GetInstanceID()),
                vtable
            );
        }

        /// <summary>A design's whole name, from the model the tile was bound to rather than the box the
        /// game squeezed it into - see <see cref="AddDesign"/>.</summary>
        private static string DesignName(ShipDesignItem item)
        {
            try
            {
                GuiShipDesign design = item.GuiShipDesign;
                if (design != null)
                {
                    return AgeText.Clean(design.GetFullTitle(null));
                }
            }
            catch (Exception e)
            {
                Log.Warn("military: reading a design's name threw: " + e);
            }

            return AgeText.Label(item.Title);
        }

        /// <summary>What the role icon beside a design's name says for itself.</summary>
        private static IList<string> DesignDetails(ShipDesignItem item)
        {
            List<string> lines = new List<string>(1);
            try
            {
                AgeTransform icon =
                    item.RoleIcon == null ? null : item.RoleIcon.AgeTransform;
                if (icon == null || !AgeWidgets.Visible(icon))
                {
                    return lines;
                }

                IList<string> role = AgeWidgets.TooltipLines(AgeWidgets.Raw(icon))();
                for (int i = 0; role != null && i < role.Count; i++)
                {
                    lines.Add(role[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("military: reading a design's role threw: " + e);
            }

            return lines;
        }

        // ---- the picked design or ship written out ----

        /// <summary>The box the game writes the picked design or the picked ship into, while it is
        /// drawing it - read-only: it has no controls in it at all, and the two bonus lines are there
        /// only for a design that has them (<c>ShipDesignOverviewPanel.Refresh</c> :46-79).</summary>
        private void BuildOverview(GraphBuilder builder, global::MilitaryScreen window)
        {
            ShipDesignOverviewPanel panel = window.ShipDesignOverviewPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(OverviewStop);
            string title = AgeText.Label(panel.TitleLabel);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            _cells.Clear();
            AddReadout(panel.NameLabel, null, "overview/name");
            AddReadout(panel.HullLabel, panel.HullTooltip, "overview/hull");
            AddReadout(panel.SizeLabel, panel.SizeTooltip, "overview/size");
            AddReadout(panel.RoleLabel, panel.RoleTooltip, "overview/role");
            AddReadout(panel.Bonus1Label, panel.Bonus1Tooltip, "overview/bonus1");
            AddReadout(panel.Bonus2Label, panel.Bonus2Tooltip, "overview/bonus2");
            AddStat(panel.HealthLabel, "%ShipStatHealthTitle", "overview/health");
            AddStat(panel.ManPowerLabel, "%ShipStatManpowerTitle", "overview/manpower");
            AddStat(panel.OffensivePowerLabel, "%ShipStatOffensiveMilitaryPowerTitle", "overview/offence");
            AddStat(panel.DefensivePowerLabel, "%ShipStatDefensiveMilitaryPowerTitle", "overview/defence");
            AddStat(panel.MovementPointsLabel, "%ShipStatMovementTitle", "overview/movement");
            AddStat(panel.CommandPointsLabel, "%ShipStatCommandPointsTitle", "overview/command-points");
            Cells.Emit(builder, _cells);
            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>One line of the overview box. The label and the sentence explaining it are separate
        /// fields on the panel, so the tooltip is named rather than looked for.</summary>
        private void AddReadout(AgePrimitiveLabel label, AgeTooltip tooltip, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform at = widget;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(at)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(_cells, widget, ControlId.Referenced(widget, Keys + key), vtable);
        }

        /// <summary>
        /// One of the six figures along the bottom of the overview box - health, troops, the two military
        /// powers, movement, command points.
        ///
        /// The game draws each as a number beside a bare symbol and names it nowhere on screen, so the
        /// caption is the game's OWN title for the statistic (<c>%ShipStat…Title</c>) rather than a mod
        /// paraphrase. The sentence explaining it is on the group AROUND the number, which is where the
        /// panel itself writes it (<c>ShipDesignBasePanel.Refresh</c> :120-123) - and only four of the six
        /// get one, so the other two are a caption and a number and nothing else.
        /// </summary>
        private void AddStat(AgePrimitiveLabel label, string titleKey, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform at = widget;
            string caption = AgeText.Clean(titleKey);
            NodeVtable vtable = GraphNodes.Readout(
                () => caption,
                () => AgeWidgets.TextOf(at),
                null,
                AgeWidgets.Raw(widget.Parent) ?? AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(_cells, widget, ControlId.Referenced(widget, Keys + key), vtable);
        }

        // ---- reading the window ----

        /// <summary>
        /// The heading a group on this page writes across its own top - "Fleets List", "Ship Designs".
        /// Neither is exposed as a field, so it is found where it is drawn.
        ///
        /// Searched UPWARDS as well as down, because the heading is not always the contents' sibling: the
        /// designs panel writes its own, while the fleets table sits one box further in than the group
        /// the heading belongs to.
        /// </summary>
        private static string GroupTitle(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; depth < 3 && at != null; depth++)
                {
                    AgeTransform title = AgeWidgets.ChildNamed(at, "Title", 0);
                    if (title != null)
                    {
                        return AgeWidgets.TextOf(title);
                    }

                    at = at.Parent;
                }
            }
            catch (Exception) { }

            return null;
        }

        private static string GroupTitle(GuiTable table)
        {
            try
            {
                return table == null ? null : GroupTitle(table.AgeTransform);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The fleet a row stands for. The wrapper the table binds is rebuilt on every refresh,
        /// so it is the fleet underneath it that identifies the row.</summary>
        private static Fleet FleetOf(GuiTableLine line)
        {
            try
            {
                GuiGarrison wrapper = line == null ? null : line.Data as GuiGarrison;
                return wrapper == null ? null : wrapper.Fleet;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the row is called when the name column draws nothing - the fleet's own name.
        /// </summary>
        private static string FleetName(GuiTableLine line)
        {
            try
            {
                GuiGarrison wrapper = line == null ? null : line.Data as GuiGarrison;
                return wrapper == null ? null : AgeText.Clean(wrapper.LocalizedName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiTable Table(global::MilitaryScreen window)
        {
            try
            {
                return window == null ? null : window.FleetsTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ScreenTitle()
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle("MilitaryScreen"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static global::MilitaryScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::MilitaryScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
