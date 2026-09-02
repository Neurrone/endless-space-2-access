using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>The panels down the left edge: the one stop they share, the block each panel emits,
    /// and the cells the game draws wordlessly that have to be named by hand.</summary>
    public sealed partial class SystemManagementScreen
    {
        // ---- the side panels ----

        /// <summary>
        /// A stop per panel the game is drawing down the left edge, top to bottom. Which ones those are
        /// is the game's answer to what the system is: a colony gets its colony, population and
        /// representative panels, an outpost and a ghost get their own sets. Declaring what is drawn
        /// rather than what a colony has is what makes the other two work without being modelled.
        ///
        /// The ghost pair is the one set no save here can reach - the state needs a player empire playing
        /// the Umbral Choir, which no save in this repo does - so it was measured by lending the two
        /// panels a real colony and showing them (2026-08-25). Every widget the game drew was declared: the growth
        /// gauge, the affinity of the next population, each population count with its parties, the
        /// panel's own explanation, the link status, and both destination buttons with their refusal
        /// reasons in the buffer. The two boxes the game hides while the link is unset
        /// (<c>GhostInfoSidePanel.Refresh</c> :89-101, :114-126) stayed hidden and undeclared, which is
        /// the same rule every other panel here is read by. What the lend cannot prove is the CONTENT a
        /// real ghost would carry, and what it leaves open is what the two stops are called
        /// (<see cref="PanelName"/>).
        /// </summary>
        private void BuildSidePanels(GraphBuilder builder)
        {
            // The merged stop's own name is a pushed level, so it has to be popped before anything
            // that is not in that stop is declared - the bottom panels are declared after this and
            // read "System information, Hangar, ..." while it was left open. Tracked here and closed
            // on every exit path, including the catch, which is what the push contract asks for.
            bool merged = false;
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    // The spaceport is a WORK surface and keeps a stop of its own; every other panel
                    // the game is drawing is a region of the merged one. Asked of the panel rather
                    // than of a list, so an outpost's or a ghost system's own set merges without
                    // being modelled.
                    if (panel is SpaceportSidePanel)
                    {
                        if (merged)
                        {
                            merged = false;
                            builder.PopContext();
                        }

                        builder.BeginStop("system:side/" + panel.GetType().Name);
                        builder.PushContext(PanelName(panel));
                        BuildPanel(builder, panel, i);
                        builder.PopContext();
                        continue;
                    }

                    if (!merged)
                    {
                        merged = true;
                        // Keyed by where the run STARTS, so the ordinary page - where every merged
                        // panel precedes the port - always answers "system:side" and the stop's
                        // remembered position survives a rebuild. A run beginning after the port
                        // would key itself apart rather than collide with it.
                        builder.BeginStop(i == 0 ? SidePanelsStop : SidePanelsStop + "/" + i);
                        builder.PushContext(ModStrings.Get(ModStrings.SystemSidePanels));
                    }

                    // The region key is the stop key this panel used to have, so a walk diff reads as
                    // "stop became region" with nothing else moved.
                    builder.SetRegion("system:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    BuildPanel(builder, panel, i);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the side panels threw: " + e);
            }
            finally
            {
                if (merged)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>One side panel's contents, under whatever stop or region the caller has opened for
        /// it. Which reader a panel gets is the panel's own type: three of them are hand-modelled and
        /// everything else goes through the shared readout walk.</summary>
        private void BuildPanel(GraphBuilder builder, SidePanel panel, int index)
        {
            // The key prefix is the panel's INDEX among the drawn panels, which is what it was before
            // the merge - so every node keeps the id it had and a remembered cursor still finds it.
            string keyPrefix = "system:side/" + index + "/";
            ColonyInfoSidePanel colony = panel as ColonyInfoSidePanel;
            if (colony != null)
            {
                BuildColonyInfo(builder, colony);
                return;
            }

            SpaceportSidePanel spaceport = panel as SpaceportSidePanel;
            if (spaceport != null)
            {
                BuildSpaceport(builder, spaceport, keyPrefix);
                return;
            }

            RepresentativesStarSystemSidePanel representatives =
                panel as RepresentativesStarSystemSidePanel;
            if (representatives != null)
            {
                BuildRepresentatives(builder, representatives, keyPrefix);
                return;
            }

            BuildReadouts(builder, panel, keyPrefix);
        }

        /// <summary>What a side panel is called. The game writes no title on the ones a system draws -
        /// it marks each with an icon in its corner and explains it in that icon's tooltip - so they are
        /// named here, and anything else falls through to the shared reader's own answer
        /// (<see cref="SidePanels.Name"/>).</summary>
        private static string PanelName(SidePanel panel)
        {
            if (panel is ColonyInfoSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemColonyPanel);
            }

            if (panel is ColonyPopulationSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemPopulationPanel);
            }

            if (panel is RepresentativesStarSystemSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemRepresentativesPanel);
            }

            // The spaceport panel is another of the unlabelled boxes: without a name it fell through
            // to its header icon's sentence, so the stop was called "This panel allows you to send
            // population to a colonized planet." The word is the game's own, off the panel's title.
            if (panel is SpaceportSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemSpaceportPanel);
            }

            if (panel is OutpostInfoSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemOutpostPanel);
            }

            // The hero panel is the fourth of the unlabelled boxes. Without a name of its own it fell
            // through to its header tooltip, so the stop was called "Shows information concerning the
            // Governor assigned to this star system" - a sentence, where every other stop is a word.
            if (panel is ColonyHeroSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemGovernorPanel);
            }

            // The two boxes a ghost system gets are the same kind of unlabelled box, and without a name
            // they fell through to their header sentences (measured by lending them a colony,
            // 2026-08-25). "Sanctuary" is the game's own word for a ghost colony, so both names stay in
            // its vocabulary even though the labels are the mod's (owner-approved 2026-08-25).
            if (panel is GhostPopulationSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemSanctuaryPopulationPanel);
            }

            if (panel is GhostInfoSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemSanctuaryPanel);
            }

            // The third of the sanctuary boxes, and the same kind of unlabelled box again: without a
            // name it fell through to its header sentence, so the stop was called "This panel shows
            // where the Ships and Populations created by this System will spawn" (measured by lending
            // it the fixture's own colony, 2026-08-29). "Sanctuary Link" is the game's own word for
            // what the panel sets - both its rows are headed with it
            // (<c>%ShipsSpawnPointTitle</c> "[ship] Sanctuary Link:",
            // <c>%PopulationsSpawnPointTitle</c>) - so the name stays in that vocabulary, as the two
            // ghost panels' do.
            if (panel is ShipsSpawnPointSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemSanctuaryLinksPanel);
            }

            return SidePanels.Name(panel);
        }

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

        // ---- reading a panel nobody has modelled ----

        /// <summary>
        /// A panel read as it is drawn, through the shared side-panel reader
        /// (<see cref="SidePanels.Readouts"/>). The population and representative panels are all
        /// readouts and no decisions, and the panels an outpost or a ghost gets instead are the same
        /// shape, so they are all read that way rather than each having its own list of fields to keep
        /// in step with the game.
        ///
        /// The two hooks that reader takes are this page's own: <see cref="Special"/> for the readouts
        /// the shape of a widget tree cannot name, and <see cref="Transparent"/> for a group the game
        /// made clickable that is really a band of readouts.
        /// </summary>
        private void BuildReadouts(GraphBuilder builder, SidePanel panel, string keyPrefix)
        {
            _cells.Clear();
            SidePanels.Readouts(_cells, panel, keyPrefix, SpecialCell, Transparent);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The representatives panel, which is the one side panel the game draws as two CAPTIONED
        /// blocks: who this system sends to the senate, and how its citizens react to what happens.
        ///
        /// Both captions carry a sentence the game writes nowhere else, so both stay rows AND name the
        /// block under them - a context has no buffer, so converting them would delete the sentence.
        /// The blocks are read off the drawn layout: the sensitivity block is the group the breakdown
        /// graph is drawn in, and everything above it is the representatives block.
        /// </summary>
        private void BuildRepresentatives(
            GraphBuilder builder,
            RepresentativesStarSystemSidePanel panel,
            string keyPrefix
        )
        {
            AgeTransform sensitivity =
                panel.PoliticalSensitivityBreakdown == null
                    ? null
                    : panel.PoliticalSensitivityBreakdown.Parent;
            _blocks.Clear();
            IList<AgeTransform> children =
                panel.ContentGroup == null ? null : panel.ContentGroup.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Flow control: each block kept here is read as a section of its own below, and the
                // list is walked in drawn order - a block the panel is not drawing would open one over
                // nothing.
                if (children[i] != null && AgeWidgets.Visible(children[i]))
                {
                    _blocks.Add(children[i]);
                }
            }

            _blocks.Sort(ByDrawnY);
            int split = _blocks.IndexOf(sensitivity);
            if (split <= 0)
            {
                BuildReadouts(builder, panel, keyPrefix);
                return;
            }

            EmitBlock(builder, panel, keyPrefix, 0, split);
            EmitBlock(builder, panel, keyPrefix, split, _blocks.Count);
        }

        private static readonly Comparison<AgeTransform> ByDrawnY = (left, right) =>
            left.GetGlobalPosition().y.CompareTo(right.GetGlobalPosition().y);

        /// <summary>One captioned block of a panel read in pieces: its own lines, one per row, under the
        /// caption the game drew over them - which is the topmost line the block produced, and is a row
        /// of the block as well as its name.
        ///
        /// The blocks are CONTEXTS and no longer regions of their own (owner design 2026-08-29): the
        /// side panels are one stop now and a region there is one PANEL, so a panel splitting itself
        /// into two would put five region-jumps where the design asks for four. The captions still name
        /// the blocks and are still rows, so nothing the player can hear was lost - only the region
        /// chord's stop inside this one panel.</summary>
        private void EmitBlock(
            GraphBuilder builder,
            SidePanel panel,
            string keyPrefix,
            int from,
            int to
        )
        {
            _cells.Clear();
            for (int i = from; i < to; i++)
            {
                SidePanels.Block(_cells, panel, _blocks[i], keyPrefix, SpecialCell, Transparent);
            }

            if (_cells.Count == 0)
            {
                return;
            }

            string caption = Caption(_cells);
            if (caption != null)
            {
                builder.PushContext(caption);
            }

            try
            {
                Cells.EmitLinear(builder, _cells);
            }
            finally
            {
                if (caption != null)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>The caption a block is called by: the topmost line the game drew in it, where that
        /// line is words rather than a control. A block whose first line is a control has no caption and
        /// is named by nothing rather than by its first button.</summary>
        private static string Caption(List<Cell> cells)
        {
            Cell top = null;
            float y = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                float at = cells[i].Widget.GetGlobalPosition().y;
                if (top == null || at < y)
                {
                    top = cells[i];
                    y = at;
                }
            }

            if (top == null || AgeWidgets.Button(top.Widget) != null)
            {
                return null;
            }

            string text = AgeWidgets.TextOf(top.Widget);
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static bool SpecialCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            if (GovernorInformation(cells, widget, keyPrefix, panel as ColonyHeroSidePanel))
            {
                return true;
            }

            if (SpaceportPopulations(widget, panel as SpaceportSidePanel))
            {
                return true;
            }

            Cell special = Special(widget, keyPrefix, panel);
            if (special == null)
            {
                return false;
            }

            cells.Add(special);
            AddNestedDossiers(cells, widget, keyPrefix);
            return true;
        }

        /// <summary>
        /// The dossiers a row's own tooltip names INSIDE itself, as CHILDREN of that row.
        ///
        /// A population entry's tooltip ends by naming the political parties those people lean
        /// towards, and each name carries the party's own dossier - reachable by a mouse with one more
        /// hover and by nothing else, because the game draws one tooltip at a time
        /// (<see cref="PoliticsDossier"/>). They hang UNDER the population as a "Tooltips" region, like
        /// every other node in the game that owns dossiers beyond its own
        /// (<see cref="TooltipChildren"/>); until 2026-08-22 they were the row BELOW it instead,
        /// because this panel emits a flat list of cells and a cell could not open a subtree. It can
        /// now (<see cref="Cells.Declare"/>), so the compromise is retired.
        /// </summary>
        private static void AddNestedDossiers(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix
        )
        {
            PopulationCount population = widget.GetComponent<PopulationCount>();
            if (population == null || cells.Count == 0)
            {
                return;
            }

            List<TooltipChildren.Dossier> parties = PoliticsDossier.Parties(population.Tooltip);
            if (parties.Count == 0)
            {
                return;
            }

            Cell owner = cells[cells.Count - 1];
            owner.Dossiers = parties;
            owner.Key = keyPrefix + widget.name + "/population";
        }

        // ---- the readouts the tree's shape cannot name ----

        /// <summary>
        /// A control the panels draw as symbols and numbers, read from the game's own model instead of
        /// from the words on it - because there are none. Each of these was a line of bare digits
        /// before: "2", "1", "3", "50% Content", "+Imperials 9 Turn", and one graph that produced no
        /// line at all.
        ///
        /// Null for everything else, which is the ordinary walk.
        /// </summary>
        private static Cell Special(AgeTransform widget, string keyPrefix, SidePanel panel)
        {
            PopulationCount population = widget.GetComponent<PopulationCount>();
            if (population != null)
            {
                return PopulationCell(widget, population, keyPrefix);
            }

            SystemRepresentativeItem representative = widget.GetComponent<SystemRepresentativeItem>();
            if (representative != null)
            {
                return RepresentativeCell(widget, representative, keyPrefix);
            }

            ColonyPopulationSidePanel population2 = panel as ColonyPopulationSidePanel;
            if (population2 != null)
            {
                HappinessSidePanelItem approval = population2.HapinessGroup;
                if (approval != null && ReferenceEquals(widget, approval.AgeTransform))
                {
                    return ApprovalCell(widget, approval, population2, keyPrefix);
                }

                GrowthItem growth = population2.GrowthGaugeItem;
                if (
                    growth != null
                    && growth.NextPopulationLabel != null
                    && ReferenceEquals(widget, growth.NextPopulationLabel.AgeTransform.Parent)
                )
                {
                    return GrowthCell(widget, growth, keyPrefix);
                }

                if (ReferenceEquals(widget, population2.OutpostsGroup))
                {
                    return OutpostsCell(widget, population2, keyPrefix);
                }
            }

            OutpostInfoSidePanel outpost = panel as OutpostInfoSidePanel;
            if (
                outpost != null
                && outpost.GrowthSourceName != null
                && ReferenceEquals(widget, outpost.GrowthSourceName.AgeTransform)
            )
            {
                return GrowthSourceCell(widget, outpost, keyPrefix);
            }

            RepresentativesStarSystemSidePanel representatives =
                panel as RepresentativesStarSystemSidePanel;
            if (
                representatives != null
                && ReferenceEquals(widget, representatives.PoliticalSensitivityBreakdown)
            )
            {
                return SensitivityCell(widget, representatives, keyPrefix);
            }

            ColonyHeroSidePanel governor = panel as ColonyHeroSidePanel;
            if (governor != null && ReferenceEquals(widget, governor.HeroPortraitGroup))
            {
                return GovernorPortraitCell(widget, governor, keyPrefix);
            }

            return null;
        }

        /// <summary>
        /// The band the governor panel draws beside the portrait: the hero's name, the symbol for their
        /// affinity, the gauge their experience is drawn in, the symbol for their class.
        ///
        /// Declared here rather than walked, because the shape of the band answers wrongly twice. The
        /// NAME is the portrait's own words - the panel writes the hero's title on both, and the portrait
        /// is where the dossier hangs (<see cref="GovernorPortraitCell"/>) - and the label carries a
        /// tooltip the panel never gives a hero to (measured: class <c>Hero</c>, target null), so the
        /// walk's line for it announced a dossier that can never draw and repeated the name under it. The
        /// two SYMBOLS are the opposite case: the game hangs a whole dossier on each and writes no word
        /// beside them, so a walk that keeps a line for having text dropped the only two things in the
        /// band the portrait does not already say. Each is named the way every wordless icon in this mod
        /// is named, by the wrapper on its own tooltip - "Imperials", "Counselor".
        ///
        /// Claiming the band means the gauge inside it is this method's to declare as well, which is why
        /// it is the one place the level line is built.
        /// </summary>
        private static bool GovernorInformation(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            ColonyHeroSidePanel panel
        )
        {
            if (panel == null || !ReferenceEquals(widget, panel.HeroInformationGroup))
            {
                return false;
            }

            GovernorSymbolCell(cells, panel.AffinityIcon, panel.AffinityTooltip, keyPrefix);
            // Banding input: the cell is appended straight to the list, so the gate never sees it until
            // the bands are already drawn - and the governor's symbols share one row.
            if (
                panel.ExperienceGauge != null
                && AgeWidgets.Visible(panel.ExperienceGauge.AgeTransform)
            )
            {
                cells.Add(
                    GovernorLevelCell(panel.ExperienceGauge.AgeTransform, panel, keyPrefix)
                );
            }

            GovernorSymbolCell(cells, panel.ClassIcon, panel.ClassTooltip, keyPrefix);
            return true;
        }

        /// <summary>One of the two symbols in that band: what the wrapper on its tooltip calls it, and
        /// that tooltip's own dossier, pointed at the symbol so the game draws it.</summary>
        private static void GovernorSymbolCell(
            List<Cell> cells,
            AgePrimitiveImage icon,
            AgeTooltip tooltip,
            string keyPrefix
        )
        {
            AgeTransform widget = icon == null ? null : icon.AgeTransform;
            // Banding input: same row, same reason - the cell is appended without the gate's question.
            if (widget == null || tooltip == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tip = tooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tip)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget, tooltip);
            cells.Add(
                new Cell
                {
                    Widget = widget,
                    Id = ControlId.For(widget, keyPrefix + widget.name),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The governor's portrait, once a hero holds the post. The game hangs the hero's dossier on the
        /// portrait IMAGE inside the group and leaves the clickable group itself textless and
        /// tooltipless, so the shape walk declared it as a nameless "button" - the tooltip is the only
        /// place the hero's name lives on this control, and the pointer has to be aimed at the child
        /// that carries it or the dossier never draws.
        ///
        /// The click is <c>OnInspectCb</c>, the same click the panel's own Inspect button carries: two
        /// controls, one command, both kept because the game draws both.
        /// </summary>
        private static Cell GovernorPortraitCell(
            AgeTransform widget,
            ColonyHeroSidePanel panel,
            string keyPrefix
        )
        {
            AgeTooltip tooltip = panel.HeroTooltip;
            AgeControlButton button = widget.AgeControl as AgeControlButton;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TooltipTitle(tooltip),
                () => AgeWidgets.Press(button),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, button, tooltip, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/governor-portrait"),
                Vtable = vtable,
            };
        }

        /// <summary>The governor's level. The gauge draws the number alone and explains what experience
        /// is on its tooltip, so the digit arrived captionless ("1, Heroes gain experience through their
        /// assignment..."); the word for it is the game's own, the one the hero cards put beside the
        /// same number.</summary>
        private static Cell GovernorLevelCell(
            AgeTransform widget,
            ColonyHeroSidePanel panel,
            string keyPrefix
        )
        {
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            AgePrimitiveLabel level = panel.LevelLabel;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => HeroCards.LevelCaption()),
                    GraphNodes.ValuePart(() => AgeText.Label(level)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/governor-level"),
                Vtable = vtable,
            };
        }

        /// <summary>Whether a group the game made clickable is really a band of readouts. The approval
        /// box answers a click only in the developers' god mode, and treating it as one control is what
        /// glued its icon, its percentage and its status word into a single "50% Content" line.
        /// </summary>
        private static bool Transparent(AgeTransform widget, SidePanel panel)
        {
            ColonyPopulationSidePanel population = panel as ColonyPopulationSidePanel;
            return population != null
                && population.HapinessGroup != null
                && ReferenceEquals(widget, population.HapinessGroup.AgeTransform.Parent);
        }

        /// <summary>One kind of person living here, read as every panel that lists them reads one
        /// (<see cref="PopulationRows.Count"/>).</summary>
        private static Cell PopulationCell(
            AgeTransform widget,
            PopulationCount unit,
            string keyPrefix
        )
        {
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/population"),
                Vtable = PopulationRows.Count(widget, unit, unit.Tooltip),
            };
        }

        /// <summary>
        /// Which colony is feeding this outpost. The panel's other rows are a caption and a value side
        /// by side, so the ordinary walk names them; this one is the colony's NAME alone, with the
        /// only words saying what that name is doing there sitting on the row's own tooltip - so the
        /// name is the value and the game's sentence is the row's tooltip, exactly as the rows above it
        /// read. The button beside it that changes the colony is left to the ordinary walk, which
        /// already names it from its own tooltip.
        /// </summary>
        private static Cell GrowthSourceCell(
            AgeTransform widget,
            OutpostInfoSidePanel panel,
            string keyPrefix
        )
        {
            OutpostInfoSidePanel it = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(panel.ColonyGroup);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.GrowthSourceName)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, panel.ColonyGroup);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/growth-source"),
                Vtable = vtable,
            };
        }

        /// <summary>A party's seats on this system's council. Drawn as the party's emblem and a count,
        /// with the party itself on the tooltip - the tooltip's own words are the internal name of the
        /// party ("Politics01"), so the wrapper is the only place its title can come from.</summary>
        private static Cell RepresentativeCell(
            AgeTransform widget,
            SystemRepresentativeItem item,
            string keyPrefix
        )
        {
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            AgePrimitiveLabel count = item.ProbabilityLabel;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tooltip)),
                    GraphNodes.ValuePart(() => AgeText.Label(count)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/representative"),
                Vtable = vtable,
            };
        }

        /// <summary>How the people here feel about being governed: the game's own name for the measure -
        /// which is a different word for an empire that rules by honour - then the percentage and the
        /// status word the panel draws.</summary>
        private static Cell ApprovalCell(
            AgeTransform widget,
            HappinessSidePanelItem approval,
            ColonyPopulationSidePanel panel,
            string keyPrefix
        )
        {
            HappinessSidePanelItem it = approval;
            ColonyPopulationSidePanel owner = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            AgeTooltip iconTooltip = AgeWidgets.Raw(
                approval.HappinessIcon == null ? null : approval.HappinessIcon.AgeTransform
            );
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ApprovalName(owner)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.HappinessValueLabel)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.HappinessStatusLabel)),
                },
            };
            // Two hover targets on one row, in the order they are drawn: the icon.s one-line gloss on
            // what Approval is, and the row.s own dossier, which the row points at. The gloss used to be
            // a reviewed line here - words on a row the pointer never visits, which the game therefore
            // never draws - and is now an entry of its own, aimed at the icon a mouse would have
            // pointed at.
            TooltipChildren.Carried carried = TooltipChildren.Split(
                new List<AgeTooltip> { iconTooltip, tooltip }
            );
            vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipSection(carried.Own));
            AgeWidgets.PointAt(vtable, widget);
            string approvalKey = keyPrefix + widget.name + "/approval";
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, approvalKey),
                Vtable = vtable,
                Dossiers = carried.Children,
                Key = approvalKey,
            };
        }

        /// <summary>What this system's approval figure is CALLED - which is a different word for an
        /// empire that runs on honour. The game answers it with one system-level member,
        /// <c>IHappinessProvider.HappinessTag</c> (<c>ColonizedStarSystem</c> :241), which is the name
        /// of the gui element the panel writes into its own tooltip
        /// (<c>HappinessSidePanelItem.Refresh</c> :29) - so the word is the game's own rather than this
        /// mod picking between two empire properties by re-asking <c>CanUseHonor</c>.</summary>
        private static string ApprovalName(ColonyPopulationSidePanel panel)
        {
            try
            {
                IHappinessProvider system =
                    panel == null ? null : panel.ColonizedStarSystem as IHappinessProvider;
                return system == null
                    ? null
                    : AgeText.Clean(Gui.GetLocalizedTitle(system.HappinessTag));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Who is being born here next and when. The panel draws the kind as a symbol with a
        /// plus in front of it and the wait as a bare number of turns; the sentence the game explains
        /// the symbol with is the only thing on the panel that says what either of them means, so it is
        /// what this is called.</summary>
        private static Cell GrowthCell(AgeTransform widget, GrowthItem growth, string keyPrefix)
        {
            GrowthItem it = growth;
            AgeTooltip kind = AgeWidgets.Raw(growth.NextPopulationLabel.AgeTransform);
            AgeTooltip when = growth.TurnsBeforeNextPop == null
                ? null
                : AgeWidgets.Raw(growth.TurnsBeforeNextPop.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () =>
                            CardActions.FirstLine(kind)
                            ?? AgeText.Label(it.NextPopulationLabel)
                    ),
                    GraphNodes.ValuePart(() => Drawn(it.TurnsBeforeNextPop)),
                    GraphNodes.ValuePart(() => Drawn(it.NextPopulationDestinationLabel)),
                },
            };
            // The kind tooltip is the row.s OTHER hover target and the wait.s own is the one the row
            // points at, so the kind becomes an entry of its own rather than a reviewed line the row
            // cannot make the game draw.
            TooltipChildren.Carried carried = TooltipChildren.Split(
                new List<AgeTooltip> { kind, when }
            );
            vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipSection(carried.Own));
            AgeWidgets.PointAt(vtable, widget);
            string growthKey = keyPrefix + widget.name + "/growth";
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, growthKey),
                Vtable = vtable,
                Dossiers = carried.Children,
                Key = growthKey,
            };
        }

        /// <summary>
        /// How many outposts this colony is feeding. The game draws the number alone beside a symbol
        /// and writes no title for the row anywhere in its corpus - the only words about it are the
        /// sentence on the row's own tooltip, which names the outposts and so belongs to the row as its
        /// detail rather than as its name. So the count is said in the mod's own counted phrase and the
        /// game's sentence follows it under the ordinary tooltip rule.
        ///
        /// The number comes from the system the panel is showing, not from the digits on the label: the
        /// label is the count already turned into text for the eye, and the model is what a phrase that
        /// has to choose a plural form needs.
        /// </summary>
        private static Cell OutpostsCell(
            AgeTransform widget,
            ColonyPopulationSidePanel panel,
            string keyPrefix
        )
        {
            ColonyPopulationSidePanel it = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => OutpostsSupplied(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/outposts"),
                Vtable = vtable,
            };
        }

        private static string OutpostsSupplied(ColonyPopulationSidePanel panel)
        {
            try
            {
                ColonizedStarSystem system = panel == null ? null : panel.ColonizedStarSystem;
                int count = system == null ? 0 : system.OutpostMigrationDestinationSystems.Count;
                return count <= 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.SystemSupplyingOutpost,
                        ModStrings.SystemSupplyingOutposts,
                        count
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A drawn-but-blank label answers null here rather than the empty string
        /// <see cref="AgeWidgets.DrawnLabel"/> keeps the two cases apart with: one caller falls back to
        /// the wrapper's title with ?? for an effect line drawn as a bare picture.</summary>
        private static string Drawn(AgePrimitiveLabel label)
        {
            string drawn = AgeWidgets.DrawnLabel(label);
            return string.IsNullOrEmpty(drawn) ? null : drawn;
        }

        /// <summary>
        /// The political sensitivity graph: one bar per party, as tall a fraction of the plot as that
        /// share of the people here leans towards it. The bars carry no text whatever - the graph is
        /// drawn from clipped rectangles - so the parties come from the game's own list of them, in the
        /// order it lays the bars out, and each share is how far up its own bar is left unclipped.
        ///
        /// The bars a party has no support in are drawn faded, so only the ones with any are spoken;
        /// all of them are in the review buffer.
        /// </summary>
        private static Cell SensitivityCell(
            AgeTransform widget,
            RepresentativesStarSystemSidePanel panel,
            string keyPrefix
        )
        {
            RepresentativesStarSystemSidePanel it = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => CardActions.FirstLine(tooltip)),
                    GraphNodes.ValuePart(() => SensitivityText(it, true)),
                },
                // The graph.s tooltip opens with the sentence that is already the row.s NAME and then
                // says what the sensitivity is for. It reads by its own kind, and the readout drops the
                // opening line the name has already said.
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(tooltip),
                    NodeSection.Buffer(() => SensitivityDetails(it))
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/sensitivity"),
                Vtable = vtable,
            };
        }

        private static string SensitivityText(
            RepresentativesStarSystemSidePanel panel,
            bool supportedOnly
        )
        {
            MessageBuilder message = new MessageBuilder();
            List<string> bars = new List<string>();
            Sensitivity(panel, supportedOnly, bars);
            for (int i = 0; i < bars.Count; i++)
            {
                message.ListItem(bars[i]);
            }

            return message.Build();
        }

        private static IList<string> SensitivityDetails(RepresentativesStarSystemSidePanel panel)
        {
            List<string> lines = new List<string>();
            Sensitivity(panel, false, lines);
            return lines;
        }

        private static void Sensitivity(
            RepresentativesStarSystemSidePanel panel,
            bool supportedOnly,
            List<string> into
        )
        {
            try
            {
                AgeTransform container = panel.PoliticsGaugesContainer;
                IList<AgeTransform> bars = container == null ? null : container.Children;
                if (bars == null)
                {
                    return;
                }

                IList<GuiPolitics> parties = Parties();
                for (int i = 0; i < bars.Count && i < parties.Count; i++)
                {
                    PoliticsSensitivityGauge gauge =
                        bars[i] == null ? null : bars[i].GetComponent<PoliticsSensitivityGauge>();
                    if (gauge == null || gauge.Clipper == null)
                    {
                        continue;
                    }

                    float share = (100f - gauge.Clipper.PercentTop) * 0.01f;
                    if (supportedOnly && share <= 0f)
                    {
                        continue;
                    }

                    into.Add(
                        new MessageBuilder()
                            .Fragment(AgeText.Clean(parties[i].Title))
                            .Fragment(Amplitude.Extensions.FloatExtensions.ToString(share, 0, true))
                            .Build()
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the political sensitivity graph threw: " + e);
            }
        }

        private static readonly List<GuiPolitics> _parties = new List<GuiPolitics>();

        /// <summary>The parties the graph has a bar for, in the graph's own order: the game's list of
        /// them with the independents left out, which is the same filter the panel applies when it
        /// makes the bars.</summary>
        private static IList<GuiPolitics> Parties()
        {
            _parties.Clear();
            try
            {
                System.Collections.IList all = Gui.GuiWrapperProviderService.GuiPolitics;
                for (int i = 0; i < all.Count; i++)
                {
                    GuiPolitics party = all[i] as GuiPolitics;
                    if (party != null && !party.PoliticsDefinition.IsNeutral)
                    {
                        _parties.Add(party);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: listing the political parties threw: " + e);
            }

            return _parties;
        }

        /// <summary>
        /// The two arrows the game draws either side of the system's name, which walk the empire's own
        /// colonised systems (<c>StarSystemScreen.CycleStarSystemHelper</c> :180-197). They are declared
        /// with the name rather than in a stop of their own because that is where the game puts them,
        /// and <see cref="Cells.EmitLinear"/> takes the reading order off the rectangles - so previous,
        /// the name, next comes out in the order the player sees.
        ///
        /// The game gives the arrows no title at all, only a sentence in each one's own tooltip, so the
        /// mod names them the way it names the planet page's pair - and each name ends with the chord
        /// that does the same thing from anywhere on the page, since the whole point of declaring the
        /// buttons is that a player who found one has found the gesture too.
        ///
        /// They belong to the colony panel, which the game binds for a colony, an outpost and a ghost
        /// alike (<c>StarSystemScreen.BindStarSystemNode</c> :555-560) - the same condition under which
        /// it draws the arrows at all.
        /// </summary>
        private static void AddSystemPaging(List<Cell> cells)
        {
            StarSystemScreen window = Window();
            if (window == null)
            {
                return;
            }

            AddSystemPage(
                cells,
                window.PreviousSystemButton,
                "system:previous",
                ModStrings.SystemPrevious,
                UiActions.PagePrev
            );
            AddSystemPage(
                cells,
                window.NextSystemButton,
                "system:next",
                ModStrings.SystemNext,
                UiActions.PageNext
            );
        }

        private static void AddSystemPage(
            List<Cell> cells,
            AgeControlButton button,
            string key,
            string nameKey,
            string actionKey
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            // Banding input: Add below is Cells.Add, which takes the button without asking the gate.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTransform host = widget;
            string named = nameKey;
            string action = actionKey;
            NodeVtable vtable = GraphNodes.Button(
                () => ChordNames.Label(ModStrings.Get(named), action, 0),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(host),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.Point(vtable, it);
            Add(cells, widget, ControlId.For(button, key), vtable);
        }
    }
}
