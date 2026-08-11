using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Screens;

namespace ES2Access.UI
{
    /// <summary>
    /// The ship designer's body, wherever the game draws it.
    ///
    /// <c>ShipDesignEditionPanel</c> is one prefab with two hosts: the Military screen's Create and Edit
    /// buttons put it in <c>ShipDesignModalWindow</c>, and a hero's inspection window hosts the same
    /// panel on its ship page. So the four bands the panel itself draws are declared here and the HOST
    /// declares only its own frame - the heading it writes and the row of buttons along its bottom -
    /// which is where the two differ (Apply/Create/Auto/Reset against Apply/Reset/Auto/Back).
    ///
    /// The bands are Tab stops, in the order they are drawn across the window: the structural
    /// information and costs down the left edge (x40), the module list beside it (x350), the ship's
    /// statistics on the right (x980), and the ship's own module slots in the middle (y304 and below,
    /// under all three). Measured rects, not the widget tree, which nests all four under one panel.
    ///
    /// Two of those bands are read from the SHAPE of what is drawn rather than modelled field by field
    /// (<see cref="SidePanels.Content"/>): the costs table and the statistics table are stacks of
    /// captioned groups of numbers, forty-odd labels between them, and every one of them is a readout.
    /// The characteristics band is written out row by row instead, because three of its five rows are
    /// controls (a name to type, a hull to choose) or carry a tooltip on the VALUE that the shape walk
    /// would drop in favour of the group's.
    ///
    /// **Slots and modules are peers of one kind, so both linearise** (ui-navigation's roster-grid
    /// rule): the module list is drawn as a two-row wrapping strip and the slots are scattered around a
    /// 3D ship by <c>ComputeModuleSlotPosition2D</c>, and in both cases a 2D walk would be walking a
    /// rendering accident. One node per tile, in drawn reading order.
    ///
    /// **What the mouse can do to a module, and what the keyboard therefore does.** Measured on the
    /// live prefab, because none of it is guessable from the class:
    ///
    /// - a module tile's own button is <c>UseLeftClick=false</c>, <c>UseDoubleClick=true</c> - a single
    ///   click does NOTHING at all, and the double click is <c>OnDoubleClickCb</c> ->
    ///   <c>OnModuleAutoEquip</c>, which drops the module into the first slot that will take it. So
    ///   Enter on a module row is silent (click parity: the game answers a single click with silence)
    ///   and Alt+Enter - the control's other activation - is the auto-equip.
    /// - a slot's drawn button, empty or filled, is wired to <c>OnSlotUnequipCb</c>. So Enter on a slot
    ///   is that click: it takes the module out, and on an empty slot the game's own handler finds
    ///   nothing to take and does nothing.
    /// - moving a module into a particular slot is the game's DRAG, so it is the mod's carry: Space on
    ///   a module row (or on a filled slot, which the game also lets the mouse drag from) picks it up,
    ///   every slot that would take it says so while it is held, and Enter on one puts it there.
    ///
    /// The carry commits through the game's own <c>IDragDropClient.ApplyDrop</c> with the dragged-item
    /// record filled in exactly as <c>OnModuleItemDragStarted</c> fills it, which is what buys the
    /// swap-and-displace rules for a slot-to-slot move for free. What it deliberately does NOT do is
    /// call <c>DragDropWindow.StartDragDrop</c>: that starts a real drag that follows the mouse and
    /// ends when a button nobody is holding comes up. Whether a slot will take the carried module is
    /// the game's own <c>SlotDefinition.CanModuleBeBound</c> against the design's own prerequisite
    /// context - the same expression the game builds its own valid-target list from.
    /// </summary>
    public static class ShipDesignRows
    {
        /// <summary>What a carried ship module is, for the slots that will take one - see
        /// <see cref="CarryItem.Kind"/>.</summary>
        public const string ModuleKind = "ship-module";

        /// <summary>How the module list's category strip maps onto the game's own filter enum: the
        /// toggles are in the enum's order (<c>GuiRadioGroup.OnToggleSwitchCb</c> sends the toggle's
        /// index as the selection), so the index is what names a toggle the game drew as a bare icon.
        /// </summary>
        private static readonly string[] CategoryTitles =
        {
            null,
            "CategoryModuleWeapon",
            "CategoryModuleDefense",
            "CategoryModuleSupport",
            "CategoryModuleSquadron",
        };

        public static object InfoStop(string prefix)
        {
            return prefix + "/info";
        }

        public static object ModulesStop(string prefix)
        {
            return prefix + "/modules";
        }

        public static object StatsStop(string prefix)
        {
            return prefix + "/stats";
        }

        public static object SlotsStop(string prefix)
        {
            return prefix + "/slots";
        }

