using System;
using System.Collections.Generic;
using System.Globalization;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

// The game has its own VictoryScreen in the global namespace; this adapts it, so the two names have to
// coexist.
using GameVictoryScreen = VictoryScreen;

namespace ES2Access.Screens
{
    /// <summary>
    /// The score screen a finished game ends on - and the same screen the journal opens for a game that
    /// finished long ago (<c>VictoryScreen</c>, bound with <c>fromJournal</c> either way).
    ///
    /// The prefab is a tab bar over a stack of panels, and neither half of that is what the player
    /// meets: the tab bar (<c>ScreenSelection.TogglesTable</c>) is drawn invisible and only
    /// <c>Panels[1]</c>, the scores panel, exists at all - the other two entries are null. So this
    /// reads the one panel, in the six blocks it draws, and the tab bar is one of the three toggle
    /// tables the bottom-button reading is told to skip. (It had to be told: two of the tables name
    /// their first child <c>Item000</c>, and the shared reading's name-plus-index key made them one
    /// control - which threw <c>Duplicate control id</c> out of every build and left the whole page
    /// declaring nothing.)
    ///
    /// Six stops, in the order the panel draws them:
    ///  1. the outcome - the window's own heading where the game hung a sentence on it, then the
    ///     paragraph of lore and the four label/value rows, under the header that says how the game
    ///     ended;
    ///  2. Game Details - the caption, which carries an explanation and so is a row as well as the
    ///     name of the block, and the four trivia tiles;
    ///  3. the empires, as the one-of-N the game made them;
    ///  4. the figures, likewise;
    ///  5. the graph, as the table it is a picture of (<see cref="BuildHistory"/>);
    ///  6. the buttons along the bottom.
    ///
    /// The game's model is select-then-act and is kept: picking an empire or a figure switches the
    /// game's own toggle, and everything else on the page - the score, the rank, the trivia, the
    /// figure list, the plotted curves - follows from the game's own rebind
    /// (<c>VictoryScreenScoresPanel.HighlightEmpire</c> :119-148, <c>SetFigure</c> :167-179). Nothing
    /// here re-derives any of it.
    ///
    /// Which way OUT is drawn depends on where the player came from: a game that just ended offers
    /// "back to menu" and a route into the journal, and a game opened FROM the journal offers a way
    /// back to it (<c>Bind</c> :71-89 sets the three buttons' visibility). All of them are read off
    /// what is drawn, so the screen does not have to know which case it is in.
    ///
    /// Layer 0, with the main menu and the new-game lobby: this is another out-of-game page that
    /// REPLACES the menu rather than floating over it - the menu is hidden while it is up and shown
    /// again when it closes (<c>BackToPreviousMenu</c>), so the two are never both live.
    ///
    /// Escape is the game's: the window answers it by going back where the player came from, menu or
    /// journal, which is a different destination in each case and one only the game knows.
    /// </summary>
    public sealed partial class VictoryScreen : Screen
    {
        private static readonly object OutcomeStop = "victory:outcome";
        private static readonly object TriviaStop = "victory:trivia";
        private static readonly object EmpiresStop = "victory:empires";
        private static readonly object FiguresStop = "victory:figures";
        private static readonly object HistoryStop = "victory:history";
        private static readonly object ActionsStop = "victory:actions";

        /// <summary>The mod's own name for the page, for the frames before the window has written its
        /// heading. Optional: a build without the phrase says nothing rather than reading the
        /// key.</summary>
        private const string ScreenNameKey = ModStrings.ScreenVictory;

        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>Portrait path to the localization key of the name it belongs to - a null ENTRY for
        /// a portrait nothing in the game's data owns, so a fruitless search is made once. Keyed on
        /// the game's own asset path rather than on the fact holding it, so the table serves every
        /// empire whose tile draws the same picture and holds nothing of a finished game alive.
        /// <see cref="Owner"/> says what the search costs.</summary>
        private readonly Dictionary<string, string> _named = new Dictionary<string, string>();

        public override string Key
        {
            get { return ModStrings.ScreenVictory; }
        }

