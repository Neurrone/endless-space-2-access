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
    /// The bands are Tab stops: the structural information and costs down the left edge (x40), the
    /// module list beside it (x350), the ship's own module slots in the middle (y304 and below, under
    /// all three), and the ship's statistics on the right (x980). Measured rects, not the widget tree,
    /// which nests all four under one panel. Slots come before statistics rather than after, because
    /// the slots are what the player is here to change and the statistics are what those changes come
    /// out as - the owner's ordering, 2026-08-18.
    ///
    /// Each band's own drawn caption is the LEVEL its rows sit under (<c>PushContext</c>) rather than a
    /// row of its own: a caption is heard on the way in, and a heading that is also a row is a row that
    /// says nothing the arrival did not already say. Inside a band, a captioned group is a labelled
    /// region and the rest are key-only regions, so Alt+Up/Down walks the band's sections either way.
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
    ///   and the double-click chord (Ctrl+Alt+Enter) is the auto-equip. Alt+Enter on a tile is silent:
    ///   the game wires no modified click here. <c>OnModuleAutoEquip</c> (:1489-1502) reads no
    ///   modifiers, so the Control and Alt the player is still holding change nothing about it - the
    ///   panel's two Control reads are elsewhere (<c>ApplyDropDraggedFromSlotItem</c> :1459, a
    ///   slot-to-slot drop, and <c>ShipDesignEditionSlotItem</c> :64, which only runs inside a real
    ///   <c>DragDropWindow</c> drag the mod never starts).
    /// - a slot's drawn button, empty or filled, is wired to <c>OnSlotUnequipCb</c>. So Enter on a slot
    ///   is that click: it takes the module out, and on an empty slot the game's own handler finds
    ///   nothing to take and does nothing.
    /// - moving a module into a particular slot is the game's DRAG, so it is the mod's carry: Space on
    ///   a module row (or on a filled slot, which the game also lets the mouse drag from) picks it up,
    ///   every slot that would take it says so while it is held, and Enter on one puts it there.
    /// - taking a module OFF the ship is the same drag released over nothing, which is nowhere a
    ///   keyboard can aim, so the slots end with a node of the mod's own that stands for "nothing"
    ///   (<see cref="AddRemoveTarget"/>).
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
        /// <summary>
        /// A design's whole name, for the overview box the Military screen and a hero's ship page both
        /// draw it in.
        ///
        /// Not off the drawn label: the panel writes <c>GetFullTitle(NameLabel)</c> into it
        /// (<c>ShipDesignOverviewPanel.Refresh</c> :51), and that overload truncates to the label's width
        /// with '.' as the ellipsis and glues the revision number on with no separator - the same reason
        /// the Military screen's design tiles ask the model instead. Passing a null label asks the game
        /// for the title untruncated and spaced.
        /// </summary>
        public static string OverviewName(ShipDesignOverviewPanel panel)
        {
            try
            {
                GuiShipDesign design = panel == null ? null : panel.GuiShipDesign;
                string full = design == null ? null : AgeText.Clean(design.GetFullTitle(null));
                return string.IsNullOrEmpty(full)
                    ? AgeText.Label(panel == null ? null : panel.NameLabel)
                    : full;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The six figures every host of the ship-design panel draws along its bottom - health, troops,
        /// the two military powers, movement, command points - each named from the game's own title for
        /// the statistic.
        ///
        /// <c>ShipDesignBasePanel.Refresh</c> (:113-119) writes the numbers into the six labels and
        /// writes no caption anywhere: measured on all three hosts of the prefab, the group around each
        /// number holds the label and the explaining sentence and nothing else. So the caption is the
        /// game's <c>%ShipStat…Title</c> string, and it lives here rather than in each host - the
        /// Military screen's overview box, a hero's ship page and the designer's own statistics table
        /// all draw the SAME panel, and the map was duplicated in two of them while the third read the
        /// numbers bare.
        ///
        /// <paramref name="declared"/> collects the groups these numbers were drawn in, for a host that
        /// also reads the band by SHAPE and must leave them to this.
        /// </summary>
        public static void AddSimpleStats(
            List<Cell> cells,
            ShipDesignBasePanel panel,
            string keyPrefix,
            List<AgeTransform> declared = null
        )
        {
            AddStat(cells, panel.HealthLabel, "%ShipStatHealthTitle", keyPrefix + "health", declared);
            AddStat(
                cells,
                panel.ManPowerLabel,
                "%ShipStatManpowerTitle",
                keyPrefix + "manpower",
                declared
            );
            AddStat(
                cells,
                panel.OffensivePowerLabel,
                "%ShipStatOffensiveMilitaryPowerTitle",
                keyPrefix + "offence",
                declared
            );
            AddStat(
                cells,
                panel.DefensivePowerLabel,
                "%ShipStatDefensiveMilitaryPowerTitle",
                keyPrefix + "defence",
                declared
            );
            AddStat(
                cells,
                panel.MovementPointsLabel,
                "%ShipStatMovementTitle",
                keyPrefix + "movement",
                declared
            );
            AddStat(
                cells,
                panel.CommandPointsLabel,
                "%ShipStatCommandPointsTitle",
                keyPrefix + "command-points",
                declared
            );
        }

        /// <summary>One captioned figure, remembering the group it was drawn in so the shape walk can
        /// skip it. <paramref name="column"/> marks a figure whose group is SHARED with its neighbours,
        /// which is then laid out by its own label instead.</summary>
        private static void AddStat(
            List<Cell> cells,
            AgePrimitiveLabel label,
            string titleKey,
            string key,
            List<AgeTransform> declared,
            bool column = false
        )
        {
            AgeTransform at = label == null ? null : label.AgeTransform;
            Cells.AddStat(cells, label, titleKey, key, column ? at : null);
            AgeTransform group = at == null ? null : at.Parent;
            if (declared != null && group != null && !declared.Contains(group))
            {
                declared.Add(group);
            }
        }

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
            BuildSlots(builder, panel, prefix, cells);
            BuildStats(builder, panel, prefix, cells);
        }

        // ---- the left edge: what the design is, and what it costs ----

        /// <summary>
        /// The two captioned boxes down the left edge, as one stop under the caption of the first: the
        /// game draws "Structural Information" over the characteristics and "Costs" over the box below
        /// it, and a row has to be heard in the box it belongs to, so each box is a region.
        ///
        /// Three of the four regions carry no label of their own. The leading block's caption has become
        /// the STOP's, so repeating it as a region name would say it twice on the way in; the Ark notice
        /// has no caption at all. A region with no label is still a jump target - what a key-only region
        /// buys is that Alt+Up/Down reaches those rows and does not dead-end on them.
        ///
        /// A region jump with nowhere to go is silent and consumed either way (<c>KeyGraph.MoveRegion</c>
        /// returns unmoved for a lone region exactly as it does for a node with no region at all), so the
        /// regions are declared whatever the box is drawing - including the state the earlier shape was
        /// careful about, a design with no hull yet, where the game hides the costs outright
        /// (<c>Refresh</c> :713-731) and the leading block is all there is.
        /// </summary>
        private static void BuildInfo(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            TextFieldEditor editor,
            List<Cell> cells
        )
        {
            bool labelled = false;
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
                labelled = Caption(builder, FirstLabel(panel.CharacteristicsTable));
                if (characteristics)
                {
                    builder.SetRegion(prefix + "/info/characteristics");
                    BuildCharacteristics(builder, panel, prefix, editor);
                }

                if (costed)
                {
                    BuildCosts(builder, panel, prefix, cells);
                }

                // The last band of the same box, drawn under the costs (y538) and only for a mothership
                // design: the game's own warning about what an Ark can carry. It belongs to neither
                // captioned group, so it is declared after both and gets a region of its own - unlabelled,
                // because the game drew no caption over it, and its own so that the jump out of the costs
                // reaches it and the jump back leaves it.
                AgeTransform notice =
                    panel.ArksVisualNoticeLabel == null
                        ? null
                        : panel.ArksVisualNoticeLabel.AgeTransform;
                if (notice != null && AgeWidgets.Visible(notice))
                {
                    builder.SetRegion(prefix + "/info/notice");
                    SettingRows.AddReadout(builder, notice, prefix + "/info/notice");
                }
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the left-hand column threw: " + e);
            }
            finally
            {
                if (labelled)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>
        /// What the design costs to build and to keep, and what the empire has in the bank - the two
        /// halves of the second box, each a region of the info stop under the caption the game drew over
        /// it.
        ///
        /// One cost per row, rather than the columns the box lays them out in: a strategic-resource cost
        /// is a number beside a symbol and the row it landed in is the width of the box, not a fact about
        /// the cost (ui-navigation's roster-grid rule). The caption of each half is that half's region
        /// name and no longer a row, so the box is walked by what it says rather than by its own
        /// headings.
        ///
        /// Split by the game's own bands rather than by rect: the empire's stock is the group holding
        /// <c>EmpireMoneyLabel</c>, everything else the box draws (construction, the retrofit cost the
        /// game shows while upgrading, upkeep) is the cost half, and whichever band holds the caption is
        /// left out of both.
        /// </summary>
        private static void BuildCosts(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            AgeTransform costs = panel.CostsTable;
            AgeTransform caption = FirstLabel(costs);
            AgeTransform captioned = BandOf(costs, caption);
            AgeTransform stock = Group(panel.EmpireMoneyLabel);
            AgeTransform stocked = BandOf(costs, stock) ?? stock;

            builder.SetRegion(prefix + "/info/costs");
            bool labelled = Caption(builder, caption);
            try
            {
                cells.Clear();
                IList<AgeTransform> children = costs.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (
                        child == null
                        || ReferenceEquals(child, captioned)
                        || ReferenceEquals(child, stocked)
                    )
                    {
                        continue;
                    }

                    SidePanels.Content(cells, child, prefix + "/cost/", Resources, null);
                }

                EmitLinear(builder, cells);
            }
            finally
            {
                if (labelled)
                {
                    builder.PopContext();
                }
            }

            if (stocked == null || !AgeWidgets.Visible(stocked))
            {
                return;
            }

            builder.SetRegion(prefix + "/info/stock");
            // The heading of this half stays a ROW as well as the region's name: the game hung the
            // explanation of the whole reminder on it ("A reminder of your current strategic resources
            // stock..."), a region name is a spoken phrase with no buffer behind it, and there is nowhere
            // else that sentence could be read from - the same rule the side panels follow
            // (<see cref="SidePanels.Readouts"/>). The announcer drops the duplicate on arrival.
            bool named = Caption(builder, FirstLabel(stocked));
            try
            {
                cells.Clear();
                SidePanels.Content(cells, stocked, prefix + "/cost/", Resources, null);
                EmitLinear(builder, cells);
            }
            finally
            {
                if (named)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>
        /// The name, hull, size, role and hull bonuses, in the order the panel stacks them - the whole of
        /// what the box says once its caption has become the stop's own name, which is why the first row
        /// here is the one Tab lands on.
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
        /// The shape walk would leave it saying just "50": the resource's own name is on
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
        ///
        /// Two regions: the switches that decide what is drawn, and what is drawn. The game draws them
        /// as one strip across the top and a wrapping list under it. The switches stay ONE row - the
        /// same reading the star system's constructible filters get (owner ruling): they are a
        /// select-one group the panel re-derives from the filter in force, and the row they are drawn in
        /// is the row the player walks. The modules under them are one per row, because a wrapping grid
        /// of tiles wraps where the table ran out of width. The band's own "Modules" caption is the
        /// stop's name.
        ///
        /// The game captions neither half, so each carries a word of the mod's own as its LEVEL -
        /// "Filters" over the switches, "Available" over the list. Without them the two halves are told
        /// apart only by what happens to be under the cursor, and a jump between them lands on a row
        /// with nothing saying which half it is in.
        /// </summary>
        private static void BuildModules(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            bool labelled = false;
            try
            {
                if (panel.ModulesGroup == null || !AgeWidgets.Visible(panel.ModulesGroup))
                {
                    return;
                }

                builder.BeginStop(ModulesStop(prefix));
                // The panel names the label it writes "Modules" into after the statistics box it was
                // copied from; what it DRAWS is the caption over this band.
                labelled = Caption(builder, FirstLabel(panel.ModulesGroup));

                builder.SetRegion(prefix + "/modules/filters");
                builder.PushContext(ModStrings.Get(ModStrings.ShipDesignFilters));
                try
                {
                    cells.Clear();
                    AddCategories(cells, panel, prefix);
                    AddObsolete(cells, panel, prefix);
                    Cells.Emit(builder, cells);
                }
                finally
                {
                    builder.PopContext();
                }

                builder.SetRegion(prefix + "/modules/list");
                builder.PushContext(ModStrings.Get(ModStrings.ShipDesignAvailable));
                try
                {
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
                finally
                {
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the module list threw: " + e);
            }
            finally
            {
                if (labelled)
                {
                    builder.PopContext();
                }
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
                OnDoubleClick = () => AutoEquip(it, enabled),
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
        ///
        /// The shape alone is not enough to NAME them, though: the game draws a caption beside some of
        /// these numbers (the range accuracies, the two military powers' icons, the mining rates) and
        /// none at all beside the rest, so every figure whose caption exists only in the game's string
        /// table is declared by name FIRST and its group is then skipped by the walk.
        ///
        /// FOUR REGIONS, and the shape walk's own cells are split between two of them by the band the
        /// game drew them in - anything under <c>RangeEfficienciesTable</c> is a per-range figure, the
        /// rest is the combat block. In order: the switch on its own (a region of one, so Alt+Up/Down
        /// reaches it and leaves it rather than dead-ending there); the four RUNNING TOTALS, which are
        /// what a design is judged by and are emitted in the owner's order rather than the panel's -
        /// health, manpower, movement, command points, where the panel draws offence and defence in
        /// between; the whole combat block in drawn order; and the per-range figures under the game's own
        /// word for them, "Accuracy" (<c>%AccuracyTitle</c>) - the accuracies always, and the damage
        /// figures beside them once the switch is on.
        ///
        /// The band's "Ship Statistics" caption is the stop's name and no longer a row, and the walk is
        /// told to leave it (and the switch, declared here) alone through the same skip list the named
        /// figures use.
        /// </summary>
        private static void BuildStats(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            bool labelled = false;
            try
            {
                AgeTransform table = panel.StatisticsTable;
                if (table == null || !AgeWidgets.Visible(table))
                {
                    return;
                }

                string keys = prefix + "/stat/";
                AgeTransform caption = FirstLabel(table);
                AgeTransform ranges = panel.RangeEfficienciesTable;

                _namedStats.Clear();
                if (caption != null)
                {
                    _namedStats.Add(caption);
                }

                AgeTransform switched = AgeWidgets.Transform(panel.ShowDetailedStatsToggle);
                if (switched != null)
                {
                    _namedStats.Add(switched);
                }

                _switchCells.Clear();
                _runningCells.Clear();
                _combatCells.Clear();
                _rangeCells.Clear();
                AddDetailedSwitch(_switchCells, panel, keys);
                AddRunningStats(_runningCells, panel, keys, _namedStats);
                AddCombatStats(_combatCells, panel, keys, _namedStats);
                AddRangeStats(_rangeCells, panel, keys, _namedStats);

                cells.Clear();
                SidePanels.Content(cells, table, keys, Stats, null);
                for (int i = 0; i < cells.Count; i++)
                {
                    (Under(cells[i].Widget, ranges) ? _rangeCells : _combatCells).Add(cells[i]);
                }

                _namedStats.Clear();
                if (
                    _switchCells.Count == 0
                    && _runningCells.Count == 0
                    && _combatCells.Count == 0
                    && _rangeCells.Count == 0
                )
                {
                    return;
                }

                builder.BeginStop(StatsStop(prefix));
                labelled = Caption(builder, caption);

                builder.SetRegion(keys + "switch");
                EmitOrdered(builder, _switchCells);
                builder.SetRegion(keys + "running");
                EmitOrdered(builder, _runningCells);
                builder.SetRegion(keys + "combat");
                EmitLinear(builder, _combatCells);
                builder.SetRegion(keys + "range");
                EmitRanges(builder, _rangeCells);
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the statistics threw: " + e);
            }
            finally
            {
                _namedStats.Clear();
                _switchCells.Clear();
                _runningCells.Clear();
                _combatCells.Clear();
                _rangeCells.Clear();
                if (labelled)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>The per-range figures under the game's own word for what they are. Not a caption the
        /// panel draws - it draws none over this band at all - but the title the game keeps for the
        /// statistic itself, which is the same string the hidden accuracy figure is named from.</summary>
        private static void EmitRanges(GraphBuilder builder, List<Cell> cells)
        {
            string title = AgeText.Clean("%AccuracyTitle");
            bool named =
                !string.IsNullOrEmpty(title) && title[0] != '%' && cells.Count > 0;
            if (named)
            {
                builder.PushContext(title);
            }

            try
            {
                EmitLinear(builder, cells);
            }
            finally
            {
                if (named)
                {
                    builder.PopContext();
                }
            }
        }

        // The statistics band's four regions, gathered before anything is declared because two of them
        // are filled by the shape walk and have to be split by the band each cell was drawn in. Reused
        // rather than allocated per frame, main-thread only, like the skip list below.
        private static readonly List<Cell> _switchCells = new List<Cell>(1);
        private static readonly List<Cell> _runningCells = new List<Cell>(4);
        private static readonly List<Cell> _combatCells = new List<Cell>(24);
        private static readonly List<Cell> _rangeCells = new List<Cell>(8);

        /// <summary>What the shape walk is to leave alone: the groups whose figures were already declared
        /// with the caption the game keeps in its string table (so the walk does not read the number on
        /// its own), plus the two widgets this band declares itself - the caption, which is the stop's
        /// name, and the detailed-stats switch, which is a region of one. Refilled per band, main-thread
        /// only.</summary>
        private static readonly List<AgeTransform> _namedStats = new List<AgeTransform>();

        /// <summary>The four running totals a design is judged by, in the order the owner asked to hear
        /// them rather than the order the panel draws them in: the panel puts the two military powers
        /// between manpower and movement, and those belong with the combat block below. Named from the
        /// game's own titles, the same six <see cref="AddSimpleStats"/> gives every other host of the
        /// prefab - two of which are declared here instead, in <see cref="AddCombatStats"/>.</summary>
        private static void AddRunningStats(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string keyPrefix,
            List<AgeTransform> declared
        )
        {
            AddStat(cells, panel.HealthLabel, "%ShipStatHealthTitle", keyPrefix + "health", declared);
            AddStat(
                cells,
                panel.ManPowerLabel,
                "%ShipStatManpowerTitle",
                keyPrefix + "manpower",
                declared
            );
            AddStat(
                cells,
                panel.MovementPointsLabel,
                "%ShipStatMovementTitle",
                keyPrefix + "movement",
                declared
            );
            AddStat(
                cells,
                panel.CommandPointsLabel,
                "%ShipStatCommandPointsTitle",
                keyPrefix + "command-points",
                declared
            );
        }

        /// <summary>
        /// The two military powers the base panel draws, plus the figures this panel adds to them, each
        /// named from the game's own title for it - the whole combat block, in the order the panel lays
        /// it out.
        ///
        /// Measured on the live prefab, with the hidden rows included: NOT ONE of these groups holds a
        /// caption label (<c>DetailedOffensiveStatsPanel</c>, <c>DetailedDefensiveStatsPanel</c> and
        /// <c>DetailedRangeDPSGroup</c> contain a single value label each and nothing else), while the
        /// range ACCURACIES above them do draw their own <c>…RangeTitle</c> and are left to the shape
        /// walk. So every row here is a bare number today - the panel writes them all with
        /// <c>FloatExtensions.ToString</c> (<c>ShipDesignEditionPanel.RefreshOffensiveAndDefensiveStats</c>
        /// :1146-1188, <c>RefreshRangeEfficiency</c> :1082-1087) and explains each with a
        /// <c>%…Description</c> SENTENCE, which is a gloss and not a name.
        ///
        /// Six of the titles are not <c>%ShipStat…</c> keys because the game does not keep one under
        /// that name: the four weapon powers are titled as module CATEGORIES
        /// (<c>%CategoryWeaponKineticTitle</c> = "Kinetic", the same registry entry the module strip's
        /// toggles are named from) and the two squadron counts as module SUB-categories
        /// (<c>%SubCategoryModuleSquadronFighterTitle</c> = "Fighter", on the GuiElements that also
        /// carry the <c>[fighter]</c>/<c>[bomber]</c> symbols -
        /// <c>Public\Gui\GuiElements[SubCategories].xml:425-441</c>); the two absorptions have a
        /// percentage-specific title (<c>%…AbsorptionPercentTitle</c>, "Hull Plating absorption",
        /// rather than the sentence-length <c>%…AbsorptionTitle</c>), and the shield capacity is
        /// titled after the property it reads (<c>%ShieldTitle</c> = "Shield power", from
        /// <c>SimulationProperties.Ship.Shield</c>).
        ///
        /// One row has NO title anywhere in the game's strings - the plating health bonus - so it
        /// passes a null key and is named by the first line of its own description, which is then not
        /// announced twice. A mod paraphrase is not an option, and the title of the property it
        /// actually reads (<c>SimulationProperties.Ship.ShipHealth</c> -> <c>%ShipHealthTitle</c>) is
        /// the word "Health", which is already the caption of the ship's own health two rows up.
        /// </summary>
        private static void AddCombatStats(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string keyPrefix,
            List<AgeTransform> declared
        )
        {
            AddStat(
                cells,
                panel.OffensivePowerLabel,
                "%ShipStatOffensiveMilitaryPowerTitle",
                keyPrefix + "offence",
                declared
            );
            AddStat(
                cells,
                panel.DefensivePowerLabel,
                "%ShipStatDefensiveMilitaryPowerTitle",
                keyPrefix + "defence",
                declared
            );
            AddStat(
                cells,
                panel.KineticPowerLabel,
                "%CategoryWeaponKineticTitle",
                keyPrefix + "kinetic",
                declared
            );
            AddStat(
                cells,
                panel.MissilePowerLabel,
                "%CategoryWeaponMissileTitle",
                keyPrefix + "missile",
                declared
            );
            AddStat(
                cells,
                panel.LaserPowerLabel,
                "%CategoryWeaponLaserTitle",
                keyPrefix + "laser",
                declared
            );
            AddStat(
                cells,
                panel.BeamPowerLabel,
                "%CategoryWeaponBeamTitle",
                keyPrefix + "beam",
                declared
            );
            AddStat(
                cells,
                panel.FighterCountLabel,
                "%SubCategoryModuleSquadronFighterTitle",
                keyPrefix + "fighters",
                declared
            );
            AddStat(
                cells,
                panel.BomberCountLabel,
                "%SubCategoryModuleSquadronBomberTitle",
                keyPrefix + "bombers",
                declared
            );
            AddStat(
                cells,
                panel.AccuracyLevelLabel,
                "%AccuracyTitle",
                keyPrefix + "accuracy",
                declared
            );
            AddStat(cells, panel.EvasionLevelLabel, "%EvasionTitle", keyPrefix + "evasion", declared);
            AddStat(cells, panel.PlatingHealthBonusLabel, null, keyPrefix + "plating-health", declared);
            AddStat(
                cells,
                panel.PlatingAbsorptionLabel,
                "%HullPlatingAbsorptionPercentTitle",
                keyPrefix + "plating-absorption",
                declared
            );
            AddStat(
                cells,
                panel.ShieldCapacityLabel,
                "%ShieldTitle",
                keyPrefix + "shield-capacity",
                declared
            );
            AddStat(
                cells,
                panel.ShieldAbsorptionLabel,
                "%ShieldAbsorptionPercentTitle",
                keyPrefix + "shield-absorption",
                declared
            );
        }

        /// <summary>
        /// The damage the design does at each range, which the game draws under the accuracies of the
        /// same three ranges and only while its detailed switch is on.
        ///
        /// The three figures are three labels of ONE group, so the Nth of them is paired with the Nth
        /// range title - the same title the accuracy column above it draws for itself. Reading the group
        /// instead is what produced three identical "(0)"s with nothing saying which range each belonged
        /// to; passing each label as its own row is what keeps the three off one id and one rect.
        /// </summary>
        private static void AddRangeStats(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string keyPrefix,
            List<AgeTransform> declared
        )
        {
            AddStat(
                cells,
                panel.LongRangeDPSLabel,
                "%ShipStatRangeLongTitle",
                keyPrefix + "dps-long",
                declared,
                true
            );
            AddStat(
                cells,
                panel.MediumRangeDPSLabel,
                "%ShipStatRangeMediumTitle",
                keyPrefix + "dps-medium",
                declared,
                true
            );
            AddStat(
                cells,
                panel.ShortRangeDPSLabel,
                "%ShipStatRangeShortTitle",
                keyPrefix + "dps-short",
                declared,
                true
            );
        }

        private static bool Stats(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            // The skip list covers both what was declared by name and what the band declares itself -
            // its caption and its switch - so a widget on it is simply not walked into.
            if (_namedStats.Contains(widget))
            {
                return true;
            }

            RepartitionHorizontalGauge gauge = widget.GetComponent<RepartitionHorizontalGauge>();
            if (gauge != null)
            {
                AddGauge(cells, gauge, keyPrefix + widget.name);
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
        ///
        /// Which half is which IS drawn, though, once: the caption row between the two gauges
        /// (<c>ProjectileTitle</c> / <c>EnergyTitle</c>, measured as siblings of both gauges inside
        /// <c>BalanceGauges</c>) heads the left and right columns of BOTH bars - the offensive bar's
        /// halves are kinetic+missile against laser+beam, and the defensive bar's are the hull plating
        /// and shield absorptions those two weapon families are stopped by, which is what those
        /// captions' own tooltips say. Without them the bar read out a naked "100%".
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
                AgeTransform band = gauge.AgeTransform == null ? null : gauge.AgeTransform.Parent;
                MessageBuilder message = new MessageBuilder();
                Half(
                    message,
                    band,
                    "ProjectileTitle",
                    gauge.LeftGauge,
                    50f - gauge.LeftGauge.PercentLeft
                );
                Half(
                    message,
                    band,
                    "EnergyTitle",
                    gauge.RightGauge,
                    gauge.RightGauge.PercentRight - 50f
                );
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One half of a balance bar under the caption the game drew over its column, so the
        /// share is not read out on its own. A half the game left at nothing is drawn at zero width and
        /// hidden, and is skipped here for the same reason.</summary>
        private static void Half(
            MessageBuilder message,
            AgeTransform band,
            string captionName,
            AgeTransform half,
            float reach
        )
        {
            string share = Share(half, reach);
            if (share == null)
            {
                return;
            }

            message.ListItem(AgeWidgets.TextOf(AgeWidgets.ChildNamed(band, captionName, 0)));
            message.Fragment(share);
        }

        /// <summary>One half of a balance bar as the share it was drawn at. Each half is anchored at
        /// the middle and stretched out to its own side by half of its share
        /// (<c>RepartitionHorizontalGauge.Refresh</c>), so <paramref name="reach"/> - how far its outer
        /// edge got from the middle - is half the proportion. The two halves reach in OPPOSITE
        /// directions and each has to be measured from the middle its own way, which is what an earlier
        /// reading of the right half against the bar's far END got wrong: with the right half at 37% it
        /// said 163%, and the fixture hid it because a half the game gives nothing to is drawn at zero
        /// width and hidden.</summary>
        private static string Share(AgeTransform half, float reach)
        {
            if (half == null || !half.Visible)
            {
                return null;
            }

            int percent = (int)Math.Abs(Math.Round(reach * 2f));
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
                AddRemoveTarget(builder, panel, prefix);
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
        /// Filled, it is the module's name and nothing else, and a button that takes the module out;
        /// empty, it is the word "empty" and the markers, and no action, because that is what a click on
        /// it does. Either way it is where a carried module is put down, and it says so while one is
        /// held. The markers are the SLOT's own facts, so on a filled slot they move out of the readout
        /// and into the review buffer, where they cannot be mistaken for the module's.
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
                    // What the slot ITSELF is - the categories it takes, its multiplier, what it costs -
                    // and only while it is empty. Full, those words are a statement about the SLOT read
                    // straight after the name of the MODULE in it, and they read as the module's own
                    // roles ("Titanium Slug, defence module, support module"); the module's name is the
                    // whole of what a filled slot is called, and the slot's own facts move to the review
                    // buffer below. Kept as a PART that answers null rather than a part that goes away:
                    // the live watch re-baselines when the part list changes shape, which is exactly the
                    // fill/unfill transition the watch above exists to announce.
                    GraphNodes.ValuePart(() => Filled(it) ? null : SlotMarkers(it), false),
                },
                Sections = GraphNodes.Sections(() => SlotDetails(it), tooltip),
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
                // The same test the drop makes, so the word and the outcome cannot disagree: a slot the
                // carried module does not fit is not somewhere it can go.
                DropAccepts = held => Takes(owner, it, held),
            };

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

        /// <summary>What the slot is called: the module in it, or "empty".
        ///
        /// <c>GuiSlot.Title</c> answers both, but its answer for an empty slot is the game's generic
        /// title for the CategorySlot element - a phrase about slots in general, in front of the marker
        /// list that says what THIS slot takes. The state is the thing the player is walking the ship
        /// for, so the state is what the name says.</summary>
        private static string SlotName(ShipDesignEditionSlotItem slot)
        {
            try
            {
                if (slot.GuiSlot == null)
                {
                    return null;
                }

                return Filled(slot)
                    ? AgeText.Clean(slot.GuiSlot.Title)
                    : ModStrings.Get(ModStrings.ShipDesignSlotEmpty);
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
                Add(message, SlotCategories(slot));
                Add(message, SlotCosts(slot));
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The module categories the slot is restricted to, as the game's own titles for them,
        /// and only while the game is drawing their icons.</summary>
        private static string SlotCategories(ShipDesignEditionSlotItem slot)
        {
            if (slot.SlotCategoriesTable == null || !slot.SlotCategoriesTable.Visible)
            {
                return null;
            }

            MessageBuilder message = new MessageBuilder();
            string[] categories = slot.GuiSlot.ModuleTypeRestrictions;
            for (int i = 0; categories != null && i < categories.Length; i++)
            {
                string title = AgeText.Clean(Gui.GetTitle(categories[i]));
                if (!string.IsNullOrEmpty(title) && title[0] != '%')
                {
                    Add(message, title);
                }
            }

            return message.Build();
        }

        /// <summary>The rest of what the game draws round the slot's edge: the multiplier it applies to
        /// whatever is fitted, and the resource the slot itself costs on top of the module.</summary>
        private static string SlotCosts(ShipDesignEditionSlotItem slot)
        {
            MessageBuilder message = new MessageBuilder();
            Add(message, Marker(slot.SlotMultiplier2));
            Add(message, Marker(slot.SlotMultiplier3));
            Add(message, Marker(slot.SlotMultiplier4));
            if (slot.SpecialCostMarker != null && slot.SpecialCostMarker.Visible)
            {
                Add(message, AgeText.Clean(slot.GuiSlot.SpecialCost));
            }

            return message.Build();
        }

        /// <summary>
        /// What a FILLED slot has to say about itself that its name no longer does: the markers the game
        /// draws round its edge - the multiplier it applies and the resource it costs on top of the
        /// module.
        ///
        /// NOT what it accepts, even though that is the other half of what the markers say: the module
        /// in it carries the game's own tooltip, and that tooltip already ends with a "Slot Information /
        /// Module Type Restriction" section listing exactly those categories (measured on the live
        /// panel). The tooltip is in this node's buffer too, so a line of the mod's would be the same
        /// sentence twice, one of them a paraphrase.
        ///
        /// Empty, the markers are in the readout already (<see cref="SlotMarkers"/>) and repeating them
        /// here would be the same words twice on one control.
        /// </summary>
        private static IList<string> SlotDetails(ShipDesignEditionSlotItem slot)
        {
            try
            {
                if (!Filled(slot))
                {
                    return null;
                }

                string costs = SlotCosts(slot);
                return string.IsNullOrEmpty(costs) ? null : new string[] { costs };
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

        /// <summary>
        /// Where a module is dropped to take it off the ship - the mod's own node, at the end of the
        /// slots, and the one gesture on this panel with no widget of its own behind it.
        ///
        /// The mouse's way of removing a module is to drag it off the ship and let go over nothing
        /// (<c>ApplyDrop(null)</c> -> <c>RemoveModuleFromSlot</c>), and "nothing" is not somewhere a
        /// keyboard can aim. Declared even while nothing is being carried, because a place the player has
        /// to already know about is a place they will never find: walking to the end of the slots is how
        /// the removal announces that it exists. It says "drop target" only while a module that could
        /// actually come off is held, which is the standard indication and needs no words of its own.
        ///
        /// Not declared at all while the design is read-only: there is no gesture to explain on a page
        /// nothing can be taken off, and an inert instruction is worse than none.
        ///
        /// Keyed on the container, which the panel keeps for as long as the page is up - the slot items
        /// under it are pooled and rebound on every refresh.
        /// </summary>
        private static void AddRemoveTarget(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix
        )
        {
            if (!Editable(panel))
            {
                return;
            }

            ShipDesignEditionPanel owner = panel;
            NodeVtable vtable = new NodeVtable
            {
                // No control type, like every other line this mod draws itself: it is an instruction the
                // player reads, and a role word on it would name a widget the game never drew.
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.ShipDesignRemoveTarget)),
                },
                DropKind = ModuleKind,
                OnDrop = held => Remove(owner, held),
                // The same test the drop makes, so the word and the outcome cannot disagree - the
                // <see cref="Takes"/> rule, on the one target that is not a slot.
                DropAccepts = held => Removable(owner, held),
            };

            builder.AddItem(
                ControlId.Referenced(
                    panel.ShipDesignSlotItemsContainer,
                    prefix + "/slot/remove-target"
                ),
                vtable
            );
        }

        private static bool Editable(ShipDesignEditionPanel panel)
        {
            try
            {
                return panel.GuiShipDesign != null
                    && panel.CurrentMode != ShipDesignEditionPanel.Mode.ReadOnly;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether taking the carried module off the ship is a thing that could happen here, which is
        /// what both the "drop target" word and the drop itself ask.
        ///
        /// A module carried off the LIST is the case this rules out: it was never fitted, so there is
        /// nothing to take off, and the game's own <c>ApplyDrop</c> (:345-351, which acts only with a
        /// slot on one end or the other) does nothing with it either. It is not somewhere that module
        /// can go, and it says so both ways - no drop word while it is held, and the ordinary refusal
        /// on a press.
        /// </summary>
        private static bool Removable(ShipDesignEditionPanel panel, CarryItem held)
        {
            return held != null
                && held.Cargo is ShipDesignEditionSlotItem
                && Editable(panel)
                && Carried(held) != null;
        }

        /// <summary>
        /// Take the carried module off the ship, the way letting go over nothing does it: the same
        /// dragged-item record the drag fills in, and then <c>ApplyDrop</c> with no target at all, which
        /// is what the game's own drag hands it when the pointer is over nothing it recognises.
        /// </summary>
        private static DropResult Remove(ShipDesignEditionPanel panel, CarryItem held)
        {
            try
            {
                if (!Removable(panel, held))
                {
                    return DropResult.Refused(null);
                }

                GuiModule module = Carried(held);
                DragDropWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<DragDropWindow>(false)
                    : null;
                if (window == null || window.ShipDesignModuleDraggedItem == null)
                {
                    return DropResult.Refused(null);
                }

                window.ShipDesignModuleDraggedItem.SourceItem = held.Cargo as GuiBehaviour;
                window.ShipDesignModuleDraggedItem.GuiModule = module;
                ((IDragDropClient)panel).ApplyDrop(null);
                return DropResult.Done(
                    ModStrings.Format(ModStrings.ShipDesignModuleRemoved, held.Name)
                );
            }
            catch (Exception e)
            {
                Log.Warn("ship design: removing a carried module threw: " + e);
                return DropResult.Refused(null);
            }
        }

        // ---- shared ----

        /// <summary>Emit one node per cell, in the order the game drew them - <see
        /// cref="Cells.EmitLinear"/>, which is where this loop lives now that every screen's bands read
        /// that way. Kept as the name this file's own emit calls have always used.</summary>
        public static void EmitLinear(GraphBuilder builder, List<Cell> cells)
        {
            Cells.EmitLinear(builder, cells);
        }

        /// <summary>Emit one node per cell in the order they were DECLARED, for a set whose reading order
        /// is a decision rather than a rect - the running totals, which the panel draws with the military
        /// powers interleaved.</summary>
        private static void EmitOrdered(GraphBuilder builder, List<Cell> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                builder.AddItem(cells[i].Id, cells[i].Vtable);
            }
        }

        /// <summary>Push a drawn caption as the LEVEL the rows under it sit in - a stop's name or a
        /// region's - and say whether it has to be popped. A band the game left uncaptioned gets no level
        /// rather than an empty one.</summary>
        private static bool Caption(GraphBuilder builder, AgeTransform caption)
        {
            string text = caption == null ? null : AgeWidgets.TextOf(caption);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            builder.PushContext(text);
            return true;
        }

        /// <summary>Whether a widget is drawn inside a band - which is how the shape walk's cells are
        /// split between the regions of the statistics box, whose bands are the game's own grouping and
        /// not something a rect could answer.</summary>
        private static bool Under(AgeTransform widget, AgeTransform band)
        {
            if (band == null)
            {
                return false;
            }

            AgeTransform at = widget;
            int guard = 0;
            while (at != null && guard++ < 12)
            {
                if (ReferenceEquals(at, band))
                {
                    return true;
                }

                at = at.Parent;
            }

            return false;
        }

        /// <summary>Which child of a band a widget sits in, for splitting a box by the groups the game
        /// laid it out in rather than by name.</summary>
        private static AgeTransform BandOf(AgeTransform band, AgeTransform inside)
        {
            AgeTransform at = inside;
            int guard = 0;
            while (at != null && guard++ < 12)
            {
                if (ReferenceEquals(at.Parent, band))
                {
                    return at;
                }

                at = at.Parent;
            }

            return null;
        }

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
