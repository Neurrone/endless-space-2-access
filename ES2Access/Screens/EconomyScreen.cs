using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The economy screen: the page F3 opens, with two tabs - what the empire produces and owns, and the
    /// galactic marketplace it can trade on.
    ///
    /// The MARKETPLACE tab is behind a technology, and the game says so on the tab itself rather than
    /// hiding it (<c>EconomyScreen.Refresh</c> :148-168 writes the missing-technology failure into the
    /// toggle's tooltip and switches it off), so the tab is declared REFUSING with the game's own
    /// sentence. Everything behind it is therefore declared off what is DRAWN when it is drawn, and the
    /// order of its panels is read from where they are rather than from a list here: the fixture this was
    /// built against cannot research that technology, so the layout of the marketplace half was never
    /// measured and a hard-coded order would have been a guess. The two halves of the trading panels -
    /// buying and selling - are one shape in the game (<c>MarketplaceTradableItemsPanel</c>) and are read
    /// by one builder here.
    ///
    /// Three things about this page are the game's own model rather than choices:
    ///
    /// - <b>The resource grids are read-only.</b> Every click handler on a luxury or strategic item is
    ///   gated on the developers' god mode (<c>ResourceItem</c>), and the panels themselves answer a
    ///   click with a technology unlock that only happens in god mode
    ///   (<c>TradeCompaniesPanel.OnPanelCb</c>, <c>RecipesPanel.OnPanelCb</c>) - a click the game answers
    ///   with silence stays silent here.
    /// - <b>A resource the empire has not located has no name.</b> The game draws a question mark and
    ///   hangs a sentence saying so where the resource's own dossier would be
    ///   (<c>ResourceItem.SetTooltipProperties</c> - the tooltip's target is only set for a resource the
    ///   empire knows). That sentence is what the item is called here; the name the model would still
    ///   answer for is never spoken, because the player cannot see it.
    /// - <b>Buying is a quantity, not a press.</b> Enter on a market row is the row's own click, which
    ///   both selects the row and adds ONE to the quantity; Ctrl and Shift held with it are the game's
    ///   own multiplier clicks (<c>MarketplacePanel.GetQuantityToAddFromClick</c> reads the physically
    ///   held modifier, so the chords replay the click rather than reimplementing the arithmetic). The
    ///   quantity the game arrived at is spoken back, because the press changes a number somewhere else
    ///   on the screen and nothing else would say so.
    ///
    /// The two tickers along the bottom of the marketplace - the advertisements and the market events -
    /// are a DELIBERATE DEVIATION from reading what is drawn (owner-approved). The game animates them as
    /// a conveyor of pooled items, one visible at a time, each truncated to fit
    /// (<c>MarketplaceAdBanner.QueueNextAd</c>, <c>AdItem.Bind</c>), so what is drawn is a moving
    /// fragment of one row. Each banner is declared instead as the stable, untruncated list the game's
    /// own service holds (<c>ITradingManagementService.GetAdvertisement</c> /
    /// <c>GetEventsFeedback</c>), composed from the same templates the drawn item composes from.
    ///
    /// The Create-advertisement button the ad banner draws is NOT declared: the window it opens
    /// (<c>AdCreationModalWindow</c>) is a class of constants with no controls in this build and is not
    /// registered with the GUI service at all (measured - <c>GetWindow</c> answers null), so the game's
    /// own handler logs an error and opens nothing. The banner is also never shown by
    /// <c>MarketplacePanel</c>, which only ever shows the events one.
    ///
    /// Escape and F3 stay the game's: the page is the game's own and its own close paths work. It is one
    /// of the icon strip's screens, which the engine draws in an exclusive window stack - opening any
    /// other one hides this instantly - which is why they all share a layer.
    /// </summary>
    public sealed partial class EconomyScreen : Screen
    {
        /// <summary>The buy table's line components, swept once per frame. Both questions the price
        /// graph asks - which widget this resource's row is drawn as, and which of the panel's curves
        /// is that row's - walk the same table for the same row on the frame the focused row changes,
        /// and the walk is the expensive half of each. The MATCHING stays per call, because the reveal
        /// between them scrolls the table and the game rebinds which resource each line is showing:
        /// the components are the same either side of that, and what they are bound to is not.
        ///
        /// Switched-off lines are left out, which is the question the two callers were already asking:
        /// the panel builds its curve series from the lines the table is currently showing.</summary>
        private static readonly FrameSweep<GuiTableLineBuyable> BuyLines =
            new FrameSweep<GuiTableLineBuyable>("economy", false);

        private static readonly object TabsStop = "economy:tabs";
        private static readonly object CompaniesStop = "economy:trade-companies";
        private static readonly object LuxuriesStop = "economy:luxuries";
        private static readonly object StrategicsStop = "economy:strategics";
        private static readonly object RecipesStop = "economy:recipes";
        /// <summary>A trading panel is ONE stop with three regions in it, not three stops (owner ruling
        /// 2026-08-30): the filters, the list and the strip the trade is set up in are bands of one box
        /// the game draws with one heading over it, and Tab is what moves between BOXES.</summary>
        private static readonly object BuyStop = "economy:market/buy";
        private static readonly object SellStop = "economy:market/sell";

        /// <summary>The strip a trade is set up in is a stop of its OWN, right after the panel it
        /// belongs to (owner ruling 2026-08-30): it is where the player goes to ACT once they have
        /// found what they came for, and reaching it should not be the length of the list away. Its name
        /// says what is being traded, because that is the one thing the strip's own controls never
        /// repeat.</summary>
        private static readonly object BuyBandStop = "economy:market/buy-transaction";
        private static readonly object SellBandStop = "economy:market/sell-transaction";
        private static readonly object HistoryStop = "economy:market/history";
        private static readonly object TaxesStop = "economy:market/taxes";
        private static readonly object LogStop = "economy:market/log";
        private static readonly object AdsStop = "economy:market/ads";
        private static readonly object EventsStop = "economy:market/events";

        /// <summary>The words the game already has for the marketplace's unlabelled strip: the caption
        /// over its price column, and the titles on the two trade buttons - which the game writes on one
        /// of them and leaves off the two it swaps in for the two currencies.</summary>
        private const string PriceTitleKey = "%MarketplaceScreenHeaderPriceTitle";
        private const string BuyButtonTitleKey = "%MarketplaceScreenBuyButtonTitle";
        private const string SellButtonTitleKey = "%MarketplaceScreenSellButtonTitle";


        /// <summary>The clusters the game draws over every page. They are drawn over this one too.
        /// </summary>
        private readonly GlobalHud _hud = new GlobalHud();

        private readonly TableSheet _buyTable;

        /// <summary>The two boxes the marketplace lets the player type into, which hand the keyboard to
        /// the game's own editor a frame after the request. One editor, because the player can only be
        /// in one box at a time.</summary>
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<GuiPanel> _boards = new List<GuiPanel>();
        private readonly List<AgeTransform> _bands = new List<AgeTransform>();

        /// <summary>The two resource lattices this page draws, one instance each so a build never
        /// measures one grid over the other's.</summary>
        private readonly ResourceGrid _luxuries = new ResourceGrid();
        private readonly ResourceGrid _strategics = new ResourceGrid();

        public EconomyScreen()
        {
            _buyTable = new TableSheet("economy:buy/", TradableOf);
            _buyTable.RowName = TradableName;
            _buyTable.Decorate = MarketRow;
        }

        public override string Key
        {
            get { return ModStrings.ScreenEconomy; }
        }

        /// <summary>Above the view levels it is drawn over, beside the empire summary and the senate: the
        /// strip of icons in the corner opens all of them and the engine's window stack lets only one be
        /// up at a time.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.ScreenTitle("EconomyScreen");
                return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.ScreenEconomy) : title;
            }
        }

        /// <summary>Whichever tab is drawn, because that is what the player opened the page for. The
        /// panels down the left edge and the tab bar itself are a Shift+Tab away.</summary>
        public override object InitialFocusStop
        {
            get { return MarketDrawn(Window()) ? BuyStop : CompaniesStop; }
        }

        /// <summary>A page the player closes and comes straight back to, with the cursor where they left
        /// it.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape is the game's: it closes the screen, which is what the page's own exit does
        /// too.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>While the keyboard is on its way to one of the marketplace's boxes, what the player
        /// types is meant for the box and not for a search of this page.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        /// <summary>Arrival gates on the page being WORKABLE, not just on no modal being up: the renderer
        /// switches the whole background stack off while a modal is showing and back on a frame or more
        /// AFTER the modal reports itself gone, so coming back on "no modal" alone lands the cursor on a
        /// page whose every control is still switched off and reads one passing "unavailable".</summary>
        public override bool IsActive()
        {
            try
            {
                global::EconomyScreen window = Window();
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow
                    && AgeWidgets.Operable(window.AgeTransform);
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
            _editor.Cancel();
            _hud.Forget();
            RestoreCurves();
            _revealed = null;
            _history.Clear();
            _historyKeys.Clear();
        }

        public override void OnUpdate()
        {
            _editor.Update();
            _hud.Update();
            Curves();
        }

        public override void Build(GraphBuilder builder)
        {
            global::EconomyScreen window = Window();
            if (window == null)
            {
                return;
            }

            // Down and across the screen: the empire's banners along the top with this page's own title
            // beside them, the panels down the left edge, the tab bar and whichever tab it is showing,
            // then the right-hand edge and the turn controls.
            _hud.Top(builder);
            BuildSidePanels(builder);
            BuildTabs(builder, window);
            if (MarketDrawn(window))
            {
                BuildMarket(builder, window.MarketplacePanel);
            }
            else
            {
                BuildEconomy(builder, window.EconomyPanel);
            }

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

        // ---- the panels down the left edge ----

        /// <summary>A stop per panel the game is drawing down the left edge, top to bottom, read off what
        /// is in them. Both of this page's own panels write a heading across their top, so the shared
        /// reader's name is the drawn one; the trade panel is only there while the empire can have
        /// trading companies and while the marketplace tab is not the one showing
        /// (<c>EconomyScreen.OnSwitchModeCb</c> :198-212), which is why the list is asked for rather than
        /// remembered.</summary>
        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop("economy:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    _cells.Clear();
                    SidePanels.Readouts(_cells, panel, "economy:side/" + i + "/", null, null);
                    Cells.EmitLinear(builder, _cells);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading the side panels threw: " + e);
            }
        }

        /// <summary>What a panel down the left edge is called. The inflation box writes no heading and
        /// keeps its explanation on the box itself rather than on an icon, so the shared reader has
        /// nothing to call it but its class name - and a stop is announced by its name on every Tab into
        /// it. A word of the mod's is used only where the shared reader came up with nothing.</summary>
        private static string PanelName(SidePanel panel)
        {
            string found = SidePanels.Name(panel);
            if (found != panel.GetType().Name)
            {
                return found;
            }

            if (panel is InflationSidePanel)
            {
                return ModStrings.Get(ModStrings.EconomyInflationPanel);
            }

            return panel is TradeOverviewSidePanel
                ? ModStrings.Get(ModStrings.EconomyTradePanel)
                : found;
        }

        // ---- the tab bar ----

        /// <summary>
        /// The two tabs, in the order they are drawn, read off the strip the game lays them out in rather
        /// than off the window's fields: the window names only one of the two toggles it actually draws
        /// (<c>ToggleSystems</c> is null in a live game - measured) and the other is reached through a
        /// separate transform.
        ///
        /// Enter is the toggle's own click, which swaps the panel underneath in the same frame - so
        /// nothing is spoken for the press beyond the tab saying it is now the selected one. The
        /// marketplace tab refuses while its technology is unresearched and says the game's own sentence
        /// about which technology that is; the instruction to hold Control and click, which the game
        /// appends for a mouse, stays in the review buffer.
        /// </summary>
        private void BuildTabs(GraphBuilder builder, global::EconomyScreen window)
        {
            AgeTransform strip = TabStrip(window);
            if (strip == null)
            {
                return;
            }

            builder.BeginStop(TabsStop);
            _cells.Clear();
            try
            {
                IList<AgeTransform> children = strip.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AddTab(_cells, children[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading the tab bar threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The strip the tab toggles sit in - found from the one toggle the window names, so it
        /// is whatever the prefab actually parents them to.</summary>
        private static AgeTransform TabStrip(global::EconomyScreen window)
        {
            try
            {
                AgeTransform toggle = AgeWidgets.Transform(window.ToggleEconomy);
                return toggle == null ? null : toggle.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddTab(List<Cell> cells, AgeTransform widget, int index)
        {
            if (widget == null)
            {
                return;
            }

            AgeControlToggle toggle = Toggle(widget);
            if (toggle == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform at = AgeWidgets.Transform(toggle);
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            // The marketplace tab is one the game blocks for a missing technology, which it does by
            // leaving the toggle switched on to carry a "why not?" link, so availability is the shared
            // test rather than the enable flag (AgeWidgets.Offered).
            NodeVtable vtable = GraphNodes.Tab(
                () => AgeWidgets.TextOf(at),
                () => it.State,
                () => AgeWidgets.Offered(at),
                tooltip
            );
            vtable.OnActivate = () =>
            {
                if (AgeWidgets.Offered(at))
                {
                    AgeWidgets.Toggle(it);
                }
            };
            AgeWidgets.Point(vtable, it, tooltip, at);
            Cells.Add(cells, at, ControlId.For(toggle, "economy:tab/" + index), vtable);
        }

        private static AgeControlToggle Toggle(AgeTransform widget)
        {
            try
            {
                AgeControlToggle own = widget.GetComponent<AgeControlToggle>();
                return own != null ? own : widget.GetComponentInChildren<AgeControlToggle>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- shared ----

        /// <summary>
        /// The heading the game draws across a box, as the stop's NAME - and as its first node where the
        /// heading carries a sentence explaining the box that exists nowhere else, since a name is a
        /// spoken phrase with no buffer behind it. A bare word gets no node of its own: that is the
        /// shared rule (<see cref="Captions"/>), and a container name the control inside it repeats is
        /// dropped by the announcer, so the box still says what it is either way.
        /// </summary>
        private bool AddHeading(GraphBuilder builder, AgeTransform band, object key)
        {
            AgeTransform heading = band == null ? null : AgeWidgets.ChildNamed(band, "Title", 2);
            return Captions.Push(builder, heading, key, HeadingText(band));
        }

        /// <summary>
        /// Whether the game has named the thing this control is drawing.
        ///
        /// Every one of these grids swaps the tooltip's CLASS with the answer - the resource's own
        /// dossier for something the empire has found, no class and a single sentence saying it has not
        /// been found otherwise (<c>ResourceItem.SetTooltipProperties</c>, <c>SalableItem.SetTooltip</c>,
        /// <c>IngredientSlot.Bind</c>) - and the class is reassigned on every bind. The tooltip's TARGET
        /// is not: the game leaves the last thing's wrapper on it, which is how a slot the player had just
        /// emptied went on calling itself by the component that had been in it. So the class is the test,
        /// and it doubles as the reason the sentence is spoken as the control's NAME rather than as a
        /// tooltip on top of one.
        /// </summary>
        internal static bool Identified(AgeTooltip tooltip)
        {
            return GraphNodes.ModeFor(tooltip) == TooltipMode.Indicate;
        }

        /// <summary>The heading the game drew across a box, or nothing where it drew none - the words a
        /// stop and the table inside it are both named by, read from the one place.</summary>
        private static string HeadingText(AgeTransform band)
        {
            AgeTransform heading = band == null ? null : AgeWidgets.ChildNamed(band, "Title", 2);
            return heading == null ? null : AgeWidgets.TextOf(heading);
        }

        /// <summary>Close the box's name off again, so the next box is not declared inside it - the
        /// shared pop-if-pushed (<see cref="Captions.Pop"/>).</summary>
        private static void Unname(GraphBuilder builder, bool named)
        {
            Captions.Pop(builder, named);
        }

        /// <summary>What a marketplace panel is called: the heading it draws if it draws one, else a word
        /// of the mod's - a stop is announced by its name on every Tab into it. All four of these panels
        /// DO draw one; the mod's words are the fallback for the frames before the game has written
        /// them.</summary>
        private static string PanelName(GuiPanel panel, string modKey)
        {
            try
            {
                AgeTransform heading = PanelCaption(panel);
                string drawn = heading == null ? null : AgeWidgets.TextOf(heading);
                return string.IsNullOrEmpty(drawn) ? ModStrings.Get(modKey) : drawn;
            }
            catch (Exception)
            {
                return ModStrings.Get(modKey);
            }
        }

        /// <summary>The label a marketplace panel writes its own name into. The prefabs call it
        /// "PanelTitle"; "Title" is what the economy tab's boxes call theirs, and both are handed to
        /// the shared search (<see cref="WindowShape.TitleWidget"/>) so one lookup answers for both.
        /// </summary>
        private static AgeTransform PanelCaption(GuiPanel panel)
        {
            try
            {
                return panel == null
                    ? null
                    : WindowShape.TitleWidget(panel.AgeTransform, PanelTitleNames);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly string[] PanelTitleNames = { "PanelTitle", "Title" };

        /// <summary>The heading the game writes across a marketplace panel, as that panel's first line -
        /// but only where it carries the sentence explaining the panel, which is the standing rule for a
        /// caption: a bare word is the block's NAME, and a node that repeats it is a step past nothing.
        /// </summary>
        private static void AddPanelCaption(List<Cell> cells, GuiPanel panel, string key)
        {
            AgeTransform caption = PanelCaption(panel);
            AgeTooltip tooltip = caption == null ? null : AgeWidgets.Raw(caption);
            // A content read, and the same one <see cref="Captions"/> makes: whether the game is drawing
            // the caption is what decides whether the sentence on it exists to be offered at all.
            if (
                caption != null
                && AgeWidgets.Visible(caption)
                && tooltip != null
                && AgeWidgets.Draws(tooltip)
            )
            {
                Cells.AddReadout(cells, caption, key);
            }
        }

        private static void Band(List<AgeTransform> bands, AgeTransform widget)
        {
            // Which bands enter the page's dispatch list, in drawn order - a collection made before
            // anything is declared, and this tests the wrapping GROUP rather than the panel each
            // builder gates on for itself.
            if (widget != null && AgeWidgets.Visible(widget) && !bands.Contains(widget))
            {
                bands.Add(widget);
            }
        }

        /// <summary>The box a panel is drawn inside, which is what carries its heading.</summary>
        private static AgeTransform GroupOf(GuiPanel panel)
        {
            try
            {
                AgeTransform at = panel == null ? null : panel.AgeTransform;
                return at == null ? null : at.Parent ?? at;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Board(List<GuiPanel> boards, GuiPanel panel)
        {
            // Which boards enter the marketplace's dispatch list - the same collection question.
            if (panel != null && AgeWidgets.Visible(panel.AgeTransform))
            {
                boards.Add(panel);
            }
        }

        private static readonly Comparison<GuiPanel> PanelsInReadingOrder = (left, right) =>
            AgeLayout.TopThenLeft(left.AgeTransform, right.AgeTransform);

        // ---- reading the window ----

        /// <summary>Whether the marketplace is the tab being drawn. Asked of what is on screen rather
        /// than of the window's remembered tab, which the game restores from a field other screens
        /// write.</summary>
        private static bool MarketDrawn(global::EconomyScreen window)
        {
            try
            {
                // Flow control: whether the marketplace tab's whole subtree is walked at all.
                return window != null
                    && window.MarketplacePanel != null
                    && AgeWidgets.Visible(window.MarketplacePanel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The thing a market row stands for. The wrapper the table binds is rebuilt whenever
        /// the section refreshes and the line widget is pooled, so it is the tradable underneath that
        /// identifies the row.</summary>
        private static readonly TableSheet.RowObject TradableOf = TableSheet.Model<IGuiTradable>(
            tradable =>
            {
                GuiBuyable buyable = tradable as GuiBuyable;
                return buyable != null ? (object)buyable.Tradable : tradable;
            }
        );

        /// <summary>What a market row is called when its name column draws nothing.</summary>
        private static readonly TableSheet.RowLabel TradableName =
            TableSheet.Name<IGuiTradable>(tradable => tradable.Title);

        private static global::EconomyScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::EconomyScreen>(false)
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
