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
    /// The page's own drawn heading is a node, first in reading order - except on the skill page, where
    /// the heading names the wheel that is the whole page and is said as the wheel's own name instead -
    /// and a page change puts the cursor back on the page's landing, which is what says a page changed,
    /// in the game's own words for it.
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
    public sealed partial class HeroInspectionScreen : Screen
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
            get { return ModStrings.ScreenHeroInspection; }
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

        /// <summary>
        /// Escape does two different things here - back to the overview from a side page, and close from
        /// the overview, behind its own confirmation once either side page has been touched - and the mod
        /// takes the key so that its own Back does all of them.
        ///
        /// Taking it costs the game nothing, because <see cref="Back"/> hands the key straight back to
        /// the window through the very control the game wired to Escape. Claimed only while that control
        /// is there to press: a window drawing no close button keeps its own Escape.
        /// </summary>
        public override bool ConsumesBack
        {
            get { return WindowShape.CloseControl(Window()) != null; }
        }

        /// <summary>
        /// The window's own Escape, replayed as a press of the button the game wired it to.
        ///
        /// The close button's handler is <c>OnCancelCb</c>, and <c>GuiModalWindow.OnCancelCb</c>
        /// (:102-105) is nothing but <c>HandleInput(InputAction.Exit)</c> - so this ONE press is the
        /// whole of the game's Escape branch (<c>HeroInspectionModalWindow.HandleInput</c> :101-123):
        /// a side page switches back to the overview, the overview hides the window, and a page the
        /// player has edited raises the game's own lose-changes confirmation first. None of that is
        /// re-implemented here, which is the point - the branch reads private state (the ship design
        /// panel) and answers its confirmation with a private callback, and a copy would drift.
        ///
        /// Pressing the control rather than calling <c>HandleInput</c> directly is deliberate: the
        /// direct call is what wedged the screen stack once before (test-recipes, "Resetting game
        /// state").
        /// </summary>
        public override bool Back()
        {
            return WindowShape.PressClose(Window());
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
                switch (window.CurrentHubMode)
                {
                    case HeroInspectionModalWindow.HeroHubMode.SkillTree:
                        BuildSkillPage(builder, window);
                        break;
                    case HeroInspectionModalWindow.HeroHubMode.ShipDesign:
                        BuildHeading(builder, window);
                        BuildShipPage(builder, window);
                        break;
                    default:
                        BuildHeading(builder, window);
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

        /// <summary>
        /// The one line the game writes across the top of whichever page is up. Declared once, in the
        /// first stop, and never repeated per band.
        ///
        /// Not on the SKILL page, where the same words are the name of the panel below them and are
        /// said there instead (<see cref="BuildTrees"/>): the page is the wheel, so a stop holding
        /// nothing but its heading is a stop between the player and the only thing on the page.
        /// </summary>
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
                        return Drawn(AgeWidgets.Transform(window.SkillTreeEditionPanel.TitleLabel));
                    case HeroInspectionModalWindow.HeroHubMode.ShipDesign:
                        return Drawn(AgeWidgets.Transform(window.ShipDesignPanelTitle));
                    default:
                        return WindowShape.TitleWidget(
                            window.OverviewPanel.AgeTransform,
                            OverviewTitleNames
                        );
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly string[] OverviewTitleNames = { "OverviewTitle" };

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
                    // Flow control: a child the band is not drawing is not one of this hero's pages, and is not walked.
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
            Cells.AddControl(
                _cells,
                AgeWidgets.Transform(AgeWidgets.WiredTo(box, handler)),
                Keys + key
            );
        }

        private void AddControl(AgeTransform widget, string key)
        {
            Cells.AddControl(_cells, widget, Keys + key);
        }

        /// <summary>A captioned row of a column: the words the game drew in it and the sentence it
        /// explains itself with, which these rows hang on a child rather than on the row.</summary>
        private void AddRow(AgeTransform row, string key)
        {
            if (!AgeWidgets.Painted(row))
            {
                return;
            }

            if (AddDossierRow(row, key))
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
            Cells.Add(_cells, row, ControlId.For(row, Keys + key), vtable);
        }

        /// <summary>
        /// A row that is not one line with one explanation but SEVERAL dossiers side by side - the
        /// hero's card draws four wordless symbols in one strip (the ship it is on, its affinity, its
        /// party, its class) and keeps the whole page about each behind its own symbol.
        ///
        /// Read as one row it kept the LAST of the four and dropped three, silently: a row carries one
        /// tooltip in the ordinary reading, and nothing in the speech says the other three exist. So a
        /// row with more than one dossier becomes a NODE PER DOSSIER, in one row, each named by the
        /// game's own header for it - which is the same treatment a card's badges get everywhere else.
        ///
        /// Only for a row that really has several: one dossier, or several with no names to tell them
        /// apart, is the ordinary captioned row and is left exactly as it was.
        /// </summary>
        private bool AddDossierRow(AgeTransform row, string key)
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(4);
            TooltipChildren.AddInside(found, row);
            if (found.Count < 2)
            {
                return false;
            }

            for (int i = 0; i < found.Count; i++)
            {
                AgeTransform at = found[i].Anchor ?? row;
                Cells.Add(
                    _cells,
                    at,
                    ControlId.Structural(Keys + key + "/dossier/" + i),
                    TooltipChildren.Node(found[i])
                );
            }

            return true;
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
            AgeTransform widget = AgeWidgets.Transform(label);
            if (widget == null)
            {
                return;
            }

            AgeTransform at = widget;
            Func<string> said = name ?? (() => AgeWidgets.TextOf(at));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(said) },
            };
            // BOTH sentences, because the prefab hangs two different ones on two different widgets and
            // the node stands on the inner of them. The overview draws each fact as a GROUP holding an
            // icon and a label: the CAPTION ("The hull of this Ship") sits on the group, while the value's
            // own dossier - the size class's rules, the role's targeting table - sits on the label, and
            // for the name there is no second one at all. Reading only the label's therefore left every
            // caption on this box painted and unsaid (measured 2026-08-28: four of them), and reading only
            // the group's would have thrown away the richer half. This is the same split
            // <see cref="Captions"/> records for block captions - the word and its explanation on
            // different widgets - met here one level further in.
            // TWO HOVER TARGETS, TWO NODES. The overview draws each fact as a GROUP holding an icon and
            // a label, and the game hangs a different tooltip on each: the CAPTION ("The hull of this
            // Ship") on the group, and the value's own dossier - the size class's rules, the role's
            // targeting table - on the label inside it. Only one tooltip can be on screen at a time, so
            // a node that declared both could only ever raise whichever it pointed at, and the other
            // half was words the player was promised and never shown.
            //
            // So the line keeps the caption - it is what a mouse resting anywhere on the line raises,
            // and the group encloses everything - and the inner dossier becomes a nested entry under it
            // aimed at the label, exactly as a technology's dot carries one entry per thing it unlocks
            // (<see cref="TooltipChildren"/>, and <c>ResearchScreen.Unlocks</c> for the precedent).
            // Focusing the entry puts the pointer on the label, so the game draws that card and no
            // other.
            AgeTooltip inner = tooltip ?? AgeWidgets.Raw(widget);
            AgeTooltip caption = AgeWidgets.Raw(Parent(widget));
            bool nests = inner != null && caption != null && !AgeWidgets.SameTooltip(inner, caption);
            vtable.Sections = GraphNodes.Sections(null, caption ?? inner);
            AgeWidgets.PointAt(vtable, widget, caption ?? inner);
            Cell cell = Cells.Add(_cells, widget, ControlId.For(widget, Keys + key), vtable);
            if (!nests)
            {
                return;
            }

            List<TooltipChildren.Dossier> nested = new List<TooltipChildren.Dossier>(1);
            TooltipChildren.AddPlain(nested, inner, widget);
            if (nested.Count > 0)
            {
                cell.Dossiers = nested;
                cell.Key = Keys + key;
            }
        }

        private static AgeTransform Parent(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
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
                // Flow control: a child the group is not drawing holds no words this fact put there.
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
                AgeTransform label = AgeWidgets.Transform(panel.AssignmentLabel);
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
                if (AgeWidgets.Under(AgeWidgets.Transform(legends[i]), box))
                {
                    return true;
                }
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


        private static AgeTransform Drawn(AgeTransform widget)
        {
            // Content: the shared answer to "is the game drawing this", which the callers branch on rather than declare.
            return widget != null && AgeWidgets.Visible(widget) ? widget : null;
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

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }
    }
}
