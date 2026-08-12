using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The senate election - the three-step wizard the game raises over whatever the player was
    /// looking at when the turn resolved, and will not let go of until it is finished or skipped.
    ///
    /// It is nobody's subscreen. There is no button that opens it: the game shows and hides the window
    /// from one predicate of its own,
    /// <c>SetGameWindowVisibility&lt;ElectionModalWindow&gt;(IsInElection)</c>
    /// (<c>GuiManager.cs:1554</c>), where <c>IsInElection</c> is the empire's own election being in
    /// interactive mode and mid-flight (<c>GuiManager.cs:482-497</c>). So this is a polled screen with
    /// its own predicate, like the loading screen - not a child pushed by an opener.
    ///
    /// The wizard is the GAME's: <c>ElectionModalWindow.Step</c> plus one
    /// <see cref="global::ElectionPanel"/> shown at a time (<c>ElectionModalWindow.cs:106-125</c>),
    /// step 0 the party-support panel, step 1 the vote breakdown, step 2 the result
    /// (<c>Election.cs:271</c>, <c>ElectionProcessTask_UI_Results_Local.cs:36</c>,
    /// <c>ElectionModalWindow.cs:299</c>). Nothing here models the steps as a mod-owned sequence -
    /// which panel is drawn is the question asked every rebuild, and the step number is only used to
    /// notice that it changed.
    ///
    /// Three things about it are worth knowing before reading the code.
    ///
    /// **Escape is the game's, and it asks first.** The window handles Exit itself and raises
    /// <c>%SkipInteractiveElectionTitle</c> - "Are you sure you want to skip the election?" - through
    /// the shared message box (<c>ElectionModalWindow.cs:127-142</c>), which the mod's
    /// <see cref="MessageBoxScreen"/> already speaks. So <see cref="ConsumesBack"/> stays false and
    /// there is nothing to write here for it. On the LAST step the same key presses Finish instead
    /// (:137), which is also the game's own behaviour and equally free.
    ///
    /// **The game clears its own pointer state on every step change** - <c>OverrolledTransform</c> and
    /// <c>FocusedControl</c> both nulled, and the Next Step button's interaction state reset
    /// (<c>ElectionModalWindow.cs:71-77</c>). The tooltip target heals itself, because
    /// <c>PointerFocus.LateTick</c> re-asserts it every frame; the button's HIGHLIGHT does not, because
    /// focus visuals are applied once per focus change and the cursor has not moved. So a step change
    /// re-arms them (see <see cref="OnUpdate"/>) rather than leaving the screen looking unhovered for
    /// as long as the player stands still.
    ///
    /// **The mod's own cursor is not affected by any of that**: it lives in the navigator's state, not
    /// in the engine's focus field. What a step change does do is replace every node of the panel
    /// stops at once, which the graph answers with its ordinary nearest-survivor reconciliation.
    ///
    /// The window's panels are declared one drawn GROUP per Tab stop, because that is how the game
    /// switches them on and off: the election actions appear only once a party is picked
    /// (<c>ElectionBeforePanel.cs:140,234</c>), the law list only while its own Show button has been
    /// pressed (:341-346), the unlocked-law list or a "no new laws" label but never both
    /// (<c>ElectionFinalPanel.cs:143-156</c>). Everything is declared from live visibility, so a group
    /// the fixture never draws simply never appears rather than reading as an empty dead end.
    ///
    /// This screen ships CODE-VERIFIED ONLY: an election needs a real election turn, which no save in
    /// the project's fixtures reaches. The open measurements are in the session's test report.
    /// </summary>
    public sealed class ElectionScreen : Screen
    {
        // The window's own chrome, then the shown panel's groups, then the two buttons along the
        // bottom. Only one panel's stops exist at a time - the others are not drawn.
        private static readonly object HeaderStop = "election:header";
        private static readonly object ActionsStop = "election:actions";

        private static readonly object BeforeCandidatesStop = "election:before/candidates";
        private static readonly object BeforeActionsStop = "election:before/actions";
        private static readonly object BeforeLawsStop = "election:before/laws";
        private static readonly object BeforeResourcesStop = "election:before/resources";

        private static readonly object LocalSupportStop = "election:local/support";
        private static readonly object LocalSystemStop = "election:local/system";

        private static readonly object FinalWinnersStop = "election:final/winners";
        private static readonly object FinalLawsStop = "election:final/laws";
        private static readonly object FinalOutcomesStop = "election:final/outcomes";

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>Which step the player has been told about. Instance state, so a hot reload starts
        /// it over rather than inheriting a stale answer.</summary>
        private readonly StepWatch _steps = new StepWatch();

        /// <summary>Set once a panel has finished arriving, and cleared when the game unbinds the
        /// window.</summary>
        private bool _arrived;

        public override string Key
        {
            get { return "screen.election"; }
        }

        /// <summary>
        /// Above every page the game can leave drawn underneath it. Nothing opens this window, so its
        /// number is not "one above its opener" but "one above everything that can still be up when a
        /// turn resolves": the view levels and the scan view, the icon-strip screens, the panels and
        /// modals those can have open. It stays under the notification screen, which is drawn over
        /// everything, and well under the message box - which is this window's OWN skip confirmation
        /// and must take focus from it.
        /// </summary>
        public override int Layer
        {
            get { return 36; }
        }

        /// <summary>The question the shown step asks, in the game's own words - "Select a political
        /// party to support" (<c>ElectionPanel.cs:12</c>, and <c>ElectionFinalPanel.cs:71-75</c> for
        /// the result panel's two variants). It is BOTH the screen's spoken name and a node at the top
        /// of the panel, so it can be gone back to; focus starts on the panel's content, which is what
        /// keeps it from being said twice. A step CHANGE says the new one the same way - see
        /// <see cref="OnUpdate"/>.</summary>
        public override string ScreenName
        {
            get { return PanelTitle(CurrentPanel()); }
        }

        /// <summary>
        /// Arriving and leaving are different questions.
        ///
        /// We arrive when a panel has finished animating in: until then <c>GuiPanel.OnBeginHide</c>
        /// and <c>OnEndShow</c> leave the panel's transform DISABLED
        /// (<c>Amplitude.Unity.Gui/GuiPanel.cs:250,255</c>), so every control inside it would read
        /// "unavailable" exactly once, invisibly.
        ///
        /// We leave when the game UNBINDS the window - it drops the domestic-affairs agency it was
        /// opened for as the last thing it does (<c>ElectionModalWindow.cs:265</c>, in
        /// <c>OnEndHide</c>), and not before. Gating the departure on visibility instead would hand
        /// the player back to a page that is still disabled underneath.
        ///
        /// Between the two, the arrival latch holds through every step change: the panels swap over
        /// several frames and a screen that blinked out mid-swap would announce the galaxy underneath.
        /// </summary>
        public override bool IsActive()
        {
            ElectionModalWindow window = Window();
            try
            {
                if (window == null || window.DepartmentOfDomesticAffairs == null)
                {
                    _arrived = false;
                    return false;
                }

                if (!_arrived)
                {
                    _arrived = window.Shown && window.IsReady && CurrentPanel() != null;
                }

                return _arrived;
            }
            catch (Exception)
            {
                _arrived = false;
                return false;
            }
        }

        /// <summary>Escape is the game's: the window answers Exit by raising its own skip
        /// confirmation, which the message-box screen speaks.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>Arrival announces the step it arrived on (<see cref="ScreenName"/> is that step's
        /// title), so the watcher starts already knowing it.</summary>
        public override void OnPush()
        {
            _steps.Baseline(Step());
        }

        public override void OnPop()
        {
            _steps.Forget();
        }

        /// <summary>
        /// A step the game turned by itself - the result panel arrives on the election completing
        /// (<c>ElectionModalWindow.cs:297-300</c>), not on a keypress - said passively, queued, in the
        /// game's own words for that step.
        ///
        /// Nothing is committed until it is actually spoken: the step number changes BEFORE the new
        /// panel is shown (<c>ElectionModalWindow.cs:112-117</c>), so the first frame or two have a
        /// step with no drawn title, and consuming the change there would lose the announcement.
        ///
        /// The same moment re-arms the focus visuals the game just cleared for itself
        /// (<c>ElectionModalWindow.cs:71-77</c>): dropping the navigator's record of what is showing
        /// makes the next frame re-apply the hover and the tooltip for the control the cursor is
        /// already on.
        /// </summary>
        public override void OnUpdate()
        {
            try
            {
                int step = Step();
                if (!_steps.IsNew(step))
                {
                    return;
                }

                string title = PanelTitle(CurrentPanel());
                if (string.IsNullOrEmpty(title))
                {
                    return;
                }

                _steps.Told(step);
                Voice.Say(title, false);

                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.ClearVisual();
                }
            }
            catch (Exception e)
            {
                Log.Warn("election: watching the step threw: " + e);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            ElectionModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            BuildHeader(builder, window);

            // Whichever panel is drawn, and only that one - the others are not on screen, so their
            // stops do not exist this frame. A step mid-swap draws none of them, which declares the
            // window's chrome and its buttons and nothing else; that is legal and is what keeps the
            // cursor alive across the change.
            ElectionPanel panel = CurrentPanel();
            ElectionBeforePanel before = panel as ElectionBeforePanel;
            ElectionLocalPanel local = panel as ElectionLocalPanel;
            ElectionFinalPanel final = panel as ElectionFinalPanel;
            if (before != null)
            {
                BuildBefore(builder, before);
            }
            else if (local != null)
            {
                BuildLocal(builder, local);
            }
            else if (final != null)
            {
                BuildFinal(builder, final);
            }

            BuildActions(builder, window);
        }

        // ---- the window's own chrome ----

        /// <summary>The government the election is being held under, the Hissho observance line where
        /// the empire has festivals, and the step marks - everything the window draws around whichever
        /// panel is showing (<c>ElectionModalWindow.cs:189-215,237-249</c>).</summary>
        private void BuildHeader(GraphBuilder builder, ElectionModalWindow window)
        {
            builder.BeginStop(HeaderStop);
            _cells.Clear();

            AddReadout(
                _cells,
                Widget(window.GovernmentTitle),
                "election:government",
                Raw(window.GovernmentTitle)
            );

            // One line for a panel whose children are all primitives: the caption the game draws
            // ("Next Hissho Observance:") and the festival's name are read together, and the line's
            // own tooltip carries the constructible's dossier.
            AgeTransform festival = window.HisshosFestivalPanel;
            if (festival != null && AgeWidgets.Visible(festival))
            {
                AddReadout(_cells, festival, "election:festival", Raw(window.NextFestivalLine));
            }

            AddStepMarks(_cells, window);
            Cells.Emit(builder, _cells);
        }

        /// <summary>
        /// The step marks, as the game draws them: one <c>AgeControlToggle</c> per step with the
        /// showing one switched on (<c>ElectionModalWindow.cs:269-275</c>).
        ///
        /// Whether they are CLICKABLE is prefab data, so it is asked of the widget rather than assumed:
        /// the engine dispatches a toggle's switch only when it names both an object and a method
        /// (<c>AgeControlToggle.cs:214-216</c>), which is the same test used here. A mark the game
        /// wired is a radio the player can jump steps with; one it did not is a read-only position
        /// mark - focusable and saying which step is current, with no activation to promise.
        ///
        /// No spoken "N of M" is invented for them: the marks are peers reached with left and right,
        /// so the graph's own position stamp says it, and the game itself draws dots.
        /// </summary>
        private static void AddStepMarks(List<Cell> cells, ElectionModalWindow window)
        {
            AgeControlToggle[] marks = window.StepToggles;
            for (int i = 0; marks != null && i < marks.Length; i++)
            {
                AgeControlToggle mark = marks[i];
                AgeTransform widget = AgeWidgets.Transform(mark);
                if (widget == null || !AgeWidgets.Visible(widget))
                {
                    continue;
                }

                AgeControlToggle it = mark;
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                TooltipMode? mode = ModeOf(widget);
                NodeVtable vtable;
                if (Clickable(mark))
                {
                    vtable = GraphNodes.Radio(
                        () => NameOf(widget, tooltip),
                        () => it.State,
                        () => AgeWidgets.Toggle(it),
                        () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                        null,
                        tooltip,
                        mode
                    );
                }
                else
                {
                    // The one role-less control type: there is nothing here to work, and a mark that
                    // said "radio button" would promise a step jump the game does not offer.
                    vtable = new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => NameOf(widget, tooltip)),
                            GraphNodes.SelectedPart(() => it.State),
                        },
                        Sections = GraphNodes.Sections(null, tooltip, mode),
                    };
                }

                AgeWidgets.Point(vtable, mark, tooltip, widget);
                Cells.Add(cells, widget, ControlId.Structural("election:step/" + i), vtable);
            }
        }

        /// <summary>Whether the engine would dispatch this toggle's switch - the engine's own test
        /// (<c>AgeControlToggle.cs:214</c>), asked because the wiring is serialized in the prefab and
        /// cannot be read out of the game's source.</summary>
        private static bool Clickable(AgeControlToggle toggle)
        {
            try
            {
                return toggle != null
                    && toggle.OnSwitchObject != null
                    && !string.IsNullOrEmpty(toggle.OnSwitchMethod);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- step 0: pick a party, pick an action ----

        private void BuildBefore(GraphBuilder builder, ElectionBeforePanel panel)
        {
            builder.BeginStop(BeforeCandidatesStop);
            _cells.Clear();
            AddPanelTitle(_cells, panel);
            int firstCard = _cells.Count;
            AddCandidates(_cells, panel);
            Cells.Emit(builder, _cells);

            // The cards, not the question above them: the question is what arriving announces, and
            // the selected card's own Selected part then refines the landing onto the party the game
            // has already picked for the player (ElectionBeforePanel.cs:203-237).
            if (_cells.Count > firstCard)
            {
                builder.SetStart(_cells[firstCard].Id);
            }

            builder.BeginStop(BeforeActionsStop);
            _cells.Clear();
            if (AgeWidgets.Visible(panel.ElectionActionsGroup))
            {
                // A caption the game draws over SEVERAL controls is a node of its own.
                AddReadout(
                    _cells,
                    Widget(panel.ElectionActionTitle),
                    "election:action-caption",
                    Raw(panel.ElectionActionTitle)
                );
                AddElectionActions(_cells, panel);
            }

            Cells.Emit(builder, _cells);

            builder.BeginStop(BeforeLawsStop);
            _cells.Clear();
            if (AgeWidgets.Visible(panel.PoliticsLawsGroup))
            {
                // Exactly one of the two is drawn (ElectionBeforePanel.cs:341-346), so declaring both
                // by visibility is what puts "Show ... laws" or "Hide" on the page and never both.
                AddButton(_cells, Widget(panel.ShowPoliticsLawsButton), "show-laws");
                AddButton(_cells, Widget(panel.HidePoliticsLawsButton), "hide-laws");
                if (AgeWidgets.Visible(Widget(panel.PoliticsLawsScrollView)))
                {
                    AddLawCards(_cells, panel.PoliticsLawsTable, "election:before/law");
                }
            }

            Cells.Emit(builder, _cells);

            builder.BeginStop(BeforeResourcesStop);
            _cells.Clear();
            if (AgeWidgets.Visible(panel.EmpireResourcesGroup))
            {
                AddReadout(
                    _cells,
                    Widget(panel.EmpireMoneyLabel),
                    "election:money",
                    Raw(panel.EmpireMoneyLabel)
                );
                AddReadout(
                    _cells,
                    Widget(panel.EmpireInfluenceLabel),
                    "election:influence",
                    Raw(panel.EmpireInfluenceLabel)
                );
            }

            Cells.Emit(builder, _cells);
        }

        /// <summary>
        /// The parties standing, as the game's own cards.
        ///
        /// They are radios because that is the game's model: the toggle's switch handler makes one of
        /// them the choice and clears the rest (<c>ElectionBeforePanel.cs:239-254</c>), and picking is
        /// not yet doing - nothing is applied until Next Step hands the choice over (:348-355). Every
        /// card is enabled (:181-183), so a card that reads unavailable means the panel itself is.
        ///
        /// The card's whole face is read: the party, the senator standing for it or the game's
        /// "Political Party" where there is none (<c>CandidateCard.cs:45-52</c>), the survey score
        /// (:53-62), the experience line and the level. All of it is permanently drawn, so all of it
        /// is spoken - none of it is tooltip-ruled.
        /// </summary>
        private static void AddCandidates(List<Cell> cells, ElectionBeforePanel panel)
        {
            AgeTransform table = panel.CandidateCardsTable;
            IList<AgeTransform> children = Children(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                CandidateCard card = Component<CandidateCard>(child);
                if (card == null || !AgeWidgets.Visible(child) || card.GuiPolitics == null)
                {
                    continue;
                }

                AgeControlToggle toggle = card.Toggle;
                AgeTransform widget = child;
                // The party's dossier hangs off a child of the card, not off the card: pointing at the
                // card itself would draw nothing while the readout still said it had a tooltip.
                AgeTooltip tooltip = card.PoliticsTooltip ?? AgeWidgets.Raw(child);
                NodeVtable vtable = GraphNodes.Radio(
                    () => AgeWidgets.TextOf(widget),
                    () => toggle != null && toggle.State,
                    () => AgeWidgets.Toggle(toggle),
                    () => AgeWidgets.Operable(AgeWidgets.Transform(toggle)),
                    null,
                    tooltip
                );
                AgeWidgets.Point(vtable, toggle, tooltip, Anchor(tooltip, widget));
                Cells.Add(cells, widget, ControlId.Structural("election:candidate/" + i), vtable);
            }
        }

        /// <summary>
        /// The actions the government offers on the party that has been picked.
        ///
        /// Radios again, and exclusive the same way (<c>ElectionBeforePanel.cs:256-271</c>). What is
        /// different is that one can be REFUSED: the panel switches off any action the empire cannot
        /// pay for (:147-161), so it stays on the page, reads unavailable, and swallows the keypress -
        /// which is the point, because what an action costs and that it is out of reach is exactly what
        /// the player is here to find out. The cost is drawn on the toggle
        /// (<c>ElectionActionToggle.cs:23-27</c>) and so is spoken with its name; the game's own
        /// "cannot afford" sentence is in the tooltip it assembles
        /// (<c>GuiElectionAction.Description</c>), which the review buffer carries.
        /// </summary>
        private static void AddElectionActions(List<Cell> cells, ElectionBeforePanel panel)
        {
            IList<AgeTransform> children = Children(panel.ElectionActionTogglesTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                ElectionActionToggle item = Component<ElectionActionToggle>(child);
                if (
                    item == null
                    || !AgeWidgets.Visible(child)
                    || item.ElectionActionDefinition == null
                )
                {
                    continue;
                }

                AgeControlToggle toggle = item.Toggle;
                AgeTransform widget = child;
                AgeTooltip tooltip = item.Tooltip ?? AgeWidgets.Raw(child);
                Func<bool> offered = () => AgeWidgets.Operable(AgeWidgets.Transform(toggle));
                NodeVtable vtable = GraphNodes.Radio(
                    () => AgeWidgets.TextOf(widget),
                    () => toggle != null && toggle.State,
                    () => AgeWidgets.Toggle(toggle),
                    offered,
                    null,
                    tooltip
                );
                GraphNodes.AddRefusal(vtable, tooltip, offered);

                AgeWidgets.Point(vtable, toggle, tooltip, Anchor(tooltip, widget));
                Cells.Add(cells, widget, ControlId.Structural("election:action/" + i), vtable);
            }
        }

        // ---- step 1: the vote breakdown ----

        private void BuildLocal(GraphBuilder builder, ElectionLocalPanel panel)
        {
            builder.BeginStop(LocalSupportStop);
            _cells.Clear();
            AddPanelTitle(_cells, panel);
            int firstBar = _cells.Count;
            AddSupportGauges(_cells, panel);
            // The empire's running total is a bare "37" on its own label: the words for it are the two
            // captions the prefab draws BESIDE it in the same group - "Overall Empire" above and "Total
            // representatives" below (measured on the prefab; neither the label nor the group carries a
            // tooltip, and %TotalElectorsTitle appears nowhere in the game's code). So the group is the
            // line, which is also what puts its header on the page - nothing else here reads it.
            AgeTransform total = Widget(panel.TotalElectorsValue);
            AgeTransform box = total == null ? null : total.Parent;
            AddReadout(
                _cells,
                box ?? total,
                "election:total-electors",
                Raw(box) ?? Raw(panel.TotalElectorsValue)
            );
            Cells.Emit(builder, _cells);
            bool started = _cells.Count > firstBar;
            if (started)
            {
                builder.SetStart(_cells[firstBar].Id);
            }

            builder.BeginStop(LocalSystemStop);
            _cells.Clear();

            // The carousel's own caption: the system it is showing and where that is in the round.
            // The drawn index is spoken because the game draws it and nothing else here says it - the
            // systems are not graph siblings, so no position is stamped for them.
            AgeTransform name = Widget(panel.StarSystemNameLabel);
            if (name != null && AgeWidgets.Visible(name))
            {
                AgePrimitiveLabel which = panel.StarSystemNameLabel;
                AgePrimitiveLabel index = panel.StarSystemIndexLabel;
                ControlId id = ControlId.Structural("election:system");
                NodeVtable vtable = GraphNodes.Readout(
                    () => AgeText.Label(which),
                    () => AgeText.Label(index),
                    null,
                    Raw(which)
                );
                AgeWidgets.PointAt(vtable, name);
                Cells.Add(_cells, name, id, vtable);

                // The bars above draw no words of their own, so on a build where none of them was
                // declared the system line is the first thing here that is not the panel's question -
                // and the question is what arriving already said.
                if (!started)
                {
                    builder.SetStart(id);
                }
            }

            AddButton(_cells, panel.PreviousSystemButton, "previous-system");
            AddButton(_cells, panel.NextSystemButton, "next-system");
            AddRepresentatives(_cells, panel);
            Cells.Emit(builder, _cells);
        }

        /// <summary>
        /// The cumulative support bars.
        ///
        /// The panel binds these with a party icon and a fill ratio and no words at all
        /// (<c>ElectionLocalPanel.cs:296-308</c>) - it never touches their tooltips, so whether one is
        /// there is prefab data. Declared from what is actually on the widget: a bar the prefab named
        /// or gave a tooltip becomes a readout, and one that is only a coloured bar is left out rather
        /// than declared as a nameless number. Which party each bar is for cannot be recovered from the
        /// game's source - the ordered list behind the table is private - so the same per-party counts
        /// are reached through the representative rows below, which the game DOES name.
        /// </summary>
        private static void AddSupportGauges(List<Cell> cells, ElectionLocalPanel panel)
        {
            IList<AgeTransform> children = Children(panel.PoliticsCumulativeSupportGaugesTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (
                    Component<PoliticsCumulativeSupportGauge>(child) == null
                    || !AgeWidgets.Visible(child)
                )
                {
                    continue;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(child);
                if (string.IsNullOrEmpty(AgeWidgets.TextOf(child)) && tooltip == null)
                {
                    continue;
                }

                AddReadout(cells, child, "election:support/" + i, tooltip);
            }
        }

        /// <summary>
        /// How the current system's votes are split - one row per party with any, the row being an
        /// icon and a count (<c>ElectionLocalPanel.cs:258-264</c>,
        /// <c>SystemRepresentativeItem.cs:9-20</c>). The party is named nowhere on the row, only on the
        /// wrapper the game hangs on its tooltip, which is the case
        /// <see cref="AgeWidgets.TooltipTitle"/> exists for.
        /// </summary>
        private static void AddRepresentatives(List<Cell> cells, ElectionLocalPanel panel)
        {
            IList<AgeTransform> children = Children(panel.SystemRepresentativeTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                SystemRepresentativeItem item = Component<SystemRepresentativeItem>(child);
                if (item == null || !AgeWidgets.Visible(child))
                {
                    continue;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(child);
                AgePrimitiveLabel count = item.ProbabilityLabel;
                NodeVtable vtable = GraphNodes.Readout(
                    () => AgeWidgets.TooltipTitle(tooltip),
                    () => AgeText.Label(count),
                    null,
                    tooltip
                );
                AgeWidgets.PointAt(vtable, child);
                Cells.Add(
                    cells,
                    child,
                    ControlId.Structural("election:representatives/" + i),
                    vtable
                );
            }
        }

        // ---- step 2: the result ----

        private void BuildFinal(GraphBuilder builder, ElectionFinalPanel panel)
        {
            builder.BeginStop(FinalWinnersStop);
            _cells.Clear();
            AddPanelTitle(_cells, panel);
            int firstCard = _cells.Count;
            AddWinners(_cells, panel);
            Cells.Emit(builder, _cells);
            if (_cells.Count > firstCard)
            {
                builder.SetStart(_cells[firstCard].Id);
            }

            builder.BeginStop(FinalLawsStop);
            _cells.Clear();
            // The group or the label, never both (ElectionFinalPanel.cs:143-156).
            AddReadout(
                _cells,
                Widget(panel.NoNewLawsLabel),
                "election:no-new-laws",
                Raw(panel.NoNewLawsLabel)
            );
            if (
                AgeWidgets.Visible(panel.UnlockedLawsGroup)
                && AgeWidgets.Visible(Widget(panel.UnlockedLawScrollView))
            )
            {
                AddLawCards(_cells, panel.UnlockedLawLinesTable, "election:final/law");
            }

            Cells.Emit(builder, _cells);

            // What choosing an election action came to. Declared from visibility like everything else,
            // which for the shipped game means never: Refresh sets the outcomes group AND the
            // no-outcomes label invisible unconditionally as its last act, after the branch that had
            // just decided between them (ElectionFinalPanel.cs:157-181). The code is here so that the
            // day the game draws them - a patch, another build - they are read; nothing is invented in
            // their place while it does not.
            builder.BeginStop(FinalOutcomesStop);
            _cells.Clear();
            AddReadout(
                _cells,
                Widget(panel.NoActionOutcomesLabel),
                "election:no-outcomes",
                Raw(panel.NoActionOutcomesLabel)
            );
            if (AgeWidgets.Visible(panel.ElectionActionOutcomesGroup))
            {
                AddReadout(
                    _cells,
                    Widget(panel.ActionOutcomesLabel),
                    "election:outcomes-caption",
                    Raw(panel.ActionOutcomesLabel)
                );
                AddOutcomeLines(_cells, panel);
            }

            Cells.Emit(builder, _cells);
        }

        /// <summary>The senators the election returned. Read-only cards - the panel binds no client to
        /// them and their toggles are never wired (<c>ElectionFinalPanel.cs:184-204</c>) - so what they
        /// are is what they say: the party, its experience line, the senator's level, and the party's
        /// dossier on the tooltip. The support they won is drawn as a circular gauge with no number on
        /// it, and nothing here invents one.</summary>
        private static void AddWinners(List<Cell> cells, ElectionFinalPanel panel)
        {
            IList<AgeTransform> children = Children(panel.WinnerSenatorCardsTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                WinnerSenatorCard card = Component<WinnerSenatorCard>(child);
                if (card == null || !AgeWidgets.Visible(child) || card.Senator == null)
                {
                    continue;
                }

                AgeTooltip tooltip = card.PoliticsTooltip ?? AgeWidgets.Raw(child);
                AgeTransform widget = child;
                NodeVtable vtable = GraphNodes.Readout(
                    () => AgeWidgets.TextOf(widget),
                    () => null,
                    null,
                    tooltip
                );
                AgeWidgets.PointAt(vtable, Anchor(tooltip, widget));
                Cells.Add(cells, widget, ControlId.Structural("election:winner/" + i), vtable);
            }
        }

        private static void AddOutcomeLines(List<Cell> cells, ElectionFinalPanel panel)
        {
            IList<AgeTransform> children = Children(panel.ElectionActionOutcomesTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                ElectionActionOutcomeLine line = Component<ElectionActionOutcomeLine>(child);
                if (line == null || !AgeWidgets.Visible(child))
                {
                    continue;
                }

                AddReadout(cells, Widget(line.OutcomeTitleLabel), "election:outcome/" + i);
            }
        }

        // ---- the two buttons along the bottom ----

        /// <summary>
        /// Next Step - or Finish on the last step - and Skip, which is drawn only while there is a next
        /// step to skip to (<c>ElectionModalWindow.cs:56-69</c>). Both are named by the game, on the
        /// button and in its tooltip, and both are pressed through the button's own wiring: Next Step
        /// is re-pointed at whichever panel is showing (:118), so replaying it hands the step over to
        /// the right panel, and Skip raises the same confirmation Escape does (:307-310).
        /// </summary>
        private void BuildActions(GraphBuilder builder, ElectionModalWindow window)
        {
            builder.BeginStop(ActionsStop);
            _cells.Clear();
            AddButton(_cells, Widget(window.NextStepButton), "next-step");
            AddButton(_cells, Widget(window.SkipElectionButton), "skip");
            Cells.Emit(builder, _cells);
        }

        // ---- shared ----

        /// <summary>The step's own question, as its panel drew it.</summary>
        private static void AddPanelTitle(List<Cell> cells, ElectionPanel panel)
        {
            AddReadout(cells, Widget(panel.PanelTitleLabel), "election:panel-title");
        }

        /// <summary>
        /// The laws a party carries, wherever they are listed - the party's own list on step 0 and the
        /// ones the election unlocked on step 2 are the same card type
        /// (<c>ElectionBeforePanel.cs:316-325</c>, <c>ElectionFinalPanel.cs:206-215</c>).
        ///
        /// Read-only, because the game binds them disabled: <c>LawCard.Bind(..., disabled: true)</c>
        /// switches the card's transform off (<c>LawCard.cs:65</c>) and the panels switch the toggle
        /// off too. So they are readouts rather than refusing controls - there is no action here being
        /// withheld, this is a list.
        /// </summary>
        private static void AddLawCards(List<Cell> cells, AgeTransform table, string keyPrefix)
        {
            IList<AgeTransform> children = Children(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                LawCard card = Component<LawCard>(child);
                if (card == null || !AgeWidgets.Visible(child) || card.GuiLaw == null)
                {
                    continue;
                }

                AgeTooltip tooltip = card.Tooltip ?? AgeWidgets.Raw(child);
                AgePrimitiveLabel title = card.LawShortTitle;
                NodeVtable vtable = GraphNodes.Readout(
                    () => AgeText.Label(title),
                    () => null,
                    null,
                    tooltip
                );
                AgeWidgets.PointAt(vtable, Anchor(tooltip, child));
                Cells.Add(cells, child, ControlId.Structural(keyPrefix + "/" + i), vtable);
            }
        }

        /// <summary>A button the game is drawing, declared whenever it is drawn - including while it is
        /// switched off, which is how the carousel's arrows read on an empire with one system
        /// (<c>ElectionLocalPanel.cs:254-255</c>): unavailable rather than absent.</summary>
        private static void AddButton(List<Cell> cells, AgeTransform widget, string name)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> offered = () => AgeWidgets.Operable(it);
            NodeVtable vtable = GraphNodes.Button(
                () => NameOf(it, tooltip),
                () => AgeWidgets.Press(it),
                offered,
                tooltip,
                ModeOf(widget)
            );
            GraphNodes.AddRefusal(vtable, tooltip, offered);

            AgeWidgets.Point(vtable, AgeWidgets.Button(widget), tooltip, widget);
            Cells.Add(cells, widget, ControlId.Structural("election:button/" + name), vtable);
        }

        /// <summary>
        /// What the game calls a control: the words drawn on it, else the name it keeps on the wrapper
        /// hung on its tooltip, else the sentence that tooltip opens with. The arrows either side of the
        /// system carousel and the step marks draw nothing at all and are named only there.
        ///
        /// The tooltip's own text is the LAST resort and only when its words are really on the widget -
        /// a renderer-assembled tooltip's content field holds authoring leftovers, so it is never a
        /// name.
        /// </summary>
        private static string NameOf(AgeTransform widget, AgeTooltip tooltip)
        {
            string drawn = AgeWidgets.TextOf(widget);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            string kept = AgeWidgets.TooltipTitle(tooltip);
            if (!string.IsNullOrEmpty(kept))
            {
                return kept;
            }

            if (AgeWidgets.Readable(tooltip) == null)
            {
                return null;
            }

            IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
            return lines.Count > 0 ? lines[0] : null;
        }

        /// <summary>
        /// How a wordless control's tooltip reaches the player.
        ///
        /// A control that draws no words is NAMED by its tooltip (see <see cref="NameOf"/>), so
        /// announcing that tooltip as well would say the name twice. It is indicated instead: the
        /// player hears that there is more and the review buffer holds every word of it, with the first
        /// line dropped where it only repeats the name. A control that draws its own words is left to
        /// the ordinary rule.
        ///
        /// Whether a control draws words is fixed by the prefab, so this is decided once when the node
        /// is declared rather than asked every frame.
        /// </summary>
        private static TooltipMode? ModeOf(AgeTransform widget)
        {
            return string.IsNullOrEmpty(AgeWidgets.TextOf(widget))
                ? (TooltipMode?)TooltipMode.Indicate
                : null;
        }

        /// <summary>A line the player reads and does not work - what the whole phrase under
        /// <paramref name="widget"/> says, with whatever its tooltip adds.</summary>
        private static void AddReadout(
            List<Cell> cells,
            AgeTransform widget,
            string key,
            AgeTooltip tooltip = null
        )
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip its = tooltip ?? AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeWidgets.TextOf(it),
                () => null,
                null,
                its
            );
            AgeWidgets.PointAt(vtable, it);
            Cells.Add(cells, widget, ControlId.Structural(key), vtable);
        }

        /// <summary>Where a card's tooltip should be drawn: under the widget the tooltip itself sits
        /// on, which for a card is a child of it rather than the card.</summary>
        private static AgeTransform Anchor(AgeTooltip tooltip, AgeTransform fallback)
        {
            try
            {
                AgeTransform own = tooltip == null ? null : tooltip.AgeTransform;
                return own ?? fallback;
            }
            catch (Exception)
            {
                return fallback;
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

        private static AgeTransform Widget(AgeControl control)
        {
            return AgeWidgets.Transform(control);
        }

        private static AgeTooltip Raw(AgePrimitiveLabel label)
        {
            return AgeWidgets.Raw(Widget(label));
        }

        private static AgeTooltip Raw(AgeTransform widget)
        {
            return AgeWidgets.Raw(widget);
        }

        private static IList<AgeTransform> Children(AgeTransform table)
        {
            try
            {
                return table == null ? null : table.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static T Component<T>(AgeTransform widget)
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

        /// <summary>Which step the window says it is on, or -1 while it has none
        /// (<c>ElectionModalWindow.cs:38,177</c>).</summary>
        private static int Step()
        {
            ElectionModalWindow window = Window();
            try
            {
                return window == null ? -1 : window.Step;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static string PanelTitle(ElectionPanel panel)
        {
            try
            {
                return panel == null ? null : AgeText.Label(panel.PanelTitleLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The panel the player is looking at, or null while none has finished arriving.
        ///
        /// "Which panel is drawn" rather than "which step was I told about": the window's own step
        /// number is set before the panel is shown and the two disagree for a few frames every time.
        /// Fully arrived means visible and not mid-animation either way - a panel on its way in has
        /// its transform disabled (<c>GuiPanel.cs:250,255</c>), which every control inside it would
        /// otherwise report as unavailable.
        /// </summary>
        private static ElectionPanel CurrentPanel()
        {
            ElectionModalWindow window = Window();
            try
            {
                ElectionPanel[] panels = window == null ? null : window.ElectionPanels;
                for (int i = 0; panels != null && i < panels.Length; i++)
                {
                    ElectionPanel panel = panels[i];
                    if (
                        panel != null
                        && !panel.Showing
                        && !panel.Hiding
                        && AgeWidgets.Visible(panel.AgeTransform)
                    )
                    {
                        return panel;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private static ElectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<ElectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
