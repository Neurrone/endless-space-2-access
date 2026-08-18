using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// A hero's own window: the card, the ship they fly and the wheel of skills they can spend a level
    /// on, opened by every Inspect button in the game (the Academy's, a fleet row's hero cell, the
    /// hero picker's, the level-up notification's).
    ///
    /// <b>It is three pages, not one.</b> The window is a hub: a strip of three panels that SLIDES
    /// sideways so that exactly one of them is over the frame (<c>SwitchHubMode</c>
    /// <c>HeroInspectionModalWindow.cs:182-217</c>) - the ship designer on the left, the overview in the
    /// middle, the skill tree on the right. Which page is up is the game's own state
    /// (<c>CurrentHubMode</c>), and that is what this screen is built off: during the slide BOTH panels
    /// are still drawn, so "what is visible" would declare two pages at once for a fifth of a second.
    /// The page's own drawn heading is a node, first in reading order, and a page change puts the cursor
    /// back on the page's landing - which is what says a page changed, in the game's own words for it.
    ///
    /// The game's own way into the side pages is Left and Right (<c>HandleInput</c> :125-148), and those
    /// arrows are the mod's while this screen is focused, so that route is dead. It costs nothing: each
    /// page is opened by a button the game DRAWS - the pencil in the overview's ship and skill boxes -
    /// and left by the "Hero Overview" tab the side pages draw against the window's edge.
    ///
    /// <b>Nothing here commits anything.</b> Both side pages are previews: fitting a module rewrites a
    /// copy of the design, spending a skill point appends to a pending list and unlocks the skill on a
    /// COPY of the hero (<c>SkillTreeEditionPanel.OnSkillCb</c> :451-470), and the only things that post
    /// an order are the Apply buttons along the bottom (<c>ApplySkills</c> :79-100,
    /// <c>ApplyShipDesign</c>). Reset drops the pending set. So Enter on a skill is the dot's own click
    /// and is safe, which is why it needs no confirmation of the mod's.
    ///
    /// Escape is the game's, and it means two different things on purpose: from a side page it goes back
    /// to the overview, and from the overview it closes the window - behind the game's own
    /// "you will lose your changes" box when either side page has been touched (:103-124).
    ///
    /// The ship page is the same <c>ShipDesignEditionPanel</c> prefab the Military screen's designer
    /// hosts, read by <see cref="ShipDesignRows"/>; this window's INSTANCE is asked for from the
    /// window's own subtree, because two of them are alive at once and a scene-wide lookup finds
    /// whichever was instantiated first.
    /// </summary>
    public sealed class HeroInspectionScreen : Screen
    {
        /// <summary>The prefix this window's ids and stops are keyed under.</summary>
        private const string Keys = "hero:";

        private static readonly object TitleStop = "hero:title";
        private static readonly object ShipOverviewStop = "hero:ship";
        private static readonly object CardStop = "hero:card";
        private static readonly object SkillsOverviewStop = "hero:skills";
        private static readonly object StoryStop = "hero:story";
        private static readonly object TreeInfoStop = "hero:tree-info";
        private static readonly object TreeStop = "hero:tree";
        private static readonly object TreeStatsStop = "hero:tree-stats";
        private static readonly object ActionsStop = "hero:actions";

        /// <summary>The prefix the shared ship-designer reader keys the ship page under - its own, not
        /// this window's, because the same reader serves the designer modal and the two must not collide
        /// in a graph that only ever holds one of them.</summary>
        private const string ShipKeys = "hero-ship";

        /// <summary>The handler each of the two pencils in the overview is wired to. Which transform
        /// carries it is prefab data, so they are found by what they DO.</summary>
        private const string EditShipHandler = "OnEditShipDesignCb";

        private const string EditSkillsHandler = "OnEditSkillTreeCb";

        /// <summary>The card's own name row, which is also the rename button
        /// (<c>HeroInspectionModalWindow.OnRenameCb</c> :500-506).</summary>
        private const string RenameHandler = "OnRenameCb";

        private readonly TextFieldEditor _editor = new TextFieldEditor();

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>The page the cursor has been put on. It changes when the game changes page, which is
        /// the one thing on this screen that happens without a key of the mod's having been
        /// pressed.</summary>
        private HeroInspectionModalWindow.HeroHubMode _seen;

        public override string Key
        {
            get { return "screen.hero-inspection"; }
        }

        /// <summary>Over everything that opens it - the Academy page and the hero picker at 15 and 28,
        /// the level-up notification at 18 - and under the two things it can raise itself: the rename box
        /// at 80 and the lose-changes message box at 100.</summary>
        public override int Layer
        {
            get { return 45; }
        }

        /// <summary>The hero and the page, which is what has just opened. The page's own heading says the
        /// same words where it is drawn, so focus deliberately does not land on it.</summary>
        public override string ScreenName
        {
            get
            {
                try
                {
                    HeroInspectionModalWindow window = Window();
                    string name = window == null || window.GuiHero == null
                        ? null
                        : AgeText.Clean(window.GuiHero.Title);
                    string title = AgeWidgets.TextOf(Heading(window));
                    string said = new MessageBuilder().Fragment(name).Fragment(title).Build();
                    return string.IsNullOrEmpty(said)
                        ? ModStrings.Get(ModStrings.ScreenHeroInspection)
                        : said;
                }
                catch (Exception)
                {
                    return ModStrings.Get(ModStrings.ScreenHeroInspection);
                }
            }
        }

        /// <summary>What the page is for: the hero on the overview, the wheel on the skill page, the
        /// design on the ship page. The heading is a Shift+Tab away, and saying it as the screen name and
        /// then again as the first control is the one thing an arrival must not do.</summary>
        public override object InitialFocusStop
        {
            get
            {
                switch (Mode())
                {
                    case HeroInspectionModalWindow.HeroHubMode.SkillTree:
                        return TreeStop;
                    case HeroInspectionModalWindow.HeroHubMode.ShipDesign:
                        return ShipDesignRows.InfoStop(ShipKeys);
                    default:
                        return CardStop;
                }
            }
        }

        /// <summary>Escape is the game's, and it does two different things with it: back to the overview
        /// from a side page, and close from the overview - behind its own confirmation once either side
        /// page has been touched.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>False while the ship page's name box has been asked for and the keyboard has not
        /// changed hands yet: what the player types next belongs in the box, not in a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override bool IsActive()
        {
            try
            {
                HeroInspectionModalWindow window = Window();
                // The window keeps its hero only while it is bound (<c>Unbind</c> at end-hide), and a
                // window with no hero draws three empty pages.
                return window != null
                    && window.Shown
                    && window.IsReady
                    && window.GuiHero != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void OnPush()
        {
            _seen = Mode();
        }

        public override void OnPop()
        {
            _editor.Cancel();
        }

        public override void OnUpdate()
        {
            _editor.Update();
            FollowPage();
        }

        /// <summary>
        /// The player has changed page - pressed a pencil, pressed the "Hero Overview" tab, or pressed
        /// Escape on a side page - and the whole screen now means something else.
        ///
        /// Two things happen, in this order, and both are needed. The page's own name is SAID, because
        /// the graph would not say it: the cursor is about to land in the page's content, and the one
        /// node that carries the page's name is its heading. And the cursor is dropped, because the
        /// graph's own focus recovery would otherwise keep whichever node survived the change - the
        /// Close button along the bottom is on every page - and leave the player standing on the old
        /// page's business. The next build seats it again, on this page's own landing.
        /// </summary>
        private void FollowPage()
        {
            try
            {
                HeroInspectionModalWindow.HeroHubMode mode = Mode();
                if (mode == HeroInspectionModalWindow.HeroHubMode.None || mode == _seen)
                {
                    return;
                }

                _seen = mode;
                // Queued, not interrupting: it is something that happened, and the landing's readout
                // follows it.
                Voice.Say(AgeWidgets.TextOf(Heading(Window())), false);
                ModEntry.Navigator.Blur();
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: following the page change threw: " + e);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            HeroInspectionModalWindow window = Window();
            if (window == null || Settling(window))
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                switch (window.CurrentHubMode)
                {
                    case HeroInspectionModalWindow.HeroHubMode.SkillTree:
                        BuildSkillPage(builder, window);
                        break;
                    case HeroInspectionModalWindow.HeroHubMode.ShipDesign:
                        BuildShipPage(builder, window);
                        break;
                    default:
                        BuildOverview(builder, window);
                        break;
                }

                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the window threw: " + e);
            }
        }

        /// <summary>
        /// Whether the page is still arriving, in which case this screen declares NOTHING.
        ///
        /// Two tests, and both were needed. The slide is one 0.3-second interpolation of the strip's own
        /// offset (measured: <c>HubPanelsTable.AgeFirstModifierSet</c> holds a single
        /// <c>PixelOffsetLeft</c> item of duration 0.3), and a page read while it is moving reads
        /// correctly but reads switched OFF - every control on it announces itself as unavailable, which
        /// is a refusal that is not there. And the strip stops moving a frame or more BEFORE the engine
        /// switches the page back on, so waiting for the slide alone still catches the last of those
        /// frames (measured: the card announced "unavailable" on exactly the frame the slide ended).
        /// This is the same arrival-on-operability gate the Academy page needs against its own modals.
        ///
        /// Eighteen frames of an empty graph is the cheap answer: the game ignores its own keys for this
        /// window while the strip moves too (<c>HandleInput</c> :103-148 tests the same flag), so there
        /// is nothing a player can do with the page while it is on its way in.
        /// </summary>
        private static bool Settling(HeroInspectionModalWindow window)
        {
            try
            {
                AgeModifierSet slide =
                    window.HubPanelsTable == null ? null : window.HubPanelsTable.AgeFirstModifierSet;
                if (slide != null && slide.ModifiersRunning)
                {
                    return true;
                }

                AgeTransform page = Page(window);
                return page == null || !AgeWidgets.Operable(page);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The panel the strip is showing, which is the one this screen declares.</summary>
        private static AgeTransform Page(HeroInspectionModalWindow window)
        {
            switch (window.CurrentHubMode)
            {
                case HeroInspectionModalWindow.HeroHubMode.SkillTree:
                    return window.SkillTreeEditionPanel == null
                        ? null
                        : window.SkillTreeEditionPanel.AgeTransform;
                case HeroInspectionModalWindow.HeroHubMode.ShipDesign:
                    return window.ShipDesignHolder == null
                        ? null
                        : window.ShipDesignHolder.AgeTransform;
                case HeroInspectionModalWindow.HeroHubMode.Overview:
                    return window.OverviewPanel == null
                        ? null
                        : window.OverviewPanel.AgeTransform;
                default:
                    return null;
            }
        }

        // ---- the page's own heading ----

        /// <summary>The one line the game writes across the top of whichever page is up. Declared once,
        /// in the first stop, and never repeated per band.</summary>
        private void BuildHeading(GraphBuilder builder, HeroInspectionModalWindow window)
        {
            AgeTransform heading = Heading(window);
            if (heading == null)
            {
                return;
            }

            builder.BeginStop(TitleStop);
            _cells.Clear();
            Cells.AddReadout(_cells, heading, Keys + "heading");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The label the current page draws its own name in.
        ///
        /// Each page keeps it somewhere else: the skill tree's is the panel's own
        /// <c>TitleLabel</c> (<c>SkillTreeBasePanel.cs:10</c>, which is also where the game appends
        /// " (read only)"), the ship page's is <c>ShipDesignPanelTitle</c>, and the overview's is a plain
        /// label the prefab names and binds to nothing at all.
        /// </summary>
        private static AgeTransform Heading(HeroInspectionModalWindow window)
        {
            try
            {
                if (window == null)
                {
                    return null;
                }

                switch (window.CurrentHubMode)
                {
                    case HeroInspectionModalWindow.HeroHubMode.SkillTree:
                        return Drawn(Widget(window.SkillTreeEditionPanel.TitleLabel));
                    case HeroInspectionModalWindow.HeroHubMode.ShipDesign:
                        return Drawn(Widget(window.ShipDesignPanelTitle));
                    default:
                        return AgeWidgets.ChildNamed(
                            window.OverviewPanel.AgeTransform,
                            "OverviewTitle",
                            1
                        );
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the overview page ----

        /// <summary>
        /// The middle page, in the three columns the game draws it in: the ship on the left, the hero's
        /// card in the middle, the skill wheel on the right, and then the hero's own story across the
        /// bottom.
        ///
        /// Focus lands on the CARD rather than on the leftmost column, because the hero is what every
        /// Inspect button in the game promised.
        /// </summary>
        private void BuildOverview(GraphBuilder builder, HeroInspectionModalWindow window)
        {
            HeroOverviewPanel panel = window.OverviewPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            BuildShipBox(builder, panel);
            BuildCard(builder, panel);
            BuildSkillsBox(builder, panel);
            BuildStory(builder, panel);
        }

        /// <summary>
        /// The read-only summary of the hero's ship: its name, hull, size and role, the modules fitted
        /// into it drawn as dots over a rendered ship, and the six figures the game puts along the
        /// bottom.
        ///
        /// Every one of them is a readout - there is not one control in the box except the pencil that
        /// opens the ship page. The box as a WHOLE is also a click that opens that page (a second
        /// <c>EditShipDesignButton</c> stretched over all 300x400 of it, measured), and that one is not
        /// declared: it is the mouse's way of doing what the pencil does, the same reason the Academy's
        /// hero pills are left out.
        /// </summary>
        private void BuildShipBox(GraphBuilder builder, HeroOverviewPanel panel)
        {
            ShipDesignOverviewPanel box = panel.ShipDesignOverviewPanel;
            if (box == null || !AgeWidgets.Visible(box.AgeTransform))
            {
                return;
            }

            builder.BeginStop(ShipOverviewStop);
            string title = AgeText.Label(box.TitleLabel);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            // Three runs, and a region each, because the box draws three things: what the design IS, the
            // dots over the rendered ship, and the figures along the bottom. Every one of them reads one
            // node per row - a grid of characteristics and a grid of figures are peers of one kind whose
            // wrap points belong to the box, not to the data - so the regions are what tells the three
            // apart, and they cover the whole stop so the jump key can leave from anywhere in it.
            builder.SetRegion("hero:ship/characteristics");
            _cells.Clear();
            AddPencil(box.AgeTransform, EditShipHandler, "overview/edit-ship");
            // The name comes from the design and not from the box the game squeezed it into - see
            // ShipDesignRows.OverviewName.
            ShipDesignOverviewPanel it = box;
            AddLine(box.NameLabel, null, "overview/name", () => ShipDesignRows.OverviewName(it));
            AddLine(box.HullLabel, box.HullTooltip, "overview/hull");
            AddLine(box.SizeLabel, box.SizeTooltip, "overview/size");
            AddLine(box.RoleLabel, box.RoleTooltip, "overview/role");
            AddLine(box.Bonus1Label, box.Bonus1Tooltip, "overview/bonus1");
            AddLine(box.Bonus2Label, box.Bonus2Tooltip, "overview/bonus2");
            Cells.EmitLinear(builder, _cells);

            builder.SetRegion("hero:ship/modules");
            _cells.Clear();
            AddFittedModules(box);
            Cells.EmitLinear(builder, _cells);

            builder.SetRegion("hero:ship/figures");
            _cells.Clear();
            AddStats(box);
            Cells.EmitLinear(builder, _cells);
            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>
        /// The dots the game draws over the rendered ship, one per slot the hull has.
        ///
        /// They draw no words at all - a filled slot is a coloured sector, an empty one a ring - and
        /// what each stands for is on the wrapper behind its tooltip, which is the module's name or the
        /// game's own word for an empty slot (<c>ShipDesignOverviewSlotItem.Bind</c> sets
        /// <c>Target = GuiSlot</c>).
        ///
        /// Keyed on the ITEM and not on the slot behind it, unlike the ship page's rows: the two pages
        /// draw the same slots, and a shared backing object is what the graph's focus recovery follows
        /// first - so a page change would silently drag the cursor from one page's row onto the other
        /// page's, announcing a node nobody asked for on the way.
        /// </summary>
        private void AddFittedModules(ShipDesignOverviewPanel box)
        {
            try
            {
                AgeTransform container = box.ShipDesignSlotItemsContainer;
                if (container == null || !AgeWidgets.Visible(container))
                {
                    return;
                }

                ShipDesignOverviewSlotItem[] slots =
                    container.GetComponentsInChildren<ShipDesignOverviewSlotItem>(true);
                for (int i = 0; i < slots.Length; i++)
                {
                    ShipDesignOverviewSlotItem slot = slots[i];
                    if (
                        slot == null
                        || slot.GuiSlot == null
                        || !Painted(slot.AgeTransform)
                    )
                    {
                        continue;
                    }

                    AgeTooltip tooltip = slot.SlotTooltip ?? AgeWidgets.Raw(slot.AgeTransform);
                    string name = AgeWidgets.TooltipTitle(tooltip);
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => name),
                        },
                        Sections = GraphNodes.Sections(null, tooltip),
                    };
                    AgeWidgets.PointAt(vtable, slot.AgeTransform);
                    Cells.Add(
                        _cells,
                        slot.AgeTransform,
                        ControlId.Referenced(slot.AgeTransform, Keys + "overview/slot/" + i),
                        vtable
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the fitted modules threw: " + e);
            }
        }

        /// <summary>The six figures along the bottom of the box - named by the one map every host of this
        /// panel shares (<see cref="ShipDesignRows.AddSimpleStats"/>).</summary>
        private void AddStats(ShipDesignOverviewPanel box)
        {
            ShipDesignRows.AddSimpleStats(_cells, box, Keys + "overview/");
        }

        /// <summary>
        /// The hero's card, which on this page is also the rename button: the row the card draws the
        /// hero's name in is wired to <c>OnRenameCb</c> (measured - the transform is
        /// <c>HeroTitle</c>), and clicking it raises the game's own name box. So the card is one node
        /// saying the hero's name, doing what a click on that name does, and holding the whole drawn
        /// card in its review buffer.
        ///
        /// The card's own assignment row is a button too - it puts the galaxy view on wherever the hero
        /// is posted - and it is a child node, the only one this card draws that goes anywhere. It is
        /// switched off while the hero has no posting, which is the game's own answer and needs no test
        /// here.
        /// </summary>
        private void BuildCard(GraphBuilder builder, HeroOverviewPanel panel)
        {
            HeroDetailedCard card = panel.HeroInspectionCard;
            if (card == null || !AgeWidgets.Visible(card.AgeTransform))
            {
                return;
            }

            builder.BeginStop(CardStop);
            _cells.Clear();
            AgeControlButton rename = HeroCards.Wired(card, RenameHandler);
            AgeTransform row = AgeWidgets.Transform(rename);
            HeroDetailedCard it = card;
            AgeTooltip tooltip = AgeWidgets.Raw(row);
            NodeVtable vtable = GraphNodes.Button(
                HeroCards.Name(card),
                () => AgeWidgets.PressPropagating(rename),
                () => AgeWidgets.Operable(row),
                tooltip
            );
            vtable.Sections = GraphNodes.Sections(() => HeroCards.Lines(it), tooltip);
            AgeWidgets.Point(vtable, rename, tooltip, card.AgeTransform);
            Cells.Add(
                _cells,
                card.AgeTransform,
                ControlId.Referenced(card.AgeTransform, Keys + "card"),
                vtable
            );
            HeroCards.Buttons(_cells, card, Keys + "card");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The skill wheel as the overview draws it: the points the hero has left to spend, the three
        /// branches as coloured pies, and the pencil that opens the page where they can be spent.
        ///
        /// It is not the wheel again - reading the wheel twice would be reading it wrong. What the
        /// overview draws of a branch is a pie with an arc for progress and no words anywhere, so a
        /// branch here is its name and the game's own sentence about what it contains, and how far along
        /// it is belongs to the page that draws the figures.
        /// </summary>
        private void BuildSkillsBox(GraphBuilder builder, HeroOverviewPanel panel)
        {
            SkillTreeBasePanel box = panel.SkillTreeOverviewPanel;
            if (box == null || !AgeWidgets.Visible(box.AgeTransform))
            {
                return;
            }

            builder.BeginStop(SkillsOverviewStop);
            string title = AgeText.Label(box.TitleLabel);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            _cells.Clear();
            AddPencil(box.AgeTransform, EditSkillsHandler, "overview/edit-skills");
            AddRow(box.SkillPointsGroup, "overview/points");
            Cells.EmitLinear(builder, _cells);

            // The branches are drawn as slices of one circle - the icons sit wherever the middle of each
            // slice happens to fall - so they read as the list of three they are, in the order the game
            // slices the circle up in. That order, and not where the icons landed, is also the order the
            // skill page's own branches and the figures beside them are in.
            _cells.Clear();
            try
            {
                HeroSkillTreeItem[] trees =
                    box.AgeTransform.GetComponentsInChildren<HeroSkillTreeItem>(true);
                for (int i = 0; i < trees.Length; i++)
                {
                    HeroSkillTreeItem tree = trees[i];
                    AgeTransform icon = tree == null ? null : Widget(tree.IconImage);
                    if (tree.SkillTreeDefinition == null || !AgeWidgets.Visible(icon))
                    {
                        continue;
                    }

                    AgeTooltip tooltip = AgeWidgets.Raw(icon);
                    string name = TreeName(tree);
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => name),
                        },
                        Sections = GraphNodes.Sections(null, tooltip),
                    };
                    AgeWidgets.PointAt(vtable, icon);
                    // Keyed on the ICON, not on the branch definition the skill page's groups are keyed
                    // on: the two pages draw the same three branches, and a shared backing object is
                    // what the graph's focus recovery follows first, so a page change would drag the
                    // cursor onto the other page's node and announce it on the way.
                    Cells.Add(
                        _cells,
                        icon,
                        ControlId.Referenced(icon, Keys + "overview/branch/" + i),
                        vtable
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the overview's branches threw: " + e);
            }

            for (int i = 0; i < _cells.Count; i++)
            {
                builder.AddItem(_cells[i].Id, _cells[i].Vtable);
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>The paragraph the page draws across its bottom edge: who the hero is, in the game's
        /// own words, permanently on screen inside a scroll view. Always-drawn text, so it is spoken in
        /// full as the line it is rather than left to a tooltip rule.</summary>
        private void BuildStory(GraphBuilder builder, HeroOverviewPanel panel)
        {
            HeroDetailedCard card = panel.HeroInspectionCard;
            AgeTransform story = card == null ? null : Widget(card.DescriptionLabel);
            if (
                story == null
                || !AgeWidgets.Visible(story)
                || string.IsNullOrEmpty(HeroCards.Description(card)())
            )
            {
                return;
            }

            builder.BeginStop(StoryStop);
            _cells.Clear();
            Cells.AddReadout(_cells, story, Keys + "story");
            Cells.EmitLinear(builder, _cells);
        }

        // ---- the skill-tree page ----

        /// <summary>The right-hand page, in the three columns the game draws it in: what the hero has to
        /// spend and what the pending picks would do down the left edge, the wheel in the middle, and the
        /// figures about the wheel down the right edge.</summary>
        private void BuildSkillPage(GraphBuilder builder, HeroInspectionModalWindow window)
        {
            SkillTreeEditionPanel panel = window.SkillTreeEditionPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            BuildTreeInfo(builder, panel);
            BuildTrees(builder, panel);
            BuildTreeStats(builder, panel);
        }

        /// <summary>
        /// The left-hand column: the points left to spend, where the hero is posted, and the block of
        /// bonuses the pending picks would produce.
        ///
        /// The bonuses are the whole feedback a preview has - spending a point rewrites them and posts
        /// no order - so they are read as the game writes them: one line per effect, under the caption
        /// that says which situation the effects apply in
        /// (<c>SkillTreeEditionPanel.RefreshEffects</c> :398-404 binds one item per
        /// <c>DescriptorEffectSet</c> of the PREVIEWED hero).
        /// </summary>
        private void BuildTreeInfo(GraphBuilder builder, SkillTreeEditionPanel panel)
        {
            builder.BeginStop(TreeInfoStop);
            _cells.Clear();
            try
            {
                AddRow(panel.SkillPointsGroup, "tree/points");
                AddRow(Assignment(panel), "tree/assignment");
                AddCaption(panel.SkillEffectsTable, 3, "tree/effects-caption");
                IList<AgeTransform> items = panel.SkillEffectsTable == null
                    ? null
                    : panel.SkillEffectsTable.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AddEffectSet(items[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the skill page's left column threw: " + e);
            }

            // Declaration order, not the drawn rows: the column is one stack, and the bonuses are the
            // one band on this window whose contents the game rebuilds from a pooled table while the
            // player watches. A pooled item the game has not re-laid-out yet overlaps its neighbour, and
            // banding by drawn position then reads two blocks of effects a line at a time, alternating
            // (measured, after a Reset).
            for (int i = 0; i < _cells.Count; i++)
            {
                builder.AddItem(_cells[i].Id, _cells[i].Vtable);
            }
        }

        /// <summary>One block of the bonuses list: the situation it applies in, then one line per effect
        /// the game wrote into it.</summary>
        private void AddEffectSet(AgeTransform widget, int index)
        {
            PanelFeatureEffectsSetsItem item =
                widget == null ? null : widget.GetComponent<PanelFeatureEffectsSetsItem>();
            if (item == null || !Painted(widget))
            {
                return;
            }

            Cells.AddReadout(_cells, Widget(item.TitleLabel), Keys + "tree/effect/" + index);
            AgeTransform table =
                item.EffectMapper == null ? null : item.EffectMapper.EffectLinesTable;
            IList<AgeTransform> lines = table == null ? null : table.Children;
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                if (Painted(lines[i]))
                {
                    Cells.AddReadout(
                        _cells,
                        lines[i],
                        Keys + "tree/effect/" + index + "/line/" + i
                    );
                }
            }
        }

        /// <summary>
        /// The wheel: three branches, four rings each, one or two skills to a ring.
        ///
        /// A wheel is not a list and this one is not really a wheel either - it is three pies
        /// (<c>SkillTreeBasePanel.BindSkillTree</c> :182-193 gives each branch a sector of the circle),
        /// each of four rings out from the middle (<c>HeroSkillTreeItem.BindSkillTreeStage</c> :148-157),
        /// with the ring's skills spread along its arc. So it is declared as the tree it is: branch,
        /// ring, skill, in the order the game laid them out - which is the order of the definitions
        /// themselves, inner ring first.
        ///
        /// A ring is LOCKED until the hero has spent enough points to reach it
        /// (<c>SkillTreeEditionPanel.CountPointsAndEnableStages</c> :238-300 disables the ring item, and
        /// the pending picks count towards it), and a locked ring's skills refuse for free: the game
        /// leaves each dot's own Enable flag ON and switches the RING off, so the answer comes from the
        /// ancestor walk rather than from the dot (measured - all 21 dots read Enable true while three
        /// rings of each branch are disabled).
        /// </summary>
        private void BuildTrees(GraphBuilder builder, SkillTreeEditionPanel panel)
        {
            AgeTransform table = panel.SkillTreesTable;
            IList<AgeTransform> trees = table == null ? null : table.Children;
            if (trees == null)
            {
                return;
            }

            builder.BeginStop(TreeStop);
            builder.PushContext(ModStrings.Get(ModStrings.HeroSkillTrees));
            try
            {
                for (int i = 0; i < trees.Count; i++)
                {
                    HeroSkillTreeItem tree =
                        trees[i] == null ? null : trees[i].GetComponent<HeroSkillTreeItem>();
                    if (
                        tree == null
                        || tree.SkillTreeDefinition == null
                        || !Painted(tree.AgeTransform)
                    )
                    {
                        continue;
                    }

                    ControlId id = ControlId.Referenced(
                        tree.SkillTreeDefinition,
                        Keys + "tree/branch/" + i
                    );
                    builder.BeginGroup(id, BranchVtable(panel, tree, i));
                    BuildStages(builder, panel, tree, i);
                    builder.EndGroup();
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the wheel threw: " + e);
            }

            builder.PopContext();
        }

        /// <summary>One branch of the wheel: what the game calls it, how much of it is done, and the
        /// sentence the game hangs on the icon at its centre.</summary>
        private static NodeVtable BranchVtable(
            SkillTreeEditionPanel panel,
            HeroSkillTreeItem tree,
            int index
        )
        {
            AgeTransform icon = Widget(tree.IconImage);
            AgeTooltip tooltip = AgeWidgets.Raw(icon);
            string name = TreeName(tree);
            NodeVtable vtable = GraphNodes.Group(() => name, null, tooltip);
            SkillTreeEditionPanel owner = panel;
            int at = index;
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Completion(owner, at), false));
            AgeWidgets.PointAt(vtable, icon);
            return vtable;
        }

        /// <summary>How far along a branch is, in the words the page itself draws for it down the
        /// right-hand column ("0/12"): the completion lines are bound one per branch in branch order
        /// (<c>SkillTreeEditionPanel.RefreshSkillTrees</c> :302-307).</summary>
        private static string Completion(SkillTreeEditionPanel panel, int index)
        {
            try
            {
                IList<AgeTransform> lines = panel.TreeCompletionLinesTable == null
                    ? null
                    : panel.TreeCompletionLinesTable.Children;
                if (lines == null || index < 0 || index >= lines.Count)
                {
                    return null;
                }

                SkillTreeCompletionLine line = lines[index].GetComponent<SkillTreeCompletionLine>();
                return line == null ? null : AgeText.Label(line.PointsLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The rings of one branch, inner first - the order the game binds them in and the
        /// order they unlock in.</summary>
        private void BuildStages(
            GraphBuilder builder,
            SkillTreeEditionPanel panel,
            HeroSkillTreeItem tree,
            int branch
        )
        {
            IList<AgeTransform> stages = tree.SkillTreeStagesTable == null
                ? null
                : tree.SkillTreeStagesTable.Children;
            for (int i = 0; stages != null && i < stages.Count; i++)
            {
                HeroSkillTreeStageItem stage =
                    stages[i] == null ? null : stages[i].GetComponent<HeroSkillTreeStageItem>();
                if (
                    stage == null
                    || stage.SkillTreeStage == null
                    || !Painted(stage.AgeTransform)
                )
                {
                    continue;
                }

                HeroSkillTreeStageItem it = stage;
                int ring = i;
                int rings = stages.Count;
                NodeVtable vtable = GraphNodes.Group(
                    () => StageName(ring, rings),
                    () => AgeWidgets.Operable(it.AgeTransform)
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => StageRequirement(it), false));
                builder.BeginGroup(
                    ControlId.Referenced(
                        stage.SkillTreeStage,
                        Keys + "tree/branch/" + branch + "/ring/" + i
                    ),
                    vtable
                );
                BuildSkills(builder, panel, stage, branch, i);
                builder.EndGroup();
            }
        }

        /// <summary>
        /// Which ring this is, counted out from the middle.
        ///
        /// The game has no name for a ring. The only words it writes anywhere near one are the three
        /// legends down the right-hand column, each a threshold with a leader line pointing at the ring
        /// it unlocks ("Used Skill Points 4":
        /// <c>SkillTreeEditionPanel.RefreshLevelLabels</c> :363-382 writes
        /// <c>Gui.Localize("%SkillTreeStageLevelTitle") + RequiredLevel</c>). Naming the ring from that
        /// was reading the leader line as a caption: every ring of every branch announced itself as
        /// "Used skill points 0" and nothing said what the figure was about. So the ring is named for
        /// where it is - which is the one thing a sighted player can see about it without following a
        /// line - and the threshold is said as the sentence it means, in
        /// <see cref="StageRequirement"/>.
        /// </summary>
        private static string StageName(int ring, int rings)
        {
            return ModStrings.Format(ModStrings.HeroSkillRing, ring + 1, rings);
        }

        /// <summary>
        /// How many points have to have been spent anywhere in the wheel before this ring opens - the
        /// figure the page draws in its right-hand legend, said as what it means.
        ///
        /// Taken from the ring's own definition rather than from the legend labels, because the page
        /// draws one set of them for all three branches and one fewer than there are rings: the outermost
        /// ring's threshold is drawn nowhere at all. The innermost ring asks for nothing, and says so by
        /// saying nothing.
        /// </summary>
        private static string StageRequirement(HeroSkillTreeStageItem stage)
        {
            try
            {
                int required = stage.SkillTreeStage.RequiredLevel;
                return required <= 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.HeroSkillRingPoint,
                        ModStrings.HeroSkillRingPoints,
                        required
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The dots along one ring's arc, in the order the ring's definition lists them.
        /// </summary>
        private void BuildSkills(
            GraphBuilder builder,
            SkillTreeEditionPanel panel,
            HeroSkillTreeStageItem stage,
            int branch,
            int ring
        )
        {
            IList<AgeTransform> skills = stage.SkillTreeSkillsTable == null
                ? null
                : stage.SkillTreeSkillsTable.Children;
            for (int i = 0; skills != null && i < skills.Count; i++)
            {
                HeroSkillTreeSkillItem skill =
                    skills[i] == null
                        ? null
                        : skills[i].GetComponent<HeroSkillTreeSkillItem>();
                if (
                    skill == null
                    || skill.HeroSkillDefinition == null
                    || !Painted(skill.AgeTransform)
                )
                {
                    continue;
                }

                builder.AddItem(
                    ControlId.Referenced(
                        skill.HeroSkillDefinition,
                        Keys + "tree/branch/" + branch + "/ring/" + ring + "/skill/" + i
                    ),
                    SkillVtable(panel, skill)
                );
            }
        }

        /// <summary>
        /// One skill, which is one press of "spend a point on this".
        ///
        /// It is a BUTTON and not the tick the game drew, because pressing it repeatedly buys the next
        /// level each time up to the skill's last: the dot's tick only means "a level of this is
        /// pending" (<c>HeroSkillTreeSkillItem.Refresh</c> :139 writes
        /// <c>Toggle.State = GetPendingLevels &gt; 0</c>), and a checkbox that said "ticked" would be
        /// saying the wrong thing about a skill on its second of three levels. So what it says is the
        /// level it stands at out of the levels it has, and then that a further level is pending - which
        /// is exactly what the game draws as a ring of coloured arcs round the dot (:143-158).
        ///
        /// Enter is the dot's own click - state first, then the handler, which is what the mouse does
        /// (the game's own handler flips the tick back and lets its refresh rewrite it). It commits
        /// nothing: <c>OnSkillCb</c> :451-470 appends to a pending list and unlocks the skill on a copy
        /// of the hero. A skill at its last level, and one whose ring is locked, are already REFUSING
        /// without anything here: the game switches the dot off at :140-142 for the first and the ring
        /// off for the second.
        ///
        /// <b>The one refusal the mod composes itself.</b> A skill that names other skills as
        /// prerequisites is one the game will not let stand: <c>Refresh</c> :101-130 writes
        /// <c>%NeedTheseSkills</c> and the missing skills' titles onto the dot's tooltip and switches the
        /// dot off - and then line 142 switches it back ON and line 159 overwrites the tooltip with the
        /// skill's own name, so by the time anybody can read either one, both are gone. The game lets the
        /// click through and undoes it on its next refresh. So the test is made here, off the same
        /// <c>RequiredSkills</c> the game reads, and the sentence is put back together out of the game's
        /// own words - approved as a deliberate deviation. No skill in the base game's own hero trees
        /// declares a required skill, so this path belongs to the bonus trees a Nakalim hero carries.
        /// </summary>
        private static NodeVtable SkillVtable(
            SkillTreeEditionPanel panel,
            HeroSkillTreeSkillItem skill
        )
        {
            SkillTreeEditionPanel owner = panel;
            HeroSkillTreeSkillItem it = skill;
            AgeTooltip tooltip = skill.Tooltip ?? AgeWidgets.Raw(skill.AgeTransform);
            Func<string> missing = () => Missing(owner, it);
            Func<bool> enabled = () =>
                AgeWidgets.Operable(it.AgeTransform) && string.IsNullOrEmpty(missing());
            Func<string> level = () => SkillLevel(owner, it);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => SkillName(it)),
                    GraphNodes.DisabledPart(enabled),
                    GraphNodes.ValuePart(level),
                    new NodeAnnouncement(missing, live: false),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                // What the press produced, read back at once - and nothing at all from a dot that is
                // refusing, which is what every other refusing control in the mod does: the reason was
                // said when focus arrived. A dot the game leaves ENABLED while the hero has no point to
                // spend answers with the level it still stands at, which is the only sign the press
                // went nowhere (the game answers it with a sound).
                StateText = () => enabled() ? level() : null,
                OnActivate = () =>
                {
                    if (enabled())
                    {
                        AgeWidgets.Toggle(it.Toggle);
                    }
                },
            };
            AgeWidgets.Point(vtable, it.Toggle, tooltip, it.AgeTransform);
            return vtable;
        }

        /// <summary>
        /// What the game calls a skill. Off the wrapper the dot builds for it, which is what the tooltip
        /// window heads its dossier with.
        ///
        /// The wrapper is right for a DOT and would be wrong for a starting skill, where it answers the
        /// generic "Starting Skill" instead (<c>GuiHeroSkill.Title</c> :22-32) - but a dot is never a
        /// starting skill (the panel builds those wrappers with the flag set, and only for the box in the
        /// right-hand column: <see cref="Named"/>). Going to the skill's DEFINITION instead would be
        /// worse in both places: <c>GuiWrapper.Title</c> reads the skill's gui element and answers empty
        /// when there is none, while <c>Gui.GetTitle</c> on the definition's name answers the engine's
        /// "(missing GuiElement)" debug string for one of this hero's skills and, for the other, a key
        /// that resolves to the HERO's name (measured on Dmitri Lenko: "HeroSkill01Terrans04 (missing
        /// GuiElement)" and "Dmitri Lenko").
        /// </summary>
        private static string SkillName(HeroSkillTreeSkillItem skill)
        {
            try
            {
                return skill.GuiHeroSkill != null
                    ? AgeText.Clean(skill.GuiHeroSkill.Title)
                    : AgeText.Clean(Gui.Localize(Gui.GetTitle(skill.HeroSkillDefinition.Name)));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where the skill stands: the level the hero has of it out of the levels it has at
        /// all, and then the level a pending pick would take it to. Both are the numbers the game paints
        /// the ring of arcs round the dot with - one arc per level, coloured for owned, for pending, and
        /// for neither (<c>HeroSkillTreeSkillItem.Refresh</c> :143-158).</summary>
        private static string SkillLevel(
            SkillTreeEditionPanel panel,
            HeroSkillTreeSkillItem skill
        )
        {
            try
            {
                HeroSkillDefinition definition = skill.HeroSkillDefinition;
                int levels = definition.SkillLevels.Length;
                int owned = panel.GuiHero.GetHeroSkillLevel(definition) + 1;
                int pending = panel.GetTotalSkillLevel(definition) + 1;
                MessageBuilder message = new MessageBuilder();
                message.ListItem(ModStrings.Format(ModStrings.HeroSkillLevel, owned, levels));
                if (pending > owned)
                {
                    message.ListItem(ModStrings.Format(ModStrings.HeroSkillPending, pending));
                }

                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The skills this one needs first and the hero has not got, in the game's own words -
        /// the sentence the game writes and then overwrites, put back together. Null where there is
        /// nothing missing, which is every skill in the base game's own hero trees.</summary>
        private static string Missing(
            SkillTreeEditionPanel panel,
            HeroSkillTreeSkillItem skill
        )
        {
            try
            {
                HeroSkillDefinition[] required = skill.HeroSkillTreeSkill.RequiredSkills;
                if (required == null || required.Length == 0)
                {
                    return null;
                }

                MessageBuilder message = new MessageBuilder();
                int missing = 0;
                for (int i = 0; i < required.Length; i++)
                {
                    if (panel.GetTotalSkillLevel(required[i]) >= 0)
                    {
                        continue;
                    }

                    if (missing++ == 0)
                    {
                        message.Fragment(AgeText.Clean(Gui.Localize(NeedTheseSkills)));
                    }

                    message.ListItem(
                        AgeText.Clean(Gui.Localize(Gui.GetTitle(required[i].Name)))
                    );
                }

                return missing == 0 ? null : message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string NeedTheseSkills = "%NeedTheseSkills";

        /// <summary>
        /// The right-hand column, box by box in the order the game stacks them: how much of each branch
        /// is done, the points each ring needs, the skills the hero started with, the masteries they
        /// have built up - and, for a hero who can carry them, the relic skills.
        ///
        /// Read box by box because three of the four say their figures with an icon and no words: a
        /// starting skill and a mastery line are named only on the wrapper behind their tooltips, and a
        /// completion line is a figure and a name drawn side by side.
        /// </summary>
        private void BuildTreeStats(GraphBuilder builder, SkillTreeEditionPanel panel)
        {
            AgeTransform banner = Banner(panel);
            IList<AgeTransform> boxes = banner == null ? null : banner.Children;
            if (boxes == null)
            {
                return;
            }

            builder.BeginStop(TreeStatsStop);
            _cells.Clear();
            try
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    AgeTransform box = boxes[i];
                    if (!Painted(box))
                    {
                        continue;
                    }

                    if (HoldsLegends(box, panel))
                    {
                        continue;
                    }

                    if (Holds(box, panel.TreeCompletionLinesTable))
                    {
                        AddBox(box, panel.TreeCompletionLinesTable, "tree/completion");
                    }
                    else if (Holds(box, panel.StartingSkillItemsTable))
                    {
                        AddNamedBox(box, panel.StartingSkillItemsTable, "tree/starting");
                    }
                    else if (
                        panel.HeroMasteryPanel != null
                        && Holds(box, panel.HeroMasteryPanel.MasteryLinesContainer)
                    )
                    {
                        AddNamedBox(
                            box,
                            panel.HeroMasteryPanel.MasteryLinesContainer,
                            "tree/mastery"
                        );
                    }
                    else if (Holds(box, panel.RelicSkillItemsTable))
                    {
                        AddRelics(box, panel);
                    }
                    else
                    {
                        AddBox(box, null, "tree/box/" + i);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the skill page's right column threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>A box of the right-hand column: whatever heading it draws, and then one line per row
        /// of the table inside it. Both are read as ROWS rather than walked into: a heading is a group
        /// holding a label and an icon, and its explaining sentence is on the label rather than on the
        /// group, which is what <see cref="AddRow"/> is for.</summary>
        private void AddBox(AgeTransform box, AgeTransform table, string key)
        {
            AddHeads(box, table, key);
            IList<AgeTransform> rows = table == null ? null : table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                AddRow(rows[i], key + "/row/" + i);
            }
        }

        /// <summary>Whatever a box draws above its table - a heading, or in one case the three ring
        /// legends that are the whole of the box.</summary>
        private void AddHeads(AgeTransform box, AgeTransform table, string key)
        {
            IList<AgeTransform> children = box.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && !ReferenceEquals(child, table))
                {
                    AddRow(child, key + "/head/" + i);
                }
            }
        }

        /// <summary>The same for a box whose rows draw an icon and a figure and keep their NAME on the
        /// wrapper behind a tooltip: a mastery line (the level reached out of the highest this hero can
        /// reach) and a starting skill (which draws nothing but its own symbol).</summary>
        private void AddNamedBox(AgeTransform box, AgeTransform table, string key)
        {
            AddHeads(box, table, key);
            IList<AgeTransform> rows = table == null ? null : table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                AgeTransform row = rows[i];
                if (!Painted(row))
                {
                    continue;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(row);
                string name = Named(tooltip);
                AgeTransform at = row;
                NodeVtable vtable = GraphNodes.Readout(
                    () => name,
                    () => AgeWidgets.TextOf(at),
                    null,
                    tooltip
                );
                AgeWidgets.PointAt(vtable, row);
                Cells.Add(_cells, row, ControlId.Referenced(row, Keys + key + "/row/" + i), vtable);
            }
        }

        /// <summary>
        /// What a row drawn as a bare symbol is called: the name the game keeps on the wrapper behind
        /// its tooltip.
        ///
        /// For a starting skill that name is the game's own word for the KIND of thing - "Starting
        /// Skill" (<c>GuiHeroSkill.Title</c> :22-32 answers that for a starting skill and not the
        /// skill's own title) - so a hero with two of them has two rows saying the same words, and what
        /// tells them apart is the dossier each draws. That is the game's own reading of its own icons,
        /// and it was re-measured on Dmitri Lenko's two: <c>Gui.GetTitle</c> on their definitions answers
        /// the engine's "HeroSkill01Terrans04 (missing GuiElement)" for one and, for the other, a key that
        /// resolves to the HERO's own name ("Dmitri Lenko"). So there is nothing better to say, and both
        /// alternatives would be worse than the game's own word for the kind.
        /// </summary>
        private static string Named(AgeTooltip tooltip)
        {
            return AgeWidgets.TooltipTitle(tooltip);
        }

        /// <summary>
        /// The relic skills a Nakalim or Templar hero can learn, which the game draws as a flat strip
        /// rather than as a wheel and gates on relics rather than on skill points
        /// (<c>SkillTreeEditionPanel.OnRelicSkillCb</c> :513-544). Pressing one is its own toggle, and
        /// unlike a skill it really is a toggle: the same press learns and unlearns.
        /// </summary>
        private void AddRelics(AgeTransform box, SkillTreeEditionPanel panel)
        {
            IList<AgeTransform> children = box.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && !ReferenceEquals(child, panel.RelicSkillItemsTable))
                {
                    Cells.AddReadout(_cells, child, Keys + "tree/relic/head/" + i);
                }
            }

            IList<AgeTransform> rows = panel.RelicSkillItemsTable == null
                ? null
                : panel.RelicSkillItemsTable.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                RelicSkillTreeItem relic =
                    rows[i] == null ? null : rows[i].GetComponent<RelicSkillTreeItem>();
                if (
                    relic == null
                    || relic.HeroSkillDefinition == null
                    || !Painted(relic.AgeTransform)
                )
                {
                    continue;
                }

                RelicSkillTreeItem it = relic;
                AgeTooltip tooltip = relic.Tooltip ?? AgeWidgets.Raw(relic.AgeTransform);
                NodeVtable vtable = GraphNodes.Checkbox(
                    () => RelicName(it),
                    () => it.Toggle != null && it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    () => AgeWidgets.Operable(it.AgeTransform),
                    tooltip
                );
                AgeWidgets.Point(vtable, it.Toggle, tooltip, it.AgeTransform);
                Cells.Add(
                    _cells,
                    relic.AgeTransform,
                    ControlId.Referenced(relic.AgeTransform, Keys + "tree/relic/row/" + i),
                    vtable
                );
            }
        }

        private static string RelicName(RelicSkillTreeItem relic)
        {
            try
            {
                return relic.GuiHeroSkill != null
                    ? AgeText.Clean(relic.GuiHeroSkill.Title)
                    : AgeText.Clean(Gui.Localize(Gui.GetTitle(relic.HeroSkillDefinition.Name)));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the ship-design page ----

        /// <summary>The left-hand page: the same designer body the Military screen's Create and Edit
        /// buttons open, hosted here with a different band of buttons along the bottom. The panel is
        /// asked for from THIS window's subtree, because the designer modal has one of its own and a
        /// scene-wide lookup would find whichever was instantiated first.</summary>
        private void BuildShipPage(GraphBuilder builder, HeroInspectionModalWindow window)
        {
            ShipDesignEditionPanel panel = Designer(window);
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            ShipDesignRows.Build(builder, panel, ShipKeys, _editor, _cells);
        }

        private static ShipDesignEditionPanel Designer(HeroInspectionModalWindow window)
        {
            try
            {
                return window.GetComponentInChildren<ShipDesignEditionPanel>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the band along the bottom ----

        /// <summary>
        /// Everything the window draws outside its three pages: the "Hero Overview" tab a side page
        /// draws against the window's edge, and the row along the bottom.
        ///
        /// Which buttons are in that row is the question, and the game answers it by hiding them
        /// (<c>RefreshButtons</c> :328-402 shows exactly the ones this page offers and writes each
        /// refusal onto its tooltip - Apply Design and Reset Design on the ship page, Apply Skills and
        /// Reset Skills on the skill page, and on the overview whichever single Apply matches what has
        /// been touched). The one thing in the row that is not a button is the sentence the window
        /// writes for a hero it will not let the player change, which is declared as the line of text it
        /// is. Read off the band in drawn order, so nothing here needs a test of the mod's.
        /// </summary>
        private void BuildActions(GraphBuilder builder, HeroInspectionModalWindow window)
        {
            _cells.Clear();
            try
            {
                AddControl(AgeWidgets.Transform(window.BackToOverviewLeftButton), "back-left");
                AddControl(AgeWidgets.Transform(window.BackToOverviewRightButton), "back-right");
                AgeTransform band = Band(window);
                IList<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child == null || !AgeWidgets.Visible(child))
                    {
                        continue;
                    }

                    if (AgeWidgets.Button(child) != null)
                    {
                        Cells.AddControl(_cells, child, Keys + "button/" + child.name);
                        continue;
                    }

                    Cells.AddReadout(_cells, child, Keys + "note/" + child.name);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the button band threw: " + e);
            }

            if (_cells.Count == 0)
            {
                return;
            }

            builder.BeginStop(ActionsStop);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The row of buttons along the bottom, found through one of its members: the window
        /// exposes every button in it and not the group they sit in.</summary>
        private static AgeTransform Band(HeroInspectionModalWindow window)
        {
            try
            {
                AgeTransform button = AgeWidgets.Transform(window.ApplyDesignButton);
                return button == null ? null : button.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- reading the window ----

        /// <summary>One of the two pencils in the overview: the small one the game draws in a box's
        /// heading, not the invisible one stretched over the whole box. Both are wired to the same
        /// handler, so they are told apart by size - the drawn one is the one a sighted player can see.
        /// </summary>
        private void AddPencil(AgeTransform box, string handler, string key)
        {
            try
            {
                AgeControlButton[] found = box.GetComponentsInChildren<AgeControlButton>(true);
                AgeControlButton pencil = null;
                float smallest = float.MaxValue;
                for (int i = 0; i < found.Length; i++)
                {
                    AgeControlButton button = found[i];
                    AgeTransform widget = AgeWidgets.Transform(button);
                    if (
                        button == null
                        || button.OnActivateMethod != handler
                        || !AgeWidgets.Visible(widget)
                    )
                    {
                        continue;
                    }

                    float area = widget.Width * widget.Height;
                    if (area < smallest)
                    {
                        smallest = area;
                        pencil = button;
                    }
                }

                Cells.AddControl(_cells, AgeWidgets.Transform(pencil), Keys + key);
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: looking for the '" + handler + "' button threw: " + e);
            }
        }

        private void AddControl(AgeTransform widget, string key)
        {
            Cells.AddControl(_cells, widget, Keys + key);
        }

        /// <summary>A captioned row of a column: the words the game drew in it and the sentence it
        /// explains itself with, which these rows hang on a child rather than on the row.</summary>
        private void AddRow(AgeTransform row, string key)
        {
            if (!Painted(row))
            {
                return;
            }

            AgeTooltip tooltip = SettingRows.LastTooltip(row);
            AgeTransform at = row;
            NodeVtable vtable = GraphNodes.Readout(
                () => null,
                () => AgeWidgets.TextOf(at),
                null,
                tooltip
            );
            AgeWidgets.PointAt(vtable, tooltip == null ? row : tooltip.AgeTransform);
            Cells.Add(_cells, row, ControlId.Referenced(row, Keys + key), vtable);
        }

        /// <summary>One line of a box - the words the game drew in it and the sentence explaining
        /// them.</summary>
        private void AddLine(
            AgePrimitiveLabel label,
            AgeTooltip tooltip,
            string key,
            Func<string> name = null
        )
        {
            AgeTransform widget = Widget(label);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform at = widget;
            Func<string> said = name ?? (() => AgeWidgets.TextOf(at));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(said) },
                Sections = GraphNodes.Sections(null, tooltip ?? AgeWidgets.Raw(widget)),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(_cells, widget, ControlId.Referenced(widget, Keys + key), vtable);
        }

        /// <summary>The caption a box draws over a table buried some way inside it - the scroll view the
        /// bonuses live in puts three groups between the two. The first label of the ancestor that has
        /// one, which is where every one of these boxes draws its heading.</summary>
        private void AddCaption(AgeTransform table, int up, string key)
        {
            AgeTransform at = table;
            for (int i = 0; at != null && i < up; i++)
            {
                at = at.Parent;
            }

            IList<AgeTransform> children = at == null ? null : at.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (
                    child != null
                    && AgeWidgets.Visible(child)
                    && child.GetComponent<AgePrimitiveLabel>() != null
                )
                {
                    Cells.AddReadout(_cells, child, Keys + key);
                    return;
                }
            }
        }

        /// <summary>The group the skill page draws the hero's posting in: the caption and the value
        /// together, which is the row a sighted player reads.</summary>
        private static AgeTransform Assignment(SkillTreeEditionPanel panel)
        {
            try
            {
                AgeTransform label = Widget(panel.AssignmentLabel);
                AgeTransform line = label == null ? null : label.Parent;
                return line == null ? null : line.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The right-hand column of the skill page, found through one of the tables in it: the
        /// panel exposes every table and not the column they are stacked in.</summary>
        private static AgeTransform Banner(SkillTreeEditionPanel panel)
        {
            try
            {
                AgeTransform table = panel.TreeCompletionLinesTable;
                AgeTransform box = table == null ? null : table.Parent;
                return box == null ? null : box.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether a box of the right-hand column is the one holding the ring legends - the three
        /// thresholds the page draws with a leader line each into the wheel.
        ///
        /// It is skipped, alone among the boxes. As lines of their own they are three bare figures in
        /// the order the page stacked them, with nothing saying which ring each line points at and no
        /// line at all for the outermost ring; the figures are said where they belong instead, on the
        /// rings (<see cref="StageRequirement"/>).
        /// </summary>
        private static bool HoldsLegends(AgeTransform box, SkillTreeEditionPanel panel)
        {
            AgePrimitiveLabel[] legends = panel.StageLevelLabels;
            for (int i = 0; legends != null && i < legends.Length; i++)
            {
                if (Holds(box, Widget(legends[i])))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a box of a column is the one holding a table.</summary>
        private static bool Holds(AgeTransform box, AgeTransform table)
        {
            AgeTransform at = table;
            for (int i = 0; at != null && i < 6; i++)
            {
                if (ReferenceEquals(at, box))
                {
                    return true;
                }

                at = at.Parent;
            }

            return false;
        }

        /// <summary>What the game calls a branch of the wheel. Its own title is a localization KEY - the
        /// game writes it into a label and lets the engine resolve it - so it is resolved here.</summary>
        private static string TreeName(HeroSkillTreeItem tree)
        {
            try
            {
                return AgeText.Clean(Gui.Localize(Gui.GetTitle(tree.SkillTreeDefinition.Name)));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private HeroInspectionModalWindow.HeroHubMode Mode()
        {
            HeroInspectionModalWindow window = Window();
            return window == null
                ? HeroInspectionModalWindow.HeroHubMode.None
                : window.CurrentHubMode;
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

        private static AgeTransform Widget(AgePrimitiveImage image)
        {
            try
            {
                return image == null ? null : image.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Drawn(AgeTransform widget)
        {
            return widget != null && AgeWidgets.Visible(widget) ? widget : null;
        }

        /// <summary>Whether a row of a pooled table is really on the screen - every table on this window
        /// is built with <c>ReserveChildren</c>, and a row the game has stopped using is left in the pool
        /// FADED rather than hidden (measured: after a Reset the bonuses box kept a whole extra effect
        /// block and two extra lines, all invisible on screen and all reading <c>Visible</c>). The test
        /// itself is <see cref="AgeWidgets.Painted"/>, shared with every other pooled table in the
        /// mod.</summary>
        private static bool Painted(AgeTransform widget)
        {
            return AgeWidgets.Painted(widget);
        }

        private static HeroInspectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<HeroInspectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
