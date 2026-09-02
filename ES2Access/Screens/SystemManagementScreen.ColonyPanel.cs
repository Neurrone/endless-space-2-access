using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>The colony panel's own cells: the hand-modelled information panel a colony gets, and
    /// the wordless readouts and controls in it that have to be named one at a time.</summary>
    public sealed partial class SystemManagementScreen
    {
        /// <summary>
        /// The colony panel, hand-modelled because it is the one side panel that is mostly controls:
        /// the system's name is a rename button, the upkeep line opens the improvements list, and the
        /// automation policy is a list to choose from.
        ///
        /// Most of what the panel can draw it draws for nobody: an Ark exploiting the system, a
        /// citadel's second garrison, a ghost's decolonize tick, a siege or a blockade, the empires
        /// that have seen through a cloak. Every one of those is declared here and gated on the game's
        /// own drawn flag - the same rule the side panels themselves are chosen by - so a save that
        /// reaches the state gets the line without anything here modelling the state.
        ///
        /// The order is the panel's own, top to bottom, and <see cref="Cells.EmitLinear"/> takes it off
        /// the rectangles rather than off the order the cells are added, so a group the game collapses
        /// takes its line with it.
        /// </summary>
        private void BuildColonyInfo(GraphBuilder builder, ColonyInfoSidePanel panel)
        {
            _cells.Clear();

            // The banner and the little level badge in its corner are TWO of the game's buttons and go
            // to two different screens (<see cref="BannerButton"/>), so they are two rows - the badge
            // named with the game's own word for what it is, since the figure it draws has already been
            // said by the banner above it.
            AddReadout(
                _cells,
                panel.SystemBanner,
                "system:colony/banner",
                () =>
                    ModStrings.Format(
                        ModStrings.SystemLevel,
                        AgeText.Label(panel.LevelLabel)
                    ),
                null,
                null,
                BannerButton(panel, "OnSystemBannerClickCb")
            );
            AddReadout(
                _cells,
                panel.LevelGroup,
                "system:colony/level",
                CardActions.GameText(SystemLevelTitle),
                null,
                null,
                BannerButton(panel, "OnSystemLevelClickSb")
            );

            AddMothership(_cells, panel);
            AddSystemPaging(_cells);

            AgeControlButton rename = panel.RenameButton;
            // Banding input: Cells.Add takes the button without asking the gate, and its rectangle is
            // what puts it on the same row as the system's name.
            if (rename != null && AgeWidgets.Visible(AgeWidgets.Transform(rename)))
            {
                AgeControlButton it = rename;
                AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(rename));
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(panel.SystemTitleLabel),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                    tooltip
                );
                AgeWidgets.Point(vtable, it);
                Add(_cells, AgeWidgets.Transform(rename), ControlId.For(rename, "system:colony/rename"), vtable);
            }

            AddInfoIcons(_cells, panel);
            AddTemporaryEffects(_cells, panel);

            // The garrison dossier - what the defence is, how efficient it is, which troops it is made
            // of - is a tooltip the panel keeps in a field of its own and hangs on the GROUP around the
            // number, not on the number: read from the number's own transform there is no tooltip at
            // all, which is how this line came to say "240/240" and nothing else. The caption is the
            // game's own word for the value - the dossier wrapper's title, "System Garrison" (owner
            // ruled 2026-08-19, matching the citadel row). No fallback word: a tooltip yielding no
            // title leaves the bare value (owner ruled 2026-08-19, unauthorized fallbacks disallowed).
            AddReadout(
                _cells,
                panel.SecurityValue == null ? null : panel.SecurityValue.AgeTransform,
                "system:colony/security",
                () => AgeWidgets.TooltipTitle(panel.SecurityAndTroopsTooltip),
                () => AgeText.Label(panel.SecurityValue),
                panel.SecurityAndTroopsTooltip
            );
            AddCitadelManpower(_cells, panel);
            AddReadout(
                _cells,
                panel.UpkeepLabel == null ? null : panel.UpkeepLabel.AgeTransform,
                "system:colony/upkeep",
                () => AgeText.Label(panel.UpkeepLabel)
            );

            AgeTransform improvements = ImprovementsButton(panel);
            // Banding input: same door, same reason - the cell is banded by where it is drawn.
            if (improvements != null && AgeWidgets.Visible(improvements))
            {
                AgeTransform it = improvements;
                AgeTooltip tooltip = AgeWidgets.Raw(improvements);
                NodeVtable vtable = GraphNodes.Button(
                    () => ModStrings.Get(ModStrings.SystemImprovements),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(it),
                    tooltip
                );
                AgeWidgets.PointAt(vtable, it);
                Add(_cells, it, ControlId.For(it, "system:colony/improvements"), vtable);
            }

            AddDecolonizeGhost(_cells, panel);
            AddMilitaryStatus(_cells, panel);
            AddOwnership(_cells, panel);
            AddFidsiCells(_cells, panel);
            AddResources(_cells, panel);
            AddWreckedMotherships(_cells, panel);
            AddPolicy(_cells, panel);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The Ark parked on this system, which a Vodyani colony IS: the panel draws its name and a
        /// button that sends it back out into the galaxy
        /// (<c>ColonyInfoSidePanel.RefreshExploited</c> :650-667), and draws neither for anybody else.
        ///
        /// The NAME is a control and not a readout - the button behind it opens the game's rename box
        /// (<c>OnMothershipRenameCb</c> :953), exactly as the system's own title does - so it is
        /// declared the same way the title is: called by the name written on it, with the ship's
        /// dossier behind it. That dossier hangs on the LABEL rather than on the button, and the
        /// button's own tooltip is a key the game's corpus has no entry for (measured:
        /// <c>%StarSystemSideRenameMothershipDescription</c> localizes to itself), so the label's is
        /// the one declared and the one the pointer is aimed at.
        ///
        /// Detach is the game's own word for its button (<c>%StarSystemSideDetachMothershipTitle</c>);
        /// what it does, and why it cannot be done today, are in its own tooltip, which the panel
        /// rewrites with the ship's refusals every refresh.
        /// </summary>
        private static void AddMothership(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgePrimitiveLabel name = panel.MothershipNameLabel;
            // Different widget and banding input: the cell below stands on the NAME label, which the
            // panel leaves drawn inside a mothership group it has switched off.
            if (
                panel.MothershipGroup == null
                || !AgeWidgets.Visible(panel.MothershipGroup)
                || name == null
            )
            {
                return;
            }

            AgeTransform label = name.AgeTransform;
            AgeControlButton open = label.Parent == null
                ? null
                : label.Parent.AgeControl as AgeControlButton;
            AgeTooltip ship = AgeWidgets.Raw(label);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Label(name),
                () => AgeWidgets.Press(open),
                () => AgeWidgets.Operable(label),
                ship
            );
            AgeWidgets.Point(vtable, open, ship, label);
            Add(cells, label, ControlId.For(label, "system:colony/mothership"), vtable);

            AgeControlButton detach = panel.DetachButton;
            AgeTransform widget = AgeWidgets.Transform(detach);
            // Banding input: Cells.Add takes the button without asking the gate, and it bands with the
            // mothership's name above.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton it = detach;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable button = GraphNodes.Button(
                CardActions.GameText("%StarSystemSideDetachMothershipTitle"),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            AgeWidgets.Point(button, it);
            Add(cells, widget, ControlId.For(detach, "system:colony/detach"), button);
        }

        /// <summary>
        /// The row of badges beside the system's name: that this is somebody's home system, that a
        /// trading company keeps its headquarters or a subsidiary here, and that the system is cloaked
        /// (<c>ColonyInfoSidePanel.Refresh</c> :439-483). Each is drawn only when it is true of this
        /// system, and each is one node, because each carries a sentence of its own.
        ///
        /// The game writes no caption on any of them and hangs no wrapper on their tooltips, so each is
        /// called by the sentence its own tooltip explains it with - the same naming a wordless symbol
        /// gets everywhere else in this mod. The readout then drops that opening line from the tooltip
        /// it announces, so the rest of it - including the list of empires that have seen through the
        /// cloak, which is the only place that list exists - is handed over as well as reviewable.
        /// </summary>
        private static void AddInfoIcons(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AddInfoIcon(cells, panel.HomeSystemImage, "home");
            AddInfoIcon(cells, panel.TradeInfrastructuremage, "trade");
            AddInfoIcon(cells, panel.InvisibilityImage, "cloak");
        }

        private static void AddInfoIcon(List<Cell> cells, AgePrimitiveImage icon, string key)
        {
            AgeTransform widget = icon == null ? null : icon.AgeTransform;
            // Banding input: the three status icons are worked into one row by their rectangles, and
            // Cells.Add takes them without asking the gate.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => CardActions.FirstLine(tooltip)),
                },
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(tooltip)
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            cells.Add(
                new Cell
                {
                    Widget = widget,
                    Id = ControlId.For(widget, "system:colony/icon/" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The buffs and curses running on this system (<c>RefreshTemporaryEffects</c> :711-736). The
        /// panel has two layouts for the same list and shows exactly one of them - a line with the
        /// effect's name and how long it has left while there are one or two, a strip of bare symbols
        /// once there are more - so whichever table is drawn is the one read, and the reading is the
        /// same either way: what the item says, and its dossier behind it.
        ///
        /// The strip's items carry no label at all (measured: the simple prefab's
        /// <c>TemporaryEffectLine.Label</c> is null), so there each effect is called by the wrapper on
        /// its own tooltip, which is where the game keeps its title.
        /// </summary>
        private static void AddTemporaryEffects(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AddTemporaryEffects(cells, panel.TemporaryEffectsLineTable, "line");
            AddTemporaryEffects(cells, panel.TemporaryEffectsSimpleItemTable, "item");
        }

        private static void AddTemporaryEffects(
            List<Cell> cells,
            AgeTransform table,
            string key
        )
        {
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(table);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                TemporaryEffectLine line =
                    item == null ? null : item.GetComponent<TemporaryEffectLine>();
                // Pooled (ColonyInfoSidePanel.cs:723 ReserveChildren): a colony with fewer temporary
                // effects than the one read before it keeps the surplus lines Visible at alpha 0,
                // still holding the other colony's words.
                if (line == null || !AgeWidgets.Paints(item))
                {
                    continue;
                }

                TemporaryEffectLine it = line;
                AddReadout(
                    cells,
                    item,
                    "system:colony/effect/" + key + "/" + i,
                    () =>
                        Drawn(it.Label)
                        ?? AgeWidgets.TooltipTitle(it.Tooltip),
                    null,
                    line.Tooltip
                );
            }
        }

        /// <summary>The second pool of troops a Hissho citadel keeps, drawn beside the system's own
        /// (<c>RefreshSecurityAndUpkeep</c> :556-564) and only where the system has a citadel. The
        /// number is a stock over a maximum and the game writes no word beside it; the word is the one
        /// on the wrapper the panel hangs on the group's tooltip - "Citadel Garrison" - which is also
        /// where the breakdown of those troops lives.</summary>
        private static void AddCitadelManpower(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.CitadelManpowerGroup;
            // Banding input: Cells.Add takes the group without asking the gate, and its rectangle is
            // what puts the citadel's pool beside the system's own.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(group);
            AgePrimitiveLabel value = panel.CitadelManpowerValue;
            AddReadout(
                cells,
                group,
                "system:colony/citadel-manpower",
                () => AgeWidgets.TooltipTitle(tooltip),
                () => AgeText.Label(value),
                tooltip
            );
        }

        /// <summary>
        /// The tick a GHOST system draws where a colony draws its upkeep: schedule this sanctuary to be
        /// abandoned at the end of the turn, or unschedule it
        /// (<c>OnDecolonizeGhostToggleCb</c> :1002-1019). It is a real two-state box - the panel reads
        /// its state back off the standing order every refresh - so it is declared as one, and Enter is
        /// its own click, which posts the order or cancels it.
        ///
        /// The game names it on the action rather than on the tick
        /// (<c>%DecolonizeGhostActionTitle</c>), and the tooltip is that action's description with the
        /// panel's own reasons for refusing appended.
        /// </summary>
        private static void AddDecolonizeGhost(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeControlToggle toggle = panel.DecolonizeGhostToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            // Banding input: Cells.Add takes the tick without asking the gate, and it bands with
            // whatever the panel drew on its row.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Checkbox(
                CardActions.GameText("%DecolonizeGhostActionTitle"),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            // Ticking it posts an ORDER and the tick only becomes true once the department holds the
            // action, so the state read back on the keypress is the state before it - doubly so here,
            // because the game's own handler flips the box a second time on top of the click's flip
            // (<c>AgeControlToggle.HandleMouseUpOrDown</c> :211-215 flips, then dispatches;
            // <c>OnDecolonizeGhostToggleCb</c> :1004 flips again). The live value part is what says
            // what actually happened, when it happens.
            vtable.StateText = null;
            AgeWidgets.Point(vtable, it);
            Add(cells, widget, ControlId.For(toggle, "system:colony/decolonize"), vtable);
        }

        /// <summary>
        /// The banner the panel puts up when something military is happening to this system - it is
        /// frozen in a time bubble, being invaded, being converted, under siege, or blockaded
        /// (<c>RefreshMilitaryStatusAndOwnership</c> :569-615). One of the five at most, and nothing
        /// at all the rest of the time.
        ///
        /// The game writes the state's own word on the banner and assembles the paragraph behind it
        /// from the descriptor doing it, so the word is the line and the paragraph is the review.
        /// </summary>
        private static void AddMilitaryStatus(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.SystemMilitaryStatusGroup;
            // Banding input: Cells.Add takes the banner without asking the gate, and the panel draws it
            // only while a status is running.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgePrimitiveLabel label = panel.SystemMilitaryStatusLabel;
            AddReadout(
                cells,
                group,
                "system:colony/military-status",
                () => AgeText.Label(label)
            );
        }

        /// <summary>
        /// How much of this system its owner actually holds, drawn only while somebody else holds some
        /// of it (<c>RefreshMilitaryStatusAndOwnership</c> :633-646). The panel draws the percentage
        /// beside a symbol and writes no caption, so the caption is the game's own title for the
        /// property the number comes from - the same naming the five outputs above it get.
        ///
        /// The group answers a click, but only in the developers' god mode
        /// (<c>OnOwnershipGroupCb</c> :889-900), so it is a readout here rather than a button that
        /// does nothing - the same treatment the population panel's approval box gets.
        /// </summary>
        private static void AddOwnership(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.OwnershipGroup;
            // Banding input: Cells.Add takes the group without asking the gate.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgePrimitiveLabel label = panel.OwnershipLabel;
            AddReadout(
                cells,
                group,
                "system:colony/ownership",
                () =>
                    AgeText.Clean(
                        Gui.GetLocalizedTitle(SimulationProperties.StarSystem.Ownership)
                    ),
                () => AgeText.Label(label),
                panel.OwnershipTooltip
            );
        }

        /// <summary>
        /// The strategics and luxuries this system is exploiting. The panel keeps the banner hidden
        /// until it has something in it (<c>ResourcesBanner_Refresh</c> :847-851), so being drawn is
        /// the gate and an empty banner contributes nothing.
        ///
        /// One row per resource, read the way the empire's own stockpile strip is read
        /// (<see cref="GlobalHud"/>): the resource's name, then what is held and what the next turn
        /// does to it, computed rather than read off the labels - the labels are animated towards
        /// their targets and a reading taken mid-slide is a number the game never displayed.
        ///
        /// Which of the two figures is said is the labels' answer, though: this panel's items keep
        /// their stock label HIDDEN at prefab level (measured - the banner asks for neither
        /// <c>ShowAllStock</c> nor <c>ShowIfNonZeroStock</c>) and draw the per-turn figure alone, and a
        /// system-located resource's stock is always 0, so reading one would say a "0" the game never
        /// wrote. Each figure is therefore GATED on its own label being drawn and still COMPUTED from
        /// the cache when it is.
        /// </summary>
        private static void AddResources(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            ResourcesPanel banner = panel.ResourcesBanner;
            AgeTransform table = banner == null ? null : banner.ResourceItemsTable;
            // Flow control, and a different widget than the cells: the BANNER is what the panel hides,
            // while the items inside it keep their own flags and are what the cells stand on.
            if (table == null || !AgeWidgets.Visible(banner.AgeTransform))
            {
                return;
            }

            try
            {
                IList<AgeTransform> items = table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AgeTransform widget = items[i];
                    ResourceItem item =
                        widget == null ? null : widget.GetComponent<ResourceItem>();
                    GuiLocatedResource resource =
                        item == null ? null : item.GuiLocatedResource;
                    // Banding input: Cells.Add takes each item without asking the gate, and the items
                    // are worked into the banner's row by where they are drawn.
                    if (resource == null || !AgeWidgets.Visible(widget))
                    {
                        continue;
                    }

                    GuiLocatedResource it = resource;
                    ResourceItem row = item;
                    NodeVtable vtable = GraphNodes.Readout(
                        () => AgeText.Clean(it.Title),
                        () => ResourceRows.Figures(row),
                        null,
                        item.Tooltip
                    );
                    AgeWidgets.Point(vtable, item.Button, item.Tooltip, widget);
                    cells.Add(
                        new Cell
                        {
                            Widget = widget,
                            Id = ControlId.For(
                                item,
                                "system:colony/resource/" + resource.Name
                            ),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the colony panel's resources threw: " + e);
            }
        }

        /// <summary>How many wrecked Arks are drifting in this system, which is the only thing the
        /// panel's special-features table has ever held (<c>RefreshSpecialFeatures</c> :686-709) and is
        /// drawn only where there is at least one. The count is a bare number beside a symbol; the
        /// caption is the game's own title for the property it counts, and what the wrecks are worth
        /// and who may salvage them is the sentence on its own tooltip.</summary>
        private static void AddWreckedMotherships(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.MothershipsGroup;
            // Banding input: Cells.Add takes the group without asking the gate, and the panel draws it
            // only where there is at least one wreck.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgePrimitiveLabel count = panel.MothershipsLabel;
            AddReadout(
                cells,
                group,
                "system:colony/wrecked-motherships",
                () =>
                    AgeText.Clean(
                        Gui.GetLocalizedTitle(
                            SimulationProperties.StarSystem.WreckedMothershipCount
                        )
                    ),
                () => AgeText.Label(count),
                panel.MothershipsTooltip
            );
        }

        /// <summary>The system's five outputs, one readout each, named by the game's own titles for the
        /// properties behind them - the same pairing the panel draws as an icon and a number.</summary>
        private static void AddFidsiCells(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            FidsiEnumerator fidsi = panel.FidsiEnumerator;
            AgeTransform group = fidsi == null ? null : fidsi.FidsiGroup;
            // Flow control: the five outputs under the group are read one property at a time.
            if (group == null || fidsi.FidsiProperties == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            int count = Math.Min(fidsi.DisplayedProperties, fidsi.FidsiProperties.Count);
            for (int i = 0; i < count; i++)
            {
                AgeTransform item = ChildAt(group, i);
                GuiSimulationProperty property = fidsi.FidsiProperties[i];
                if (item == null || property == null)
                {
                    continue;
                }

                AgeTransform widget = item;
                GuiSimulationProperty it = property;
                AddReadout(
                    cells,
                    widget,
                    "system:colony/fidsi/" + i,
                    () => AgeText.Clean(Gui.GetLocalizedTitle(it.Name)),
                    () => AgeWidgets.TextOf(widget)
                );
            }
        }

        /// <summary>The automation policy: a list the control opens, which is a screen of its own - the
        /// same one every drop list in the game gets.</summary>
        private static void AddPolicy(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeControlDropList list = panel.PolicyDroplist;
            AgeTransform group = panel.PolicyGroup;
            // Banding input, and a different widget: the cell stands on the drop list, while the GROUP
            // is what the panel hides - the list inside keeps its own flag.
            if (list == null || group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeControlDropList it = list;
            ColonyInfoSidePanel owner = panel;
            AgeTransform widget = AgeWidgets.Transform(list);
            string title = LabelIn(group);
            NodeVtable vtable = GraphNodes.ComboBox(
                () => title,
                () => DropListScreen.EntryText(it, it.SelectedItem),
                () =>
                    DropListScreen.Open(
                        it,
                        title,
                        index =>
                        {
                            it.SelectedItem = index;
                            Send(it.OnSelectionObject, it.OnSelectionMethod, owner);
                        }
                    ),
                () => AgeWidgets.Operable(widget)
            );
            // Activating this opens a list rather than changing the setting, so there is no new state
            // to report: the list that opens says where it starts.
            vtable.StateText = null;
            AgeWidgets.PointAt(vtable, widget);
            Add(cells, widget, ControlId.For(list, "system:colony/policy"), vtable);
        }

        private static void Send(GameObject target, string method, Component fallback)
        {
            if (target == null && fallback != null)
            {
                target = fallback.gameObject;
            }

            if (target != null && !string.IsNullOrEmpty(method))
            {
                target.SendMessage(method, target, SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// One of the two buttons the panel hides inside its banner picture: the banner itself opens
        /// the empire summary at its systems list (<c>OnSystemBannerClickCb</c> :915-928), and the
        /// level badge in the banner's corner opens the economy screen at its own tab
        /// (<c>OnSystemLevelClickSb</c> :930-943). Neither carries a word or a tooltip of its own -
        /// the banner's tooltip belongs to the LEVEL, which is what the row already says - so they are
        /// found by the handler the prefab wired, as the improvements button beside them is.
        /// </summary>
        private static AgeTransform BannerButton(ColonyInfoSidePanel panel, string handler)
        {
            return AgeWidgets.Transform(AgeWidgets.WiredTo(panel.SystemBanner, handler));
        }

        /// <summary>The game's own word for what the badge in the banner's corner is - it draws the
        /// figure and names it nowhere on the panel.</summary>
        private const string SystemLevelTitle = "%SystemLevelTitle";

        private static AgeTransform ImprovementsButton(ColonyInfoSidePanel panel)
        {
            return AgeWidgets.Transform(
                AgeWidgets.WiredTo(panel.SystemUpkeepGroup, "OnImprovementsCb")
            );
        }
    }
}
