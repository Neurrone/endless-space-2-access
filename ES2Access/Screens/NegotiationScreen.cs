using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The negotiation table: the window that opens when the player clicks an empire in the diplomacy
    /// ring, answers a treaty offer with a counter-offer, or follows a relation-change popup's link to
    /// talk about it.
    ///
    /// <b>The contract arrives late.</b> Showing the window does not build a deal - it asks the server for
    /// one (<c>InitializeAndStartContractStub</c>, <c>OnBeginShow</c> :551-574) and the answer comes back
    /// in a callback. Until then <c>CurrentContract</c> is null, the window's own <c>Refresh</c> hides
    /// everything but the frame (:635-642), and there is nothing to walk. So arrival waits for the
    /// contract, not for the window: a cursor that landed on the frame would be standing in an empty box.
    ///
    /// <b>Closing this window is an ACTION</b>, and one of the few in the mod that are: leaving an unsigned
    /// negotiation posts <c>OrderChangeDiplomaticContractState(Inactive)</c>
    /// (<c>UnBindActiveContract</c> :772-785). Nothing here does that on the mod's behalf - Escape is the
    /// game's, as it is on every other modal - but it is why this window cannot be opened and closed freely
    /// while testing.
    ///
    /// Eight bands, in the order the window draws them:
    /// the two empires along the top with the status between them; the empire dossier, while the tick box
    /// beside the title has it open; the pressure gauge, with the button that forces a truce during a war;
    /// the two shelves of terms, one per empire; the basket the deal is being assembled in; what the
    /// computer thinks of the deal so far; and the buttons that reset it, ask the computer to fill it in,
    /// or make the offer.
    ///
    /// <b>Two readouts are deliberately less than what is on screen.</b> The deal-approval band draws a
    /// gauge and lights one of five faces; what it does NOT draw is the computer's written reasons, which
    /// the window computes into a local variable and throws away (<c>RefreshDealApproval</c> :994-1006) -
    /// so they are not spoken here either, because a screen reader saying something no sighted player can
    /// see is not accessibility. And the god-mode handlers on the influence stock and the pressure group
    /// (:1524-1579) are not declared at all.
    ///
    /// The <b>Suggest terms</b> button switches the whole content off while the computer is thinking
    /// (<c>StartSuggestTermsFeedback</c> :1055-1062), so every control in the window reads unavailable for
    /// as long as that takes. That is what the window does, and it is read as it is.
    /// </summary>
    public sealed class NegotiationScreen : Screen
    {
        private static readonly object HeaderStop = "negotiation:header";
        private static readonly object DossierStop = "negotiation:dossier";
        private static readonly object PressureStop = "negotiation:pressure";
        private static readonly object MyTermsStop = "negotiation:my-terms";
        private static readonly object TheirTermsStop = "negotiation:their-terms";
        private static readonly object ContractStop = "negotiation:contract";
        private static readonly object ApprovalStop = "negotiation:approval";
        private static readonly object ActionsStop = "negotiation:actions";
        private static readonly object DossierRegion = "negotiation:dossier/panel";

        private const string Keys = "negotiation:";

        private readonly List<Cell> _cells = new List<Cell>();
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        /// <summary>What the table last said about itself, so a change the game makes on its own - the
        /// computer re-evaluating the deal, the basket filling itself in after Suggest terms, the offer
        /// getting dearer - is spoken where the player is rather than only where they walk. One tuple,
        /// diffed; baselined to nothing, so arriving says nothing.</summary>
        private string _watched;

        public override string Key
        {
            get { return "screen.negotiation"; }
        }

        /// <summary>Above the notification popups that open it - the mod's notification screen does not
        /// stand down for modals, and the popup that raised this one is still on screen behind it - and
        /// below the message box the truce button's confirmation raises.</summary>
        public override int Layer
        {
            get { return 41; }
        }

        public override string ScreenName
        {
            get
            {
                string drawn = Title(Window());
                return string.IsNullOrEmpty(drawn)
                    ? ModStrings.Get(ModStrings.ScreenNegotiation)
                    : drawn;
            }
        }

        /// <summary>The basket, because that is the deal - the shelves are what it is filled from and are
        /// one Tab away in either direction.</summary>
        public override object InitialFocusStop
        {
            get { return ContractStop; }
        }

        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape is the game's: it closes the modal, which is also what the window's own close
        /// button does.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>While a quantity box has been asked for and the keyboard has not changed hands yet,
        /// what the player types is meant for the box.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        /// <summary>Arrival waits for the CONTRACT, and departure comes when the window drops it - which
        /// is the last thing hiding does. Being buried under another modal (the truce confirmation, the
        /// game's message box) takes the keyboard away, because the stack is exclusive and this window is
        /// not on screen while one is up.</summary>
        public override bool IsActive()
        {
            try
            {
                NegotiationModalWindow window = Window();
                return window != null
                    && window.Shown
                    && window.IsReady
                    && window.CurrentContract != null
                    && !Buried(window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Buried(NegotiationModalWindow window)
        {
            try
            {
                GuiManager manager = Gui.GuiGameWindowService as GuiManager;
                GuiModalWindow top = manager == null ? null : manager.ModalOnTop;
                return top != null && !ReferenceEquals(top, window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void OnPush()
        {
            _watched = null;
        }

        public override void OnPop()
        {
            _editor.Cancel();
            _watched = null;
        }

        public override void OnUpdate()
        {
            _editor.Update();
            AnnounceChanges();
        }

        public override void Build(GraphBuilder builder)
        {
            NegotiationModalWindow window = Window();
            if (window == null || window.CurrentContract == null)
            {
                return;
            }

            BuildHeader(builder, window);
            BuildDossier(builder, window);
            BuildPressure(builder, window);
            BuildTerms(builder, window, MyTermsStop, window.MyTermsPanel, window.MyAtWarLabel, "mine", ModStrings.NegotiationMyTerms);
            BuildTerms(builder, window, TheirTermsStop, window.HisTermsPanel, window.HisAtWarLabel, "theirs", ModStrings.NegotiationTheirTerms);
            BuildContract(builder, window);
            BuildApproval(builder, window);
            BuildActions(builder, window);
        }

        // ---- the two empires ----

        /// <summary>The strip across the top: the tick box that opens the dossier, then each empire's
        /// banner (who they are, their alliance or metaplot team, their influence stock and - for the
        /// other empire - their attitude with the annotations behind it), then the diplomatic status
        /// between them and the abilities that status carries.</summary>
        private void BuildHeader(GraphBuilder builder, NegotiationModalWindow window)
        {
            builder.BeginStop(HeaderStop);
            builder.PushContext(ModStrings.Get(ModStrings.NegotiationHeader));
            _cells.Clear();
            try
            {
                AddInfoToggle(window);
                AddBanner(window.MyEmpireBanner, "my-banner");
                AddBanner(window.HisEmpireBanner, "his-banner");
                Cells.AddReadout(_cells, Of(window.DiplomaticStatusLabel), Keys + "status");
                AddStatusIcon(window);
                AddAbilities(window);
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading the header threw: " + e);
            }

            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        private void AddInfoToggle(NegotiationModalWindow window)
        {
            AgeControlToggle toggle = window.EmpireInfoToggle;
            AgeTransform at = AgeWidgets.Transform(toggle);
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => Named(at, tooltip),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(at),
                tooltip,
                TooltipMode.None
            );
            AgeWidgets.Point(vtable, it, tooltip, at);
            Cells.Add(_cells, at, ControlId.Referenced(at, Keys + "empire-info"), vtable);
        }

        /// <summary>One empire's banner as one line: everything the panel draws about them, and the
        /// annotations the game hangs off the attitude label as reviewable content.</summary>
        private void AddBanner(NegotiationEmpireBannerPanel panel, string key)
        {
            AgeTransform at = panel == null ? null : panel.AgeTransform;
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            NegotiationEmpireBannerPanel it = panel;
            AgeTransform attitude = Of(panel.AttitudeLabel);
            AgeTooltip why = AgeWidgets.Raw(attitude);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Banner(it)),
                },
                Sections = GraphNodes.Sections(null, why),
            };
            AgeWidgets.PointAt(vtable, attitude ?? at);
            Cells.Add(_cells, at, ControlId.Referenced(at, Keys + key), vtable);
        }

        private static string Banner(NegotiationEmpireBannerPanel panel)
        {
            MessageBuilder message = new MessageBuilder();
            message.ListItem(Words(panel.EmpireTitleLabel));
            message.ListItem(Words(panel.AllianceNameLabel));
            message.ListItem(Words(panel.InfluenceStockLabel));
            message.ListItem(Words(panel.AttitudeLabel));
            return message.Build();
        }

        /// <summary>The status icon between the banners, whose tooltip is the only place the game explains
        /// what the status MEANS. The words beside it are already read as the status line.</summary>
        private void AddStatusIcon(NegotiationModalWindow window)
        {
            AgeTransform at = Of(window.DiplomaticStatusIcon);
            if (at == null || !AgeWidgets.Visible(at) || AgeWidgets.Raw(at) == null)
            {
                return;
            }

            Cells.Add(
                _cells,
                at,
                ControlId.Referenced(at, Keys + "status-icon"),
                StatusIcon(at)
            );
        }

        /// <summary>A figure the game draws as a bare icon and names only on its tooltip: the status icon
        /// between the banners, and each of the abilities in the strip. The words are the tooltip's own
        /// sentence where it carries one, and otherwise the name of the thing behind it - an ability's
        /// dossier is renderer-assembled, so its title is where the name lives (measured: an ability item
        /// read by the sentence alone came out with no name at all).</summary>
        private static NodeVtable StatusIcon(AgeTransform at)
        {
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            Func<string> sentence = CardActions.NameFromTooltip(tooltip);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() =>
                    {
                        string said = sentence();
                        return string.IsNullOrEmpty(said)
                            ? AgeWidgets.TooltipTitle(tooltip)
                            : said;
                    }),
                },
                Sections = GraphNodes.Sections(null, tooltip, ModeOf(tooltip)),
            };
            AgeWidgets.PointAt(vtable, at);
            return vtable;
        }

        /// <summary>A tooltip whose sentence has become the icon's NAME must not be announced again; one
        /// the renderer assembles has a name of its own and its content is still worth indicating.
        /// </summary>
        private static TooltipMode ModeOf(AgeTooltip tooltip)
        {
            return AgeWidgets.Readable(tooltip) != null
                ? TooltipMode.None
                : GraphNodes.ModeFor(tooltip);
        }

        /// <summary>The strip of diplomatic abilities the status carries, or the game's own label for
        /// having none. Unlike the ring's leader card, the abilities HERE keep their tooltips (:823), so
        /// each is its own readout named by the sentence on it.</summary>
        private void AddAbilities(NegotiationModalWindow window)
        {
            AgeTransform none = window.NoDiplomaticAbilitiesLabel;
            if (none != null && AgeWidgets.Visible(none))
            {
                Cells.AddReadout(_cells, none, Keys + "no-abilities");
                return;
            }

            AgeTransform table = window.DiplomaticAbilitiesTable;
            IList<AgeTransform> children = table == null || !AgeWidgets.Visible(table)
                ? null
                : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform at = children[i];
                if (at == null || !AgeWidgets.Visible(at) || AgeWidgets.Raw(at) == null)
                {
                    continue;
                }

                Cells.Add(
                    _cells,
                    at,
                    ControlId.Referenced(at, Keys + "ability/" + i),
                    StatusIcon(at)
                );
            }
        }

        // ---- the dossier ----

        /// <summary>The sheet of prose about the other empire, while the tick box has it open - the same
        /// panel the introduction popup opens, read by the same shared reader
        /// (<see cref="EmpireDossier"/>).</summary>
        private void BuildDossier(GraphBuilder builder, NegotiationModalWindow window)
        {
            NegotiationEmpireInfoPanel panel = window.EmpireInfoPanel;
            if (!EmpireDossier.Open(panel))
            {
                return;
            }

            builder.BeginStop(DossierStop);
            builder.PushContext(ModStrings.Get(ModStrings.NegotiationDossier));
            EmpireDossier.Build(builder, panel, Keys + "dossier/", DossierRegion);
            builder.PopContext();
        }

        // ---- pressure ----

        /// <summary>
        /// The pressure the player has built up against this empire, or - at war - how exhausted the war
        /// has left them. Both are the same gauge under two titles, and the game writes the per-turn trend
        /// into the gauge's own tooltip (<c>GetPressureTooltipContent</c> :916-937), so the gauge is a
        /// readout and the trend comes with it. The markers along it are the thresholds a demand or a
        /// forced truce needs, each with the game's own sentence about whether it has been reached.
        ///
        /// At war the band also draws the button that forces a truce; it is left drawn while refusing,
        /// with the game's failure appended to its own description (:886-895), and its Enter raises the
        /// game's own confirmation before any order is posted.
        /// </summary>
        private void BuildPressure(GraphBuilder builder, NegotiationModalWindow window)
        {
            builder.BeginStop(PressureStop);
            builder.PushContext(ModStrings.Get(ModStrings.NegotiationPressure));
            _cells.Clear();
            try
            {
                Cells.AddReadout(_cells, Of(window.PressureGroupTitle), Keys + "pressure-title");
                AgeTransform gauge = window.PressureGauge == null
                    ? null
                    : window.PressureGauge.AgeTransform;
                if (gauge != null && AgeWidgets.Visible(gauge))
                {
                    _cells.Add(
                        Cells.Readout(gauge, AgeWidgets.Raw(gauge), Keys + "pressure-gauge")
                    );
                }

                AddThresholds(window);
                AddTruce(window);
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading the pressure band threw: " + e);
            }

            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        private void AddThresholds(NegotiationModalWindow window)
        {
            AgeTransform table = window.PressureThresholdTable;
            IList<AgeTransform> children = table == null || !AgeWidgets.Visible(table)
                ? null
                : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform at = children[i];
                if (at == null || !AgeWidgets.Visible(at))
                {
                    continue;
                }

                // The marker draws no words at all: what it is, and whether it has been reached, are both
                // in the tooltip the item writes for itself (<c>GaugeThresholdItem.Bind</c> :12-32), so
                // that sentence is the readout's name.
                GaugeThresholdItem item = at.GetComponent<GaugeThresholdItem>();
                AgeTooltip tooltip = item != null && item.Tooltip != null
                    ? item.Tooltip
                    : AgeWidgets.Raw(at);
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(CardActions.NameFromTooltip(tooltip)),
                        GraphNodes.DisabledPart(() => AgeWidgets.Enabled(at)),
                    },
                    Sections = GraphNodes.Sections(null, tooltip, TooltipMode.None),
                };
                AgeWidgets.PointAt(vtable, at);
                Cells.Add(
                    _cells,
                    at,
                    ControlId.Referenced(at, Keys + "threshold/" + i),
                    vtable
                );
            }
        }

        private void AddTruce(NegotiationModalWindow window)
        {
            AgeTransform at = window.TruceButton;
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            AgeTransform host = at;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            Func<bool> offered = () => AgeWidgets.Offered(host);
            NodeVtable vtable = GraphNodes.Button(
                () => Named(host, tooltip),
                () => AgeWidgets.Press(host),
                offered,
                tooltip,
                TooltipMode.None
            );
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            AgeWidgets.PointAt(vtable, at);
            Cells.Add(_cells, at, ControlId.Referenced(at, Keys + "truce"), vtable);
        }

        // ---- the two shelves of terms ----

        /// <summary>One shelf: the category filters, then the table of terms under the game's own headers.
        /// A shelf the game has emptied because the two empires are at war draws a sentence saying so
        /// instead (<c>MyAtWarLabel</c> / <c>HisAtWarLabel</c>, :653-662), and that sentence is what the
        /// stop holds.</summary>
        private void BuildTerms(
            GraphBuilder builder,
            NegotiationModalWindow window,
            object stop,
            NegotiationTermsPanel panel,
            AgePrimitiveLabel empty,
            string key,
            string caption
        )
        {
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(stop);
            builder.PushContext(ModStrings.Get(caption));
            try
            {
                AgeTransform blank = Of(empty);
                if (blank != null && AgeWidgets.Visible(blank))
                {
                    _cells.Clear();
                    Cells.AddReadout(_cells, blank, Keys + key + "/empty");
                    Cells.Emit(builder, _cells);
                    builder.PopContext();
                    return;
                }

                builder.SetRegion(Keys + key + "/filters");
                builder.PushContext(ModStrings.Get(ModStrings.NegotiationFilters));
                NegotiationTerms.Filters(builder, panel, Keys + key);
                builder.PopContext();

                GraphSheet sheet = new GraphSheet(builder, Keys + key + "/");
                sheet.Region(ModStrings.Get(ModStrings.NegotiationTerms), NegotiationTerms.Columns());
                NegotiationTerms.Shelf(sheet, panel.TermsTable, Keys + key);
                sheet.Finish();
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading a term shelf threw: " + e);
            }

            builder.PopContext();
        }

        // ---- the basket ----

        /// <summary>
        /// The deal as it stands: what both empires would do, what the player would give, and what they
        /// would get (<c>NegotiationContributionPanel.Refresh</c> :77-123). Three regions rather than one
        /// table, because which side a term is on is the whole meaning of the deal.
        ///
        /// The placeholder lines the panel draws in demand mode - a slot the player still has to fill in -
        /// are declared while they are drawn, by visibility, the same as anything else here.
        /// </summary>
        private void BuildContract(GraphBuilder builder, NegotiationModalWindow window)
        {
            NegotiationContributionPanel panel = window.ContributionPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(ContractStop);
            builder.PushContext(ModStrings.Get(ModStrings.NegotiationContract));
            try
            {
                string[] columns = Basket();
                GraphSheet sheet = new GraphSheet(builder, Keys + "contract/");
                Region(sheet, panel.SymmetricalTermsTable, columns, ModStrings.NegotiationContractBoth, "both");
                Region(sheet, panel.MyTermsTable, columns, ModStrings.NegotiationContractMine, "mine");
                Region(sheet, panel.MyPlaceholderTermsTable, columns, ModStrings.NegotiationContractMine, "mine-slots");
                Region(sheet, panel.HisTermsTable, columns, ModStrings.NegotiationContractTheirs, "theirs");
                Region(sheet, panel.HisPlaceholderTermsTable, columns, ModStrings.NegotiationContractTheirs, "theirs-slots");
                sheet.Finish();
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading the deal threw: " + e);
            }

            builder.PopContext();
        }

        private void Region(
            GraphSheet sheet,
            AgeTransform table,
            string[] columns,
            string caption,
            string key
        )
        {
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            sheet.Region(ModStrings.Get(caption), columns);
            NegotiationTerms.Basket(sheet, table, Keys + "contract/" + key, _editor);
        }

        /// <summary>The basket's columns: the shelf's two, plus the stepper a resource term is haggled
        /// with.</summary>
        private static string[] Basket()
        {
            string[] shelf = NegotiationTerms.Columns();
            return new string[]
            {
                shelf[0],
                shelf[1],
                ModStrings.Get(ModStrings.NegotiationQuantity),
            };
        }

        // ---- what the computer thinks ----

        /// <summary>
        /// The deal-approval band, drawn only while the other empire is run by the computer (:666-673).
        ///
        /// What it says is WHERE THE GAUGE STANDS, because that is what the band draws: a bar from
        /// dislike to approval and one lit face out of five. The written reasons the computer sends with
        /// its evaluation are assembled into a local variable and dropped on the floor (:994-1006), so
        /// there is nothing there to read out - and reading it anyway would tell a screen-reader player
        /// something the screen does not say.
        /// </summary>
        private void BuildApproval(GraphBuilder builder, NegotiationModalWindow window)
        {
            AgeTransform group = window.DealApprovalGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.BeginStop(ApprovalStop);
            _cells.Clear();
            try
            {
                NegotiationModalWindow it = window;
                NodeVtable vtable = GraphNodes.Readout(
                    () => ModStrings.Get(ModStrings.NegotiationApproval),
                    () => Approval(it),
                    null,
                    window.DealApprovalTooltip
                );
                AgeWidgets.PointAt(vtable, group);
                Cells.Add(
                    _cells,
                    group,
                    ControlId.Referenced(group, Keys + "approval"),
                    vtable
                );
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading the deal approval threw: " + e);
            }

            Cells.Emit(builder, _cells);
        }

        /// <summary>Where the approval gauge stands, as the number the gauge itself is set from.</summary>
        private static string Approval(NegotiationModalWindow window)
        {
            try
            {
                BiDirectionalGauge gauge = window.DealApprovalGauge;
                if (gauge == null || gauge.MainGauge == null)
                {
                    return null;
                }

                // The bar runs from -1 to 1 and is drawn as a fraction of its track; the track's own
                // percentage is the position the player is looking at.
                float left = gauge.MainGauge.PercentLeft;
                float right = gauge.MainGauge.PercentRight;
                int percent = (int)Math.Round(right > left ? right : left);
                return ModStrings.Format(ModStrings.NegotiationApprovalValue, percent);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the buttons ----

        /// <summary>Reset the deal, ask the computer to fill it in, or make the offer. All three are the
        /// game's own clicks with the game's own refusals: Reset needs a non-empty deal, Suggest terms
        /// needs a computer opponent who has not already answered, and Make an offer has five separate
        /// sentences for the five ways it can be blocked (:1151-1190). Reset has NO confirmation in the
        /// game and gets none here - the mod does not add gates the mouse does not have - so it reads as
        /// what it is and clears the deal on Enter.</summary>
        private void BuildActions(GraphBuilder builder, NegotiationModalWindow window)
        {
            builder.BeginStop(ActionsStop);
            builder.PushContext(ModStrings.Get(ModStrings.NegotiationActions));
            _cells.Clear();
            try
            {
                AddButton(window.ResetButton, "reset");
                AddButton(window.SuggestTermsButton, "suggest");
                // What the offer costs is drawn INSIDE the button (:1191-1200 writes both labels into
                // it), so the button's own caption already carries it and a separate readout would say
                // "Cost : 0 Influence" twice (measured).
                AddButton(window.MakeOfferButton, "offer");
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading the offer buttons threw: " + e);
            }

            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        private void AddButton(AgeTransform widget, string key)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            Func<bool> offered = () => AgeWidgets.Offered(at);
            NodeVtable vtable = GraphNodes.Button(
                () => Named(at, tooltip),
                () => AgeWidgets.Press(at),
                offered,
                tooltip,
                TooltipMode.None
            );
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            AgeWidgets.PointAt(vtable, at);
            Cells.Add(_cells, at, ControlId.Referenced(at, Keys + key), vtable);
        }

        // ---- what the game changes on its own ----

        /// <summary>
        /// The three things about this table the game changes without the player touching anything: what
        /// the computer thinks of the deal (it re-evaluates whenever the basket changes, :1613-1624), what
        /// is IN the basket (Suggest terms fills it in), and what the offer would cost.
        ///
        /// Diffed as one phrase and spoken only on a change, without interrupting - the player is usually
        /// mid-walk of a shelf of two hundred terms when the answer comes back.
        /// </summary>
        private void AnnounceChanges()
        {
            try
            {
                NegotiationModalWindow window = Window();
                if (window == null || window.CurrentContract == null)
                {
                    _watched = null;
                    return;
                }

                MessageBuilder message = new MessageBuilder();
                if (AgeWidgets.Visible(window.DealApprovalGroup))
                {
                    message.ListItem(ModStrings.Get(ModStrings.NegotiationApproval));
                    message.Fragment(Approval(window));
                }

                message.ListItem(Words(window.OfferCostLabel));
                string said = message.Build();
                string terms = Basketed(window);
                string state = said + "|" + terms;
                bool first = _watched == null;
                if (state == _watched)
                {
                    return;
                }

                _watched = state;
                if (!first)
                {
                    Voice.Say(said, false);
                }
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: watching the deal threw: " + e);
                _watched = null;
            }
        }

        /// <summary>How many terms are in the deal, as a cheap fingerprint of the basket - enough to
        /// notice Suggest terms having filled it in without walking the contract every frame.</summary>
        private static string Basketed(NegotiationModalWindow window)
        {
            try
            {
                DiplomaticContract contract = window.CurrentContract;
                return contract == null || contract.Options == null || contract.Options.Count == 0
                    ? "0"
                    : contract.Options[0].Terms.Count.ToString();
            }
            catch (Exception)
            {
                return "?";
            }
        }

        // ---- reading widgets ----

        /// <summary>A control the game draws as a bare icon: what its own tooltip calls it, else what it
        /// draws.</summary>
        private static string Named(AgeTransform widget, AgeTooltip tooltip)
        {
            string drawn = AgeWidgets.TextOf(widget);
            return string.IsNullOrEmpty(drawn) ? CardActions.FirstLine(tooltip) : drawn;
        }

        private static string Words(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !AgeWidgets.Visible(label.AgeTransform)
                    ? null
                    : AgeText.Label(label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Of(AgePrimitiveLabel label)
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

        private static AgeTransform Of(AgePrimitiveImage image)
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

        /// <summary>The heading the window writes over itself. It writes the raw key into the label
        /// (:646), so it is localized here rather than read off the widget.</summary>
        private static string Title(NegotiationModalWindow window)
        {
            try
            {
                return window == null
                    ? null
                    : AgeText.Clean(Gui.Localize("%NegotiationModalWindowTitle"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static NegotiationModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<NegotiationModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
