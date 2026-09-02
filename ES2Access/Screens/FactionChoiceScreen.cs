using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The faction chooser the lobby's Change button opens, made navigable.
    ///
    /// The page is a grid of faction cards and, under it, four bands describing whichever card is
    /// picked: its ship hulls, its affinity and traits and starting situation, and its lore. Each drawn
    /// band is a Tab stop announced by the heading the game wrote on it, and inside a band every
    /// control is a row of its own - the thirteen cards included, because a grid whose columns an arrow
    /// key crosses is a grid you have to be able to see. The hulls band is the one that is not a band
    /// of controls at all but a page turner, and it is read as the list it turns
    /// (<see cref="BuildHulls"/>).
    ///
    /// The game's model is SELECT then LEAVE, and it is kept: a card's own click path
    /// (<c>FactionCard.OnToggleFactionCb</c> -> <c>OnToggleGuiFaction</c> :498-514) only moves the
    /// selection, which rebinds every band under the grid. Nothing here uses the card's DOUBLE click
    /// (<c>OnDoubleClickGuiFaction</c> :529-544), which selects AND commits.
    ///
    /// **Leaving this window commits.** Escape is the game's, and on this window the game's answer to
    /// it is not a cancel: <c>HandleInput</c> (:119-131) calls <c>OnValidateCb</c> (:661-668), which
    /// writes the registry, asks the session to change the player's faction and hides the window. So
    /// does the button the game labels "Cancel" and describes as "Return to the game configuration" -
    /// it is wired to <c>GuiModalWindow.OnCancelCb</c>, whose whole body is
    /// <c>HandleInput(InputAction.Exit)</c> (measured live: picking Sophons and pressing Cancel left
    /// the lobby on Sophons). There is no way out of this window that does not take the highlighted
    /// faction with it, so the screen says so on arrival. Saying it is the only honest option: the
    /// game's own words here would mislead a player who could not see what happened next.
    ///
    /// The screen stands down while the custom-faction editor is up - the game hides the whole choice
    /// panel for it, and that editor has no screen of its own.
    /// </summary>
    public sealed class FactionChoiceScreen : Screen
    {
        private const string CardsStop = "faction-choice:cards";
        private const string CustomStop = "faction-choice:custom";
        private const string HullsStop = "faction-choice:hulls";
        private const string TraitsStop = "faction-choice:traits";
        private const string DescriptionStop = "faction-choice:description";
        private const string ActionsStop = "faction-choice:actions";

        // The three bands the traits stop is drawn as, for Alt+up/down.
        private const string SummaryRegion = "faction-choice:summary";
        private const string TraitsRegion = "faction-choice:traits-list";
        private const string StartingRegion = "faction-choice:starting-list";

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<AgeTransform> _cells = new List<AgeTransform>();
        private readonly List<AgeTransform> _titles = new List<AgeTransform>();

        private static readonly Func<AgeTransform, AgeTransform> Itself = widget => widget;

        public override string Key
        {
            get { return ModStrings.ScreenFactionChoice; }
        }

        /// <summary>Over the lobby that opens it, and under everything a control of its own can raise -
        /// the delete confirmation goes to the message box at 100. The advanced settings sit at 5; the
        /// two are modals over the same page and are never up together, but they get their own numbers
        /// so the budget reads as what it is.</summary>
        public override int Layer
        {
            get { return 6; }
        }

        /// <summary>The game's own heading, and nothing else. That leaving this window confirms the
        /// highlighted faction is written down where a tester reads it, not said to the player on every
        /// arrival: a page that opens with a paragraph of the mod's own prose is a page nobody wants to
        /// come back to.</summary>
        public override string ScreenName
        {
            get
            {
                FactionChoiceModalWindow window = Window();
                string title = window == null ? null : AgeText.Label(window.WindowTitle);
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenFactionChoice)
                    : title;
            }
        }

        /// <summary>
        /// Ours while the chooser is up, has finished animating in and is showing its own panel.
        ///
        /// Operable as well as shown, for the same reason the lobby is: the engine re-enables a window
        /// a frame or so after it says it is shown, and a screen that declared its controls before that
        /// read every one of them as "unavailable" - including the faction card the player already had,
        /// which is what the arrival announcement lands on.
        ///
        /// The custom-faction editor replaces the choice panel inside this same window and has no
        /// screen yet, so the mod stands down for it rather than declaring a page that is not drawn.
        /// </summary>
        public override bool IsActive()
        {
            FactionChoiceModalWindow window = Window();
            try
            {
                GuiPanel panel = window == null ? null : window.FactionChoicePanel;

                // The PANEL's own enablement, not just the window's. Coming back from the custom
                // faction editor the window never closed - only the panel was swapped - and a screen
                // that asked about the window alone declared its cards while the panel was still
                // fading in and disabled, so every card, including the player's own, read
                // "unavailable" on arrival.
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

        /// <summary>Escape belongs to the game - and here the game commits with it. Denying the window
        /// its own key would leave the player with no way out at all.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            FactionChoiceModalWindow window = Window();
            if (window == null || window.FactionsTable == null)
            {
                return;
            }

            BuildCards(builder, window);
            BuildCustomButtons(builder, window);
            BuildHulls(builder, window);
            BuildTraits(builder, window);
            BuildDescription(builder, window);

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        // ---- the grid of factions ----

        /// <summary>The cards, one row each. The game lays them out five to a line; an arrow that moved
        /// across the line would be an arrow whose meaning depends on where in the grid you happen to
        /// be, so up and down walk all thirteen and the list scrolls itself as they do
        /// (<see cref="ScrollIntoView"/> finds the scroll view above whatever card focus landed on).
        ///
        /// A card only ever SELECTS. Its own switch handler is replayed, which is what tells the window
        /// to rebind the hulls, traits and lore underneath - and only the selected card says so, which
        /// is what lands focus on the faction already chosen when the stop is entered.</summary>
        private void BuildCards(GraphBuilder builder, FactionChoiceModalWindow window)
        {
            builder.BeginStop(CardsStop);
            AgeTransform group = Group(window);
            string title = AgeText.Label(OptionsScreen.LabelIn(group));
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            try
            {
                _cells.Clear();
                IList<AgeTransform> children = AgeWidgets.DrawnChildren(window.FactionsTable);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    if (SettingRows.Drawn(children[i]) && Get<FactionCard>(children[i]) != null)
                    {
                        _cells.Add(children[i]);
                    }
                }

                int index = 0;
                foreach (List<AgeTransform> row in AgeLayout.Rows(_cells, Itself))
                {
                    for (int i = 0; i < row.Count; i++)
                    {
                        AddCard(builder, row[i], index);
                        index++;
                    }
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

        private static void AddCard(GraphBuilder builder, AgeTransform widget, int index)
        {
            FactionCard card = Get<FactionCard>(widget);
            AgeControlToggle toggle = card == null ? null : card.SelectionToggle;
            if (toggle == null)
            {
                return;
            }

            AgeTransform it = widget;
            AgeControlToggle pick = toggle;
            FactionCard entry = card;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            // A card the game is refusing carries the reason in its own tooltip and nothing else - an
            // available one's is empty, so announcing it says nothing until there is something to say.
            // Every tooltip on the card - that reason, and the difficulty rating drawn on it - is in
            // the buffer either way.
            NodeVtable vtable = GraphNodes.Radio(
                () => AgeText.Label(entry.FactionTitle),
                () => pick.State,
                () => AgeWidgets.Toggle(pick),
                () => AgeWidgets.Operable(it)
            );
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(2);
            vtable.Sections = SettingRows.RowSections(it, tooltip, dossiers);

            vtable.OnFocusVisual = () => PointerFocus.MoveToToggle(pick, tooltip, it);
            vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
            string cardKey = "faction-choice:card/" + index;
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(card, cardKey), vtable, card),
                cardKey,
                dossiers
            );
        }

        /// <summary>The row of custom-faction buttons under the grid - its own stop, the way a panel's
        /// bottom control row always is, so reaching Add does not mean walking thirteen factions.
        /// </summary>
        private void BuildCustomButtons(GraphBuilder builder, FactionChoiceModalWindow window)
        {
            builder.BeginStop(CustomStop);
            AddBand(builder, AgeWidgets.Parent(AgeWidgets.Transform(window.AddFactionButton)), "faction-choice:custom/");
        }

        // ---- what the chosen faction is ----

        /// <summary>
        /// The ship hulls the faction flies, one row per hull.
        ///
        /// What the game draws is a page turner: one hull, a pair of arrows under it, and a bar per
        /// hull showing which of them is up. So the band is the LIST the arrows step through - a row
        /// per hull, the row the cursor is on being the hull the window is showing - and the engine's
        /// own "n of m" takes the bars' place. Arriving on a row turns the window to that hull through
        /// the arrow the mouse would click, and the row then reads whatever the window drew: the hull's
        /// name, with the description the game hangs on it in the buffer. The arrows and the separate
        /// readout row are gone; they were the mouse's way of doing what the cursor now does.
        ///
        /// The hulls are counted from the list the window filtered for this faction, never from the
        /// bars it drew - that table pools its children and keeps the surplus alive.
        /// </summary>
        private void BuildHulls(GraphBuilder builder, FactionChoiceModalWindow window)
        {
            AgeTransform title = AgeWidgets.Transform(window.HullTitle);

            // Three levels up, not two: the hull's name sits in a title group inside the band's
            // content, and stopping a level short takes the heading from the hull itself - which read
            // "Karga-class, Karga-class".
            AgeTransform group = AgeWidgets.Parent(AgeWidgets.Parent(AgeWidgets.Parent(title)));
            if (!SettingRows.Drawn(title))
            {
                return;
            }

            // A window whose private list cannot be read still draws a hull, and the one row that says
            // so is worth more than an empty band.
            int count = Math.Max(1, HullCount(window));
            AgeTooltip tooltip = SettingRows.LastTooltip(title);
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(2);
            IList<NodeSection> sections = SettingRows.RowSections(title, tooltip, dossiers);

            builder.BeginStop(HullsStop);

            // Entering the band for the first time lands on the hull the window is ALREADY showing,
            // not on row one: the band is a view of the window's own page turner, and a landing that
            // ignored which page is up would turn the picture out from under a player who had only
            // come to read it.
            builder.LandStopOn(ControlId.Structural(HullKey + CurrentHull(window)));

            bool named = Push(builder, group, "faction-choice:hulls/title");
            try
            {
                for (int i = 0; i < count; i++)
                {
                    int index = i;
                    AgeTransform it = title;
                    NodeVtable vtable = new NodeVtable
                    {
                        // No role word: the hull is not a control the player works, it is what the
                        // window is showing them.
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() =>
                            {
                                Show(index);
                                return AgeWidgets.TextOf(it);
                            }),
                        },
                        Sections = sections,
                    };
                    AgeWidgets.PointAt(vtable, AgeWidgets.TooltipOwner(tooltip) ?? title);

                    // Not SetStart: that is the whole GRAPH's landing, and the page's is the faction
                    // card the game has selected. Which hull row this one stop opens on is the
                    // stop-scoped tool - LandStopOn above - and a position the player left here
                    // still outranks it, which is the order a remembered place should come in.
                    // One drawn viewer, N paged contents - evidence is the viewer, identity the index;
                    // per-hull widgets do not exist (the game shows one at a time; Show(index) pages
                    // it).
                    TooltipChildren.Declare(
                        builder,
                        Nodes.Drawn(ControlId.Structural(HullKey + index), vtable, it),
                        HullKey + index,
                        dossiers
                    );
                }
            }
            finally
            {
                Captions.Pop(builder, named);
            }
        }

        /// <summary>
        /// Turn the window to hull <paramref name="index"/> the way clicking its arrow does, if it is
        /// not showing it already and if that hull's row is the one the cursor is standing on.
        ///
        /// Reading the label is where the turn happens because it is the only thing that runs between
        /// the cursor arriving and the landing being spoken. It is guarded twice over: the window is
        /// already on the hull for every read but the first, and a read on a row that is not the
        /// focused one (a graph dump, a type-ahead pass over the stop) turns nothing.
        ///
        /// The step itself is the game's own arrow button, pressed the way a mouse presses it, taking
        /// whichever way round the set is shorter - the window's own handlers are what wrap the index
        /// and rebind the picture, and nothing here reproduces them.
        /// </summary>
        private static void Show(int index)
        {
            try
            {
                FactionChoiceModalWindow window = Window();
                if (window == null || FocusedHull() != index)
                {
                    return;
                }

                int count = HullCount(window);
                int current = CurrentHull(window);
                if (count < 2 || current < 0 || index >= count || current == index)
                {
                    return;
                }

                int forwards = (index - current + count) % count;
                bool ahead = forwards * 2 <= count;
                AgeControlButton arrow = ahead ? window.NextHullButton : window.PreviousHullButton;
                for (int step = 0; step < count && CurrentHull(window) != index; step++)
                {
                    int before = CurrentHull(window);
                    AgeWidgets.Press(arrow);
                    if (CurrentHull(window) == before)
                    {
                        // The arrow answered with nothing - the window is not stepping, and pressing
                        // it again would only be a louder way of not moving.
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("faction choice: turning to the hull under the cursor threw: " + e);
            }
        }

        /// <summary>Which hull's row the cursor is on, or -1 for anywhere else.</summary>
        private static int FocusedHull()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator == null ? -1 : navigator.FocusedIndex(HullKey);
        }

        private const string HullKey = "faction-choice:hull/";

        /// <summary>How many hulls this faction flies: the list the window filtered for it, which is
        /// the same list its arrows step through. -1 where the window will not say.</summary>
        private static int HullCount(FactionChoiceModalWindow window)
        {
            System.Collections.IList hulls =
                Field(ref _hulls, "filteredShipHulls", window) as System.Collections.IList;
            return hulls == null ? -1 : hulls.Count;
        }

        /// <summary>Which of them the window is showing, or -1 where it will not say.</summary>
        private static int CurrentHull(FactionChoiceModalWindow window)
        {
            object current = Field(ref _current, "currentHull", window);
            return current is int ? (int)current : -1;
        }

        private static FieldInfo _hulls;
        private static FieldInfo _current;

        private static object Field(ref FieldInfo cache, string name, FactionChoiceModalWindow window)
        {
            try
            {
                if (window == null)
                {
                    return null;
                }

                if (cache == null)
                {
                    cache = GameHandlers.Field(typeof(FactionChoiceModalWindow), name);
                }

                return cache == null ? null : cache.GetValue(window);
            }
            catch (Exception e)
            {
                Log.Warn("faction choice: reading " + name + " threw: " + e);
                return null;
            }
        }

        /// <summary>The affinity and traits band: the two lines the faction is summarised by, then the
        /// two lists the game draws side by side - each under the heading it drew above it, so "Raia"
        /// is heard as a starting planet rather than as an unexplained word.</summary>
        private void BuildTraits(GraphBuilder builder, FactionChoiceModalWindow window)
        {
            AgeTransform affinity = AgeWidgets.Parent(AgeWidgets.Transform(window.AffinityLabel));
            AgeTransform content = AgeWidgets.Parent(affinity);
            if (!SettingRows.Drawn(affinity))
            {
                return;
            }

            builder.BeginStop(TraitsStop);
            bool named = Push(builder, AgeWidgets.Parent(content), "faction-choice:traits/title");
            try
            {
                // Three bands drawn one under the other, and Alt+up/down jumps between them. Regions
                // live INSIDE a stop - the engine will not carry the jump across a Tab stop - so this
                // is the one band on the page long enough to want them, and it gets all three rather
                // than a lone region the key would swallow silently.
                builder.SetRegion(SummaryRegion);
                SettingRows.AddReadout(builder, affinity, "faction-choice:affinity");
                SettingRows.AddReadout(
                    builder,
                    AgeWidgets.Parent(AgeWidgets.Transform(window.MajorPopulationLabel)),
                    "faction-choice:population"
                );

                // The two headings the game drew over the lists. They are the only direct children of
                // the band that carry a tooltip of their own, which is what tells them apart from the
                // summary lines above and the scroll views below - no prefab name needed.
                _titles.Clear();
                IList<AgeTransform> children = AgeWidgets.DrawnChildren(content);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    if (SettingRows.Drawn(children[i]) && AgeWidgets.Raw(children[i]) != null)
                    {
                        _titles.Add(children[i]);
                    }
                }

                _titles.Sort(Across);
                builder.SetRegion(TraitsRegion);
                AddList(builder, Heading(0), window.FactionTraitsTable, "faction-choice:trait/");
                builder.SetRegion(StartingRegion);
                AddList(
                    builder,
                    Heading(1),
                    window.StartingSituationTable,
                    "faction-choice:starting/"
                );
            }
            finally
            {
                builder.SetRegion(null);
                Captions.Pop(builder, named);
            }
        }

        private AgeTransform Heading(int index)
        {
            return index < _titles.Count ? _titles[index] : null;
        }

        /// <summary>One of the two lists under its own heading, a row per line the game drew.</summary>
        private static void AddList(
            GraphBuilder builder,
            AgeTransform heading,
            AgeTransform table,
            string key
        )
        {
            string title = AgeText.Label(OptionsScreen.LabelIn(heading));
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            try
            {
                IList<AgeTransform> children = AgeWidgets.DrawnChildren(table);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    SettingRows.AddReadout(builder, children[i], key + i);
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

        /// <summary>The lore. One row, named the way the game named the band - the faction's own title -
        /// with the paragraph itself in the review buffer rather than read out whole on every pass
        /// through the page. The label already holds all of its words while the typewriter is still
        /// drawing them.</summary>
        private void BuildDescription(GraphBuilder builder, FactionChoiceModalWindow window)
        {
            AgePrimitiveLabel text = window.FactionDescription;
            AgeTransform group = AgeWidgets.Parent(AgeWidgets.Transform(window.FactionDescriptionScrollView));
            AgePrimitiveLabel title = OptionsScreen.LabelIn(group);
            if (text == null || group == null || !SettingRows.Drawn(group))
            {
                return;
            }

            AgePrimitiveLabel lore = text;
            AgePrimitiveLabel heading = title;

            // The heading the game drew over the band - the faction's own name - and then the
            // paragraph itself. The game draws this text permanently rather than behind a hover, and
            // text the game always shows is text the player always hears; the buffer keeps the same
            // words as separate lines to walk. The tooltip on the heading ("The background story for
            // this faction") is DECLARED here rather than dropped - it is the row's own, and it reads
            // by its own kind like every other.
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.Label(heading),
                () => AgeText.Label(lore),
                () => Lore(heading, lore),
                AgeWidgets.Raw(AgeWidgets.Transform(title))
            );
            AgeWidgets.PointAt(vtable, AgeWidgets.Transform(title));

            builder.BeginStop(DescriptionStop);
            builder.AddItem(Nodes.Drawn(ControlId.For(text, "faction-choice:description"), vtable, text));
        }

        // ---- the bottom row ----

        /// <summary>Cancel and Select, one node per row, in the order they are drawn - the
        /// cancel-and-confirm bar every screen in the mod walks the same way. Both carry the game's own words; that both
        /// of them in fact commit is on the test script, not pasted over what the game wrote.</summary>
        private void BuildActions(GraphBuilder builder, FactionChoiceModalWindow window)
        {
            AddBand(builder, AgeWidgets.Parent(AgeWidgets.Transform(window.ValidateButton)), "faction-choice:button/");
        }

        /// <summary>Every drawn button of a band, one node per row.</summary>
        private void AddBand(GraphBuilder builder, AgeTransform band, string key)
        {
            _cells.Clear();
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(band);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                _cells.Add(children[i]);
            }

            SettingRows.AddButtons(builder, _cells, key);
        }

        // ---- shared ----

        /// <summary>Push the heading a band was drawn under, if it drew one - and declare it as the
        /// band's first row where the game hung an explanation on it (<see cref="Captions"/>). The
        /// heading is the LABEL inside the band, which is where these prefabs draw it.</summary>
        private static bool Push(GraphBuilder builder, AgeTransform group, string key)
        {
            return Captions.Push(
                builder,
                AgeWidgets.Transform(OptionsScreen.LabelIn(group)),
                key
            );
        }

        /// <summary>The band the grid was drawn in, for the heading over it.</summary>
        private static AgeTransform Group(FactionChoiceModalWindow window)
        {
            return AgeWidgets.Parent(AgeWidgets.Transform(window.FactionsTableScrollView));
        }

        /// <summary>The band's own words, for the review buffer: the heading the game drew over it and
        /// then the paragraph, a line at a time.</summary>
        private static IList<string> Lore(AgePrimitiveLabel heading, AgePrimitiveLabel text)
        {
            List<string> lines = new List<string>();
            SettingRows.Append(lines, AgeText.Lines(AgeText.Label(heading)));
            SettingRows.Append(lines, AgeText.Lines(AgeText.Label(text)));
            return lines;
        }

        private static int Across(AgeTransform first, AgeTransform second)
        {
            return AgeLayout.ReadingOrder(first, second);
        }

        private static FactionChoiceModalWindow Window()
        {
            return GameWindows.Of<FactionChoiceModalWindow>();
        }

        private static T Get<T>(AgeTransform widget)
            where T : UnityEngine.Component
        {
            try
            {
                return widget == null ? null : widget.GetComponent<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}
