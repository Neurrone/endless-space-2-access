using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The senate: the page F2 opens over whatever the player was looking at, where the empire's
    /// politics are drawn as a column of side panels down the left edge and four boxes in a two-by-two
    /// grid in the middle.
    ///
    /// The grid is walked the way it is read - across the top row, then across the bottom - rather than
    /// in the order the window happens to hold its panels in (which is column by column). The four
    /// boxes each carry a drawn heading with a sentence on it explaining what the box is for, so the
    /// heading is BOTH the stop's name and the stop's first node: the words are the game's, the
    /// explanation would otherwise be unreachable, and the announcer drops a container name that merely
    /// repeats the control inside it.
    ///
    /// What can be DONE here is deliberately narrow, because that is the game's own model:
    ///
    /// - The party rows in the senate breakdown look clickable and are not. The game sends
    ///   <c>OnClickPolitics</c> with <c>DontRequireReceiver</c> and nothing anywhere handles it
    ///   (<c>PoliticalAssemblyPanel.OnClickSupportLine</c> :380-383), so a click here is answered with
    ///   silence - and a click the game answers with silence stays silent. They are readouts.
    /// - The senator cards are hover areas: the mouse gets a highlight and three tooltips out of them
    ///   and nothing else. Read-only, with what the game wrote on them and in them.
    /// - The law slots are the game's select-then-act. A slot holding a law is a toggle that only
    ///   SELECTS it, and Abolish acts on the selection; an empty slot's toggle opens the laws window
    ///   instead; a locked slot refuses with the game's own sentence about why. Enter is that toggle in
    ///   every case, which is exactly what a mouse click does.
    /// - The census is a ring of arcs the game labels; the one button under it opens the population
    ///   window. The game draws either "Population details" or the gene hunter's own button, never
    ///   both (<c>PopulationCensusPanel.Bind</c> :62-63), so whichever is drawn is what is declared.
    ///
    /// The side panels - the government, the next election and the support history - are read by the
    /// shared side-panel reader (<see cref="SidePanels"/>), which is also what gives the government
    /// panel's Change Government button and the election panel's Survey button their refusals in the
    /// game's own words.
    ///
    /// Escape and F2 stay the game's: the screen is the game's own and its own close paths work. The
    /// page is one of the icon strip's, which the engine draws in an exclusive window stack - opening
    /// any other one hides this instantly (measured), which is why they all share a layer.
    /// </summary>
    public sealed class SenateScreen : Screen
    {
        private static readonly object AssemblyStop = "senate:assembly";
        private static readonly object SenatorsStop = "senate:senators";
        private static readonly object LawsStop = "senate:laws";
        private static readonly object CensusStop = "senate:census";

        /// <summary>Shared by the law slots and by the senator cards, so up and down out of a strip of
        /// cards keeps the column it was in.</summary>
        private static readonly object SlotRowKey = "senate:slot-row";

        /// <summary>The clusters the game draws over every page. They are drawn over this one too.
        /// </summary>
        private readonly GlobalHud _hud = new GlobalHud();

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<GuiPanel> _boards = new List<GuiPanel>();

        public override string Key
        {
            get { return "screen.senate"; }
        }

        /// <summary>Above the view levels it is drawn over, beside the technology wheel and the quest
        /// journal: the strip of icons in the corner opens all of them and the engine's window stack
        /// lets only one be up at a time.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = ScreenTitle();
                return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.ScreenSenate) : title;
            }
        }

        /// <summary>The senate itself, which is what the player opened the page for. The panels down
        /// the left edge are a Shift+Tab away.</summary>
        public override object InitialFocusStop
        {
            get { return AssemblyStop; }
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
                global::SenateScreen window = Window();
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
            _hud.Forget();
        }

        public override void OnUpdate()
        {
            _hud.Update();
        }

        public override void Build(GraphBuilder builder)
        {
            global::SenateScreen window = Window();
            if (window == null)
            {
                return;
            }

            // Down and across the screen: the empire's banners along the top with this page's own title
            // beside them, the panels down the left edge, the four boxes in the middle in reading
            // order, then the right-hand edge and the turn controls.
            _hud.Top(builder);
            BuildSidePanels(builder);
            BuildBoards(builder, window);
            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.Turn(builder);
        }

        // ---- the panels down the left edge ----

        /// <summary>A stop per panel the game is drawing down the left edge, top to bottom, read off
        /// what is in them.</summary>
        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop("senate:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    _cells.Clear();
                    SidePanels.Readouts(
                        _cells,
                        panel,
                        "senate:side/" + i + "/",
                        ElectionActionCell,
                        null
                    );
                    Cells.Emit(builder, _cells);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("senate: reading the side panels threw: " + e);
            }
        }

        /// <summary>
        /// One of the actions the next election's panel lists - what this government lets the empire do
        /// to bend the result, with what it would cost.
        ///
        /// The shape of the tree calls each one a line of drawn text, and it is a SWITCH: the game builds
        /// the row from its <c>ElectionActionToggle</c> prefab, whose tick is what a mouse clicks and
        /// whose handler passes the row on to whoever is hosting it
        /// (<c>ElectionActionToggle.OnToggleCb</c> :38-45 sends <c>OnToggleAction</c> to the panel). So it
        /// is declared as the box it is, and Enter is that same tick.
        ///
        /// Here it never ticks: this panel switches every action off as it binds it
        /// (<c>NextElectionSidePanel.SetupElectionAction</c> :131-141 sets <c>State = false</c> and
        /// <c>Enable = false</c>), because the choosing happens in the election window when an election is
        /// actually running - the same rows, enabled, read by <see cref="ElectionScreen"/>. Reading
        /// unavailable is therefore the truth about this copy of them, and the game writes no reason for
        /// it anywhere: the row's tooltip is the action's own dossier, which the tooltip window assembles,
        /// so it is indicated and the review buffer carries it.
        ///
        /// The name comes from the action's wrapper and not from the drawn label, which the game squeezes
        /// into 164 pixels and truncates ("Reinforced intimidation o." for "Reinforced intimidation on
        /// Citizens"). The table is pooled, so a retired row contributes nothing rather than the last
        /// action's words.
        /// </summary>
        private static bool ElectionActionCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            ElectionActionToggle action = Component(widget);
            if (action == null)
            {
                return false;
            }

            if (action.ElectionActionDefinition == null || !AgeWidgets.Painted(widget))
            {
                return true;
            }

            ElectionActionToggle it = action;
            AgeTransform at = widget;
            AgeTooltip tooltip = action.Tooltip ?? AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(at);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => ActionName(it),
                () => it.Toggle != null && it.Toggle.State,
                () => AgeWidgets.Toggle(it.Toggle),
                enabled,
                tooltip,
                null,
                null,
                () => Cost(it)
            );
            // No refusal part: the game writes none for these rows, and the tooltip's content field holds
            // the wrapper's NAME rather than words (measured: reading it aloud produced
            // "ElectionActionEmpire01"). The renderer builds the dossier itself, and the review buffer
            // carries what it draws.
            AgeWidgets.Point(vtable, it.Toggle, tooltip, widget);
            // Keyed on the ACTION the row was bound to: the rows share a pooled prefab and every one of
            // them arrives at the same depth under the same table, so the shape-driven key the shared
            // walk would build is the same string for all of them.
            Cells.Add(
                cells,
                widget,
                ControlId.Referenced(
                    widget,
                    keyPrefix + "election-action/" + action.ElectionActionDefinition.Name
                ),
                vtable
            );
            return true;
        }

        private static ElectionActionToggle Component(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<ElectionActionToggle>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the game calls an election action, asked of the wrapper the row was bound from
        /// rather than read off the truncated label the row draws.</summary>
        private static string ActionName(ElectionActionToggle action)
        {
            try
            {
                GuiElectionAction wrapper = Gui.GuiWrapperProviderService.GetGuiElectionAction(
                    action.ElectionActionDefinition.Name
                );
                string title = wrapper == null ? null : AgeText.Clean(wrapper.Title);
                return string.IsNullOrEmpty(title) ? AgeText.Label(action.TitleLabel) : title;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the action would cost, where the row draws it: the game hides the price on an
        /// action that is a readout rather than a choice (<c>ElectionActionToggle.Bind</c> :22-27).
        /// </summary>
        private static string Cost(ElectionActionToggle action)
        {
            try
            {
                AgeTransform cost = Widget(action.CostLabel);
                return cost == null || !AgeWidgets.Visible(cost)
                    ? null
                    : AgeText.Label(action.CostLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a side panel is called. These three are unlabelled boxes - the game marks two
        /// of them with an icon explaining itself on hover and the third with nothing at all - and a
        /// stop is announced by its name on every Tab into it, so each gets a word rather than the
        /// sentence the shared reader would otherwise fall through to.</summary>
        private static string PanelName(SidePanel panel)
        {
            if (panel is GovernmentTypeSidePanel)
            {
                return ModStrings.Get(ModStrings.SenateGovernmentPanel);
            }

            if (panel is NextElectionSidePanel)
            {
                return ModStrings.Get(ModStrings.SenateElectionPanel);
            }

            if (panel is SupportHistorySidePanel)
            {
                return ModStrings.Get(ModStrings.SenateSupportPanel);
            }

            return SidePanels.Name(panel);
        }

        // ---- the four boxes in the middle ----

        /// <summary>The grid, in the order it is read: across the top row, then across the bottom. The
        /// window holds the four in a different order from the one it draws them in, so the order comes
        /// from where they are.</summary>
        private void BuildBoards(GraphBuilder builder, global::SenateScreen window)
        {
            _boards.Clear();
            Board(_boards, window.PoliticalAssemblyPanel);
            Board(_boards, window.SenatorsPanel);
            Board(_boards, window.ActiveLawsPanel);
            Board(_boards, window.PopulationCensusPanel);
            _boards.Sort(InReadingOrder);

            for (int i = 0; i < _boards.Count; i++)
            {
                GuiPanel board = _boards[i];
                PoliticalAssemblyPanel assembly = board as PoliticalAssemblyPanel;
                if (assembly != null)
                {
                    BuildAssembly(builder, assembly);
                    continue;
                }

                SenatorsPanel senators = board as SenatorsPanel;
                if (senators != null)
                {
                    BuildSenators(builder, senators);
                    continue;
                }

                ActiveLawsPanel laws = board as ActiveLawsPanel;
                if (laws != null)
                {
                    BuildLaws(builder, laws);
                    continue;
                }

                BuildCensus(builder, board as PopulationCensusPanel);
            }
        }

        private static void Board(List<GuiPanel> boards, GuiPanel panel)
        {
            if (panel != null && AgeWidgets.Visible(panel.AgeTransform))
            {
                boards.Add(panel);
            }
        }

        private static readonly Comparison<GuiPanel> InReadingOrder = (left, right) =>
        {
            UnityEngine.Rect a = left.AgeTransform.GetGlobalPosition();
            UnityEngine.Rect b = right.AgeTransform.GetGlobalPosition();
            int rows = a.y.CompareTo(b.y);
            return rows != 0 ? rows : a.x.CompareTo(b.x);
        };

        /// <summary>
        /// Who sits in the senate: one row per party with how many seats it holds, and the total
        /// underneath.
        ///
        /// The rows are readouts. The panel wires a click on one to a message nothing listens for, and
        /// the pie chart beside them says the same thing again in a picture - so the rows are the
        /// whole of it, and the chart is not declared.
        /// </summary>
        private void BuildAssembly(GraphBuilder builder, PoliticalAssemblyPanel panel)
        {
            builder.BeginStop(AssemblyStop);
            bool named = AddHeading(builder, panel, "senate:assembly/heading");

            AgeTransform table = panel.PoliticalSupportLinesTable;
            IList<AgeTransform> rows = table == null ? null : table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                AddSupportLine(builder, rows[i], i);
            }

            _cells.Clear();
            Cells.AddReadout(_cells, TotalGroup(panel), "senate:assembly/total");
            Emit(builder, _cells, null);
            Unname(builder, named);
        }

        /// <summary>One party's seats. The row draws the party's name and its number of representatives
        /// and hangs the party's own wrapper on its tooltip, which is where everything else about it
        /// is.</summary>
        private static void AddSupportLine(GraphBuilder builder, AgeTransform widget, int index)
        {
            PoliticalSupportLine line =
                widget == null ? null : widget.GetComponent<PoliticalSupportLine>();
            if (line == null || !AgeWidgets.Visible(widget) || line.GuiPolitics == null)
            {
                return;
            }

            PoliticalSupportLine it = line;
            AgeTooltip tooltip = line.Tooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.TitleLabel)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.SupportLabel)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.Point(vtable, line.HoverArea, tooltip, widget);
            builder.AddItem(
                ControlId.Referenced(widget, "senate:assembly/party/" + index),
                vtable
            );
        }

        /// <summary>The count the panel draws under the party rows, taken with the caption beside it -
        /// the group holds both, and the tooltip explaining what was counted is on the group.</summary>
        private static AgeTransform TotalGroup(PoliticalAssemblyPanel panel)
        {
            try
            {
                return panel.TotalRepresentativesCount == null
                    ? null
                    : panel.TotalRepresentativesCount.AgeTransform.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The three senator slots. A slot holds a party once one of its senators is seated, and the
        /// card then draws the party, how much political experience it has and - where a hero of the
        /// player's matches it - that hero's level and their senate skills.
        ///
        /// Read-only: the card's only control is a hover area, whose whole job is highlighting the
        /// party elsewhere on the page. What it says is what the game drew on it, and its three
        /// tooltips are in the review buffer.
        /// </summary>
        private void BuildSenators(GraphBuilder builder, SenatorsPanel panel)
        {
            builder.BeginStop(SenatorsStop);
            bool named = AddHeading(builder, panel, "senate:senators/heading");

            AgeTransform table = panel.SenatorCardsTable;
            IList<AgeTransform> cards = table == null ? null : table.Children;
            _cells.Clear();
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                AddSenatorCard(_cells, cards[i], i);
            }

            Emit(builder, _cells, SlotRowKey);
            Unname(builder, named);
        }

        private static void AddSenatorCard(List<Cell> cells, AgeTransform widget, int index)
        {
            SenatorCard card = widget == null ? null : widget.GetComponent<SenatorCard>();
            if (card == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            SenatorCard it = card;
            AgeTransform at = widget;
            AgeTransform skills = AgeWidgets.ChildNamed(widget, "SenatorSkillsGroup", 3);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => SenatorName(it)),
                    GraphNodes.ValuePart(() => HeroLevel(it)),
                    GraphNodes.ValuePart(() => Experience(it)),
                    GraphNodes.ValuePart(() => AgeWidgets.TextOf(skills)),
                    GraphNodes.DisabledPart(() => AgeWidgets.Operable(at)),
                },
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(it.PortraitTooltip),
                    GraphNodes.TooltipSection(it.PoliticsTooltip),
                    GraphNodes.TooltipSection(it.ExperienceTooltip),
                    new NodeSection(() => AgeWidgets.DrawnLines(at), TooltipMode.None)
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, "senate:senator/" + index), vtable);
        }

        /// <summary>Which party holds the slot. A slot with no party in it draws a bare picture and no
        /// words at all, so those two states are named by the mod - the game's own sentence about them
        /// is on the tooltip, where the game put it.</summary>
        private static string SenatorName(SenatorCard card)
        {
            try
            {
                if (card.GuiPolitics != null)
                {
                    return AgeText.Label(card.PoliticsNameLabel);
                }

                return ModStrings.Get(
                    card.LockedSlot
                        ? ModStrings.SenateLockedSenatorSlot
                        : ModStrings.SenateEmptySenatorSlot
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How much political experience the party in this slot has. Gated on the slot holding
        /// a party at all: the game leaves the last one's word written on a card it has emptied and only
        /// stops drawing it, and the label's own flag still reads visible because it is the group
        /// ABOVE it that was hidden.</summary>
        private static string Experience(SenatorCard card)
        {
            try
            {
                return card.GuiPolitics == null
                    ? null
                    : AgeText.Label(card.PoliticsExperienceLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The level of the hero leading this party, which the card draws as a bare number in
        /// a ring. The game has a caption for it on its own hero cards, so that is the one used.
        /// </summary>
        private static string HeroLevel(SenatorCard card)
        {
            try
            {
                if (
                    card.HeroExperienceGroup == null
                    || !AgeWidgets.Visible(card.HeroExperienceGroup)
                )
                {
                    return null;
                }

                string level = AgeText.Label(card.LevelLabel);
                if (string.IsNullOrEmpty(level))
                {
                    return null;
                }

                string caption = AgeText.Clean(Gui.Localize(HeroLevelKey));
                return string.IsNullOrEmpty(caption) || caption[0] == '%'
                    ? level
                    : caption + " " + level;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string HeroLevelKey = "%HeroCardLevelTitle";

        /// <summary>
        /// The laws in force and the six slots they sit in, with the upkeep line and the two buttons
        /// the panel draws under them.
        ///
        /// Abolish is declared even while it refuses: hearing that it is there, and the game's own
        /// reason it will not go, is the point. It acts on whichever slot is SELECTED, which is why
        /// selecting is all Enter on a slot does.
        /// </summary>
        private void BuildLaws(GraphBuilder builder, ActiveLawsPanel panel)
        {
            builder.BeginStop(LawsStop);
            bool named = AddHeading(builder, panel, "senate:laws/heading");

            _cells.Clear();
            LawCards.Cards(_cells, panel.LawCardsTable, "senate:law-slot/");
            Emit(builder, _cells, SlotRowKey);

            _cells.Clear();
            Cells.AddControl(_cells, AgeWidgets.Transform(panel.VoteLawsButton), "senate:laws/vote");
            Cells.AddControl(_cells, AgeWidgets.Transform(panel.AbrogateButton), "senate:laws/abolish");
            Cells.AddReadout(
                _cells,
                panel.LawUpkeepLabel == null ? null : panel.LawUpkeepLabel.AgeTransform,
                "senate:laws/upkeep"
            );
            Emit(builder, _cells, null);
            Unname(builder, named);
        }

        /// <summary>
        /// Who lives in the empire: the total, then one row per slice of the ring the panel draws, then
        /// the one button under it.
        ///
        /// The panel MERGES the smallest populations into a group of their own once there are more than
        /// eight down either side, so the rows are whatever slices are drawn rather than one per
        /// population - and a merged slice says what it holds on its own tooltip, which the game wrote.
        /// </summary>
        private void BuildCensus(GraphBuilder builder, PopulationCensusPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            builder.BeginStop(CensusStop);
            bool named = AddHeading(builder, panel, "senate:census/heading");

            _cells.Clear();
            AddCensusTotal(_cells, panel);
            Emit(builder, _cells, null);

            _cells.Clear();
            AgeTransform container = panel.PopulationCensusArcsContainer;
            IList<AgeTransform> arcs = container == null ? null : container.Children;
            for (int i = 0; arcs != null && i < arcs.Count; i++)
            {
                AddCensusArc(_cells, arcs[i], i);
            }

            Emit(builder, _cells, null);

            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.Transform(panel.PopulationDetailsButton),
                "senate:census/details"
            );
            Cells.AddControl(
                _cells,
                AgeWidgets.Transform(panel.GeneManagementButton),
                "senate:census/genes"
            );
            Emit(builder, _cells, null);
            Unname(builder, named);
        }

        /// <summary>How many people there are in the empire, taken as the caption the panel writes over
        /// the number and the number itself. The two are separate labels drawn in the middle of the
        /// ring, and the group AROUND them holds the whole ring as well - reading that group instead
        /// glued the total to every slice's label.</summary>
        private static void AddCensusTotal(List<Cell> cells, PopulationCensusPanel panel)
        {
            AgeTransform count =
                panel.TotalCountLabel == null ? null : panel.TotalCountLabel.AgeTransform;
            AgeTransform caption = AgeWidgets.ChildNamed(panel.AgeTransform, "TotalCountTitle", 3);
            if (count == null || !AgeWidgets.Visible(count))
            {
                return;
            }

            AgeTransform title = caption;
            AgeTransform value = count;
            AgeTooltip tooltip = AgeWidgets.Raw(caption) ?? AgeWidgets.Raw(count);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(title)),
                    GraphNodes.ValuePart(() => AgeWidgets.TextOf(value)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, TooltipOwner(tooltip) ?? count);
            Cells.Add(cells, count, ControlId.Referenced(count, "senate:census/total"), vtable);
        }

        /// <summary>One slice of the ring: who they are, how many of them there are, and - where the
        /// game is offering one - what boosting them would cost or how long the boost it is already
        /// running has left.</summary>
        private static void AddCensusArc(List<Cell> cells, AgeTransform widget, int index)
        {
            PopulationCensusArc arc =
                widget == null ? null : widget.GetComponent<PopulationCensusArc>();
            if (arc == null || arc.GuiPopulation == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform labels = arc.LabelsContainer;
            if (labels == null || !AgeWidgets.Visible(labels))
            {
                return;
            }

            PopulationCensusArc it = arc;
            AgeTooltip tooltip = arc.Tooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.AffinityLabel)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.CountLabel)),
                    GraphNodes.ValuePart(() => BoostText(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, TooltipOwner(tooltip) ?? labels);
            // Keyed on the LABELS rather than on the arc: the container is what is laid out down the
            // side of the ring, and the rows are worked out from where things are drawn.
            Cells.Add(
                cells,
                labels,
                ControlId.Referenced(widget, "senate:census/arc/" + index),
                vtable
            );
        }

        private static string BoostText(PopulationCensusArc arc)
        {
            try
            {
                return arc.PopulationBoostLabel == null
                    || !AgeWidgets.Visible(arc.PopulationBoostLabel.AgeTransform)
                    ? null
                    : AgeText.Label(arc.PopulationBoostLabel);
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
        /// the box says what the box is. Declaring it as well is not a repetition: the heading carries
        /// a sentence explaining the box that exists nowhere else, and a container name that merely
        /// repeats the control inside it is dropped by the announcer.
        /// </summary>
        private bool AddHeading(GraphBuilder builder, GuiPanel panel, string key)
        {
            AgeTransform heading = AgeWidgets.ChildNamed(panel.AgeTransform, "Title", 2);
            string text = heading == null ? null : AgeWidgets.TextOf(heading);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            builder.PushContext(text);
            Cell cell = Cells.Readout(heading, AgeWidgets.Raw(heading), key);
            builder.AddItem(cell.Id, cell.Vtable);
            return true;
        }

        /// <summary>Close the box's name off again, so the next box is not declared inside it.</summary>
        private static void Unname(GraphBuilder builder, bool named)
        {
            if (named)
            {
                builder.PopContext();
            }
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

        private static AgeTransform TooltipOwner(AgeTooltip tooltip)
        {
            try
            {
                return tooltip == null ? null : tooltip.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

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

        private static string ScreenTitle()
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle("SenateScreen"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static global::SenateScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::SenateScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
