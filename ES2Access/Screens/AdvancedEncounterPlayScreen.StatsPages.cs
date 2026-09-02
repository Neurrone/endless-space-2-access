using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The figures: the four pages of stats the window keeps behind its four switches, and
    /// the fighters line written under them.</summary>
    public sealed partial class AdvancedEncounterPlayScreen
    {
        /// <summary>
        /// The figures: the four pages of stats the window keeps behind its four switches, and the
        /// fighters line the window writes under them.
        ///
        /// Named ("Stats") like the two sides are, because Tab now stops here on its own way round and
        /// a stop the player lands in says what it is. Positions stay ON through the level - the pager
        /// rows are a list, and the place-in-list stamp is what replaced the ticked switch
        /// (<see cref="Pages"/>).
        /// </summary>
        private void Figures(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(FiguresRegion);
            bool named = BattleRows.Context(builder, ModStrings.BattleStats, true);
            try
            {
                Pages(builder, window);
                BattleRows.NoteBeside(
                    builder,
                    window.FightersStanceRatioLabel,
                    "advanced-play/fighters"
                );
            }
            finally
            {
                BattleRows.Close(builder, named);
            }

            builder.SetRegion(null);
        }

        /// <summary>
        /// The stats as the LIST of pages they are: one row per page, and standing on a row is what
        /// turns the window's box to that page.
        ///
        /// The window draws four switches and one box, which costs the keyboard five controls to read
        /// one page. Here the pages ARE the list, exactly as the tutorial's are
        /// (<see cref="TutorialScreen"/>) and as the faction window's hulls are
        /// (<c>FactionChoiceScreen.BuildHulls</c>): up and down walk the four pages, the box follows
        /// visibly, and the switches are not declared at all because the list has taken over their
        /// whole job. Where the page number was said by the switch being ticked it is now the engine's
        /// own place-in-list stamp.
        ///
        /// Entering the stop lands on the page the window is ALREADY showing, never on row one: a
        /// landing that ignored which page is up would turn the picture out from under a player who
        /// had only come to read it. A position the player left here still outranks it, which is the
        /// order a remembered place should come in.
        ///
        /// One drawn viewer, N paged contents - evidence is the box every page is painted into, and
        /// identity is the index; per-page widgets exist but three of the four are switched off at any
        /// moment, so a row keyed on its own panel would be a row the gate drops for being the page
        /// the player is not on.
        /// </summary>
        private void Pages(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            AgeControlToggle[] toggles = window.StatsToggles;
            AgeTransform[] panels = window.StatsPanels;
            AgeTransform box = Box(panels);
            if (toggles == null || panels == null || box == null)
            {
                return;
            }

            int count = Math.Min(toggles.Length, panels.Length);
            for (int i = 0; i < count; i++)
            {
                ControlId id = ControlId.Structural(StatPageKey + i);
                builder.AddItem(Nodes.Drawn(id, Page(window, i), box));
                if (toggles[i] != null && toggles[i].State)
                {
                    builder.LandStopOn(id);
                }
            }
        }

        /// <summary>The box the window paints whichever page is showing into - the one thing on this
        /// band that is drawn whatever page that is, which is what every row stands or falls with. It
        /// is asked of the panels rather than named, because the window keeps no field for it.
        /// </summary>
        private static AgeTransform Box(AgeTransform[] panels)
        {
            for (int i = 0; panels != null && i < panels.Length; i++)
            {
                AgeTransform parent = panels[i] == null ? null : panels[i].Parent;
                if (parent != null)
                {
                    return parent;
                }
            }

            return null;
        }

        /// <summary>
        /// One page of the stats: the figures the window is drawing on it, and - asked first - the box
        /// being turned to this page in the first place.
        ///
        /// Reading the row's WORDS is where the turn happens rather than a focus hook, because that is
        /// the only thing that runs between the cursor arriving and the landing being spoken; a switch
        /// driven from the hook would read the page the player just left. It is guarded on the row
        /// being the focused one, so a graph dump or a type-ahead pass over the stop turns no pages.
        ///
        /// The page's NAME and each of its figures are announcement parts of their own
        /// (<see cref="Figures"/>). Spoken they read as the one sentence they always did - the
        /// announcer joins the parts with the same separator the composed string used - and in the
        /// review buffer they are what a multi-part row is there: one line per part, steppable
        /// (owner-reported 2026-08-29, where "Military power" ran into the balance sentence).
        ///
        /// No role word: a page is not a control the player works, it is what the window is showing
        /// them.
        /// </summary>
        private NodeVtable Page(AdvancedEncounterPlayModalWindow window, int index)
        {
            AdvancedEncounterPlayModalWindow it = window;
            int page = index;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() =>
                    {
                        Show(it, page);
                        return OptionalText.Phrase(NameKey(Which(it, page)));
                    }),
                },
            };
            Figures(vtable, window, index);
            vtable.Sections = Explanations(vtable, window, index);
            return vtable;
        }

        /// <summary>
        /// What the game says about this page, in its own words: the sentence on each diagram the page
        /// draws, and - on the first page - the sentence the window hangs on the band of switches.
        ///
        /// A page draws up to three diagrams and a control announces the ONE tooltip it points at, so
        /// the first diagram's is the aimed one and the rest are reviewed sections, the same shape the
        /// sorting band's sentence takes (<see cref="Band"/>).
        ///
        /// The page with NO diagram of its own points at the band of switches instead: the sentence
        /// the game hung there is about the list itself ("Choose which set of stats you want to see"),
        /// which is exactly what a row on a page with nothing else to explain it has to say - and
        /// pointing at it is what makes the game DRAW the words the row speaks, where before this the
        /// row said them and aimed nowhere (owner ruling 2026-08-29, off the painted-side audit). A
        /// page that has a diagram AND is the first keeps that sentence as a reviewed section, which
        /// is where it always was.
        ///
        /// A row with neither releases the pointer: there is no control under the cursor to light up,
        /// and nothing to leave a neighbour's tooltip hanging over.
        /// </summary>
        private static IList<NodeSection> Explanations(
            NodeVtable vtable,
            AdvancedEncounterPlayModalWindow window,
            int index
        )
        {
            AgeTransform[] diagrams = Diagrams(window, Panel(window, index));
            AgeTooltip band = AgeWidgets.Raw(window.StatsTogglesGroup);
            AgeTooltip aim = diagrams.Length == 0 ? band : AgeWidgets.Raw(diagrams[0]);
            List<NodeSection> sections = new List<NodeSection>(4);
            IList<NodeSection> aimed = GraphNodes.SectionsFor(vtable, aim);
            for (int i = 0; aimed != null && i < aimed.Count; i++)
            {
                sections.Add(aimed[i]);
            }

            if (aim == null)
            {
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
            }

            for (int i = 1; i < diagrams.Length; i++)
            {
                Add(sections, GraphNodes.ReviewedTooltipSection(AgeWidgets.Raw(diagrams[i])));
            }

            if (index == 0 && diagrams.Length != 0)
            {
                Add(sections, GraphNodes.ReviewedTooltipSection(band));
            }

            return sections.Count == 0 ? null : sections;
        }

        private static void Add(List<NodeSection> sections, NodeSection section)
        {
            if (section != null)
            {
                sections.Add(section);
            }
        }

        /// <summary>Turn the box to <paramref name="index"/>'s page the way clicking its switch does,
        /// if it is not there already and if that page's row is the one the cursor is standing on. The
        /// window's own handler is what unticks the old switch, hides its panel and shows the new one,
        /// so the page is turned by the game rather than by the mod arranging panels.</summary>
        private static void Show(AdvancedEncounterPlayModalWindow window, int index)
        {
            try
            {
                AgeControlToggle[] toggles = window.StatsToggles;
                if (toggles == null || index < 0 || index >= toggles.Length)
                {
                    return;
                }

                AgeControlToggle toggle = toggles[index];
                if (toggle == null || toggle.State || FocusedPage() != index)
                {
                    return;
                }

                AgeWidgets.Select(toggle);
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: turning to the stats page under the cursor threw: " + e);
            }
        }

        /// <summary>Which page's row the cursor is on, or -1 for anywhere else.</summary>
        private static int FocusedPage()
        {
            return ModEntry.Navigator == null ? -1 : ModEntry.Navigator.FocusedIndex(StatPageKey);
        }

        /// <summary>The four pages, as the things they say rather than as the order the prefab happens
        /// to lay its switches out in.</summary>
        private enum Stats
        {
            Trajectories,
            Military,
            Damage,
            Ranges,
        }

        /// <summary>Which page a row stands for, asked of what the window keeps in the panel rather
        /// than of where the panel sits in the array (<see cref="Panel"/>). A window that will not say
        /// reads as the page with no gauge of its own, which is the one the arena draws.</summary>
        private static Stats Which(AdvancedEncounterPlayModalWindow window, int index)
        {
            try
            {
                AgeTransform panel = Panel(window, index);
                if (panel != null && panel == AgeWidgets.Transform(window.BattlePowerGauge))
                {
                    return Stats.Military;
                }

                if (panel != null && panel == Parent(AgeWidgets.Transform(window.EnergyPowerGauge)))
                {
                    return Stats.Damage;
                }

                if (
                    panel != null
                    && panel == Parent(AgeWidgets.Transform(window.ShortRangePowerGauge))
                )
                {
                    return Stats.Ranges;
                }
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: asking which stats page a row is threw: " + e);
            }

            return Stats.Trajectories;
        }

        /// <summary>What a page is CALLED - the mod's own word for it, since the game draws four
        /// wordless switches and names none of them.</summary>
        private static string NameKey(Stats page)
        {
            switch (page)
            {
                case Stats.Military:
                    return ModStrings.BattleStatsMilitary;
                case Stats.Damage:
                    return ModStrings.BattleStatsDamage;
                case Stats.Ranges:
                    return ModStrings.BattleStatsRange;
                default:
                    return ModStrings.BattleStatsTrajectories;
            }
        }

        /// <summary>
        /// What a page says beyond its name: the figures it is drawing, one announcement PART each -
        /// which is what the page IS, since none of them writes a number anywhere.
        ///
        /// A part per figure rather than one composed sentence, because the two surfaces want
        /// different shapes of the same content: the announcer joins the parts into the one sentence
        /// the row always spoke, and the review buffer gives each part a line of its own to step
        /// through. A figure the window is not drawing answers null and contributes to neither.
        ///
        /// The trajectory page's figures are one per CURVE SLOT the arena holds, resolved at read time
        /// (<see cref="Curve"/>): the container pools its curves, so the slots are stable while which
        /// of them is drawn is not.
        ///
        /// What the other three pages' figures mean, none of which is written anywhere on them:
        ///
        /// - MILITARY is which side the arcs say is stronger and by how much, in the two fleets' own
        ///   names - the same sentence the battle popups read, off the game's own helper.
        /// - DAMAGE is the two rings' splits, NET OF DEFENCES: the window sizes each ring from what one
        ///   side's weapons of that type get through the other side's defences of that type
        ///   (<c>RefreshDamageStats</c> :398-413), which is why a ring can be all one side's - nothing
        ///   of the other's is getting through - and why the phrase says both halves rather than only
        ///   the one with something in it. The last figure is the window's own comparison BETWEEN the
        ///   types: whichever ring is drawn fatter is where the greater quantity of damage is, a fact
        ///   the picture states and neither ring's split does.
        /// - RANGE is the three rings' splits and then which range both fleets are most suited to,
        ///   which is what the window says by drawing that ring thickest (<c>RefreshRangeStats</c>
        ///   :375-396 sizes each from the two sides' average efficiency at that range, measured against
        ///   the other two).
        /// </summary>
        private static void Figures(
            NodeVtable vtable,
            AdvancedEncounterPlayModalWindow window,
            int index
        )
        {
            AdvancedEncounterPlayModalWindow it = window;
            switch (Which(window, index))
            {
                case Stats.Military:
                    Figure(vtable, () => BalanceText(it));
                    return;
                case Stats.Damage:
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Shares(
                                it.EnergyPowerGauge,
                                ModStrings.BattleEnergyDamageShare
                            )
                    );
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Shares(
                                it.PhysicalPowerGauge,
                                ModStrings.BattleProjectileDamageShare
                            )
                    );
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Thickest(
                                new[] { it.EnergyPowerGauge, it.PhysicalPowerGauge },
                                new[]
                                {
                                    ModStrings.BattleEnergyBiggerThreat,
                                    ModStrings.BattleProjectileBiggerThreat,
                                }
                            )
                    );
                    return;
                case Stats.Ranges:
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Shares(
                                it.ShortRangePowerGauge,
                                ModStrings.BattleShortRangeShare
                            )
                    );
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Shares(
                                it.MediumRangePowerGauge,
                                ModStrings.BattleMediumRangeShare
                            )
                    );
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Shares(
                                it.LongRangePowerGauge,
                                ModStrings.BattleLongRangeShare
                            )
                    );
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Thickest(
                                new[]
                                {
                                    it.ShortRangePowerGauge,
                                    it.MediumRangePowerGauge,
                                    it.LongRangePowerGauge,
                                },
                                new[]
                                {
                                    ModStrings.BattleShortRangeMatters,
                                    ModStrings.BattleMediumRangeMatters,
                                    ModStrings.BattleLongRangeMatters,
                                }
                            )
                    );
                    return;
                default:
                    AgeTransform container = Panel(window, index);
                    IList<AgeTransform> children = Children(container);
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        int at = i;
                        Figure(vtable, () => Curve(it, container, at));
                    }

                    return;
            }
        }

        /// <summary>One figure as a part of the row's reading: never watched, because a page the
        /// player is standing on redraws its arcs as the window recomputes them and a figure that
        /// announced itself under a standing cursor would talk over the plan being chosen.</summary>
        private static void Figure(NodeVtable vtable, Func<string> text)
        {
            vtable.Announcements.Add(GraphNodes.ValuePart(text, false));
        }

        /// <summary>
        /// One of the curves the arena draws for this side, as the clause it is: which flotilla the
        /// line belongs to and the range the plan has it fighting at. Null for a slot the container is
        /// not drawing a curve in.
        ///
        /// The container's own visibility is the PAGE's state and not the curve's, so the curves are
        /// asked one step (<see cref="AgeWidgets.DrawnChild"/>) rather than through the container's
        /// gate: standing on this row is what makes the container visible, and by the time these words
        /// are composed it already is. A locked flotilla still gets a curve - the game fades it rather
        /// than dropping it - so a locked one is said, because it is on the player's screen.
        /// </summary>
        private static string Curve(
            AdvancedEncounterPlayModalWindow window,
            AgeTransform container,
            int index
        )
        {
            try
            {
                AgeTransform child = AgeWidgets.DrawnChild(Children(container), index);
                EncounterPlayTrajectoryCurve curve =
                    child == null
                        ? null
                        : child.GetComponentInChildren<EncounterPlayTrajectoryCurve>();
                return curve == null ? null : Engagement(window, curve.TrajectoryIndex);
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: reading a trajectory curve threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Which page a panel is showing, asked of what the window keeps in it rather than of where it
        /// sits in the array. The three gauge pages are the panels the window's own gauge fields are
        /// drawn in - the balance gauge IS its panel, the damage and range gauges sit inside theirs -
        /// and the remaining page is the arena's trajectory container, which the window has no field
        /// for at all (measured 2026-08-29: <c>TrajectoryContainerLeft</c>, <c>PowerBalanceGroup</c>,
        /// <c>DamageGroup</c>, <c>RangeGroup</c>, in that order). Asked this way, a prefab that
        /// reorders the switches reorders the list and nothing else.
        /// </summary>
        private static AgeTransform Panel(AdvancedEncounterPlayModalWindow window, int index)
        {
            AgeTransform[] panels = window.StatsPanels;
            return panels == null || index < 0 || index >= panels.Length ? null : panels[index];
        }

        /// <summary>The diagrams a page draws, in the order it draws them - what its sentences hang
        /// on. Never null, so a caller can count them.</summary>
        private static AgeTransform[] Diagrams(
            AdvancedEncounterPlayModalWindow window,
            AgeTransform panel
        )
        {
            AgeTransform balance = AgeWidgets.Transform(window.BattlePowerGauge);
            AgeTransform energy = AgeWidgets.Transform(window.EnergyPowerGauge);
            AgeTransform physical = AgeWidgets.Transform(window.PhysicalPowerGauge);
            AgeTransform shortRange = AgeWidgets.Transform(window.ShortRangePowerGauge);
            if (panel != null && panel == balance)
            {
                return new[] { balance };
            }

            if (panel != null && panel == Parent(energy))
            {
                return new[] { energy, physical };
            }

            if (panel != null && panel == Parent(shortRange))
            {
                return new[]
                {
                    shortRange,
                    AgeWidgets.Transform(window.MediumRangePowerGauge),
                    AgeWidgets.Transform(window.LongRangePowerGauge),
                };
            }

            // The trajectory page: the arena's curves carry no sentence of their own.
            return Nothing;
        }

        private static readonly AgeTransform[] Nothing = new AgeTransform[0];

        /// <summary>Where one flotilla will fight, under the plan the window has selected: the number
        /// the game writes the flotilla down as (one-based, as every line on this window draws it) and
        /// the range in the game's own words for it - composed exactly as the card's range diagram
        /// composes its own sentence (<c>BattlePlayCardRangeIndicator.Refresh</c> :73-76), because the
        /// bare range name localizes to "Short" and what the game shows a player is "Short Range".
        /// </summary>
        private static string Engagement(AdvancedEncounterPlayModalWindow window, int index)
        {
            try
            {
                EncounterPlayDefinition play = window.SelectedPlayerPlayDefinition;
                EncounterFlotillaDefinition[] flotillas = play == null ? null : play.Flotillas;
                if (flotillas == null || index < 0 || index >= flotillas.Length)
                {
                    return null;
                }

                string range = Convert.ToString(flotillas[index].OptimalRangeName);
                if (string.IsNullOrEmpty(range))
                {
                    return null;
                }

                return OptionalText.Phrase(
                    ModStrings.BattleFlotillaRange,
                    index + 1,
                    Gui.Localize(RangeTitleKey, Gui.GetLocalizedTitle(range))
                );
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: reading a flotilla's range threw: " + e);
                return null;
            }
        }

        private static AgeTransform Parent(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string BalanceText(AdvancedEncounterPlayModalWindow window)
        {
            try
            {
                return BattleBalance.BalanceText(
                    window.PlayerEncounterGroup,
                    window.EnemyEncounterGroup,
                    true
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}
