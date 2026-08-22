using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Who lives in the empire, one people at a time: the window the senate's census button opens, and
    /// the same window the star system page's population rows open.
    ///
    /// Left column, then right: the list of peoples, each with how many of them there are; then
    /// everything the window writes about the one that is selected - what they are, what they do to a
    /// planet, what collecting more of them unlocks, what they contribute politically, and how they
    /// react to political events. The list is a set of RADIO buttons because that is what the game made
    /// them (<c>PopulationModalWindow.OnTogglePopulationAffinityFilter</c> :497-510 makes one the
    /// selection and rebinds the whole right-hand side), and the boost button beside each row is a
    /// control of its own with the game's own sentence about what it would cost and whether it can be
    /// had at all.
    ///
    /// The reaction wheel is the one place two drawings say the same thing. The game draws a column of
    /// party names beside a ring of sectors, one sector per party, and hovering either highlights the
    /// other; the sector is what carries the sentence about what this people would do to that party's
    /// support, and it carries the party's name too. So the sectors are the rows and the column beside
    /// them is not declared a second time - it is a legend for a picture, with the same six words on it.
    ///
    /// Nothing here is pressed lightly: Assimilate posts an order behind the game's own confirmation
    /// box, and boosting a people spends a luxury resource. Both are declared with the game's refusals
    /// and neither is anything but the button's own click.
    ///
    /// There is no screen name: the window's heading is declared where it is drawn and focus lands on
    /// it, which says what has just opened, once.
    /// </summary>
    public sealed class PopulationScreen : Screen
    {
        private static readonly object HeadingStop = "population:heading";
        private static readonly object ListStop = "population:list";
        private static readonly object DetailStop = "population:detail";
        private static readonly object PoliticsStop = "population:politics";
        private static readonly object ActionsStop = "population:actions";

        /// <summary>The sections of the two right-hand stops, declared whatever the selected people
        /// happens to fill, so the region jump means the same thing on every people. The captioned
        /// blocks key themselves on the prefix their lines are keyed with.</summary>
        private const string AffinityRegion = "population:detail/affinity";
        private const string AssimilateRegion = "population:detail/assimilate";
        private const string PoliticsIntroRegion = "population:politics/intro";
        private const string TraitsRegion = "population:politics/traits";
        private const string ReactionsRegion = "population:politics/reactions";

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.population"; }
        }

        /// <summary>
        /// Above everything either of its two openers can have up.
        ///
        /// It is opened from the senate, which shares 15 with the other pages the icon strip opens, AND
        /// from the star system page, which sits at 10 and can raise the planet-constructibles panel at
        /// 20 and the system-selection modal at 25. Rather than reason about which of those can be up
        /// at the same time, it takes a number above the whole 25-35 band of modals and stays under
        /// the message box its Assimilate button raises. (The notification screen sits below the
        /// modal band now - the engine draws every modal over a popup.)
        /// </summary>
        public override int Layer
        {
            get { return 36; }
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
                PopulationModalWindow window = Window();
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
            PopulationModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                BuildList(builder, window);
                BuildDetail(builder, window);
                BuildPolitics(builder, window);
                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("population: reading the window threw: " + e);
            }
        }

        private void BuildHeading(GraphBuilder builder, PopulationModalWindow window)
        {
            builder.BeginStop(HeadingStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "Title", 3),
                "population:title"
            );
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The peoples of the empire, under the caption the window draws over them - a bare
        /// word with nothing on hover (measured), so it names the list rather than standing in it. A
        /// people and the button that would favour them are two controls, walked one per step: the
        /// button is a control of its own kind and there is nothing to preserve a column of.</summary>
        private void BuildList(GraphBuilder builder, PopulationModalWindow window)
        {
            builder.BeginStop(ListStop);
            bool named = Caption(
                builder,
                AgeWidgets.ChildNamed(window.AgeTransform, "EmpirePopulationTitle", 3)
            );

            _cells.Clear();
            AgeTransform table = window.PopulationAffinityFiltersTable;
            IList<AgeTransform> rows = table == null ? null : table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                AddPeople(_cells, rows[i], i);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        /// <summary>The caption the window draws over a band, as the band's own name. A caption the
        /// game left empty pushes nothing, so nothing is announced under a blank level.</summary>
        private static bool Caption(GraphBuilder builder, AgeTransform widget)
        {
            string text =
                widget == null || !AgeWidgets.Visible(widget) ? null : AgeWidgets.TextOf(widget);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            builder.PushContext(text);
            return true;
        }

        private static void Unname(GraphBuilder builder, bool named)
        {
            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>One people: their name, how many of them there are, whether they are the one the
        /// right-hand side is describing, and - for a gene hunter - the two splicing markers. The boost
        /// button beside them is the game's own, with the game's own sentence about what it costs or how
        /// long the boost already running has left.
        ///
        /// The markers are the mod's own words because the game has none: it draws a picture for each and
        /// hangs no tooltip on either, and only a gene-hunter empire sees them at all
        /// (<c>PopulationAffinityFilter.BindGeneHunterSpecifics</c> :87-99 flips nothing but
        /// <c>Visible</c>). They read off that visibility, so a row the game marked says so and a row it
        /// did not is silent.</summary>
        private static void AddPeople(List<Cell> cells, AgeTransform widget, int index)
        {
            PopulationAffinityFilter row =
                widget == null ? null : widget.GetComponent<PopulationAffinityFilter>();
            if (row == null || row.GuiPopulation == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            PopulationAffinityFilter it = row;
            AgeTransform toggle = AgeWidgets.Transform(row.Toggle);
            if (toggle != null && AgeWidgets.Visible(toggle))
            {
                NodeVtable vtable = GraphNodes.Radio(
                    () => AgeText.Label(it.AffinityLabel),
                    () => it.Toggle != null && it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    () => AgeWidgets.Operable(toggle)
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => Count(it)));
                vtable.Announcements.Add(GraphNodes.ValuePart(() => Marker(it)));
                AgeWidgets.Point(vtable, it.Toggle);
                Cells.Add(
                    cells,
                    toggle,
                    ControlId.Referenced(widget, "population:people/" + index),
                    vtable
                );
            }

            Cells.AddControl(cells, row.PopulationBoostButton, "population:boost/" + index);
        }

        /// <summary>Whether this people has already been spliced into the empire's own, or whether there
        /// are now enough of them to splice - the two pictures a gene hunter's rows carry and nobody
        /// else's do. Never both: the game shows the "ready" marker only while the splice has not
        /// happened.</summary>
        private static string Marker(PopulationAffinityFilter row)
        {
            try
            {
                if (AgeWidgets.Visible(row.AssimilatedGroup))
                {
                    return ModStrings.Get(ModStrings.PopulationAssimilated);
                }

                return AgeWidgets.Visible(row.ReadyForAssimilationIcon)
                    ? ModStrings.Get(ModStrings.PopulationReadyForAssimilation)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Count(PopulationAffinityFilter row)
        {
            try
            {
                return row.PopulationGroup == null || !AgeWidgets.Visible(row.PopulationGroup)
                    ? null
                    : AgeText.Label(row.PopulationCountLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the window says about the selected people, in the order it is drawn: their name, the
        /// paragraph about them, the collection thresholds, then the three captioned blocks - what they
        /// do to a planet, what collecting them unlocks, what they contribute politically - and the
        /// assimilation band when the game draws one.
        ///
        /// Each captioned block is a region of its own, declared whether or not this people fills it,
        /// so the region jump means the same thing on every people; the people's own name and paragraph
        /// and the assimilation button are the two sections the game captions nothing over, and they
        /// are keyed rather than given a word the game does not draw. None of the four captions carries
        /// anything on hover (measured), so each names its block instead of standing in it.
        /// </summary>
        private void BuildDetail(GraphBuilder builder, PopulationModalWindow window)
        {
            builder.BeginStop(DetailStop);
            builder.SetRegion(AffinityRegion);
            _cells.Clear();
            Cells.AddReadout(_cells, Widget(window.AffinityTitle), "population:affinity");
            AddParagraph(_cells, window.AffinityDescription, "population:affinity-description");
            Cells.EmitLinear(builder, _cells);

            AddThresholds(builder, window);

            // One emission per captioned block. The window draws two of them SIDE BY SIDE, so laying
            // the lot out by where they are drawn read across both at once and put each caption three
            // lines away from what it captioned.
            Block(builder, Widget(window.EffectsOnPlanet), "population:planet-effects");
            Block(
                builder,
                AgeWidgets.ChildNamed(window.AgeTransform, "CollectionEffects", 5),
                "population:collection-effects"
            );
            Block(builder, Widget(window.PoliticalOpinion), "population:political-output");
            Block(builder, Widget(window.AssimilationEffects), "population:assimilation");

            builder.SetRegion(AssimilateRegion);
            _cells.Clear();
            Cells.AddControl(_cells, AgeWidgets.Transform(window.AssimilateButton), "population:assimilate");
            Cells.EmitLinear(builder, _cells);
            builder.SetRegion(null);
        }

        private void Block(GraphBuilder builder, AgeTransform group, string keyPrefix)
        {
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.SetRegion(keyPrefix);
            bool named = Caption(builder, AgeWidgets.ChildNamed(group, "Title", 1));
            _cells.Clear();
            AgeTransform table = AgeWidgets.ChildNamed(group, "EffectsTable", 4);
            IList<AgeTransform> lines = table == null ? null : table.Children;
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                Cells.AddReadout(_cells, lines[i], keyPrefix + "/" + i);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        /// <summary>How many of a people it takes to unlock each collection bonus, and what each one
        /// gives - which the window draws as a row of circles with the effect on each circle's own
        /// tooltip and no words anywhere. They are peers of one kind, so they are walked one per step
        /// rather than sideways.</summary>
        private void AddThresholds(GraphBuilder builder, PopulationModalWindow window)
        {
            AgeTransform group = AgeWidgets.ChildNamed(window.AgeTransform, "CollectionUnlockGroup", 5);
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.SetRegion("population:thresholds");
            bool named = Caption(builder, AgeWidgets.ChildNamed(group, "Title", 1));

            _cells.Clear();
            AgeTransform table = window.PopulationThresholdsTable;
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AddThreshold(_cells, items[i], i);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        /// <summary>One collection threshold. The circle says nothing in words, so its name is the
        /// first line of the sentence the game hangs on it and the rest stays in the buffer.</summary>
        private static void AddThreshold(List<Cell> cells, AgeTransform widget, int index)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform circle = AgeWidgets.ChildNamed(widget, "Circle", 2) ?? widget;
            AgeTooltip tooltip = AgeWidgets.Raw(circle);
            if (tooltip == null)
            {
                return;
            }

            AgeTransform at = circle;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => CardActions.FirstLine(AgeWidgets.Raw(at))),
                },
                Sections = GraphNodes.Sections(null, tooltip, TooltipMode.None),
            };
            AgeWidgets.PointAt(vtable, circle);
            Cells.Add(
                cells,
                circle,
                ControlId.Referenced(widget, "population:threshold/" + index),
                vtable
            );
        }

        /// <summary>
        /// How this people reacts to what happens in politics: the paragraph explaining the idea, the
        /// political traits they have, and one row per party saying what they would do to its support.
        ///
        /// The party rows are the ring's own sectors rather than the column of names beside it: the
        /// sector carries the party's name AND the sentence, and the column is the same six words drawn
        /// again as a legend. The sectors all occupy the same rectangle, so they are declared in the
        /// game's own order rather than laid out by where they are drawn.
        ///
        /// The panel's own caption names the whole stop and the traits' caption names the traits;
        /// neither carries anything on hover (measured), so neither is a node. Three regions, declared
        /// whatever this people has: the paragraph, the traits, and the ring.
        /// </summary>
        private void BuildPolitics(GraphBuilder builder, PopulationModalWindow window)
        {
            AgeTransform group = AgeWidgets.ChildNamed(window.AgeTransform, "PoliticalAffinityGroup", 3);
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.BeginStop(PoliticsStop);
            bool titled = Caption(
                builder,
                AgeWidgets.ChildNamed(group, "PoliticalAffinityTitle", 2)
            );

            builder.SetRegion(PoliticsIntroRegion);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(group, "PoliticalAffinityDescription", 2),
                "population:politics-description"
            );
            Cells.EmitLinear(builder, _cells);

            builder.SetRegion(TraitsRegion);
            bool named = Caption(builder, AgeWidgets.ChildNamed(group, "PsychoTraitsTitle", 3));
            _cells.Clear();
            AgeTransform traits = window.PsychoTraitItemsTable;
            IList<AgeTransform> items = traits == null ? null : traits.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                Cells.AddReadout(_cells, items[i], "population:trait/" + i);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);

            builder.SetRegion(ReactionsRegion);
            AgeTransform sectors = window.PoliticsFiltersContainer;
            IList<AgeTransform> wheel = sectors == null ? null : sectors.Children;
            for (int i = 0; wheel != null && i < wheel.Count; i++)
            {
                AddReaction(builder, wheel[i], i);
            }

            builder.SetRegion(null);
            Unname(builder, titled);
        }

        private static void AddReaction(GraphBuilder builder, AgeTransform widget, int index)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            PoliticsFilterSector sector = widget.GetComponent<PoliticsFilterSector>();
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            AgeTransform at = widget;
            PoliticsFilterSector it = sector;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    // Named from the party rather than off the sector's own words: a sector that
                    // reacts to a SECOND party draws that one's name inside itself too, and a row
                    // called "Scientists Industrialists" names neither.
                    GraphNodes.LabelPart(
                        () => it == null ? AgeWidgets.TextOf(at) : PartyName(it)
                    ),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(
                ControlId.Referenced(widget, "population:reaction/" + index),
                vtable
            );
        }

        private static string PartyName(PoliticsFilterSector sector)
        {
            try
            {
                return sector.GuiPolitics == null
                    ? null
                    : AgeText.Clean(Gui.GetLocalizedTitle(sector.GuiPolitics.Name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void BuildActions(GraphBuilder builder, PopulationModalWindow window)
        {
            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "CloseButton", 2),
                "population:close"
            );
            if (_cells.Count > 0)
            {
                builder.BeginStop(ActionsStop);
                Cells.EmitLinear(builder, _cells);
            }
        }

        /// <summary>A paragraph the window draws in full rather than offering on a hover: spoken whole,
        /// and walkable line by line in the review buffer.</summary>
        private static void AddParagraph(List<Cell> cells, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = Widget(label);
            if (widget == null)
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.FullLabel(it)),
                },
                Sections = GraphNodes.Sections(
                    new NodeSection(() => AgeText.Lines(AgeText.FullLabel(it)), TooltipMode.None)
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        private static AgeTransform Widget(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !AgeWidgets.Visible(label.AgeTransform)
                    ? null
                    : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Widget(GuiPanelFeature feature)
        {
            try
            {
                return feature == null ? null : feature.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PopulationModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PopulationModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }

        /// <summary>What every node this screen declares is keyed under, so the audit can
        /// tell its content from the shared heads-up display stops.</summary>
        public override string NodePrefix
        {
            get { return "population:"; }
        }
    }
}
