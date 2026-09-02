using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>The empire banner across the top of the HUD: what the empire has, what the next turn
    /// does to it, and the buttons drawn along it.</summary>
    public sealed partial class GlobalHud
    {
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
        ///
        /// EACH ROW IS ITS OWN REGION (owner ruling, 2026-08-19). This is the first stop on every
        /// page in the game, and it is four unrelated things stacked in one corner: the strip that
        /// opens the game's screens, the three totals the empire is spending, what is being
        /// researched, and the stockpiles. Walked as one flat stop, the eighth screen icon and the
        /// first total were neighbours with nothing between them saying the player had crossed from
        /// one thing to another. So the rows carry the levels, announced on the way in and not on
        /// every node, which is the shape the galaxy's own panels already have - and each row is a
        /// REGION as well as a level, so that Alt+Up/Down jumps panel to panel down the corner. The two
        /// halves ride on different mechanisms and shipped apart once: a level is announced by the path
        /// diff and a region jump reads the node's own region key, so the rows read as four things and
        /// the jump key still did nothing (owner-reported, 2026-08-19). They are set together now
        /// (<see cref="Name"/>) for exactly that reason.
        ///
        /// The names are the mod's own: the game draws these banners as icons and figures with no
        /// caption anywhere on them (measured - <c>ControlBanner</c>, <c>EmpireBanner</c> and
        /// <c>ResourcesPanel</c> hold tables and value areas and no label of their own), so there is
        /// no game word to prefer. The faction panels the game stacks underneath for the empires that
        /// have them are named too (<see cref="AddFactionPanels"/>), and five of those seven DO have a
        /// game word, because the thing each of them counts is a titled thing in the game's own data.
        ///
        /// The CLUSTER is named over the rows, with the chord that focuses it after the word - "Hud
        /// (Ctrl+H)". The rows say which band the player is in; without a level above them the landing
        /// said "Controls" and never said which corner of the screen that was.
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
            int from = cells.Count;
            AddScreenToggles(cells, window.ControlBanner);
            Name(cells, from, ModStrings.Get(ModStrings.HudControlsPanel), "controls");
            from = cells.Count;
            AddTotals(cells, window.EmpireBanner, empire);
            Name(cells, from, ModStrings.Get(ModStrings.HudKeyResourcesPanel), "key-resources");
            from = cells.Count;
            AddResearch(cells, window.EmpireBanner, empire);
            Name(cells, from, ModStrings.Get(ModStrings.GalaxyResearch), "research");
            from = cells.Count;
            AddStockpiles(cells, window.StrategicsBanner);
            Name(cells, from, ModStrings.Get(ModStrings.HudStrategicResourcesPanel), "strategics");
            AddFactionPanels(cells, window);

            builder.BeginStop(EmpireStop);
            // The cluster's own word, over the rows' words: without it the player lands on "Controls"
            // and is never told which corner of the screen that is. Popped in a finally for the same
            // reason the notification strip's is - a level left open takes every stop after it.
            builder.PushContext(ChordNames.Label(ModStrings.Get(ModStrings.HudPanel), UiActions.FocusEmpire, 0));
            try
            {
                int line = 0;
                foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
                {
                    string named = RowName(row);
                    // EVERY row carries a region, not only the named ones: the jump is asked of the
                    // focused node's own region key, so one unregioned line in the middle of the stop is
                    // a key that does nothing exactly there. A line two panels share has no name to take
                    // one from and takes its place in the stop instead.
                    string region = RowRegion(row);
                    builder.SetRegion(EmpireStop + "/" + (region ?? "line/" + line));
                    line++;
                    if (named != null)
                    {
                        builder.PushContext(named);
                    }

                    builder.StartRow();
                    foreach (Cell cell in row)
                    {
                        builder.AddItem(Nodes.Drawn(cell.Id, cell.Vtable, cell.Widget));
                    }

                    builder.EndRow();
                    if (named != null)
                    {
                        builder.PopContext();
                    }
                }
            }
            finally
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>Name the cells one contributor has just added, so that the row they fall into can
        /// say what it is and be jumped to. Applied AFTER the contributor rather than passed into it:
        /// which panel a cell came from is this method's own knowledge, and the helpers that read the
        /// banners have no business carrying a word about the player's ear.
        ///
        /// The word and the region key are set together and never apart: a row the player hears as a
        /// thing of its own is a row the region jump has to be able to land on, and the two coming from
        /// one call is what stops a later contributor from adding the level and forgetting the key
        /// (which is exactly what the four banner rows shipped with).</summary>
        private static void Name(List<Cell> cells, int from, string named, string region)
        {
            if (string.IsNullOrEmpty(named))
            {
                return;
            }

            for (int i = from; i < cells.Count; i++)
            {
                cells[i].Row = named;
                cells[i].Region = region;
            }
        }

        /// <summary>What a row is called - which is a question about the whole row and not about its
        /// first cell. The rows fall out of the RECTANGLES, so nothing here can promise that one
        /// contributor's cells are a row of their own; a line that has picked up cells from two of
        /// them is a line no single word describes, and it is declared with no level rather than under
        /// the name of whichever cell happened to be leftmost.</summary>
        private static string RowName(List<Cell> row)
        {
            string named = row.Count == 0 ? null : row[0].Row;
            for (int i = 1; i < row.Count; i++)
            {
                if (row[i].Row != named)
                {
                    return null;
                }
            }

            return named;
        }

        /// <summary>Which region a row is, on the same terms as <see cref="RowName"/>: the panel every
        /// cell of it came from, or nothing where the rectangles put two panels on one line.</summary>
        private static string RowRegion(List<Cell> row)
        {
            string region = row.Count == 0 ? null : row[0].Region;
            for (int i = 1; i < row.Count; i++)
            {
                if (row[i].Region != region)
                {
                    return null;
                }
            }

            return region;
        }

        /// <summary>
        /// The two clusters the game draws across the top of every page that is drawn over a view
        /// level, in the order it draws them: what the empire is worth in the left corner, then what
        /// the player is looking at in the centre.
        ///
        /// One call rather than two, because the top of the screen is the same on every such page and
        /// the next page to be modelled should not be able to inherit half of it. A page that has to
        /// put something of its own between them can still call the two halves separately.
        ///
        /// A page standing on a rung of the zoom ladder passes its own ladder in, and it is handed
        /// straight to the cluster that owns the ordering (<see cref="ViewTitle"/>): a system's page and
        /// a planet's are rungs like any other, and the ladder is how the player steps off them.
        /// </summary>
        public void Top(GraphBuilder builder, ZoomLadder zoom = null)
        {
            Empire(builder);
            ViewTitle(builder, zoom);
        }

        /// <summary>
        /// What the player is looking at, as the game writes it across the top centre: the lens that
        /// would X-ray the view and, where the page has one, the zoom ladder - one control per row.
        ///
        /// The words the game draws over the cluster - the view's name - are declared NOWHERE (owner
        /// ruling, 2026-08-18, superseding the level-label reading): the screen already says which page
        /// the player is on when it arrives, so a level repeating it prefixes the first thing in this
        /// stop with a word the player has just heard. The control those words sit on is not declared
        /// either - a Close button carrying the same caption on every page above the galaxy
        /// (<c>TopTitlePanel.Setup</c>) - because Escape already leaves the page and a button called
        /// "Technology Screen" that closes the technology screen reads as the way IN.
        ///
        /// What the cluster IS called on the map is "View Controls", the mod's own words (owner ruling
        /// 2026-08-19): the view's name says which page the player is on, which the screen has already
        /// said on arrival, while what this stop holds is the two controls over how that page is being
        /// looked at.
        ///
        /// The lens is named by the game, and what it is named changes as the camera climbs: the map's
        /// zoom step picks a layer descriptor and the descriptor picks the lens, so the same button
        /// reads "Diplomacy scan" from far out and "System scan" up close. The label is read live for
        /// exactly that reason, and the game hides the whole group on the pages that have no lens.
        ///
        /// The zoom comes FIRST where a page has one (owner ruling): it is what the player reaches for,
        /// and the lens is the rarer errand. A page passes its own ladder in rather than appending it
        /// afterwards, because the order is this cluster's to decide.
        ///
        /// A page with neither a lens nor a ladder declares no stop at all - an empty stop is a Tab
        /// press that lands nowhere - which is why this answers whether it declared one.
        /// </summary>
        public bool ViewTitle(GraphBuilder builder, ZoomLadder zoom = null)
        {
            GameOverlayWindow window = OverlayWindow();
            TopTitlePanel panel = window == null ? null : window.TopTitlePanel;
            // Flow control: a stop, a lens and a zoom ladder are opened under it, and the answer tells
            // the caller whether a stop was declared at all.
            if (panel == null || !panel.Shown || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return false;
            }

            bool ladder = zoom != null && ZoomLadder.Rungs;
            if (!ladder && !ScanDrawn(panel))
            {
                return false;
            }

            builder.BeginStop(ViewTitleStop);
            if (!ladder)
            {
                AddScanToggle(builder, panel);
                return true;
            }

            // Named only where the ladder is - the galaxy map, the one page whose cluster holds more
            // than the lens button. A page showing the lens alone is one control, and a level over a
            // single control is a word said before every reading of it (owner ruling 2026-08-19).
            builder.PushContext(ModStrings.Get(ModStrings.HudViewControlsPanel));
            zoom.Build(builder, "hud:view-title/zoom");
            AddScanToggle(builder, panel);
            builder.PopContext();
            return true;
        }

        /// <summary>The lens toggle. The tooltip explaining it is hung on the GROUP around the label
        /// and the icon rather than on the button, which is what the game shows a tooltip for and so is
        /// what the pointer is aimed at.</summary>
        private static void AddScanToggle(GraphBuilder builder, TopTitlePanel panel)
        {
            if (!ScanDrawn(panel))
            {
                return;
            }

            AgeTransform group = panel.ScanGroup;
            AgeControlButton button = panel.ScanButton;
            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(group);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Label(panel.ScanLabel),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(group),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, group);
            builder.AddItem(Nodes.Drawn(ControlId.For(button, "hud:view-title/scan"), vtable, button));
        }

        /// <summary>Whether the page has a lens at all - asked before the stop is begun as well as
        /// while filling it, because a stop with nothing in it is a Tab press that lands nowhere.
        /// </summary>
        private static bool ScanDrawn(TopTitlePanel panel)
        {
            return panel.ScanGroup != null
                && panel.ScanButton != null
                && AgeWidgets.Visible(panel.ScanGroup);
        }

        /// <summary>A control on its way into the graph, still carrying the widget it was read from:
        /// the rows are worked out from the whole cluster at once, which cannot be done while
        /// declaring it row by row.</summary>
        private sealed class Cell
        {
            public AgeTransform Widget;
            public ControlId Id;
            public NodeVtable Vtable;

            /// <summary>What the row this cell lands in is called, or null where nothing has named it
            /// (<see cref="RowName"/>).</summary>
            public string Row;

            /// <summary>Which region of the stop the row this cell lands in IS - the panel it was read
            /// off, in a word that is not the player's, because a region key has to be the same string
            /// on every rebuild and the name is a localized one. Null where nothing has named it
            /// (<see cref="RowRegion"/>).</summary>
            public string Region;
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        /// <summary>The strip of icons along the top, each of which opens one of the game's screens.
        /// The game gives them no captions at all - the name of the screen and the key that opens it
        /// are in the tooltip, which is where both are read from.
        ///
        /// A toggle can also carry a BADGE with a sentence of its own - the senate icon's dot, "The
        /// leading political party in the Senate" - and that sentence exists nowhere else on the
        /// screen. Every tooltip inside the toggle is therefore declared, in drawn order, with the
        /// button's OWN speaking and the badges reviewable: which of a row's tooltips speaks is the
        /// screen's call where the row is a control plus a badge, and what the button OPENS is the
        /// thing a player standing on it asked for (measured 2026-08-23: the badge's sentence had no
        /// surface at all).</summary>
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
                    // Banding input: AddCell appends straight to the list, so the gate never sees these
                    // until the strip has already been worked into rows by their rectangles.
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
                    List<AgeTooltip> inside = new List<AgeTooltip>(2);
                    AgeWidgets.Tooltips(widget, inside);
                    if (inside.Count > 1)
                    {
                        vtable.Sections = ToggleSections(inside);
                    }

                    AgeWidgets.PointAt(vtable, widget);
                    cells.Add(
                        new Cell
                        {
                            Widget = widget,
                            Id = ControlId.For(
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

        /// <summary>One icon.s explanations as sections: the FIRST is the icon.s own and speaks; every
        /// later one is a badge inside it and is reviewable. The order is the drawn one the resolver
        /// answers in - the control.s own tooltip, then the badges inside it - so the button says what it
        /// opens and the badge.s sentence is a buffer line away rather than nowhere at all. Which of
        /// several tooltips is the icon.s OWN is a fact about the icon; how loudly that one reads is the
        /// tooltip.s own kind to answer.
        ///
        /// The one place in the mod where a badge stays a REVIEWED section rather than becoming an entry
        /// of its own, and it is a shape constraint rather than a judgement: the strip's icons are laid
        /// into a graph ROW, and a node inside an open row cannot be a group
        /// (<c>GraphBuilder.BeginGroup</c> refuses one). The badges are content-backed sentences, so
        /// dropping them would lose real words rather than an empty promise - which is what the ruling
        /// asks for everywhere it CAN be applied. Reported 2026-08-28; also unverified live, because no
        /// screen icon in this fixture carries a second tooltip at all (measured: 0 of 8).</summary>
        private static IList<NodeSection> ToggleSections(List<AgeTooltip> tooltips)
        {
            List<NodeSection> sections = new List<NodeSection>(tooltips.Count);
            for (int i = 0; i < tooltips.Count; i++)
            {
                if (i > 0)
                {
                    NodeSection badge = GraphNodes.ReviewedTooltipSection(tooltips[i]);
                    if (badge != null)
                    {
                        sections.Add(badge);
                    }

                    continue;
                }

                IList<NodeSection> tip = GraphNodes.HintSections(tooltips[i]);
                for (int j = 0; tip != null && j < tip.Count; j++)
                {
                    sections.Add(tip[j]);
                }
            }

            return sections.Count == 0 ? null : sections;
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
            // Banding input: AddCell appends without the gate's question, and the banner's readouts are
            // worked into one row by where they are drawn.
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
                    Id = ControlId.For(label, "hud:empire/" + key),
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
                    Id = ControlId.For(it, "hud:empire/research"),
                    Vtable = vtable,
                }
            );
            AddResearchBuyout(cells, banner);
        }

        /// <summary>
        /// Buying the technology being researched outright, from the button the banner draws at the end
        /// of the research line.
        ///
        /// Same rule the construction queue's buy-outs follow (ES2 facts): the game HIDES this button
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
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Buyouts.Cost(it)));
            GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Offered(at));
            AgeWidgets.PointAt(vtable, at);
            cells.Add(
                new Cell
                {
                    Widget = at,
                    Id = ControlId.For(buyout, "hud:empire/research-buyout"),
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
                    // Banding input: same door as the rest of the banner - AddCell takes the item
                    // without asking the gate, and its rectangle is what puts it on the strip's row.
                    if (resource == null || !AgeWidgets.Visible(item.AgeTransform))
                    {
                        continue;
                    }

                    GuiLocatedResource it = resource;
                    ResourceItem row = item;
                    NodeVtable vtable = GraphNodes.Readout(
                        () => AgeText.Clean(it.Title),
                        () => ResourceRows.Figures(row),
                        null,
                        item.Tooltip
                    );
                    AgeWidgets.Point(vtable, item.Button, item.Tooltip, item.AgeTransform);
                    cells.Add(
                        new Cell
                        {
                            Widget = item.AgeTransform,
                            Id = ControlId.For(item, "hud:empire/resource/" + resource.Name),
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

    }
}
