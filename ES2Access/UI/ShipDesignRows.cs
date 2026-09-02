using System;
using System.Collections.Generic;
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
    public static partial class ShipDesignRows
    {
        /// <summary>The three editor tables, each swept once per table per frame. All three are POOLED
        /// - the module list refills as the player filters it, the category strip is rebuilt per hull
        /// and the slot container per design - so the sweep is kept for the frame and no longer.
        /// </summary>
        private static readonly FrameSweep<ShipDesignModuleItem> Modules =
            new FrameSweep<ShipDesignModuleItem>("ship design");

        private static readonly FrameSweep<AgeControlToggle> Categories =
            new FrameSweep<AgeControlToggle>("ship design");

        private static readonly FrameSweep<ShipDesignEditionSlotItem> Slots =
            new FrameSweep<ShipDesignEditionSlotItem>("ship design");

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
                // Both bools decide whether a STOP is opened at all and which bands fill it - flow, not
                // one node's existence.
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
                if (notice != null)
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

            // Flow control: the side-panel reading below descends the whole panel.
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
                    : ControlId.For(panel.NameTextField, prefix + "/info/name"),
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
            // Flow control: which branch is built - the drop list is a wired prefab field, always
            // non-null; its VISIBILITY is what says the chooser is the drawn one. Without this the
            // hull NAME the panel draws once a hull is picked was never declared, and the combo it
            // stood in for was then dropped by the gate for not being drawn - so the line went silent
            // instead of reading the hull.
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
            // Flow control: the caption below is found by a walk and a readout added per bonus.
            if (panel.BonusesGroup == null || !AgeWidgets.Visible(panel.BonusesGroup))
            {
                return;
            }

            AgeTransform caption = FirstLabel(panel.BonusesGroup);
            if (caption != null)
            {
                AddCaption(
                    builder,
                    caption,
                    panel.BonusesGroup,
                    prefix + "/info/bonuses"
                );
            }

            AddBonus(builder, panel.Bonus1Label, prefix + "/info/bonus/1");
            AddBonus(builder, panel.Bonus2Label, prefix + "/info/bonus/2");
            AddBonus(builder, panel.Bonus3Label, prefix + "/info/bonus/3");
        }

        /// <summary>A caption the game draws over a block, where the sentence explaining the block is
        /// hung on the BLOCK and not on the label the word is written on - the shared rule
        /// (<see cref="Captions"/>), applied here where the caption is already a node of its own and
        /// no level is being pushed. Where the label carries its own explanation that one stands and
        /// the block's is left to whatever declares the block.</summary>
        private static void AddCaption(
            GraphBuilder builder,
            AgeTransform label,
            AgeTransform group,
            string key
        )
        {
            if (
                SettingRows.LastTooltip(label) == null
                && Captions.Row(builder, label, key, group)
            )
            {
                return;
            }

            SettingRows.AddReadout(builder, label, key);
        }

        private static void AddBonus(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget != null)
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
            // Kept: these cells are counted (with the three other stat lists) to decide whether the
            // statistics stop and its caption are opened at all, so a cell for a figure the panel is
            // not drawing would put a caption over nothing.
            if (widget == null || !AgeWidgets.Visible(widget))
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
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
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
                builder.AddItem(Nodes.Drawn(cells[i].Id, cells[i].Vtable, cells[i].Widget));
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
                    // Content: which child supplies a band's caption.
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
            GameHandlers.Method(
                typeof(ShipDesignEditionPanel),
                "OnNameTextFieldGainFocusCb"
            );
    }
}
