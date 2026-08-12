using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The bits of the game that are on the screen whatever the player is looking at: what the empire
    /// is worth along the top, the name of the view and its scan lens in the top centre, the quest the
    /// game is tracking, the notification icons and a collapsed tutorial down the right-hand edge, and
    /// the turn controls in the bottom corner.
    ///
    /// None of them belongs to a page. The galaxy, the star system's management page and a planet's
    /// overview are three different view levels of the same running game, and the game draws these
    /// clusters over all three - so a player who walked into a system could still see the End
    /// Turn button, the dust total and the tutorial bar and had no way to reach any of them. They
    /// were declared by the galaxy screen because the galaxy is where they were first met, which is
    /// not a reason for them to live there.
    ///
    /// So they are declared here and every page that is drawn under them asks for them, in the order
    /// they are drawn relative to that page's own content. The stop keys are shared on purpose: a
    /// stop's remembered cursor position is kept per screen, so the same key on three screens is
    /// three positions and not one, and naming them after the cluster rather than after the galaxy is
    /// what stops a reader of the graph believing the star system page has grown a galaxy.
    ///
    /// A stop exists on a frame only if the game is showing something at it. There are no
    /// placeholders: where the game draws nothing for having no notifications, Tab finds nothing
    /// there either, because a stop that says "nothing" is a stop the player has to walk past to
    /// learn what a glance at the screen would have told them.
    ///
    /// The turn changing is watched here for the same reason: it is the one thing on any of these
    /// pages that happens TO the player rather than being done by them, and it goes on happening
    /// wherever they are standing. The watch is instance state, so it is reload-safe by construction
    /// and each page keeps its own. The pinned quest and the MODE THE CURSOR IS IN
    /// (<see cref="AnnounceCursorMode"/>) are watched beside it, both for the same reason: they change
    /// under the player, wherever the player is.
    ///
    /// Everything is read from the game's own model rather than from the labels on the banners. Every
    /// number up there is animated - the dust total counts up to its new value over a second or so -
    /// so the labels are a picture of a number on its way somewhere, and the model is the number.
    /// </summary>
    public sealed class GlobalHud
    {
        public static readonly object EmpireStop = "hud:empire";
        public static readonly object ViewTitleStop = "hud:view-title";
        public static readonly object QuestStop = "hud:quest";
        public static readonly object TutorialStop = "hud:tutorial";
        public static readonly object NotificationStop = "hud:notifications";
        public static readonly object TurnStop = "hud:turn";

        private List<Fleet> _idleFleets = new List<Fleet>();

        private int _turn = -1;

        /// <summary>The multiplayer wait: whether the player's turn is over and the game is still on the
        /// others, and how many of them were still playing when that was last said. Instance state, like
        /// the turn watch, so each page keeps its own and a reload starts the watch over.</summary>
        private bool _waiting;

        private int _playing = -1;

        /// <summary>The two private fields of <c>EndTurnWindow</c> the turn timer is only readable from,
        /// looked up once per load rather than per frame.</summary>
        private static FieldInfo _timerEnd;

        private static FieldInfo _timerKind;

        /// <summary>The journal this page is listening to, kept so that the subscription can be given
        /// back. Instance state, so a hot reload takes it with the page.</summary>
        private QuestJournal _journal;

        /// <summary>Set by the journal's own event and drained by <see cref="Update"/>: the watcher
        /// only records that the pinned quest changed, and the per-frame pump is what speaks.</summary>
        private bool _questChanged;

        /// <summary>The instruction the game is currently showing for the cursor's mode, or null while
        /// the cursor is in no mode. Instance state, so each page keeps its own and a hot reload starts
        /// the watch over.</summary>
        private string _instruction;

        // ---- the passive watch ----

        /// <summary>Start the watch from the turn that is showing, so arriving on a page never
        /// announces a turn nobody just took. The pinned quest needs no such baseline - the game
        /// raises an event when it changes, so there is nothing to compare against. The cursor mode is
        /// baselined for the same reason as the turn: walking onto a page while a mode is already up
        /// must not announce it as though the player had just asked for it.</summary>
        public void Baseline()
        {
            _turn = Turn();
            _questChanged = false;
            _instruction = Instruction();
            _waiting = WaitingForOthers();
            _playing = PlayersPlaying(TurnWindow());
            WatchQuests();
        }

        /// <summary>Stop watching. The next arrival baselines afresh rather than comparing against
        /// however many turns passed while the player was somewhere else, and the journal gets its
        /// subscription back - the page is not there to announce anything.</summary>
        public void Forget()
        {
            _turn = -1;
            _questChanged = false;
            _instruction = null;
            _waiting = false;
            _playing = -1;
            ForgetQuests();
        }

        /// <summary>The turn ends and the next one begins on the game's schedule, not the player's -
        /// and while it does, the player is usually nowhere near the End Turn button. The same is
        /// true of the quest the game is tracking: finishing one pins the next, and of the mode the
        /// mouse cursor is in, which the game announces by writing an instruction across the screen.
        /// </summary>
        public void Update()
        {
            AnnounceTurn();
            AnnounceTurnWait();
            AnnounceQuest();
            AnnounceCursorMode();
        }

        private void AnnounceTurn()
        {
            try
            {
                int turn = Turn();
                if (turn < 0 || turn == _turn)
                {
                    return;
                }

                bool first = _turn < 0;
                _turn = turn;
                if (!first)
                {
                    Voice.Say(ModStrings.Format(ModStrings.GalaxyTurn, turn), false);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the turn threw: " + e);
            }
        }

        /// <summary>
        /// The other half of a multiplayer turn: the player has ended theirs and the game is waiting on
        /// everybody else.
        ///
        /// The game shows it by rewriting the End Turn caption to "Pending"
        /// (<c>EndTurnWindow.RefreshEndTurnLabel</c> :1123-1160) and by unlit slots on the ready ring,
        /// and nothing announces either: the turn NUMBER does not change while the wait lasts, so the
        /// turn watch above sees nothing until it is over. So the wait says itself when it starts, and
        /// each time one more player finishes - which is the only progress there is to report while the
        /// player can do nothing but listen.
        ///
        /// Gated on the ready ring, which the game draws outside single player only (:735): in a solo
        /// game the same client states are passed through on every turn and none of them is a wait.
        /// </summary>
        private void AnnounceTurnWait()
        {
            try
            {
                EndTurnWindow window = TurnWindow();
                int playing = PlayersPlaying(window);
                if (playing < 0)
                {
                    _waiting = false;
                    _playing = -1;
                    return;
                }

                bool waiting = WaitingForOthers();
                if (waiting && !_waiting)
                {
                    Voice.Say(ModStrings.Get(ModStrings.GalaxyTurnWaiting), false);
                }
                else if (waiting && playing > 0 && _playing > playing)
                {
                    Voice.Say(PlayersText(window), false);
                }

                _waiting = waiting;
                _playing = playing;
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the multiplayer wait threw: " + e);
            }
        }

        /// <summary>Whether the player's own turn is over and the game has not started the next one -
        /// the state the End Turn caption reads "Pending" in.</summary>
        private static bool WaitingForOthers()
        {
            try
            {
                return Gui.GuiGameWindowService != null
                    && Gui.GuiGameWindowService.CurrentGameClientStateType
                        == typeof(GameClientState_Turn_Finished);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- the mode the cursor is in ----

        /// <summary>
        /// The game's own instruction for the mode the mouse is in, when it changes, and a word when the
        /// mode ends.
        ///
        /// Some orders are given in two steps: pressing "launch a probe", "take this system", "fire the
        /// obliterator", "start a hacking operation" does not act - it puts the CURSOR into a mode and
        /// waits for the player to click a target. Nine cursors work this way, and the only thing on
        /// screen saying so is a line of text the game writes across the top
        /// (<c>UserInstructionsWindow</c>, shown by <c>GuiManager</c>:1552 exactly while
        /// <c>CurrentCursor.HasUserInstructions</c>). Without this, pressing such a button reads as doing
        /// nothing at all, and the player is left in a mode they cannot see.
        ///
        /// It is announced and nothing more: entering the mode says what the game says, and leaving it
        /// says that it is over. What the mode is OPERATED with is the galaxy page's business - Enter
        /// confirms at the focused node and backslash is the mode's own right click
        /// (<see cref="ES2Access.UI.CursorTargeting"/>) - and this is the one place that says the mode
        /// ended, however it ended.
        ///
        /// Watched through the WINDOW rather than through the cursor service: the window's caption is the
        /// finished, localized sentence the player would read, the game has already decided whether the
        /// mode is one worth showing (a mode with no instruction draws nothing), and it is two field
        /// reads per frame against a service subscription that would have to be given back on every page
        /// change.
        /// </summary>
        private void AnnounceCursorMode()
        {
            try
            {
                string instruction = Instruction();
                if (instruction == _instruction)
                {
                    return;
                }

                bool ended = string.IsNullOrEmpty(instruction);
                _instruction = instruction;
                Voice.Say(
                    ended ? OptionalText.Phrase(ModeEndedKey) : instruction,
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the cursor mode threw: " + e);
            }
        }

        /// <summary>The mod's own word for a mode ending, which the game marks by simply taking its
        /// instruction off the screen. Optional: a build without the phrase says nothing rather than
        /// reading the key.</summary>
        private const string ModeEndedKey = "cursor.mode-ended";

        /// <summary>What the game is instructing the player to do with the cursor, or null while it is
        /// instructing nothing. The window is hidden whenever there is no mode, so its own visibility is
        /// the whole test.</summary>
        private static string Instruction()
        {
            UserInstructionsWindow window = InstructionsWindow();
            try
            {
                return window == null || !window.Shown
                    ? null
                    : AgeText.Label(window.UserIntructionCaption);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static UserInstructionsWindow InstructionsWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<UserInstructionsWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the empire ----

        /// <summary>
        /// What the empire is worth, in the rows the corner of the screen it comes from is drawn in:
        /// the strip of icons that open the game's screens, the running totals under it, the research
        /// line under those, and the stockpiles under that.
        ///
        /// The rows are worked out from the rectangles, which is why the whole cluster is gathered
        /// before any of it is declared. Four panels contribute to it and none of them knows about
        /// the others, so where their lines fall relative to each other is a question only this can
        /// answer - and it answers it by looking.
        /// </summary>
        public void Empire(GraphBuilder builder)
        {
            GameOverlayWindow window = OverlayWindow();
            Empire empire = PlayerEmpire();
            if (window == null || empire == null)
            {
                return;
            }

            List<Cell> cells = new List<Cell>();
            AddScreenToggles(cells, window.ControlBanner);
            AddTotals(cells, window.EmpireBanner, empire);
            AddResearch(cells, window.EmpireBanner, empire);
            AddStockpiles(cells, window.StrategicsBanner);
            AddFactionPanels(cells, window);

            builder.BeginStop(EmpireStop);
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                builder.StartRow();
                foreach (Cell cell in row)
                {
                    builder.AddItem(cell.Id, cell.Vtable);
                }

                builder.EndRow();
            }
        }

        /// <summary>
        /// The two clusters the game draws across the top of every page that is drawn over a view
        /// level, in the order it draws them: what the empire is worth in the left corner, then what
        /// the player is looking at in the centre.
        ///
        /// One call rather than two, because the top of the screen is the same on every such page and
        /// the next page to be modelled should not be able to inherit half of it. A page that has to
        /// put something of its own between them can still call the two halves separately.
        /// </summary>
        public void Top(GraphBuilder builder)
        {
            Empire(builder);
            ViewTitle(builder);
        }

        /// <summary>
        /// What the player is looking at, as the game writes it across the top centre: the name of the
        /// view, and the lens that would X-ray it.
        ///
        /// The name is a BUTTON wherever there is somewhere to go back to. The game draws one widget
        /// for both jobs - a plain label on the galaxy, which is the bottom of the game, and a Close
        /// button carrying the same words on every page that is drawn over it and on every screen the
        /// icon strip opens (<c>TopTitlePanel.Setup</c>). So a keyboard player leaves a system's page
        /// the way a mouse does, by pressing the thing that says where they are.
        ///
        /// The lens is named by the game, and what it is named changes as the camera climbs: the map's
        /// zoom step picks a layer descriptor and the descriptor picks the lens, so the same button
        /// reads "Diplomacy scan" from far out and "System scan" up close. The label is read live for
        /// exactly that reason, and the game hides the whole group on the pages that have no lens.
        /// </summary>
        public void ViewTitle(GraphBuilder builder)
        {
            GameOverlayWindow window = OverlayWindow();
            TopTitlePanel panel = window == null ? null : window.TopTitlePanel;
            if (panel == null || !panel.Shown || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            // Side by side, which is how the game draws them, so left and right walk them.
            builder.BeginStop(ViewTitleStop);
            builder.StartRow();
            AddViewName(builder, panel);
            AddScanToggle(builder, panel);
            builder.EndRow();
        }

        /// <summary>The name of the view: the Close button where the game has drawn one, a plain line
        /// of text where it has not. Both carry the same words, which is why this is one node and not
        /// two.</summary>
        private static void AddViewName(GraphBuilder builder, TopTitlePanel panel)
        {
            AgeControlButton close = panel.CloseButton;
            AgeTransform button = AgeWidgets.Transform(close);
            if (button != null && AgeWidgets.Visible(button))
            {
                AgeControlButton it = close;
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(panel.CloseButtonLabel),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(button),
                    AgeWidgets.Raw(button)
                );
                AgeWidgets.Point(vtable, it);
                builder.AddItem(ControlId.Referenced(close, "hud:view-title/name"), vtable);
                return;
            }

            AgePrimitiveLabel title = panel.TitleLabel;
            if (title == null || !AgeWidgets.Visible(title.AgeTransform))
            {
                return;
            }

            NodeVtable readout = GraphNodes.Readout(
                () => AgeText.Label(title),
                () => null,
                null,
                AgeWidgets.Raw(title.AgeTransform)
            );
            AgeWidgets.PointAt(readout, title.AgeTransform);
            builder.AddItem(ControlId.Referenced(title, "hud:view-title/name"), readout);
        }

        /// <summary>The lens toggle. The tooltip explaining it is hung on the GROUP around the label
        /// and the icon rather than on the button, which is what the game shows a tooltip for and so is
        /// what the pointer is aimed at.</summary>
        private static void AddScanToggle(GraphBuilder builder, TopTitlePanel panel)
        {
            AgeTransform group = panel.ScanGroup;
            AgeControlButton button = panel.ScanButton;
            if (group == null || button == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(group);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Label(panel.ScanLabel),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(group),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, group);
            builder.AddItem(ControlId.Referenced(button, "hud:view-title/scan"), vtable);
        }

        /// <summary>A control on its way into the graph, still carrying the widget it was read from:
        /// the rows are worked out from the whole cluster at once, which cannot be done while
        /// declaring it row by row.</summary>
        private sealed class Cell
        {
            public AgeTransform Widget;
            public ControlId Id;
            public NodeVtable Vtable;
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        /// <summary>The strip of icons along the top, each of which opens one of the game's screens.
        /// The game gives them no captions at all - the name of the screen and the key that opens it
        /// are in the tooltip, which is where both are read from.</summary>
        private static void AddScreenToggles(List<Cell> cells, ControlBanner banner)
        {
            if (banner == null || banner.TogglesTable == null)
            {
                return;
            }

            try
            {
                foreach (
                    ControlBannerToggle toggle in banner.TogglesTable.GetChildren<ControlBannerToggle>(
                        false
                    )
                )
                {
                    AgeTransform widget = toggle.AgeTransform;
                    if (toggle.Screen == null || !AgeWidgets.Visible(widget))
                    {
                        continue;
                    }

                    ControlBanner strip = banner;
                    GuiScreen screen = toggle.Screen;
                    AgeTooltip tooltip = AgeWidgets.Raw(widget);
                    NodeVtable vtable = GraphNodes.Button(
                        () => ScreenTitle(screen),
                        () => strip.OnControlBannerToggle(screen),
                        () => AgeWidgets.Enabled(widget),
                        tooltip
                    );
                    AgeWidgets.PointAt(vtable, widget);
                    cells.Add(
                        new Cell
                        {
                            Widget = widget,
                            Id = ControlId.Referenced(
                                toggle,
                                "hud:empire/screen/" + screen.GetType().Name
                            ),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the screen icons threw: " + e);
            }
        }

        /// <summary>What the game calls the screen an icon opens - the same title it writes as the
        /// first line of the icon's own tooltip.</summary>
        private static string ScreenTitle(GuiScreen screen)
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle(screen.GetType().Name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The running totals the banner across the top carries.</summary>
        private static void AddTotals(List<Cell> cells, EmpireBanner banner, Empire empire)
        {
            if (banner == null)
            {
                return;
            }

            Empire it = empire;
            AddTotal(
                cells,
                banner.MoneyLabel,
                "dust",
                SimulationProperties.Empire.NetEmpireMoney,
                () => Value(it, SimulationProperties.Empire.BankAccount),
                () => Value(it, SimulationProperties.Empire.NetEmpireMoney)
            );
            AddTotal(
                cells,
                banner.ManpowerLabel,
                "manpower",
                SimulationProperties.Empire.EmpireManpower,
                () => Value(it, SimulationProperties.Empire.EmpireManpowerStock),
                () =>
                    Value(it, SimulationProperties.Empire.EmpireManpower)
                    - Value(it, SimulationProperties.Empire.EmpireManpowerUpkeep)
            );
            AddTotal(
                cells,
                banner.EmpirePointLabel,
                "influence",
                SimulationProperties.Empire.NetEmpireEmpirePoint,
                () => Value(it, SimulationProperties.Empire.EmpireEmpirePointStock),
                () => Value(it, SimulationProperties.Empire.NetEmpireEmpirePoint)
            );
        }

        /// <summary>One of the banner's running totals: what it is called, what there is of it, and
        /// what the next turn will add or take away.</summary>
        private static void AddTotal(
            List<Cell> cells,
            AgePrimitiveLabel label,
            string key,
            StaticString property,
            Func<float> stock,
            Func<float> net
        )
        {
            if (label == null || !AgeWidgets.Visible(label.AgeTransform))
            {
                return;
            }

            AgeTransform area = Area(label);
            AgeTooltip tooltip = AgeWidgets.Raw(area);
            NodeVtable vtable = GraphNodes.Readout(
                () => Gui.GetLocalizedTitle(property),
                () => StockAndNet(stock(), net(), 0),
                null,
                tooltip
            );
            AgeWidgets.PointAt(vtable, area);
            cells.Add(
                new Cell
                {
                    Widget = area,
                    Id = ControlId.Referenced(label, "hud:empire/" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>What is being researched and how long is left, or the game's own words for having
        /// queued nothing. Opening it is the banner's own click, which is what knows whether the
        /// technology screen can be reached at all.</summary>
        private static void AddResearch(List<Cell> cells, EmpireBanner banner, Empire empire)
        {
            AgeControlButton button = banner == null ? null : banner.ResearchButton;
            // The tutorial hides the whole research area until it has taught the rest, and the game
            // hides it outright for an empire that cannot research.
            if (
                button == null
                || !AgeWidgets.Visible(banner.ResearchGroup)
                || !AgeWidgets.Visible(AgeWidgets.Transform(button))
            )
            {
                return;
            }

            AgeControlButton it = button;
            Empire owner = empire;
            // The banner hangs the technology's tooltip on the line of text, not on the button - which
            // is stretched across the whole banner - so that is both what the game shows a tooltip for
            // and what it should be drawn under.
            AgeTransform line =
                banner.ResearchLabel == null
                    ? AgeWidgets.Transform(button)
                    : banner.ResearchLabel.AgeTransform;
            AgeTooltip tooltip = AgeWidgets.Raw(line);
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyResearch),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Enabled(AgeWidgets.Transform(it)),
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => ResearchText(owner)));
            AgeWidgets.Point(vtable, it, tooltip, line);
            cells.Add(
                new Cell
                {
                    Widget = AgeWidgets.Transform(it),
                    Id = ControlId.Referenced(it, "hud:empire/research"),
                    Vtable = vtable,
                }
            );
            AddResearchBuyout(cells, banner);
        }

        /// <summary>
        /// Buying the technology being researched outright, from the button the banner draws at the end
        /// of the research line.
        ///
        /// Same rule the construction queue's buy-outs follow (es2-facts): the game HIDES this button
        /// for an empire that cannot buy technology at all and otherwise leaves it drawn and switched
        /// off with the reason written into its own tooltip (<c>EmpireBanner.RefreshBuyout</c>
        /// :470-515), so DRAWN is what declares it and <c>Enable</c> is what offers it. Which currency
        /// this could be bought with, and why the answer is no today, is exactly what the player asks
        /// the banner.
        /// </summary>
        private static void AddResearchBuyout(List<Cell> cells, EmpireBanner banner)
        {
            BuyoutButton buyout = banner.BuyoutButton;
            if (buyout == null || !AgeWidgets.Visible(buyout.AgeTransform))
            {
                return;
            }

            BuyoutButton it = buyout;
            AgeTransform at = buyout.AgeTransform;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Button(
                () =>
                    ModStrings.Format(
                        ModStrings.SystemBuyOut,
                        AgeText.Clean(Gui.GetLocalizedTitle("Empire" + it.Resource))
                    ),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Offered(at),
                tooltip
            );
            // The price the button writes on itself, and only while the button is on offer: a refused
            // one carries a marker there rather than a number ("x", "-") and its tooltip already names
            // the amount that cannot be afforded.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => BuyoutCost(it, at)));
            GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Offered(at));
            AgeWidgets.PointAt(vtable, at);
            cells.Add(
                new Cell
                {
                    Widget = at,
                    Id = ControlId.Referenced(buyout, "hud:empire/research-buyout"),
                    Vtable = vtable,
                }
            );
        }

        private static string BuyoutCost(BuyoutButton buyout, AgeTransform widget)
        {
            try
            {
                return AgeWidgets.Offered(widget) && buyout.CostLabel != null
                    ? AgeText.Label(buyout.CostLabel)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ResearchText(Empire empire)
        {
            try
            {
                DepartmentOfScience science = empire.GetAgency<DepartmentOfScience>();
                Construction construction = science.ResearchQueue.Peek();
                if (construction == null)
                {
                    return AgeText.Clean("%NoResearchQueued");
                }

                TechnologyDefinition definition =
                    construction.ConstructibleElement as TechnologyDefinition;
                GuiTechnology2 technology = Gui.GuiWrapperProviderService.GetGuiTechnology2(
                    definition.Name
                );
                int turns = science.GetTechnologyRemainingTurn(definition);
                string title = technology == null ? null : AgeText.Clean(technology.Title);
                if (turns < 0 || turns == int.MaxValue)
                {
                    return title;
                }

                return new MessageBuilder()
                    .ListItem(title)
                    .ListItem(ModStrings.Format(ModStrings.GalaxyTurnsRemaining, turns))
                    .Build();
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the research line threw: " + e);
                return null;
            }
        }

        /// <summary>The strategic and luxury resources the empire holds, in the order the strip beside
        /// the banner shows them. A resource sitting at zero stays in the list - the strip dims it
        /// rather than dropping it, and "we have none of that" is the answer to the question.</summary>
        private static void AddStockpiles(List<Cell> cells, ResourcesPanel panel)
        {
            if (panel == null || panel.ResourceItemsTable == null)
            {
                return;
            }

            try
            {
                foreach (ResourceItem item in panel.ResourceItemsTable.GetChildren<ResourceItem>(false))
                {
                    GuiLocatedResource resource = item.GuiLocatedResource;
                    if (resource == null || !AgeWidgets.Visible(item.AgeTransform))
                    {
                        continue;
                    }

                    GuiLocatedResource it = resource;
                    // Small holdings of a strategic or a luxury are counted in tenths, which is how
                    // the strip itself writes them.
                    NodeVtable vtable = GraphNodes.Readout(
                        () => AgeText.Clean(it.Title),
                        () =>
                            StockAndNet(
                                it.GetStockValueFromCache(),
                                it.GetNetValueFromCache(),
                                it.GetStockValueFromCache() < 10f ? 1 : 0
                            ),
                        null,
                        item.Tooltip
                    );
                    AgeWidgets.Point(vtable, item.Button, item.Tooltip, item.AgeTransform);
                    cells.Add(
                        new Cell
                        {
                            Widget = item.AgeTransform,
                            Id = ControlId.Referenced(item, "hud:empire/resource/" + resource.Name),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the resource strip threw: " + e);
            }
        }

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

        private static void AddFactionPanels(List<Cell> cells, GameOverlayWindow window)
        {
            try
            {
                AddLifeforce(cells, window.LifeforceStatusPanel);
                AddGenes(cells, window.GeneManagementShortcutPanel);
                AddTimeBubbles(cells, window.TimeBubbleStockPanel);
                AddGoldenAge(cells, window.GoldenAgePanel);
                AddPirateMark(cells, window.PirateMarkPanel);
                AddHonor(cells, window.HonorManagementPanel);
                AddRelics(cells, window.RelicManagementPanel);
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
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform it = group;
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
                    Id = ControlId.Referenced(it, "hud:empire/" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>A button the game draws as a bare icon and names only in the sentence its tooltip
        /// opens with - the two "show me where that is" buttons, the bubble's own destroy.</summary>
        private static void AddDrawnButton(List<Cell> cells, AgeTransform widget, string key)
        {
            if (AgeWidgets.Visible(widget))
            {
                AddDrawnButton(cells, widget, ControlId.Referenced(widget, "hud:empire/" + key));
            }
        }

        private static void AddDrawnButton(List<Cell> cells, AgeTransform widget, ControlId id)
        {
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
            AddCell(cells, widget, ControlId.Referenced(widget, key), vtable);
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
        /// these panels really use (measured: MothershipCount, TempleRelics, FIDIRelics).
        /// </summary>
        private static string PropertyTitle(string property)
        {
            try
            {
                if (string.IsNullOrEmpty(property) || Gui.GetGuiElement(property) == null)
                {
                    return null;
                }

                string title = AgeText.Clean(Gui.GetLocalizedTitle(property));
                return string.IsNullOrEmpty(title) || title[0] == '%' ? null : title;
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
            return label == null || !AgeWidgets.Visible(label.AgeTransform)
                ? null
                : AgeText.Label(label);
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

        // ---- the pinned quest ----

        /// <summary>
        /// The quest the game is tracking, as the panel in the top right corner shows it: what it is
        /// called, how it is going, and what has to be done next.
        ///
        /// Three nodes at most, because the game draws three things to click: the panel itself, which
        /// opens the journal on this quest, and the two bare icons on it - the marker that takes the
        /// camera to wherever the quest is happening, and the pin that lets it go. They are drawn, so
        /// they are walked; neither is captioned, so each is named by the mod and explains itself with
        /// the game's own tooltip. An icon the game is not drawing is not a node: the marker is hidden
        /// outright for a quest with nowhere to point at, and a node that took the camera nowhere would
        /// teach the player that part of the panel is decoration.
        ///
        /// The stop is there only while the game draws the panel, which is two questions and not one:
        /// the game hides the whole window behind any full screen it opens, and it draws nothing at
        /// all while no quest is pinned. Neither state gets a placeholder - a stop saying "no quest"
        /// is a stop the player walks past to learn what a glance would have told them.
        /// </summary>
        public void Quest(GraphBuilder builder)
        {
            PinnedQuestPanel panel = QuestPanel();
            if (panel == null)
            {
                return;
            }

            PinnedQuestPanel it = panel;
            AgeTooltip hint = panel.QuestObjectiveTooltip;
            AgeControlButton open = AgeWidgets.Button(panel.AgeTransform);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.FullLabel(it.QuestTitleLabel),
                () => AgeWidgets.Press(open),
                null,
                hint
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => QuestProgress(it)));
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => AgeText.FullLabel(it.QuestObjectiveLabel))
            );
            // The panel is the thing that lights up, but the tooltip worth reading hangs off the
            // objective's own label inside it - pointing at the panel would leave the readout saying
            // "has tooltip" over a tooltip the game never drew.
            AgeWidgets.Point(
                vtable,
                open,
                hint,
                hint == null ? panel.AgeTransform : hint.AgeTransform
            );

            builder.BeginStop(QuestStop);
            builder.AddItem(ControlId.Referenced(panel.PinnedQuest, "hud:quest"), vtable);
            AddQuestButton(
                builder,
                panel.ShowLocationButton,
                ModStrings.HudQuestShowLocation,
                "hud:quest/location"
            );
            AddQuestButton(builder, panel.UnpinButton, ModStrings.HudQuestUnpin, "hud:quest/unpin");
        }

        /// <summary>One of the icons the panel draws on itself, where the game is drawing it. Drawn AND
        /// enabled: the game hides the marker for a quest with nowhere to point at without ever
        /// switching it off, so asking about enablement alone declares a control the player cannot see
        /// and the game will not act on.</summary>
        private static void AddQuestButton(
            GraphBuilder builder,
            AgeControlButton button,
            string nameKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (widget == null || !AgeWidgets.Visible(widget) || !AgeWidgets.Operable(widget))
            {
                return;
            }

            AgeControlButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () => AgeWidgets.Press(it),
                null,
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Structural(key), vtable);
        }

        /// <summary>How the quest is going, in the game's own word for it - "Ongoing", or the count of
        /// what is done out of what is needed where the objective has one. The panel hides this label
        /// outright while a quest is waiting on the player to choose between objectives.</summary>
        private static string QuestProgress(PinnedQuestPanel panel)
        {
            try
            {
                return AgeWidgets.Visible(panel.QuestProgressLabel.AgeTransform)
                    ? AgeText.FullLabel(panel.QuestProgressLabel)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Which quest the game is tracking has changed - a quest finished and the journal
        /// pinned the next one, or the player let this one go. Said wherever they are standing,
        /// because the panel is drawn over every page and nothing else reports the change.</summary>
        private void AnnounceQuest()
        {
            if (!_questChanged)
            {
                return;
            }

            _questChanged = false;
            try
            {
                Voice.Say(QuestAnnouncement(), false);
            }
            catch (Exception e)
            {
                Log.Warn("hud: announcing the pinned quest threw: " + e);
            }
        }

        /// <summary>The panel's own words where the game is drawing them, and the quest's title on its
        /// own where it is not - the journal can pin a quest while a full screen is covering the
        /// panel. Nothing pinned is its own sentence rather than an empty one.</summary>
        private string QuestAnnouncement()
        {
            PinnedQuestPanel panel = QuestPanel();
            if (panel != null)
            {
                return ModStrings.Format(
                    ModStrings.HudQuestPinned,
                    new MessageBuilder()
                        .ListItem(AgeText.FullLabel(panel.QuestTitleLabel))
                        .ListItem(QuestProgress(panel))
                        .Build()
                );
            }

            Quest quest = ActiveQuest();
            return quest == null
                ? ModStrings.Get(ModStrings.HudQuestUnpinned)
                : ModStrings.Format(
                    ModStrings.HudQuestPinned,
                    AgeText.Clean(new GuiQuest(quest).Title)
                );
        }

        /// <summary>Listen to the player empire's journal for the tracked quest changing. Subscribed
        /// when the page arrives and given back when it leaves, so the mod holds no subscription
        /// nobody is listening to and a hot reload - which pops every page - leaves none behind.
        /// </summary>
        private void WatchQuests()
        {
            ForgetQuests();
            try
            {
                Empire empire = PlayerEmpire();
                DepartmentOfInternalAffairs affairs =
                    empire == null ? null : empire.GetAgency<DepartmentOfInternalAffairs>();
                QuestJournal journal = affairs == null ? null : affairs.QuestJournal;
                if (journal == null)
                {
                    return;
                }

                _journal = journal;
                journal.ActiveQuestChange += OnActiveQuestChange;
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the quest journal threw: " + e);
            }
        }

        private void ForgetQuests()
        {
            try
            {
                if (_journal != null)
                {
                    _journal.ActiveQuestChange -= OnActiveQuestChange;
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: releasing the quest journal threw: " + e);
            }

            _journal = null;
        }

        /// <summary>Only sets state: what the change should say is worked out - and said - from the
        /// per-frame pump, which is also where the panel has finished rewriting itself.</summary>
        private void OnActiveQuestChange(object sender, QuestJournalChangeEventArgs e)
        {
            _questChanged = true;
        }

        private Quest ActiveQuest()
        {
            try
            {
                return _journal == null ? null : _journal.ActiveQuest;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The panel while the game is really showing a quest on it. Three answers have to
        /// agree: the window is up at all (the game hides it behind every full screen it opens), the
        /// panel still holds a quest (it drops it the moment it starts fading out), and nothing above
        /// it in the tree has been hidden.</summary>
        private static PinnedQuestPanel QuestPanel()
        {
            try
            {
                PinnedQuestWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PinnedQuestWindow>(false)
                    : null;
                if (window == null || !window.Shown)
                {
                    return null;
                }

                PinnedQuestPanel panel = window.PinnedQuestPanel;
                return panel != null
                    && panel.PinnedQuest != null
                    && AgeWidgets.Visible(panel.AgeTransform)
                    ? panel
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the collapsed tutorial ----

        /// <summary>The bar a collapsed tutorial leaves at the top of the right-hand edge - its title,
        /// its close button, the arrow that brings it back. The tutorial screen knows how to read it;
        /// what this decides is that it is declared by the pages that PLACE it - the galaxy and the
        /// screens the HUD's own edge belongs to - because it is one of the fixtures of that edge and
        /// not a thing that follows the keyboard onto a modal, a notification or an error box (owner
        /// decision 2026-08-12). The accepted consequence is that a tutorial minimised over a modal
        /// waits for the modal to close before it can be brought back.</summary>
        public void Tutorial(GraphBuilder builder)
        {
            builder.BeginStop(TutorialStop);
            TutorialScreen.BuildCollapsedBar(builder);
        }

        // ---- notifications ----

        /// <summary>
        /// Everything the game is waiting to tell the player, as a list they can walk instead of a
        /// column of icons they would have to click. Enter opens one - the popup that appears is a
        /// screen of ours and takes over from here - and Backspace throws it away. With nothing
        /// waiting the game shows an empty corner, so this stop is not there at all.
        ///
        /// What a stop here holds is what the strip holds: an icon and, on hovering it, its title.
        /// Not the notification's description - the game does not show that until the popup is opened,
        /// and opening it is what this stop's Enter is for. Putting the whole text in the buffer here
        /// made the strip a second place to read the message, one that answered before the player had
        /// asked and disagreed with the screen as drawn.
        /// </summary>
        public void Notifications(GraphBuilder builder)
        {
            builder.BeginStop(NotificationStop);
            int count = 0;
            try
            {
                IGuiNotificationService service = Gui.GuiNotificationService;
                if (service == null)
                {
                    return;
                }

                NotificationItem[] items = NotificationItems();
                foreach (GuiNotification notification in service.GetPlayerEmpireGuiNotifications())
                {
                    GuiNotification it = notification;
                    NodeVtable vtable = GraphNodes.Button(
                        () => AgeText.Clean(it.GetTitle()),
                        () => Open(it),
                        null,
                        null
                    );
                    vtable.OnSecondary = () => Dismiss(it);
                    vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipDetails(IconTooltip(it, items)), null);
                    builder.AddItem(ControlId.Referenced(it, "hud:notification/" + count), vtable);
                    count++;
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the notifications threw: " + e);
            }
        }

        /// <summary>The tooltip the strip hangs on this notification's icon - read from the icon
        /// rather than composed from the notification, so it stays whatever the game decides to put
        /// there. Today the game binds it to the notification's title, and the buffer drops a first
        /// line that only repeats the control's name, so the usual result is a buffer holding exactly
        /// the one line the strip shows.</summary>
        private static AgeTooltip IconTooltip(GuiNotification notification, NotificationItem[] items)
        {
            try
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (ReferenceEquals(items[i].GuiNotification, notification))
                    {
                        return items[i].Tootlip;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: matching a notification to its icon threw: " + e);
            }

            return null;
        }

        private static readonly NotificationItem[] NoItems = new NotificationItem[0];

        private static NotificationItem[] NotificationItems()
        {
            try
            {
                NotificationItemsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<NotificationItemsWindow>(false)
                    : null;
                return window == null
                    ? NoItems
                    : window.GetComponentsInChildren<NotificationItem>(true);
            }
            catch (Exception e)
            {
                Log.Warn("hud: finding the notification icons threw: " + e);
                return NoItems;
            }
        }

        private static void Open(GuiNotification notification)
        {
            try
            {
                Gui.GuiNotificationService.ToggleGuiNotification(notification);
            }
            catch (Exception e)
            {
                Log.Warn("hud: opening a notification threw: " + e);
            }
        }

        /// <summary>Throw a notification away. One the game will not let go of stays, silently: the
        /// key simply did nothing, which is what a key that does not apply here should do.</summary>
        private static void Dismiss(GuiNotification notification)
        {
            try
            {
                if (notification.IsDismissible)
                {
                    Gui.GuiNotificationService.DismissGuiNotification(notification);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: dismissing a notification threw: " + e);
            }
        }

        // ---- the turn ----

        /// <summary>What the turn itself offers: end it, move everything that was told to move, walk
        /// to the next fleet with nothing to do, and open the game menu.</summary>
        public void Turn(GraphBuilder builder)
        {
            EndTurnWindow window = TurnWindow();
            if (window == null || !AgeWidgets.Visible(window.AgeTransform))
            {
                return;
            }

            List<Cell> found = new List<Cell>();
            EndTurnWindow it = window;
            AgeControlButton endTurn = window.EndTurnButton;
            if (AgeWidgets.Visible(AgeWidgets.Transform(endTurn)))
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => EndTurnLabel(it),
                    () => AgeWidgets.Press(endTurn),
                    () => CanEndTurn(it)
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => TurnText(it)));
                vtable.Sections = GraphNodes.Sections(() => EndTurnReason(it), null);
                AgeWidgets.Point(vtable, endTurn);
                found.Add(
                    new Cell
                    {
                        Widget = AgeWidgets.Transform(endTurn),
                        Id = ControlId.Referenced(endTurn, "hud:end-turn"),
                        Vtable = vtable,
                    }
                );
            }

            AddTurnButton(found, window.ApplyMovementsButton, "apply-movements", ModStrings.GalaxyApplyMovements, null);
            AddTurnButton(found, window.NextIdleFleetButton, "next-idle-fleet", ModStrings.GalaxyNextIdleFleet, IdleFleetsText);
            AddTurnButton(found, window.GameMenuButton, "game-menu", ModStrings.GalaxyGameMenu, null);
            AddRequestToggle(found, window.RequestToggle);
            AddSync(found, window);
            AddPlayers(found, window);
            AddTimers(found, window);

            builder.BeginStop(TurnStop);
            builder.StartRow();
            List<ControlId> ids = new List<ControlId>();
            for (int i = 0; i < found.Count; i++)
            {
                ids.Add(found[i].Id);
                builder.AddItem(found[i].Id, found[i].Vtable);
            }

            builder.EndRow();

            // The controls sit in a cluster rather than along one axis, so up and down walk them too
            // and nobody has to guess which way they are laid out.
            for (int i = 1; i < ids.Count; i++)
            {
                builder.Connect(ids[i - 1], GraphDir.Down, ids[i]);
                builder.Connect(ids[i], GraphDir.Up, ids[i - 1]);
            }
        }

        private void AddTurnButton(
            List<Cell> found,
            AgeControlButton button,
            string key,
            string nameKey,
            Func<string> value
        )
        {
            if (!AgeWidgets.Visible(AgeWidgets.Transform(button)))
            {
                return;
            }

            AgeControlButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Enabled(AgeWidgets.Transform(it)),
                AgeWidgets.Readable(AgeWidgets.Raw(AgeWidgets.Transform(it)))
            );
            if (value != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(value));
            }

            AgeWidgets.Point(vtable, it);
            found.Add(
                new Cell
                {
                    Widget = AgeWidgets.Transform(it),
                    Id = ControlId.Referenced(it, "hud:" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The switch tucked in beside the turn controls that shows what an ALLIANCE is coordinating: the
        /// requests allies pin on the map, and the panel they are sent from - the game opens the list and
        /// flips ping visibility together on one click
        /// (<c>EndTurnWindow.OnToggleRequestCb</c> :1337-1354).
        ///
        /// It is drawn on every game and switched off for an empire in no alliance, with the game's own
        /// sentence for why on its tooltip (<c>RequestToggleTooltipContent</c> :555-570) - which is the
        /// whole reason to declare it while it refuses: a control nobody can find is a feature nobody
        /// knows exists. The game writes no caption for it anywhere (a bare icon, whose tooltip is a
        /// sentence about what a click would do rather than a name), so the name is the mod's.
        /// </summary>
        private void AddRequestToggle(List<Cell> found, AgeControlToggle toggle)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (!AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(at);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => ModStrings.Get(ModStrings.GalaxyAllianceRequests),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                enabled,
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.Point(vtable, it, tooltip, widget);
            found.Add(
                new Cell
                {
                    Widget = widget,
                    Id = ControlId.Referenced(toggle, "hud:alliance-requests"),
                    Vtable = vtable,
                }
            );
        }

        // ---- the multiplayer half of the turn cluster ----

        /// <summary>
        /// Whether the game is still in step with the other players, and the host's way out when it is
        /// not.
        ///
        /// The game draws the state as a tinted icon and puts the whole of its meaning on a tooltip
        /// (<c>EndTurnWindow.RefreshSyncState</c> :1254-1269 hangs the <c>SyncStatus&lt;state&gt;</c>
        /// element's description there), so the sentence is what this row SAYS rather than something
        /// hanging off it. The group is drawn only outside single player (:734), which is what keeps
        /// every line here absent from a solo game.
        ///
        /// The button beside it returns everybody to the lobby to reload the last auto-save
        /// (<c>OnDesyncStatusClickCb</c> :1318-1321) and is switched on only for the host, and only on a
        /// checksum mismatch - so it is declared while refusing, like every other button the mod
        /// declares: knowing the way out exists is the point.
        /// </summary>
        private void AddSync(List<Cell> found, EndTurnWindow window)
        {
            AgeTransform group = window.SyncGroup;
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            EndTurnWindow it = window;
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.GalaxySyncState),
                () => SyncText(it),
                null,
                null
            );
            AgeWidgets.PointAt(vtable, group);
            found.Add(
                new Cell
                {
                    Widget = group,
                    Id = ControlId.Referenced(group, "hud:sync"),
                    Vtable = vtable,
                }
            );

            AddTurnButton(found, window.DesyncButton, "desync", ModStrings.GalaxyReturnToLobby, null);
        }

        /// <summary>
        /// Where the other players are in their turn: how many are still playing, and a line each for
        /// what the game says about them.
        ///
        /// Read off the ring of slots the game draws around the End Turn button - which is drawn in
        /// multiplayer only (:735) and, unlike the players list, is NOT gated on where the mouse is
        /// (<c>EndTurnWindow.SpecificUpdate</c> :906-921 shows that list only while the physical cursor
        /// is inside the button, and the mod moves no cursor). Each slot already carries the game's own
        /// sentence about its player - leader and faction, then the state word
        /// (<c>CompetitorOrbitalSlot.Refresh</c> :45-68) - so nothing here recomputes a player state.
        ///
        /// One row rather than one per player: the cluster is a handful of buttons in the corner of the
        /// screen, and eight more stops in it would be walked past on every pass. The per-player lines
        /// are the row's reviewable content.
        /// </summary>
        private void AddPlayers(List<Cell> found, EndTurnWindow window)
        {
            AgeTransform ring = window.CompetitorsCircularTable;
            if (!AgeWidgets.Visible(ring))
            {
                return;
            }

            EndTurnWindow it = window;
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.GalaxyPlayers),
                () => PlayersText(it),
                () => PlayerLines(it),
                null,
                // The count changes as players end their turn, and the watch below is what announces
                // that wherever the player is standing; a watched value would say it twice here.
                false
            );
            found.Add(
                new Cell
                {
                    Widget = ring,
                    Id = ControlId.Referenced(ring, "hud:players"),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The clocks a multiplayer game can be running: the whole game's, which the game writes as a
        /// label, and the current turn's, which it draws as arcs around the End Turn button with no
        /// number written anywhere.
        ///
        /// Neither value is watched. Both change every second, and a value that re-announces itself
        /// under the cursor would talk over everything else the player is doing; asked for, they are
        /// current.
        /// </summary>
        private void AddTimers(List<Cell> found, EndTurnWindow window)
        {
            EndTurnWindow it = window;
            AgeTransform global = window.GlobalTimerLabel == null
                ? null
                : window.GlobalTimerLabel.AgeTransform;
            if (AgeWidgets.Visible(global))
            {
                NodeVtable vtable = GraphNodes.Readout(
                    () => ModStrings.Get(ModStrings.GalaxyGlobalTimer),
                    () => OneLine(AgeText.Label(it.GlobalTimerLabel)),
                    null,
                    null,
                    false
                );
                found.Add(
                    new Cell
                    {
                        Widget = global,
                        Id = ControlId.Referenced(global, "hud:global-timer"),
                        Vtable = vtable,
                    }
                );
            }

            AgeTransform arc = window.CommonTimerArc == null
                ? null
                : window.CommonTimerArc.AgeTransform;
            if (arc == null || TimerSeconds(window) < 0)
            {
                return;
            }

            NodeVtable turnTimer = GraphNodes.Readout(
                () => ModStrings.Get(TimerNameKey(it)),
                () => ModStrings.Format(ModStrings.GalaxyTimerSeconds, TimerSeconds(it)),
                null,
                null,
                false
            );
            found.Add(
                new Cell
                {
                    Widget = arc,
                    Id = ControlId.Referenced(arc, "hud:turn-timer"),
                    Vtable = turnTimer,
                }
            );
        }

        /// <summary>What the game says about the synchronization state - the tooltip's sentence, which
        /// is the only words there are for it.</summary>
        private static string SyncText(EndTurnWindow window)
        {
            try
            {
                return OneLine(AgeText.Tooltip(window.SyncTooltip));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How many players have not ended their turn, counted the way the game counts them:
        /// the slots of the ready ring whose unready icon is showing (<c>EndTurnWindow.Refresh</c>
        /// :857-880). -1 when there is no ring, which is every single-player game.</summary>
        private static int PlayersPlaying(EndTurnWindow window)
        {
            try
            {
                AgeTransform ring = window == null ? null : window.CompetitorsCircularTable;
                if (!AgeWidgets.Visible(ring))
                {
                    return -1;
                }

                IList<AgeTransform> slots = ring.Children;
                int playing = 0;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    CompetitorOrbitalSlot slot = Slot(slots[i]);
                    if (slot != null && slot.UnreadyIcon != null && slot.UnreadyIcon.Visible)
                    {
                        playing++;
                    }
                }

                return playing;
            }
            catch (Exception e)
            {
                Log.Warn("hud: counting the players still playing threw: " + e);
                return -1;
            }
        }

        private static string PlayersText(EndTurnWindow window)
        {
            int playing = PlayersPlaying(window);
            if (playing < 0)
            {
                return null;
            }

            return playing == 0
                ? ModStrings.Get(ModStrings.GalaxyPlayersAllReady)
                : ModStrings.Plural(
                    ModStrings.GalaxyPlayerPlaying,
                    ModStrings.GalaxyPlayersPlaying,
                    playing
                );
        }

        /// <summary>A line per player, in the game's own words: leader and faction, then where they are
        /// in their turn - and, for a human who is not the local player, the whisper instruction the
        /// game appends to the same tooltip, which is reviewable rather than spoken.</summary>
        private static IList<string> PlayerLines(EndTurnWindow window)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform ring = window == null ? null : window.CompetitorsCircularTable;
                if (!AgeWidgets.Visible(ring))
                {
                    return lines;
                }

                IList<AgeTransform> slots = ring.Children;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    CompetitorOrbitalSlot slot = Slot(slots[i]);
                    if (slot == null)
                    {
                        continue;
                    }

                    foreach (string line in AgeText.Lines(AgeText.Tooltip(slot.Tooltip)))
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the player states threw: " + e);
            }

            return lines;
        }

        private static CompetitorOrbitalSlot Slot(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<CompetitorOrbitalSlot>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// How long the running turn timer has left, in whole seconds, or -1 while no timer is running.
        ///
        /// The window draws the three timers as ARCS with no number on them and keeps the end time and
        /// the kind of timer in private fields (:157-163, written from the timer service's own event
        /// :1520-1530), so there is nothing on screen to read and the fields are the only source. The
        /// same expression the window uses: end time minus the game's clock (:1071).
        /// </summary>
        private static int TimerSeconds(EndTurnWindow window)
        {
            try
            {
                if (window == null || TimerKind(window) == GameTimerType.None)
                {
                    return -1;
                }

                FieldInfo field = TimerField("currentTimerEndTime", ref _timerEnd);
                if (field == null)
                {
                    return -1;
                }

                double left = (double)field.GetValue(window) - global::Game.Time;
                return left <= 0.0 ? -1 : (int)left;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>Which of the three clocks is running, so the row can name itself: the turn's own
        /// timer, the overtime the previous turns banked, or the shortened one the last player left in
        /// the turn is given.</summary>
        private static GameTimerType TimerKind(EndTurnWindow window)
        {
            try
            {
                FieldInfo field = TimerField("currentTimerType", ref _timerKind);
                return field == null
                    ? GameTimerType.None
                    : (GameTimerType)field.GetValue(window);
            }
            catch (Exception)
            {
                return GameTimerType.None;
            }
        }

        private static string TimerNameKey(EndTurnWindow window)
        {
            switch (TimerKind(window))
            {
                case GameTimerType.Overtime:
                    return ModStrings.GalaxyOvertimeTimer;
                case GameTimerType.LastPlayer:
                    return ModStrings.GalaxyLastPlayerTimer;
                default:
                    return ModStrings.GalaxyTurnTimer;
            }
        }

        private static FieldInfo TimerField(string name, ref FieldInfo cache)
        {
            if (cache != null)
            {
                return cache;
            }

            try
            {
                cache = typeof(EndTurnWindow).GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
            }
            catch (Exception)
            {
                cache = null;
            }

            return cache;
        }

        /// <summary>The button's own caption, which the game writes over two lines and rewrites while
        /// a turn is being processed - so it says what the button is doing, not only what it is.
        /// </summary>
        private static string EndTurnLabel(EndTurnWindow window)
        {
            string caption = OneLine(AgeText.Label(window.EndTurnTitle));
            return string.IsNullOrEmpty(caption)
                ? ModStrings.Get(ModStrings.GalaxyEndTurn)
                : caption;
        }

        /// <summary>Which turn it is. Read from the turn service rather than from the label beside the
        /// button, which the game writes as an icon token followed by the number.</summary>
        private static string TurnText(EndTurnWindow window)
        {
            int turn = Turn(window);
            return turn < 0 ? null : ModStrings.Format(ModStrings.GalaxyTurn, turn);
        }

        /// <summary>
        /// The three gates the game's own end-turn shortcut passes, in its own order: nothing is in
        /// the way, the tutorial is not holding the turn back, and the session will accept it.
        /// </summary>
        private static bool CanEndTurn(EndTurnWindow window)
        {
            try
            {
                if (!Gui.GuiGameWindowService.CanEndTurnByShortcut)
                {
                    return false;
                }

                if (window.EndTurnDisabler != null && window.EndTurnDisabler.IsTargetDisabled())
                {
                    return false;
                }

                return window.EndTurnService != null
                    && window.EndTurnService.Target != null
                    && window.EndTurnService.Target.CanEndTurn();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Why the button is refusing, when the game says. It hangs no tooltip on this one
        /// button, but the tutorial holding an element back is a thing the game has words for and puts
        /// on every other element it holds back, so those are the words used here.</summary>
        private static IList<string> EndTurnReason(EndTurnWindow window)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTooltip tooltip = AgeWidgets.Readable(
                    AgeWidgets.Raw(AgeWidgets.Transform(window.EndTurnButton))
                );
                foreach (string line in AgeText.Lines(AgeText.Tooltip(tooltip)))
                {
                    lines.Add(line);
                }

                if (window.EndTurnDisabler != null && window.EndTurnDisabler.IsTargetDisabled())
                {
                    string reason = AgeText.Clean("%TutorialDisabledElementDescription");
                    if (!string.IsNullOrEmpty(reason))
                    {
                        lines.Add(reason);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the end-turn reason threw: " + e);
            }

            return lines;
        }

        /// <summary>How many fleets are waiting to be given something to do, counted the way the
        /// button beside it counts them.</summary>
        private string IdleFleetsText()
        {
            try
            {
                Empire empire = Gui.PlayerEmpire;
                if (empire == null)
                {
                    return null;
                }

                global::FleetsScreen.GetIdleFleets(empire, ref _idleFleets);
                return ModStrings.Format(ModStrings.GalaxyIdleFleets, _idleFleets.Count);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- shared ----

        /// <summary>A stock and what the next turn does to it, in the game's own number formatting -
        /// grouped, rounded down, and signed for the part that is a change.</summary>
        private static string StockAndNet(float stock, float net, int decimals)
        {
            return ModStrings.Format(
                ModStrings.GalaxyStockAndNet,
                Amount(stock, false, decimals),
                Amount(net, true, decimals)
            );
        }

        /// <summary>A number the way the game writes it.</summary>
        internal static string Amount(float value, bool signed, int decimals)
        {
            try
            {
                return Gui.FormatAmount(value, true, Gui.Rounding.Floor, signed, decimals);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static float Value(Empire empire, StaticString property)
        {
            try
            {
                return empire.GetPropertyValue(property);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>The banner hangs each total's tooltip on the group around the label rather than on
        /// the label, because the icon beside it is part of the same hover target - and that group is
        /// also the shape the player sees, so it is what the row model measures and what the tooltip is
        /// drawn under.</summary>
        private static AgeTransform Area(AgePrimitiveLabel label)
        {
            try
            {
                AgeTransform widget = label.AgeTransform;
                AgeTransform group = widget.Parent;
                return group != null && AgeWidgets.Raw(group) != null ? group : widget;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game writes the End Turn caption over two lines. Spoken, that is one phrase.
        /// </summary>
        private static string OneLine(string text)
        {
            MessageBuilder message = new MessageBuilder();
            foreach (string line in AgeText.Lines(text))
            {
                message.Fragment(line);
            }

            return message.Build();
        }

        internal static int Turn()
        {
            return Turn(TurnWindow());
        }

        private static int Turn(EndTurnWindow window)
        {
            try
            {
                if (
                    window == null
                    || window.EndTurnService == null
                    || window.EndTurnService.Target == null
                )
                {
                    return -1;
                }

                return window.EndTurnService.Target.Turn + 1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        internal static Empire PlayerEmpire()
        {
            try
            {
                return Gui.PlayerEmpire;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static EndTurnWindow TurnWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<EndTurnWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GameOverlayWindow OverlayWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GameOverlayWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
