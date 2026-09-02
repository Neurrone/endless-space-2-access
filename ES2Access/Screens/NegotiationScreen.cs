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
    /// Seven stops, in the order the window draws them: the player's own empire under the window's
    /// heading; the relationship between the two, which is the status and what it permits; the OTHER
    /// empire, with the tick box that opens their dossier and the dossier itself nested under it; the
    /// pressure gauge, with the button that forces a truce during a war; the two shelves of terms, one
    /// per empire; the deal being assembled, ending with what the computer thinks of it; and the buttons
    /// that reset it, ask the computer to fill it in, or make the offer.
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
        private static readonly object TitleStop = "negotiation:title";
        private static readonly object MyEmpireStop = "negotiation:my-empire";
        private static readonly object RelationStop = "negotiation:relationship";
        private static readonly object TheirEmpireStop = "negotiation:their-empire";
        private static readonly object PressureStop = "negotiation:pressure";
        private static readonly object MyTermsStop = "negotiation:my-terms";
        private static readonly object TheirTermsStop = "negotiation:their-terms";
        private static readonly object ContractStop = "negotiation:contract";
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
            get { return ModStrings.ScreenNegotiation; }
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
                    && !WindowShape.Buried(window);
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

            BuildTitle(builder, window);
            BuildMyEmpire(builder, window);
            BuildRelationship(builder, window);
            BuildTheirEmpire(builder, window);
            BuildPressure(builder, window);
            BuildTerms(builder, window, MyTermsStop, window.MyTermsPanel, window.MyAtWarLabel, "mine", ModStrings.NegotiationMyTerms);
            BuildTerms(builder, window, TheirTermsStop, window.HisTermsPanel, window.HisAtWarLabel, "theirs", ModStrings.NegotiationTheirTerms);
            BuildContract(builder, window);
            BuildActions(builder, window);
        }

        // ---- the two empires ----

        /// <summary>
        /// The heading the window writes across its top, as a Tab stop of its own and the first one
        /// (owner ruling 2026-08-27).
        ///
        /// It is a stop rather than a row of the banner's because it belongs to the WINDOW and not to
        /// either empire: riding the player's own banner put the screen's name inside "Your empire",
        /// which is a level it is not under. Its whole reason for existing as a node at all is the
        /// sentence the game hung on it - a screen's spoken name is a phrase with no review buffer
        /// behind it, so an explanation has nowhere else to live (<see cref="Captions.Row"/>).
        /// </summary>
        private void BuildTitle(GraphBuilder builder, NegotiationModalWindow window)
        {
            AgeTransform title = AgeWidgets.Transform(window.WindowTitle);
            if (title == null)
            {
                return;
            }

            builder.BeginStop(TitleStop);
            _cells.Clear();
            _cells.Add(Cells.Readout(title, Explanation(title), Keys + "title"));
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The sentence hung on the heading, wherever the prefab hung it - on the label or on
        /// the title box around it (ES2 facts, "A block caption's WORD and its EXPLANATION sit on
        /// different widgets").</summary>
        private static AgeTooltip Explanation(AgeTransform title)
        {
            AgeTooltip own = AgeWidgets.Raw(title);
            return own ?? AgeWidgets.Raw(title == null ? null : title.Parent);
        }

        /// <summary>
        /// The player's own side of the table: the banner the game draws for this empire - who they are,
        /// the alliance or metaplot team they belong to, and the influence they have to spend on the
        /// deal.
        ///
        /// The stop is NAMED by the banner's own name line and the same line is its first row, which is
        /// the ordinary caption shape: a level the announcer says on the way in, dropped again where the
        /// row below repeats it (<c>GraphAnnouncer.DuplicatesNext</c>).
        ///
        /// The influence stock is a readout though the game made it a button: what its click does is the
        /// god-mode handler, which is not declared anywhere on this window.
        /// </summary>
        private void BuildMyEmpire(GraphBuilder builder, NegotiationModalWindow window)
        {
            NegotiationEmpireBannerPanel panel = window.MyEmpireBanner;
            AgeTransform name = AgeWidgets.Transform(panel == null ? null : panel.EmpireTitleLabel);
            builder.BeginStop(MyEmpireStop);
            bool named = Captions.Push(builder, name);
            _cells.Clear();
            try
            {
                AddBanner(panel, "my");
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading your own banner threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            if (_cells.Count > 0)
            {
                builder.LandStopOn(_cells[0].Id);
            }

            Captions.Pop(builder, named);
        }

        /// <summary>The strip between the two banners: what the diplomatic status IS, and the three
        /// things that status permits. The game draws the status as words and the permissions as bare
        /// icons and captions the band with nothing, so the mod's word names it.</summary>
        private void BuildRelationship(GraphBuilder builder, NegotiationModalWindow window)
        {
            builder.BeginStop(RelationStop);
            builder.PushContext(ModStrings.Get(ModStrings.NegotiationRelationship));
            _cells.Clear();
            try
            {
                AddStatus(window);
                AddAbilities(window);
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading the relationship threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>
        /// The other empire: their banner, how they feel about the player and why, the tick box that
        /// opens their dossier, and - while it is ticked - the dossier itself, nested here rather than
        /// standing as a Tab stop of its own, because it is a panel this box opens and closes.
        /// </summary>
        private void BuildTheirEmpire(GraphBuilder builder, NegotiationModalWindow window)
        {
            NegotiationEmpireBannerPanel panel = window.HisEmpireBanner;
            AgeTransform name = AgeWidgets.Transform(panel == null ? null : panel.EmpireTitleLabel);
            builder.BeginStop(TheirEmpireStop);
            bool named = Captions.Push(builder, name);
            _cells.Clear();
            try
            {
                AddAlliance(panel, "his");
                AddStock(panel, "his");
                AddAttitude(panel);
                // The banner first, then the box: the game draws the box in the window's top corner,
                // clear of both banners, so laying the two out by rectangle would put the box ahead of
                // the empire it is about and leave the dossier it opens hanging under the attitude.
                Cells.EmitLinear(builder, _cells);
                _cells.Clear();
                AddInfoToggle(window);
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading their banner threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            BuildDossier(builder, window);
            Captions.Pop(builder, named);
        }

        /// <summary>The status line and the empire it is about, as the one sentence the eye reads across
        /// the top of the window ("In cold war with Leaper (AI) (Cravers)") - the words are drawn on two
        /// widgets a banner apart, and reading them as two rows makes the player assemble the sentence
        /// themselves. It stands on the status LABEL, which is the half that carries the meaning, and
        /// carries the emblem's tooltip, which is the only place the game says what the status lets each
        /// side do.</summary>
        private void AddStatus(NegotiationModalWindow window)
        {
            AgeTransform label = AgeWidgets.Transform(window.DiplomaticStatusLabel);
            if (label == null)
            {
                return;
            }

            AgeTransform icon = AgeWidgets.Transform(window.DiplomaticStatusIcon);
            AgeTooltip tooltip = AgeWidgets.Raw(icon);
            NegotiationModalWindow it = window;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Relation(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, icon ?? label, tooltip);
            Cells.Add(_cells, label, ControlId.For(label, Keys + "status"), vtable);
        }

        /// <summary>The sentence the two widgets make between them.</summary>
        private static string Relation(NegotiationModalWindow window)
        {
            MessageBuilder message = new MessageBuilder();
            message.Fragment(Words(window.DiplomaticStatusLabel));
            message.Fragment(
                window.HisEmpireBanner == null
                    ? null
                    : Words(window.HisEmpireBanner.EmpireTitleLabel)
            );
            return message.Build();
        }

        private void AddInfoToggle(NegotiationModalWindow window)
        {
            AgeControlToggle toggle = window.EmpireInfoToggle;
            AgeTransform at = AgeWidgets.Transform(toggle);
            if (at == null)
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
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, at);
            Cells.Add(_cells, at, ControlId.For(at, Keys + "empire-info"), vtable);
        }

        /// <summary>One empire's banner, a drawn line at a time: their name, the alliance or metaplot
        /// team the game names beside it, and the influence they are bringing to the table. Each is its
        /// own row because each is a separate fact drawn in a separate place - the one line they used to
        /// be made the player sit through all of it to hear any of it.</summary>
        private void AddBanner(NegotiationEmpireBannerPanel panel, string key)
        {
            if (panel == null)
            {
                return;
            }

            Cells.AddReadout(_cells, AgeWidgets.Transform(panel.EmpireTitleLabel), Keys + key + "-name");
            AddAlliance(panel, key);
            AddStock(panel, key);
        }

        private void AddAlliance(NegotiationEmpireBannerPanel panel, string key)
        {
            AgeTransform at = AgeWidgets.Transform(panel == null ? null : panel.AllianceNameLabel);
            if (at != null)
            {
                Cells.AddReadout(_cells, at, Keys + key + "-alliance");
            }
        }

        private void AddStock(NegotiationEmpireBannerPanel panel, string key)
        {
            AgeTransform at = AgeWidgets.Transform(panel == null ? null : panel.InfluenceStockLabel);
            if (at != null)
            {
                Cells.AddReadout(_cells, at, Keys + key + "-influence");
            }
        }

        /// <summary>How the other empire feels about the player, with the annotations the game hangs off
        /// the label as the reason - the one place it says WHY.</summary>
        private void AddAttitude(NegotiationEmpireBannerPanel panel)
        {
            AgeTransform at = AgeWidgets.Transform(panel == null ? null : panel.AttitudeLabel);
            if (at != null)
            {
                _cells.Add(Cells.Readout(at, AgeWidgets.Raw(at), Keys + "attitude"));
            }
        }

        /// <summary>A figure the game draws as a bare icon and names only on its tooltip: each of the
        /// abilities in the permissions strip. The words are the tooltip's own sentence where it carries
        /// one, and otherwise the name of the thing behind it - an ability's dossier is
        /// renderer-assembled, so its title is where the name lives (measured: an ability item read by
        /// the sentence alone came out with no name at all).</summary>
        private static NodeVtable IconOnly(AgeTransform at)
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
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, at);
            return vtable;
        }

        /// <summary>The strip of diplomatic abilities the status carries, or the game's own label for
        /// having none. Unlike the ring's leader card, the abilities HERE keep their tooltips (:823), so
        /// each is its own readout named by the sentence on it.</summary>
        private void AddAbilities(NegotiationModalWindow window)
        {
            AgeTransform none = window.NoDiplomaticAbilitiesLabel;
            // Flow control: which branch is built - the empty-state label is a wired prefab field,
            // always non-null; its VISIBILITY is what says the empty state is the drawn one.
            // Without this the "no abilities" line stood in for the strip of abilities the status
            // was drawing.
            if (none != null && AgeWidgets.Visible(none))
            {
                Cells.AddReadout(_cells, none, Keys + "no-abilities");
                return;
            }

            IList<AgeTransform> children = AgeWidgets.DrawnChildren(
                window.DiplomaticAbilitiesTable
            );
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Pooled by ReserveChildren/RefreshChildrenIList like every table on this window, and
                // that pair parks a surplus item at alpha 0 WITHOUT unbinding it - so a permission the
                // current status no longer carries is a faded item still wearing the last status's
                // tooltip. Only drawn-ness separates the two (<see cref="AgeWidgets.DrawnChild"/>).
                AgeTransform at = AgeWidgets.DrawnChild(children, i);
                if (at == null || AgeWidgets.Raw(at) == null)
                {
                    continue;
                }

                Cells.Add(
                    _cells,
                    at,
                    ControlId.For(at, Keys + "ability/" + i),
                    IconOnly(at)
                );
            }
        }

        // ---- the dossier ----

        /// <summary>The sheet of prose about the other empire, while the tick box has it open - the same
        /// panel the introduction popup opens, read by the same shared reader
        /// (<see cref="EmpireDossier"/>). Declared inside the OTHER EMPIRE's stop, immediately below the
        /// box that opens it: it is a panel this box shows and hides, and a Tab stop that comes and goes
        /// as a checkbox is ticked moves the whole screen under the player.</summary>
        private void BuildDossier(GraphBuilder builder, NegotiationModalWindow window)
        {
            NegotiationEmpireInfoPanel panel = window.EmpireInfoPanel;
            if (!EmpireDossier.Open(panel))
            {
                return;
            }

            builder.PushContext(ModStrings.Get(ModStrings.NegotiationDossier));
            EmpireDossier.Build(builder, panel, Keys + "dossier/", DossierRegion);
            builder.PopContext();
        }

        // ---- pressure ----

        /// <summary>
        /// The pressure the player has built up against this empire, or - at war - how exhausted the war
        /// has left them. Both are the same gauge under two titles.
        ///
        /// What the gauge SHOWS is where the split between the two empires sits, and the game writes that
        /// nowhere in words: it is the length of the bar's left half (<c>DualityGauge.Refresh</c>, driven
        /// from <c>GetDiplomaticGaugeLevelWith</c> at :828-838), so the two shares are read off the track
        /// and named by the banners they are drawn under. The per-turn TREND is the second line of the
        /// gauge's own tooltip (<c>GetPressureTooltipContent</c> :916-937) and is spoken after the
        /// position, which is the order the eye takes them in. The markers along the bar are the
        /// thresholds a demand or a forced truce needs, each with the game's own sentence about whether it
        /// has been reached.
        ///
        /// At war the band also draws the button that forces a truce; it is left drawn while refusing,
        /// with the game's failure appended to its own description (:886-895), and its Enter raises the
        /// game's own confirmation before any order is posted.
        /// </summary>
        private void BuildPressure(GraphBuilder builder, NegotiationModalWindow window)
        {
            builder.BeginStop(PressureStop);

            // The game titles this band itself, and the title is one of two words depending on whether
            // there is a war on - so the band is named by whichever the game drew and the mod's word is
            // only the fallback. The title stands in the band as a row only if it explains itself
            // (Captions), which is the shared rule for every caption over a block.
            AgeTransform title = AgeWidgets.Transform(window.PressureGroupTitle);
            bool named = Captions.Push(
                builder,
                title,
                Keys + "pressure-title",
                Captions.Text(title) ?? ModStrings.Get(ModStrings.NegotiationPressure)
            );
            _cells.Clear();
            try
            {
                AddGauge(window);
                AddThresholds(window);
                AddTruce(window);
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading the pressure band threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            Captions.Pop(builder, named);
        }

        /// <summary>The bar itself: where the split between the two empires stands, then which way it is
        /// moving.
        ///
        /// Its tooltip is not declared as a surface of its own, which is the one place on this screen a
        /// drawn tooltip is not: both of its lines are already spoken HERE. The first is the description
        /// the game also hung on the band's title, where it is declared and reviewable, and the second IS
        /// the trend this node reads as its value - so a section would put the same two lines in the same
        /// buffer twice. The pointer is still aimed at it, so the words the player sees on hover are the
        /// words they hear.</summary>
        private void AddGauge(NegotiationModalWindow window)
        {
            AgeTransform gauge = window.PressureGauge == null
                ? null
                : window.PressureGauge.AgeTransform;
            if (gauge == null)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(gauge);
            NegotiationModalWindow it = window;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Split(it)),
                },
                // The gauge's own tooltip, declared rather than mined for its last line: it is plain
                // text the game wrote, so it is said whole - the description the band's title shares
                // and the per-turn trend written under it.
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, gauge, tooltip);
            Cells.Add(_cells, gauge, ControlId.For(gauge, Keys + "pressure-gauge"), vtable);
        }

        /// <summary>How the bar is split between the two empires, each share named by the banner it is
        /// drawn under. The left half is the watching empire's (<c>DiplomacyScreen.GetPressureColor</c>
        /// tints it as the positive side) and the rest of the track is the other empire's.</summary>
        private static string Split(NegotiationModalWindow window)
        {
            try
            {
                DualityGauge gauge = window.PressureGauge;
                AgeTransform mine = gauge == null ? null : gauge.LeftGauge;
                if (mine == null)
                {
                    return null;
                }

                int ours = (int)Math.Round(mine.PercentRight);
                MessageBuilder message = new MessageBuilder();
                message.ListItem(Share(window.MyEmpireBanner, ours));
                message.ListItem(Share(window.HisEmpireBanner, 100 - ours));
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Share(NegotiationEmpireBannerPanel panel, int percent)
        {
            string name = panel == null ? null : Words(panel.EmpireTitleLabel);
            return string.IsNullOrEmpty(name)
                ? null
                : ModStrings.Format(ModStrings.NegotiationPressureShare, name, percent);
        }

        /// <summary>
        /// WHERE a threshold marker sits along the bar, on the same 0-to-100 track the gauge's own two
        /// shares are read off - so "at 80 percent" is a place the player can compare their 42 against.
        ///
        /// The game puts the marker there by writing the number straight onto the widget
        /// (<c>GaugeThresholdItem.Bind</c> :29 sets <c>PercentLeft</c> from
        /// <c>GaugeThresholdData.Value</c>, which is the threshold rescaled onto the track), so this is
        /// the game's own figure rather than the rectangle measured back out of it. Checked against the
        /// drawing anyway, 2026-08-27: the two markers read 80 and 100, and their rectangles sit at
        /// x 668 and 828 on a track from x 40 to x 840 - 80% and 100% of it.
        ///
        /// Without it the two markers were two sentences and a refusal, with nothing to tell them apart
        /// or to measure the bar against (owner-reported).
        /// </summary>
        private static string Notch(AgeTransform widget)
        {
            try
            {
                return ModStrings.Format(
                    ModStrings.NegotiationThresholdAt,
                    (int)Math.Round(widget.PercentLeft)
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void AddThresholds(NegotiationModalWindow window)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(window.PressureThresholdTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Same pool, same retirement: the band is rebuilt with a different number of markers
                // when a war starts or ends (:844-848 reserves truce thresholds, :878-881 demand ones),
                // and the surplus is parked at alpha 0 with its old tooltip still on it.
                AgeTransform at = AgeWidgets.DrawnChild(children, i);
                if (at == null)
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
                AgeTransform notch = at;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        // The PLACE first and the sentence after it (owner ruling 2026-08-27): the two
                        // markers say much the same thing at length and differ only in where they sit,
                        // so a player comparing them against the bar hears the number that separates
                        // them before the paragraph they share.
                        //
                        // The sentence stays a LABEL part rather than becoming a value: a label is
                        // spoken and deliberately left OUT of the review buffer (NodeBuffer), and the
                        // tooltip section below is already the place those same words are reviewed. Made
                        // a value, it read out twice on the buffer key.
                        GraphNodes.LabelPart(() => Notch(notch)),
                        GraphNodes.LabelPart(CardActions.NameFromTooltip(tooltip)),
                        GraphNodes.DisabledPart(() => AgeWidgets.Enabled(at)),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                };
                AgeWidgets.PointAt(vtable, at);
                Cells.Add(
                    _cells,
                    at,
                    ControlId.For(at, Keys + "threshold/" + i),
                    vtable
                );
            }
        }

        private void AddTruce(NegotiationModalWindow window)
        {
            AgeTransform at = window.TruceButton;
            if (at == null)
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
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            AgeWidgets.PointAt(vtable, at);
            Cells.Add(_cells, at, ControlId.For(at, Keys + "truce"), vtable);
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
            // Flow control: the shelf below is walked term by term.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(stop);
            // The game captions each shelf itself ("My terms", "Their terms") and hangs the sentence
            // saying what it holds on that caption, so the shelf is named by what the game drew and the
            // mod's word is only the fallback. The caption stands as the stop's first row because it
            // explains itself, which is the shared rule for every caption over a block (Captions).
            AgeTransform title = AgeWidgets.ChildNamed(panel.AgeTransform, "Title", 1);
            bool named = Captions.Push(
                builder,
                title,
                Keys + key + "/title",
                Captions.Text(title) ?? ModStrings.Get(caption)
            );
            try
            {
                AgeTransform blank = AgeWidgets.Transform(empty);
                // Flow control: which branch is built - the empty-state label is a wired prefab
                // field, always non-null; its VISIBILITY is what says the empty state is the drawn
                // one. Without this the at-war sentence stood in for a shelf the game was drawing
                // in full, and the filters, the terms and the basket they fill were never declared.
                if (blank != null && AgeWidgets.Visible(blank))
                {
                    _cells.Clear();
                    Cells.AddReadout(_cells, blank, Keys + key + "/empty");
                    Cells.EmitLinear(builder, _cells);
                    Captions.Pop(builder, named);
                    return;
                }

                builder.SetRegion(Keys + key + "/filters");
                builder.PushContext(ModStrings.Get(ModStrings.ShipDesignFilters));
                NegotiationTerms.Filters(builder, panel, Keys + key);
                builder.PopContext();

                GraphSheet sheet = new GraphSheet(builder, Keys + key + "/");
                sheet.Region(ModStrings.Get(ModStrings.NegotiationTerms), NegotiationTerms.Columns());
                NegotiationTerms.Headers(builder, panel, Keys + key);
                NegotiationTerms.Shelf(sheet, panel.TermsTable, Keys + key, window);
                sheet.Finish();
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading a term shelf threw: " + e);
            }

            Captions.Pop(builder, named);
        }

        // ---- the basket ----

        /// <summary>
        /// The deal as it stands: what both empires would do, what the player would give, and what they
        /// would get (<c>NegotiationContributionPanel.Refresh</c> :77-123), and last of all what the
        /// computer makes of the whole thing. Three regions rather than one table, because which side a
        /// term is on is the whole meaning of the deal.
        ///
        /// <b>Declared even when the deal is empty</b>, which is how it opens: the game draws the band's
        /// caption and both contribution captions over the empty tables, so there is something on screen
        /// to declare - and a Tab stop that only exists once the player has put something in it is a stop
        /// they cannot find their way back to. The two contribution captions carry the game's own sentence
        /// about which side is which, so each is a row of its region as well as its name.
        ///
        /// The placeholder lines the panel draws in demand mode - a slot the player still has to fill in -
        /// are declared while they are drawn, by visibility, the same as anything else here, and sit in
        /// the region of the side that would fill them.
        /// </summary>
        private void BuildContract(GraphBuilder builder, NegotiationModalWindow window)
        {
            NegotiationContributionPanel panel = window.ContributionPanel;
            // Flow control: the contract basket below is walked term by term.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            AgeTransform root = panel.AgeTransform;
            AgeTransform title = AgeWidgets.ChildNamed(root, "Title", 1);
            builder.BeginStop(ContractStop);
            bool named = Captions.Push(
                builder,
                title,
                Keys + "contract/title",
                Captions.Text(title) ?? ModStrings.Get(ModStrings.NegotiationContract)
            );
            try
            {
                string[] columns = Basket();
                GraphSheet sheet = new GraphSheet(builder, Keys + "contract/");
                sheet.Region(ModStrings.Get(ModStrings.NegotiationContractBoth), columns);
                Rows(sheet, window, panel.SymmetricalTermsTable, "both");

                Side(sheet, root, "MyContributionGroup", ModStrings.NegotiationContractMine, columns);
                Rows(sheet, window, panel.MyTermsTable, "mine");
                Rows(sheet, window, panel.MyPlaceholderTermsTable, "mine-slots");

                Side(sheet, root, "HisContributionGroup", ModStrings.NegotiationContractTheirs, columns);
                Rows(sheet, window, panel.HisTermsTable, "theirs");
                Rows(sheet, window, panel.HisPlaceholderTermsTable, "theirs-slots");

                AddApproval(sheet, window);
                sheet.Finish();
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: reading the deal threw: " + e);
            }

            Captions.Pop(builder, named);
        }

        /// <summary>One side of the deal, opened under the caption the game draws over it. The caption is
        /// also the region's first LINE, because the game hung its own sentence on it saying which way the
        /// terms below go - and a sheet line rather than a loose row, so the rows above and below it stay
        /// on one up-and-down chain.</summary>
        private void Side(
            GraphSheet sheet,
            AgeTransform root,
            string group,
            string fallback,
            string[] columns
        )
        {
            AgeTransform box = AgeWidgets.ChildNamed(root, group, 1);
            AgeTransform caption = box == null ? null : AgeWidgets.ChildNamed(box, "Title", 1);
            sheet.Region(Captions.Text(caption) ?? ModStrings.Get(fallback), columns);
            if (caption == null || !AgeWidgets.Draws(AgeWidgets.Raw(caption)))
            {
                return;
            }

            Cell cell = Cells.Readout(caption, AgeWidgets.Raw(caption), Keys + "contract/" + group);
            sheet.Line(cell.Vtable, caption);
        }

        private void Rows(
            GraphSheet sheet,
            NegotiationModalWindow window,
            AgeTransform table,
            string key
        )
        {
            // Flow control: the basket column below is walked term by term.
            if (table != null && AgeWidgets.Visible(table))
            {
                NegotiationTerms.Basket(sheet, table, Keys + "contract/" + key, _editor, window);
            }
        }

        /// <summary>The basket's columns: the shelf's three - name, type, cost - plus the stepper a
        /// resource term is haggled with.</summary>
        private static string[] Basket()
        {
            string[] shelf = NegotiationTerms.Columns();
            return new string[]
            {
                shelf[0],
                shelf[1],
                shelf[2],
                ModStrings.Get(ModStrings.NegotiationQuantity),
            };
        }

        // ---- what the computer thinks ----

        /// <summary>
        /// The deal-approval band, drawn only while the other empire is run by the computer (:666-673) -
        /// the LAST line of the deal, which is where the game draws it and what it is about.
        ///
        /// What it says is WHERE THE GAUGE STANDS, because that is what the band draws. The written
        /// reasons the computer sends with its evaluation are assembled into a local variable and dropped
        /// on the floor (:994-1006), so there is nothing there to read out - and reading it anyway would
        /// tell a screen-reader player something the screen does not say. Neither is the row of five faces
        /// beside the bar read as words: measured 2026-08-27, the five are bare images with no label and
        /// no tooltip between them, so the game has no name for any of them and this mod does not invent
        /// five.
        /// </summary>
        private void AddApproval(GraphSheet sheet, NegotiationModalWindow window)
        {
            AgeTransform group = window.DealApprovalGroup;
            if (group == null)
            {
                return;
            }

            NegotiationModalWindow it = window;
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.NegotiationApproval),
                () => Face(it),
                null,
                window.DealApprovalTooltip
            );
            AgeWidgets.PointAt(vtable, group);
            sheet.Region(null);
            sheet.Line(vtable, group);
        }

        /// <summary>
        /// WHICH FACE the band has lit, as a place among the five it draws.
        ///
        /// The band is a scale of five faces with a bar running out from its centre, and the thing the
        /// game decides and paints is the FACE: <c>RefreshDealApproval</c> :985-992 turns the evaluation
        /// into a bracket 0..4 and lights that one, leaving the other four at their faded alpha. The bar's
        /// exact reach inside a bracket is a nuance the game names nowhere.
        ///
        /// So the percentage is no longer what this says (owner-reported 2026-08-27: "it reads -35%
        /// approval - this doesn't sound right"), and measurement bears him out: at evaluation -0.348 the
        /// bar reaches a third of the way to the left, and the face the game lights is the MIDDLE one.
        /// A player told "-35 percent" hears something far worse than what is drawn.
        ///
        /// Read off the ALPHA the game wrote rather than recomputed from the evaluation, so this says the
        /// face that is actually lit; the fade factor is the widget's own, not a constant.
        ///
        /// <b>The place is said with the shared position words and no scale word of the mod's own.</b>
        /// Swept 2026-08-27: the game has no name for any of the five - no <c>GuiElement</c> among 10,446
        /// matches a smiley, the string "smiley" appears nowhere in any of the ten localizations, and
        /// <c>%NegotiationModalWindowDealApprovalDescription</c> is the only DealApproval key in the game.
        /// Naming them would be five words this mod invented, so the words are the owner's to supply and
        /// until he does the position stands in.
        /// </summary>
        private static string Face(NegotiationModalWindow window)
        {
            try
            {
                AgeTransform[] faces = window.AIEvaluationSmileys;
                if (faces == null || faces.Length == 0)
                {
                    return null;
                }

                for (int i = 0; i < faces.Length; i++)
                {
                    AgeTransform face = faces[i];
                    // Content: which face reads as the lit one. Alpha is what the game writes to say so.
                    if (face != null && face.Alpha > face.FadeOnDisableFactor + 0.001f)
                    {
                        // The mod's one "N of M" idiom rather than the template by hand, so the lit
                        // face reads the way every other position in a set does.
                        return new MessageBuilder().PushFraction(i + 1, faces.Length).Build();
                    }
                }

                // Nothing lit: the game draws no opinion at all - an empty contract, or a one-sided
                // Declaration, which it refuses to evaluate on screen however strongly it feels
                // (:980 sends both of those to StopAIEvaluationFeedback).
                return ModStrings.Get(ModStrings.NavCellEmpty);
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

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
            WindowShape.Close(builder, window, Keys);
        }

        private void AddButton(AgeTransform widget, string key)
        {
            if (widget == null)
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
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            AgeWidgets.PointAt(vtable, at);
            Cells.Add(_cells, at, ControlId.For(at, Keys + key), vtable);
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
                // Content: whether the approval fragment joins a spoken sentence. No node here.
                if (AgeWidgets.Visible(window.DealApprovalGroup))
                {
                    message.ListItem(ModStrings.Get(ModStrings.NegotiationApproval));
                    message.Fragment(Face(window));
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

        /// <summary>What a control is called: the words it draws, else the title the game gave its
        /// tooltip, else that tooltip's own first line (<see cref="CardActions.FirstLine"/>, which is
        /// where the words-are-really-on-the-widget test lives).</summary>
        private static string Named(AgeTransform widget, AgeTooltip tooltip)
        {
            string drawn = AgeWidgets.TextOf(widget);
            return string.IsNullOrEmpty(drawn)
                ? AgeWidgets.TooltipTitle(tooltip) ?? CardActions.FirstLine(tooltip)
                : drawn;
        }

        private static string Words(AgePrimitiveLabel label)
        {
            try
            {
                return AgeWidgets.DrawnLabel(label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The heading the window writes over itself, read off the label the game drew it in -
        /// with the localization of its own key as the fallback, because the window writes the raw key
        /// into the label (:646) and a build that never refreshed it would read back the key.</summary>
        private static string Title(NegotiationModalWindow window)
        {
            try
            {
                if (window == null)
                {
                    return null;
                }

                string drawn = AgeWidgets.TextOf(AgeWidgets.Transform(window.WindowTitle));
                return string.IsNullOrEmpty(drawn) || drawn.StartsWith("%")
                    ? AgeText.Title("%NegotiationModalWindowTitle")
                    : drawn;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static NegotiationModalWindow Window()
        {
            return GameWindows.Of<NegotiationModalWindow>();
        }
    }
}
