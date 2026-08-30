using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

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
    public sealed class EconomyScreen : Screen
    {
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

        /// <summary>The resources the price-history table declared a row for, in the order it declared
        /// them - what turns the focused ROW back into the thing whose curve the game draws.</summary>
        private readonly List<GuiBuyable> _history = new List<GuiBuyable>();

        /// <summary>One token per tradable, so a history row keys and reconciles by the RESOURCE without
        /// carrying the resource itself: the buy table's own rows already carry that object, and two
        /// nodes sharing one reference are one control to the cursor.</summary>
        private readonly Dictionary<ulong, object> _historyKeys = new Dictionary<ulong, object>();

        /// <summary>Whether this screen is currently holding the price graph's curves dimmed. Only ever
        /// true while the cursor is on a history row, and the restore is the game's own call.</summary>
        private bool _dimmed;

        /// <summary>Which history row the buy table was last scrolled FOR - the row's own token, so a
        /// rebuilt wrapper is still the same row. It is what keeps the scroll to the moment focus
        /// ARRIVES: a scroll re-issued every frame would fight a hand on the wheel.</summary>
        private object _revealed;

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
            get { return "screen.economy"; }
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
                string title = ScreenTitle();
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

        // ---- the trade and resources tab ----

        /// <summary>The four boxes of the first tab, in the order they are read - the trading companies
        /// down the left, the resource grids and the development projects down the right. The order comes
        /// from where they are drawn, because whether the strategics grid is there at all depends on the
        /// empire (<c>EconomyPanel.Refresh</c> :160-175) and it moves what is under it.</summary>
        private void BuildEconomy(GraphBuilder builder, EconomyPanel panel)
        {
            // Flow control: the four boxes under it are each collected and walked.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            _bands.Clear();
            Band(_bands, GroupOf(panel.TradeCompaniesPanel));
            Band(_bands, GroupOf(panel.LuxuriesPanel));
            Band(_bands, panel.StrategicsGroup);
            Band(_bands, GroupOf(panel.RecipesPanel));
            _bands.Sort(InReadingOrder);

            for (int i = 0; i < _bands.Count; i++)
            {
                AgeTransform band = _bands[i];
                if (ReferenceEquals(band, GroupOf(panel.TradeCompaniesPanel)))
                {
                    BuildCompanies(builder, panel.TradeCompaniesPanel, band);
                }
                else if (ReferenceEquals(band, GroupOf(panel.LuxuriesPanel)))
                {
                    BuildResources(
                        builder,
                        LuxuriesStop,
                        band,
                        panel.LuxuriesPanel,
                        panel.LuxuryResourcesHeaderTable,
                        ResourceDefinition.Type.Luxury,
                        EconomyPanel.LuxuriesResourcesFamiliesNumber,
                        _luxuries
                    );
                }
                else if (ReferenceEquals(band, panel.StrategicsGroup))
                {
                    BuildResources(
                        builder,
                        StrategicsStop,
                        band,
                        panel.StrategicsPanel,
                        panel.StrategicResourcesHeaderTable,
                        ResourceDefinition.Type.Strategic,
                        EconomyPanel.StrategicResourcesFamiliesNumber,
                        _strategics
                    );
                }
                else
                {
                    BuildRecipes(builder, panel.RecipesPanel, band);
                }
            }
        }

        /// <summary>
        /// The trading companies, or the one line saying there are none.
        ///
        /// That line is declared BY NAME rather than left to the shape reader: the panel it sits in draws
        /// nothing else, so the shape reader takes the panel as one line and the panel carries no tooltip
        /// - which loses the technology the game names as the thing that is missing. The label carries it
        /// (<c>TradeCompaniesPanel.Refresh</c> :164-172), so the label is the node.
        ///
        /// The companies themselves are read from the shape of what is drawn rather than modelled line by
        /// line: a company draws its name, level, headquarters, income and route counts as a strip of
        /// captioned groups, and the two things it can be TOLD to do - be renamed, and have one of its
        /// two improvements bought - are drawn as controls inside that strip, so they come out as
        /// controls. The panel's own click answers with a technology unlock that only happens in the
        /// developers' god mode (<c>OnPanelCb</c> :215-222), so it is never declared as a button.
        /// </summary>
        private void BuildCompanies(
            GraphBuilder builder,
            TradeCompaniesPanel panel,
            AgeTransform band
        )
        {
            builder.BeginStop(CompaniesStop);
            bool named = AddHeading(builder, band, "economy:companies/heading");
            _cells.Clear();
            try
            {
                if (panel != null)
                {
                    Cells.AddReadout(
                        _cells,
                        panel.NoTradeCompaniesLabel == null
                            ? null
                            : panel.NoTradeCompaniesLabel.AgeTransform,
                        "economy:companies/none"
                    );
                    SidePanels.Content(
                        _cells,
                        panel.TradeCompanyLinesTablesTable,
                        "economy:companies/",
                        null,
                        null
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading the trading companies threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        /// <summary>
        /// One of the resource grids, read as the table it is drawn as: a row of family icons across the
        /// top, then the resources the empire can see, laid out in the column of the family they belong
        /// to.
        ///
        /// The icons ARE the columns' headings - measured: each sits centred over one column of items -
        /// and each is named by the effect its family improves, taken from the game's own list in the
        /// order the game filled the header table from it (<c>EconomyPanel.RefreshResourcesTables</c>
        /// :187-206). The FAMILY IS MEANINGFUL: the game's own data gives every eighth luxury the same
        /// target effect (<c>GuiElements[Luxuries].xml</c>, <c>RecipeIngredientDefinitions.xml</c>), so
        /// the resource under the Food heading is the one that improves Food - which is why the family
        /// is what the columns are.
        ///
        /// The lattice is SPARSE: the panel keeps every resource in the table and fades the ones the
        /// empire has nothing of (<c>ResourcesPanel.RefreshResourceItem</c> :190-225), which is why the
        /// drawn test asks for alpha and not just visibility - the game leaves those items Visible and
        /// simply makes them invisible. Measured on the luxury grid, turn 21 of the beginner save: 24
        /// items in three lines of eight, the whole third line at alpha 0 and four of the second.
        ///
        /// What the table reading is, and how the families are both its column captions and a header
        /// ROW above the first line, is <see cref="ResourceGrid"/>. BOTH grids read that way (owner
        /// ruling 2026-08-19, amended 2026-08-21): they are the same lattice of the same shape, and a
        /// player who has learnt one has learnt the other.
        /// </summary>
        private void BuildResources(
            GraphBuilder builder,
            object stop,
            AgeTransform band,
            ResourcesPanel panel,
            AgeTransform headers,
            ResourceDefinition.Type type,
            int families,
            ResourceGrid grid
        )
        {
            // Flow control: the grid below reads a whole lattice of cells.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(stop);
            // The panel's own caption belongs to the same region as the table under it. Set BEFORE the
            // heading rather than in the emitter, because a stop that regions everything except its
            // first node is a stop where the jump key does nothing exactly where the player lands.
            builder.SetRegion(stop + "/legend");
            bool named = AddHeading(builder, band, stop + "/heading");
            string[] columns = null;
            try
            {
                columns = FamilyNames(type, families, grid.Columns(headers));
                grid.Read(panel.ResourceItemsTable, ResourceCell);
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading a resource grid threw: " + e);
            }

            grid.Emit(builder, columns, stop, HeadingText(band));
            Unname(builder, named);
        }

        /// <summary>What each family is called, off the game's own registry, in the order the game
        /// filled the heading table from it. Shared with the recipe window, which draws the same grid of
        /// the same families.</summary>
        internal static string[] FamilyNames(
            ResourceDefinition.Type type,
            int families,
            int drawn
        )
        {
            string[] names = new string[Math.Max(drawn, families)];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = FamilyName(type, families, i);
            }

            return names;
        }

        /// <summary>
        /// What a resource family's column is called - the resource it improves, in the game's own words
        /// for it.
        ///
        /// The heading is drawn as an ICON and nothing else (<c>EconomyPanel.RefreshResourceHeader</c>
        /// :177-185 sets an image, a tint and a tooltip - no label), so what it says is the name of the
        /// thing pictured: "Industry". The family's own title is a sentence about what the family does
        /// ("Improves Industry", and one of them carries an icon typo that reads a second resource's
        /// name), which is a description - it stays where the game puts it, on the heading's tooltip.
        ///
        /// A family with no short name of its own - the compound strategic ones - keeps that title, which
        /// is the only thing the game says about them. A name the corpus never wrote comes back as its
        /// own key - parked text, which is not a name to speak.
        ///
        /// Shared with the recipe window, which draws the same grid of the same families: the copy it
        /// had went straight to the title and so read the family DESCRIPTION - including the icon typo
        /// ("Improves Industry Food") this one exists to step around.
        /// </summary>
        internal static string FamilyName(ResourceDefinition.Type type, int families, int index)
        {
            try
            {
                GuiResource resource = NthResource(type, families, index);
                if (resource == null)
                {
                    return null;
                }

                string improved = ImprovedResource(resource.TargetEffect.ToString());
                if (improved != null)
                {
                    return improved;
                }

                Amplitude.Unity.Gui.ExtendedGuiElement element =
                    Gui.GetExtendedGuiElement(resource.TargetEffect);
                string title = element == null ? null : AgeText.Clean(Gui.Localize(element.Title));
                return string.IsNullOrEmpty(title) || title[0] == '%' ? null : title;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What each resource family improves, as the game's own short titles for those
        /// resources - the words the rest of the game calls Food and Manpower by. Keyed by the family's
        /// target effect, which is the only thing that says which resource a family is for; a family the
        /// game has no single word for (two resources at once, or an effect that is not a resource at
        /// all) is absent and reads its sentence instead.</summary>
        private static readonly Dictionary<string, string[]> ImprovedResources =
            new Dictionary<string, string[]>
            {
                { "TargetEffectFood", new[] { "%SubCategoryFoodTitle" } },
                { "TargetEffectIndustry", new[] { "%SubCategoryIndustryTitle" } },
                { "TargetEffectDust", new[] { "%SubCategoryDustTitle" } },
                { "TargetEffectScience", new[] { "%SubCategoryScienceTitle" } },
                { "TargetEffectEmpirePoint", new[] { "%SubCategoryInfluenceTitle" } },
                { "TargetEffectHappiness", new[] { "%SubCategoryApprovalTitle" } },
                { "TargetEffectMilitary", new[] { "%CategoryManpowerTitle" } },
                { "TargetEffectTrade", new[] { "%SubCategoryTradeTitle" } },
                { "TargetEffectHonor", new[] { "%HonorTitle" } },
                {
                    "TargetEffectFoodIndustry",
                    new[] { "%SubCategoryFoodTitle", "%SubCategoryIndustryTitle" }
                },
                {
                    "TargetEffectDustScience",
                    new[] { "%SubCategoryDustTitle", "%SubCategoryScienceTitle" }
                },
                {
                    "TargetEffectFoodIndustryDustScience",
                    new[]
                    {
                        "%SubCategoryFoodTitle",
                        "%SubCategoryIndustryTitle",
                        "%SubCategoryDustTitle",
                        "%SubCategoryScienceTitle",
                    }
                },
            };

        /// <summary>See <see cref="ImprovedResources"/>. Null for a family that is not in it, and for one
        /// whose names the corpus never wrote.</summary>
        private static string ImprovedResource(string targetEffect)
        {
            string[] keys;
            if (
                string.IsNullOrEmpty(targetEffect)
                || !ImprovedResources.TryGetValue(targetEffect, out keys)
            )
            {
                return null;
            }

            MessageBuilder names = new MessageBuilder();
            for (int i = 0; i < keys.Length; i++)
            {
                string name = AgeText.Clean(Gui.Localize(keys[i]));
                if (string.IsNullOrEmpty(name) || name[0] == '%')
                {
                    return null;
                }

                names.ListItem(name);
            }

            string said = names.Build();
            return string.IsNullOrEmpty(said) ? null : said;
        }

        private static GuiResource NthResource(
            ResourceDefinition.Type type,
            int families,
            int index
        )
        {
            System.Collections.Generic.IList<GuiResource> all = Gui.GuiWrapperProviderService.GuiResources;
            int found = 0;
            for (int i = 0; all != null && i < all.Count && found < families; i++)
            {
                if (all[i] != null && all[i].ResourceType == type)
                {
                    if (found == index)
                    {
                        return all[i];
                    }

                    found++;
                }
            }

            return null;
        }

        /// <summary>
        /// One resource in a grid: what it is, how much of it there is and what it is earning per turn.
        ///
        /// The holding and the per-turn change are two adjacent numbers ("0", "+0") with nothing on the
        /// row saying which is which (<c>ResourceItem.Refresh</c> :124-142), so they read as ONE value
        /// through the same template the empire banner's own stocks use - the game leaves the net label
        /// empty for a resource it has no figure for, and then the holding reads alone.
        ///
        /// A resource the empire has not located yet has NO name here. The game draws a question mark
        /// for it and, instead of the resource's own dossier, hangs the one sentence saying it has not
        /// been found (<c>ResourceItem.SetTooltipProperties</c> - the target is set only for a resource
        /// the empire knows). That sentence is what the item is called, and the readout drops that line
        /// from the tooltip it goes on to announce.
        ///
        /// The cell never says which family it belongs to: the column it sits in is announced as the
        /// edge the player crossed to reach it - or, on a landing that crossed no edge, as the column
        /// heading itself - and a word on the cell as well would say it twice. What the family DOES is
        /// on the heading node a step above the column (<see cref="ResourceGrid"/>), not in this cell's
        /// buffer, which holds the resource's own dossier and nothing else.
        /// </summary>
        private static NodeVtable ResourceCell(AgeTransform widget)
        {
            ResourceItem item = widget == null ? null : widget.GetComponent<ResourceItem>();
            if (item == null || !SettingRows.Drawn(widget))
            {
                return null;
            }

            ResourceItem it = item;
            AgeTooltip tooltip = item.Tooltip ?? AgeWidgets.Raw(widget);
            bool named = Identified(tooltip);
            string label = named
                ? AgeWidgets.TooltipTitle(tooltip)
                : CardActions.FirstLine(tooltip);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => label),
                    GraphNodes.ValuePart(() => StockAndNet(it.StockLabel, it.NetLabel)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return vtable;
        }


        /// <summary>A stock and what the next turn does to it, as the game drew the two numbers - the
        /// same phrasing the empire banner reads its own stocks with, so the second figure is heard as a
        /// rate rather than as a second holding.</summary>
        private static string StockAndNet(AgePrimitiveLabel stock, AgePrimitiveLabel net)
        {
            string held = AgeText.Label(stock);
            string rate = AgeText.Label(net);
            if (string.IsNullOrEmpty(rate))
            {
                return held;
            }

            return ModStrings.Format(ModStrings.GalaxyStockAndNet, held, rate);
        }

        /// <summary>
        /// The system development projects: one line per slot the empire has, or the one line saying it
        /// has none.
        ///
        /// A slot already holding a project is drawn with its button switched off
        /// (<c>RecipeLine.Bind</c> :50-64) - it is a readout of what the project is and what went into
        /// it - and an empty one the empire could fill draws "click to create" and opens the project
        /// window. Enter is that button either way, so a slot the game will not let the player fill
        /// refuses with whatever the game wrote on it.
        /// </summary>
        private void BuildRecipes(GraphBuilder builder, RecipesPanel panel, AgeTransform band)
        {
            // Flow control: a stop and a heading context would be opened around nothing, and every
            // project line under the panel would be read first.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(RecipesStop);
            bool named = AddHeading(builder, band, "economy:recipes/heading");
            _cells.Clear();
            try
            {
                Cells.AddReadout(
                    _cells,
                    panel.NoAvailableRecipeLabel == null
                        ? null
                        : panel.NoAvailableRecipeLabel.AgeTransform,
                    "economy:recipes/none"
                );
                AgeTransform table = panel.RecipeLinesTable;
                IList<AgeTransform> lines = table == null ? null : table.Children;
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    AddRecipeLine(_cells, lines[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading the development projects threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        private static void AddRecipeLine(List<Cell> cells, AgeTransform widget, int index)
        {
            RecipeLine line = widget == null ? null : widget.GetComponent<RecipeLine>();
            if (line == null)
            {
                return;
            }

            RecipeLine it = line;
            AgeTransform press = AgeWidgets.Transform(line.LineButton);
            AgeTooltip tooltip = line.Tooltip ?? AgeWidgets.Raw(widget);
            Func<bool> offered = () => press != null && AgeWidgets.Operable(press);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.RecipeTitleLabel)),
                    GraphNodes.ValuePart(() => Creatable(it)),
                    GraphNodes.DisabledPart(offered),
                },
                Sections = GraphNodes.Sections(() => Ingredients(it), tooltip),
                OnActivate = () =>
                {
                    if (offered())
                    {
                        AgeWidgets.Press(it.LineButton);
                    }
                },
            };
            GraphNodes.AddRefusal(vtable, tooltip, offered);

            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, "economy:recipe/" + index), vtable);
        }

        /// <summary>The invitation the game writes on an empty slot the empire could fill, and nothing on
        /// a slot already holding a project.</summary>
        private static string Creatable(RecipeLine line)
        {
            try
            {
                return AgeWidgets.DrawnLabel(line.ClickToCreateRecipeTitleLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What went into a project, off the row of slots the line draws - each one a picture
        /// with the resource's own words on it and nothing written.</summary>
        private static IList<string> Ingredients(RecipeLine line)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform table = line.IngredientSlotsTable;
                IList<AgeTransform> slots = table == null ? null : table.Children;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    AgeTransform slot = slots[i];
                    if (slot == null || !SettingRows.Drawn(slot))
                    {
                        continue;
                    }

                    AgeTooltip tooltip = AgeWidgets.Raw(slot);
                    string said =
                        AgeWidgets.TooltipTitle(tooltip) ?? CardActions.FirstLine(tooltip);
                    if (!string.IsNullOrEmpty(said) && !lines.Contains(said))
                    {
                        lines.Add(said);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading a project's components threw: " + e);
            }

            return lines;
        }

        // ---- the marketplace tab ----

        /// <summary>The marketplace's panels, in the order they are drawn. Read from where they are
        /// rather than from a list here: this half of the page is behind a technology the fixture cannot
        /// research, so its layout was never measured and any fixed order would be a guess.</summary>
        private void BuildMarket(GraphBuilder builder, MarketplacePanel panel)
        {
            _boards.Clear();
            Board(_boards, panel.BuyableItemsPanel);
            Board(_boards, panel.SalableItemsPanel);
            Board(_boards, panel.TaxesPanel);
            Board(_boards, panel.ExchangeInformationsPanel);
            Board(_boards, panel.EventsBanner);
            Board(_boards, panel.AdsBanner);
            _boards.Sort(PanelsInReadingOrder);

            for (int i = 0; i < _boards.Count; i++)
            {
                GuiPanel board = _boards[i];
                try
                {
                    MarketplaceBuyableItemsPanel buy = board as MarketplaceBuyableItemsPanel;
                    if (buy != null)
                    {
                        BuildBuy(builder, buy);
                        continue;
                    }

                    MarketplaceSalableItemsPanel sell = board as MarketplaceSalableItemsPanel;
                    if (sell != null)
                    {
                        BuildSell(builder, sell);
                        continue;
                    }

                    MarketplaceTaxesPanel taxes = board as MarketplaceTaxesPanel;
                    if (taxes != null)
                    {
                        BuildTaxes(builder, taxes);
                        continue;
                    }

                    MarketplaceExchangeInformationsPanel log =
                        board as MarketplaceExchangeInformationsPanel;
                    if (log != null)
                    {
                        BuildLog(builder, log);
                        continue;
                    }

                    MarketplaceEventsBanner events = board as MarketplaceEventsBanner;
                    if (events != null)
                    {
                        BuildEvents(builder, events);
                        continue;
                    }

                    BuildAds(builder, board as MarketplaceAdBanner);
                }
                catch (Exception e)
                {
                    Log.Warn("economy: reading " + board.GetType().Name + " threw: " + e);
                }
            }
        }

        /// <summary>
        /// The buying half, as ONE stop with the three bands the game draws down it as regions: the
        /// heading, the section filters, the list of what is on offer, and the strip the trade is set up
        /// in.
        ///
        /// The list stays a real TABLE (owner ruling 2026-08-30, reversing a same-day ruling that had
        /// made it one node per row): the game binds this <c>GuiTable</c> to the column set its SECTION
        /// declares - three for the resource sections, ten for ships and heroes
        /// (<c>Public/Gui/GuiElements[Marketplace].xml</c>) - so the columns are a fact of the game's own
        /// data and a walk across them is a walk across what the game says about each offer. Nothing here
        /// counts columns: the shared table reading pairs each cell to the heading the game drew over it,
        /// so a section with ten of them inherits ten.
        ///
        /// The price graph is declared AFTER this stop rather than where the game draws it, between the
        /// filters and the table (owner ruling 2026-08-30): it is a page of its own to read, and putting
        /// it in the middle of the buying walk makes the table the far side of it.
        /// </summary>
        private void BuildBuy(GraphBuilder builder, MarketplaceBuyableItemsPanel panel)
        {
            builder.BeginStop(BuyStop);
            builder.PushContext(AgeText.Clean(Gui.Localize(BuyButtonTitleKey)));
            AddCaption(builder, panel, "economy:buy/");
            BuildSections(builder, panel, "economy:buy/");

            GuiTable table = panel.BuyableItemsGuiTable;
            // Flow control: the shared table reading walks every line of it, and a region would be
            // opened around nothing.
            if (table != null && AgeWidgets.Visible(table.AgeTransform))
            {
                // The band's name and its role are pushed HERE rather than left to the sheet, because
                // the sort headers are part of it and the sheet opens after them; the sheet is then
                // given no title of its own, so the region is announced once, on the heading row the
                // jump lands on. It still sets the stop's landing on the first data row.
                builder.SetRegion("economy:buy/available");
                builder.PushContext(
                    ModStrings.Get(ModStrings.EconomyAvailable),
                    GraphSheet.TableRoleText == null ? null : GraphSheet.TableRoleText()
                );
                _buyTable.Headers(builder, table);
                _buyTable.Rows(builder, table, null);
                builder.PopContext();
            }

            builder.PopContext();

            BuildBand(builder, panel, BuyBandStop, "economy:buy/", BuyButtonTitleKey, BandName(panel));
            BuildHistory(builder, panel);
        }

        /// <summary>The selling half, the same three bands in one stop. Its items are not a table at all
        /// - they are plain toggles the panel pools
        /// (<c>MarketplaceSalableItemsPanel.RefreshTradableItemsTable</c> :117-130) - and they read one
        /// per row, as the buying list beside them now does: the dense strip the game wraps them into is
        /// a rendering accident, and walking it sideways bought nothing.</summary>
        private void BuildSell(GraphBuilder builder, MarketplaceSalableItemsPanel panel)
        {
            builder.BeginStop(SellStop);
            builder.PushContext(AgeText.Clean(Gui.Localize(SellButtonTitleKey)));
            AddCaption(builder, panel, "economy:sell/");
            BuildSections(builder, panel, "economy:sell/");

            AgeTransform table = panel.SalableItemsTable;
            // Flow control: a region and a context would be opened around nothing, and every item under
            // the table would be read in the band above.
            if (table != null && AgeWidgets.Visible(table))
            {
                builder.SetRegion("economy:sell/available");
                builder.PushContext(ModStrings.Get(ModStrings.EconomyAvailable));
                _cells.Clear();
                IList<AgeTransform> items = table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AddSalableItem(_cells, panel, items[i], i);
                }

                Cells.EmitLinear(builder, _cells);
                builder.PopContext();
                if (_cells.Count > 0)
                {
                    builder.LandStopOn(_cells[0].Id);
                }
            }

            builder.PopContext();

            BuildBand(builder, panel, SellBandStop, "economy:sell/", SellButtonTitleKey, BandName(panel));
        }

        /// <summary>The panel's own drawn heading, in a region of its own so the stop is regioned all
        /// the way through and the jump out of the filters reaches it.</summary>
        private void AddCaption(GraphBuilder builder, GuiPanel panel, string keyPrefix)
        {
            _cells.Clear();
            AddPanelCaption(_cells, panel, keyPrefix + "title");
            if (_cells.Count == 0)
            {
                return;
            }

            builder.SetRegion(keyPrefix + "heading");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The section radios - strategic resources, luxuries, ships, heroes - filtered and
        /// ordered by the game (<c>BuildGuiTradableSections</c>). A section the empire may not trade in
        /// is drawn switched off with the game's own reason on it, and stays declared while it refuses:
        /// which markets exist, and why not this one, is what the player came here to find out.
        ///
        /// Each label opens with the category's ICON, which reads as the word the engine's symbol
        /// registry gives it - and for the strategics section that word is "Titanium", the name of one
        /// of the resources in it. The leading icon is dropped for the same reason it is dropped
        /// everywhere else: it is a picture standing beside the words, not a word of them.</summary>
        private void BuildSections(
            GraphBuilder builder,
            MarketplaceTradableItemsPanel panel,
            string keyPrefix
        )
        {
            AgeTransform table = panel.MarketTabRadiosTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            // Flow control: same - a region would be opened around nothing and every radio read in the
            // band above it.
            if (children == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            builder.SetRegion(keyPrefix + "filters");
            builder.PushContext(ModStrings.Get(ModStrings.EconomyFilters));
            _cells.Clear();
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                MarketTabRadio radio =
                    widget == null ? null : widget.GetComponent<MarketTabRadio>();
                if (radio == null || radio.Toggle == null)
                {
                    continue;
                }

                MarketTabRadio it = radio;
                AgeTransform at = AgeWidgets.Transform(radio.Toggle);
                AgeTooltip tooltip = radio.Tooltip ?? AgeWidgets.Raw(widget);
                // A section the empire may not trade in is blocked the same way the marketplace tab is
                // (<c>MarketTabRadio.Bind</c> :19-31), so the shared availability test rather than the
                // enable flag alone.
                Func<bool> offered = () => AgeWidgets.Offered(at);
                NodeVtable vtable = GraphNodes.Radio(
                    () => AgeText.LabelWithoutLeadingIcon(it.Label),
                    () => it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    offered,
                    null,
                    tooltip
                );
                GraphNodes.AddRefusal(vtable, tooltip, offered);

                AgeWidgets.Point(vtable, radio.Toggle, tooltip, widget);
                Cells.Add(
                    _cells,
                    widget,
                    ControlId.For(widget, keyPrefix + "filter/" + i),
                    vtable
                );
            }

            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        /// <summary>
        /// One thing the empire could sell. The item draws a picture and a stock figure and nothing else,
        /// so it is named by the wrapper the game hangs on its tooltip - and for a luxury the empire has
        /// never located, by the one sentence the game writes there instead
        /// (<c>SalableItem.SetTooltip</c>).
        ///
        /// Enter is the item's own click: it makes this the selection and adds to the quantity, with Ctrl
        /// and Shift the game's own multipliers.
        /// </summary>
        private void AddSalableItem(
            List<Cell> cells,
            MarketplaceSalableItemsPanel panel,
            AgeTransform widget,
            int index
        )
        {
            SalableItem item = widget == null ? null : widget.GetComponent<SalableItem>();
            if (item == null || !SettingRows.Drawn(widget) || item.SelectionToggle == null)
            {
                return;
            }

            SalableItem it = item;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            string label = SalableName(widget);
            Func<bool> offered = () => AgeWidgets.Operable(widget);
            NodeVtable vtable = GraphNodes.Radio(
                () => label,
                () => it.SelectionToggle.State,
                () => AgeWidgets.Toggle(it.SelectionToggle),
                offered,
                null,
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(it.StockLabel)));
            MarketplaceSalableItemsPanel owner = panel;
            MarketGestures(
                vtable,
                () => AgeWidgets.Toggle(it.SelectionToggle),
                offered,
                () => QuantityText(owner)
            );
            AgeWidgets.Point(vtable, it.SelectionToggle, tooltip, widget);
            Cells.Add(cells, widget, ControlId.For(widget, "economy:salable/" + index), vtable);
        }

        /// <summary>
        /// The gestures a market row has beyond its plain click: the game's own multiplier clicks.
        ///
        /// <c>MarketplacePanel.GetQuantityToAddFromClick</c> (:124-136) reads the modifier the player is
        /// physically holding, so the chords replay the SAME click rather than reimplementing the
        /// arithmetic - Ctrl five at a time, Shift the whole stock - and the game's clamping and its
        /// selection rules apply unchanged. The quantity it arrived at is spoken back, because the press
        /// changes a number in the strip along the bottom of the panel and nothing else would say so.
        /// </summary>
        private void MarketGestures(
            NodeVtable vtable,
            Action click,
            Func<bool> offered,
            Func<string> quantity
        )
        {
            Action press = click;
            Func<bool> can = offered;
            Func<string> landed = quantity;
            Action guarded = () =>
            {
                if (can())
                {
                    press();
                }
            };
            vtable.OnSelectToggle = guarded;
            vtable.OnSelectRange = guarded;
            vtable.StateText = () => can() ? landed() : null;
            MarketChordHints(vtable, can);
        }

        /// <summary>The two multiplier chords, said in the buffer of everything that has them - the
        /// sellable tiles, the buy table.s rows and the strip.s own steppers. The game.s tooltip on some
        /// of those already says what a modified CLICK does; what the keyboard.s chords do is the mod.s
        /// to say, and it is said in one place so no surface offering the gesture can forget it.
        /// </summary>
        private static void MarketChordHints(NodeVtable vtable, Func<bool> offered)
        {
            NodeHints.Add(vtable, ModStrings.HintMarketFive, UiActions.SelectToggle, 0, offered);
            NodeHints.Add(vtable, ModStrings.HintMarketAll, UiActions.SelectRange, 0, offered);
        }

        /// <summary>
        /// What the strip is currently set up to trade, which is what its stop is called: nothing else
        /// in the strip says WHICH resource its price and its total belong to, and a stop the player
        /// tabs into has to name itself.
        ///
        /// A strip with nothing picked keeps a name of the mod's own rather than none: the stop is
        /// there, it refuses, and a nameless one would announce a bare role word.
        /// </summary>
        private static string BandName(MarketplaceTradableItemsPanel panel)
        {
            bool buying = panel is MarketplaceBuyableItemsPanel;
            string what = SelectedName(panel);
            return string.IsNullOrEmpty(what)
                ? ModStrings.Get(
                    buying
                        ? ModStrings.EconomyBuyTransaction
                        : ModStrings.EconomySellTransaction
                )
                : ModStrings.Format(
                    buying ? ModStrings.EconomyBuyingWhat : ModStrings.EconomySellingWhat,
                    what
                );
        }

        /// <summary>
        /// The thing the panel has picked, in the words its own list calls it by.
        ///
        /// The buying half is the game's own answer: it writes the picked buyable's title across the
        /// left of the strip and empties that label again when nothing is picked
        /// (<c>RefreshBottomGroup</c> :185-207). The selling half draws no such label, so the picked
        /// TILE is asked - and asked through <see cref="SalableName"/>, so a luxury the empire has never
        /// located is named here by the same sentence the tile is named by rather than by the name the
        /// player is not allowed to have.
        /// </summary>
        private static string SelectedName(MarketplaceTradableItemsPanel panel)
        {
            try
            {
                MarketplaceBuyableItemsPanel buy = panel as MarketplaceBuyableItemsPanel;
                if (buy != null)
                {
                    return AgeText.Label(buy.SelectedBuyableNameLabel);
                }

                MarketplaceSalableItemsPanel sell = panel as MarketplaceSalableItemsPanel;
                AgeTransform table = sell == null ? null : sell.SalableItemsTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    SalableItem item =
                        items[i] == null ? null : items[i].GetComponent<SalableItem>();
                    if (
                        item != null
                        && item.SelectionToggle != null
                        && item.SelectionToggle.State
                        && SettingRows.Drawn(items[i])
                    )
                    {
                        return SalableName(items[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading what is being traded threw: " + e);
            }

            return null;
        }

        /// <summary>What a sellable tile is called: the game's own wrapper title where the empire has
        /// located the resource, and the one sentence the game writes in its place where it has not
        /// (<c>SalableItem.SetTooltip</c>). Decided in ONE place, so the tile and the name of the strip
        /// that trades it cannot disagree about a resource the player may not be told the name of.
        /// </summary>
        private static string SalableName(AgeTransform widget)
        {
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            return Identified(tooltip)
                ? AgeWidgets.TooltipTitle(tooltip)
                : CardActions.FirstLine(tooltip);
        }

        private void MarketRow(GuiTableLine line, NodeVtable vtable)
        {
            GuiTableLine row = line;
            MarketGestures(
                vtable,
                () => AgeWidgets.Toggle(row.SelectionToggle),
                () => AgeWidgets.Enabled(row.AgeTransform),
                () => QuantityText(BuyPanel())
            );
        }

        /// <summary>
        /// How much the panel is currently set to trade, off the box the game writes it into - which is
        /// the number a sighted player reads, and which the game writes SYNCHRONOUSLY from its own
        /// quantity setter (<c>MarketplaceTradableItemsPanel.SelectedTradableQuantity</c> :80-92 calls
        /// <c>ReplaceInputText</c>), so it is already the new number when a press asks for it.
        ///
        /// Asked of the panel whose control was pressed. Buying and selling are two panels with a box
        /// each: this used to read the BUYING one whatever was pressed, so every sell-side gesture
        /// announced the buy side's quantity - "Quantity 0" after a click that had just set it to one
        /// (measured 2026-08-30).
        /// </summary>
        private static string QuantityText(MarketplaceTradableItemsPanel panel)
        {
            try
            {
                string text = SettingRows.FieldText(panel == null ? null : panel.QuantityTextField);
                return string.IsNullOrEmpty(text)
                    ? null
                    : ModStrings.Format(ModStrings.EconomyQuantity, text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The price-and-quantity strip along the bottom of a trading panel, as the one row the game
        /// writes it as: what a unit costs, how many are being traded, and what the trade comes to.
        ///
        /// Read as that equation rather than as the widgets it is made of. The game draws a bare "-"
        /// between the price and the quantity and a bare "=" before the total, and those two glyphs are
        /// punctuation, not controls - a walk of the shape of the tree used to declare each of them as a
        /// node whose name was a hyphen. The two steppers are the game's own buttons, named by the mod
        /// because the game writes no word on them (its sentence about what a click does, and what Ctrl
        /// and Shift do to it, stays in their buffers where every other explanation is), and the trade
        /// button is named by the game's own Buy or Sell title with its running total as the value.
        ///
        /// The quantity box is the game's own editor, handed the keyboard a frame after the request. Its
        /// arrows are NOT wired to the steppers: left and right on a focused box navigate, and the value
        /// is changed by opening the edit and typing it (owner ruling 2026-08-27, first made on the
        /// negotiation basket - arrows that move a value the player only meant to walk past are a value
        /// changed by accident).
        /// </summary>
        private void BuildBand(
            GraphBuilder builder,
            MarketplaceTradableItemsPanel panel,
            object stop,
            string keyPrefix,
            string titleKey,
            string name
        )
        {
            _cells.Clear();

            // What the buying half is currently set to trade, which the game writes across the left of
            // the strip once something is picked (<c>RefreshBottomGroup</c> :185-207 leaves it blank
            // otherwise, and a blank one contributes no line). Not in the approved shape for this row -
            // the fixture it was measured on had nothing selected, so nothing was drawn there - and kept
            // because it is drawn: it is the only place the strip says WHAT is being bought.
            MarketplaceBuyableItemsPanel buy = panel as MarketplaceBuyableItemsPanel;
            if (buy != null)
            {
                Cells.AddReadout(_cells, buy.SelectedBuyableNameGroup, keyPrefix + "selected");
            }

            // Where a bought ship would appear, which the game draws in this strip only while what is
            // selected is a ship. It is the other member that is not part of the equation, and the game
            // draws it between the name and the price.
            // Flow control on a wired prefab field: the panel hides the group for anything that is not a
            // ship (<c>RefreshBottomGroup</c> :185-207), so this is which of its two shapes is drawn.
            if (
                buy != null
                && buy.ShipSpawnPointButtonGroup != null
                && AgeWidgets.Visible(buy.ShipSpawnPointButtonGroup)
            )
            {
                Cells.AddControl(_cells, buy.ShipSpawnPointButtonGroup, keyPrefix + "spawn-point");
            }

            AddPrice(panel, keyPrefix);
            AddStepper(
                panel.QuantityMinusButton,
                ModStrings.EconomyDecrement,
                keyPrefix + "minus",
                () => QuantityText(panel)
            );
            AddQuantity(panel, keyPrefix);
            AddStepper(
                panel.QuantityPlusButton,
                ModStrings.EconomyIncrement,
                keyPrefix + "plus",
                () => QuantityText(panel)
            );
            AddTradeButtons(panel, keyPrefix, titleKey);

            // Flow control: a stop would be opened around nothing on a frame where the game has taken
            // the whole strip away.
            if (_cells.Count == 0)
            {
                return;
            }

            builder.BeginStop(stop);
            builder.PushContext(name);
            Cells.EmitRow(builder, _cells);
            builder.PopContext();
        }

        /// <summary>What one unit is going for, under the game's own word for it - the strip draws the
        /// number with nothing beside it but the "-" that separates it from the quantity.</summary>
        private void AddPrice(MarketplaceTradableItemsPanel panel, string keyPrefix)
        {
            AgePrimitiveLabel label = panel.UnitPriceLabel;
            AgeTransform at = label == null ? null : label.AgeTransform;
            // No visibility test: the gate asks whether the game is drawing this label, and its whole
            // ancestry with it.
            if (at == null)
            {
                return;
            }

            AgeTransform group = at.Parent ?? at;
            AgeTooltip tooltip = AgeWidgets.Raw(group) ?? AgeWidgets.Raw(at);
            AgePrimitiveLabel it = label;
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.Clean(Gui.Localize(PriceTitleKey)),
                () => AgeText.Label(it),
                null,
                tooltip
            );
            Cells.Add(_cells, group, ControlId.For(group, keyPrefix + "unit-price"), vtable);
        }

        /// <summary>One of the two arrows beside a number the game lets the player step. The game writes
        /// no word on them at all, so the mod names them; what a press does is the game's own sentence,
        /// and the number it lands on is spoken back, because the press changes a figure somewhere else
        /// in the strip and nothing else would say so.</summary>
        private void AddStepper(
            AgeControlButton button,
            string nameKey,
            string key,
            Func<string> state
        )
        {
            AgeTransform at = AgeWidgets.Transform(button);
            // A branch chooser on a wired prefab field: the tax box keeps a pair of these inside the
            // form it is not drawing, and this is which pair is being read.
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            Func<string> landed = state;
            // Whether the press that is being reported on actually happened. Asking the button again
            // afterwards is not the same question: the press the game accepts LAST is the one that takes
            // the value to its limit and switches the button off, so a refusal test run after the fact
            // swallowed exactly the number the player most wanted (measured: incrementing to a stock of
            // five said "unavailable" and never said five).
            bool[] acted = new bool[1];
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () =>
                {
                    acted[0] = true;
                    AgeWidgets.Press(it);
                },
                () => AgeWidgets.Offered(at),
                tooltip
            );
            // Nothing at all on a press the game refuses: the player heard "unavailable" on the way in.
            vtable.StateText = () =>
            {
                if (!acted[0])
                {
                    return null;
                }

                acted[0] = false;
                return landed();
            };
            // The same press again for the two multiplier chords. A node with only an activation does
            // NOTHING for them - they are their own vtable entries - so the button the game reads the
            // held modifier inside (<c>OnQuantityPlusCb</c> :368-379) has to be wired three times to be
            // reachable three ways. The arithmetic is never the mod's: what Ctrl and Shift turn one
            // press into is decided inside the game's own handler.
            Func<bool> offered = () => AgeWidgets.Offered(at);
            Action chord = () =>
            {
                if (offered())
                {
                    acted[0] = true;
                    AgeWidgets.Press(it);
                }
            };
            vtable.OnSelectToggle = chord;
            vtable.OnSelectRange = chord;
            MarketChordHints(vtable, offered);
            AgeWidgets.Point(vtable, button, tooltip, at);
            Cells.Add(_cells, at, ControlId.For(at, key), vtable);
        }

        /// <summary>The box the quantity is typed into, as the game's own editor.</summary>
        private void AddQuantity(MarketplaceTradableItemsPanel panel, string keyPrefix)
        {
            AgeControlTextField field = panel.QuantityTextField;
            AgeTransform at = AgeWidgets.Transform(field);
            // No visibility test here either - same reason as the price beside it.
            if (at == null)
            {
                return;
            }

            Cell cell = SettingRows.TextFieldCell(
                field,
                null,
                null,
                null,
                null,
                ControlId.For(field, keyPrefix + "quantity"),
                _editor
            );
            if (cell == null)
            {
                return;
            }

            cell.Vtable.StateText = () => QuantityText(panel);
            // A NUMBER is typed here rather than free text, so the role word says so.
            cell.Vtable.ControlType = ControlTypes.NumericEditField;
            _cells.Add(cell);
        }

        /// <summary>The button that makes the trade - or, once the empire can buy with influence as well
        /// as with dust, the two of them the game swaps in for it
        /// (<c>MarketplaceBuyableItemsPanel.RefreshTradeButtons</c> :291-303). Those two carry no title of
        /// their own, only a running total beside a currency, so the game's own Buy title names them and
        /// the total is the value - currency and all, exactly as the game wrote it into the label.
        /// </summary>
        private void AddTradeButtons(
            MarketplaceTradableItemsPanel panel,
            string keyPrefix,
            string titleKey
        )
        {
            AddTradeButton(panel.TradeButton, panel.TotalPriceLabel, titleKey, keyPrefix + "trade");
            MarketplaceBuyableItemsPanel buy = panel as MarketplaceBuyableItemsPanel;
            if (buy != null)
            {
                AddTradeButton(
                    buy.DustTradeButton,
                    buy.TotalDustPriceLabel,
                    titleKey,
                    keyPrefix + "trade-dust"
                );
                AddTradeButton(
                    buy.EmpirePointTradeButton,
                    buy.TotalEmpirePointPriceLabel,
                    titleKey,
                    keyPrefix + "trade-influence"
                );
            }
        }

        private void AddTradeButton(
            AgeControlButton button,
            AgePrimitiveLabel total,
            string titleKey,
            string key
        )
        {
            AgeTransform at = AgeWidgets.Transform(button);
            // A branch chooser, not an existence gate: the panel keeps all three trade buttons wired and
            // shows either the one or the pair (<c>RefreshTradeButtons</c> :291-303).
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            AgeControlButton it = button;
            AgePrimitiveLabel amount = total;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Clean(Gui.Localize(titleKey)),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Offered(at),
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(amount)));
            AgeWidgets.Point(vtable, button, tooltip, at);
            Cells.Add(_cells, at, ControlId.For(at, key), vtable);
        }

        // ---- the price graph ----

        /// <summary>
        /// The price graph, as the table it is a picture of: a row per resource the buy table is showing,
        /// a column per turn, and the value the game plotted in each cell.
        ///
        /// Nothing on this panel is text a walk could find. The game draws a set of coloured curves with
        /// numbered axes and no name anywhere on it - which resource a line belongs to is its colour, and
        /// what a point is worth is where it sits between two axis labels. The series here are the same
        /// ones the renderer plots (<c>TradableHistoryCurvesPanel.Refresh</c> :95-152): the buy table's
        /// own lines, over the turn window the marketplace's <c>TradableHistorySpanTurnCount</c> property
        /// sets, trimmed at the front to the earliest turn any of them has a reading for. The columns are
        /// DISPLAYED turn numbers, because the game's own X axis draws snapshot turn plus one, and each
        /// value goes through the game's own amount formatter rounded to a whole number, the way its axis
        /// labels are.
        ///
        /// Two things are deliberately not here. The Y axis's scale labels: they exist to place a line on
        /// a picture, and the cells state the values themselves. And the game's
        /// highlight-the-selected-curve filter, which is a sighted-only narrowing of the same data
        /// (owner-approved) - what the mod does instead is <see cref="Curves"/>, which points the same
        /// highlight at whichever row the cursor is on.
        /// </summary>
        private void BuildHistory(GraphBuilder builder, MarketplaceBuyableItemsPanel panel)
        {
            _history.Clear();
            TradableHistoryCurvesPanel curves = panel.TradableHistoryCurvesPanel;
            // Flow control: whether the graph is read at all. Its rows are SYNTHETIC - they are built
            // from the game's snapshot lists, not from widgets - so no gate stands behind them, and this
            // is the whole of their existence test.
            if (curves == null || !AgeWidgets.Visible(curves.AgeTransform))
            {
                return;
            }

            AgeTransform empty = curves.NoDataAvailableGroup;
            // Flow control, on a wired prefab field that is always there: this is the BRANCH the panel
            // chooses between its two forms (<c>Refresh</c> :127-137 shows one and hides the other), and
            // the game's own words for an empty window are then the whole of the stop.
            if (empty != null && AgeWidgets.Visible(empty))
            {
                builder.BeginStop(HistoryStop);
                builder.PushContext(ModStrings.Get(ModStrings.EconomyPriceHistory));
                _cells.Clear();
                Cells.AddReadout(_cells, empty, "economy:history/no-data");
                Cells.EmitLinear(builder, _cells);
                builder.PopContext();
                return;
            }

            List<GuiTableLine> lines = _buyTable.Lines(panel.BuyableItemsGuiTable);
            for (int i = 0; i < lines.Count; i++)
            {
                ITradableItem item = lines[i] as ITradableItem;
                GuiBuyable buyable = item == null ? null : item.GuiTradable as GuiBuyable;
                if (buyable != null && buyable.History != null && buyable.History.Count > 0)
                {
                    _history.Add(buyable);
                }
            }

            int floor = WindowStart();
            int first = int.MaxValue;
            int last = -1;
            for (int r = 0; r < _history.Count; r++)
            {
                List<TradableSnapshot> history = _history[r].History;
                for (int s = 0; s < history.Count; s++)
                {
                    int turn = history[s].Turn;
                    if (turn < floor)
                    {
                        continue;
                    }

                    if (turn < first)
                    {
                        first = turn;
                    }

                    if (turn > last)
                    {
                        last = turn;
                    }
                }
            }

            // Flow control: no reading inside the window is a graph with nothing on it, and the game
            // draws its own words for that (above) rather than an empty table.
            if (last < first)
            {
                _history.Clear();
                return;
            }

            // NEWEST first (owner ruling 2026-08-30): what a resource is worth NOW is the question this
            // table is opened with, and the answer is then one press right of the row's name rather than
            // the length of the window away. So column 1 is the latest turn and the walk goes back.
            int columns = last - first + 1;
            string[] headers = new string[columns + 1];
            for (int c = 0; c < columns; c++)
            {
                // The DISPLAYED turn, under the same word the turn log names a turn by: a bare number
                // crossed into says nothing about what kind of number it is.
                headers[c + 1] = ModStrings.Format(ModStrings.HudTurnLogTurn, last - c + 1);
            }

            builder.BeginStop(HistoryStop);
            GraphSheet sheet = new GraphSheet(builder, "economy:history/");
            sheet.Region(ModStrings.Get(ModStrings.EconomyPriceHistory), headers);
            AddHistoryCaption(builder, sheet, curves);

            Func<string>[] cells = new Func<string>[columns];
            for (int r = 0; r < _history.Count; r++)
            {
                GuiBuyable buyable = _history[r];
                for (int c = 0; c < columns; c++)
                {
                    // A turn this resource has no reading for is still a CELL - dropping it would put a
                    // neighbour's price under the wrong turn on the way across - and it says the word
                    // every other empty cell in the mod says.
                    cells[c] = Nothing;
                }

                List<TradableSnapshot> history = buyable.History;
                for (int s = 0; s < history.Count; s++)
                {
                    int column = last - history[s].Turn;
                    if (column >= 0 && column < columns)
                    {
                        string drawn = Gui.FormatAmount(
                            UnityEngine.Mathf.RoundToInt(history[s].Value),
                            true,
                            false,
                            false
                        );
                        cells[column] = () => drawn;
                    }
                }

                string title = AgeText.Clean(buyable.Title);
                NodeVtable primary = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => title),
                    },
                };
                sheet.Row(primary, HistoryKey(buyable), null, cells);
            }

            sheet.Finish();
            // Tab into the graph lands on a resource rather than on the sentence about the graph, the
            // same rule every other table on this page lands by.
            builder.LandStopOn(sheet.FirstRow);
        }

        /// <summary>The one sentence the game writes about the graph, which it hangs on the panel itself
        /// rather than on any caption - there is no drawn heading here to carry it, so the row says the
        /// mod's own name for the block and the sentence goes in its buffer.</summary>
        private static void AddHistoryCaption(
            GraphBuilder builder,
            GraphSheet sheet,
            TradableHistoryCurvesPanel curves
        )
        {
            AgeTooltip about = AgeWidgets.Raw(curves.AgeTransform);
            if (about == null || !AgeWidgets.Draws(about))
            {
                return;
            }

            ControlId lead = ControlId.For(curves.AgeTransform, "economy:history/about");
            NodeVtable saying = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.EconomyPriceHistory)),
                },
            };
            saying.Sections = GraphNodes.SectionsFor(saying, about);
            builder.AddNode(Nodes.Drawn(lead, saying, curves.AgeTransform));
            sheet.Follows(lead);
        }

        private static readonly Func<string> Nothing = () => null;

        /// <summary>The oldest turn the price graph plots: the game's own window, read off the same
        /// marketplace property the curves panel binds itself with
        /// (<c>TradableHistoryCurvesPanel.Bind</c> :61-68, <c>Refresh</c> :93-94).</summary>
        private static int WindowStart()
        {
            try
            {
                int turn = Gui.Game.Turn;
                ITradingManagementService trading = Trading();
                if (trading == null)
                {
                    return 0;
                }

                int span = UnityEngine.Mathf.RoundToInt(
                    trading.SimulationObject.GetPropertyValue(
                        SimulationProperties.Marketplace.TradableHistorySpanTurnCount
                    )
                );
                return UnityEngine.Mathf.Clamp(turn - span, 0, turn);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>What a price-history row is keyed and reconciled by. Not the tradable itself, which
        /// the buy table's own rows already carry: reference identity is followed before the structural
        /// key, so two nodes sharing one object are one control to the cursor and focus would teleport
        /// between the graph and the table. A token per tradable keeps the row identified across a
        /// re-sort without being the same thing as the row below it.</summary>
        private object HistoryKey(GuiBuyable buyable)
        {
            ulong uid = buyable.Tradable.UID;
            object token;
            if (!_historyKeys.TryGetValue(uid, out token))
            {
                token = new object();
                _historyKeys[uid] = token;
            }

            return token;
        }

        /// <summary>
        /// The visual courtesy that goes with the price-history table: while the cursor is on one of its
        /// rows, the game's own graph draws that resource's curve bright and dims the others, so a
        /// sighted observer can see what is being read (owner-approved).
        ///
        /// The lever is the curve widget's own Enable flag, which is exactly what the game writes for its
        /// own highlight (<c>TradableHistoryCurvesPanel.RefreshTradableHistoryCurve</c> :177,193).
        /// Selecting the buy table's LINE would look the same and is deliberately not used: that
        /// selection is real trade state - it feeds the quantity and the totals - and walking a table
        /// must not spend it.
        ///
        /// The curve widgets carry no back-reference to their resource; they are bound positionally to
        /// the panel's own list, which is built from the POOLED order of the table's line components
        /// rather than the drawn order (<c>Refresh</c> :100-109), so the index is recomputed with the
        /// game's own enumeration. Reasserted every frame because the panel rebinds itself whenever the
        /// table scrolls, and handed back by calling the panel's own refresh, so what returns is the
        /// game's rule rather than the mod's guess at it.
        /// </summary>
        private void Curves()
        {
            try
            {
                MarketplaceBuyableItemsPanel buy = BuyPanel();
                TradableHistoryCurvesPanel curves =
                    buy == null ? null : buy.TradableHistoryCurvesPanel;
                if (curves == null)
                {
                    return;
                }

                GuiBuyable wanted = FocusedHistoryRow();
                Reveal(buy, wanted);
                int index = wanted == null ? -1 : CurveIndex(buy, wanted);
                IList<AgeTransform> children =
                    curves.TradableHistoryCurvesContainer == null
                        ? null
                        : curves.TradableHistoryCurvesContainer.Children;
                if (index < 0 || children == null)
                {
                    RestoreCurves();
                    return;
                }

                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] != null)
                    {
                        children[i].Enable = i == index;
                    }
                }

                _dimmed = true;
            }
            catch (Exception e)
            {
                Log.Warn("economy: highlighting a price curve threw: " + e);
            }
        }

        /// <summary>
        /// Bring the buy table's own line for the focused history row into its scrolling window.
        ///
        /// Not a courtesy: it is what makes the graph HAVE that resource's curve at all. The curves
        /// panel rebuilds its series from the lines the table is currently showing
        /// (<c>TradableHistoryCurvesPanel.Refresh</c> :100-109) and marks itself dirty whenever the
        /// table's virtual area moves more than two pixels (<c>SpecificUpdate</c> :85-93), so a row
        /// scrolled out of that window has no curve to point the highlight at - and on a section long
        /// enough to scroll, the rows the player is reading in the table below are exactly the ones that
        /// go out of it.
        ///
        /// Through the shared reveal, which asks the scroll view's own question - does this widget sit
        /// inside the viewport - and does nothing at all when it does. The engine offers no scroll-TO
        /// call to prefer over it: <c>AgeControlScrollView</c> is public only in its four Reset jumps
        /// and <c>MouseWheel</c>, and its clamping, its scrollbar placement and its OnScroll message all
        /// live behind the private <c>ConstraintAndPlace</c> - so replaying the wheel IS the engine's
        /// own entry point, and writing the virtual area directly would skip the notification the curves
        /// panel is watching for.
        ///
        /// Once per ARRIVAL, never per frame and never on the way out: a scroll the player made stays
        /// where they put it.
        /// </summary>
        private void Reveal(MarketplaceBuyableItemsPanel panel, GuiBuyable wanted)
        {
            object key = wanted == null ? null : HistoryKey(wanted);
            if (ReferenceEquals(key, _revealed))
            {
                return;
            }

            _revealed = key;
            if (wanted == null)
            {
                return;
            }

            AgeTransform line = LineOf(panel, wanted);
            if (line != null)
            {
                ScrollIntoView.Reveal(line);
            }
        }

        /// <summary>The widget the buy table draws this resource's row as, found the way the curves
        /// panel finds its own series - the game's own enumeration of the table's line components, which
        /// is why a row it has stopped enumerating is a row this answers nothing for.</summary>
        private static AgeTransform LineOf(MarketplaceBuyableItemsPanel panel, GuiBuyable wanted)
        {
            try
            {
                GuiTable table = panel == null ? null : panel.BuyableItemsGuiTable;
                if (table == null || wanted.Tradable == null)
                {
                    return null;
                }

                GuiTableLineBuyable[] found = table.GetComponentsInChildren<GuiTableLineBuyable>();
                for (int i = 0; i < found.Length; i++)
                {
                    GuiBuyable buyable =
                        found[i] == null ? null : found[i].GuiTradable as GuiBuyable;
                    if (
                        buyable != null
                        && buyable.Tradable != null
                        && buyable.Tradable.UID == wanted.Tradable.UID
                    )
                    {
                        return found[i].AgeTransform;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: finding a resource's table row threw: " + e);
            }

            return null;
        }

        /// <summary>Give the graph back to the game, exactly as it had it.</summary>
        private void RestoreCurves()
        {
            if (!_dimmed)
            {
                return;
            }

            _dimmed = false;
            try
            {
                MarketplaceBuyableItemsPanel buy = BuyPanel();
                if (buy != null && buy.TradableHistoryCurvesPanel != null)
                {
                    buy.TradableHistoryCurvesPanel.OnSelectedItemChanged();
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: restoring the price curves threw: " + e);
            }
        }

        private static MarketplaceBuyableItemsPanel BuyPanel()
        {
            global::EconomyScreen window = Window();
            MarketplacePanel market = window == null ? null : window.MarketplacePanel;
            return market == null ? null : market.BuyableItemsPanel;
        }

        /// <summary>The resource the cursor is standing on a price-history row of, or nothing where it is
        /// standing anywhere else. Which row it is on is what says which resource: the sheet stamps the
        /// row on every one of its cells, so the answer is the same from any column.</summary>
        private GuiBuyable FocusedHistoryRow()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null || navigator.Screen != this)
            {
                return null;
            }

            GraphNode node = navigator.CurrentNode;
            if (node == null || !HistoryStop.Equals(node.StopKey) || node.Vtable == null)
            {
                return null;
            }

            TableRow row = node.Vtable.Row;
            int index = row == null ? 0 : row.Index;
            return index >= 1 && index <= _history.Count ? _history[index - 1] : null;
        }

        /// <summary>Which of the graph's curves is this resource's.</summary>
        private static int CurveIndex(MarketplaceBuyableItemsPanel panel, GuiBuyable wanted)
        {
            GuiTable table = panel.BuyableItemsGuiTable;
            if (table == null || wanted.Tradable == null)
            {
                return -1;
            }

            GuiTableLineBuyable[] found = table.GetComponentsInChildren<GuiTableLineBuyable>();
            int index = 0;
            for (int i = 0; i < found.Length; i++)
            {
                GuiBuyable buyable = found[i] == null ? null : found[i].GuiTradable as GuiBuyable;
                if (buyable == null)
                {
                    continue;
                }

                if (buyable.Tradable != null && buyable.Tradable.UID == wanted.Tradable.UID)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        // ---- the tax box ----

        /// <summary>
        /// The marketplace's tax box, in whichever of its two forms the game is drawing: the owner's,
        /// with the rate to set and what setting it would cost, or everybody else's, with where the
        /// marketplace is, who owns it and the rate they have set
        /// (<c>MarketplaceTaxesPanel.Refresh</c> :112-188).
        ///
        /// The game draws its facts as a row of values with no word over any of them, so each is a line
        /// of its own under the mod's caption for what it states, with the game's own sentence about it
        /// in the buffer. The location is a button as well as a fact: it takes the map to the system the
        /// marketplace was built in, and is drawn switched off with the reason on it until somebody
        /// builds one.
        ///
        /// The owner's form is FIXTURE-BLOCKED on the save this was built against (its marketplace is
        /// unbuilt and unowned). Its rate box is the game's editor under the same typing rule as the
        /// trading strip's quantity, its two steppers move one percentage point per press with no coarse
        /// variant (<c>OnIncreaseTaxRateButtonClickCb</c> :240-252), and its Set button is named by the
        /// game's own drawn label, which states what the change costs.
        /// </summary>
        private void BuildTaxes(GraphBuilder builder, MarketplaceTaxesPanel panel)
        {
            builder.BeginStop(TaxesStop);
            builder.PushContext(PanelName(panel, ModStrings.EconomyTaxesPanel));
            _cells.Clear();
            AddPanelCaption(_cells, panel, "economy:taxes/title");
            // A branch chooser on a wired prefab field, not an existence gate: the panel keeps both
            // forms and shows one (<c>Refresh</c> :155-171), so this is which of the two is being read.
            if (panel.OwnedGroup != null && AgeWidgets.Visible(panel.OwnedGroup))
            {
                AddLocation(panel.OwnedLocationButton, panel.OwnedLocationLabel);
                AddTaxRate(panel);
            }
            else
            {
                AddLocation(panel.NotOwnedLocationButton, panel.NotOwnedLocationLabel);
                Cells.AddStat(
                    _cells,
                    panel.NotOwnedOwnerNameLabel,
                    ModStrings.Get(ModStrings.EconomyOwner),
                    "economy:taxes/owner"
                );
                Cells.AddStat(
                    _cells,
                    panel.NotOwnedTaxRateLabel,
                    ModStrings.Get(ModStrings.EconomyTaxRate),
                    "economy:taxes/rate"
                );
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>Where the marketplace is, and the game's own button that takes the map there.
        /// </summary>
        private void AddLocation(AgeTransform group, AgePrimitiveLabel label)
        {
            // No visibility test: which of the two forms is drawn was decided by the caller, and the
            // gate asks the rest.
            if (group == null)
            {
                return;
            }

            AgeControlButton button = AgeWidgets.Button(group);
            AgeTooltip tooltip = AgeWidgets.Raw(group);
            AgePrimitiveLabel it = label;
            NodeVtable vtable;
            if (button == null)
            {
                vtable = GraphNodes.Readout(
                    () => ModStrings.Get(ModStrings.EconomyLocation),
                    () => AgeText.Label(it),
                    null,
                    tooltip
                );
            }
            else
            {
                AgeControlButton press = button;
                vtable = GraphNodes.Button(
                    () => ModStrings.Get(ModStrings.EconomyLocation),
                    () => AgeWidgets.Press(press),
                    () => AgeWidgets.Offered(group),
                    tooltip
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(it)));
                AgeWidgets.Point(vtable, button, tooltip, group);
            }

            Cells.Add(_cells, group, ControlId.For(group, "economy:taxes/location"), vtable);
        }

        /// <summary>The owner's rate editor, its two steppers and the button that pays for the change -
        /// each a line of its own, because the game hangs no shared caption over them to read them
        /// under.</summary>
        private void AddTaxRate(MarketplaceTaxesPanel panel)
        {
            AgeControlTextField field = panel.TaxRateTextField;
            AgeTransform at = AgeWidgets.Transform(field);
            // A branch chooser again: the box lives in the owner form, which the panel keeps wired and
            // hides while somebody else owns the marketplace.
            if (at != null && AgeWidgets.Visible(at))
            {
                Cell cell = SettingRows.TextFieldCell(
                    field,
                    () => ModStrings.Get(ModStrings.EconomyTaxRate),
                    AgeWidgets.Raw(at.Parent ?? at),
                    null,
                    null,
                    ControlId.For(field, "economy:taxes/rate"),
                    _editor
                );
                if (cell != null)
                {
                    // Same ruling as the trading strip's quantity: the arrows are the player walking,
                    // not the player setting, and the rate is typed into the edit.
                    cell.Vtable.StateText = () => SettingRows.FieldText(field);
                    cell.Vtable.ControlType = ControlTypes.NumericEditField;
                    _cells.Add(cell);
                }
            }

            Func<string> rate = () => SettingRows.FieldText(panel.TaxRateTextField);
            AddStepper(
                panel.DecreaseTaxRateButton,
                ModStrings.EconomyDecrement,
                "economy:taxes/minus",
                rate
            );
            AddStepper(
                panel.IncreaseTaxRateButton,
                ModStrings.EconomyIncrement,
                "economy:taxes/plus",
                rate
            );
            Cells.AddControl(_cells, panel.ApplyTaxRateButton, "economy:taxes/apply");
        }

        // ---- the exchange log ----

        /// <summary>What has been traded, newest at the bottom - which is where the game scrolls the list
        /// to. The game groups the list by turn and draws a header above each group (a line with no
        /// transactions behind it IS the header -
        /// <c>MarketplaceExchangeInformationsPanel.Refresh</c> :16-62,
        /// <c>TradableTransactionLine.Bind</c>), so each turn is a region of its own named by the header
        /// the game drew and each transaction is a line under it. A transaction of somebody else's is
        /// already anonymised by the game before it is written, so what is drawn is what may be read.
        /// </summary>
        private void BuildLog(GraphBuilder builder, MarketplaceExchangeInformationsPanel panel)
        {
            builder.BeginStop(LogStop);
            builder.PushContext(PanelName(panel, ModStrings.EconomyLogPanel));
            try
            {
                // The panel's own heading, and the sentence it draws over the whole list while the empire
                // may not see one, in a region of their own: a stop is regioned all the way through or
                // not at all, and the jump out of the last turn has to land somewhere.
                builder.SetRegion("economy:log/head");
                _cells.Clear();
                AddPanelCaption(_cells, panel, "economy:log/title");
                Cells.AddReadout(
                    _cells,
                    panel.NotOwnerLabel == null ? null : panel.NotOwnerLabel.AgeTransform,
                    "economy:log/not-owner"
                );
                Cells.EmitLinear(builder, _cells);

                AgeTransform table = panel.TradableTransactionsTable;
                IList<AgeTransform> lines = table == null ? null : table.Children;
                bool turn = false;
                _cells.Clear();
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    AgeTransform line = lines[i];
                    if (line == null || !SettingRows.Drawn(line))
                    {
                        continue;
                    }

                    string header = TurnHeader(line);
                    if (string.IsNullOrEmpty(header))
                    {
                        Cells.AddReadout(_cells, line, "economy:log/line/" + i);
                        continue;
                    }

                    Cells.EmitLinear(builder, _cells);
                    _cells.Clear();
                    if (turn)
                    {
                        builder.PopContext();
                    }

                    builder.SetRegion("economy:log/turn/" + i);
                    builder.PushContext(header);
                    turn = true;
                }

                Cells.EmitLinear(builder, _cells);
                if (turn)
                {
                    builder.PopContext();
                }
            }
            finally
            {
                builder.PopContext();
            }
        }

        /// <summary>The words a line the game drew names a turn with, or nothing where the line is a
        /// transaction. The header is the wrapper the game made with no transactions in it.</summary>
        private static string TurnHeader(AgeTransform line)
        {
            try
            {
                TradableTransactionLine drawn = line.GetComponent<TradableTransactionLine>();
                GuiTradableTransaction of = drawn == null ? null : drawn.GuiTradableTransaction;
                bool header =
                    of != null && of.TradableTransactions != null && of.TradableTransactions.Count == 0;
                return header ? AgeWidgets.TextOf(line) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The market events, as the list the game's own service holds rather than as the one item the
        /// conveyor happens to have on screen.
        ///
        /// This is the page's one deliberate departure from declaring what is drawn (owner-approved). The
        /// banner animates pooled items across the panel one at a time and sizes each to its text
        /// (<c>MarketplaceEventsBanner.QueueNext</c> :102-125), so a walk of what is drawn would find a
        /// single moving fragment. The rows here are composed from the same call and the same template
        /// the drawn item composes from (<c>MarketplaceEventItem.Bind</c>).
        /// </summary>
        private void BuildEvents(GraphBuilder builder, MarketplaceEventsBanner banner)
        {
            List<KeyValuePair<StaticString, StaticString>> feedback = Feedback();
            if (feedback == null || feedback.Count == 0)
            {
                return;
            }

            builder.BeginStop(EventsStop);
            builder.PushContext(ModStrings.Get(ModStrings.EconomyEventsPanel));
            for (int i = 0; i < feedback.Count; i++)
            {
                string said = EventText(feedback[i].Key, feedback[i].Value);
                if (string.IsNullOrEmpty(said))
                {
                    continue;
                }

                string text = said;
                builder.StartRow();
                // Synthetic: composed from the empire's own event record, which the panel draws
                // nowhere - the enumeration above is what says these are real.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("economy:event/" + i),
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => text),
                        },
                    }
                ));
                builder.EndRow();
            }

            builder.PopContext();
        }

        private static List<KeyValuePair<StaticString, StaticString>> Feedback()
        {
            try
            {
                ITradingManagementService service =
                    Amplitude.Unity.Framework.Services.GetService<ITradingManagementService>();
                if (service == null)
                {
                    return null;
                }

                List<KeyValuePair<StaticString, StaticString>> found =
                    new List<KeyValuePair<StaticString, StaticString>>();
                service.GetEventsFeedback(found);
                return found;
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading the market events threw: " + e);
                return null;
            }
        }

        /// <summary>One event's sentence, composed the way the drawn item composes it: the affected
        /// thing's own title inside the template the event names.</summary>
        private static string EventText(StaticString element, StaticString effect)
        {
            try
            {
                Amplitude.Unity.Gui.GuiElement guiElement = Gui.GetGuiElement(element);
                string name =
                    guiElement == null ? null : AgeText.Clean(Gui.Localize(guiElement.Title));
                return string.IsNullOrEmpty(name)
                    ? null
                    : AgeText.Clean(Gui.Localize(effect.ToString(), name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The advertisements, read the same way and for the same reason as the events - and untruncated,
        /// which the drawn item is not (<c>AdItem.Bind</c> cuts the empire's name to fit the strip).
        ///
        /// The Create-advertisement button the banner draws is not declared: the window it opens has no
        /// controls at all in this build and is not registered with the GUI service (measured), so the
        /// game's own handler logs an error and opens nothing.
        /// </summary>
        private void BuildAds(GraphBuilder builder, MarketplaceAdBanner banner)
        {
            if (banner == null)
            {
                return;
            }

            ITradingManagementService service = Trading();
            int count = 0;
            try
            {
                count = service == null ? 0 : service.ActiveAdvertisementsCount;
            }
            catch (Exception)
            {
                return;
            }

            if (count == 0)
            {
                return;
            }

            builder.BeginStop(AdsStop);
            builder.PushContext(ModStrings.Get(ModStrings.EconomyAdsPanel));
            for (int i = 0; i < count; i++)
            {
                string said = AdText(service, i);
                if (string.IsNullOrEmpty(said))
                {
                    continue;
                }

                string text = said;
                builder.StartRow();
                // Synthetic: composed from the trading company's own offer list, drawn nowhere.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("economy:ad/" + i),
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => text),
                        },
                    }
                ));
                builder.EndRow();
            }

            builder.PopContext();
        }

        /// <summary>One advertisement: who wants something - or the game's own word for an empire that
        /// asked not to be named - and what they want, in the game's own template for it.</summary>
        private static string AdText(ITradingManagementService service, int index)
        {
            try
            {
                MarketplaceAdvertisement ad = service.GetAdvertisement(index);
                if (ad == null)
                {
                    return null;
                }

                string who = ad.IsAnonymous
                    ? AgeText.Clean(Gui.Localize(AdItem.AnonymousEmpireLoc.ToString()))
                    : Gui.GuiWrapperProviderService
                        .GetGuiEmpire(ad.EmpireIndex)
                        .GetLeaderName(Gui.PlayerEmpire);
                Amplitude.Unity.Gui.ExtendedGuiElement element =
                    Gui.GetExtendedGuiElement(ad.ItemName);
                string what =
                    element == null ? null : AgeText.Clean(Gui.Localize(element.Title));
                if (string.IsNullOrEmpty(what))
                {
                    return null;
                }

                return new MessageBuilder()
                    .ListItem(AgeText.Clean(who))
                    .ListItem(
                        AgeText.Clean(
                            Gui.Localize(AdItem.AdvertisementDescLoc.ToString(), what)
                        )
                    )
                    .Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ITradingManagementService Trading()
        {
            try
            {
                return Amplitude.Unity.Framework.Services.GetService<ITradingManagementService>();
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

        /// <summary>Close the box's name off again, so the next box is not declared inside it.</summary>
        private static void Unname(GraphBuilder builder, bool named)
        {
            if (named)
            {
                builder.PopContext();
            }
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
        /// "PanelTitle"; "Title" is what the economy tab's boxes call theirs, and it is kept as the
        /// fallback so one lookup answers for both.</summary>
        private static AgeTransform PanelCaption(GuiPanel panel)
        {
            try
            {
                AgeTransform at = panel == null ? null : panel.AgeTransform;
                return at == null
                    ? null
                    : AgeWidgets.ChildNamed(at, "PanelTitle", 2)
                        ?? AgeWidgets.ChildNamed(at, "Title", 2);
            }
            catch (Exception)
            {
                return null;
            }
        }

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

        private static readonly Comparison<AgeTransform> InReadingOrder = (left, right) =>
        {
            UnityEngine.Rect a = left.GetGlobalPosition();
            UnityEngine.Rect b = right.GetGlobalPosition();
            int rows = a.y.CompareTo(b.y);
            return rows != 0 ? rows : a.x.CompareTo(b.x);
        };

        private static readonly Comparison<GuiPanel> PanelsInReadingOrder = (left, right) =>
            InReadingOrder(left.AgeTransform, right.AgeTransform);


        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

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
        private static object TradableOf(GuiTableLine line)
        {
            try
            {
                IGuiTradable tradable =
                    line == null ? null : line.Data as IGuiTradable;
                GuiBuyable buyable = tradable as GuiBuyable;
                return buyable != null ? (object)buyable.Tradable : tradable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a market row is called when its name column draws nothing.</summary>
        private static string TradableName(GuiTableLine line)
        {
            try
            {
                IGuiTradable tradable = line == null ? null : line.Data as IGuiTradable;
                return tradable == null ? null : AgeText.Clean(tradable.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ScreenTitle()
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle("EconomyScreen"));
            }
            catch (Exception)
            {
                return null;
            }
        }

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
