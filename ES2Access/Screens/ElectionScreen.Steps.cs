using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The three steps the modal walks through: picking a party and an action, the vote
    /// breakdown system by system, and the result.</summary>
    public sealed partial class ElectionScreen
    {
        // ---- step 0: pick a party, pick an action ----

        private void BuildBefore(GraphBuilder builder, ElectionBeforePanel panel)
        {
            builder.BeginStop(BeforeCandidatesStop);
            _cells.Clear();
            AddPanelTitle(_cells, panel);
            int firstCard = _cells.Count;
            AddCandidates(_cells, panel);
            Cells.EmitLinear(builder, _cells);

            // The cards, not the question above them: the question is what arriving announces, and
            // the selected card's own Selected part then refines the landing onto the party the game
            // has already picked for the player (ElectionBeforePanel.cs:203-237).
            if (_cells.Count > firstCard)
            {
                builder.SetStart(_cells[firstCard].Id);
            }

            builder.BeginStop(BeforeActionsStop);
            _cells.Clear();
            // Flow control: the caption and every action toggle under it are read one by one.
            if (AgeWidgets.Visible(panel.ElectionActionsGroup))
            {
                // A caption the game draws over SEVERAL controls is a node of its own.
                AddReadout(
                    _cells,
                    AgeWidgets.Transform(panel.ElectionActionTitle),
                    "election:action-caption",
                    Raw(panel.ElectionActionTitle)
                );
                AddElectionActions(_cells, panel);
            }

            Cells.EmitLinear(builder, _cells);

            builder.BeginStop(BeforeLawsStop);
            _cells.Clear();
            // Flow control: the show/hide pair and the card table below are each walked inside it.
            if (AgeWidgets.Visible(panel.PoliticsLawsGroup))
            {
                // Exactly one of the two is drawn (ElectionBeforePanel.cs:341-346), so declaring both
                // by visibility is what puts "Show ... laws" or "Hide" on the page and never both.
                AddButton(_cells, AgeWidgets.Transform(panel.ShowPoliticsLawsButton), "show-laws");
                AddButton(_cells, AgeWidgets.Transform(panel.HidePoliticsLawsButton), "hide-laws");
                // Flow control: whether the card table is walked at all - the scroll view is what the
                // Hide button collapses, and the cards inside it stay drawn under a collapsed one.
                if (AgeWidgets.Visible(AgeWidgets.Transform(panel.PoliticsLawsScrollView)))
                {
                    AddLawCards(_cells, panel.PoliticsLawsTable, "election:before/law");
                }
            }

            Cells.EmitLinear(builder, _cells);

            builder.BeginStop(BeforeResourcesStop);
            _cells.Clear();
            // Flow control: the four readouts under it are each read in turn.
            if (AgeWidgets.Visible(panel.EmpireResourcesGroup))
            {
                AddReadout(
                    _cells,
                    AgeWidgets.Transform(panel.EmpireMoneyLabel),
                    "election:money",
                    Raw(panel.EmpireMoneyLabel)
                );
                AddReadout(
                    _cells,
                    AgeWidgets.Transform(panel.EmpireInfluenceLabel),
                    "election:influence",
                    Raw(panel.EmpireInfluenceLabel)
                );
            }

            Cells.EmitLinear(builder, _cells);
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
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                CandidateCard card = Component<CandidateCard>(child);
                // Banding input: the candidate cells are laid into rows by their rectangles, and
                // Cells.Add takes them without asking the gate.
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
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(panel.ElectionActionTogglesTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                ElectionActionToggle item = Component<ElectionActionToggle>(child);
                // Banding input: same table, same reason - the action cells are banded by rectangle.
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
            LocalCounts counts = Counts(panel);

            builder.BeginStop(LocalSupportStop);
            builder.SetRegion(LocalTitleRegion);
            _cells.Clear();
            AddPanelTitle(_cells, panel);
            Cells.EmitLinear(builder, _cells);

            // The bars under the caption the prefab draws over them. The caption is the game's own
            // word for the column and nothing else on the page says it, so it is the level the bars
            // are announced under; a caption this hunt does not find pushes nothing rather than an
            // empty one.
            builder.SetRegion(TrendsRegion);
            bool named = Trends(builder, panel);
            _cells.Clear();
            AddSupportGauges(_cells, panel, counts);
            Cells.EmitLinear(builder, _cells);
            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(LocalEmpireRegion);
            _cells.Clear();
            // The empire's running total is a bare "37" on its own label: the words for it are the two
            // captions the prefab draws BESIDE it in the same group - "Overall Empire" above and "Total
            // representatives" below (measured on the prefab; neither the label nor the group carries a
            // tooltip, and %TotalElectorsTitle appears nowhere in the game's code). So the group is the
            // line, which is also what puts its header on the page - nothing else here reads it.
            AgeTransform total = AgeWidgets.Transform(panel.TotalElectorsValue);
            AgeTransform box = total == null ? null : total.Parent;
            AddReadout(
                _cells,
                box ?? total,
                "election:total-electors",
                Raw(box) ?? Raw(panel.TotalElectorsValue)
            );
            AddCountingProgress(_cells, panel, counts);
            Cells.EmitLinear(builder, _cells);
            builder.SetRegion(null);

            builder.BeginStop(LocalSystemStop);
            _cells.Clear();
            AddButton(_cells, panel.PreviousSystemButton, "previous-system");
            AddButton(_cells, panel.NextSystemButton, "next-system");
            Cells.EmitLinear(builder, _cells);

            // The system and its representatives as ONE row, declared rather than derived: the game
            // wraps the representative icons onto a second line as soon as there are three of them
            // (measured - two at y=740, the third at y=760), and a row taken from those rectangles
            // splits the parties of one system across two lines of navigation. Which line an icon
            // happens to wrap onto is not a fact about the system.
            _cells.Clear();
            ControlId system = AddSystemName(_cells, panel);
            AddRepresentatives(_cells, panel);
            Cells.EmitRow(builder, _cells);

            // The system being counted, not the panel's question above it: the question is what
            // arriving announced, and this row is what the whole step is about.
            if (system != null)
            {
                builder.SetStart(system);
            }
        }

        /// <summary>The system the carousel is showing and where that is in the round - the head of the
        /// system's row. The drawn index is spoken because the game draws it and nothing else here says
        /// it: the systems are not graph siblings, so no position is stamped for them.</summary>
        private static ControlId AddSystemName(List<Cell> cells, ElectionLocalPanel panel)
        {
            AgeTransform name = AgeWidgets.Drawn(panel.StarSystemNameLabel);
            if (name == null)
            {
                return null;
            }

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
            Cells.Add(cells, name, id, vtable);
            return id;
        }

        /// <summary>
        /// How the current system's votes are split - one node per party with any, the node being an
        /// icon and a count (<c>ElectionLocalPanel.cs:258-264</c>,
        /// <c>SystemRepresentativeItem.cs:9-20</c>). The party is named nowhere on the row, only on the
        /// wrapper the game hangs on its tooltip, which is the case
        /// <see cref="AgeWidgets.TooltipTitle"/> exists for, and its dossier is that tooltip's.
        ///
        /// Declared in the table's own order - which is the order the game bound them in, not the order
        /// it wrapped them onto lines.
        /// </summary>
        private static void AddRepresentatives(List<Cell> cells, ElectionLocalPanel panel)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(panel.SystemRepresentativeTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                SystemRepresentativeItem item = Component<SystemRepresentativeItem>(child);
                // A pooled table retires a surplus item by parking it at alpha 0 with Visible still
                // true, and the parked item keeps the previous binding's wrapper and count. The cell
                // carries the item, so that one is taken out with the rest (<see cref="Cells"/>).
                if (item == null)
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

        /// <summary>
        /// The cumulative support bars - the "Political Trends" column down the right of the step.
        ///
        /// The panel binds these with a party icon and a fill ratio and no words at all
        /// (<c>ElectionLocalPanel.cs:296-308</c>) and never touches their tooltips, so the widget itself
        /// says nothing whatever about which party a bar is for. It is recovered from the list the bars
        /// were bound FROM: <c>ReserveChildren</c>/<c>RefreshChildrenIList</c> (:208-209) walk that list
        /// in order, so bar <c>i</c> is entry <c>i</c> of the shown system's
        /// <c>PoliticsWithLocalScoresAndCumulatedScores</c> - private, and read by reflection in
        /// <see cref="Counts"/>.
        ///
        /// What the bar draws is the party's cumulated share of the empire's representatives, so that is
        /// what it says: the count through the shown system, of the empire's total. Visibility is the
        /// senate's own available-parties filter (:306), so a party the government does not offer never
        /// appears rather than reading as a zero.
        /// </summary>
        private static void AddSupportGauges(
            List<Cell> cells,
            ElectionLocalPanel panel,
            LocalCounts counts
        )
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(panel.PoliticsCumulativeSupportGaugesTable);
            IList<KeyValuePair<PoliticsDefinition, int[]>> parties = counts.Parties;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                // Drawn-ness is not asked here: the cell carries the bar, and a retired one is taken
                // out before the cells are banded (<see cref="Cells"/>).
                if (
                    Component<PoliticsCumulativeSupportGauge>(child) == null
                    || parties == null
                    || i >= parties.Count
                )
                {
                    continue;
                }

                KeyValuePair<PoliticsDefinition, int[]> party = parties[i];
                if (party.Key == null || party.Value == null || party.Value.Length < 2)
                {
                    continue;
                }

                string label = AgeText.Clean(Gui.GetLocalizedTitle(party.Key.Name));
                string share = new MessageBuilder()
                    .PushFraction(party.Value[1], counts.Total)
                    .Build();
                NodeVtable vtable = GraphNodes.Readout(
                    () => label,
                    () => share,
                    null,
                    AgeWidgets.Raw(child)
                );
                AgeWidgets.PointAt(vtable, child);
                Cells.Add(cells, child, ControlId.Structural("election:support/" + i), vtable);
            }
        }

        /// <summary>
        /// How far the count has got, as a sentence.
        ///
        /// The game draws it as a bare three-segment bar - what was counted before this system, what
        /// this system adds, what is left (<c>ElectionLocalPanel.cs:239-250</c>) - with no words on or
        /// beside it, so the two numbers behind it are the mod's own phrasing over the game's figures.
        /// It is declared where the game DRAWS it, inside the Overall Empire box in the trends column,
        /// rather than beside the carousel it advances with.
        /// </summary>
        private static void AddCountingProgress(
            List<Cell> cells,
            ElectionLocalPanel panel,
            LocalCounts counts
        )
        {
            AgeTransform gauge = panel.SystemRepresentativesGauge;
            AgeTransform bar = gauge == null ? null : gauge.Parent;
            // Banding input: Cells.Add takes the bar without asking the gate, and its rectangle is what
            // keeps the counted line off the rows of party figures beside it.
            if (bar == null || !AgeWidgets.Visible(bar) || counts.Total <= 0)
            {
                return;
            }

            string said = ModStrings.Format(
                ModStrings.ElectionRepresentativesCounted,
                counts.Counted,
                counts.Total
            );
            NodeVtable vtable = GraphNodes.Readout(() => said, () => null, null, Raw(bar));
            AgeWidgets.PointAt(vtable, bar);
            Cells.Add(cells, bar, ControlId.Structural("election:counted"), vtable);
        }

        // ---- step 2: the result ----

        private void BuildFinal(GraphBuilder builder, ElectionFinalPanel panel)
        {
            builder.BeginStop(FinalWinnersStop);
            _cells.Clear();
            AddPanelTitle(_cells, panel);
            Cells.EmitLinear(builder, _cells);
            ControlId firstWinner = AddWinners(builder, _cells, panel);
            if (firstWinner != null)
            {
                builder.SetStart(firstWinner);
            }

            builder.BeginStop(FinalLawsStop);
            _cells.Clear();
            // The group or the label, never both (ElectionFinalPanel.cs:143-156).
            AddReadout(
                _cells,
                AgeWidgets.Transform(panel.NoNewLawsLabel),
                "election:no-new-laws",
                Raw(panel.NoNewLawsLabel)
            );
            // Flow control: whether the card table is walked at all - the cards stay drawn inside a
            // group or a scroll view the panel has switched off.
            if (
                AgeWidgets.Visible(panel.UnlockedLawsGroup)
                && AgeWidgets.Visible(AgeWidgets.Transform(panel.UnlockedLawScrollView))
            )
            {
                AddLawCards(_cells, panel.UnlockedLawLinesTable, "election:final/law");
            }

            Cells.EmitLinear(builder, _cells);

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
                AgeWidgets.Transform(panel.NoActionOutcomesLabel),
                "election:no-outcomes",
                Raw(panel.NoActionOutcomesLabel)
            );
            // Flow control: the caption and every outcome line under it are read one by one.
            if (AgeWidgets.Visible(panel.ElectionActionOutcomesGroup))
            {
                AddReadout(
                    _cells,
                    AgeWidgets.Transform(panel.ActionOutcomesLabel),
                    "election:outcomes-caption",
                    Raw(panel.ActionOutcomesLabel)
                );
                AddOutcomeLines(_cells, panel);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The senators the election returned - one ROW each: the winning party, then one node for
        /// every bloc of votes that was redirected to it.
        ///
        /// The cards are read-only (the panel binds no client to them and their toggles are never
        /// wired, <c>ElectionFinalPanel.cs:184-204</c>), but read-only is not the same as one line. A
        /// card draws three independent things - which party won, how experienced it is, and a badge
        /// per party whose votes came across - and reading the whole card with
        /// <see cref="AgeWidgets.TextOf"/> glued them into one name ("Militarists Established
        /// +Industrialists"): three facts said as if they were the card's title, none of them
        /// explainable on its own. So the party names the card, its experience tier is the value beside
        /// it, its dossier is the tooltip, and the game's own sentence about what experience MEANS
        /// (<c>SenatorBaseCard.cs:116-119</c>) is reviewable under it - buffer-only, because the card's
        /// own dossier is the tooltip worth hearing here and the experience sentence is a definition
        /// the player asks for rather than one they need on every landing.
        ///
        /// Each badge is its own node saying which party's votes were redirected, with the game's
        /// one-sentence explanation announced (<c>PoliticsMiniature.cs:14-21</c>). They are drawn
        /// scattered around the support gauge at computed angles (<c>WinnerSenatorCard.cs:116-133</c>),
        /// so their rectangles are no reading order at all - the ORDER is declared, the one the game
        /// bound the redirections in, and each badge is a row of its own under the card rather than a
        /// column of it: Down walks card, badge, badge, and Right from the card finds nothing sideways,
        /// which is the truth about a card with a badge floating beside its gauge (owner ruling
        /// 2026-08-22). They exist only where votes were redirected and the government
        /// allows redirecting them (:85-92), which the card expresses as the group's own visibility;
        /// the group fades in on a modifier, so it is asked whether it is PAINTED rather than merely
        /// visible.
        ///
        /// Which winner of how many is a fact about the ROW, not a place in a bar of choices: it is
        /// stamped as one, so it is said on arriving at a winner and not again while the player walks
        /// out to that winner's badges.
        ///
        /// The support each won is drawn as a circular gauge with no number on it, and nothing here
        /// invents one.
        /// </summary>
        /// <returns>The first winner's card, for the stop to open on.</returns>
        private static ControlId AddWinners(
            GraphBuilder builder,
            List<Cell> cells,
            ElectionFinalPanel panel
        )
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(panel.WinnerSenatorCardsTable);
            int winners = 0;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (Winner(children[i]) != null)
                {
                    winners++;
                }
            }

            ControlId first = null;
            int index = 0;
            List<Cell> one = new List<Cell>(1);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                WinnerSenatorCard card = Winner(child);
                if (card == null)
                {
                    continue;
                }

                TableRow row = new TableRow
                {
                    Key = "election:winner/" + i,
                    Index = ++index,
                    Count = winners,
                };
                cells.Clear();
                ControlId id = AddWinnerCard(cells, card, child, i, row);
                AddRedirections(cells, card, i, row);
                // One row per NODE, all sharing the winners' row key: the card and the badges around it
                // are things of their own rather than columns of one line, so Right from the card has
                // nowhere sideways to go. Positions stay off - counting single-item rows would number
                // every card and badge in the stop as one flat list - and the shared TableRow is what
                // still says "winner 1 of 2", once, on arriving at the card.
                for (int c = 0; c < cells.Count; c++)
                {
                    one.Clear();
                    one.Add(cells[c]);
                    Cells.EmitRow(builder, one, WinnersRowKey, positions: false);
                }

                if (first == null)
                {
                    first = id;
                }
            }

            return first;
        }

        /// <summary>A drawn card with a senator bound to it, or null.</summary>
        private static WinnerSenatorCard Winner(AgeTransform child)
        {
            WinnerSenatorCard card = Component<WinnerSenatorCard>(child);
            // Spoken count and flow control: AddWinners counts the answers to say "1 of N" on every
            // winner's row, and walks only the cards this answered for.
            return card != null && AgeWidgets.Visible(child) && card.Senator != null ? card : null;
        }

        /// <summary>The winning party itself: its name, the experience tier drawn under it
        /// (<c>SenatorBaseCard.cs:165-185</c>), and its dossier. The tier is asked of the drawn label
        /// rather than of the model, and only while the card is painting it - the prefab carries the
        /// last binding's word otherwise.</summary>
        private static ControlId AddWinnerCard(
            List<Cell> cells,
            WinnerSenatorCard card,
            AgeTransform widget,
            int i,
            TableRow row
        )
        {
            AgeTooltip tooltip = card.PoliticsTooltip ?? AgeWidgets.Raw(widget);
            AgePrimitiveLabel name = card.PoliticsNameLabel;
            AgePrimitiveLabel tier = card.PoliticsExperienceLabel;
            AgeTransform tierWidget = AgeWidgets.Transform(tier);
            AgeTransform whole = widget;
            // Content, both times: whether the tier is part of the card's reading, and whether the
            // sentence explaining it is offered as a section. The card's own cell stands on the card.
            NodeVtable vtable = GraphNodes.Readout(
                () => name != null ? AgeText.Label(name) : AgeWidgets.TextOf(whole),
                () => AgeWidgets.Painted(tierWidget) ? AgeText.Label(tier) : null,
                null,
                null
            );
            // The tier's sentence is a SECOND hover surface on the card - the game hangs it on the
            // experience group while the card points at the party's dossier - so by the standing ruling
            // it should be an entry of its own. It cannot be, HERE: a winner card is emitted as its own
            // graph ROW (AddWinners, Cells.EmitRow), and a node inside an open row cannot be a group
            // (GraphBuilder.BeginGroup refuses one). So it stays the reviewed line it was until the row
            // shape itself is settled - reported 2026-08-28, unconverted on purpose rather than dropped,
            // because the sentence is content-backed and dropping it would lose real words.
            vtable.Sections = GraphNodes.Sections(
                GraphNodes.TooltipSection(tooltip),
                AgeWidgets.Painted(tierWidget)
                    ? GraphNodes.ReviewedTooltipSection(card.ExperienceTooltip)
                    : null
            );
            vtable.Row = row;
            AgeWidgets.PointAt(vtable, Anchor(tooltip, widget));
            ControlId id = ControlId.Structural("election:winner/" + i);
            Cells.Add(cells, widget, id, vtable);
            return id;
        }

        /// <summary>The parties whose votes were redirected to this winner - the badges the card draws
        /// around its gauge, each naming its party and explaining itself in one sentence of the game's
        /// own (<c>PoliticsMiniature.cs:14-21</c>).</summary>
        private static void AddRedirections(
            List<Cell> cells,
            WinnerSenatorCard card,
            int i,
            TableRow row
        )
        {
            if (!AgeWidgets.Painted(card.AdditionalPoliticsGroup))
            {
                return;
            }

            IList<AgeTransform> children = AgeWidgets.DrawnChildren(card.AdditionalPoliticsContainer);
            for (int j = 0; children != null && j < children.Count; j++)
            {
                AgeTransform child = children[j];
                PoliticsMiniature badge = Component<PoliticsMiniature>(child);
                // The badge's own drawn-ness is the cells' question - each carries its badge - while
                // the group above stays asked as FLOW CONTROL: the strip is walked badge by badge.
                if (badge == null)
                {
                    continue;
                }

                AgeTooltip tooltip = badge.Tooltip ?? AgeWidgets.Raw(child);
                AgePrimitiveLabel label = badge.Label;
                NodeVtable vtable = GraphNodes.Readout(
                    () => AgeText.Label(label),
                    () => null,
                    null,
                    tooltip
                );
                vtable.Row = row;
                AgeWidgets.PointAt(vtable, Anchor(tooltip, child));
                Cells.Add(
                    cells,
                    child,
                    ControlId.Structural("election:winner/" + i + "/redirect/" + j),
                    vtable
                );
            }
        }

        private static void AddOutcomeLines(List<Cell> cells, ElectionFinalPanel panel)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(panel.ElectionActionOutcomesTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                ElectionActionOutcomeLine line = Component<ElectionActionOutcomeLine>(child);
                // Banding input: the outcome cells are banded by rectangle, and Cells.Add takes them
                // without asking the gate.
                if (line == null || !AgeWidgets.Visible(child))
                {
                    continue;
                }

                AddReadout(cells, AgeWidgets.Transform(line.OutcomeTitleLabel), "election:outcome/" + i);
            }
        }
    }
}
