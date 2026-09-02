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
            // Two hover targets on one row, in the order they are drawn: the icon's one-line gloss on
            // what Approval is, and the row's own dossier, which the row points at. The gloss used to be
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
            // The kind tooltip is the row's OTHER hover target and the wait's own is the one the row
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
                // The graph's tooltip opens with the sentence that is already the row's NAME and then
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