        /// <summary>The four bands the panel draws, in the order it draws them. The host has already
        /// declared its heading and declares its buttons after this.</summary>
        public static void Build(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            TextFieldEditor editor,
            List<Cell> cells
        )
        {
            if (panel == null || panel.GuiShipDesign == null)
            {
                return;
            }

            BuildInfo(builder, panel, prefix, editor, cells);
            BuildModules(builder, panel, prefix, cells);
            BuildStats(builder, panel, prefix, cells);
            BuildSlots(builder, panel, prefix, cells);
        }

        // ---- the left edge: what the design is, and what it costs ----

        /// <summary>
        /// The two captioned boxes down the left edge - the game draws "Structural Information" over the
        /// first and "Costs" over the second, and a row has to be heard in the box it belongs to, so
        /// each is a region of the one stop.
        ///
        /// Only while both are drawn, though: until a hull is chosen the design is invalid and the game
        /// hides the costs outright (<c>Refresh</c> :713-731), and one region on its own is a region
        /// jump that swallows every press.
        /// </summary>
        private static void BuildInfo(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            TextFieldEditor editor,
            List<Cell> cells
        )
        {
            try
            {
                bool characteristics = AgeWidgets.Visible(panel.CharacteristicsTable);
                AgeTransform costs = panel.CostsTable;
                bool costed = costs != null && AgeWidgets.Visible(costs);
                if (!characteristics && !costed)
                {
                    return;
                }

                builder.BeginStop(InfoStop(prefix));
                bool regions = characteristics && costed;
                if (characteristics)
                {
                    if (regions)
                    {
                        builder.SetRegion(prefix + "/info/characteristics");
                    }

                    BuildCharacteristics(builder, panel, prefix, editor);
                }

                if (!costed)
                {
                    return;
                }

                if (regions)
                {
                    builder.SetRegion(prefix + "/info/costs");
                }

                cells.Clear();
                SidePanels.Content(cells, costs, prefix + "/cost/", Resources, null);
                Cells.Emit(builder, cells);

                // The last band of the same box, drawn under the costs (y538) and only for a mothership
                // design: the game's own warning about what an Ark can carry. It belongs to neither
                // captioned group, so it is declared after both and in no region.
                builder.SetRegion(null);
                AgeTransform notice =
                    panel.ArksVisualNoticeLabel == null
                        ? null
                        : panel.ArksVisualNoticeLabel.AgeTransform;
                if (notice != null && AgeWidgets.Visible(notice))
                {
                    SettingRows.AddReadout(builder, notice, prefix + "/info/notice");
                }
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the left-hand column threw: " + e);
            }
        }

        /// <summary>
        /// The name, hull, size, role and hull bonuses, in the order the panel stacks them.
        ///
        /// Written out rather than walked, for the reason the class remarks on: the name is a box to
        /// type in, the hull is a list to open while a design is being CREATED and a plain label
        /// afterwards (the game swaps the two, <c>RefreshHull</c> :962-988), and size and role keep the
        /// sentence explaining the VALUE on the value's own label - which a shape walk would drop in
        /// favour of the group's "The size of this Ship".
        /// </summary>
        private static void BuildCharacteristics(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            TextFieldEditor editor
        )
        {
            AgeTransform caption = FirstLabel(panel.CharacteristicsTable);
            if (caption != null)
            {
                SettingRows.AddReadout(builder, caption, prefix + "/info/caption");
            }

            AddName(builder, panel, prefix, editor);
            AddHull(builder, panel, prefix);
            SettingRows.AddReadout(builder, Group(panel.SizeLabel), prefix + "/info/size");
            SettingRows.AddReadout(builder, Group(panel.RoleLabel), prefix + "/info/role");
            AddBonuses(builder, panel, prefix);
        }

        /// <summary>The design's name. The game lets it be typed only while a design is being created
        /// and marks the box read-only for an existing one (<c>InitializeNameTextField</c> :920-931),
        /// which the row inherits: it announces itself as a box that is refusing rather than as a line
        /// of text, because that is what a sighted player is looking at.</summary>
        private static void AddName(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            TextFieldEditor editor
        )
        {
            AgeTransform group = Group(panel.NameTextField);
            AgeTransform label = group == null ? null : FirstLabel(group);
            SettingRows.AddTextField(
                builder,
                panel.NameTextField,
                label == null ? null : (Func<string>)(() => AgeWidgets.TextOf(label)),
                AgeWidgets.Raw(group),
                panel,
                NameFieldGainFocus,
                panel.NameTextField == null
                    ? null
                    : ControlId.Referenced(panel.NameTextField, prefix + "/info/name"),
                editor
            );
        }

