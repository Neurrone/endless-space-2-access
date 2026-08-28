using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>
    /// What the manpower box's Manage button opens: how the empire's ground troops are made up, and the
    /// upgrades it can buy for each kind.
    ///
    /// The window draws a THREE COLUMN grid - "Composition", "Type", "Evolution" - with one row per
    /// troop type (Infantry, Armor, Air). The widget tree is column-major (three parallel tables, one
    /// per column, each holding one child per troop type) and so is the drawing, each column framed
    /// under its own caption and the Evolution column inside a scroll view of its own; so the three
    /// columns are the Tab stops and the troop types are the rows inside them, with each row's context
    /// carrying the type's own name - which the game draws only in the middle column, once per visual
    /// row.
    ///
    /// Four things about this window are its own:
    ///
    /// - **Nothing here is committed until Confirm.** Every stepper press, lock and upgrade tick is
    ///   local to the window (<c>GroundTroopManagementModalWindow.OnValidateCb</c> :331-357 is the only
    ///   thing that posts <c>OrderManageManpower</c>, and Reset/Close both call <c>Reset()</c>), so the
    ///   controls need no confirmation of the mod's and Confirm carries the game's own refusals.
    /// - **The composition ring is a slider.** The game draws a gauge with a percentage in it and a
    ///   minus and a plus button under it; Left and Right ARE those two buttons' clicks
    ///   (<c>GroundTroopRepartiter.OnPlusButtonCb</c>/<c>OnMinusButtonCb</c>), so the two are not also
    ///   declared as nodes of their own - the same fold the marketplace's quantity stepper gets. A
    ///   press the game cannot balance is refused by its own machinery and the value simply repeats,
    ///   which is the boundary cue every slider in the mod has. The cost of that fold is that the ring
    ///   owns Left and Right, so this column is walked as a vertical LIST rather than in the bands the
    ///   game draws it in (<see cref="Column"/>) - otherwise the padlock drawn beside the ring would be
    ///   reachable by no key at all.
    /// - **A locked troop type's reason is on its name, not on a button.** The game draws TWO invisible
    ///   hint buttons over a locked row - one over the composition cell, one over the type cell - with
    ///   the same sentence on both, forced to <c>Enable = true</c> so that a Ctrl+click can jump to the
    ///   missing technology (<c>GroundTroopManagementLine.Reset</c> :53-63, <c>Gui.FormatButtonHint</c>).
    ///   <see cref="AgeWidgets.Offered"/> answers false for exactly that trick, so neither is a control
    ///   the mod can offer as a click: a plain click on one only closes the window
    ///   (<c>GroundTroopRepartiter.OnHintCb</c>). So the sentence is declared where the type is NAMED,
    ///   once per locked row, rather than as two identical dead ends - and that node carries the row's
    ///   Ctrl+Enter, which is the hint's own jump to the missing technology
    ///   (<see cref="AgeWidgets.Locate"/>). It is named here rather than left to the shared wiring in
    ///   <see cref="Cells.Add"/> because the hint hangs off a button drawn OVER the row instead of on
    ///   the label the node was declared from.
    /// - **A locked upgrade's reason is written by the mod.** <c>GroundTroopUpgrade.RefreshTooltip</c>
    ///   asks whether the ITEM's transform is enabled, while <c>RefreshState</c> only ever disables the
    ///   item's HEADER group - so the game composes the right sentence and then never stores it, and a
    ///   locked upgrade's tooltip is empty on screen. The two sentences it would have written
    ///   (<c>LockedTroopTypePrereq</c>, <c>MissingUpgradePrereq</c>) are reproduced here off the same
    ///   test, which is the treatment the hero skill tree's prerequisite refusals already have.
    ///
    /// The upgrade cards have no title anywhere - no GuiElement exists for a manpower upgrade name -
    /// so a card is named by the effects it draws, which is the only text on it, with its cost or its
    /// "Done" badge as the value beside it.
    ///
    /// Escape is the game's: <c>GuiModalWindow</c> closes on it, which is what Close does too.
    /// </summary>
    public sealed class TroopManagementScreen : Screen
    {
        private static readonly object HeadingStop = "troops:heading";
        private static readonly object CompositionStop = "troops:composition";
        private static readonly object TypeStop = "troops:type";
        private static readonly object EvolutionStop = "troops:evolution";
        private static readonly object CostStop = "troops:cost";
        private static readonly object ActionsStop = "troops:actions";

        // Reused across builds rather than allocated per frame: Build runs every tick. The second list
        // is the cost band's, whose two groups are read before either is declared so that a stop is
        // opened only when one of them has something in it.
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<Cell> _stocks = new List<Cell>();

        public override string Key
        {
            get { return "screen.troop-management"; }
        }

        /// <summary>
        /// Above the military screen that opens it, below the tutorial popup.
        ///
        /// Measured: this window is drawn in <c>ModalRenderer</c> (<c>AgeScreen.SortingOrder</c> 5) and
        /// the tutorial popup in <c>OverlayRenderer</c> (6), so a tutorial page really does draw over
        /// this window and has to be able to take the keyboard from it - which puts this under the
        /// tutorial's 98. Nothing this window's own controls can raise sits lower: Confirm posts an
        /// order without a confirmation box, and there are no drop lists.
        /// </summary>
        public override int Layer
        {
            get { return 21; }
        }

        /// <summary>The heading, because it is drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadingStop; }
        }

        public override bool IsActive()
        {
            try
            {
                GroundTroopManagementModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the game's: the window closes itself, which is what Close does.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            GroundTroopManagementModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                BuildComposition(builder, window);
                BuildTypes(builder, window);
                BuildEvolution(builder, window);
                BuildCost(builder, window);
                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("troops: reading the window threw: " + e);
            }
        }

        private void BuildHeading(GraphBuilder builder, GroundTroopManagementModalWindow window)
        {
            builder.BeginStop(HeadingStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "TitleLabel", 3),
                "troops:title"
            );
            Cells.EmitLinear(builder, _cells);
        }

        // ---- Composition ----

        /// <summary>The left column: the caption the game draws over it, then one row per troop type -
        /// its share as a slider, the lock that pins it, and what a step of it costs.</summary>
        private void BuildComposition(GraphBuilder builder, GroundTroopManagementModalWindow window)
        {
            AgeTransform table = window.RepartitersTable;
            // Flow control: every row under the table is read before anything is declared.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            builder.BeginStop(CompositionStop);
            Caption(builder, table, "CompositionLabel", "troops:composition-caption");

            IList<AgeTransform> rows = table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                GroundTroopRepartiter row =
                    rows[i] == null ? null : rows[i].GetComponent<GroundTroopRepartiter>();
                // Different widget: the nodes under it stand on the pieces INSIDE the row, and this asks
                // about the row - which the gate's walk up the ancestry does reach, but reading a
                // retired row's pieces first costs a text walk apiece.
                if (row == null || !AgeWidgets.Visible(row.AgeTransform))
                {
                    continue;
                }

                string name = TroopName(window, row.TroopType);
                builder.PushContext(name);
                _cells.Clear();
                AddRatio(row, name, i);
                AddLock(row, i);
                Cells.AddReadout(
                    _cells,
                    AgeWidgets.ChildNamed(row.AgeTransform, "UnitCostGroup", 4),
                    "troops:step-cost/" + i
                );
                Column(builder, _cells);
                builder.PopContext();
            }
        }

        /// <summary>The share of the empire's manpower this troop type takes, and the two buttons that
        /// move it. Named after the type rather than the column, so the row still says what it is when
        /// the player steps back onto it inside the group.</summary>
        private void AddRatio(GroundTroopRepartiter row, string name, int index)
        {
            AgeTransform widget = row.RatioLabel == null ? null : row.RatioLabel.AgeTransform;
            if (widget == null)
            {
                return;
            }

            GroundTroopRepartiter it = row;
            string label = name;
            Func<bool> enabled = () => AgeWidgets.Enabled(it.FilterGroup) && !it.IsLocked;
            NodeVtable vtable = GraphNodes.Slider(
                () => label,
                () => AgeText.Label(it.RatioLabel),
                (sign, large) => Step(it, sign),
                enabled
            );
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(
                _cells,
                widget,
                ControlId.For(widget, "troops:ratio/" + index),
                vtable
            );
        }

        /// <summary>One press of the ring's own stepper. The coarse step is not a second thing the game
        /// offers - one click is one step of the game's own size, and the manager recomputes every other
        /// row's share to pay for it (<c>GroundTroopRepartiterManager.ApplyWeightChange</c>).</summary>
        private static void Step(GroundTroopRepartiter row, int sign)
        {
            try
            {
                AgeControlButton button = sign < 0 ? row.MinusButton : row.PlusButton;
                if (button != null && AgeWidgets.Operable(button.AgeTransform))
                {
                    AgeWidgets.Press(button);
                }
            }
            catch (Exception e)
            {
                Log.Warn("troops: stepping a troop share threw: " + e);
            }
        }

        /// <summary>The tick that stops this type's share from being moved to pay for another's. Drawn
        /// as a bare padlock, so the sentence the game explains it with is its name.</summary>
        private void AddLock(GroundTroopRepartiter row, int index)
        {
            AgeTransform widget = AgeWidgets.Transform(row.LockToggle);
            if (widget == null)
            {
                return;
            }

            GroundTroopRepartiter it = row;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            string label = CardActions.FirstLine(tooltip);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => label,
                () => it.LockToggle != null && it.LockToggle.State,
                () => AgeWidgets.Toggle(it.LockToggle),
                () => AgeWidgets.Offered(AgeWidgets.Transform(it.LockToggle)),
                tooltip
            );
            AgeWidgets.Point(vtable, it.LockToggle, tooltip, widget);
            Cells.Add(
                _cells,
                widget,
                ControlId.For(widget, "troops:lock/" + index),
                vtable
            );
        }

        // ---- Type ----

        /// <summary>The middle column: the caption, then one row per troop type - its name, carrying the
        /// game's own sentence for a type that is still locked, and the three figures the game writes
        /// under it.</summary>
        private void BuildTypes(GraphBuilder builder, GroundTroopManagementModalWindow window)
        {
            AgeTransform table = window.DescriptionsTable;
            // Flow control: every row under the table is read before anything is declared.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            builder.BeginStop(TypeStop);
            Caption(builder, table, "DescriptionLabel", "troops:type-caption");

            IList<AgeTransform> rows = table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                GroundTroopDescription row =
                    rows[i] == null ? null : rows[i].GetComponent<GroundTroopDescription>();
                // Different widget: the nodes under it stand on the pieces INSIDE the row, and this asks
                // about the row - which the gate's walk up the ancestry does reach, but reading a
                // retired row's pieces first costs a text walk apiece.
                if (row == null || !AgeWidgets.Visible(row.AgeTransform))
                {
                    continue;
                }

                builder.PushContext(AgeText.Label(row.NameLabel));
                _cells.Clear();
                AddTypeName(row, i);
                AddStats(row, i);
                Cells.EmitLinear(builder, _cells);
                builder.PopContext();
            }
        }

        /// <summary>
        /// What the type is called, and - while the game is drawing its hint over the row - why it
        /// cannot be used.
        ///
        /// The hint button itself is not a control here (see the class comment): it is switched on only
        /// so a Ctrl+click can reach the missing technology, and its plain click closes the window. So
        /// its sentence is spoken on the name, and the mouse-only instruction the hint appends stays in
        /// the review buffer, which every control's sections do for it
        /// (<see cref="GraphNodes.HintSections"/>).
        /// </summary>
        private void AddTypeName(GroundTroopDescription row, int index)
        {
            AgeTransform widget = row.NameLabel == null ? null : row.NameLabel.AgeTransform;
            if (widget == null)
            {
                return;
            }

            GroundTroopDescription it = row;
            AgeTransform hint = AgeWidgets.Transform(row.HintButton);
            AgeTooltip tooltip = hint == null ? null : AgeWidgets.Raw(hint);
            // Availability wording, not existence: whether the row says it is refusing.
            Func<bool> unlocked = () => hint == null || !AgeWidgets.Visible(hint);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.NameLabel)),
                    GraphNodes.DisabledPart(unlocked),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            GraphNodes.AddRefusal(vtable, tooltip, unlocked);
            if (hint != null)
            {
                // The row's own Ctrl+click, named here rather than left to the shared wiring in
                // <see cref="Cells.Add"/>: the hint hangs off a button of its own drawn OVER the row, so
                // the declared widget is not the one carrying it.
                AgeTransform locate = hint;
                vtable.OnSelectToggle = () => AgeWidgets.Locate(locate);
                NodeHints.Add(
                    vtable,
                    ModStrings.HintMissingTechnology,
                    UiActions.SelectToggle,
                    0,
                    // Availability wording again: whether the hint sentence applies right now.
                () => AgeWidgets.Visible(locate)
                );
            }

            AgeWidgets.PointAt(vtable, hint ?? widget);
            Cells.Add(
                _cells,
                widget,
                ControlId.For(widget, "troops:type-name/" + index),
                vtable
            );
        }

        /// <summary>Health, damage and cost: three captioned figures the game draws as rows of two
        /// labels, so each row is read whole rather than split into a caption and a number.</summary>
        private void AddStats(GroundTroopDescription row, int index)
        {
            try
            {
                AgeTransform stats = row.TroopStats == null ? null : row.TroopStats.AgeTransform;
                IList<AgeTransform> lines = stats == null ? null : stats.Children;
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    Cells.AddReadout(_cells, lines[i], "troops:stat/" + index + "/" + i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("troops: reading a troop's figures threw: " + e);
            }
        }

        // ---- Evolution ----

        /// <summary>
        /// The right column: the caption, then one row per troop type holding the chain of upgrades that
        /// type can be given - one upgrade per row, because column 3 of Infantry and column 3 of Armor
        /// are different upgrades and not one attribute, so the grid is a layout rather than a table.
        ///
        /// Each type's chain is a region as well as a level, so the jump key steps between the chains
        /// instead of walking fifteen cards - and the caption over the column takes one too, because a
        /// stop only some of whose nodes are in a region has nodes the jump cannot leave from. Only
        /// while there is more than one of them: a lone region is a jump key that swallows silently.
        /// </summary>
        private void BuildEvolution(GraphBuilder builder, GroundTroopManagementModalWindow window)
        {
            AgeTransform table = window.UpgradeListsTable;
            // Flow control: every row under the table is read before anything is declared.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> rows = table.Children;
            bool regions = Lists(rows) > 1;

            builder.BeginStop(EvolutionStop);
            if (regions)
            {
                builder.SetRegion("troops:evolution/caption");
            }

            Caption(builder, table, "EvolutionLabel", "troops:evolution-caption");

            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                GroundTroopUpgradeList list =
                    rows[i] == null ? null : rows[i].GetComponent<GroundTroopUpgradeList>();
                // Flow control: each list is walked upgrade by upgrade.
                if (list == null || !AgeWidgets.Visible(list.AgeTransform))
                {
                    continue;
                }

                if (regions)
                {
                    builder.SetRegion("troops:evolution/type/" + i);
                }

                builder.PushContext(TroopName(window, list.TroopType));
                _cells.Clear();
                AddUpgrades(list, i);
                Cells.EmitLinear(builder, _cells);
                builder.PopContext();
            }
        }

        /// <summary>How many troop types the column is actually drawing a chain for.</summary>
        private static int Lists(IList<AgeTransform> rows)
        {
            int count = 0;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                GroundTroopUpgradeList list =
                    rows[i] == null ? null : rows[i].GetComponent<GroundTroopUpgradeList>();
                // A COUNT: how many lists are drawn decides whether the page opens one region or several.
                if (list != null && AgeWidgets.Visible(list.AgeTransform))
                {
                    count++;
                }
            }

            return count;
        }

        private void AddUpgrades(GroundTroopUpgradeList list, int row)
        {
            try
            {
                GroundTroopUpgrade[] upgrades =
                    list.AgeTransform.GetComponentsInChildren<GroundTroopUpgrade>(true);
                for (int i = 0; upgrades != null && i < upgrades.Length; i++)
                {
                    AddUpgrade(upgrades[i], "troops:upgrade/" + row + "/" + i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("troops: reading a troop's upgrades threw: " + e);
            }
        }

        /// <summary>
        /// One upgrade in a type's chain.
        ///
        /// It has no name of its own anywhere - there is no GuiElement for a manpower upgrade - so it is
        /// named by the effects it draws, which are the only words on the card, with what the game draws
        /// in its header beside them: the game's own formatting of the cost, the technology it is waiting
        /// for, or its "Done" badge.
        ///
        /// A card the empire may still buy carries the game's own tick, which is a PREVIEW: it changes
        /// nothing until Confirm, and the window's own cost band down at the bottom is what it adds up
        /// to.
        /// </summary>
        private void AddUpgrade(GroundTroopUpgrade upgrade, string key)
        {
            if (upgrade == null)
            {
                return;
            }

            GroundTroopUpgrade it = upgrade;
            AgeTransform toggle = AgeWidgets.Transform(upgrade.Toggle);
            // Which SHAPE the card takes - a tick or a plain readout - not whether it exists.
            bool picks = toggle != null && AgeWidgets.Visible(toggle);
            Func<bool> enabled = () =>
                AgeWidgets.Offered(it.HeaderGroup)
                && (!picks || AgeWidgets.Offered(AgeWidgets.Transform(it.Toggle)));
            Func<string> label = () => AgeWidgets.TextOf(it.ContentGroup);
            Func<string> header = () => Header(it);
            Func<IList<string>> details = () => Details(it, enabled);
            AgeTooltip tooltip = Tooltip(upgrade, picks);
            AgeTransform anchor = Anchor(upgrade, picks);

            NodeVtable vtable;
            if (picks)
            {
                vtable = GraphNodes.Checkbox(
                    label,
                    () => it.Toggle != null && it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    enabled,
                    tooltip,
                    details,
                    header
                );
                AgeWidgets.Point(vtable, upgrade.Toggle, tooltip, anchor);
            }
            else
            {
                vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(label),
                        GraphNodes.ValuePart(header),
                        GraphNodes.DisabledPart(enabled),
                    },
                    Sections = GraphNodes.Sections(details, tooltip),
                };
                AgeWidgets.PointAt(vtable, anchor);
            }

            vtable.Announcements.Add(
                new NodeAnnouncement(
                    () => enabled() ? null : Reason(it),
                    live: true,
                    kind: AnnouncementKinds.Tooltip
                )
            );
            Cells.Add(
                _cells,
                upgrade.AgeTransform,
                ControlId.For(upgrade.AgeTransform, key),
                vtable
            );
        }

        /// <summary>What the card's header says: the badge for one already owned, the technology one is
        /// waiting for, or the game's own wording of what it costs.</summary>
        private static string Header(GroundTroopUpgrade upgrade)
        {
            try
            {
                // Content: which of the drawn groups the words come from.
            if (AgeWidgets.Visible(upgrade.UnlockedGroup))
                {
                    return AgeWidgets.TextOf(upgrade.UnlockedGroup);
                }

                // Content: the same choice, other branch.
                if (AgeWidgets.Visible(upgrade.TechnologyGroup))
                {
                    return AgeText.Label(upgrade.TechnologyName);
                }

                return upgrade.UpgradeDefinition == null
                    ? null
                    : AgeText.Clean(
                        Gui.FormatCosts(upgrade.UpgradeDefinition.Costs, Gui.PlayerEmpire)
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The sentence the game composes for a locked upgrade and then throws away.
        ///
        /// <c>GroundTroopUpgrade.RefreshTooltip</c> writes it only while the ITEM's own transform is
        /// disabled, and <c>RefreshState</c> only ever disables the item's HEADER group - so the card is
        /// drawn faded with an empty tooltip. Reproduced off the same test the game's own method uses,
        /// which is the treatment a prerequisite-blocked hero skill already has.
        /// </summary>
        private static string Reason(GroundTroopUpgrade upgrade)
        {
            try
            {
                if (Gui.PlayerEmpire == null)
                {
                    return null;
                }

                DepartmentOfDefense defense = Gui.PlayerEmpire.GetAgency<DepartmentOfDefense>();
                return AgeText.Clean(
                    Gui.Localize(
                        defense != null && defense.HasUnlockedTroopType(upgrade.TroopType)
                            ? GroundTroopUpgrade.MissingUpgradePrereq
                            : GroundTroopUpgrade.LockedTroopTypePrereq
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The card written out for the review buffer: the reason it is refusing, and - only where the
        /// card draws SEVERAL of them - its effects one line at a time.
        ///
        /// The card's name is already the effects joined into one phrase and its value is the header, and
        /// both of those are in the buffer because they are announced; so a card with one effect on it has
        /// nothing left to add, and one with two has each of them as a line of its own to walk.
        /// </summary>
        private static IList<string> Details(GroundTroopUpgrade upgrade, Func<bool> enabled)
        {
            List<string> lines = new List<string>();
            try
            {
                IList<string> drawn = AgeWidgets.DrawnLines(upgrade.ContentGroup);
                for (int i = 0; drawn != null && drawn.Count > 1 && i < drawn.Count; i++)
                {
                    Add(lines, drawn[i]);
                }

                if (!enabled())
                {
                    Add(lines, Reason(upgrade));
                }
            }
            catch (Exception) { }

            return lines;
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
            }
        }

        /// <summary>Which of the card's tooltips is the card's own: the tick's on one that can be
        /// bought, the badge's on one already owned, the technology's on one still gated.</summary>
        private static AgeTooltip Tooltip(GroundTroopUpgrade upgrade, bool picks)
        {
            try
            {
                if (picks)
                {
                    return AgeWidgets.Raw(AgeWidgets.Transform(upgrade.Toggle));
                }

                // Content: which of the drawn groups the words come from.
            if (AgeWidgets.Visible(upgrade.UnlockedGroup))
                {
                    return AgeWidgets.Raw(upgrade.UnlockedGroup);
                }

                // Content: the same choice, other branch.
            return AgeWidgets.Visible(upgrade.TechnologyGroup)
                    ? AgeWidgets.Raw(upgrade.TechnologyGroup)
                    : AgeWidgets.Raw(upgrade.AgeTransform);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The widget the card's tooltip hangs off, which is what the pointer has to be aimed
        /// at for the game to draw it.</summary>
        private static AgeTransform Anchor(GroundTroopUpgrade upgrade, bool picks)
        {
            try
            {
                if (picks)
                {
                    return AgeWidgets.Transform(upgrade.Toggle);
                }

                // Content: which of the drawn groups the words come from.
            if (AgeWidgets.Visible(upgrade.UnlockedGroup))
                {
                    return upgrade.UnlockedGroup;
                }

                // Content: the same choice, other branch.
            return AgeWidgets.Visible(upgrade.TechnologyGroup)
                    ? upgrade.TechnologyGroup
                    : upgrade.AgeTransform;
            }
            catch (Exception)
            {
                return upgrade.AgeTransform;
            }
        }

        // ---- what it all costs ----

        /// <summary>The band along the bottom: what the pending changes would cost, drawn only while
        /// there are any, and the empire's own stocks beside it.</summary>
        private void BuildCost(GraphBuilder builder, GroundTroopManagementModalWindow window)
        {
            AgeTransform cost = AgeWidgets.ChildNamed(
                window.AgeTransform,
                "ModificationCostGroup",
                2
            );
            AgeTransform stock = AgeWidgets.ChildNamed(
                window.AgeTransform,
                "EmpireResourcesGroup",
                2
            );
            _cells.Clear();
            _stocks.Clear();
            AddResources(_cells, cost, "troops:modification-cost");
            AddResources(_stocks, stock, "troops:empire-resources");
            if (_cells.Count == 0 && _stocks.Count == 0)
            {
                return;
            }

            builder.BeginStop(CostStop);
            EmitResources(builder, _cells, cost, "ModificationCostTitle");
            EmitResources(builder, _stocks, stock, "EmpireResourcesTitle");
        }

        /// <summary>The amounts in one captioned strip, one node per amount named by the resource the
        /// game hangs on its tooltip - the same reading the economy screen's resource grid gets, and for
        /// the same reason (the symbol beside the number is a picture).</summary>
        private void AddResources(List<Cell> cells, AgeTransform group, string keyPrefix)
        {
            // Flow control: the resource strip below is walked item by item.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform table = AgeWidgets.ChildNamed(group, "ResourceItemsTable", 2);
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AddResource(cells, items[i], keyPrefix + "/" + i);
            }
        }

        /// <summary>One strip, one amount per row, under the game's own word for what the strip is.
        ///
        /// That word is the LEVEL rather than a row of its own: the game hangs no explanation on either
        /// caption (measured focused - no tooltip is drawn and the node's buffer holds nothing but the
        /// caption itself), so nothing is lost by making it the thing the player hears on the way in
        /// instead of a line they have to step over to reach the numbers.</summary>
        private static void EmitResources(
            GraphBuilder builder,
            List<Cell> cells,
            AgeTransform group,
            string caption
        )
        {
            if (cells.Count == 0)
            {
                return;
            }

            string name = AgeWidgets.TextOf(AgeWidgets.ChildNamed(group, caption, 2));
            bool named = !string.IsNullOrEmpty(name);
            if (named)
            {
                builder.PushContext(name);
            }

            Cells.EmitLinear(builder, cells);
            if (named)
            {
                builder.PopContext();
            }
        }

        private static void AddResource(List<Cell> cells, AgeTransform widget, string key)
        {
            ResourceItem item = widget == null ? null : widget.GetComponent<ResourceItem>();
            if (item == null)
            {
                return;
            }

            ResourceItem it = item;
            AgeTooltip tooltip = item.Tooltip ?? AgeWidgets.Raw(widget);
            string name = AgeWidgets.TooltipTitle(tooltip);
            bool named = !string.IsNullOrEmpty(name);
            NodeVtable vtable = GraphNodes.Readout(
                () => named ? name : CardActions.FirstLine(tooltip),
                () => AgeText.Label(it.StockLabel),
                null,
                // Declared whether or not the wrapper named it: where it did not, the label is this
                // tooltip.s own first line and the readout drops that line from what it goes on to
                // announce - so the rest of the sentence is handed over instead of thrown away.
                tooltip
            );
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
        }

        // ---- the bottom row ----

        /// <summary>Close, Reset and Confirm, one per row in the order the band draws them. Confirm
        /// carries the game's own sentence for why it cannot be pressed - "cannot afford", "no changes"
        /// - written onto its tooltip by <c>UpdateBottomButtons</c> (:306-318).</summary>
        private void BuildActions(GraphBuilder builder, GroundTroopManagementModalWindow window)
        {
            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "BackButton", 2),
                "troops:close"
            );
            Cells.AddControl(
                _cells,
                AgeWidgets.Transform(window.ResetButton),
                "troops:reset"
            );
            Cells.AddControl(
                _cells,
                AgeWidgets.Transform(window.ValidateButton),
                "troops:confirm"
            );
            if (_cells.Count > 0)
            {
                builder.BeginStop(ActionsStop);
                Cells.EmitLinear(builder, _cells);
            }
        }

        // ---- reading the window ----

        /// <summary>
        /// One cell per row, in drawn order - a plain vertical list rather than the bands the cells are
        /// drawn in.
        ///
        /// The one place in the mod that deviates from "rows as drawn", and the slider is why: the ring
        /// takes Left and Right as its own adjustment, so anything the game draws BESIDE it on the same
        /// band - here the padlock - is reachable by nothing. Down is what reaches it instead, and the
        /// order the player walks is still the order the cell is drawn in.
        /// </summary>
        private static void Column(GraphBuilder builder, List<Cell> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                builder.StartRow();
                builder.AddItem(Nodes.Drawn(cells[i].Id, cells[i].Vtable, cells[i].Widget));
                builder.EndRow();
            }
        }

        /// <summary>The caption the game draws over a column, which is a caption over several controls
        /// and so a node of its own.</summary>
        private void Caption(
            GraphBuilder builder,
            AgeTransform table,
            string name,
            string key
        )
        {
            _cells.Clear();
            AgeTransform column = table.Parent;
            Cells.AddReadout(
                _cells,
                column == null ? null : AgeWidgets.ChildNamed(column, name, 3),
                key
            );
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>What the game calls a troop type. Drawn once per visual row, in the middle column,
        /// so the other two columns take their row names from there.</summary>
        private static string TroopName(
            GroundTroopManagementModalWindow window,
            GroundBattleTroopType type
        )
        {
            try
            {
                AgeTransform table = window.DescriptionsTable;
                IList<AgeTransform> rows = table == null ? null : table.Children;
                for (int i = 0; rows != null && i < rows.Count; i++)
                {
                    GroundTroopDescription row =
                        rows[i] == null ? null : rows[i].GetComponent<GroundTroopDescription>();
                    if (row != null && row.TroopType == type)
                    {
                        return AgeText.Label(row.NameLabel);
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private static GroundTroopManagementModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GroundTroopManagementModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
