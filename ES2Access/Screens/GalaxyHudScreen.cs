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
    /// The galaxy - the page the whole game is played from - made navigable.
    ///
    /// It is not a window. What the player calls "the map" is the galaxy itself with three unrelated
    /// windows drawn over its corners, and no one of them owns the page, so this screen is assembled
    /// from all of them plus the galaxy model underneath. Being ours is therefore not "is a window
    /// up" but "is the game showing the galaxy and nothing over it": a full screen (technology,
    /// diplomacy) or a modal replaces the page and this screen stands down, while a notification or
    /// the tutorial popup merely covers a corner of it and does not - they sit on higher layers, and
    /// a covered screen keeps its cursor, so dismissing one puts the player back where they were.
    ///
    /// Tab moves between the places there are to be, in the order the corner of the screen they come
    /// from reads: what the empire is worth, where the systems are, where the fleets are, what the game
    /// is asking about, and what the turn itself offers.
    ///
    /// A tutorial the player has collapsed is one of those places. Collapsing it hands the keyboard
    /// back to this page, and the bar the game leaves at the top of the right-hand edge - its title,
    /// its close button, the arrow that brings it back - is declared here, above the notification icons
    /// drawn under it, by the tutorial screen that knows how to read it.
    ///
    /// A stop exists on a frame only if the game is showing something at it. There are no placeholders:
    /// where the game draws nothing for having no fleets and nothing for having no notifications, Tab
    /// finds nothing there either, because a stop that says "nothing" is a stop the player has to walk
    /// past to learn what a glance at the screen would have told them.
    ///
    /// Everything is read from the game's own model rather than from the labels on the banners. Every
    /// number up there is animated - the dust total counts up to its new value over a second or so -
    /// so the labels are a picture of a number on its way somewhere, and the model is the number.
    ///
    /// The empire stop is laid out in the ROWS the cluster is drawn in - the strip of screen icons, the
    /// running totals under it, the research line, the stockpiles - and up and down move between them
    /// while left and right walk along one. The rows are measured rather than declared, so a stockpile
    /// strip the engine wraps onto a second line is walked as two lines without anything being told.
    ///
    /// The totals and the stockpiles are readable but not activatable: they are the only way a keyboard
    /// player learns what the empire is worth, and the game's own click handlers on them do nothing
    /// outside its debug mode. Focus still makes the game draw their tooltips, which is where the stat
    /// block behind each number lives - and where the review buffer reads it from.
    ///
    /// The screen icons ARE activatable, and none of the screens they open is navigable yet: opening
    /// one stands this screen down and leaves the player with the game's own keys until Escape closes
    /// it again. That is a worse place to be than here, and still better than not being told the screen
    /// exists.
    ///
    /// Focusing a system or a fleet moves the camera to it exactly as the game's own "show me this"
    /// routes do, and deliberately no further: the system management screen those routes would open
    /// for a colony of yours is not navigable yet, so this stop takes the player to the system on the
    /// map and stops there.
    ///
    /// The new turn is announced without anyone being on the End Turn button, because the turn
    /// changing is the one thing on this page that happens to the player rather than being done by
    /// them.
    /// </summary>
    public sealed class GalaxyHudScreen : Screen
    {
        private static readonly object TurnStop = "galaxy:turn";
        private static readonly object EmpireStop = "galaxy:empire";
        private static readonly object TutorialStop = "galaxy:tutorial";
        private static readonly object NotificationStop = "galaxy:notifications";
        private static readonly object SystemStop = "galaxy:systems";
        private static readonly object FleetStop = "galaxy:fleets";

        // Regions - what Alt and an arrow jump between - are declared only where a stop really has
        // two halves. A stop with one region swallows the key and moves nothing, which reads as the
        // key being broken rather than as there being nowhere else to go. The empire stop declares
        // none: its parts are its rows, and up and down already walk them.
        private static readonly object OwnedSystemsRegion = "galaxy:systems/owned";
        private static readonly object OtherSystemsRegion = "galaxy:systems/other";

        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        private List<Fleet> _idleFleets = new List<Fleet>();
        private int _turn = -1;

        public override string Key
        {
            get { return "screen.galaxy"; }
        }

        /// <summary>Above nothing - it is the bottom of the game - and below everything drawn over it:
        /// the tutorial popup, the notifications, the confirmation box.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        /// <summary>The game has no caption for the galaxy; it is simply where you are when nothing
        /// else is open.</summary>
        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenGalaxy); }
        }

        /// <summary>The page the player returns to after every screen, so coming back puts them on the
        /// control they left rather than at the top.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>The first of the stops, because Tab walks them forwards and stops at the end rather
        /// than coming round again: a cursor seated at the last stop would make the key look broken on
        /// the one screen a player arrives at before they have learnt anything else. It is also what the
        /// player wants read out on arriving - what the empire is worth.</summary>
        public override object InitialFocusStop
        {
            get { return EmpireStop; }
        }

        /// <summary>
        /// Ours while the game is showing the galaxy and nothing has replaced it. "Normal view" is the
        /// game's own name for that: a game that is ready, not in a battle, not in the scan overlay,
        /// not watching a system be discovered or a planet destroyed. A full screen or a modal on top
        /// replaces the page rather than covering it, so those stand this screen down; a loading
        /// window means the page is on its way out.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = GuiService();
                return gui != null
                    && gui.IsInNormalView
                    && !gui.IsAnyScreenVisible
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape belongs to the game: from the galaxy it raises the pause menu, and that is
        /// the route every other way out of a game goes through.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>Arrival starts the watch from the turn that is showing, so opening the page never
        /// announces a turn nobody just took.</summary>
        public override void OnPush()
        {
            _turn = Turn();
        }

        /// <summary>The turn ends and the next one begins on the game's schedule, not the player's -
        /// and while it does, the player is usually nowhere near the End Turn button.</summary>
        public override void OnUpdate()
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
                Log.Warn("galaxy: watching the turn threw: " + e);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            builder.BeginStop(EmpireStop);
            BuildEmpire(builder);

            builder.BeginStop(SystemStop);
            BuildSystems(builder);

            builder.BeginStop(FleetStop);
            BuildFleets(builder);

            // Down the right-hand edge, in the order it is drawn: the bar a collapsed tutorial leaves
            // at the top, then the column of notification icons under it.
            builder.BeginStop(TutorialStop);
            TutorialScreen.BuildCollapsedBar(builder);

            builder.BeginStop(NotificationStop);
            BuildNotifications(builder);

            builder.BeginStop(TurnStop);
            BuildTurn(builder);
        }

        // ---- the turn ----

        /// <summary>What the turn itself offers: end it, move everything that was told to move, walk
        /// to the next fleet with nothing to do, and open the game menu.</summary>
        private void BuildTurn(GraphBuilder builder)
        {
            EndTurnWindow window = TurnWindow();
            if (window == null)
            {
                return;
            }

            List<ControlId> ids = new List<ControlId>();
            builder.StartRow();

            EndTurnWindow it = window;
            AgeControlButton endTurn = window.EndTurnButton;
            if (Visible(Transform(endTurn)))
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => EndTurnLabel(it),
                    () => Press(endTurn),
                    () => CanEndTurn(it),
                    Tooltip(endTurn)
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => TurnText(it)));
                vtable.DetailLines = () => EndTurnReason(it);
                Point(vtable, endTurn);
                ControlId id = ControlId.Referenced(endTurn, "galaxy:end-turn");
                ids.Add(id);
                builder.AddItem(id, vtable);
            }

            AddTurnButton(
                builder,
                ids,
                window.ApplyMovementsButton,
                "apply-movements",
                ModStrings.GalaxyApplyMovements,
                null
            );
            AddTurnButton(
                builder,
                ids,
                window.NextIdleFleetButton,
                "next-idle-fleet",
                ModStrings.GalaxyNextIdleFleet,
                IdleFleetsText
            );
            AddTurnButton(
                builder,
                ids,
                window.GameMenuButton,
                "game-menu",
                ModStrings.GalaxyGameMenu,
                null
            );

            builder.EndRow();
            WireVertically(builder, ids);
        }

        private static void AddTurnButton(
            GraphBuilder builder,
            List<ControlId> ids,
            AgeControlButton button,
            string key,
            string nameKey,
            Func<string> value
        )
        {
            if (!Visible(Transform(button)))
            {
                return;
            }

            AgeControlButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () => Press(it),
                () => Enabled(Transform(it)),
                Tooltip(it)
            );
            if (value != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(value));
            }

            Point(vtable, it);
            ControlId id = ControlId.Referenced(it, "galaxy:" + key);
            ids.Add(id);
            builder.AddItem(id, vtable);
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
                foreach (string line in AgeText.Lines(AgeText.Tooltip(Tooltip(window.EndTurnButton))))
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
                Log.Warn("galaxy: reading the end-turn reason threw: " + e);
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

                FleetsScreen.GetIdleFleets(empire, ref _idleFleets);
                return ModStrings.Format(ModStrings.GalaxyIdleFleets, _idleFleets.Count);
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
        /// before any of it is declared. Four panels contribute to it and none of them knows about the
        /// others, so where their lines fall relative to each other is a question only the screen can
        /// answer - and it answers it by looking.
        /// </summary>
        private static void BuildEmpire(GraphBuilder builder)
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
                    if (toggle.Screen == null || !Visible(widget))
                    {
                        continue;
                    }

                    ControlBanner strip = banner;
                    GuiScreen screen = toggle.Screen;
                    AgeTooltip tooltip = Raw(widget);
                    NodeVtable vtable = GraphNodes.Button(
                        () => ScreenTitle(screen),
                        () => strip.OnControlBannerToggle(screen),
                        () => Enabled(widget),
                        tooltip,
                        GraphNodes.ModeFor(tooltip)
                    );
                    vtable.DetailLines = TooltipLines(tooltip);
                    PointAt(vtable, widget);
                    cells.Add(
                        new Cell
                        {
                            Widget = widget,
                            Id = ControlId.Referenced(
                                toggle,
                                "galaxy:empire/screen/" + screen.GetType().Name
                            ),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the screen icons threw: " + e);
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
            if (label == null || !Visible(label.AgeTransform))
            {
                return;
            }

            AgeTransform area = Area(label);
            AgeTooltip tooltip = Raw(area);
            NodeVtable vtable = Readout(
                () => Gui.GetLocalizedTitle(property),
                () => StockAndNet(stock(), net(), 0),
                TooltipLines(tooltip),
                tooltip
            );
            PointAt(vtable, area);
            cells.Add(
                new Cell
                {
                    Widget = area,
                    Id = ControlId.Referenced(label, "galaxy:empire/" + key),
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
            if (button == null || !Visible(banner.ResearchGroup) || !Visible(Transform(button)))
            {
                return;
            }

            AgeControlButton it = button;
            Empire owner = empire;
            // The banner hangs the technology's tooltip on the line of text, not on the button - which
            // is stretched across the whole banner - so that is both what the game shows a tooltip for
            // and what it should be drawn under.
            AgeTransform line =
                banner.ResearchLabel == null ? Transform(button) : banner.ResearchLabel.AgeTransform;
            AgeTooltip tooltip = Raw(line);
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyResearch),
                () => Press(it),
                () => Enabled(Transform(it)),
                tooltip,
                GraphNodes.ModeFor(tooltip)
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => ResearchText(owner)));
            vtable.DetailLines = TooltipLines(tooltip);
            Point(vtable, it, tooltip, line);
            cells.Add(
                new Cell
                {
                    Widget = Transform(it),
                    Id = ControlId.Referenced(it, "galaxy:empire/research"),
                    Vtable = vtable,
                }
            );
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
                Log.Warn("galaxy: reading the research line threw: " + e);
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
                    if (resource == null || !Visible(item.AgeTransform))
                    {
                        continue;
                    }

                    GuiLocatedResource it = resource;
                    // Small holdings of a strategic or a luxury are counted in tenths, which is how
                    // the strip itself writes them.
                    NodeVtable vtable = Readout(
                        () => AgeText.Clean(it.Title),
                        () =>
                            StockAndNet(
                                it.GetStockValueFromCache(),
                                it.GetNetValueFromCache(),
                                it.GetStockValueFromCache() < 10f ? 1 : 0
                            ),
                        TooltipLines(item.Tooltip),
                        item.Tooltip
                    );
                    Point(vtable, item.Button, item.Tooltip, item.AgeTransform);
                    cells.Add(
                        new Cell
                        {
                            Widget = item.AgeTransform,
                            Id = ControlId.Referenced(
                                item,
                                "galaxy:empire/resource/" + resource.Name
                            ),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the resource strip threw: " + e);
            }
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
        private static void BuildNotifications(GraphBuilder builder)
        {
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
                    vtable.DetailLines = GraphNodes.TooltipDetails(IconTooltip(it, items));
                    builder.AddItem(
                        ControlId.Referenced(it, "galaxy:notification/" + count),
                        vtable
                    );
                    count++;
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the notifications threw: " + e);
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
                Log.Warn("galaxy: matching a notification to its icon threw: " + e);
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
                Log.Warn("galaxy: finding the notification icons threw: " + e);
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
                Log.Warn("galaxy: opening a notification threw: " + e);
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
                Log.Warn("galaxy: dismissing a notification threw: " + e);
            }
        }

        // ---- systems ----

        /// <summary>
        /// The star systems the player can see, their own colonies first.
        ///
        /// Which ones those are is the same question the map asks when it decides whether to draw a
        /// system's name: explored at least once, and either remembered or currently in sight. Asking
        /// it the same way is what keeps this list and the map showing the same galaxy.
        ///
        /// The two halves are declared as regions to jump between only while there really are two of
        /// them. On turn one there is one colony and nothing else has been seen yet, and a lone region
        /// makes Alt and an arrow swallow the key and move nothing - which sounds like the key being
        /// broken rather than like there being nowhere else to go.
        /// </summary>
        private static void BuildSystems(GraphBuilder builder)
        {
            try
            {
                Empire empire = PlayerEmpire();
                Galaxy galaxy = Gui.Game == null ? null : Gui.Game.Galaxy;
                if (empire == null || galaxy == null)
                {
                    return;
                }

                List<StarSystemNode> owned = new List<StarSystemNode>();
                DepartmentOfTheInterior interior = empire.GetAgency<DepartmentOfTheInterior>();
                if (interior != null)
                {
                    foreach (ColonizedStarSystem colony in interior.ColonizedStarSystems)
                    {
                        if (colony.Node != null)
                        {
                            owned.Add(colony.Node);
                        }
                    }
                }

                List<StarSystemNode> other = new List<StarSystemNode>();
                foreach (StarSystemNode node in galaxy.StarSystemNodes)
                {
                    if (!owned.Contains(node) && Perceived(node, empire))
                    {
                        other.Add(node);
                    }
                }

                bool split = owned.Count > 0 && other.Count > 0;
                if (split)
                {
                    builder.SetRegion(OwnedSystemsRegion);
                }

                // Fetched once for the whole stop rather than once per system: the labels the map
                // draws are pooled by the window, not rebuilt per frame, so one walk of the label
                // window serves every system this build declares.
                StarSystemLabel[] labels = SystemLabels();

                foreach (StarSystemNode node in galaxy.StarSystemNodes)
                {
                    if (owned.Contains(node))
                    {
                        AddSystem(builder, node, true, labels);
                    }
                }

                if (split)
                {
                    builder.SetRegion(OtherSystemsRegion);
                }

                foreach (StarSystemNode node in other)
                {
                    AddSystem(builder, node, false, labels);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the systems threw: " + e);
            }
        }

        /// <summary>The map's own rule for whether a system's name is drawn: it has been explored, and
        /// it is either remembered or in sight now.</summary>
        private static bool Perceived(StarSystemNode node, Empire empire)
        {
            try
            {
                if ((int)node.Exploration[empire] < 2)
                {
                    return false;
                }

                EntityVisibility.Layer layer = node.Visibility[empire];
                return layer == EntityVisibility.Layer.Known || (int)layer >= 3;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void AddSystem(
            GraphBuilder builder,
            StarSystemNode node,
            bool owned,
            StarSystemLabel[] labels
        )
        {
            StarSystemNode it = node;
            StarSystemLabel label = LabelFor(node, labels);
            AgeTooltip tooltip = label == null ? null : label.StarTooltip;
            NodeVtable vtable = GraphNodes.Button(
                () => it.LocalizedName,
                () => Focus(it),
                null,
                tooltip,
                GraphNodes.ModeFor(tooltip)
            );
            if (owned)
            {
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(() => ModStrings.Get(ModStrings.GalaxySystemColonized))
                );
            }

            vtable.DetailLines = TooltipLines(tooltip);
            if (label != null)
            {
                vtable.OnFocusVisual = () => PointerFocus.MoveTo(null, tooltip, label.AgeTransform);
                vtable.OnBlurVisual = ReleasePointer;
            }

            builder.AddItem(ControlId.Referenced(it, "galaxy:system/" + it.GUID), vtable);
        }

        /// <summary>The on-map label carrying this system's tooltip - matched by the node reference
        /// the label was bound to, with the entity's own identity as a fallback for the rare case the
        /// window rebuilt its labels from a copy rather than the same instance this stop is holding.
        /// </summary>
        private static StarSystemLabel LabelFor(StarSystemNode node, StarSystemLabel[] labels)
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    if (ReferenceEquals(labels[i].StarSystemNode, node))
                    {
                        return labels[i];
                    }
                }

                for (int i = 0; i < labels.Length; i++)
                {
                    StarSystemNode candidate = labels[i].StarSystemNode;
                    if (candidate != null && candidate.GUID == node.GUID)
                    {
                        return labels[i];
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a system to its map label threw: " + e);
            }

            return null;
        }

        private static readonly StarSystemLabel[] NoLabels = new StarSystemLabel[0];

        /// <summary>Every label the map is currently drawing for a system - fetched fresh each time
        /// rather than cached across builds, because the window grows this list as the player explores
        /// more of the galaxy and a cache keyed on nothing that changes would go stale exactly when a
        /// newly-discovered system needed its tooltip.</summary>
        private static StarSystemLabel[] SystemLabels()
        {
            try
            {
                StarSystemLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoLabels
                    : window.GetComponentsInChildren<StarSystemLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the system labels threw: " + e);
                return NoLabels;
            }
        }

        /// <summary>Take the camera to a system, the way every "show me where this happened" button in
        /// the game does: back out to the galaxy and centre on it. Deliberately not the game's
        /// select-this-system route, which drops a colony of yours straight into the system management
        /// screen - a screen with no keyboard route out of it yet.</summary>
        private static void Focus(StarSystemNode node)
        {
            try
            {
                Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(node);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: focusing a system threw: " + e);
            }
        }

        // ---- fleets ----

        /// <summary>The player's own fleets - what each is made of and whether it is under way.
        /// Activating one takes the camera to it and selects it, which is the same thing the button
        /// that walks the idle fleets does. An empire with no fleets has no stop here: the game shows
        /// nothing for that state either.</summary>
        private static void BuildFleets(GraphBuilder builder)
        {
            try
            {
                Empire empire = PlayerEmpire();
                DepartmentOfDefense defense =
                    empire == null ? null : empire.GetAgency<DepartmentOfDefense>();
                if (defense != null)
                {
                    foreach (Fleet fleet in defense.Fleets)
                    {
                        Fleet it = fleet;
                        NodeVtable vtable = GraphNodes.Button(
                            () => it.LocalizedName,
                            () => Select(it),
                            null,
                            null
                        );
                        vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetText(it)));
                        builder.AddItem(
                            ControlId.Referenced(it, "galaxy:fleet/" + it.GUID),
                            vtable
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets threw: " + e);
            }
        }

        private static string FleetText(Fleet fleet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                message.ListItem(ModStrings.Format(ModStrings.GalaxyFleetShips, fleet.ShipsCount));
                message.ListItem(
                    fleet.IsMoving
                        ? ModStrings.Get(ModStrings.GalaxyFleetMoving)
                        : ModStrings.Format(
                            ModStrings.GalaxyFleetMovement,
                            Amount(fleet.CurrentMovementPoints, false, 0)
                        )
                );
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Take the camera to a fleet and select it, through the window's own routine for
        /// exactly that - it knows to wait for the galaxy view to finish coming back before it hands
        /// the fleet to the cursor, and getting that order wrong leaves the selection on nothing.
        /// </summary>
        private static void Select(Fleet fleet)
        {
            try
            {
                EndTurnWindow window = TurnWindow();
                if (window != null && SelectIdleFleet != null)
                {
                    SelectIdleFleet.Invoke(window, new object[] { fleet });
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting a fleet threw: " + e);
            }
        }

        // The window keeps its "go to this fleet" routine to itself, and it is the only place the
        // whole recipe - dock slot, camera, view level, cursor, fleet panel - is written down.
        private static readonly MethodInfo SelectIdleFleet = Member("SelectIdleFleet");

        private static MethodInfo Member(string name)
        {
            try
            {
                return typeof(EndTurnWindow).GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new Type[] { typeof(Fleet) },
                    null
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: looking up " + name + " threw: " + e);
                return null;
            }
        }

        // ---- shared ----

        /// <summary>A line the player reads but does not work: a name and a number. No role word -
        /// there is no control here to name, and "Empire Dust, 150, 38 per turn" is the whole of what
        /// the banner says. Its tooltip is always the class-assembled stat block behind the number, so
        /// it is always the <see cref="TooltipMode.Indicate"/> half of the rule in practice - said here
        /// as "whatever the rule decides" rather than as a hardcoded mode, so a resource whose tooltip
        /// the game ever authored as plain Content would still be read the way the rule says plain
        /// Content should be.</summary>
        private static NodeVtable Readout(
            Func<string> label,
            Func<string> value,
            Func<IList<string>> details,
            AgeTooltip tooltip
        )
        {
            List<NodeAnnouncement> parts = new List<NodeAnnouncement>
            {
                GraphNodes.LabelPart(label),
                GraphNodes.ValuePart(value),
            };
            NodeAnnouncement tooltipPart = GraphNodes.TooltipPart(GraphNodes.ModeFor(tooltip), tooltip);
            if (tooltipPart != null)
            {
                parts.Add(tooltipPart);
            }

            return new NodeVtable { Announcements = parts, DetailLines = details };
        }

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

        private static string Amount(float value, bool signed, int decimals)
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
                return group != null && Raw(group) != null ? group : widget;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Press a control the way the engine presses it: every AGE button carries the object
        /// and the method name its own mouse handler sends to, so replaying that pair runs the
        /// window's own handler with no click that could land on whatever the mouse is over.</summary>
        private static void Press(AgeControlButton button)
        {
            try
            {
                GameObject target = button.OnActivateObject;
                string method = button.OnActivateMethod;
                if (target != null && !string.IsNullOrEmpty(method))
                {
                    target.SendMessage(method, button.gameObject, SendMessageOptions.DontRequireReceiver);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: pressing a control threw: " + e);
            }
        }

        /// <summary>The controls sit in a cluster rather than along one axis, so up and down walk them
        /// too and nobody has to guess which way they are laid out.</summary>
        private static void WireVertically(GraphBuilder builder, List<ControlId> ids)
        {
            for (int i = 1; i < ids.Count; i++)
            {
                builder.Connect(ids[i - 1], GraphDir.Down, ids[i]);
                builder.Connect(ids[i], GraphDir.Up, ids[i - 1]);
            }
        }

        /// <summary>Make the control look hovered while the cursor is on it. The tooltip handed over
        /// here is the widget's own, class-driven or not: a class tooltip has nothing to READ off the
        /// widget but plenty to SHOW, and showing it is the whole point of this hook.</summary>
        private static void Point(NodeVtable vtable, AgeControlButton button)
        {
            AgeControlButton it = button;
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(it, Transform(it).AgeTooltip, it.AgeTransform);
            vtable.OnBlurVisual = ReleasePointer;
        }

        /// <summary>The same for a control whose tooltip the game hangs somewhere other than on the
        /// button - a line of text inside a button stretched across a whole banner, a strip item whose
        /// tooltip lives on the row. <paramref name="under"/> is what the tooltip is drawn beneath.
        /// </summary>
        private static void Point(
            NodeVtable vtable,
            AgeControlButton button,
            AgeTooltip tooltip,
            AgeTransform under
        )
        {
            AgeControlButton it = button;
            AgeTooltip tip = tooltip;
            AgeTransform anchor = under;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(it, tip, anchor);
            vtable.OnBlurVisual = ReleasePointer;
        }

        /// <summary>The same for a widget with no button under it: a running total, one of the screen
        /// icons. Nothing lights up - there is nothing there to light - and the tooltip appears, which
        /// for these is the whole of what the pointer was ever for.</summary>
        private static void PointAt(NodeVtable vtable, AgeTransform widget)
        {
            AgeTransform it = widget;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(it, Raw(it), it);
            vtable.OnBlurVisual = ReleasePointer;
        }

        private static readonly Action ReleasePointer = PointerFocus.Release;

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

        private static int Turn()
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

        private static AgeTransform Transform(AgeControl control)
        {
            try
            {
                return control == null ? null : control.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTooltip Tooltip(AgeControl control)
        {
            return Tooltip(Transform(control));
        }

        /// <summary>
        /// A tooltip only if the widget itself has anything to read.
        ///
        /// An AGE tooltip comes in two kinds. One carries its words in its content field. The other
        /// names a CLASS and a target, and the words are assembled by a renderer at the moment the
        /// tooltip is drawn - a stat block laid out from live data, which is worth reading but is not
        /// there to be read from the widget. The banners and the resource strip are all the second
        /// kind, and their content fields still hold whatever the prefab was authored with - the
        /// panel's own name, most of the time. Reading it aloud would announce "StrategicResourceBanner"
        /// under every resource, so a tooltip that names a class has nothing for a caller that wanted
        /// words off the widget. <see cref="TooltipLines"/> is how those are read instead.
        /// </summary>
        private static AgeTooltip Tooltip(AgeTransform transform)
        {
            try
            {
                return Readable(Raw(transform));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTooltip Readable(AgeTooltip tooltip)
        {
            try
            {
                return tooltip != null && string.IsNullOrEmpty(tooltip.Class) ? tooltip : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A widget's tooltip whatever kind it is - what a caller needs to SHOW one rather
        /// than to read it.</summary>
        private static AgeTooltip Raw(AgeTransform transform)
        {
            try
            {
                return transform == null ? null : transform.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the player would read on a tooltip, resolved when they ask to read it.
        ///
        /// A tooltip that carries its words reads straight off the widget. One that names a class has
        /// none to carry, so it is read back off the tooltip the game has DRAWN - which focus is what
        /// makes appear. That the two are the same text is then true by construction rather than by
        /// this screen reimplementing however the game assembles a stat block.
        ///
        /// It follows that these lines exist a fraction of a second after focus arrives, not with it:
        /// the game waits out its hover delay before drawing. The buffer is filled again when the
        /// tooltip lands (<see cref="PointerFocus.DrawnTooltipChanged"/>), which is well before anyone
        /// can press a key to read it.
        /// </summary>
        private static Func<IList<string>> TooltipLines(AgeTooltip tooltip)
        {
            if (tooltip == null)
            {
                return null;
            }

            AgeTooltip it = tooltip;
            return () =>
                Readable(it) != null
                    ? AgeText.Lines(AgeText.Tooltip(it))
                    : DrawnTooltip.Lines(it);
        }

        // A control inside a group the window has collapsed is still marked visible itself, so the
        // chain above it is what says whether the player can see it.
        private static bool Visible(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (!at.Visible)
                    {
                        return false;
                    }

                    at = at.Parent;
                }

                return widget != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Enabled(AgeTransform widget)
        {
            try
            {
                return widget != null && widget.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Empire PlayerEmpire()
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

        private static EndTurnWindow TurnWindow()
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

        private static GuiManager GuiService()
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