        /// <summary>The hull: the game's own drop list while one is being chosen, the hull's name
        /// afterwards. Whichever of the two the panel is DRAWING is what is declared - the mode the
        /// window was opened in is opener-set state that can go stale, what is on the screen cannot.
        /// </summary>
        private static void AddHull(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix
        )
        {
            AgeTransform list = AgeWidgets.Transform(panel.HullDropList);
            AgeTransform group = list == null ? null : list.Parent;
            AgeTransform caption = group == null ? null : FirstLabel(group);
            if (list != null && AgeWidgets.Visible(list))
            {
                SettingRows.AddCombo(
                    builder,
                    panel.HullDropList,
                    caption == null ? null : (Func<string>)(() => AgeWidgets.TextOf(caption)),
                    AgeWidgets.Raw(group),
                    prefix + "/info/hull"
                );
                return;
            }

            SettingRows.AddReadout(builder, Group(panel.HullLabel), prefix + "/info/hull-name");
        }

        /// <summary>What the hull gives the ship: a caption the game draws over up to three separate
        /// bonuses, so the caption is a node of its own and each bonus is a line with its own
        /// explanation.</summary>
        private static void AddBonuses(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix
        )
        {
            if (panel.BonusesGroup == null || !AgeWidgets.Visible(panel.BonusesGroup))
            {
                return;
            }

            AgeTransform caption = FirstLabel(panel.BonusesGroup);
            if (caption != null)
            {
                SettingRows.AddReadout(builder, caption, prefix + "/info/bonuses");
            }

            AddBonus(builder, panel.Bonus1Label, prefix + "/info/bonus/1");
            AddBonus(builder, panel.Bonus2Label, prefix + "/info/bonus/2");
            AddBonus(builder, panel.Bonus3Label, prefix + "/info/bonus/3");
        }

        private static void AddBonus(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget != null && AgeWidgets.Visible(widget))
            {
                SettingRows.AddReadout(builder, widget, key);
            }
        }

        /// <summary>
        /// A cost or a stock the game drew as a bare symbol and a number.
        ///
        /// The shape walk would leave it saying "50" and "has tooltip": the resource's own name is on
        /// the tooltip's wrapper (<c>ShipDesignResourceItem.RefreshCost</c> sets Target and a CLASS, so
        /// the words are the renderer's) and the symbol beside the number is a picture. Same reading
        /// the economy screen's resource grid gets, for the same reason.
        /// </summary>
        private static bool Resources(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            ShipDesignResourceItem cost = widget.GetComponent<ShipDesignResourceItem>();
            if (cost != null)
            {
                AddNamed(cells, widget, cost.Tooltip, cost.StockLabel, keyPrefix + widget.name);
                return true;
            }

            ResourceItem stock = widget.GetComponent<ResourceItem>();
            if (stock != null)
            {
                // The empire's own stock, drawn here as a reminder. It answers a click only in the
                // developers' god mode (<c>ResourceItem.OnClickCb</c> :166-178), so it is a readout.
                AddNamed(cells, widget, stock.Tooltip, stock.StockLabel, keyPrefix + widget.name);
                return true;
            }

            return false;
        }

