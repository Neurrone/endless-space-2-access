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
        private readonly List<TooltipChildren.Dossier> _dossiers =
            new List<TooltipChildren.Dossier>();

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
                AgeWidgets.ChildNamed(window.AgeTransform, "EmpirePopulationTitle", 3),
                "population:list-title"
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

        /// <summary>The caption the window draws over a band, as the band's own name - and as a row of
        /// its own only where the game hung a sentence on it, which is the shared rule
        /// (<see cref="Captions"/>). A caption the game left empty pushes nothing, so nothing is
        /// announced under a blank level.</summary>
        private static bool Caption(
            GraphBuilder builder,
            AgeTransform widget,
            object key = null,
            AgeTransform group = null
        )
        {
            return Captions.Push(builder, widget, key, null, group);
        }

        private static void Unname(GraphBuilder builder, bool named)
        {
            Captions.Pop(builder, named);
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
            // Banding input: the cells below are laid into rows by their rectangles, and Cells.Add does
            // not ask the gate - a row the list is not drawing would band with the ones it is.
            if (row == null || row.GuiPopulation == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            PopulationAffinityFilter it = row;
            AgeTransform toggle = AgeWidgets.Transform(row.Toggle);
            // Banding input again, and the badge below hangs on the cell this adds
            // (AddBadges reaches back for cells[cells.Count - 1]).
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
                    ControlId.For(widget, "population:people/" + index),
                    vtable
                );
                AddBadges(cells, row, "population:people/" + index);
            }

            Cells.AddControl(cells, row.PopulationBoostButton, "population:boost/" + index);
        }

        /// <summary>The badge the window draws beside a people's name where they are the empire's OWN -
        /// a wordless picture with a sentence of the game's behind it. The row becomes an expandable
        /// group carrying it, the way every other badged row does; a row without the badge stays the
        /// leaf it was.
        ///
        /// It is read off the ROW rather than off the window: the list is a scroll view of its own with
        /// no parent chain back to the window's transform, so a search from there finds nothing at all
        /// (measured 2026-08-24 - the window-side search this replaces declared no node on any save).
        /// </summary>
        private static void AddBadges(List<Cell> cells, PopulationAffinityFilter row, string key)
        {
            if (cells.Count == 0)
            {
                return;
            }

            List<TooltipChildren.Dossier> badges = new List<TooltipChildren.Dossier>(1);
            TooltipChildren.AddPlain(badges, row.MajorIcon);
            if (badges.Count == 0)
            {
                return;
            }

            Cell owner = cells[cells.Count - 1];
            owner.Dossiers = badges;
            owner.Key = key;
        }

        /// <summary>Whether this people has already been spliced into the empire's own, or whether there
        /// are now enough of them to splice - the two pictures a gene hunter's rows carry and nobody
        /// else's do. Never both: the game shows the "ready" marker only while the splice has not
        /// happened.</summary>
        private static string Marker(PopulationAffinityFilter row)
        {
            try
            {
                // Content: which of the two markers the row is called by, or neither. The pictures are
                // the whole of what the game says here - it hangs no words on either.
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
                return AgeWidgets.DrawnLabel(row.PopulationGroup, row.PopulationCountLabel);
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
        /// are keyed rather than given a word the game does not draw. Each caption names its block -
        /// and keeps a row as well where the game hung a sentence on the block, which is the shared
        /// rule (<see cref="Captions"/>).
        /// </summary>
        private void BuildDetail(GraphBuilder builder, PopulationModalWindow window)
        {
            builder.BeginStop(DetailStop);
            builder.SetRegion(AffinityRegion);

            // The people's own name is the caption over everything the window then writes about them,
            // so it names the region rather than standing in it as a row that says one word.
            bool people = Caption(builder, Widget(window.AffinityTitle), "population:affinity");
            _cells.Clear();
            AddParagraph(_cells, window.AffinityDescription, "population:affinity-description");
            Cells.EmitLinear(builder, _cells);
            Unname(builder, people);

            AddThresholds(builder, window);

            // One emission per captioned block. The window draws two of them SIDE BY SIDE, so laying
            // the lot out by where they are drawn read across both at once and put each caption three
            // lines away from what it captioned.
            Block(builder, Widget(window.EffectsOnPlanet), "population:planet-effects");
            Block(
                builder,
                AgeWidgets.ChildNamed(window.AgeTransform, "CollectionEffects", 5),
                "population:collection-effects",
                true
            );
            Block(builder, Widget(window.PoliticalOpinion), "population:political-output");
            Block(builder, Widget(window.AssimilationEffects), "population:assimilation");

            builder.SetRegion(AssimilateRegion);
            _cells.Clear();
            Cells.AddControl(_cells, AgeWidgets.Transform(window.AssimilateButton), "population:assimilate");
            Cells.EmitLinear(builder, _cells);
            builder.SetRegion(null);
        }

        /// <summary>
        /// One captioned block of effect lines.
        ///
        /// The lines are the ones the table is DRAWING, not the ones it is holding: these tables are
        /// pooled, and a line the game has finished with is left in place at alpha 0 with last bind's
        /// words still on it (measured 2026-08-22: a "Militarist" line under Collection Effects for a
        /// people with no collection effects at all). Visibility alone says nothing about that - the
        /// engine's own drawing test does (<see cref="AgeWidgets.Paints"/>).
        ///
        /// <paramref name="sayEmpty"/> is for the block whose emptiness is a fact worth hearing rather
        /// than a block the game did not draw: it then reads the game's own word for having nothing
        /// (<c>%PanelFeatureNoEffectsTitle</c>), the same phrase the game writes into its own tooltips
        /// in that case.
        /// </summary>
        private void Block(
            GraphBuilder builder,
            AgeTransform group,
            string keyPrefix,
            bool sayEmpty = false
        )
        {
            // Flow control: a region, a caption and the block's whole subtree are read below, so a block
            // the window is not drawing would open a region over nothing.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.SetRegion(keyPrefix);
            // The word is on the Title label and the sentence explaining the block is on the block
            // itself, so the caption is read off both: asking the label alone left every one of
            // these panels' explanations with no surface at all.
            bool named = Caption(
                builder,
                AgeWidgets.ChildNamed(group, "Title", 1),
                keyPrefix + "/title",
                group
            );
            _cells.Clear();
            AgeTransform table = AgeWidgets.ChildNamed(group, "EffectsTable", 4);
            IList<AgeTransform> lines = table == null ? null : table.Children;
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                // Kept although each cell now carries its line: what this block SAYS turns on the
                // count below - a block with nothing drawn in it reads the game's own word for
                // having no effects - and a ghost counted here would answer that question wrongly
                // and only be taken out afterwards.
                AgeTransform line = AgeWidgets.DrawnChild(lines, i);
                if (line != null)
                {
                    Cells.AddReadout(_cells, line, keyPrefix + "/" + i);
                }
            }

            if (_cells.Count == 0 && sayEmpty)
            {
                string nothing = AgeText.Clean(Gui.Localize("%PanelFeatureNoEffectsTitle"));
                if (!string.IsNullOrEmpty(nothing) && nothing[0] != '%')
                {
                    // Synthetic: mod-authored - the game's own "no effects" wording, put where the panel
                    // drew nothing at all.
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.Structural(keyPrefix + "/none"),
                        new NodeVtable
                        {
                            Announcements = new List<NodeAnnouncement>
                            {
                                GraphNodes.LabelPart(() => nothing),
                            },
                        }
                    ));
                }
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        /// <summary>
        /// How many of a people it takes to unlock each collection bonus and which of them are already
        /// unlocked - which the window draws as a row of circles, the number on each circle, the effect
        /// on its tooltip, and the reached ones told apart from the rest by nothing but how bright the
        /// circle is (<c>ThresholdItem.Bind</c> :68 - alpha 1 reached, 0.3 not).
        ///
        /// So each circle says the number it marks and whether it has been reached, and the effect lines
        /// stay in the buffer where a walk of the whole track is not five paragraphs long. The state is
        /// read off the same arithmetic the alpha is (<c>count &gt;= threshold</c>) rather than off the
        /// alpha itself: the number is the fact and the fade is the drawing of it.
        ///
        /// The caption over the track carries the sentence saying what the track IS, so it keeps a row
        /// as well as naming the block - and that row is where the count itself goes, because the window
        /// draws the current figure nowhere in this block at all.
        /// </summary>
        private void AddThresholds(GraphBuilder builder, PopulationModalWindow window)
        {
            AgeTransform group = AgeWidgets.ChildNamed(window.AgeTransform, "CollectionUnlockGroup", 5);
            // Flow control: the region, the caption and every threshold under it are read below.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.SetRegion("population:thresholds");
            AgeTransform caption = AgeWidgets.ChildNamed(group, "Title", 1);
            bool named = Caption(builder, caption);
            AddStatus(builder, caption, group, window);

            _cells.Clear();
            AgeTransform table = window.PopulationThresholdsTable;
            IList<AgeTransform> items = table == null ? null : table.Children;
            int count = Collected(window);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AddThreshold(_cells, items[i], i, Threshold(window, i), count);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        /// <summary>
        /// The track's own caption as a row, carrying its explanation and the count the track is
        /// measuring.
        ///
        /// The game draws the WORDS on the label and hangs the SENTENCE on the group around the whole
        /// track, so the explanation has to be read - and pointed at - where it lives; asking the label
        /// alone left <c>%CollectionUnlockGroupDescription</c> with no surface at all (measured live
        /// 2026-08-22).
        /// </summary>
        private static void AddStatus(
            GraphBuilder builder,
            AgeTransform caption,
            AgeTransform group,
            PopulationModalWindow window
        )
        {
            if (caption == null)
            {
                return;
            }

            AgeTransform at = caption;
            PopulationModalWindow it = window;
            AgeTooltip own = AgeWidgets.Raw(caption);
            AgeTooltip tooltip = own ?? AgeWidgets.Raw(group);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(at)),
                    GraphNodes.ValuePart(() => Counted(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, own != null ? caption : group, tooltip);
            builder.AddItem(Nodes.Drawn(
                ControlId.For(caption, "population:collection-status"),
                vtable,
                caption
            ));
        }

        /// <summary>One collection threshold: the number of them it takes, and whether the empire has
        /// that many. The circle it is drawn as, and the effect lines it explains itself with, are the
        /// shared track's business (<see cref="ThresholdTracks"/>).</summary>
        private static void AddThreshold(
            List<Cell> cells,
            AgeTransform widget,
            int index,
            int threshold,
            int count
        )
        {
            ThresholdItem item = widget == null ? null : widget.GetComponent<ThresholdItem>();
            string drawn = item == null ? null : AgeText.Label(item.ThresholdMaxValue);
            string figure = string.IsNullOrEmpty(drawn) ? threshold.ToString() : drawn;
            bool reached = threshold > 0 && count >= threshold;
            ThresholdTracks.Add(
                cells,
                widget,
                ModStrings.Format(
                    reached
                        ? ModStrings.PopulationThresholdReached
                        : ModStrings.PopulationThresholdNotReached,
                    figure
                ),
                "population:threshold/" + index
            );
        }

        /// <summary>How many of the selected people the empire has - the same figure the list draws
        /// beside their name, read off the window's own selection.</summary>
        private static int Collected(PopulationModalWindow window)
        {
            try
            {
                return window == null || window.SelectedGuiPopulation == null
                    ? 0
                    : window.SelectedGuiPopulation.GetCount();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string Counted(PopulationModalWindow window)
        {
            int count = Collected(window);
            return count <= 0 ? null : count.ToString();
        }

        /// <summary>What the collection bonus at this place along the track asks for.</summary>
        private static int Threshold(PopulationModalWindow window, int index)
        {
            try
            {
                PopulationCollectionBonusTrait.Item[] bonuses =
                    window == null || window.SelectedGuiPopulation == null
                        ? null
                        : window.SelectedGuiPopulation.CollectionBonuses;
                return bonuses == null || index >= bonuses.Length || bonuses[index] == null
                    ? 0
                    : bonuses[index].Threshold;
            }
            catch (Exception)
            {
                return 0;
            }
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
            // Flow control: a Tab stop of its own and three regions are opened below, and the sectors,
            // the traits and the legend are each walked inside it.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.BeginStop(PoliticsStop);
            bool titled = Caption(
                builder,
                AgeWidgets.ChildNamed(group, "PoliticalAffinityTitle", 2),
                "population:politics-title"
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
            bool named = Caption(
                builder,
                AgeWidgets.ChildNamed(group, "PsychoTraitsTitle", 3),
                "population:traits-title"
            );
            _cells.Clear();
            AgeTransform traits = window.PsychoTraitItemsTable;
            IList<AgeTransform> items = traits == null ? null : traits.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                // Pooled (PopulationModalWindow.cs:420 ReserveChildren): a population with fewer
                // traits than the one shown before it leaves the surplus items faded to nothing and
                // still Visible, holding the other population's words. Each cell carries its item, so
                // those are taken out before the cells are banded (<see cref="Cells"/>).
                Cells.AddReadout(_cells, items[i], "population:trait/" + i);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);

            // The ring is what the panel's own caption is about, so the region carries that caption
            // too: jumping into it from the traits says what has been arrived at. The announcer drops
            // the level above it, which says the same words (GraphAnnouncer.DuplicatesNext), so
            // arriving at the stop still hears the phrase once.
            builder.SetRegion(ReactionsRegion);
            bool ring = Caption(
                builder,
                AgeWidgets.ChildNamed(group, "PoliticalAffinityTitle", 2)
            );
            AgeTransform sectors = window.PoliticsFiltersContainer;
            IList<AgeTransform> wheel = sectors == null ? null : sectors.Children;
            for (int i = 0; wheel != null && i < wheel.Count; i++)
            {
                AddReaction(builder, wheel[i], i);
            }

            Unname(builder, ring);
            AddLegend(builder, window);

            builder.SetRegion(null);
            Unname(builder, titled);
        }

        /// <summary>
        /// The parties' own dossiers, which the window offers on the column of names beside the ring.
        ///
        /// That column is a legend: the same six words the sectors already carry, drawn again so a
        /// mouse can hover either. What it has that the sectors do not is one renderer-assembled
        /// dossier per party - so it is read the way every other set of dossiers on a node is, as a
        /// "Tooltips" region after the rows themselves (<see cref="TooltipChildren"/>), which keeps
        /// the six sectors the primary rows of this stop.
        /// </summary>
        private void AddLegend(GraphBuilder builder, PopulationModalWindow window)
        {
            _dossiers.Clear();
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(window.PoliticsLabelsTable);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                TooltipChildren.AddInside(_dossiers, items[i]);
            }

            TooltipChildren.Emit(builder, "population:politics/parties", _dossiers, null);
        }

        private static void AddReaction(GraphBuilder builder, AgeTransform widget, int index)
        {
            if (widget == null)
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
            builder.AddItem(Nodes.Drawn(
                ControlId.For(widget, "population:reaction/" + index),
                vtable,
                widget
            ));
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
                    NodeSection.Buffer(() => AgeText.Lines(AgeText.FullLabel(it)))
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
        }

        private static AgeTransform Widget(AgePrimitiveLabel label)
        {
            try
            {
                return AgeWidgets.Drawn(label);
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
    }
}
