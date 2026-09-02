using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The custom faction editor, made navigable.
    ///
    /// It is not a window of its own: the faction chooser hides its own panel and shows this one in
    /// its place (<c>FactionChoiceModalWindow.OnAddFactionCb</c> :546-550 / <c>OnEditFactionCb</c>
    /// :552-556), which is why the chooser's screen already stands down on
    /// <c>FactionChoicePanel.Shown</c> and this one takes over.
    ///
    /// Five drawn bands and a button bar, each band a Tab stop announced by the heading the game wrote
    /// on it, declared in the order they are drawn: Faction details, Starting set-up, Population,
    /// Available Traits, Selected Traits. Each of those headings is the LEVEL its band's rows sit in -
    /// spoken on the way in, never walked past, because none of them carries anything a row would have
    /// to hold. The window's own title stays a node, the way every modal's does.
    ///
    /// The three panels of settings are read off the drawn tree rather than from a list of the controls
    /// the game happens to have today (<see cref="Walk"/>): every one of them is a caption label
    /// followed by the control it captions, so the caption carries down to whatever follows it - which
    /// is how the three politics lists all read as "Politics" and are told apart by their position, the
    /// way they are on screen.
    ///
    /// The heart of it is the point budget. The game states it in three readouts under the selected
    /// traits - "Population: 45/60", "Traits: 75/95", "Count: 5/8" - and every list entry carries its
    /// own cost in its label ("Emperor's Will [95]", "Terran [15]"). None of that is the mod's
    /// arithmetic: the budget is declared as the three lines the game draws, live, so picking a trait
    /// and stepping back to them reads the new totals.
    ///
    /// A trait is picked or dropped by replaying its own click - the line's selection toggle, whose
    /// handler forwards to the table's client (<c>GuiTableLine.OnLineSelectionCb</c> :204-209 ->
    /// <c>CustomFactionTraitsSelectionPanel.OnLineSelection</c> :322-359), which is the path that
    /// enforces trait levels and prerequisites. Nothing here computes what may be picked.
    ///
    /// Escape is the game's and here it really cancels: while this panel is up the chooser routes Exit
    /// to <c>customFactionPanel.SendMessage("OnCancelCb")</c> (:119-131), which asks its own
    /// confirmation and then closes back to the faction list without touching the player's faction.
    /// Saving is <c>CustomFactionPanel.OnValidateCb</c> :686-700, which raises <c>FactionCreated</c>
    /// and then <c>CloseRequested</c>; the Create button is declared and wired to it, and the game
    /// keeps it refusing - with its reasons in its own tooltip - until the faction is complete.
    /// </summary>
    public sealed class CustomFactionScreen : Screen
    {
        private const string DetailsStop = "custom-faction:details";
        private const string SetupStop = "custom-faction:setup";
        private const string PopulationStop = "custom-faction:population";
        private const string AvailableStop = "custom-faction:available";
        private const string SelectedStop = "custom-faction:selected";
        private const string ActionsStop = "custom-faction:actions";

        private const string FiltersRegion = "custom-faction:filters";
        private const string TraitsRegion = "custom-faction:traits";
        private const string LinesRegion = "custom-faction:lines";
        private const string BudgetRegion = "custom-faction:budget";

        /// <summary>How far into a band to look for the controls it holds.</summary>
        private const int GroupDepth = 3;

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<AgeTransform> _cells = new List<AgeTransform>();

        /// <summary>The deferred keyboard hand-over for this page's text boxes.</summary>
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        /// <summary>The two traits tables, for their sort bands only: the rows are read as the whole
        /// line the game drew (<see cref="AddLines"/>) rather than cell by cell, because picking a trait
        /// is one click on the line and the three cells are what that line SAYS. The headings are a
        /// different thing - three buttons that re-sort a hundred and thirty traits by name, by level or
        /// by cost, each with the game's own sentence on it - and they are declared the way every other
        /// table on this game declares them.</summary>
        private readonly TableSheet _available = new TableSheet(
            "custom-faction:available/",
            TraitOf
        );

        private readonly TableSheet _selected = new TableSheet(
            "custom-faction:selected/",
            TraitOf
        );

        /// <summary>The first control declared this build - where focus starts.</summary>
        private ControlId _start;

        private static readonly Func<AgeTransform, AgeTransform> Itself = widget => widget;

        public override string Key
        {
            get { return "screen.custom-faction"; }
        }

        /// <summary>Just above the faction chooser it replaces - the two are halves of one window and
        /// are never up together - and below the message box its own Cancel and Reset raise.</summary>
        public override int Layer
        {
            get { return 7; }
        }

        /// <summary>The heading the game drew across the top. It is both the screen's spoken name and a
        /// node of its own at the top of the page; focus starts on the first control, so arriving does
        /// not say it twice.</summary>
        public override string ScreenName
        {
            get
            {
                AgePrimitiveLabel title = OptionsScreen.LabelIn(AgeWidgets.Transform(Panel()));
                string heading = AgeText.Label(title);
                return string.IsNullOrEmpty(heading) ? null : heading;
            }
        }

        public override bool IsActive()
        {
            try
            {
                FactionChoiceModalWindow window = Window();
                CustomFactionPanel panel = Panel();
                return window != null
                    && window.Shown
                    && window.IsReady
                    && panel != null
                    && panel.Shown
                    && AgeWidgets.Operable(window.AgeTransform)
                    && AgeWidgets.Operable(panel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape belongs to the game, and on this panel it is a real cancel: the window hands
        /// Exit to this panel's own cancel handler, which asks before throwing the work away.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void OnUpdate()
        {
            _editor.Update();
        }

        /// <summary>A text editor has been asked for and the keyboard has not changed hands yet:
        /// what the player types next is meant for the field, not for a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override void OnUnfocus()
        {
            _editor.Cancel();
        }

        public override void Build(GraphBuilder builder)
        {
            CustomFactionPanel panel = Panel();
            if (panel == null)
            {
                return;
            }

            BuildBand(builder, DetailsStop, Band(panel.DetailsPanel), "details", true);
            BuildBand(builder, SetupStop, Band(panel.StartSetupPanel), "setup", false);
            BuildBand(builder, PopulationStop, Band(panel.PopulationPanel), "population", false);
            BuildAvailable(builder, panel.TraitSelectionPanel);
            BuildSelected(builder, panel.TraitSelectionPanel);

            builder.BeginStop(ActionsStop);
            BuildActions(builder, panel);
        }

        // ---- the three bands of settings ----

        /// <summary>One drawn band: the heading the game wrote on it as the LEVEL its controls sit in,
        /// then a row per control it holds.</summary>
        private void BuildBand(
            GraphBuilder builder,
            string stop,
            AgeTransform band,
            string key,
            bool first
        )
        {
            if (!SettingRows.Drawn(band))
            {
                return;
            }

            builder.BeginStop(stop);

            // The window's own heading leads the page, once, where it is drawn - above the first band.
            // It stays a row: it is the window's title, which every modal declares as a node.
            if (first)
            {
                AddHeading(builder, OptionsScreen.LabelIn(AgeWidgets.Transform(Panel())), "custom-faction:title");
            }

            bool named = PushHeading(
                builder,
                HeadingOf(band),
                "custom-faction:" + key + "/title"
            );
            try
            {
                _start = null;
                Walk(builder, Content(band), "custom-faction:" + key + "/", GroupDepth, null, null);
                if (first && _start != null)
                {
                    // Focus starts on the first thing the player can work, not on the title that is
                    // also the screen's spoken name.
                    builder.SetStart(_start);
                }
            }
            finally
            {
                Captions.Pop(builder, named);
            }
        }

        /// <summary>
        /// The controls of a band, read off what is drawn.
        ///
        /// Every setting in this editor is written the same way - a caption label and then the control
        /// it captions, sometimes several controls sharing one caption - so a label that is not part of
        /// a control becomes the caption for whatever follows it, and a caption nothing follows is a
        /// line of text in its own right. That one rule covers the name fields, both affinity lists,
        /// the author, the description box, the home planet and government, the population's name and
        /// icon, and the two rows of three lists that the game captions once each.
        /// </summary>
        private void Walk(
            GraphBuilder builder,
            AgeTransform container,
            string key,
            int depth,
            AgePrimitiveLabel inherited,
            AgePrimitiveLabel skip
        )
        {
            _cells.Clear();
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(container);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (SettingRows.Drawn(children[i]))
                {
                    _cells.Add(children[i]);
                }
            }

            List<AgeTransform> order = new List<AgeTransform>();
            foreach (List<AgeTransform> row in AgeLayout.Rows(_cells, Itself))
            {
                order.AddRange(row);
            }

            // Two captions, deliberately: one INHERITED from the group this container sits in, which
            // applies to everything in it - the game captions "Politics" once and draws three lists
            // under it - and one PENDING from a label just passed, which belongs to the next control
            // and is spent on it.
            AgePrimitiveLabel pending = null;
            for (int i = 0; i < order.Count; i++)
            {
                AgeTransform child = order[i];
                string childKey = key + AgeWidgets.NameOf(child) + "/" + i;
                AgePrimitiveLabel caption = pending ?? inherited;

                AgeControl own = ControlOn(child);
                if (own != null)
                {
                    AddControl(builder, child, caption, childKey);
                    pending = null;
                    continue;
                }

                List<AgeControl> inside = new List<AgeControl>();
                Collect(child, inside, GroupDepth);
                if (inside.Count == 0)
                {
                    AgePrimitiveLabel label = OptionsScreen.LabelIn(child);
                    if (
                        label != null
                        && !ReferenceEquals(label, skip)
                        && !string.IsNullOrEmpty(AgeText.Label(label))
                    )
                    {
                        pending = label;
                    }

                    continue;
                }

                if (inside.Count == 1)
                {
                    AddControl(
                        builder,
                        AgeWidgets.Transform(inside[0]),
                        CaptionIn(child) ?? caption,
                        childKey
                    );
                    pending = null;
                    continue;
                }

                if (depth > 0)
                {
                    // A caption the game drew over SEVERAL controls is the LEVEL they sit in, not a
                    // word glued to the front of each of them: "Politics" is drawn once above three
                    // lists, and it is spoken once, on the way in.
                    AgePrimitiveLabel shared = CaptionIn(child);
                    bool named = PushHeading(builder, shared, childKey + "/title");
                    try
                    {
                        Walk(builder, child, childKey + "/", depth - 1, null, shared);
                    }
                    finally
                    {
                        Captions.Pop(builder, named);
                    }

                    pending = null;
                    continue;
                }

                for (int j = 0; j < inside.Count; j++)
                {
                    AddControl(
                        builder,
                        AgeWidgets.Transform(inside[j]),
                        caption ?? CaptionIn(child),
                        childKey + "/" + j
                    );
                }

                pending = null;
            }

            // A caption nothing followed is a line the game drew on its own, and it stays readable as
            // one rather than disappearing with the control it turned out not to have.
            if (pending != null)
            {
                SettingRows.AddReadout(builder, AgeWidgets.Transform(pending), key + "caption");
            }
        }

        /// <summary>One control, by what kind it is. The caption the game drew beside it is its name,
        /// and the caption's own tooltip is what the game has to say about it.</summary>
        private void AddControl(
            GraphBuilder builder,
            AgeTransform widget,
            AgePrimitiveLabel caption,
            string key
        )
        {
            if (widget == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            // Where focus starts on the page: the first control there is to work. Structural, because
            // that is all a ControlId is compared on and each kind of row mints its own.
            if (_start == null)
            {
                _start = ControlId.Structural(key);
            }

            AgePrimitiveLabel named = caption;
            Func<string> label = named == null ? null : (Func<string>)(() => AgeText.Label(named));
            AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(caption));

            AgeControlDropList list = widget.GetComponent<AgeControlDropList>();
            if (list != null)
            {
                SettingRows.AddCombo(builder, list, label, tooltip, key);
                return;
            }

            // The one-line boxes and the lore paragraph are the same kind of thing to the player and the
            // same control to the engine (AgeControlTextField : AgeControlTextArea), so both are asked
            // for at once - the derived one first, because every text field answers to both.
            AgeControlTextArea field =
                (AgeControlTextArea)widget.GetComponent<AgeControlTextField>()
                ?? widget.GetComponent<AgeControlTextArea>();
            if (field != null)
            {
                SettingRows.AddTextField(
                    builder,
                    field,
                    label,
                    tooltip,
                    null,
                    null,
                    ControlId.For(field, key),
                    _editor
                );
                return;
            }

            AgeControlToggle toggle = widget.GetComponent<AgeControlToggle>();
            if (toggle != null)
            {
                AddToggle(builder, toggle, widget, label, key);
                return;
            }

            AgeControlButton button = widget.GetComponent<AgeControlButton>();
            if (button != null)
            {
                SettingRows.AddButton(builder, button, key);
            }
        }

        /// <summary>A box the player ticks, or one of a set they pick one of - a trait, a filter. The
        /// name is whatever the game drew in it when the caller has no caption to give: a trait line
        /// spells out its own name, level and cost across its cells.</summary>
        private static void AddToggle(
            GraphBuilder builder,
            AgeControlToggle toggle,
            AgeTransform widget,
            Func<string> label,
            string key
        )
        {
            AgeControlToggle it = toggle;
            AgeTransform band = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                label ?? (() => AgeWidgets.TextOf(band)),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(band)
            );
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
            vtable.Sections = SettingRows.RowSections(band, AgeWidgets.Raw(band), dossiers);
            AgeWidgets.Point(vtable, it);
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(toggle, key), vtable, toggle),
                key,
                dossiers
            );
        }

        // ---- the traits ----

        /// <summary>One of the family filters down the side of the traits table. They are a radio group
        /// - exactly one is in force - so only the one in force says so, and focus entering the band
        /// lands on it.</summary>
        private static void AddFilter(
            GraphBuilder builder,
            AgeControlToggle toggle,
            AgeTransform widget,
            string key
        )
        {
            AgeControlToggle it = toggle;
            AgeTransform band = widget;
            NodeVtable vtable = GraphNodes.Radio(
                () => AgeWidgets.TextOf(band),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(band)
            );
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
            vtable.Sections = SettingRows.RowSections(band, AgeWidgets.Raw(band), dossiers);
            AgeWidgets.Point(vtable, it);
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(toggle, key), vtable, toggle),
                key,
                dossiers
            );
        }

        /// <summary>The traits on offer: the family filters the game lists down the side, then the
        /// table itself. Two bands, so Alt+up and Alt+down cross between them rather than walking a
        /// hundred and twenty traits to reach the filters.</summary>
        private void BuildAvailable(
            GraphBuilder builder,
            CustomFactionTraitsSelectionPanel panel
        )
        {
            GuiTable table = panel == null ? null : panel.AvailableTraits;
            AgeTransform band = BandOf(table);
            if (band == null || !SettingRows.Drawn(band))
            {
                return;
            }

            builder.BeginStop(AvailableStop);
            bool named = PushHeading(
                builder,
                HeadingOf(band),
                "custom-faction:available/title"
            );
            try
            {
                BuildAvailableContent(builder, panel, table);
            }
            finally
            {
                Captions.Pop(builder, named);
            }
        }

        private void BuildAvailableContent(
            GraphBuilder builder,
            CustomFactionTraitsSelectionPanel panel,
            GuiTable table
        )
        {
            builder.SetRegion(FiltersRegion);
            AgeTransform filters =
                panel.FiltersRadioGroup == null ? null : panel.FiltersRadioGroup.TogglesTable;
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(filters);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeControlToggle filter = children[i].GetComponent<AgeControlToggle>();
                if (filter != null && SettingRows.Drawn(children[i]))
                {
                    AddFilter(builder, filter, children[i], "custom-faction:filter/" + AgeWidgets.NameOf(children[i]));
                }
            }

            builder.SetRegion(TraitsRegion);
            _available.Headers(builder, table);
            AddLines(builder, table, "custom-faction:available/");
            builder.SetRegion(null);
        }

        /// <summary>What has been picked, and under it the three lines the game states the budget in.
        /// </summary>
        private void BuildSelected(GraphBuilder builder, CustomFactionTraitsSelectionPanel panel)
        {
            GuiTable table = panel == null ? null : panel.SelectedTraits;
            AgeTransform band = BandOf(table);
            if (band == null || !SettingRows.Drawn(band))
            {
                return;
            }

            builder.BeginStop(SelectedStop);
            bool named = PushHeading(
                builder,
                HeadingOf(band),
                "custom-faction:selected/title"
            );
            try
            {
                BuildSelectedContent(builder, panel, table);
            }
            finally
            {
                Captions.Pop(builder, named);
            }
        }

        private void BuildSelectedContent(
            GraphBuilder builder,
            CustomFactionTraitsSelectionPanel panel,
            GuiTable table
        )
        {
            builder.SetRegion(LinesRegion);
            _selected.Headers(builder, table);
            AddLines(builder, table, "custom-faction:selected/");

            // The budget, in the game's own words and the game's own arithmetic: three lines it keeps
            // current, each with its own explanation on it.
            builder.SetRegion(BudgetRegion);
            SettingRows.AddReadout(
                builder,
                AgeWidgets.Transform(panel.PopulationCostLabel),
                "custom-faction:budget/population"
            );
            SettingRows.AddReadout(
                builder,
                AgeWidgets.Transform(panel.FactionCostLabel),
                "custom-faction:budget/traits"
            );
            SettingRows.AddReadout(
                builder,
                AgeWidgets.Transform(panel.TraitCountLabel),
                "custom-faction:budget/count"
            );
            builder.SetRegion(null);
        }

        /// <summary>Every line of a traits table, in the order the table has sorted them. A line reads
        /// as the cells the game drew across it - the trait's name, its level and what it costs - and
        /// activating it replays its own click, which is what lets the game enforce levels and
        /// prerequisites rather than this screen guessing at them.</summary>
        private void AddLines(GraphBuilder builder, GuiTable table, string key)
        {
            IList<AgeTransform> lines = AgeWidgets.DrawnChildren(table == null ? null : table.LinesTable);
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                AgeControlToggle toggle = lines[i].GetComponent<AgeControlToggle>();
                if (toggle == null || !SettingRows.Drawn(lines[i]))
                {
                    continue;
                }

                AgeControlToggle it = toggle;
                AgeTransform line = lines[i];
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeWidgets.TextOf(line),
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Operable(line)
                );
                List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
                vtable.Sections = SettingRows.RowSections(line, AgeWidgets.Raw(line), dossiers);
                AgeWidgets.Point(vtable, it);
                // The trait keys the line but has no rectangle, so the slot it is currently drawn in is
                // what the viewport has to be scrolled to - a hundred and thirty traits through an
                // eighty-pixel window otherwise leave the cursor far below anything on screen.
                ScrollIntoView.Anchor(vtable, line);

                // Keyed by the TRAIT, never by the line. The table pools its lines and re-sorts them
                // on every change, so "Line082" is a slot, not a thing: after picking a trait the
                // cursor sat on the same slot, which by then held a different trait, and the next
                // Enter picked whatever had moved under it. The trait is what the player was on.
                object trait = DataOf(line);
                ControlId id = ControlId.For(trait ?? (object)toggle, key + TraitKey(trait, line));
                // A slot the game keys by the TRAIT in it has no widget in its identity, and the
                // trait is a model: what draws the row is the toggle, and only where the slot is
                // empty is that also what the row is about. Identity and evidence are two questions,
                // though, and the row has an answer to both: the trait says WHICH row this is across a
                // re-sort, and the line the table drew it in says whether it is on the screen at all -
                // a pooled slot scrolled out of the eighty-pixel window has that line to be asked
                // about, and the trait has nothing.
                TooltipChildren.Declare(builder, Nodes.Drawn(id, vtable, line), key + TraitKey(trait, line), dossiers);
            }
        }

        /// <summary>The same question the sort band's sheet asks of a line.</summary>
        private static object TraitOf(GuiTableLine line)
        {
            try
            {
                return line == null ? null : line.Data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a table line is currently showing, which is what the line IS to the player.
        /// </summary>
        private static object DataOf(AgeTransform line)
        {
            try
            {
                GuiTableLine component = line == null ? null : line.GetComponent<GuiTableLine>();
                return component == null ? null : component.Data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string TraitKey(object trait, AgeTransform line)
        {
            try
            {
                GuiFactionTrait guiTrait = trait as GuiFactionTrait;
                return guiTrait != null ? guiTrait.Name.ToString() : AgeWidgets.NameOf(line);
            }
            catch (Exception)
            {
                return AgeWidgets.NameOf(line);
            }
        }

        // ---- the bottom row ----

        private void BuildActions(GraphBuilder builder, CustomFactionPanel panel)
        {
            _cells.Clear();
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(
                AgeWidgets.Parent(AgeWidgets.Transform(panel.ValidateButton))
            );
            for (int i = 0; children != null && i < children.Count; i++)
            {
                _cells.Add(children[i]);
            }

            SettingRows.AddButtons(builder, _cells, "custom-faction:button/");
        }

        // ---- shared ----

        /// <summary>
        /// A heading the game drew, as a node of its own in reading order.
        ///
        /// This is the WINDOW's own title only. A heading over a band inside the content is that
        /// band's level instead (<see cref="PushHeading"/>): it names what is under it, it is spoken
        /// once on the way in, and none of them carries a tooltip that would have nowhere else to
        /// live (measured on every one of the seven this page draws). The window's title is the
        /// exception every modal makes - the drawn heading is a node, the screen's spoken name says
        /// the same words, and focus starts below it.
        /// </summary>
        private static void AddHeading(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(label);
            if (label == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            SettingRows.AddReadout(builder, widget, key);
        }

        /// <summary>The caption over a band, as the level its rows sit in: announced on the way in and
        /// never walked past, with a row of its own only where the game hung an explanation on it
        /// (<see cref="Captions"/>). The ALPHA gate is this panel.s own: its tables retire a row by
        /// fading it, and a faded caption is a level with nothing in it.</summary>
        private static bool PushHeading(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(label);
            return SettingRows.Drawn(widget) && Captions.Push(builder, widget, key);
        }

        private static AgePrimitiveLabel HeadingOf(AgeTransform band)
        {
            return OptionsScreen.LabelIn(band);
        }

        /// <summary>The caption drawn inside a group: its first label that is not part of a control.
        /// </summary>
        private static AgePrimitiveLabel CaptionIn(AgeTransform group)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(group);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (ControlOn(children[i]) != null)
                {
                    continue;
                }

                AgePrimitiveLabel label = children[i].GetComponent<AgePrimitiveLabel>();
                if (label != null)
                {
                    return label;
                }
            }

            return null;
        }

        /// <summary>The band a table was drawn in: its content's parent, which is where the game put
        /// the title.</summary>
        private static AgeTransform BandOf(GuiTable table)
        {
            try
            {
                return table == null ? null : AgeWidgets.Parent(AgeWidgets.Parent(table.AgeTransform));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Band(CustomFactionSubPanel panel)
        {
            try
            {
                return panel == null ? null : panel.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The band's contents - everything under its title.</summary>
        private static AgeTransform Content(AgeTransform band)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(band);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (AgeWidgets.NameOf(children[i]) == "Content")
                {
                    return children[i];
                }
            }

            return band;
        }

        /// <summary>The control a widget IS, if it is one the player works. A hover area or a scroll
        /// view is not: they are how the game draws, not what the player operates.</summary>
        private static AgeControl ControlOn(AgeTransform widget)
        {
            try
            {
                if (widget == null)
                {
                    return null;
                }

                AgeControl found = widget.GetComponent<AgeControlDropList>();
                if (found != null)
                {
                    return found;
                }

                // The base covers both: the one-line name and author boxes derive from it, and so does
                // the lore paragraph, which is a bare AgeControlTextArea and was therefore not a
                // control here at all - the walk read it as a box of text and the "Description" heading
                // above it was left standing on its own with nothing to name.
                found = widget.GetComponent<AgeControlTextArea>();
                if (found != null)
                {
                    return found;
                }

                found = widget.GetComponent<AgeControlToggle>();
                if (found != null)
                {
                    return found;
                }

                return widget.GetComponent<AgeControlButton>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Every control under a widget, stopping at each one: a drop list's own entries are
        /// its business, not more controls of the band.</summary>
        private static void Collect(AgeTransform widget, List<AgeControl> into, int depth)
        {
            if (widget == null || depth < 0 || !SettingRows.Drawn(widget))
            {
                return;
            }

            AgeControl own = ControlOn(widget);
            if (own != null)
            {
                into.Add(own);
                return;
            }

            IList<AgeTransform> children = AgeWidgets.DrawnChildren(widget);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Collect(children[i], into, depth - 1);
            }
        }

        private static CustomFactionPanel Panel()
        {
            try
            {
                FactionChoiceModalWindow window = Window();
                return window == null ? null : window.CustomFactionPanel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static FactionChoiceModalWindow Window()
        {
            return GameWindows.Of<FactionChoiceModalWindow>();
        }
    }
}