        private static void AddNamed(
            List<Cell> cells,
            AgeTransform widget,
            AgeTooltip tooltip,
            AgePrimitiveLabel value,
            string key
        )
        {
            if (!AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tip = tooltip ?? AgeWidgets.Raw(widget);
            AgePrimitiveLabel it = value;
            string name = AgeWidgets.TooltipTitle(tip);
            bool named = !string.IsNullOrEmpty(name);
            NodeVtable vtable = GraphNodes.Readout(
                () => named ? name : CardActions.FirstLine(tip),
                () => AgeText.Label(it),
                null,
                tip
            );
            if (!named)
            {
                // The tooltip IS the name here, and a control does not say the same sentence twice.
                vtable.Sections = GraphNodes.Sections(null, tip, TooltipMode.None);
            }

            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        // ---- the module list ----

        /// <summary>
        /// The modules the empire can fit, the two switches that decide which of them are drawn, and
        /// the strip itself.
        ///
        /// The category strip is a <c>GuiRadioGroup</c> - exactly one filter is in force - and its
        /// toggles are drawn as bare icons except the first. What names them is the game's own word for
        /// the module category each one keeps, which the toggle's index gives (see
        /// <see cref="CategoryTitles"/>).
        /// </summary>
        private static void BuildModules(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            try
            {
                if (panel.ModulesGroup == null || !AgeWidgets.Visible(panel.ModulesGroup))
                {
                    return;
                }

                builder.BeginStop(ModulesStop(prefix));
                cells.Clear();
                // The panel names the label it writes "Modules" into after the statistics box it was
                // copied from; what it DRAWS is the caption over this band.
                AgeTransform caption = FirstLabel(panel.ModulesGroup);
                if (caption != null)
                {
                    cells.Add(
                        Cells.Readout(caption, AgeWidgets.Raw(caption), prefix + "/modules/caption")
                    );
                }

                AddCategories(cells, panel, prefix);
                AddObsolete(cells, panel, prefix);
                Cells.Emit(builder, cells);

                cells.Clear();
                AgeTransform table = panel.ModulesTable;
                ShipDesignModuleItem[] items =
                    table == null
                        ? new ShipDesignModuleItem[0]
                        : table.GetComponentsInChildren<ShipDesignModuleItem>(true);
                for (int i = 0; i < items.Length; i++)
                {
                    AddModule(cells, panel, items[i], prefix, i);
                }

                EmitLinear(builder, cells);
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the module list threw: " + e);
            }
        }

        private static void AddCategories(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string prefix
        )
        {
            GuiRadioGroup group = panel.ModuleCategoriesGroup;
            AgeTransform table = group == null ? null : group.TogglesTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            AgeControlToggle[] toggles = table.GetComponentsInChildren<AgeControlToggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                AgeControlToggle toggle = toggles[i];
                AgeTransform widget = AgeWidgets.Transform(toggle);
                if (toggle == null || !AgeWidgets.Visible(widget))
                {
                    continue;
                }

                AgeControlToggle it = toggle;
                int index = i;
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = GraphNodes.Radio(
                    () => CategoryName(widget, index, tooltip),
                    () => it.State,
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Operable(widget),
                    null,
                    tooltip
                );
                AgeWidgets.Point(vtable, it);
                Cells.Add(
                    cells,
                    widget,
                    ControlId.Referenced(toggle, prefix + "/modules/category/" + i),
                    vtable
                );
            }
        }

        /// <summary>What a category toggle is called: the word it draws where it draws one, else the
        /// game's own title for the module category it keeps, else the sentence it explains itself
        /// with.</summary>
        private static string CategoryName(AgeTransform widget, int index, AgeTooltip tooltip)
        {
            string drawn = AgeWidgets.TextOf(widget);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            string element = index >= 0 && index < CategoryTitles.Length
                ? CategoryTitles[index]
                : null;
            string title = element == null ? null : AgeText.Clean(Gui.GetTitle(element));
            if (!string.IsNullOrEmpty(title) && title[0] != '%')
            {
                return title;
            }

            return CardActions.FirstLine(tooltip);
        }

        private static void AddObsolete(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string prefix
        )
        {
            AgeControlToggle toggle = panel.ShowObsoleteModulesToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeWidgets.TextOf(widget),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            Cells.Add(
                cells,
                widget,
                ControlId.Referenced(toggle, prefix + "/modules/obsolete"),
                vtable
            );
        }

        /// <summary>
        /// One module the empire could fit. The tile draws a picture and nothing else, so the name is
        /// the one the game keeps on the wrapper behind its tooltip - and the dossier that tooltip
        /// assembles (what the module does, what it costs) is indicated and walkable in the review
        /// buffer rather than recited on every pass.
        ///
        /// Keyed on the WRAPPER rather than on the tile: the strip pools its tiles and rebinds them
        /// whenever the filter or the obsolete switch changes what is drawn, while the wrappers
        /// themselves are built once (<c>CreateGuiModulesByFamilies</c> :807-829) and outlive every
        /// refresh.
        ///
        /// A module the empire cannot fit yet is drawn disabled with its reasons on the wrapper
        /// (<c>ShipDesignModuleItem.Bind</c> :31), which is where the refusal comes from.
        /// </summary>
        private static void AddModule(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            ShipDesignModuleItem item,
            string prefix,
            int index
        )
        {
            if (item == null || item.GuiEditionModule == null || !AgeWidgets.Visible(item.AgeTransform))
            {
                return;
            }

            ShipDesignModuleItem it = item;
            AgeTooltip tooltip = item.Tooltip ?? AgeWidgets.Raw(item.AgeTransform);
            Func<bool> enabled = () => AgeWidgets.Operable(it.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModuleName(it)),
                    GraphNodes.DisabledPart(enabled),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                // A single click on a module tile does nothing at all (UseLeftClick is false, measured),
                // so Enter does nothing either; the double click is the auto-equip.
                OnAlternate = () => AutoEquip(it, enabled),
                OnPickUp = () => PickModule(it),
            };
            GraphNodes.AddRefusal(vtable, tooltip, enabled);

            AgeWidgets.PointAt(vtable, item.AgeTransform);
            Cells.Add(
                cells,
                item.AgeTransform,
                ControlId.Referenced(item.GuiEditionModule, prefix + "/module/" + index),
                vtable
            );
        }

