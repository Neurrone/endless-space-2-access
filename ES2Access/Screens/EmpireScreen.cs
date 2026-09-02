using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The empire summary: the page F1 opens, with the empire's own description down the left edge and
    /// two tabs in the middle - every system the empire holds as a table, and how the empire is doing
    /// against each way of winning.
    ///
    /// The tab bar is the game's own instant switch: pressing a tab hides one panel and shows the other
    /// in the same frame, so what is declared is whatever is DRAWN rather than which tab the window
    /// remembers - other screens set that field on their way here (a notification, a colony panel), and
    /// a page modelled off the remembered value would describe the tab the player is not looking at.
    ///
    /// The systems table is the same <c>SystemListTable</c> the system-selection window binds, read by
    /// the shared table reader (<see cref="TableSheet"/>): rows down the name column, figures across,
    /// column captions spoken as the edge the player crosses. What is this page's own is that five of
    /// the fourteen columns are BUTTONS - status, population, construction, hangar and the assigned hero
    /// - and each opens a different panel under the table. That two-step is the game's own and is
    /// replayed as one gesture: the cell's own button records which cell was clicked and the click then
    /// propagates to the row's toggle, which is what actually opens the panel
    /// (<see cref="AgeWidgets.PressPropagating"/>; <c>PropagateInteraction</c> measured true on all five
    /// of this table's cell buttons). A cell the game has switched off - the construction column of an
    /// outpost or a ghost - is declared REFUSING with the sentence the game wrote into it, which is why
    /// those five columns are read here rather than by the sheet's ordinary value cell: the refusal
    /// lives on the CELL while the sheet's own availability question is about the ROW.
    ///
    /// The panel a cell opens is a second instance of the prefabs the star system page draws along its
    /// own bottom edge, so the planet cards, the constructibles, the queue and the hangar are read by
    /// the shared readers rather than modelled twice (<see cref="SystemPanels"/>).
    ///
    /// Population MOVES here, and this page offers one the star system page does not. The panel holding
    /// the cards is its own drop client (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :167-205) and takes
    /// a dropped unit two ways: onto another CARD, which is the move between two planets of this system,
    /// and onto the population cell of ANOTHER SYSTEM's row in the table, which points this system's
    /// spaceport at that system and ships the unit there. Both are offered as the mod's carry - Space to
    /// pick a unit up off a card, Enter to put it down - so the population rows are sources and both
    /// kinds of target say so.
    ///
    /// Nothing closes a detail panel: the game itself has no gesture for it - clicking the same cell
    /// again leaves it open (measured) - and it goes when another cell is clicked or the page is left.
    ///
    /// The victory tab is a wheel with no controls in it at all: six sectors drawn clockwise from the
    /// top, each with the empire's rank in that race and three performance figures around its rim. It
    /// reads as one row per sector with the figures as its children, in the order the wheel draws them.
    /// The sentence the game puts on a sector - what the condition asks for and how far along the
    /// empire is - is plain text the game wrote, so it is announced whole like every other plain
    /// tooltip in the mod. This page states no exception: a tooltip's kind is the whole of the rule.
    ///
    /// Escape and F1 stay the game's: the page is the game's own and its own close paths work. It is one
    /// of the icon strip's screens, which the engine draws in an exclusive window stack - opening any
    /// other one hides this instantly - which is why they all share a layer.
    /// </summary>
    public sealed partial class EmpireScreen : Screen
    {
        private static readonly object TabsStop = "empire:tabs";
        private static readonly object SystemsStop = "empire:systems";
        private static readonly object PlanetsStop = "empire:detail/planets";
        private static readonly object ConstructiblesStop = "empire:detail/constructibles";
        private static readonly object QueueStop = "empire:detail/queue";
        private static readonly object HangarStop = "empire:detail/hangar";
        private static readonly object VictoryStop = "empire:victory";

        /// <summary>The prefix the shared readers key this page's ids under.</summary>
        private const string Keys = "empire:";

        /// <summary>The clusters the game draws over every page. They are drawn over this one too.
        /// </summary>
        private readonly GlobalHud _hud = new GlobalHud();

        private readonly TableSheet _table;

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<PlanetCard> _cards = new List<PlanetCard>();

        /// <summary>Which panel a table cell has slid out under the table, and the system it was opened
        /// for, as of the last frame this page was asked (see <see cref="WatchDetails"/>). Instance
        /// state, so it is reload-safe.</summary>
        private Detail _detail;
        private string _detailSystem;

        public EmpireScreen()
        {
            _table = new TableSheet(Keys, SystemOf);
            _table.RowName = SystemName;
            _table.ReadCell = ActionCell;
            // The systems table's own second click opens that system's management page.
            _table.DoubleClickHint = ModStrings.HintOpenSystemManagement;
        }

        public override string Key
        {
            get { return ModStrings.ScreenEmpire; }
        }

        /// <summary>Above the view levels it is drawn over, beside the senate and the technology wheel:
        /// the strip of icons in the corner opens all of them and the engine's window stack lets only
        /// one be up at a time.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.ScreenTitle("EmpireScreen");
                return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.ScreenEmpire) : title;
            }
        }

        /// <summary>Whichever tab is drawn, because that is what the player opened the page for. The
        /// panels down the left edge and the tab bar itself are a Shift+Tab away.</summary>
        public override object InitialFocusStop
        {
            get { return VictoryDrawn(Window()) ? VictoryStop : SystemsStop; }
        }

        /// <summary>A page the player closes and comes straight back to, with the cursor where they
        /// left it.</summary>
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

        /// <summary>Arrival gates on the page being WORKABLE, not just on no modal being up: the renderer
        /// switches the whole background stack off while a modal is showing and back on a frame or more
        /// AFTER the modal reports itself gone, so coming back on "no modal" alone lands the cursor on a
        /// page whose every control is still switched off and reads one passing "unavailable".</summary>
        public override bool IsActive()
        {
            try
            {
                global::EmpireScreen window = Window();
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
            // Start the watch from what is on the screen now, so coming back to a page that still has
            // a panel out never announces an opening nobody just made.
            _detail = DrawnDetail(out _detailSystem);
        }

        public override void OnPop()
        {
            _hud.Forget();
            _detail = Detail.None;
            _detailSystem = null;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            WatchDetails();
        }

        public override void Build(GraphBuilder builder)
        {
            global::EmpireScreen window = Window();
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
            if (VictoryDrawn(window))
            {
                BuildVictory(builder, window);
            }
            else
            {
                BuildSystems(builder, window);
            }

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

        // ---- the panels down the left edge ----

        /// <summary>A stop per panel the game is drawing down the left edge, top to bottom, read off
        /// what is in them - one line per thing the panel says, whatever the box wrapped onto the same
        /// row.</summary>
        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop("empire:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    _cells.Clear();
                    SidePanels.Readouts(
                        _cells,
                        panel,
                        "empire:side/" + i + "/",
                        SpecialCell,
                        Transparent
                    );
                    Cells.EmitLinear(builder, _cells);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading the side panels threw: " + e);
            }
        }

        /// <summary>What a side panel is called. The approval box and the relic box write a heading
        /// across their own top, and the shared reader takes that; the box holding the empire's
        /// portrait and description writes none at all, so it gets a word of the mod's.</summary>
        private static string PanelName(SidePanel panel)
        {
            if (panel is EmpireDescriptionSidePanel)
            {
                return ModStrings.Get(ModStrings.EmpireDescriptionPanel);
            }

            return SidePanels.Name(panel);
        }

        /// <summary>
        /// The two things in these panels the shape of the widget tree cannot name.
        ///
        /// The first is how many systems the empire holds against how many it can hold before the rest
        /// start to suffer. The panel draws it as a bare "1/7" over a bar, and the sentence saying what
        /// the two numbers ARE is on the group around them - which the walk would otherwise descend past,
        /// leaving a fraction with nothing to say for itself. The game writes one of three sentences there
        /// depending on the empire (<c>EmpireStatusSidePanel.Refresh</c> :87-98), so the words are
        /// whatever it wrote this frame.
        ///
        /// The second is an empire relic slot (<see cref="RelicSlot"/>).
        /// </summary>
        private static bool SpecialCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            // The type test first, so that every other panel's walk costs nothing: only the relic box
            // holds slots, and it only exists for one faction.
            if (panel is EmpireRelicsSidePanel && RelicSlot(cells, widget, keyPrefix))
            {
                return true;
            }

            EmpireStatusSidePanel status = panel as EmpireStatusSidePanel;
            if (status == null || !ReferenceEquals(widget, status.OvercolonizationGroup))
            {
                return false;
            }

            cells.Add(
                Cells.Readout(widget, AgeWidgets.Raw(widget), keyPrefix + "overcolonization")
            );
            return true;
        }

        /// <summary>
        /// One of the four empire relic slots the relic box draws (<c>RelicSlotItem</c>, Nakalim only -
        /// <c>EmpireRelicsSidePanel.CanBeShown</c> requires the Templars affinity).
        ///
        /// It is here rather than left to the shape walk because pressing it does one of TWO OPPOSITE
        /// things and the game says which only by swapping one picture for another. <c>Update</c> flips
        /// the slot into remove mode the moment the slot holds a relic and back into assign mode when it
        /// empties (<c>RelicSlotItem</c> :172-188), and the drawn sign is which of
        /// <c>AssignRelicsImage</c>/<c>RemoveRelicsImage</c> is visible - so that is what is READ here,
        /// rather than the private field behind it. A slot declared by shape alone would be a button whose
        /// name never changes while its meaning does.
        ///
        /// The slot has no name of its own either: it titles itself with an icon token
        /// (<c>FIDSIGroupTitle.Text = def.GuiSymbol</c>, "[explorer]" and friends), so the name is the
        /// words the game opens the slot's own explanation with - "Exploration Relic" - and the icon's
        /// name only where that sentence is missing.
        ///
        /// Everything the game refuses with is on the slot's OWN tooltip and is carried here in full,
        /// which is what keeps the locked-slot sentence (<c>%EmpireRelicsSlotLocked</c>) and the
        /// nothing-to-assign failure reachable: both are appended to that tooltip and to nothing else.
        ///
        /// Measured by binding the four slots to the game's own <c>EmpireRelicSlotDefinition</c> rows and
        /// forcing the box visible, then restoring: "Exploration Relic:, button, Assign relics,
        /// unavailable, Effects: ... This slot is locked. You must build a Cathedral to the Lost ...", and
        /// with the remove picture drawn instead, "Exploration Relic:, button, Remove relics, 2, ...".
        /// What is NOT measured is a real Nakalim empire: the definitions load for everyone (their datatable
        /// carries no DLC prerequisite) but the box itself only shows for that affinity, so the assign and
        /// remove PRESSES have never been made.
        /// </summary>
        private static bool RelicSlot(List<Cell> cells, AgeTransform widget, string keyPrefix)
        {
            RelicSlotItem slot = Slot(widget);
            if (slot == null)
            {
                return false;
            }

            AgeTransform button = AgeWidgets.Transform(slot.button);
            // Flow control, and the gate cannot cover it - the note in the branch says why.
            if (button == null || !AgeWidgets.Visible(button))
            {
                // Nothing to press and nothing to say: the walk is still stopped, because descending
                // into the slot would scatter it into an icon, a picture and a number.
                //
                // The central gate does NOT cover this and cannot: the cell below is declared on
                // `widget`, the slot, and that is the widget the gate asks about. What is tested here
                // is the BUTTON inside it, a different widget with a state of its own - the box draws
                // an empty slot with no button at all. This is the only place the slot's existence is
                // decided: the walk stops either way, so nothing further down declares it, and the
                // node this gate refuses would otherwise be a button whose press target is not drawn.
                return true;
            }

            RelicSlotItem it = slot;
            AgeTransform at = button;
            AgeTooltip tooltip = slot.assignRelicsTooltip ?? AgeWidgets.Raw(button);
            Func<bool> offered = () => AgeWidgets.Offered(at);
            NodeVtable vtable = GraphNodes.Button(
                () => RelicSlotName(it),
                () => AgeWidgets.Press(at),
                offered,
                tooltip
            );
            // BOTH tooltips the slot carries, through the nesting sink. The name comes off the FIDSI
            // group's sentence, so that sentence has to be reachable - it used to be read for the name
            // and then declared nowhere, and everything after its first line was unreachable. The
            // slot's OWN is the one the pointer goes to, so it is last and it is the one the slot
            // announces; the group's becomes a child entry of its own, because a second tooltip a node
            // is not pointing at is a second hover target and one row means a row of NODES.
            List<AgeTooltip> found = new List<AgeTooltip>(2);
            if (
                it.FIDSIGroupTooltip != null
                && !AgeWidgets.SameTooltip(it.FIDSIGroupTooltip, tooltip)
            )
            {
                found.Add(it.FIDSIGroupTooltip);
            }

            found.Add(tooltip);
            TooltipChildren.Carried carried = TooltipChildren.Split(found);
            vtable.Sections = GraphNodes.SectionsFor(vtable, carried.Own);
            vtable.Announcements.Add(GraphNodes.ValuePart(() => RelicSlotAction(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => RelicsAssigned(it)));
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            // Kept over the door's aim, which it re-states with the slot's BUTTON added so the slot
            // lights up under the cursor.
            AgeWidgets.Point(vtable, slot.button, tooltip, button);
            // The name and the position, because there are FOUR of these in one box and a key that named
            // only the panel is the same for all four - which throws Duplicate control id and empties the
            // WHOLE page. (Measured: that is how this was found. The clones happen to carry per-slot names
            // in this prefab, and the index is what makes the key safe if a later one does not.)
            string key =
                keyPrefix + "relic-slot/" + widget.name + "/" + AgeWidgets.IndexInParent(widget);
            Cell cell = Cells.Add(cells, widget, ControlId.For(slot, key), vtable);
            cell.Dossiers = carried.Children;
            cell.Key = carried.Children == null ? null : key;
            return true;
        }

        /// <summary>Which relic slot this is, in the game's own words: the sentence it explains itself
        /// with opens with the slot's name ("Exploration Relic:"), and the drawn title is an icon.
        /// </summary>
        private static string RelicSlotName(RelicSlotItem slot)
        {
            try
            {
                string described = CardActions.FirstLine(slot.FIDSIGroupTooltip);
                if (!string.IsNullOrEmpty(described))
                {
                    return described;
                }

                AgePrimitiveLabel title = slot.FIDSIGroupTitle;
                return title == null ? null : AgeWidgets.TextOf(title.AgeTransform);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What pressing this slot would do, read off the picture the game is drawing on it.
        /// </summary>
        private static string RelicSlotAction(RelicSlotItem slot)
        {
            try
            {
                return ModStrings.Get(
                    // Content: which of two words the action is called by.
                    AgeWidgets.Visible(slot.RemoveRelicsImage)
                        ? ModStrings.EmpireRelicSlotRemove
                        : ModStrings.EmpireRelicSlotAssign
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How many relics are in the slot, while the game is drawing that group at all - it
        /// hides it for an empty slot rather than writing a zero.</summary>
        private static string RelicsAssigned(RelicSlotItem slot)
        {
            try
            {
                return AgeWidgets.DrawnLabel(slot.AssignRelicsGroup, slot.AssignedRelicsLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static RelicSlotItem Slot(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<RelicSlotItem>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether a group the game made clickable is really a band of readouts.
        ///
        /// The approval box is: the prefab wires its click to <c>OnHappinessGroupCb</c> and no such
        /// method exists anywhere, so the message goes nowhere and the mouse gets silence - and a click
        /// the game answers with silence stays silent here. Treated as the band it is, its rows are read
        /// separately and the rich approval tooltip inside it (which hangs on the group holding the
        /// figure, not on the box) is found where the game put it.
        /// </summary>
        private static bool Transparent(AgeTransform widget, SidePanel panel)
        {
            EmpireStatusSidePanel status = panel as EmpireStatusSidePanel;
            return status != null && ReferenceEquals(widget, status.HappinessAndRebellionGroup);
        }

        // ---- the tab bar ----

        /// <summary>The two tabs, in the order they are drawn. Enter is the toggle's own click, which
        /// swaps the panel underneath in the same frame - so nothing is spoken for the press beyond the
        /// tab saying it is now the selected one; the page rebuilds around it.</summary>
        private void BuildTabs(GraphBuilder builder, global::EmpireScreen window)
        {
            builder.BeginStop(TabsStop);
            _cells.Clear();
            AddTab(_cells, window.ToggleSystems, 0);
            AddTab(_cells, window.ToggleVictory, 1);
            Cells.EmitLinear(builder, _cells);
        }

        private static void AddTab(List<Cell> cells, AgeControlToggle toggle, int index)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Tab(
                () => AgeWidgets.TextOf(widget),
                () => it.State,
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            vtable.OnActivate = () => AgeWidgets.Toggle(it);
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.For(widget, "empire:tab/" + index), vtable);
        }

        // ---- reading the window ----

        /// <summary>Whether the wheel is the tab being drawn. Asked of what is on screen rather than of
        /// the window's remembered tab, which other screens set on their way here.</summary>
        private static bool VictoryDrawn(global::EmpireScreen window)
        {
            try
            {
                return window != null
                    && window.VictoryAndPerformancePanel != null
                    // Flow control: whether the Victory tab's whole subtree is walked at all.
                    && AgeWidgets.Visible(window.VictoryAndPerformancePanel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The heading a panel writes across its own top. Neither panel exposes the label, so
        /// it is found where it is drawn - the shared search (<see cref="WindowShape.Title"/>), with
        /// the name these prefabs use for it.</summary>
        private static string PanelTitle(GuiPanel panel)
        {
            try
            {
                return panel == null
                    ? null
                    : WindowShape.Title(panel.AgeTransform, PanelTitleNames);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly string[] PanelTitleNames = { "Title" };

        /// <summary>One line per thing a card's table is drawing, the way both pages that draw a planet
        /// card read one (<see cref="PlanetCardLines.Add"/>). This page read the whole subtree's text
        /// instead until stage 6a, which announced a pooled table's retired items and read a deposit as
        /// a bare number.</summary>
        private static void AddWidgetLines(List<string> lines, AgeTransform widget)
        {
            // Content: which drawn lines are gathered into a reading.
            PlanetCardLines.Add(lines, widget);
        }

        /// <summary>The system a row stands for. The wrapper the table binds is rebuilt on every
        /// refresh, so it is the system underneath it that identifies the row.</summary>
        private static readonly TableSheet.RowObject SystemOf =
            TableSheet.Model<GuiColonizedStarSystem>(wrapper => wrapper.ColonizedStarSystem);

        /// <summary>What the row is called when the name column draws nothing - the system's own name.
        /// </summary>
        private static readonly TableSheet.RowLabel SystemName =
            TableSheet.Name<GuiColonizedStarSystem>(wrapper => wrapper.LocalizedName);

        private static global::EmpireScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::EmpireScreen>(false)
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
