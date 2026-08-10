using System;
using System.Collections.Generic;
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
    /// is worth along the top, the quest the game is tracking, the notification icons and a collapsed
    /// tutorial down the right-hand edge, and the turn controls in the bottom corner.
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
    /// and each page keeps its own.
    ///
    /// Everything is read from the game's own model rather than from the labels on the banners. Every
    /// number up there is animated - the dust total counts up to its new value over a second or so -
    /// so the labels are a picture of a number on its way somewhere, and the model is the number.
    /// </summary>
    public sealed class GlobalHud
    {
        public static readonly object EmpireStop = "hud:empire";
        public static readonly object QuestStop = "hud:quest";
        public static readonly object TutorialStop = "hud:tutorial";
        public static readonly object NotificationStop = "hud:notifications";
        public static readonly object TurnStop = "hud:turn";

        private List<Fleet> _idleFleets = new List<Fleet>();

        private int _turn = -1;

        /// <summary>The journal this page is listening to, kept so that the subscription can be given
        /// back. Instance state, so a hot reload takes it with the page.</summary>
        private QuestJournal _journal;

        /// <summary>Set by the journal's own event and drained by <see cref="Update"/>: the watcher
        /// only records that the pinned quest changed, and the per-frame pump is what speaks.</summary>
        private bool _questChanged;

        // ---- the passive watch ----

        /// <summary>Start the watch from the turn that is showing, so arriving on a page never
        /// announces a turn nobody just took. The pinned quest needs no such baseline - the game
        /// raises an event when it changes, so there is nothing to compare against.</summary>
        public void Baseline()
        {
            _turn = Turn();
            _questChanged = false;
            WatchQuests();
        }

        /// <summary>Stop watching. The next arrival baselines afresh rather than comparing against
        /// however many turns passed while the player was somewhere else, and the journal gets its
        /// subscription back - the page is not there to announce anything.</summary>
        public void Forget()
        {
            _turn = -1;
            _questChanged = false;
            ForgetQuests();
        }

        /// <summary>The turn ends and the next one begins on the game's schedule, not the player's -
        /// and while it does, the player is usually nowhere near the End Turn button. The same is
        /// true of the quest the game is tracking: finishing one pins the next.</summary>
        public void Update()
        {
            AnnounceTurn();
            AnnounceQuest();
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
        /// what this decides is that it is declared HERE, on whatever page is underneath, because
        /// collapsing the popup hands the keyboard back to that page.</summary>
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