        /// <summary>With the main menu and the lobby: an out-of-game page that replaces the menu rather
        /// than covering it.</summary>
        public override int Layer
        {
            get { return 0; }
        }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.Title(Window());
                return string.IsNullOrEmpty(title) ? OptionalText.Phrase(ScreenNameKey) : title;
            }
        }

        /// <summary>The outcome, which is what the page is about and what the eye reads first.</summary>
        public override object InitialFocusStop
        {
            get { return OutcomeStop; }
        }

        public override bool IsActive()
        {
            try
            {
                GameVictoryScreen window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: Exit goes back to wherever the player came from.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            GameVictoryScreen window = Window();
            if (window == null)
            {
                return;
            }

            VictoryScreenScoresPanel panel = Scores(window);

            builder.BeginStop(OutcomeStop);
            Outcome(builder, window, panel);

            builder.BeginStop(TriviaStop);
            Trivia(builder, panel);

            builder.BeginStop(EmpiresStop);
            Empires(builder, panel);

            builder.BeginStop(FiguresStop);
            Figures(builder, panel);

            builder.BeginStop(HistoryStop);
            BuildHistory(builder, window, panel);

            builder.BeginStop(ActionsStop);
            Actions(builder, window, panel);
        }

        // ---- 1. the outcome ----

        /// <summary>
        /// How the game ended: the header that says so, the paragraph of lore under it, and the four
        /// facts the game lists beside them.
        ///
        /// The window's own heading comes first because it is drawn first, and it is declared only
        /// where the game hung a sentence on it - the ordinary caption rule, which is also why the
        /// heading being the screen's spoken name costs nothing: the announcer drops a level whose
        /// words the node below repeats, and the stop lands past it on the paragraph.
        /// </summary>
        private void Outcome(
            GraphBuilder builder,
            GameVictoryScreen window,
            VictoryScreenScoresPanel panel
        )
        {
            Captions.Row(builder, WindowShape.TitleWidget(window), "victory:title");
            if (panel == null)
            {
                return;
            }

            // The header is the game's own sentence about the ending, and it is the name of everything
            // under it rather than a row: nothing hangs off it. Where the game has handed back its own
            // key unresolved - the %VictoryScreenPlayingPlayerTitle case, which this page has been
            // heard reading aloud once before - there is no word, so no level is opened.
            string header = Written(AgeWidgets.TextOf(AgeWidgets.Transform(panel.ScoresScreenTitle)));
            bool named = !string.IsNullOrEmpty(header);
            if (named)
            {
                builder.PushContext(header);
            }

            try
            {
                _cells.Clear();
                Lore(panel);
                ControlId landing = _cells.Count == 0 ? null : _cells[0].Id;
                DataRow(panel.VictoryTypeLabel, "victory:data/victory-type");
                DataRow(panel.DifficultyLabel, "victory:data/difficulty");
                DataRow(panel.RankLabel, "victory:data/rank");
                DataRow(panel.ScoreLabel, "victory:data/score");
                Cells.EmitLinear(builder, _cells);
                if (landing != null)
                {
                    // Arrival lands on the prose rather than on the heading above it: the heading's
                    // words are already the screen's spoken name.
                    builder.LandStopOn(landing);
                    builder.SetStart(landing);
                }
            }
            finally
            {
                if (named)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>The paragraph the game writes about how it ended - one node holding its own
        /// wrapping, and nothing at all while a game is still in progress, where the panel leaves the
        /// label empty (<c>ShowVictoryStatus</c> :171-178).</summary>
        private void Lore(VictoryScreenScoresPanel panel)
        {
            AgeTransform at = AgeWidgets.Transform(panel.LoreResumeLabel);
            if (at != null && !string.IsNullOrEmpty(Written(AgeWidgets.TextOf(at))))
            {
                Cells.AddReadout(_cells, at, "victory:lore");
            }
        }

        /// <summary>
        /// One of the four facts down the left: a caption and the value beside it, as one line.
        ///
        /// The panel exposes only the VALUE labels as fields; the caption is the label drawn beside it
        /// in the same little group, which is where the prefab puts it. The explanation - the victory
        /// condition's own description, the only one of the four the game writes one for - hangs on the
        /// value label itself (<c>BindGlobalInformation</c> :77-83), so that is what the row points at
        /// and reviews.
        /// </summary>
        private void DataRow(AgePrimitiveLabel value, string key)
        {
            AgeTransform at = AgeWidgets.Transform(value);
            if (at == null)
            {
                return;
            }

            AgeTransform row = AgeWidgets.Parent(at);
            AgeTransform caption = Beside(row, at);
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Readout(
                caption == null ? (Func<string>)(() => null) : (() => AgeWidgets.TextOf(caption)),
                () => AgeWidgets.TextOf(at),
                null,
                tooltip
            );
            Cells.Add(_cells, row ?? at, ControlId.For(at, key), vtable);
        }

        /// <summary>The label the game drew beside <paramref name="value"/> in the same group, which is
        /// what captions it. One step over the group's own children - not a search - because the two
        /// are siblings by construction.</summary>
        private static AgeTransform Beside(AgeTransform row, AgeTransform value)
        {
            try
            {
                IList<AgeTransform> children = row == null ? null : row.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child != null && !ReferenceEquals(child, value) && AgeWidgets.Says(child))
                    {
                        return child;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("victory: reading a fact's caption threw: " + e);
            }

            return null;
        }

        // ---- 2. Game Details ----

        /// <summary>The four tiles the game calls Game Details, under the caption it draws over them -
        /// which is a row of its own as well as the block's name, because the game hung a sentence on
        /// it and a name has no buffer to hold one.</summary>
        private void Trivia(GraphBuilder builder, VictoryScreenScoresPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            AgeTransform group = panel.TriviasGroup;
            // Flow control, on a wired prefab field that is always there: this is the BRANCH the panel
            // chooses on whether the selected empire kept any trivia at all (<c>HighlightEmpire</c>
            // :135-146 shows the block or puts it away), and it decides whether the caption and the
            // level under it are opened - not whether one tile exists.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            bool named = Captions.Push(
                builder,
                AgeWidgets.ChildNamed(group, "Title", 1),
                "victory:trivia/caption"
            );
            try
            {
                _cells.Clear();
                IList<OfflineEmpire.Trivia> facts = Facts(panel);
                IList<AgeTransform> tiles = AgeWidgets.DrawnChildren(panel.TriviasTable);
                for (int i = 0; tiles != null && i < tiles.Count; i++)
                {
                    Tile(AgeWidgets.DrawnChild(tiles, i), i, facts);
                }

                Cells.EmitLinear(builder, _cells);
            }
            finally
            {
                Captions.Pop(builder, named);
            }
        }

        /// <summary>
        /// One trivia tile: what it counts, WHO or WHAT it counted where the tile says that in a
        /// picture, and the number in the unit the tile draws beside it - with the sentence the game
        /// hangs on the tile in the buffer. The drawn words come off the tile's own component rather
        /// than off the widget names, which the prefab has the wrong way round - the field called
        /// <c>TitleLabel</c> is drawn in a widget named DescriptionLabel.
        ///
        /// Two of the four tiles hide half of what they say in pictures, and both halves are put back
        /// here: the portrait that names the party or the hero (<see cref="Pictured"/>) and the little
        /// symbol the number is drawn against (<see cref="Counted"/>). The other two name what they
        /// count in their own titles and draw a generic picture, so they read exactly as they always
        /// did (owner ruling 2026-09-17). Nothing else the game knows about the party or the hero is
        /// read: this page does not show it.
        /// </summary>
        private void Tile(AgeTransform tile, int index, IList<OfflineEmpire.Trivia> facts)
        {
            if (tile == null)
            {
                return;
            }

            VictoryScreenTriviaItem item = tile.GetComponent<VictoryScreenTriviaItem>();
            if (item == null)
            {
                return;
            }

            AgeTransform title = AgeWidgets.Transform(item.TitleLabel);
            AgeTransform number = AgeWidgets.Transform(item.DescriptionLabel);
            AgeTooltip tooltip = AgeWidgets.Raw(tile);
            OfflineEmpire.Trivia fact = Bound(facts, index);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(title)),
                    // A second name-kind part, so it speaks beside the title and before the figure
                    // whatever else the node grows.
                    GraphNodes.LabelPart(() => Pictured(fact)),
                    GraphNodes.ValuePart(() => Counted(fact, number)),
                },
            };
            vtable.Sections = GraphNodes.SectionsFor(vtable, tooltip);
            Cells.Add(_cells, tile, ControlId.For(tile, "victory:trivia/" + index), vtable);
        }

        /// <summary>
        /// The trivia the game bound to the tile in slot <paramref name="index"/>, or null where the
        /// selected empire kept none of that kind.
        ///
        /// The slot IS the kind: the panel makes one tile per <c>TriviaType</c> in the enum's order
        /// and binds each fact into <c>trivias[(int)trivia.Type]</c>
        /// (<c>VictoryScreenScoresPanel.CreateTrivias</c> :185-201, <c>BindTrivia</c> :197-203), so the
        /// slot is asked for the fact of its own kind rather than for the n-th fact the empire kept -
        /// which is also what makes a tile the selected empire has no fact for answer nothing instead
        /// of naming the previous empire's, since the panel leaves such a tile holding the words it
        /// last bound.
        /// </summary>
        private static OfflineEmpire.Trivia Bound(IList<OfflineEmpire.Trivia> facts, int index)
        {
            for (int i = 0; facts != null && i < facts.Count; i++)
            {
                OfflineEmpire.Trivia fact = facts[i];
                if (fact != null && (int)fact.Type == index)
                {
                    return fact;
                }
            }

            return null;
        }

        /// <summary>The facts the tiles are drawn from: the SELECTED empire's, because picking an
        /// empire is what rebinds them (<c>HighlightEmpire</c> :119-148) and the page is read as it is
        /// drawn.</summary>
        private static IList<OfflineEmpire.Trivia> Facts(VictoryScreenScoresPanel panel)
        {
            try
            {
                GuiRadioGroup group = panel.EmpireRadioGroup;
                IList<AgeTransform> toggles =
                    group == null || group.TogglesTable == null ? null : group.TogglesTable.Children;
                int at = group == null ? -1 : group.CurrentSelection;
                if (toggles == null || at < 0 || at >= toggles.Count || toggles[at] == null)
                {
                    return null;
                }

                VictoryScreenEmpireToggle toggle =
                    toggles[at].GetComponent<VictoryScreenEmpireToggle>();
                OfflineEmpire empire = toggle == null ? null : toggle.Empire;
                return empire == null ? null : empire.Trivias;
            }
            catch (Exception e)
            {
                Log.Warn("victory: finding the selected empire's trivia threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// WHO or WHAT a tile is about, where the tile says it in a picture and nowhere in words: the
        /// party the empire followed longest, and the hero it ended with. Null for the two tiles whose
        /// title already names what they counted and whose picture is the same one for every empire.
        ///
        /// The game throws the name away - the fact it saved carries the portrait's path and the
        /// portrait's model, never the name (<c>OfflineEmpire.CreateTrivias</c> :239-299) - so the name
        /// is the one the game's own picture belongs to, looked up once per portrait and localized at
        /// speak time so a language change is heard.
        /// </summary>
        private string Pictured(OfflineEmpire.Trivia fact)
        {
            bool hero;
            if (!Portrait(fact, out hero))
            {
                return null;
            }

            string title;
            if (!_named.TryGetValue(fact.ImagePath, out title))
            {
                title = Owner(fact, hero);
                _named[fact.ImagePath] = title;
            }

            return title == null ? null : AgeText.Title(title);
        }

        /// <summary>Whether a fact's picture is a PORTRAIT - a party's or a hero's - rather than the
        /// same generic symbol every empire's tile of that kind draws, and which of the two it is. Only
        /// the two portrait kinds carry a picture the name can be recovered from: the other two store
        /// the tile kind's own icon, which belongs to the tile and says nothing about the
        /// empire.</summary>
        private static bool Portrait(OfflineEmpire.Trivia fact, out bool hero)
        {
            hero = fact != null && fact.Type == OfflineEmpire.TriviaType.Hero;
            return fact != null
                && !string.IsNullOrEmpty(fact.ImagePath)
                && (hero || fact.Type == OfflineEmpire.TriviaType.Politics);
        }

        /// <summary>
        /// The localization key of the name the tile's portrait belongs to, or null where nothing in
        /// the game's data owns it.
        ///
        /// The fact copied its portrait and its model straight off ONE gui element
        /// (<c>CreateTrivias</c> :262-264 and :288-291), so the element that has them back is the one
        /// the name is on. The model is what is matched on first: it is a prefab path naming one party
        /// or one hero, and only the two portrait-bearing element kinds have one at all, where the
        /// large icon is a texture other kinds of element share - the Explorations tile's picture
        /// answers to two unrelated elements, which is why the search is confined to the element kind
        /// the tile's own kind goes with and why the icon is only the fallback for a portrait whose
        /// element was given no model.
        ///
        /// One scan of the gui-element database per portrait, and the answer is kept: the database is
        /// ten thousand elements and this is asked while a tile is read.
        /// </summary>
        private static string Owner(OfflineEmpire.Trivia fact, bool hero)
        {
            try
            {
                Amplitude.Unity.Gui.GuiElement[] all =
                    Gui.GuiElementsDatabase == null ? null : Gui.GuiElementsDatabase.GetValues();
                Amplitude.Unity.Gui.GuiElement pictured = null;
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    string model;
                    if (!Portrays(all[i], hero, out model))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(fact.ModelPath) && model == fact.ModelPath)
                    {
                        return all[i].Title;
                    }

                    if (pictured == null && Large(all[i]) == fact.ImagePath)
                    {
                        pictured = all[i];
                    }
                }

                return pictured == null ? null : pictured.Title;
            }
            catch (Exception e)
            {
                Log.Warn("victory: naming a trivia tile's picture threw: " + e);
                return null;
            }
        }

        /// <summary>Whether an element is of the kind that carries the portrait a hero or a party tile
        /// draws, and the model it names if so.</summary>
        private static bool Portrays(
            Amplitude.Unity.Gui.GuiElement element,
            bool hero,
            out string model
        )
        {
            model = null;
            if (hero)
            {
                HeroGuiElement one = element as HeroGuiElement;
                if (one == null)
                {
                    return false;
                }

                model = one.ModelPath;
                return true;
            }

            PoliticsGuiElement party = element as PoliticsGuiElement;
            if (party == null)
            {
                return false;
            }

            model = party.ModelPath;
            return true;
        }

        /// <summary>The path of the big picture an element stands for, which is the size the two
        /// portrait tiles are drawn at.</summary>
        private static string Large(Amplitude.Unity.Gui.GuiElement element)
        {
            return element.Icons == null ? null : element.Icons[Gui.ImageSize.Large];
        }

        /// <summary>
        /// The tile's number in the unit its little symbol says it is in, or the bare figure where the
        /// symbol is the tile's own picture rather than a unit.
        ///
        /// Two of the four tiles draw a number whose unit is a picture: the party tile counts TURNS
        /// and the hero tile gives a LEVEL. A count and its noun are one counted phrase in every
        /// language the mod speaks (<see cref="ES2.Speech.IconCounts"/>), and the word for a hero's
        /// level is the game's own, read from the one place the mod reads it
        /// (<see cref="HeroCards.LevelCaption"/>). A figure that is not a whole non-negative number is
        /// left exactly as it was drawn.
        /// </summary>
        private static string Counted(OfflineEmpire.Trivia fact, AgeTransform number)
        {
            string drawn = AgeWidgets.TextOf(number);
            int value;
            if (
                fact == null
                || string.IsNullOrEmpty(drawn)
                || !int.TryParse(
                    drawn.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out value
                )
            )
            {
                return drawn;
            }

            if (fact.Type == OfflineEmpire.TriviaType.Politics)
            {
                return ModStrings.Plural(
                    ModStrings.IconTurnCount,
                    ModStrings.IconTurnsCount,
                    value
                );
            }

            if (fact.Type != OfflineEmpire.TriviaType.Hero)
            {
                return drawn;
            }

            string caption = HeroCards.LevelCaption();
            return caption == null
                ? drawn
                : new MessageBuilder().Fragment(caption).Fragment(drawn).Build();
        }

        // ---- 3 and 4. the two selectors ----

        /// <summary>The empires, as the game's own one-of-N: picking one is what rebinds the whole left
        /// column and re-plots the graph.</summary>
        private void Empires(GraphBuilder builder, VictoryScreenScoresPanel panel)
        {
            GuiRadioGroup group = panel == null ? null : panel.EmpireRadioGroup;
            Toggles(builder, group == null ? null : group.TogglesTable, "victory:empire/");
        }

        /// <summary>The figures the graph can plot, likewise. The list is the game's, filtered per
        /// empire (<c>VictoryScreenGraphSection.BindFigures</c> :123-147), and each one carries the
        /// game's own sentence about what it measures.</summary>
        private void Figures(GraphBuilder builder, VictoryScreenScoresPanel panel)
        {
            VictoryScreenGraphSection graphs = panel == null ? null : panel.Graphs;
            Toggles(builder, graphs == null ? null : graphs.FigureTogglesTable, "victory:figure/");
        }

        /// <summary>One table of radio toggles, read as the set the game picks exactly one of. Both
        /// tables reserve more children than they bind and retire the surplus, so what is walked is
        /// what the game is drawing.</summary>
        private void Toggles(GraphBuilder builder, AgeTransform table, string prefix)
        {
            _cells.Clear();
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                AgeControlToggle toggle =
                    child == null ? null : AgeWidgets.Control(child) as AgeControlToggle;
                if (toggle == null)
                {
                    continue;
                }

                AgeControlToggle it = toggle;
                AgeTransform at = child;
                AgeTooltip tooltip = AgeWidgets.Raw(child);
                NodeVtable vtable = GraphNodes.Radio(
                    () => AgeWidgets.TextOf(at),
                    () => it.State,
                    // A pick, not a flip: the group's handler only ever records which member is in
                    // force, and clicking the one already on would untick it for the frames until the
                    // group writes it back (<c>GuiRadioGroup.OnToggleSwitchCb</c> :101-116).
                    () => AgeWidgets.Select(it),
                    () => AgeWidgets.Offered(at),
                    null,
                    tooltip
                );
                AgeWidgets.Point(vtable, it);
                Cells.Add(_cells, child, ControlId.For(it, prefix + i), vtable);
            }

            Cells.EmitLinear(builder, _cells);
        }

        // ---- 6. the way out ----

        /// <summary>The buttons along the bottom - which of them is drawn depends on where the player
        /// came from. The three toggle tables are excluded because they are this page's own content and
        /// are declared above as the one-of-Ns they are; the hidden tab bar is excluded because the
        /// game never shows it.</summary>
        private void Actions(
            GraphBuilder builder,
            GameVictoryScreen window,
            VictoryScreenScoresPanel panel
        )
        {
            _cells.Clear();
            VictoryScreenGraphSection graphs = panel == null ? null : panel.Graphs;
            GuiRadioGroup empires = panel == null ? null : panel.EmpireRadioGroup;
            WindowShape.Controls(
                _cells,
                window,
                "victory",
                empires == null ? null : empires.TogglesTable,
                graphs == null ? null : graphs.FigureTogglesTable,
                Tabs(window)
            );
            Cells.EmitLinear(builder, _cells);
        }

        // ---- the page's own parts ----

        /// <summary>The panel the game is showing. The prefab holds three slots and fills one: the
        /// other two are null, so this asks what is DRAWN rather than which index the tab bar - itself
        /// invisible - claims to be on.</summary>
        private static VictoryScreenScoresPanel Scores(GameVictoryScreen window)
        {
            try
            {
                VictoryScreenPanel[] panels = window.Panels;
                for (int i = 0; panels != null && i < panels.Length; i++)
                {
                    VictoryScreenScoresPanel scores = panels[i] as VictoryScreenScoresPanel;
                    // Flow control: WHICH of the window's panel slots is the page being read. The slots
                    // are shown and hidden one at a time (<c>SetPanelVisible</c> :203-221) and a panel
                    // put away keeps every word it last wrote, so this is the question of which panel
                    // the whole screen is built from rather than whether any node exists.
                    if (scores != null && AgeWidgets.Visible(AgeWidgets.Transform(scores)))
                    {
                        return scores;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("victory: finding the shown panel threw: " + e);
            }

            return null;
        }

        private static AgeTransform Tabs(GameVictoryScreen window)
        {
            try
            {
                GuiRadioGroup group = window.ScreenSelection;
                return group == null ? null : group.TogglesTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The words, or nothing where what came back is a localization key the game handed
        /// over unresolved: a page that speaks "%VictoryScreenPlayingPlayerTitle" is reading the
        /// game's own plumbing aloud, and this screen has done it before.</summary>
        private static string Written(string text)
        {
            return string.IsNullOrEmpty(text) || text[0] == '%' ? null : text;
        }

        private static GameVictoryScreen Window()
        {
            return GameWindows.Of<GameVictoryScreen>();
        }
    }
}