        /// <summary>Put the module in the first slot that will take it - the tile's own double click,
        /// which is the only activation the game gives it.</summary>
        private static void AutoEquip(ShipDesignModuleItem item, Func<bool> enabled)
        {
            if (!enabled())
            {
                return;
            }

            AgeWidgets.DoubleClick(AgeWidgets.Button(AutoEquipButton(item)));
        }

        /// <summary>The tile's own button - a child of the drag area rather than the tile itself, which
        /// is where the game hangs the double click.</summary>
        private static AgeTransform AutoEquipButton(ShipDesignModuleItem item)
        {
            try
            {
                AgeControlButton button =
                    item.AgeTransform.GetComponentInChildren<AgeControlButton>(true);
                return button == null ? null : button.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ModuleName(ShipDesignModuleItem item)
        {
            try
            {
                return item.GuiEditionModule == null
                    ? null
                    : AgeText.Clean(item.GuiEditionModule.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the ship's statistics ----

        /// <summary>
        /// What the design comes out as: the four running totals, the two balance gauges, the range
        /// accuracies, and - behind the game's own switch - the per-weapon and per-defence breakdowns.
        ///
        /// Read from the shape of what is drawn, because that is what it is: thirty-odd numbers, each
        /// with the sentence explaining it on its own group, and not one of them a control. The two
        /// exceptions are declared for it: the switch itself is a checkbox, and a gauge carries its
        /// value in the width of a bar.
        /// </summary>
        private static void BuildStats(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            try
            {
                AgeTransform table = panel.StatisticsTable;
                if (table == null || !AgeWidgets.Visible(table))
                {
                    return;
                }

                cells.Clear();
                _statsPanel = panel;
                SidePanels.Content(cells, table, prefix + "/stat/", Stats, null);
                _statsPanel = null;
                if (cells.Count == 0)
                {
                    return;
                }

                builder.BeginStop(StatsStop(prefix));
                Cells.Emit(builder, cells);
            }
            catch (Exception e)
            {
                _statsPanel = null;
                Log.Warn("ship design: reading the statistics threw: " + e);
            }
        }

        /// <summary>The panel whose statistics are being read, for <see cref="Stats"/> - the walk's own
        /// callback shape carries a side panel, and this band is not one. Main-thread only, and set
        /// only across the one call.</summary>
        private static ShipDesignEditionPanel _statsPanel;

        private static bool Stats(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            ShipDesignEditionPanel owner = _statsPanel;
            if (
                owner != null
                && owner.ShowDetailedStatsToggle != null
                && ReferenceEquals(widget, owner.ShowDetailedStatsToggle.AgeTransform)
            )
            {
                AddDetailedSwitch(cells, owner, keyPrefix);
                return true;
            }

            RepartitionHorizontalGauge gauge = widget.GetComponent<RepartitionHorizontalGauge>();
            if (gauge != null)
            {
                AddGauge(cells, gauge, keyPrefix + widget.name);
                return true;
            }

            // The three damage-per-second figures the game draws under the three range accuracies, one
            // to a column. They are all labels of one group, so the shape walk would make them one line
            // - and three identical "(0)"s at that, with the sentence saying which range each belongs to
            // left on its own label. One line each instead.
            if (
                owner != null
                && owner.LongRangeDPSLabel != null
                && ReferenceEquals(widget, owner.LongRangeDPSLabel.AgeTransform.Parent)
            )
            {
                IList<AgeTransform> columns = widget.Children;
                for (int i = 0; columns != null && i < columns.Count; i++)
                {
                    Cells.AddReadout(cells, columns[i], keyPrefix + widget.name + "/" + i);
                }

                return true;
            }

            return false;
        }

        /// <summary>The game's own switch for the breakdowns, which is a tick box and has to read as
        /// one: the walk would have made it a line of text saying "Detailed stats".</summary>
        private static void AddDetailedSwitch(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string keyPrefix
        )
        {
            AgeControlToggle toggle = panel.ShowDetailedStatsToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeWidgets.TextOf(widget),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.Referenced(toggle, keyPrefix + "detailed"), vtable);
        }

        /// <summary>
        /// A balance bar: two halves growing out of the middle, with no words anywhere on it. What it
        /// says is the PROPORTION each half was given (<c>RepartitionHorizontalGauge.Refresh</c> turns
        /// a value into a percentage of the bar's half-width), and what the two halves ARE is the
        /// sentence on its own tooltip - "the balance between projectile and energy weapons". Read off
        /// the drawn geometry, which is the only place the game put it.
        /// </summary>
        private static void AddGauge(
            List<Cell> cells,
            RepartitionHorizontalGauge gauge,
            string key
        )
        {
            AgeTransform widget = gauge.AgeTransform;
            if (!AgeWidgets.Visible(widget))
            {
                return;
            }

            RepartitionHorizontalGauge it = gauge;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Readout(() => null, () => GaugeText(it), null, tooltip);
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        private static string GaugeText(RepartitionHorizontalGauge gauge)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                Add(message, Share(gauge.LeftGauge, gauge.LeftGauge.PercentLeft));
                Add(message, Share(gauge.RightGauge, gauge.RightGauge.PercentRight - 100f));
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One half of a balance bar as the share it was drawn at. Each half is anchored at
        /// the middle and stretched out to its own side by half of its share, so the distance from the
        /// middle IS the proportion.</summary>
        private static string Share(AgeTransform half, float edge)
        {
            if (half == null || !half.Visible)
            {
                return null;
            }

            int percent = (int)Math.Abs(Math.Round((edge - 50f) * 2f));
            return percent + "%";
        }

        // ---- the ship's own module slots ----

        /// <summary>
        /// Where the modules go: the slots the game draws over the rendered ship, one row each.
        ///
        /// The container has no caption of its own - the slots are drawn ON the ship - so the stop gets
        /// a word of the mod's. Their drawn positions come out of the 3D model
        /// (<c>ComputeModuleSlotPosition2D</c>), so they are put in reading order and then walked as a
        /// list: they are peers of one kind and a 2D walk over where a hull happens to put its guns
        /// would be a walk over nothing.
        /// </summary>
        private static void BuildSlots(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            try
            {
                AgeTransform container = panel.ShipDesignSlotItemsContainer;
                if (container == null || !AgeWidgets.Visible(container))
                {
                    return;
                }

                cells.Clear();
                ShipDesignEditionSlotItem[] slots =
                    container.GetComponentsInChildren<ShipDesignEditionSlotItem>(true);
                for (int i = 0; i < slots.Length; i++)
                {
                    AddSlot(cells, panel, slots[i], prefix, i);
                }

                if (cells.Count == 0)
                {
                    return;
                }

                builder.BeginStop(SlotsStop(prefix));
                builder.PushContext(ModStrings.Get(ModStrings.ShipDesignSlots));
                EmitLinear(builder, cells);
                builder.PopContext();
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the module slots threw: " + e);
            }
        }

        /// <summary>
        /// One slot: what is in it, what it will take, and the markers the game draws round its edge -
        /// the module categories it is restricted to, the multiplier it applies, and the resource it
        /// costs on top of the module.
        ///
        /// Filled, it is the module's name and a button that takes the module out; empty, it is the
        /// game's own word for a slot and no action, because that is what a click on it does. Either
        /// way it is where a carried module is put down, and it says so while one is held.
        ///
        /// Keyed on the GuiSlot rather than on the item drawing it: the container pools its items and
        /// rebinds them on every refresh of the ship, so a cursor keyed on <c>Item000</c> would be
        /// standing on a different slot a frame after a module was fitted.
        /// </summary>
        private static void AddSlot(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            ShipDesignEditionSlotItem slot,
            string prefix,
            int index
        )
        {
            if (slot == null || slot.GuiSlot == null || !AgeWidgets.Visible(slot.AgeTransform))
            {
                return;
            }

            ShipDesignEditionSlotItem it = slot;
            ShipDesignEditionPanel owner = panel;
            AgeTooltip tooltip = slot.SlotTooltip ?? AgeWidgets.Raw(slot.AgeTransform);
            Func<bool> enabled = () => AgeWidgets.Operable(it.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                // A control either way, filled or empty, and its parts never come and go: the watch
                // below re-baselines instead of speaking whenever a row's part LIST changes shape, so a
                // node that dropped its role word as it emptied swallowed the very change the watch is
                // there to announce (measured - Enter on a filled slot emptied it in silence).
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    // WATCHED, unlike every other name in the mod: what a slot is called IS what is in
                    // it, and both of the things the player does to a slot - Enter to take the module
                    // out, a drop to put one in - change it under a cursor that is standing right
                    // there. Both of those are silent in the game, so the live part is the only
                    // feedback either gesture has.
                    new NodeAnnouncement(
                        () => SlotName(it),
                        live: true,
                        kind: AnnouncementKinds.Label
                    ),
                    GraphNodes.DisabledPart(enabled),
                    GraphNodes.ValuePart(() => SlotMarkers(it), false),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                DropKind = ModuleKind,
                OnDrop = held => Drop(owner, it, held),
                // The slot's own click, empty or filled, is "take the module out"
                // (<c>OnSlotUnequipCb</c> is wired to both of the groups the slot draws): on an empty
                // one the game's handler finds nothing to take and does nothing, which is what a mouse
                // gets too. An empty slot offers nothing to pick up, and the carry key is silent there.
                OnActivate = () =>
                {
                    if (enabled())
                    {
                        AgeWidgets.Press(DrawnButton(it));
                    }
                },
                OnPickUp = () => PickSlot(it),
            };

            CarryState carry = ModEntry.Carry;
            if (carry != null)
            {
                vtable.Announcements.Add(
                    new NodeAnnouncement(
                        () =>
                            carry.Accepts(ModuleKind) && Takes(owner, it, carry.Held)
                                ? ModStrings.Get(ModStrings.CarryDropTarget)
                                : null,
                        live: true
                    )
                );
            }

            AgeWidgets.PointAt(vtable, slot.AgeTransform);
            Cells.Add(
                cells,
                slot.AgeTransform,
                ControlId.Referenced(slot.GuiSlot, prefix + "/slot/" + index),
                vtable
            );
        }

        private static bool Filled(ShipDesignEditionSlotItem slot)
        {
            try
            {
                return slot.GuiSlot != null && slot.GuiSlot.GuiModule != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>What the slot is called: the module in it, or the game's own word for an empty slot
        /// (<c>GuiSlot.Title</c> answers both).</summary>
        private static string SlotName(ShipDesignEditionSlotItem slot)
        {
            try
            {
                return slot.GuiSlot == null ? null : AgeText.Clean(slot.GuiSlot.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The markers the game draws round a slot: the module categories it accepts (icons, one per
        /// restriction), the multiplier it applies to whatever is fitted, and the resource the slot
        /// itself costs.
        ///
        /// Asked when the row is read rather than watched, because it walks the slot's definition.
        /// </summary>
        private static string SlotMarkers(ShipDesignEditionSlotItem slot)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                string[] categories = slot.GuiSlot.ModuleTypeRestrictions;
                if (slot.SlotCategoriesTable != null && slot.SlotCategoriesTable.Visible)
                {
                    for (int i = 0; categories != null && i < categories.Length; i++)
                    {
                        string title = AgeText.Clean(Gui.GetTitle(categories[i]));
                        if (!string.IsNullOrEmpty(title) && title[0] != '%')
                        {
                            Add(message, title);
                        }
                    }
                }

                Add(message, Marker(slot.SlotMultiplier2));
                Add(message, Marker(slot.SlotMultiplier3));
                Add(message, Marker(slot.SlotMultiplier4));
                if (slot.SpecialCostMarker != null && slot.SpecialCostMarker.Visible)
                {
                    Add(message, AgeText.Clean(slot.GuiSlot.SpecialCost));
                }

                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Marker(AgeTransform marker)
        {
            return marker == null || !marker.Visible ? null : AgeWidgets.TextOf(marker);
        }

        private static void Add(MessageBuilder message, string fragment)
        {
            if (!string.IsNullOrEmpty(fragment))
            {
                message.ListItem(fragment);
            }
        }

        /// <summary>Whichever of the slot's two groups the game is drawing - the picture of the module
        /// in it or the empty frame - both of which carry the same click.</summary>
        private static AgeTransform DrawnButton(ShipDesignEditionSlotItem slot)
        {
            if (slot.EquipedModuleGroup != null && slot.EquipedModuleGroup.Visible)
            {
                return AgeWidgets.Transform(slot.EquipedModuleButton) ?? slot.EquipedModuleGroup;
            }

            return slot.UnequipedModuleGroup;
        }

        // ---- carrying a module ----

        private static CarryItem PickModule(ShipDesignModuleItem item)
        {
            try
            {
                return item.GuiEditionModule == null || !AgeWidgets.Operable(item.AgeTransform)
                    ? null
                    : new CarryItem(item, ModuleName(item), ModuleKind);
            }
            catch (Exception e)
            {
                Log.Warn("ship design: picking a module up threw: " + e);
                return null;
            }
        }

        private static CarryItem PickSlot(ShipDesignEditionSlotItem slot)
        {
            try
            {
                return !Filled(slot) || !AgeWidgets.Operable(slot.AgeTransform)
                    ? null
                    : new CarryItem(slot, SlotName(slot), ModuleKind);
            }
            catch (Exception e)
            {
                Log.Warn("ship design: picking a fitted module up threw: " + e);
                return null;
            }
        }

        /// <summary>The module being carried, whether it came off the list or out of another slot.
        /// </summary>
        private static GuiModule Carried(CarryItem held)
        {
            ShipDesignModuleItem tile = held == null ? null : held.Cargo as ShipDesignModuleItem;
            if (tile != null)
            {
                return tile.GuiEditionModule;
            }

            ShipDesignEditionSlotItem slot =
                held == null ? null : held.Cargo as ShipDesignEditionSlotItem;
            return slot == null || slot.GuiSlot == null ? null : slot.GuiSlot.GuiModule;
        }

        /// <summary>
        /// Whether this slot would take the carried module - the game's own test, against the design's
        /// own prerequisite context, which is the same expression <c>OnModuleItemDragStarted</c> :1512
        /// builds its valid-target list from, plus the design being editable at all.
        ///
        /// Deliberately NOT the slot's own enabled state, which answers a different question: the
        /// category strip DIMS every slot the current filter does not cover
        /// (<c>ShipDesignBaseSlotItem.RefreshCurrentFilter</c>), and the game's own drag re-enables the
        /// compatible ones for the duration of the drag (<c>RefreshForDragDrop</c> :213-221). Reading
        /// the dimming as a refusal would make a carry fail on exactly the slots a mouse drag succeeds
        /// on whenever a filter is in force.
        /// </summary>
        private static bool Takes(
            ShipDesignEditionPanel panel,
            ShipDesignEditionSlotItem slot,
            CarryItem held
        )
        {
            try
            {
                GuiModule module = Carried(held);
                if (
                    module == null
                    || panel.GuiShipDesign == null
                    || slot.GuiSlot == null
                    || panel.CurrentMode == ShipDesignEditionPanel.Mode.ReadOnly
                )
                {
                    return false;
                }

                return slot.GuiSlot.Slot.Definition.CanModuleBeBound(
                    module.ModuleDefinition,
                    panel.GuiShipDesign.ShipDesign.SlotPrerequisitesContext
                );
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Put the carried module in this slot, the way the drag does it: the game's own dragged-item
        /// record is filled in exactly as <c>OnModuleItemDragStarted</c> / <c>OnSlotItemDragStarted</c>
        /// fill it, and then the drag's own <c>ApplyDrop</c> commits - which is what keeps the
        /// swap-and-displace rules for a module coming out of another slot.
        ///
        /// Nothing here posts an order: the whole designer is a preview until the window's own Apply or
        /// Create button is pressed.
        /// </summary>
        private static DropResult Drop(
            ShipDesignEditionPanel panel,
            ShipDesignEditionSlotItem slot,
            CarryItem held
        )
        {
            try
            {
                if (ReferenceEquals(held.Cargo, slot))
                {
                    // Back into the slot it came out of: the drag ends having moved nothing, which is
                    // what putting it down on its own row means everywhere else.
                    return DropResult.Done(ModStrings.Get(ModStrings.CarryCancelled));
                }

                GuiModule module = Carried(held);
                if (module == null || !Takes(panel, slot, held))
                {
                    return DropResult.Refused(null);
                }

                DragDropWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<DragDropWindow>(false)
                    : null;
                if (window == null || window.ShipDesignModuleDraggedItem == null)
                {
                    return DropResult.Refused(null);
                }

                window.ShipDesignModuleDraggedItem.SourceItem = held.Cargo as GuiBehaviour;
                window.ShipDesignModuleDraggedItem.GuiModule = module;
                ((IDragDropClient)panel).ApplyDrop(slot);
                return DropResult.Done(
                    ModStrings.Format(ModStrings.ShipDesignModuleFitted, held.Name)
                );
            }
            catch (Exception e)
            {
                Log.Warn("ship design: fitting a carried module threw: " + e);
                return DropResult.Refused(null);
            }
        }

        // ---- shared ----

        /// <summary>Emit one node per cell, in the order the game drew them: peers of one kind read as
        /// a list, whatever shape the layout happens to wrap them into.</summary>
        private static void EmitLinear(GraphBuilder builder, List<Cell> cells)
        {
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    builder.AddItem(row[i].Id, row[i].Vtable);
                }
            }
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        /// <summary>The caption a band draws across its own top: the first label in it, which is where
        /// every one of these boxes puts its heading.</summary>
        private static AgeTransform FirstLabel(AgeTransform band)
        {
            try
            {
                IList<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child == null || !AgeWidgets.Visible(child))
                    {
                        continue;
                    }

                    if (child.GetComponent<AgePrimitiveLabel>() != null)
                    {
                        return child;
                    }

                    AgeTransform inside = FirstLabel(child);
                    if (inside != null)
                    {
                        return inside;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The row a label or a field is drawn in - the group the game put its caption and its
        /// value in together.</summary>
        private static AgeTransform Group(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || label.AgeTransform == null ? null : label.AgeTransform.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Group(AgeControlTextField field)
        {
            try
            {
                AgeTransform widget = AgeWidgets.Transform(field);
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The game's own handler for the name box gaining the keyboard, which clears its "type a name
        // here" prompt the way a click on it does. Resolved once.
        private static readonly System.Reflection.MethodInfo NameFieldGainFocus =
            OptionsScreen.Handler(
                typeof(ShipDesignEditionPanel),
                "OnNameTextFieldGainFocusCb"
            );
    }
}
