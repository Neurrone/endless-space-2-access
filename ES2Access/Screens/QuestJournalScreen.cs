using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The quest journal: the page F7 opens over whatever the player was looking at, where the empire's
    /// quests are drawn as a horizontal strip of cards with the selected one written out underneath.
    ///
    /// The page is four panels and it is declared as four stops, in the order they are drawn: the two
    /// side panels the screen adds to the left edge (how many quests are in each state, and the events
    /// running on the empire), the filter that decides which quests the strip holds, the strip itself,
    /// and the panel that writes the selected quest out in full.
    ///
    /// The strip is a list, not a carousel. The game draws it as one, with a left and a right button
    /// that slide it along one card at a time and an alpha fade over the cards near either edge, but
    /// what it holds is one card per quest and nothing about walking them is horizontal. So the cards
    /// are rows, Enter picks one - the game's own select-then-rebind model, which is why they are radio
    /// buttons rather than buttons - and the sliding is something that happens BEHIND the cursor:
    /// landing on a card off the edge of the viewport asks the game's own paging button for another
    /// page, once per frame, until the card is inside. Nothing here writes a scroll offset and nothing
    /// fights an animation already running.
    ///
    /// Pinning is a control the card DRAWS, not a gesture on the card. The game hangs a second toggle
    /// in every card's corner that makes its quest the empire's ACTIVE quest - the one the HUD tracks in
    /// the corner of the screen - and it is not what a click on the card does, so it is not what Enter
    /// does either: the card opens, and the pin is inside it. The state it leaves behind is read back
    /// off the same toggle, and the tracked quest changing is announced by the HUD's own watcher.
    ///
    /// What the panel underneath says is whatever the game drew there, in drawn order: the quest's
    /// title, the lore, the objective and what completing it pays. The lore is a permanently drawn
    /// paragraph, so it is spoken in full rather than treated as a tooltip, and its own lines are in
    /// the review buffer to walk. Three of the panel's variants have no fixture at turn 3 and are
    /// declared from what the game draws rather than from what it was opened for: the podium a
    /// cooperative quest gets instead of a reward table, the minor-faction button, and the placeholder
    /// the game leaves while a quest waits on the player to choose an objective.
    ///
    /// Escape and F7 stay the game's: the screen is the game's own and its own close paths work.
    /// </summary>
    public sealed class QuestJournalScreen : Screen
    {
        private static readonly object PanelsStop = "quests:panels";
        private static readonly object FiltersStop = "quests:filters";
        private static readonly object ListStop = "quests:list";
        private static readonly object DetailStop = "quests:detail";
        private static readonly object ReportRegion = "quests:region/report";
        private static readonly object EventsRegion = "quests:region/events";

        /// <summary>The clusters the game draws over every page. Over this one that is the empire's
        /// banners and the strip of screen icons in the top left corner; the rest answer for
        /// themselves.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>The quest whose card the cursor has landed on and the strip has not brought into
        /// view yet, and how many more pages are worth asking for. A request rather than a move: the
        /// game's paging button animates, and a player walking the strip at speed would otherwise
        /// leave a queue of slides behind them.</summary>
        private Quest _reveal;
        private int _pagesLeft;

        /// <summary>How many pages to ask for before giving up on a card - a card the strip cannot
        /// bring inside its own margins would otherwise be paged after for ever.</summary>
        private const int PagePatience = 32;

        public override string Key
        {
            get { return "screen.quests"; }
        }

        /// <summary>Over the view levels it is drawn on top of, beside the technology wheel and the
        /// senate. Every page the strip of screen icons opens shares this number, because the engine
        /// keeps them in an exclusive window stack: showing one hides every other instantly (measured -
        /// the stack's own component on the renderer they all sit under), so no two of them are ever up
        /// together and there is nothing for a number of its own to order.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = AgeText.Label(JournalHeading());
                return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.ScreenQuests) : title;
            }
        }

        /// <summary>The strip, which is what the player opened the page for. The filter above it and
        /// the panels down the left edge are a Shift+Tab away.</summary>
        public override object InitialFocusStop
        {
            get { return ListStop; }
        }

        /// <summary>A page the player closes and comes straight back to, with the same card under the
        /// cursor.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape is the game's: it closes the screen, which is what the page's own exit does
        /// too. The type-ahead layer takes the key away only while a search is up, and puts it back
        /// itself.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        public override bool IsActive()
        {
            try
            {
                NarrativeScreen window = Window();
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null && !gui.IsAnyModalVisible && !gui.IsInLoadingWindow;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void OnPush()
        {
            _hud.Baseline();
        }

        public override void OnPop()
        {
            _hud.Forget();
            _reveal = null;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            PageIntoView();
        }

        public override void Build(GraphBuilder builder)
        {
            NarrativeScreen window = Window();
            if (window == null)
            {
                return;
            }

            // Down the screen: the empire's banners in the top left corner and this screen's own title
            // beside them, the two side panels under
            // them, then the journal's own two containers - the strip and its filter above, the
            // selected quest below.
            _hud.Top(builder);
            BuildSidePanels(builder, window);
            BuildFilters(builder, window);
            BuildList(builder, window);
            BuildDetail(builder, window);
            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

        // ---- the panels down the left edge ----

        /// <summary>
        /// The two panels the screen adds to the side bar: how many quests are in each state, and the
        /// events currently running on the empire.
        ///
        /// One stop with a region each, because they are drawn as one column against the edge of the
        /// page and neither is a place to go - both are places to read. Their contents are read off the
        /// shape of the panels rather than modelled field by field: every row in them is a caption, a
        /// number and a tooltip explaining what the number counts, and there is nothing to do to any
        /// of it.
        /// </summary>
        private void BuildSidePanels(GraphBuilder builder, NarrativeScreen window)
        {
            try
            {
                SidePanelsWindow panels = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<SidePanelsWindow>(false)
                    : null;
                if (panels == null)
                {
                    return;
                }

                QuestReportSidePanel report =
                    panels.GetComponentInChildren<QuestReportSidePanel>(true);
                NarrativeEventsSidePanel events =
                    panels.GetComponentInChildren<NarrativeEventsSidePanel>(true);

                builder.BeginStop(PanelsStop);
                AddPanel(builder, report == null ? null : report.ContentGroup, ReportRegion, "quests:report/");
                AddPanel(builder, events == null ? null : events.ContentGroup, EventsRegion, "quests:events/");
                builder.SetRegion(null);
            }
            catch (Exception e)
            {
                Log.Warn("quests: reading the side panels threw: " + e);
            }
        }

        /// <summary>
        /// One side panel as a region, under the heading the game drew over it where it drew one.
        ///
        /// A panel's leading line is sometimes a heading naming the whole panel ("Active Events") and
        /// sometimes a line of its own with a number and a sentence in it (the report's "Quests"
        /// gauge). The two are told apart by whether the game hung an explanation on it: a heading with
        /// nothing to review is the region's name and a level rather than a line, while one carrying a
        /// sentence stays a line, because a level is nowhere to put words.
        /// </summary>
        private void AddPanel(
            GraphBuilder builder,
            AgeTransform content,
            object region,
            string keyPrefix
        )
        {
            if (content == null || !AgeWidgets.Visible(content))
            {
                return;
            }

            builder.SetRegion(region);
            AgeTransform caption = Caption(content);
            if (caption == null)
            {
                Collect(builder, content, keyPrefix, 0);
                return;
            }

            builder.PushContext(AgeWidgets.TextOf(caption));
            IList<AgeTransform> children = content.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (!ReferenceEquals(children[i], caption))
                {
                    Collect(builder, children[i], keyPrefix, 1);
                }
            }

            builder.PopContext();
        }

        /// <summary>The heading the panel names itself with, or nothing where its first line is a line
        /// of its own: a heading is the first thing drawn, says something, has no explanation hanging
        /// off it, would have read as ONE line rather than a band, and has other lines under it to
        /// name.</summary>
        private static AgeTransform Caption(AgeTransform content)
        {
            // Only where the panel would have been read as a band of lines at all: one the shape rule
            // reads as a SINGLE line has no lines under a heading to name.
            if (!HasGroupChild(content))
            {
                return null;
            }

            IList<AgeTransform> children = content.Children;
            AgeTransform first = null;
            int drawn = 0;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (children[i] == null || !AgeWidgets.Visible(children[i]))
                {
                    continue;
                }

                drawn++;
                if (first == null)
                {
                    first = children[i];
                }
            }

            return drawn > 1
                && first != null
                && !HasGroupChild(first)
                && Explanation(first) == null
                && !string.IsNullOrEmpty(AgeWidgets.TextOf(first))
                ? first
                : null;
        }

        /// <summary>How deep to go looking for the lines of a panel nobody has modelled.</summary>
        private const int MaxScrapeDepth = 6;

        /// <summary>
        /// A panel read as it is drawn, the same rule the star system page's side panels are read by: a
        /// group whose children are all PRIMITIVES is one thing the game drew out of several pieces
        /// ("Current" beside "1") and reads as one line; a group holding other groups is a container,
        /// and each of those is a line of its own.
        /// </summary>
        private static void Collect(
            GraphBuilder builder,
            AgeTransform widget,
            string keyPrefix,
            int depth
        )
        {
            if (widget == null || depth > MaxScrapeDepth || !AgeWidgets.Visible(widget))
            {
                return;
            }

            try
            {
                if (depth < MaxScrapeDepth && HasGroupChild(widget))
                {
                    IList<AgeTransform> children = widget.Children;
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        Collect(builder, children[i], keyPrefix, depth + 1);
                    }

                    return;
                }

                AddReadout(builder, widget, keyPrefix + widget.name + "/" + depth);
            }
            catch (Exception e)
            {
                Log.Warn("quests: reading a panel threw: " + e);
            }
        }

        /// <summary>Whether anything inside this widget is itself a container - which is what makes the
        /// widget a band of separate lines rather than one line drawn out of pieces.</summary>
        private static bool HasGroupChild(AgeTransform widget)
        {
            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || !AgeWidgets.Visible(child))
                {
                    continue;
                }

                IList<AgeTransform> grandchildren = child.Children;
                for (int j = 0; grandchildren != null && j < grandchildren.Count; j++)
                {
                    if (grandchildren[j] != null && AgeWidgets.Visible(grandchildren[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ---- the filter ----

        /// <summary>
        /// The four filters the game draws across the top of the strip, and the heading it writes over
        /// the whole container.
        ///
        /// They are radio buttons because that is what the game made them: one group, exactly one in
        /// force, and picking one is doing - the strip is refilled on the spot. The heading is a node
        /// of its own here rather than in the strip's stop because a window's heading is declared once,
        /// in the first stop of the window it names.
        /// </summary>
        private void BuildFilters(GraphBuilder builder, NarrativeScreen window)
        {
            AgeTransform table = window.QuestSelectionTogglesTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            builder.BeginStop(FiltersStop);
            AddReadout(builder, Widget(JournalHeading()), "quests:heading");

            IList<AgeTransform> toggles = table.Children;
            for (int i = 0; toggles != null && i < toggles.Count; i++)
            {
                AddFilter(builder, window, toggles[i], i);
            }
        }

        private static void AddFilter(
            GraphBuilder builder,
            NarrativeScreen window,
            AgeTransform widget,
            int index
        )
        {
            AgeControlToggle toggle = widget == null ? null : widget.AgeControl as AgeControlToggle;
            if (toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeControlToggle control = toggle;
            GuiRadioGroup group = window.QuestFilteringRadioGroup;
            int slot = index;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Radio(
                () => AgeWidgets.TextOf(it),
                () => group != null && group.CurrentSelection == slot,
                () => AgeWidgets.Toggle(control),
                () => AgeWidgets.Operable(it),
                null,
                tooltip
            );
            AgeWidgets.Point(vtable, control);
            builder.AddItem(Nodes.Drawn(ControlId.For(toggle, "quests:filter/" + index), vtable, toggle));
        }

        // ---- the strip of cards ----

        /// <summary>
        /// One row per card the game has drawn, in the order it drew them.
        ///
        /// A card is keyed on its quest's NAME rather than on the quest itself: the pinned-quest panel
        /// the HUD declares over every page already carries that object, and two nodes naming one
        /// object are one control as far as the cursor is concerned. The name is stable for the life of
        /// the quest, which is all the cursor needs to ride a refresh out.
        ///
        /// An unbound card is a pooled slot the game has finished with, not a quest - the screen
        /// unbinds every card before refilling the table, so the quest behind a card is the game's own
        /// answer to which of them exist. Alpha says nothing here: the strip fades the cards near
        /// either edge of the viewport to a quarter and they are as real as the ones in the middle.
        /// </summary>
        private void BuildList(GraphBuilder builder, NarrativeScreen window)
        {
            AgeTransform table = window.QuestListTable;
            IList<AgeTransform> cards = table == null ? null : table.Children;
            if (cards == null)
            {
                return;
            }

            bool opened = false;
            for (int i = 0; i < cards.Count; i++)
            {
                QuestCard card = cards[i] == null ? null : cards[i].GetComponent<QuestCard>();
                if (card == null || card.GuiQuest == null || !AgeWidgets.Visible(cards[i]))
                {
                    continue;
                }

                if (!opened)
                {
                    builder.BeginStop(ListStop);
                    opened = true;
                }

                AddCard(builder, card);
            }
        }

        /// <summary>
        /// One quest as the strip draws it: what it is called, what kind of quest the game files it
        /// under, and whether it is the one being tracked.
        ///
        /// Enter is the card's own click, which selects it and rebinds the panel underneath - the game
        /// distinguishes selecting from doing and so does this. The pin is a SECOND control the card
        /// draws, in its corner, and it is declared as one: the card opens like the places on the map
        /// do, and inside it is the pin's own toggle, walked and pressed like any other. It used to be
        /// on Alt and Enter, which was the mod inventing a gesture the game does not have - there is no
        /// modified click on a quest card - and which cost the card a truthful answer to Enter, since
        /// one node was answering for two different actions.
        /// </summary>
        private void AddCard(GraphBuilder builder, QuestCard card)
        {
            QuestCard it = card;
            Quest quest = card.GuiQuest.Quest;
            AgeControlToggle select = card.AgeTransform.AgeControl as AgeControlToggle;
            NodeVtable vtable = GraphNodes.Radio(
                () => AgeText.Label(it.Title),
                () => select != null && select.State,
                () => Select(it, select),
                null,
                null,
                null
            );
            // The kind of quest, in the game's own word for it, and the pin - both read where the card
            // draws them, left to right. The pin is settled rather than watched: the HUD's own
            // announcer speaks every change of the tracked quest, and hearing it from the card as well
            // would be hearing it twice.
            vtable.Announcements.Insert(1, GraphNodes.ValuePart(() => AgeText.Label(it.Category)));
            vtable.Announcements.Insert(
                2,
                new NodeAnnouncement(() => PinState(it), false, AnnouncementKinds.Value)
            );
            vtable.OnFocusVisual = () =>
            {
                Show(quest);
                PointerFocus.MoveToToggle(select, null, it.AgeTransform);
            };
            vtable.OnBlurVisual = AgeWidgets.ReleasePointer;

            ControlId id = ControlId.Structural("quests:card/" + quest.Name);
            ScrollIntoView.Anchor(vtable, it.AgeTransform);
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                AddPin(builder, it, quest);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// The toggle the card draws in its corner, which makes this quest the empire's active one -
        /// the quest the HUD tracks in the corner of the screen.
        ///
        /// The game draws it as an icon and gives it no words, but it does name the action, on the
        /// button the quest-begun notification offers for exactly this (<c>%NotificationQuestBegunPin
        /// Title</c>), so that is what it is called. Its own tooltip explains the rest.
        /// </summary>
        private static void AddPin(GraphBuilder builder, QuestCard card, Quest quest)
        {
            AgeControlToggle pin = card.PinToggle;
            if (pin == null || !AgeWidgets.Visible(pin.AgeTransform))
            {
                return;
            }

            AgeControlToggle it = pin;
            AgeTooltip tooltip = AgeWidgets.Raw(pin.AgeTransform);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeText.Clean("%NotificationQuestBegunPinTitle"),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(it.AgeTransform),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            ScrollIntoView.Anchor(vtable, card.AgeTransform);
            builder.AddItem(Nodes.Synthetic(
                ControlId.Structural("quests:card/" + quest.Name + "/pin"),
                vtable
            ));
        }

        /// <summary>Whether the game is tracking this quest, read off the toggle the card draws for
        /// it. The game gives the state no words of its own - the pin is an icon - so this one is the
        /// mod's, and it says nothing at all for a card that is not pinned, the same silence a
        /// selection keeps.</summary>
        private static string PinState(QuestCard card)
        {
            try
            {
                AgeControlToggle pin = card.PinToggle;
                return pin != null && AgeWidgets.Visible(pin.AgeTransform) && pin.State
                    ? ModStrings.Get(ModStrings.QuestsPinned)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Select a card through its own toggle, which is what rebinds the panel underneath
        /// and plays the sound a player at the mouse would hear.</summary>
        private void Select(QuestCard card, AgeControlToggle toggle)
        {
            try
            {
                AgeWidgets.Toggle(toggle);
                Show(card.GuiQuest == null ? null : card.GuiQuest.Quest);
            }
            catch (Exception e)
            {
                Log.Warn("quests: selecting a quest threw: " + e);
            }
        }

        // ---- the strip's own paging ----

        /// <summary>Ask the strip to bring a quest's card inside the viewport. One slot, overwritten:
        /// a player walking the strip at speed is only ever owed the card they stopped on.</summary>
        private void Show(Quest quest)
        {
            _reveal = quest;
            _pagesLeft = quest == null ? 0 : PagePatience;
        }

        /// <summary>
        /// Slide the strip until the card under the cursor is inside the viewport, through the game's
        /// own paging buttons.
        ///
        /// The buttons move the strip exactly one card at a time and refuse while their own animation
        /// is running, so this is one press per frame at most and never a press into a slide already
        /// under way. The margin is the button's own width, which is what the screen fades the cards
        /// under - a card still in the fade is a card half hidden behind a button.
        /// </summary>
        private void PageIntoView()
        {
            if (_reveal == null)
            {
                return;
            }

            try
            {
                NarrativeScreen window = Window();
                QuestCard card = window == null ? null : Card(window, _reveal);
                AgeControlScrollView view = window == null ? null : window.QuestListScrollView;
                if (card == null || view == null || view.Viewport == null || _pagesLeft <= 0)
                {
                    _reveal = null;
                    return;
                }

                Rect viewport = view.Viewport.GetGlobalPosition();
                Rect rect = card.AgeTransform.GetGlobalPosition();
                float margin = window.QuestSelectionLeftButton == null
                    ? 0f
                    : window.QuestSelectionLeftButton.AgeTransform.Width;
                bool before = rect.xMin < viewport.xMin + margin;
                bool after = rect.xMax > viewport.xMax - margin;
                if (!before && !after)
                {
                    _reveal = null;
                    return;
                }

                if (Sliding(view))
                {
                    return;
                }

                _pagesLeft--;
                AgeWidgets.Press(
                    before ? window.QuestSelectionLeftButton : window.QuestSelectionRightButton
                );
            }
            catch (Exception e)
            {
                Log.Warn("quests: paging the quest strip threw: " + e);
                _reveal = null;
            }
        }

        /// <summary>Whether the strip is in the middle of a slide - the same modifier the paging
        /// buttons refuse to restart.</summary>
        private static bool Sliding(AgeControlScrollView view)
        {
            try
            {
                AgeTransform area = view.VirtualArea;
                AgeModifierSet set = area == null ? null : area.GetComponent<AgeModifierSet>();
                AgeModifierItem item;
                return set != null
                    && set.TryGetModifierItem("AgeTransform", "X", out item)
                    && item != null
                    && item.IsRunning;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The card the strip is drawing for a quest, or null once the filter has taken it out
        /// of the list.</summary>
        private static QuestCard Card(NarrativeScreen window, Quest quest)
        {
            AgeTransform table = window.QuestListTable;
            IList<AgeTransform> cards = table == null ? null : table.Children;
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                QuestCard card = cards[i] == null ? null : cards[i].GetComponent<QuestCard>();
                if (card != null && card.GuiQuest != null && card.GuiQuest.Name.Equals(quest.Name))
                {
                    return card;
                }
            }

            return null;
        }

        // ---- the selected quest ----

        /// <summary>
        /// The panel under the strip, in the order the game draws it: the heading it writes over the
        /// whole container, the quest's title, the button a minor faction quest adds beside it, the
        /// lore, then the objective and what it pays.
        ///
        /// The lore is drawn ABOVE the objective, which is not the order the panel's own fields are
        /// declared in - the rectangles are what decides, and this is one of the places the widget
        /// tree and the screen disagree.
        /// </summary>
        private void BuildDetail(GraphBuilder builder, NarrativeScreen window)
        {
            NarrativeScreenSelectedQuestPanel panel = window.SelectedQuestPanel;
            if (
                panel == null
                || !panel.Shown
                || !AgeWidgets.Visible(panel.AgeTransform)
                || window.SelectedQuest == null
            )
            {
                return;
            }

            try
            {
                builder.BeginStop(DetailStop);
                AddReadout(builder, Widget(OptionsScreen.LabelIn(panel.AgeTransform)), "quests:selected-heading");
                AddReadout(builder, Widget(panel.Title), "quests:selected-title");
                AddMinorFaction(builder, window);
                AddLore(builder, panel);
                AddObjective(builder, panel);
                AddRewards(builder, panel);
            }
            catch (Exception e)
            {
                Log.Warn("quests: reading the selected quest threw: " + e);
            }
        }

        /// <summary>The button a minor-faction quest draws beside its title, which takes the galaxy view
        /// to the civilization the quest is about. Offered only while the game is drawing it: the screen
        /// hides it for every other kind of quest and never disables it.</summary>
        private static void AddMinorFaction(GraphBuilder builder, NarrativeScreen window)
        {
            AgeTransform widget = window.MinorFactionButton;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            // The button is a wordless image and its tooltip IS its name, so the name is spoken as the
            // label and the tooltip is not announced a second time - it stays reviewable.
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Tooltip(tooltip),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(it),
                tooltip,
                TooltipMode.None
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(widget, "quests:minor-faction"), vtable, widget));
        }

        /// <summary>
        /// The quest's story, as one line the player lands on and hears whole.
        ///
        /// The game draws it permanently rather than behind a hover, so it is spoken rather than
        /// indicated - and the labels already hold every word of it while the typewriter is still
        /// drawing them. Up to three paragraphs are drawn one under another: the quest's own
        /// description, what the current objective set is about, and - once the quest is over - how it
        /// turned out. The review buffer keeps them as separate lines to walk.
        /// </summary>
        private static void AddLore(GraphBuilder builder, NarrativeScreenSelectedQuestPanel panel)
        {
            QuestLoreGroup lore = panel.Lore;
            if (lore == null || !AgeWidgets.Visible(lore.AgeTransform))
            {
                return;
            }

            QuestLoreGroup it = lore;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => LoreText(it)),
                },
                Sections = GraphNodes.Sections(() => LoreLines(it), null),
            };
            AgeWidgets.PointAt(vtable, lore.AgeTransform);
            builder.AddItem(Nodes.Drawn(ControlId.For(lore, "quests:lore"), vtable, lore));
        }

        /// <summary>Every paragraph the lore group is drawing, as one spoken phrase. Game text joins
        /// with a space, never with the mod's list separator.</summary>
        private static string LoreText(QuestLoreGroup lore)
        {
            MessageBuilder message = new MessageBuilder();
            AgePrimitiveLabel[] labels = LoreLabels(lore);
            for (int i = 0; i < labels.Length; i++)
            {
                if (Drawn(labels[i]))
                {
                    message.Fragment(AgeText.Label(labels[i]));
                }
            }

            return message.Build();
        }

        /// <summary>The same paragraphs a line at a time, which is what the buffer walks - the game
        /// writes each of them with its own line breaks and those are the boundaries.</summary>
        private static IList<string> LoreLines(QuestLoreGroup lore)
        {
            List<string> lines = new List<string>();
            AgePrimitiveLabel[] labels = LoreLabels(lore);
            for (int i = 0; i < labels.Length; i++)
            {
                if (!Drawn(labels[i]))
                {
                    continue;
                }

                foreach (string line in AgeText.Lines(AgeText.Label(labels[i])))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>The three labels the lore group stacks, in the order it stacks them.</summary>
        private static AgePrimitiveLabel[] LoreLabels(QuestLoreGroup lore)
        {
            return new[] { lore.QuestLoreLabel, lore.ObjectiveLoreLabel, lore.OutcomeLoreLabel };
        }

        private static bool Drawn(AgePrimitiveLabel label)
        {
            return label != null && AgeWidgets.Visible(label.AgeTransform);
        }

        /// <summary>
        /// What the quest wants done next, and the button the game draws beside it for quests it can
        /// point at on the map.
        ///
        /// The marker is offered only where the game DREW it: the objective panel hides the button for
        /// a quest with nowhere to point and leaves it enabled, so asking about enablement alone would
        /// offer an action that does nothing. Pressing it takes the camera to the target and closes the
        /// journal, which is the game's own doing.
        ///
        /// A quest waiting on the player to choose between objectives has no objective yet, and the
        /// game draws its own placeholder in the same place; that is what is declared then.
        /// </summary>
        private static void AddObjective(
            GraphBuilder builder,
            NarrativeScreenSelectedQuestPanel panel
        )
        {
            AgeTransform pending = panel.PendingObjectiveChoiceGroup;
            if (pending != null && AgeWidgets.Visible(pending))
            {
                AddReadout(builder, pending, "quests:pending-choice");
            }

            QuestObjectiveTitle objective = panel.Objective;
            if (
                objective == null
                || !objective.Shown
                || !AgeWidgets.Visible(objective.AgeTransform)
            )
            {
                return;
            }

            AgeControlButton marker = objective.ShowLocationButton;
            bool offered =
                marker != null
                && AgeWidgets.Visible(marker.AgeTransform)
                && AgeWidgets.Operable(marker.AgeTransform);
            AgePrimitiveLabel title = objective.ObjectiveTitle;
            AgeTooltip hint = objective.ObjectiveTooltip;
            AgeControlButton it = marker;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = offered ? ControlTypes.Button : null,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(title)),
                },
                // The objective's own hint first, then - for a quest the game will point at - what the
                // marker button says it does, in the order the two are drawn across the row.
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(hint),
                    offered ? GraphNodes.TooltipSection(AgeWidgets.Raw(marker.AgeTransform)) : null
                ),
                OnActivate = offered ? (Action)(() => AgeWidgets.Press(it)) : null,
            };
            if (hint != null)
            {
                AgeWidgets.PointAt(vtable, hint.AgeTransform);
            }

            builder.AddItem(Nodes.Drawn(ControlId.For(objective, "quests:objective"), vtable, objective));
        }

        /// <summary>
        /// What completing the quest pays: the caption the game writes beside the list, then one line
        /// per thing in it.
        ///
        /// A quest several empires are racing on gets a PODIUM instead - one line per place, saying
        /// what that place is worth - and the game swaps one table for the other in the same slot. Both
        /// are read from whichever is drawn rather than from what kind of quest this is.
        ///
        /// The caption is a level over those lines rather than a line of its own: the game hangs no
        /// explanation on it (measured), so there is nothing about it to review. It is only a level
        /// while there is something under it - a caption over an empty table is what a sighted player
        /// reads too, and stays a line.
        /// </summary>
        private static void AddRewards(
            GraphBuilder builder,
            NarrativeScreenSelectedQuestPanel panel
        )
        {
            AgeTransform group = panel.RewardGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform title = Widget(OptionsScreen.LabelIn(group));

            QuestRewardsTable rewards = panel.Rewards;
            AgeTransform table = null;
            string keyPrefix = null;
            if (rewards != null && AgeWidgets.Visible(rewards.AgeTransform))
            {
                table = rewards.RewardsTable;
                keyPrefix = "quests:reward/";
            }
            else
            {
                QuestPodiumTable podium = panel.Podium;
                if (podium != null && AgeWidgets.Visible(podium.AgeTransform))
                {
                    table = podium.PodiumLineTable;
                    keyPrefix = "quests:podium/";
                }
            }

            string caption =
                title == null || !Drawn(table) || AgeWidgets.Raw(title) != null
                    ? null
                    : AgeWidgets.TextOf(title);
            if (string.IsNullOrEmpty(caption))
            {
                AddReadout(builder, title, "quests:reward-title");
                AddItems(builder, table, keyPrefix);
                return;
            }

            builder.PushContext(caption);
            AddItems(builder, table, keyPrefix);
            builder.PopContext();
        }

        /// <summary>Whether the table has a line in it the player would hear - the same test
        /// <see cref="AddReadout"/> makes, asked before the caption over it is turned into a level that
        /// would otherwise name nothing.</summary>
        private static bool Drawn(AgeTransform table)
        {
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = AgeWidgets.DrawnChild(items, i);
                if (
                    item != null
                    && AgeWidgets.Visible(item)
                    && !string.IsNullOrEmpty(AgeWidgets.TextOf(item))
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The lines of a rewards table, minus the ones it is no longer drawing: the table is
        /// pooled (<c>QuestRewardsTable.BuildRewards</c> :77 reserves its children), so a quest with
        /// fewer rewards than the one read before it leaves the surplus items faded to nothing while
        /// they stay Visible and keep the other quest's words.</summary>
        private static void AddItems(GraphBuilder builder, AgeTransform table, string keyPrefix)
        {
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = AgeWidgets.DrawnChild(items, i);
                if (item != null)
                {
                    AddReadout(builder, item, keyPrefix + i);
                }
            }
        }

        // ---- shared ----

        /// <summary>A line the game draws and the player only reads: a heading, a caption and a number,
        /// a reward and what it is. Declared only while the game is drawing words there - a group with
        /// nothing to say is not a control.</summary>
        private static void AddReadout(GraphBuilder builder, AgeTransform widget, string key)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            string text = AgeWidgets.TextOf(widget);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            AgeTooltip tooltip = Explanation(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, tooltip == null ? widget : tooltip.AgeTransform);
            builder.AddItem(Nodes.Drawn(ControlId.For(widget, key), vtable, widget));
        }

        /// <summary>
        /// The tooltip that explains a line, which is not always on the widget the line was read from.
        ///
        /// A line built out of several pieces carries its explanation on whichever piece the game
        /// expects the mouse to be over - the gauge in the quest report has no words of its own and
        /// hangs the only sentence about it on the title inside it. One tooltip under a line is that
        /// line's; several would be a row of different things and are left to the pieces.
        /// </summary>
        private static AgeTooltip Explanation(AgeTransform widget)
        {
            AgeTooltip own = AgeWidgets.Raw(widget);
            if (own != null)
            {
                return own;
            }

            AgeTooltip found = null;
            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTooltip child =
                    children[i] == null || !AgeWidgets.Visible(children[i])
                        ? null
                        : AgeWidgets.Raw(children[i]);
                if (child == null)
                {
                    continue;
                }

                if (found != null)
                {
                    return null;
                }

                found = child;
            }

            return found;
        }

        /// <summary>The heading the game writes over the quest list, which is the name of the page.
        /// </summary>
        private static AgePrimitiveLabel JournalHeading()
        {
            try
            {
                NarrativeScreen window = Window();
                AgeTransform toggles = window == null ? null : window.QuestSelectionTogglesTable;
                AgeTransform container = Ancestor(toggles, 3);
                return container == null ? null : OptionsScreen.LabelIn(container);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The container a widget is <paramref name="levels"/> up from. The screen names the
        /// toggles table and not the container it lives in, and the heading hangs off the container.
        /// </summary>
        private static AgeTransform Ancestor(AgeTransform widget, int levels)
        {
            AgeTransform at = widget;
            for (int i = 0; at != null && i < levels; i++)
            {
                at = at.Parent;
            }

            return at;
        }

        private static AgeTransform Widget(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static NarrativeScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<NarrativeScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
