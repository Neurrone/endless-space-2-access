using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
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
    public sealed class EconomyScreen : Screen
    {
        private static readonly object TabsStop = "economy:tabs";
        private static readonly object CompaniesStop = "economy:trade-companies";
        private static readonly object LuxuriesStop = "economy:luxuries";
        private static readonly object StrategicsStop = "economy:strategics";
        private static readonly object RecipesStop = "economy:recipes";
        private static readonly object BuyTabsStop = "economy:market/buy-tabs";
        private static readonly object BuyRowsStop = "economy:market/buy";
        private static readonly object BuyBandStop = "economy:market/buy-band";
        private static readonly object SellTabsStop = "economy:market/sell-tabs";
        private static readonly object SellRowsStop = "economy:market/sell";
        private static readonly object SellBandStop = "economy:market/sell-band";
        private static readonly object TaxesStop = "economy:market/taxes";
        private static readonly object LogStop = "economy:market/log";
        private static readonly object AdsStop = "economy:market/ads";
        private static readonly object EventsStop = "economy:market/events";

        /// <summary>Shared by the marketplace's sell list, so up and down out of a wrapped strip of
        /// tiles keeps the column it was in - the list is dense, unlike the resource grids.</summary>
        private static readonly object GridRowKey = "economy:grid-row";

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
            get { return MarketDrawn(Window()) ? BuyTabsStop : CompaniesStop; }
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
        }

        public override void OnUpdate()
        {
            _editor.Update();
            _hud.Update();
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
            if (widget == null || !AgeWidgets.Visible(widget))
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
            Cells.Add(cells, at, ControlId.Referenced(toggle, "economy:tab/" + index), vtable);
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
        /// the empire knows). That sentence is what the item is called, and it is then not announced a
        /// second time as a tooltip.
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
                Sections = GraphNodes.Sections(
                    null,
                    tooltip,
                    named ? GraphNodes.ModeFor(tooltip) : TooltipMode.None
                ),
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
            if (line == null || !AgeWidgets.Visible(widget))
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
            Cells.Add(cells, widget, ControlId.Referenced(widget, "economy:recipe/" + index), vtable);
        }

        /// <summary>The invitation the game writes on an empty slot the empire could fill, and nothing on
        /// a slot already holding a project.</summary>
        private static string Creatable(RecipeLine line)
        {
            try
            {
                return line.ClickToCreateRecipeTitleLabel == null
                    || !AgeWidgets.Visible(line.ClickToCreateRecipeTitleLabel.AgeTransform)
                    ? null
                    : AgeText.Label(line.ClickToCreateRecipeTitleLabel);
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

        /// <summary>The buying half: the sections across the top, the table of what is on offer, and the
        /// price-and-quantity strip along the bottom.</summary>
        private void BuildBuy(GraphBuilder builder, MarketplaceBuyableItemsPanel panel)
        {
            BuildSections(builder, BuyTabsStop, panel, "economy:buy-section/");

            GuiTable table = panel.BuyableItemsGuiTable;
            if (table != null && AgeWidgets.Visible(table.AgeTransform))
            {
                builder.BeginStop(BuyRowsStop);
                _buyTable.Headers(builder, table);
                _buyTable.Rows(builder, table, PanelName(panel, ModStrings.EconomyBuyPanel));
            }

            builder.BeginStop(BuyBandStop);
            _cells.Clear();
            SidePanels.Content(_cells, panel.AgeTransform, "economy:buy-band/", MarketBandCell, null);
            Cells.Emit(builder, _cells);
        }

        /// <summary>The selling half. Its items are not a table at all - they are plain toggles the panel
        /// pools (<c>MarketplaceSalableItemsPanel.RefreshTradableItemsTable</c> :117-130) - so they read
        /// as one row per thing on offer, with the same click and the same chords the buy table's rows
        /// have.</summary>
        private void BuildSell(GraphBuilder builder, MarketplaceSalableItemsPanel panel)
        {
            BuildSections(builder, SellTabsStop, panel, "economy:sell-section/");

            AgeTransform table = panel.SalableItemsTable;
            if (table != null && AgeWidgets.Visible(table))
            {
                builder.BeginStop(SellRowsStop);
                builder.PushContext(PanelName(panel, ModStrings.EconomySellPanel));
                _cells.Clear();
                IList<AgeTransform> items = table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AddSalableItem(_cells, items[i], i);
                }

                Emit(builder, _cells, GridRowKey);
                builder.PopContext();
            }

            builder.BeginStop(SellBandStop);
            _cells.Clear();
            SidePanels.Content(_cells, panel.AgeTransform, "economy:sell-band/", MarketBandCell, null);
            Cells.Emit(builder, _cells);
        }

        /// <summary>The section radios - strategic resources, luxuries, ships, heroes - filtered and
        /// ordered by the game (<c>BuildGuiTradableSections</c>). A section the empire may not trade in
        /// is drawn switched off with the game's own reason on it, and stays declared while it refuses:
        /// which markets exist, and why not this one, is what the player came here to find out.</summary>
        private void BuildSections(
            GraphBuilder builder,
            object stop,
            MarketplaceTradableItemsPanel panel,
            string keyPrefix
        )
        {
            AgeTransform table = panel.MarketTabRadiosTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            if (children == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            builder.BeginStop(stop);
            _cells.Clear();
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                MarketTabRadio radio =
                    widget == null ? null : widget.GetComponent<MarketTabRadio>();
                if (radio == null || !AgeWidgets.Visible(widget) || radio.Toggle == null)
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
                    () => AgeText.Label(it.Label),
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
                    ControlId.Referenced(widget, keyPrefix + i),
                    vtable
                );
            }

            Cells.Emit(builder, _cells);
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
        private void AddSalableItem(List<Cell> cells, AgeTransform widget, int index)
        {
            SalableItem item = widget == null ? null : widget.GetComponent<SalableItem>();
            if (item == null || !SettingRows.Drawn(widget) || item.SelectionToggle == null)
            {
                return;
            }

            SalableItem it = item;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            bool named = Identified(tooltip);
            string label = named
                ? AgeWidgets.TooltipTitle(tooltip)
                : CardActions.FirstLine(tooltip);
            Func<bool> offered = () => AgeWidgets.Operable(widget);
            NodeVtable vtable = GraphNodes.Radio(
                () => label,
                () => it.SelectionToggle.State,
                () => AgeWidgets.Toggle(it.SelectionToggle),
                offered,
                null,
                tooltip,
                named ? GraphNodes.ModeFor(tooltip) : TooltipMode.None
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(it.StockLabel)));
            MarketGestures(vtable, () => AgeWidgets.Toggle(it.SelectionToggle), offered);
            AgeWidgets.Point(vtable, it.SelectionToggle, tooltip, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, "economy:salable/" + index), vtable);
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
        private void MarketGestures(NodeVtable vtable, Action click, Func<bool> offered)
        {
            Action press = click;
            Func<bool> can = offered;
            Action guarded = () =>
            {
                if (can())
                {
                    press();
                }
            };
            vtable.OnSelectToggle = guarded;
            vtable.OnSelectRange = guarded;
            vtable.StateText = () => can() ? QuantityText() : null;
        }

        private void MarketRow(GuiTableLine line, NodeVtable vtable)
        {
            GuiTableLine row = line;
            MarketGestures(
                vtable,
                () => AgeWidgets.Toggle(row.SelectionToggle),
                () => AgeWidgets.Enabled(row.AgeTransform)
            );
        }

        /// <summary>How much the marketplace is currently set to trade, off the box the game writes it
        /// into - which is the number a sighted player reads.</summary>
        private static string QuantityText()
        {
            try
            {
                global::EconomyScreen window = Window();
                MarketplacePanel panel = window == null ? null : window.MarketplacePanel;
                AgeControlTextField field =
                    panel == null || panel.BuyableItemsPanel == null
                        ? null
                        : panel.BuyableItemsPanel.QuantityTextField;
                string text = SettingRows.FieldText(field);
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
        /// The one control in a trading panel's bottom strip the shape of the tree cannot read: the box
        /// the quantity is typed into.
        ///
        /// It is declared as the game's own editor, handed the keyboard a frame after the request, and
        /// its two stepper buttons are its left and right - held with Shift, the game's own whole-stock
        /// step, because those buttons read the physically held modifier too
        /// (<c>OnQuantityPlusCb</c> :368-379).
        ///
        /// The rest of the strip - unit price, total, the trade buttons, the spawn-point picker - is read
        /// by shape, which is what keeps a panel this stage could not draw from being modelled off
        /// guesses about which label captions which number. The two tables above the strip are consumed
        /// here so the shape walk does not read them a second time.
        /// </summary>
        private bool MarketBandCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            if (widget == null)
            {
                return false;
            }

            if (
                widget.GetComponent<GuiTable>() != null
                || widget.GetComponent<MarketTabRadio>() != null
                || widget.GetComponent<TradableHistoryCurvesPanel>() != null
                || widget.GetComponent<SalableItem>() != null
            )
            {
                return true;
            }

            AgeControlTextField field = widget.GetComponent<AgeControlTextField>();
            if (field == null)
            {
                return false;
            }

            MarketplaceTradableItemsPanel owner = Owner(widget);
            Cell cell = SettingRows.TextFieldCell(
                field,
                null,
                null,
                null,
                null,
                ControlId.Referenced(field, keyPrefix + "quantity"),
                _editor
            );
            if (cell == null)
            {
                return true;
            }

            if (owner != null)
            {
                MarketplaceTradableItemsPanel it = owner;
                cell.Vtable.OnAdjust = (sign, large) => Step(it, sign);
                cell.Vtable.StateText = QuantityText;
                // The arrows work the stepper here rather than a caret, which is the whole of the
                // difference between this box and every other one - so the role word says so.
                cell.Vtable.ControlType = ControlTypes.NumericEditField;
            }

            cells.Add(cell);
            return true;
        }

        /// <summary>One press of the quantity's own stepper. Which button, and what the press means, are
        /// the game's: the modifier the player is holding is what turns one into five or into the whole
        /// stock.</summary>
        private static void Step(MarketplaceTradableItemsPanel panel, int sign)
        {
            try
            {
                AgeControlButton button =
                    sign < 0 ? panel.QuantityMinusButton : panel.QuantityPlusButton;
                if (button != null && AgeWidgets.Operable(button.AgeTransform))
                {
                    AgeWidgets.Press(button);
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: stepping the quantity threw: " + e);
            }
        }

        private static MarketplaceTradableItemsPanel Owner(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < 12; depth++)
                {
                    MarketplaceTradableItemsPanel panel =
                        at.GetComponent<MarketplaceTradableItemsPanel>();
                    if (panel != null)
                    {
                        return panel;
                    }

                    at = at.Parent;
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// The marketplace's tax panel, in whichever of its two forms the game is drawing: the owner's,
        /// with the rate to set and what setting it would cost, or everybody else's, with the owner's
        /// name and the rate they have set (<c>MarketplaceTaxesPanel.Refresh</c> :112-188).
        ///
        /// Read by shape for the same reason as the trading strip, with the rate's own box declared as
        /// the game's editor. Its two stepper buttons move one percentage point per press and have no
        /// coarse variant, so Shift does the same as a plain arrow - the game's own behaviour
        /// (<c>OnIncreaseTaxRateButtonClickCb</c> :240-252).
        /// </summary>
        private void BuildTaxes(GraphBuilder builder, MarketplaceTaxesPanel panel)
        {
            builder.BeginStop(TaxesStop);
            builder.PushContext(PanelName(panel, ModStrings.EconomyTaxesPanel));
            _cells.Clear();
            SidePanels.Content(_cells, panel.AgeTransform, "economy:taxes/", TaxCell, null);
            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        private bool TaxCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            AgeControlTextField field =
                widget == null ? null : widget.GetComponent<AgeControlTextField>();
            if (field == null)
            {
                return false;
            }

            MarketplaceTaxesPanel owner = TaxPanel(widget);
            Cell cell = SettingRows.TextFieldCell(
                field,
                null,
                null,
                null,
                null,
                ControlId.Referenced(field, keyPrefix + "rate"),
                _editor
            );
            if (cell == null)
            {
                return true;
            }

            if (owner != null)
            {
                MarketplaceTaxesPanel it = owner;
                cell.Vtable.OnAdjust = (sign, large) =>
                    AgeWidgets.Press(
                        sign < 0 ? it.DecreaseTaxRateButton : it.IncreaseTaxRateButton
                    );
                cell.Vtable.StateText = () => SettingRows.FieldText(field);
                cell.Vtable.ControlType = ControlTypes.NumericEditField;
            }

            cells.Add(cell);
            return true;
        }

        private static MarketplaceTaxesPanel TaxPanel(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < 12; depth++)
                {
                    MarketplaceTaxesPanel panel = at.GetComponent<MarketplaceTaxesPanel>();
                    if (panel != null)
                    {
                        return panel;
                    }

                    at = at.Parent;
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>What has been traded, newest at the bottom - which is where the game scrolls the list
        /// to. One row per line the game drew, with the turn headers it puts between them; a transaction
        /// of somebody else's is already anonymised by the game before it is written
        /// (<c>MarketplaceExchangeInformationsPanel.Refresh</c> :16-62), so what is drawn is what may be
        /// read.</summary>
        private void BuildLog(GraphBuilder builder, MarketplaceExchangeInformationsPanel panel)
        {
            builder.BeginStop(LogStop);
            builder.PushContext(PanelName(panel, ModStrings.EconomyLogPanel));
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                panel.NotOwnerLabel == null ? null : panel.NotOwnerLabel.AgeTransform,
                "economy:log/not-owner"
            );
            AgeTransform table = panel.TradableTransactionsTable;
            IList<AgeTransform> lines = table == null ? null : table.Children;
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                AgeTransform line = lines[i];
                if (line != null && SettingRows.Drawn(line))
                {
                    Cells.AddReadout(_cells, line, "economy:log/line/" + i);
                }
            }

            Cells.Emit(builder, _cells);
            builder.PopContext();
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
                builder.AddItem(
                    ControlId.Structural("economy:event/" + i),
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => text),
                        },
                    }
                );
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
                builder.AddItem(
                    ControlId.Structural("economy:ad/" + i),
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => text),
                        },
                    }
                );
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
        /// The heading the game draws across a box, as the stop's own first node.
        ///
        /// It is also the stop's NAME, pushed as the context around everything in the box, so Tab into
        /// the box says what the box is. Declaring it as well is not a repetition: the heading carries a
        /// sentence explaining the box that exists nowhere else, and a container name that merely repeats
        /// the control inside it is dropped by the announcer.
        /// </summary>
        private bool AddHeading(GraphBuilder builder, AgeTransform band, object key)
        {
            AgeTransform heading = band == null ? null : AgeWidgets.ChildNamed(band, "Title", 2);
            string text = HeadingText(band);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            builder.PushContext(text);
            Cell cell = Cells.Readout(heading, AgeWidgets.Raw(heading), key.ToString());
            builder.AddItem(cell.Id, cell.Vtable);
            return true;
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
        /// of the mod's - a stop is announced by its name on every Tab into it, and these four panels
        /// could not be measured for a drawn heading.</summary>
        private static string PanelName(GuiPanel panel, string modKey)
        {
            try
            {
                AgeTransform heading =
                    panel == null ? null : AgeWidgets.ChildNamed(panel.AgeTransform, "Title", 2);
                string drawn = heading == null ? null : AgeWidgets.TextOf(heading);
                return string.IsNullOrEmpty(drawn) ? ModStrings.Get(modKey) : drawn;
            }
            catch (Exception)
            {
                return ModStrings.Get(modKey);
            }
        }

        private static void Band(List<AgeTransform> bands, AgeTransform widget)
        {
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

        private static void Emit(GraphBuilder builder, List<Cell> cells, object rowKey)
        {
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                builder.StartRow(rowKey);
                foreach (Cell cell in row)
                {
                    builder.AddItem(cell.Id, cell.Vtable);
                }

                builder.EndRow();
            }
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        // ---- reading the window ----

        /// <summary>Whether the marketplace is the tab being drawn. Asked of what is on screen rather
        /// than of the window's remembered tab, which the game restores from a field other screens
        /// write.</summary>
        private static bool MarketDrawn(global::EconomyScreen window)
        {
            try
            {
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
