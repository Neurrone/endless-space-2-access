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
    /// An election needs a real election turn, which no save in the project's fixtures reaches, so this
    /// screen was written code-verified. Step 1, the vote breakdown, has since been measured live on a
    /// player's own election (one-system empire); steps 0 and 2 are still code-verified only. What that
    /// one system could not show is in <c>docs/test-recipes/modals-and-outgame.md</c>, under "The
    /// election wizard".
    /// </summary>
    public sealed partial class ElectionScreen : Screen
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

        /// <summary>The three sections of the vote-breakdown column, in the order it draws them: the
        /// step's own question, the bars the prefab captions, and the empire's running total with how
        /// far the count has got. Declared whatever the panel holds, so the region jump means the same
        /// thing on every system.</summary>
        private const string LocalTitleRegion = "election:local/title";
        private const string TrendsRegion = "election:local/trends";
        private const string LocalEmpireRegion = "election:local/empire";

        private static readonly object FinalWinnersStop = "election:final/winners";

        /// <summary>Shared by every winner's card and every badge under it, so Down steps from one
        /// winner's card to the next winner's card and from a badge to the badge below it, rather than
        /// down one winner's whole column and into the next. Each of them is a row of ONE: a redirect
        /// badge is a thing in its own right, not a column of the card (owner ruling 2026-08-22), so
        /// Right from the card falls through silently.</summary>
        private static readonly object WinnersRowKey = "election:final/winner-row";

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

        /// <summary>The vote-breakdown panel whose carousel has already been held still, so the flag is
        /// written once per arrival rather than every frame. Instance state: a hot reload starts it over
        /// and holds the carousel again, which is idempotent.</summary>
        private ElectionLocalPanel _held;

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
            _held = null;
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
        ///
        /// It is also where the vote breakdown's carousel is held still (<see cref="HoldCarousel"/>):
        /// which panel is showing is asked here every frame anyway, and the flag is written once per
        /// arrival on it.
        /// </summary>
        public override void OnUpdate()
        {
            try
            {
                ElectionPanel showing = CurrentPanel();
                ElectionLocalPanel local = showing as ElectionLocalPanel;
                if (local == null)
                {
                    _held = null;
                }
                else if (!ReferenceEquals(_held, local))
                {
                    _held = local;
                    HoldCarousel(local);
                }

                int step = Step();
                if (!_steps.IsNew(step))
                {
                    return;
                }

                string title = PanelTitle(showing);
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
                AgeWidgets.Transform(window.GovernmentTitle),
                "election:government",
                Raw(window.GovernmentTitle)
            );

            // One line for a panel whose children are all primitives: the caption the game draws
            // ("Next Hissho Observance:") and the festival's name are read together, and the line's
            // own tooltip carries the constructible's dossier.
            // Whether the empire has festivals at all is AddReadout's own drawn test, asked of this
            // same panel.
            AgeTransform festival = window.HisshosFestivalPanel;
            AddReadout(_cells, festival, "election:festival", Raw(window.NextFestivalLine));

            AddStepMarks(_cells, window);
            Cells.EmitLinear(builder, _cells);
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
                // Banding input: Cells.Add takes each mark without asking the gate, and the marks are
                // worked into one row by where they are drawn along the track.
                if (widget == null || !AgeWidgets.Visible(widget))
                {
                    continue;
                }

                AgeControlToggle it = mark;
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable;
                if (Clickable(mark))
                {
                    vtable = GraphNodes.Radio(
                        () => NameOf(widget, tooltip),
                        () => it.State,
                        () => AgeWidgets.Toggle(it),
                        () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                        null,
                        tooltip
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
                        Sections = GraphNodes.Sections(null, tooltip),
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
            AddButton(_cells, AgeWidgets.Transform(window.NextStepButton), "next-step");
            AddButton(_cells, AgeWidgets.Transform(window.SkipElectionButton), "skip");
            Cells.EmitLinear(builder, _cells);
        }

        // ---- shared ----

        /// <summary>
        /// The caption the prefab draws over the column of party bars, as the level they are announced
        /// under.
        ///
        /// The words are serialized in the prefab - the panel has no label field for them
        /// (<c>ElectionLocalPanel.cs:36-66</c>) - so they are hunted the way a board's heading is:
        /// the group's own "Title" child first, then the first drawn child of the group that is not
        /// the gauge table and says something. Whatever comes back is the GAME's word; where nothing
        /// does, nothing is pushed, so the bars are never announced under an empty level.
        /// </summary>
        private static bool Trends(GraphBuilder builder, ElectionLocalPanel panel)
        {
            AgeTransform group = panel.PoliticsSupportGroup;
            // Flow control: a region and every trend bar under it are read below, and the answer tells
            // the caller whether the region was opened at all.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return false;
            }

            string text = AgeWidgets.TextOf(AgeWidgets.ChildNamed(group, "Title", 2));
            if (string.IsNullOrEmpty(text))
            {
                text = Caption(group, panel.PoliticsCumulativeSupportGaugesTable);
            }

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            builder.PushContext(text);
            return true;
        }

        /// <summary>The first drawn child of <paramref name="group"/> that says something and is not
        /// the table of bars (nor an ancestor of it), read one level deep so a container of bars can
        /// never be mistaken for a caption.</summary>
        private static string Caption(AgeTransform group, AgeTransform bars)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(group);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                // Content: which of the group's children is the word the bars are announced under - a
                // caption, not a node.
                if (child == null || !AgeWidgets.Visible(child) || AgeWidgets.Under(bars, child))
                {
                    continue;
                }

                string text = AgeWidgets.TextOf(child, 1);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return null;
        }

        /// <summary>The step's own question, as its panel drew it.</summary>
        private static void AddPanelTitle(List<Cell> cells, ElectionPanel panel)
        {
            AddReadout(cells, AgeWidgets.Transform(panel.PanelTitleLabel), "election:panel-title");
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
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                LawCard card = Component<LawCard>(child);
                // Banding input: the law cells are laid into rows by their rectangles, and Cells.Add
                // takes them without asking the gate.
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
            // Banding input: Cells.Add takes the button without asking the gate, and its rectangle is
            // what puts it in a row with whatever the panel drew beside it.
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
                tooltip
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
            return string.IsNullOrEmpty(drawn)
                ? AgeWidgets.TooltipTitle(tooltip) ?? CardActions.FirstLine(tooltip)
                : drawn;
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
            // Banding input, as at AddButton: Cells.Add takes the readout without asking the gate, and
            // the panels pass PAIRS of labels here where the game draws only one of them.
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
            return AgeWidgets.TooltipOwner(tooltip) ?? fallback;
        }

        private static AgeTooltip Raw(AgePrimitiveLabel label)
        {
            return AgeWidgets.Raw(AgeWidgets.Transform(label));
        }

        private static AgeTooltip Raw(AgeTransform widget)
        {
            return AgeWidgets.Raw(widget);
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
                    // Flow control: which of the election's steps the whole build walks - the window
                    // keeps every panel and shows one.
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
            return GameWindows.Of<ElectionModalWindow>();
        }
    }
}
