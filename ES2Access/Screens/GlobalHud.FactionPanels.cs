using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The faction readouts the game stacks under the banners.</summary>
    public sealed partial class GlobalHud
    {
        // ---- the faction readouts under the banners ----
        //
        // The game stacks up to seven more panels straight under the three banners, in the same column
        // and at the same width (measured: the banners fill the top 106 pixels and the stack begins
        // exactly there), and shows each one only to an empire that has the thing it counts - a
        // Vodyani's essence and Arks, a gene hunter's assimilation, a Riftborn's time bubbles, a golden
        // age's countdown, a pirate mark anyone may buy, a Hissho's keii, a Templar's relics. They are
        // part of the same cluster as the banners, so they are cells of the same stop and their rows
        // fall out of the rectangles like every other row up there; nothing here decides which row
        // anything is on.
        //
        // Which of them is DRAWN is the game's own answer, asked per frame
        // (<c>GameOverlayWindow.Update*Visibility</c>), and nothing here re-derives the affinities and
        // unlocks behind it.
        //
        // Several of these carry clicks that do nothing outside the game's own debug mode - the essence
        // and keii totals post a resource transfer only while it is in god mode, and the time bubble
        // panel's own click is that and nothing else - so those are readouts, exactly as the dust and
        // manpower totals beside them are. Only a click the game would really act on is a button.

        // Each of the seven is a ROW of its own and says what it is on the way in, the same way the four
        // banner rows above them do (owner ruling 2026-08-19, which supersedes the "no level at all"
        // reading recorded on <see cref="Empire"/>). Five are named by the game's own title for the
        // thing the panel counts and two by the mod's, because the corpus has no bare title for them -
        // see the ModStrings comment on <see cref="ModStrings.HudSingularitiesPanel"/>.
        //
        // The word rides on the CELLS, not on the call order: these rows fall out of the rectangles like
        // every other row of this stop, so a panel the game happened to draw level with another gets no
        // level at all rather than the wrong one (<see cref="RowName"/>).
        private static void AddFactionPanels(List<Cell> cells, GameOverlayWindow window)
        {
            try
            {
                int from = cells.Count;
                AddLifeforce(cells, window.LifeforceStatusPanel);
                Name(cells, from, AgeText.Title("%NetEmpireLifeforceTitle"), "lifeforce");
                from = cells.Count;
                AddGenes(cells, window.GeneManagementShortcutPanel);
                Name(cells, from, AgeText.Title("%AssimilationShortcutTitle"), "genes");
                from = cells.Count;
                AddTimeBubbles(cells, window.TimeBubbleStockPanel);
                Name(cells, from, ModStrings.Get(ModStrings.HudSingularitiesPanel), "singularities");
                from = cells.Count;
                AddGoldenAge(cells, window.GoldenAgePanel);
                Name(cells, from, AgeText.Title("%GoldenAgeTitle"), "golden-age");
                from = cells.Count;
                AddPirateMark(cells, window.PirateMarkPanel);
                Name(cells, from, ModStrings.Get(ModStrings.HudPirateMarkPanel), "pirate-mark");
                from = cells.Count;
                AddHonor(cells, window.HonorManagementPanel);
                Name(cells, from, AgeText.Title("%HonorTitle"), "honor");
                from = cells.Count;
                AddRelics(cells, window.RelicManagementPanel);
                Name(cells, from, AgeText.Title("%RelicsTitle"), "relics");
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the faction panels threw: " + e);
            }
        }

        /// <summary>What a Vodyani empire lives on: the essence it holds against what it can hold and
        /// what the turn will bring, and how many Arks are carrying it. Read off the panel's own labels
        /// rather than out of the model, because what it writes is a stock, a ceiling and a net in one
        /// line and the model would have to be re-assembled into it.</summary>
        private static void AddLifeforce(List<Cell> cells, LifeforceStatusPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AddValue(
                cells,
                "lifeforce",
                Tip(panel.LifeforceTooltip),
                SimulationProperties.Empire.NetEmpireLifeforce,
                panel.LifeforceValue
            );
            AddValue(cells, "motherships", Area(panel.MothershipValue), null, panel.MothershipValue);
        }

        /// <summary>How close a gene hunter is to absorbing another people - the line the panel writes
        /// while it is counting, or the icon it swaps in when it is ready - and the button beside it
        /// that opens the population screen. The game wires that button in its prefab and exposes no
        /// field for it, so it is found by being the panel's button (<see cref="OnlyButton"/>).</summary>
        private static void AddGenes(List<Cell> cells, GeneManagementShortcutPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AgePrimitiveLabel status = panel.AssimilationStatusLabel;
            AgeTransform line = status == null ? null : status.AgeTransform;
            // Content: which of the two the panel is drawing - the status sentence or the ready icon,
            // never both - and banding input either way, because AddCell does not ask the gate.
            if (AgeWidgets.Visible(line))
            {
                AgePrimitiveLabel it = status;
                AddCell(
                    cells,
                    line,
                    "hud:empire/assimilation",
                    GraphNodes.Readout(() => AgeText.Label(it), () => null, null, AgeWidgets.Raw(line))
                );
            }
            else
            {
                AgeTransform ready =
                    panel.ReadyIcon == null ? null : panel.ReadyIcon.AgeTransform;
                // The other half of that choice, and the same banding reason.
                if (AgeWidgets.Visible(ready))
                {
                    AgeTooltip tooltip = AgeWidgets.Raw(ready);
                    AddCell(
                        cells,
                        ready,
                        "hud:empire/assimilation",
                        GraphNodes.Readout(
                            CardActions.NameFromTooltip(tooltip),
                            () => null,
                            null,
                            tooltip
                        )
                    );
                }
            }

            AddDrawnButton(cells, OnlyButton(panel.AgeTransform), "population");
        }

        /// <summary>The bubbles a Riftborn empire is holding, one node each, in the order the strip
        /// lays them out - an empty slot included, because the strip draws one and "there is room for
        /// another" is the answer to what the strip is being asked. Pressing one puts the map into the
        /// mode that plants it, or takes the camera to the one already planted; the small button on it
        /// throws it away behind the game's own confirmation.</summary>
        private static void AddTimeBubbles(List<Cell> cells, TimeBubbleStockPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AgeTransform table = panel.TimeBubbleTable;
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                // Banding input: AddCell takes each bubble without asking the gate, and the bubbles are
                // worked into a row by where they are drawn.
                if (item == null || !AgeWidgets.Visible(item))
                {
                    continue;
                }

                AgeTransform it = item;
                AddCell(
                    cells,
                    it,
                    ControlId.Structural("hud:empire/time-bubble/" + i),
                    GraphNodes.Button(
                        ThingName(it),
                        () => AgeWidgets.Press(it),
                        () => AgeWidgets.Operable(it),
                        AgeWidgets.Raw(it)
                    )
                );

                TimeBubbleItem bubble = item.GetComponent<TimeBubbleItem>();
                AgeTransform destroy =
                    bubble == null ? null : AgeWidgets.Transform(bubble.DestroyBubbleButton);
                AddDrawnButton(
                    cells,
                    destroy,
                    ControlId.Structural("hud:empire/time-bubble/" + i + "/destroy")
                );
            }
        }

        /// <summary>How long a golden age has left, or how long the ship that starts one is locked in a
        /// garrison, plus the button that takes the camera to that ship. Each line is read as the words
        /// its own group draws, caption and figure together, because the game spreads them over two
        /// labels and only one of them is a field.</summary>
        private static void AddGoldenAge(List<Cell> cells, GoldenAgePanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AddDrawnLine(
                cells,
                panel.NextGoldenAgeDurationGroup,
                "golden-age",
                Tip(panel.GoldenAgeGaugeTooltip)
            );
            AddDrawnLine(cells, panel.LockDurationGroup, "golden-age-lock", null);
            AddDrawnButton(cells, panel.ColonizerLocationButton, "golden-age-locate");
        }

        /// <summary>The pirate mark: what it is aimed at and how long it has left where one is running,
        /// an offer to aim one where it is not. The item itself is the button that starts the aiming -
        /// the game switches the map into a targeting cursor - and it REFUSES while a mark is already
        /// out, with its own tooltip naming the system that is marked.</summary>
        private static void AddPirateMark(List<Cell> cells, PirateMarkInventoryPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AgeTransform item = panel.PirateMarkItem;
            // Banding input: AddCell appends the mark without the gate's question.
            if (AgeWidgets.Visible(item))
            {
                AgeTransform it = item;
                AddCell(
                    cells,
                    it,
                    "hud:empire/pirate-mark",
                    GraphNodes.Button(
                        () => AgeWidgets.TextOf(it),
                        () => AgeWidgets.Press(it),
                        () => AgeWidgets.Operable(it),
                        AgeWidgets.Raw(it)
                    )
                );
            }

            AddDrawnButton(cells, panel.ShowLocationButton, "pirate-mark-locate");
        }

        /// <summary>A Hissho empire's keii, and the actions its gauge unlocks - one node per threshold
        /// the panel draws a button on, named by the wrapper the game hangs on that button's own
        /// tooltip, with the turns a running one has left beside it. Pressing one starts it (the map
        /// takes a cursor for choosing where) or calls a running one off, which is the button's own
        /// click either way.</summary>
        private static void AddHonor(List<Cell> cells, HonorManagementPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AddValue(
                cells,
                "honor",
                Tip(panel.HonorTooltip),
                SimulationProperties.Empire.NetEmpireHonor,
                ValueLabel(panel.HonorValueField)
            );

            AgeTransform table = panel.HonorGaugeSegmentsTable;
            IList<AgeTransform> segments = table == null ? null : table.Children;
            for (int i = 0; segments != null && i < segments.Count; i++)
            {
                HonorGaugeSegment segment =
                    segments[i] == null ? null : segments[i].GetComponent<HonorGaugeSegment>();
                AgeControlButton button = segment == null ? null : segment.ActionButton;
                AgeTransform action = AgeWidgets.Transform(button);
                // Banding input: AddCell takes each segment's action without asking the gate, and the
                // gauge's segments are banded by where they are drawn along it.
                if (!AgeWidgets.Visible(action))
                {
                    continue;
                }

                AgeTooltip tooltip = segment.ActionTooltip;
                AgeControlButton it = button;
                AgePrimitiveLabel turns = segment.RemainingTurnsLabel;
                NodeVtable vtable = GraphNodes.Button(
                    WrapperName(tooltip),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(action),
                    tooltip
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => Turns(turns)));
                AgeWidgets.Point(vtable, it, tooltip, action);
                cells.Add(
                    new Cell
                    {
                        Widget = action,
                        Id = ControlId.Structural("hud:empire/honor-action/" + i),
                        Vtable = vtable,
                    }
                );

                // The segment's own gauge carries a SECOND dossier - the keii property the track is
                // measuring (<c>HonorGaugeSegment.Refresh</c> :67-69) - and only one tooltip can be
                // drawn at a time, so it is a node beside the action rather than a promise folded into
                // it.
                List<TooltipChildren.Dossier> gauge = new List<TooltipChildren.Dossier>(1);
                TooltipChildren.Add(gauge, segment.GaugeGroup);
                for (int g = 0; g < gauge.Count; g++)
                {
                    cells.Add(
                        new Cell
                        {
                            Widget = segment.GaugeGroup,
                            Id = ControlId.Structural("hud:empire/honor-gauge/" + i),
                            Vtable = TooltipChildren.Node(gauge[g]),
                        }
                    );
                }
            }
        }

        /// <summary>What a Templar empire has collected and where it has put it. The panel keeps a
        /// group at zero rather than dropping it - it dims it instead - so all five are read, and "we
        /// have none of those" is the answer to the question.</summary>
        private static void AddRelics(List<Cell> cells, RelicManagementPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AddValue(
                cells,
                "relics",
                panel.NetRelicsGroup,
                SimulationProperties.Empire.NetEmpireRelics,
                panel.NetRelicsLabel
            );
            AddValue(
                cells,
                "relics-research",
                panel.ResearchRelicsGroup,
                SimulationProperties.Empire.ResultingResearchRelics,
                panel.ResearchRelicsLabel
            );
            AddValue(
                cells,
                "relics-hero",
                panel.HeroRelicsGroup,
                SimulationProperties.Empire.HeroRelics,
                panel.HeroRelicsLabel
            );
            AddValue(
                cells,
                "relics-empire",
                panel.FIDIRelicsGroup,
                SimulationProperties.Empire.FIDIRelics,
                panel.FIDIRelicsLabel
            );
            AddValue(
                cells,
                "relics-temple",
                panel.TempleRelicsGroup,
                SimulationProperties.Empire.TempleRelics,
                panel.TempleRelicsLabel
            );
        }

        /// <summary>Whether the game is showing one of these panels at all - it keeps every one of them
        /// alive and hides the ones this empire has no use for.</summary>
        private static bool Drawn(GuiPanel panel)
        {
            try
            {
                // Flow control: each caller reads a whole panel's worth of cells under this answer.
                return panel != null && panel.Shown && AgeWidgets.Visible(panel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>One of these panels' readouts: what the game calls the thing, and the figure the
        /// panel is drawing for it.</summary>
        private static void AddValue(
            List<Cell> cells,
            string key,
            AgeTransform area,
            string property,
            AgePrimitiveLabel value
        )
        {
            // Banding input: AddCell appends without the gate's question, and the banner passes every
            // one of its value groups through here whether or not this empire has that currency.
            if (!AgeWidgets.Visible(area))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(area);
            AgePrimitiveLabel it = value;
            AddCell(
                cells,
                area,
                "hud:empire/" + key,
                GraphNodes.Readout(Naming(property, tooltip), () => AgeText.Label(it), null, tooltip)
            );
        }

        /// <summary>A line the game writes as a caption and a figure in separate labels inside one
        /// group, read as the one phrase it looks like.</summary>
        private static void AddDrawnLine(
            List<Cell> cells,
            AgeTransform group,
            string key,
            AgeTransform under
        )
        {
            // Banding input, as at AddValue: AddCell appends without the gate's question.
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform it = group;
            // Content: which widget's tooltip the line is read with - the panel hangs it under the group
            // on some pages and on the group itself on others.
            AgeTransform area = AgeWidgets.Visible(under) ? under : group;
            AgeTooltip tooltip = AgeWidgets.Raw(area);
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeWidgets.TextOf(it),
                () => null,
                null,
                tooltip
            );
            AgeWidgets.PointAt(vtable, area);
            cells.Add(
                new Cell
                {
                    Widget = it,
                    Id = ControlId.For(it, "hud:empire/" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>A button the game draws as a bare icon and names only in the sentence its tooltip
        /// opens with - the two "show me where that is" buttons, the bubble's own destroy.</summary>
        private static void AddDrawnButton(List<Cell> cells, AgeTransform widget, string key)
        {
            AddDrawnButton(cells, widget, ControlId.For(widget, "hud:empire/" + key));
        }

        private static void AddDrawnButton(List<Cell> cells, AgeTransform widget, ControlId id)
        {
            // Banding input: AddCell appends without the gate's question, and these bare icons band
            // with the line they sit beside.
            if (!AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(it);
            AddCell(
                cells,
                it,
                id,
                GraphNodes.Button(
                    CardActions.NameFromTooltip(tooltip),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(it),
                    tooltip
                )
            );
        }

        private static void AddCell(
            List<Cell> cells,
            AgeTransform widget,
            string key,
            NodeVtable vtable
        )
        {
            AddCell(cells, widget, ControlId.For(widget, key), vtable);
        }

        private static void AddCell(
            List<Cell> cells,
            AgeTransform widget,
            ControlId id,
            NodeVtable vtable
        )
        {
            AgeWidgets.PointAt(vtable, widget);
            cells.Add(new Cell { Widget = widget, Id = id, Vtable = vtable });
        }

        /// <summary>What to call a readout: the title the game keeps for the simulation property behind
        /// it, and where it keeps none, the sentence its own tooltip opens with. Half of these are drawn
        /// as an icon and a figure with the words nowhere but in the tooltip.</summary>
        private static Func<string> Naming(string property, AgeTooltip tooltip)
        {
            string it = property;
            AgeTooltip tip = tooltip;
            return () =>
            {
                string title = PropertyTitle(it);
                return string.IsNullOrEmpty(title) ? CardActions.FirstLine(tip) : title;
            };
        }

        /// <summary>
        /// What the game calls a simulation property, or nothing where it has no name to give.
        ///
        /// Asked about a property it has no GUI element for, the game answers with a pink "(missing
        /// GuiElement)" placeholder written for its own designers; asked about one whose title is not in
        /// the localization, it answers with the key. Neither is a name, and both are on properties
        /// these panels really use (measured: MothershipCount, TempleRelics, FIDIRelics). The
        /// still-a-key half of that is <see cref="AgeText.Title"/>; the GUI-element test is this
        /// page's own.
        /// </summary>
        private static string PropertyTitle(string property)
        {
            try
            {
                if (string.IsNullOrEmpty(property) || Gui.GetGuiElement(property) == null)
                {
                    return null;
                }

                return AgeText.Title(Gui.GetLocalizedTitle(property));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What one of these wordless items is: the words it draws, the name off the wrapper
        /// the game hangs on its tooltip, and failing both the sentence that tooltip opens with.
        /// </summary>
        private static Func<string> ThingName(AgeTransform widget)
        {
            AgeTransform it = widget;
            Func<string> named = WrapperName(AgeWidgets.Raw(widget));
            return () =>
            {
                string drawn = AgeWidgets.TextOf(it);
                return string.IsNullOrEmpty(drawn) ? named() : drawn;
            };
        }

        /// <summary>The same for a control whose tooltip the game hangs somewhere other than on it - the
        /// keii gauge's action buttons, whose tooltip is a field of the segment. Only the tooltip is
        /// asked: the words drawn ON such a button are the turns its action has left, which is a value
        /// and not a name.</summary>
        private static Func<string> WrapperName(AgeTooltip tooltip)
        {
            AgeTooltip tip = tooltip;
            return () =>
            {
                string named = AgeWidgets.TooltipTitle(tip);
                return string.IsNullOrEmpty(named) ? CardActions.FirstLine(tip) : named;
            };
        }

        /// <summary>The one button a panel draws, found by BEING one: the game wires the click in its
        /// prefab and exposes no field for it, and matching on the widget's name would tie this to a
        /// string inside an asset.</summary>
        private static AgeTransform OnlyButton(AgeTransform panel)
        {
            try
            {
                IList<AgeTransform> children = panel == null ? null : panel.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    // Content: WHICH child is the panel's one button. A panel keeps children it is not
                    // drawing, and the first of those with a button on it is not the one.
                    if (
                        child != null
                        && AgeWidgets.Visible(child)
                        && child.GetComponent<AgeControlButton>() != null
                    )
                    {
                        return child;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>The label a value field is drawn on - the field is a behaviour that writes into an
        /// <c>AgePrimitiveLabel</c> on its own transform.</summary>
        private static AgePrimitiveLabel ValueLabel(GuiValueField field)
        {
            try
            {
                return field == null
                    ? null
                    : field.AgeTransform.GetComponent<AgePrimitiveLabel>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Turns(AgePrimitiveLabel label)
        {
            return AgeWidgets.DrawnLabel(label);
        }

        private static AgeTransform Tip(AgeTooltip tooltip)
        {
            try
            {
                return tooltip == null ? null : tooltip.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}
