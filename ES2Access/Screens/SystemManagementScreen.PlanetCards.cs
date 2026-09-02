using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The planets across the middle of the page: the card each one gets, what its review
    /// buffer holds, the actions hung off it, and the population ring a card carries units in and out
    /// of.</summary>
    public sealed partial class SystemManagementScreen
    {
        // ---- the planets ----

        /// <summary>
        /// The planet cards across the middle, in the order they are drawn - which is left to right,
        /// and is NOT the order the system holds its planets in: the table lays the cards out from the
        /// right, so the model's first planet is the rightmost card. Measured rather than assumed,
        /// because a reading order taken from the model would have been backwards.
        /// </summary>
        private void BuildPlanets(GraphBuilder builder, StarSystemScreen window)
        {
            try
            {
                // Picking a population unit up is only offered where there is somewhere to put it
                // down, and what THIS page offers is the game's own target list: the other planet
                // cards, and the spaceport panel whenever it is drawn
                // (<see cref="PopulationMoves.OnSystemPage"/>). Asking only about a second colony -
                // what this was until 2026-08-29 - made the carry silent on every marker of a
                // one-colony system whose port the mouse could drag into.
                bool canCarry = PopulationMoves.OnSystemPage(window);
                OpenCardBeingSeated(builder);
                for (int i = 0; i < _planets.Count; i++)
                {
                    AddPlanet(builder, _planets[i], canCarry);
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the planet cards threw: " + e);
            }
        }

        /// <summary>
        /// One planet card.
        ///
        /// ENTER IS THE CARD'S OWN CLICK, which on this page is the planet's own page. The card is an
        /// AGE overlay and carries no click of its own - the click the game answers is the one on the
        /// PLANET behind it (<c>GalaxyPlanetCursorTarget.OnCursorClick</c> :30-53, which asks for
        /// <c>GalaxyViewLevel_PlanetOverview</c> while this view level is up), and that is what
        /// <see cref="GalaxyViewLevels.OpenPlanet"/> posts. Nothing is spoken for it: the page changes
        /// and the page announces itself.
        ///
        /// Everything else the card offers is where the card draws it. The rename button beside the
        /// title and the colonize button under it are child nodes; the population the card draws as a
        /// ring of markers is a row per SLOT of that ring, in up to three bands
        /// (<see cref="AddPopulationSlots"/>), and people are moved by CARRYING them (the carry key on
        /// a slot picks up what the game's own drag would take from that marker, the activation key on
        /// another card or the spaceport puts them down) rather than by a menu entry per unit and
        /// destination, which is the same gesture a ship gets in the fleet panel and the same drag the
        /// mouse has here. The drop lives on the SLOTS and not on the card: an empty one is the plain
        /// add, an occupied one the game's swap.
        /// </summary>
        private void AddPlanet(
            GraphBuilder builder,
            PlanetLabel_SystemManagement label,
            bool canCarry
        )
        {
            Planet planet = label.Planet;
            if (planet == null)
            {
                return;
            }

            PlanetLabel_SystemManagement it = label;
            // The card's own status button carries the game's sentence about the state - "too hostile
            // to be colonized", and which technology would change that. It is DECLARED as the card's
            // tooltip, so the card says it has one and the buffer holds its words; what it is not is
            // announced, which is this screen's one deliberate override of the short/long rule (the
            // sentence runs to three lines and would be read out on every pass down the planets).
            AgeTransform status = StatusWidget(label);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetTitle)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetStatus)),
                    // An outpost's card ends in the game's own sentence about how it is getting on
                    // ("Colony in 24 Turn"), which is drawn on the card and so is spoken, not buffered.
                    GraphNodes.ValuePart(() => Drawn(it.OutpostBottomCaption)),
                },
                // The status tooltip first, then the rest of the card, which is the order the card
                // draws them in.
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(AgeWidgets.Raw(status)),
                    NodeSection.Buffer(() => PlanetDetails(it))
                ),
                OnActivate = () => GalaxyViewLevels.OpenPlanet(it.Planet),
            };

            // THE CARD ITSELF TAKES NO DROP (owner ruling 2026-08-29). The game's mouse accepts one
            // anywhere on the card's rectangle, but a keyboard player is walking rows, and a card
            // header that also swallowed drops made two rows out of one gesture: the header and the
            // free slot under it both said "drop target" and did different things. So the drop lives
            // on the SLOTS alone (<see cref="AddPopulationSlot"/>) - an empty one is the plain add,
            // an occupied one the swap - which reaches every outcome the mouse reaches and says where
            // the people are going. A full planet then offers only its swaps, and a planet with room
            // offers its free places.
            AgeWidgets.PointAt(vtable, status ?? label.AgeTransform);

            string key = "system:planet/" + planet.GUID;
            ControlId id = ControlId.For(planet, key);
            List<CardActions.CardAction> rename = new List<CardActions.CardAction>(1);
            CardActions.AddNamedByMod(rename, label.PlanetRenameButton, ModStrings.SystemRenamePlanet);
            List<CardActions.CardAction> buttons = PlanetButtons(label);
            List<CardActions.CardAction> outpost = OutpostActions(label);
            List<Population> units = new List<Population>(4);
            Ring ring = PlanetRing(label, key);
            List<PopulationSlots.Slot> slots = RingSlots(ring, units);
            List<TooltipChildren.Dossier> dossiers = PlanetDossiers(label);
            // Flow control: whether the card is a leaf or a group. A card whose ONLY content is a
            // Sanctuary band would otherwise be declared as a leaf and the band never walked into.
            bool ghost = AgeWidgets.Visible(label.GhostGroup);
            if (
                rename.Count == 0
                && buttons.Count == 0
                && outpost.Count == 0
                && slots.Count == 0
                && dossiers.Count == 0
                && !ghost
            )
            {
                // Synthetic: the card stands for the PLANET, and the walk over the drawn planet
                // labels is what says the system is showing it.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                // Down the card, in the order it is drawn: the rename button beside the title, the
                // population ring in the middle, the action buttons along the bottom - and then, as a
                // region of their own, the dossiers the card draws no words for at all.
                object outer = TooltipChildren.Actions(builder, key);
                CardActions.Emit(builder, key + "/name", rename);
                AddPopulationSlots(builder, ring, units, slots, canCarry);
                CardActions.Emit(builder, key, buttons);
                CardActions.Emit(builder, key + "/outpost", outpost);
                AddGhost(builder, key, label, canCarry);
                TooltipChildren.Emit(builder, key, dossiers, outer);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// The SANCTUARY band the card grows along its bottom when a ghost colony is sitting on this
        /// world (<c>PlanetLabel_SystemManagement.RefreshGhostStatus</c> :1192-1250), read in the order
        /// the game draws it: the band's own title, then the Sanctuary's population ring, then the
        /// button that turns one of its people into a sleeper.
        ///
        /// The band is drawn for a RIVAL's Sanctuary too - the group's only test is that the ghost
        /// exists and that the player can see its system (:1194) - and what a rival's draws is the
        /// title and the population figure alone: the game hides the ring, the outputs and the button
        /// for anybody else's (:1217, :1229). So everything below is gated on its own drawn flag and a
        /// rival's band simply reads shorter, with no ownership test written here.
        ///
        /// The title is the band's line and carries the figures the band draws no words for: the
        /// population count, which the game writes as a bare "3/5" beside a symbol, and the five
        /// outputs, which are the same strip of pips the card reads for the world itself
        /// (<see cref="PlanetOutputs"/>). Its own tooltip is what the game says about the Sanctuary -
        /// whose it is, and, for a rival's, how it could be got rid of.
        ///
        /// THE SANCTUARY'S RING IS HOVER-ONLY (measured 2026-08-29): the game shows
        /// <c>GhostPopulationEnumeratorFocused</c> and the outputs strip while the pointer is inside
        /// the band's own rectangle and hides them again on the way out (:648-693), and unlike the
        /// world's ring there is no simple one drawn underneath. So the slots exist exactly while the
        /// game draws them, which is the rule every other row here follows - and it works out, because
        /// landing on the band's title is what puts the pointer inside the band
        /// (<see cref="AgeWidgets.PointAt"/>), so the ring is there by the time the player steps down
        /// into it, exactly as it is there for a mouse that has hovered the band.
        ///
        /// CONTENT IS UNVERIFIED (<c>docs/planets.md</c>): a Sanctuary needs a player empire that HAS
        /// ghost systems - the Umbral Choir, a Penumbra faction chosen at new-game time - and no save
        /// in this repo is one, so the band was measured by lending the card a colony and showing the
        /// group. What that proves is the STRUCTURE - which widgets are declared, what the
        /// reader makes of each - and never what a real ghost would say in them.
        /// </summary>
        private static void AddGhost(
            GraphBuilder builder,
            string key,
            PlanetLabel_SystemManagement label,
            bool canCarry
        )
        {
            try
            {
                // Flow control: whether the band is walked at all. The group is a wired prefab field
                // and so always there; what says a Sanctuary exists is the game drawing it.
                if (!AgeWidgets.Visible(label.GhostGroup))
                {
                    return;
                }

                PlanetLabel_SystemManagement it = label;
                AgeTransform title =
                    label.GhostTitle == null ? null : label.GhostTitle.AgeTransform;
                if (title != null)
                {
                    NodeVtable vtable = GraphNodes.Readout(
                        () => AgeText.Label(it.GhostTitle),
                        null,
                        () => GhostDetails(it),
                        AgeWidgets.Raw(title)
                    );
                    AgeWidgets.PointAt(vtable, title);
                    builder.AddItem(
                        Nodes.Drawn(ControlId.For(title, key + "/ghost"), vtable, title)
                    );
                }

                Ring ring = GhostRing(label, key);
                List<Population> units = new List<Population>(4);
                List<PopulationSlots.Slot> slots = RingSlots(ring, units);
                AddPopulationSlots(builder, ring, units, slots, canCarry);

                // The one thing the band can DO, and a standard refusable card action: the game keeps
                // it drawn and switched off with its reason written into its own tooltip by the game's
                // own failure formatter (:1229-1249), so it is declared while drawn and offered while
                // the game offers it, named by the sentence that explains it.
                List<CardActions.CardAction> traitor = new List<CardActions.CardAction>(1);
                CardActions.AddRefusable(
                    traitor,
                    label.TraitorButton,
                    // Named by the SENTENCE its tooltip explains it with, not by a title: the game
                    // hangs plain content there with no wrapper and no header line, so asking for a
                    // title answered nothing and the row announced itself role-first ("button, Click
                    // to consume one population..."). Measured on the lent band, 2026-08-29 - the
                    // same treatment the card's own wordless buttons get.
                    CardActions.NameFromTooltip(label.TraitorButton)
                );
                CardActions.Emit(builder, key + "/ghost", traitor);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet card's Sanctuary band threw: " + e);
            }
        }

        /// <summary>The figures the Sanctuary band draws with no words of its own: how many people live
        /// there out of how many could, and the five outputs the ghost colony is making. The count the
        /// game writes as "3/5" beside a symbol, so it is composed as the fraction it is with the
        /// game's own word for the symbol; the outputs are read exactly as the card's own strip is
        /// (<see cref="AddFidsi"/>).</summary>
        private static IList<string> GhostDetails(PlanetLabel_SystemManagement label)
        {
            List<string> lines = new List<string>(6);
            try
            {
                ColonizedPlanet ghost = label.GhostColonizedPlanet;
                if (ghost == null)
                {
                    return lines;
                }

                AgePrimitiveLabel count = label.GhostPopulationCount;
                // Content, and of a DIFFERENT widget than the node stands on: these are lines of the
                // title's buffer, so the gate never sees them and nothing else would stop a rival's
                // hidden figure being read out.
                if (count != null && AgeWidgets.Visible(count.AgeTransform))
                {
                    AddLine(
                        lines,
                        ModStrings.Format(
                            ModStrings.FractionUnit,
                            ghost.PopulationCount,
                            ghost.MaxPopulation,
                            AgeText.Clean(PopulationIcon)
                        )
                    );
                }

                // The outputs strip is HOVER-ONLY: the game shows it while the pointer is inside the
                // band and hides it again on the way out (:669-693), like the card's own detailed
                // ring. So its own drawn flag is not the question - these are BUFFER lines, which is
                // what hover-revealed content gets - and the question is whether the game ever
                // COMPUTED them, which it does for a Sanctuary of the player's own and for nobody
                // else (:1216-1222 refreshes the enumerator only there). A rival's strip keeps
                // whatever it was last bound with, so reading it would be a made-up figure.
                FidsiEnumerator fidsi = label.GhostFidsiEnumerator;
                if (fidsi == null || fidsi.FidsiProperties == null || OwnGhost(label) == null)
                {
                    return lines;
                }

                Amplitude.Unity.Simulation.SimulationObject simulation = ghost.SimulationObject;
                if (simulation == null)
                {
                    return lines;
                }

                IList<string> numbers = PlanetOutputs.Numbers(simulation, fidsi);
                for (int i = 0; i < numbers.Count; i++)
                {
                    lines.Add(numbers[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a Sanctuary's figures threw: " + e);
            }

            return lines;
        }

        /// <summary>The symbol the game ends its Sanctuary population count with, which is the only
        /// word it writes for that figure.</summary>
        private const string PopulationIcon = "[population]";

        /// <summary>
        /// The dossiers a planet card carries beyond the sentence on its status button: the planet's
        /// own, and one per FIDSI figure in the strip of pips down its side.
        ///
        /// The card draws each pip as a picture and a bare number, and keeps everything about what
        /// that number MEANS - what it is called, what it is made of, what would change it - in a
        /// dossier behind the pip. The card's buffer already carries the captioned figures
        /// (<see cref="PlanetDetails"/>); this is the page behind each one.
        ///
        /// The card keeps TWO strips and swaps them (<c>FidsiScoreTable</c> for a planet nobody has
        /// settled, the <c>FidsiEnumerator</c>'s duplets once it is a colony) with the other one left
        /// bound to whatever it last showed, so the strip is taken from whichever is DRAWN and the
        /// resolver drops the pips of the hidden one.
        ///
        /// The improvement box is the third: the card draws which improvement this world has - or that
        /// one is being built, or that there is none - and the game keeps what that MEANS on a tooltip
        /// field of its own rather than on the box (<c>RefreshPlanetImprovement</c> :1335-1394), so
        /// nothing hanging off the card could ever have found it. It is either a sentence the game
        /// wrote or the improvement's own dossier, depending on which of the three states the world is
        /// in, and both read here.
        /// </summary>
        private static List<TooltipChildren.Dossier> PlanetDossiers(
            PlanetLabel_SystemManagement label
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(6);
            try
            {
                TooltipChildren.Add(found, label.PlanetTooltipFrame);
                TooltipChildren.AddInside(found, label.FidsiScoreTable);
                TooltipChildren.AddInside(
                    found,
                    label.FidsiEnumerator == null ? null : label.FidsiEnumerator.AgeTransform
                );
                AddDepositDossiers(found, label);
                // Content: which dossiers the card offers. These become a region of the card's own
                // node, not nodes the gate ever sees.
                if (AgeWidgets.Visible(label.ImprovementStatus))
                {
                    TooltipChildren.Add(
                        found,
                        label.ImprovementTooltip,
                        label.ImprovementStatus
                    );
                    TooltipChildren.AddPlain(
                        found,
                        label.ImprovementTooltip,
                        label.ImprovementStatus
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet card's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// The page behind each deposit the world is sitting on - what the resource is for, who can
        /// work it and what is stopping them.
        ///
        /// The item draws a picture and a figure, and everything else about the resource is a dossier
        /// the renderer assembles from the wrapper it binds (<c>ResourceDepositItem.Refresh</c> :36-42
        /// sets the class, the target and the failure sentences), so the card's line
        /// (<see cref="PlanetCardLines"/>) says which resource and how much of it, and this is where
        /// the rest of it is read. The pooled table's retired items keep the PREVIOUS planet's wrapper on
        /// their tooltip, so each item is asked the gate's own drawing test at ADMISSION - the one
        /// place early enough to stop a ghost winning the dedupe.
        /// </summary>
        private static void AddDepositDossiers(
            List<TooltipChildren.Dossier> found,
            PlanetLabel_SystemManagement label
        )
        {
            AgeTransform group = label.ResourceDepositsGroup;
            // Content: whether the deposits contribute dossiers at all - they become a region of the
            // card's node rather than nodes of their own.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            IList<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Admission, not the gate: the collector DEDUPES by tooltip
                // (<see cref="TooltipChildren.Add"/>), so a retired row still holding the previous
                // binding's deposit would swallow the drawn row that shares it, and the gate - which
                // only ever sees finished nodes - would then drop the one node the pair had left. The
                // gate's OWN test is asked, under the same flag, rather than a second opinion; the
                // shared door cannot ask it for every caller (<see cref="TooltipChildren.Admitted"/>).
                AgeTransform child = children[i];
                if (child == null || !NodeGate.StillDrawn(child))
                {
                    continue;
                }

                ResourceDepositItem item = child.GetComponent<ResourceDepositItem>();
                if (item != null)
                {
                    TooltipChildren.Add(found, item.Tooltip, child);
                }
            }
        }

        /// <summary>Which of the card's own buttons the game is drawing. Rename is emitted separately
        /// because the card draws it at the top, beside the title, and these along the bottom.</summary>
        private static List<CardActions.CardAction> PlanetButtons(PlanetLabel_SystemManagement label)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(1);
            try
            {
                CardActions.AddNamedByMod(found, label.ColonizeButton, ModStrings.SystemColonize);
                // The three the card draws along its bottom for a world that is already yours: pick a
                // specialization improvement, reduce an anomaly, terraform. The sibling EMPIRE screen
                // declares exactly these off the same prefab family (<c>EmpireScreen.CardButtons</c>)
                // and this page declared none of them, so choosing a planet's specialization was
                // unreachable from the system page by keyboard at all. Each names itself in the
                // sentence its own tooltip explains it with - and each is kept while DRAWN, because
                // the game switches them off with the reason appended to that tooltip and a blocked
                // one should refuse rather than vanish.
                CardActions.AddRefusableNamedByTooltip(found, label.BuildInfrastructureButton);
                CardActions.AddRefusableNamedByTooltip(found, label.ReduceAnomalyButton);
                CardActions.AddRefusableNamedByTooltip(found, label.TerraformButton);
                AddAnomalyHints(found, label);
                AddCuriosities(found, label);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet card's buttons threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// The anomalies on the card, as the CONTROLS the game made them: each row's own click jumps to
        /// the technology that would let the anomaly be reduced (<c>PlanetAnomalyItem.OnHintCb</c>),
        /// which the mouse has and no node stood on. The click is wired on the ROW, not on the little
        /// hint button beside it - that one only carries the hint's state - so the row is what is
        /// declared and the button is what decides whether it would do anything.
        ///
        /// Kept declared while the row is drawn and OFFERED only while the hint is live, the same
        /// treatment every other blocked control on these cards gets: the game only fills the hint in
        /// for a world of yours whose reduction is blocked, and a row that answers "unavailable" is the
        /// truthful reading of a click that would do nothing. The anomaly's own dossier - the paragraph
        /// and the reduction prerequisites - rides along as the node's tooltip; the card's buffer keeps
        /// naming the anomalies as it always did.
        ///
        /// The table pools its items, so admission is what keeps a retired row out of the numbering.
        /// </summary>
        private static void AddAnomalyHints(
            List<CardActions.CardAction> found,
            PlanetLabel_SystemManagement label
        )
        {
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(label.PlanetAnomaliesTable);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform row = items[i];
                PlanetAnomalyItem item = row == null ? null : row.GetComponent<PlanetAnomalyItem>();
                if (item == null || item.HintButton == null)
                {
                    continue;
                }

                PlanetAnomalyItem it = item;
                AgeTransform hint = item.HintButton.AgeTransform;
                // Through the collector's admission filter like every other entry: this list is
                // NUMBERED, and the table below is pooled, so a hand-built row cannot be allowed to
                // skip the one test that keeps a retired one out of the count.
                CardActions.Add(
                    found,
                    new CardActions.CardAction
                    {
                        Widget = row,
                        Label = () => AgeWidgets.TooltipTitle(it.Tooltip),
                        Tooltip = it.Tooltip,
                        Offered = () => AgeWidgets.Hinted(hint),
                    }
                );
            }
        }

        /// <summary>
        /// The curiosities the card is drawing, each one the same wired button the map's own card
        /// carries: a wordless icon kept CLICKABLE while refused, with the reason in its own tooltip
        /// (<c>PlanetCuriosityItem.Refresh</c>). Named off the wrapper the game hangs on that tooltip,
        /// which is the only place the thing in orbit has a name.
        ///
        /// This card mixes three kinds of item into one table, so the curiosity items are picked out by
        /// their own component rather than by position; the rest of the table stays a line of the card's
        /// (<see cref="PlanetDetails"/>).
        ///
        /// Admission is the gate, as on the anomalies table above: this table is pooled too
        /// (<c>PlanetLabel_SystemManagement.RefreshPlanetCuriosities</c> :1297 <c>ReserveChildren</c>),
        /// so a card showing fewer curiosities than the one read before it keeps the surplus items
        /// <c>Visible</c> at alpha 0 - and a retired item has had its tooltip unbound, so it has no
        /// name either. Measured on Heka II, which offered one drawn curiosity and one leftover from
        /// another planet declared as a nameless "button, unavailable".
        /// </summary>
        private static void AddCuriosities(
            List<CardActions.CardAction> found,
            PlanetLabel_SystemManagement label
        )
        {
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(label.PlanetCuriositiesTable);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                // Drawn-ness is the collector's question, not this walk's: a curiosity the pool has
                // retired never enters the numbered list (<see cref="CardActions.AddRefusable"/>).
                AgeTransform item = items[i];
                if (item != null && SkipCuriosities(item))
                {
                    CardActions.AddRefusable(found, item, CardActions.TitleOf(item));
                }
            }
        }

        /// <summary>
        /// What an OUTPOST's card offers, in the order the card draws it: the strip of outpost actions
        /// along the top of the outpost group, then the decolonize tick under them.
        ///
        /// The game draws an action as a tick with a price on it and its name NOWHERE - the name, what
        /// it does, how long it takes and what it costs all live in the wrapper on its own tooltip
        /// (<c>GuiOutpostAction</c>) - so that wrapper's title is what the node is called and the
        /// tooltip is the dossier behind it. An action the faction cannot have at all the game hides
        /// outright (the <c>Discard</c> failure flag, <c>OutpostActionItem.Bind</c>), so those are not
        /// here; one it is merely refusing today stays drawn and switched off, and is declared refusing
        /// with the game's own reason. Enter is the tick's own click, which starts the action, or -
        /// only on the turn it started, which is the whole of the game's cancel window - cancels it
        /// with a refund (<c>PlanetLabel_SystemManagement.OnOutpostActionSwitchCb</c> :1566).
        ///
        /// Decolonize is the same shape: Enter is its click, and the game raises its own confirmation
        /// box, which speaks through <c>MessageBoxScreen</c> like every other one. Ticked, it is
        /// already scheduled and the click unschedules it with no confirmation at all (:1587).
        ///
        /// The strip is POOLED (<c>RefreshOutpostActions</c> :988 <c>ReserveChildren</c>), so a tick is
        /// admitted on the drawing test rather than on the visibility flag a retired row keeps: an
        /// outpost offering fewer actions than the one read before it would otherwise declare the
        /// surplus ticks, still wearing the other outpost's name - and renumber the real ones.
        /// </summary>
        private static List<CardActions.CardAction> OutpostActions(
            PlanetLabel_SystemManagement label
        )
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            try
            {
                // Flow control: whether the outpost's action list is collected at all - the actions
                // below are NUMBERED by their place in it.
                if (label.OutpostGroup == null || !AgeWidgets.Visible(label.OutpostGroup))
                {
                    return found;
                }

                AgeTransform table = label.OutpostActionsTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    // A row the pool has retired - faded as a ROW while its tick stays at alpha 1 -
                    // is dropped by the collector's own admission filter, which walks the tick's
                    // ancestry and so sees the faded row above it (CardActions.AddToggle).
                    OutpostActionItem item =
                        items[i] == null ? null : items[i].GetComponent<OutpostActionItem>();
                    if (item == null)
                    {
                        continue;
                    }

                    OutpostActionItem it = item;
                    CardActions.AddToggle(
                        found,
                        item.Toggle,
                        CardActions.TitleOf(item.Toggle),
                        () => OutpostActionValue(it)
                    );
                }

                CardActions.AddToggle(
                    found,
                    label.DecolonizeToggle,
                    CardActions.GameText("%PlanetDecolonizeTitle"),
                    null
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: reading an outpost card's actions threw: " + e);
            }

            return found;
        }

        /// <summary>What the game writes on an outpost action: what it would cost while it is only on
        /// offer, and how many turns it has left once it is running.</summary>
        private static string OutpostActionValue(OutpostActionItem item)
        {
            try
            {
                return item.DurationGroup != null && item.DurationGroup.Visible
                    ? Drawn(item.DurationLabel)
                    : Drawn(item.CostLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Everything the card shows that the readout cannot carry: what kind of world it is, what
        /// living there is like, what has been found on it, and its five outputs. In the order the card
        /// draws them, top to bottom - under the status tooltip, which the card declares as a tooltip
        /// section of its own rather than folding it in here (a tooltip read as "details" is a tooltip
        /// nothing ever indicates).
        /// </summary>
        private static IList<string> PlanetDetails(PlanetLabel_SystemManagement label)
        {
            List<string> lines = new List<string>();
            try
            {
                AddWidgetLines(lines, label.PlanetTypeGroup);
                AddWidgetLines(lines, label.PlanetSizeGroup);
                AddWidgetLines(lines, label.PlanetGameplayTypeTable);
                AddWidgetLines(lines, label.PlanetAnomaliesTable);
                // The card puts three kinds of thing in this one table - what sort of world it is, what
                // was found on it, and the curiosities still to be looked into. The curiosities are
                // buttons and are child nodes of their own, so only the rest of the table is read here.
                AddWidgetLines(lines, label.PlanetCuriositiesTable, SkipCuriosities);
                // What the world is sitting on. The same reader: a deposit item writes its resource's
                // name nowhere on itself, and the shared reading knows to ask its wrapper.
                AddWidgetLines(lines, label.ResourceDepositsGroup);
                AddDepletion(lines, label);
                AddWidgetLines(lines, label.ImprovementStatus);
                AddFidsi(lines, label);
                AddOutpost(lines, label);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet's details threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// How worn out the world is - a mining probe's damage, or a Craver colony eating the planet it
        /// lives on. The game draws this only while the planet is being depleted or already is
        /// (<c>PlanetLabel_SystemManagement.RefreshPlanetDepletion</c> :1321-1332), so being drawn is
        /// the gate, and it writes the state and how many turns are left on the item itself with the
        /// sentence behind them in its own tooltip.
        ///
        /// A FULLY depleted planet swaps that tooltip for an assembled dossier, whose words do not
        /// exist until the tooltip is drawn - so the state line still reads and the paragraph arrives
        /// when the player looks at it, rather than being invented here.
        /// </summary>
        private static void AddDepletion(List<string> lines, PlanetLabel_SystemManagement label)
        {
            PlanetDepletionStatusItem item = label.PlanetDepletionStatusItem;
            // Content: whether the depletion state is one of the card's lines.
            if (item == null || !AgeWidgets.Visible(item.AgeTransform))
            {
                return;
            }

            AddLine(lines, Drawn(item.Title));
            Add(lines, AgeWidgets.TooltipLines(item.Tooltip));
        }

        /// <summary>
        /// The lines an OUTPOST's card carries that nothing else on it says: who owns it (a plain
        /// label the game only draws while the system is an outpost), when the next population unit
        /// arrives and which kind it will be - both of which the card draws as a bare number and a
        /// symbol, so the two sentences the game explains them with are what carries them - and last
        /// the help behind the progress caption, whose own words are already spoken as the card's
        /// state.
        /// </summary>
        private static void AddOutpost(List<string> lines, PlanetLabel_SystemManagement label)
        {
            // Content: whether the outpost's progress is among the card's lines - a colonized system
            // draws none of it.
            if (label.OutpostGroup == null || !AgeWidgets.Visible(label.OutpostGroup))
            {
                return;
            }

            AddLine(lines, Drawn(label.OutpostOwnerLabel));
            Add(lines, AgeWidgets.TooltipLines(Tooltip(label.OutpostOwnerLabel)));

            GrowthGaugeItem growth = label.GrowthLine;
            if (growth != null)
            {
                AddLine(lines, Drawn(growth.TurnsBeforeNextPop));
                Add(lines, AgeWidgets.TooltipLines(Tooltip(growth.TurnsBeforeNextPop)));
                Add(lines, AgeWidgets.TooltipLines(Tooltip(growth.NextPopulationIcon)));
            }

            Add(lines, AgeWidgets.TooltipLines(Tooltip(label.OutpostBottomCaption)));
        }

        /// <summary>The tooltip a drawn primitive carries, whatever kind of primitive it is.</summary>
        private static AgeTooltip Tooltip(AgePrimitive primitive)
        {
            try
            {
                return primitive == null ? null : AgeWidgets.Raw(primitive.AgeTransform);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The planet's five outputs, named by the game's own property titles, in the two
        /// shapes the card draws them in. A COLONY's are written as numbers and read as numbers, off
        /// the colony's own simulation object. A world nobody has settled gets no numbers at all: the
        /// card hides that row and draws a table of rating pips instead
        /// (<c>PlanetLabel_SystemManagement.BindPlanet</c> :358-368), which the map's card does too,
        /// so the lines of both shapes are composed for both cards in <see cref="PlanetOutputs"/>.
        /// Which shape is drawn is the game's own test - whether the planet is a colony - so it is
        /// the test here.</summary>
        private static void AddFidsi(List<string> lines, PlanetLabel_SystemManagement label)
        {
            FidsiEnumerator fidsi = label.FidsiEnumerator;
            if (fidsi == null || fidsi.FidsiProperties == null)
            {
                return;
            }

            ColonizedPlanet colony = label.ColonizedPlanet;
            if (colony == null)
            {
                IList<string> ratings = PlanetOutputs.Ratings(
                    label.Planet,
                    fidsi,
                    label.FidsiParametersGuiElement
                );
                for (int i = 0; i < ratings.Count; i++)
                {
                    AddLine(lines, ratings[i]);
                }

                return;
            }

            Amplitude.Unity.Simulation.SimulationObject simulation = colony.SimulationObject;
            if (simulation == null)
            {
                return;
            }

            IList<string> numbers = PlanetOutputs.Numbers(simulation, fidsi);
            for (int i = 0; i < numbers.Count; i++)
            {
                lines.Add(numbers[i]);
            }
        }

        /// <summary>The colony this card is for WHOEVER owns it - the same object the card binds its
        /// population ring to (<c>PlanetLabel.BindPlanet</c> takes it straight off
        /// <c>Planet.ColonizedPlanet</c>, so an enemy outpost's card holds the enemy's colony), and so
        /// the one to read the ring's contents from. Null on a world nobody has settled.</summary>
        private static ColonizedPlanet Colony(PlanetLabel_SystemManagement label)
        {
            try
            {
                return label == null ? null : label.ColonizedPlanet;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The colony this card is for when it is the PLAYER's, or null - the card of an
        /// unsettled world, or of somebody else's colony, is neither a source nor a target.</summary>
        private static ColonizedPlanet Settled(PlanetLabel_SystemManagement label)
        {
            return PopulationRings.Settled(Colony(label));
        }

        /// <summary>
        /// The SLOTS of a colony's population ring, and the unit filling each - the card's middle,
        /// read as the ring is drawn rather than as the model is stored.
        ///
        /// The game draws one marker per slot and says everything about a slot in its COLOUR: an
        /// ordinary place to live, a place under the overpopulation arc, a place the world's current
        /// maximum has locked. <see cref="PopulationSlots"/> is that arithmetic; this supplies its
        /// terms from the colony (<paramref name="units"/> comes back holding one entry per population
        /// unit, in <c>PopulationsByAffinity</c> order, which is the order the game's own enumerator
        /// lays the markers out in) and asks the RING whether there is one to read at all.
        ///
        /// Contents from the model, existence from the drawing. The detailed ring the markers' own
        /// tooltips hang on is only shown under a mouse (<c>PlanetLabel_SystemManagement</c> swaps it
        /// in on hover), so reading a slot's affinity off a marker would answer nothing while the
        /// player is on the keyboard - and equally, a card the game is drawing no ring on has no slots
        /// to offer, whatever the model says the planet could hold.
        ///
        /// A world NOBODY has settled gets a ring too - measured 2026-08-26, one marker per point of
        /// its maximum population on every card in the system - because the enumerator falls back to
        /// the PLANET's own figures when there is no colony
        /// (<c>PlanetPopulationEnumerator.GetPopulationOwnerData</c> :71-75) and only the ring's
        /// ENABLE flag is gated on <c>IsAvailable</c>. Those markers are all empty, none is locked and
        /// no arc is drawn over them (<see cref="PopulationSlots.BuildUnsettled"/>), so how much room
        /// a world has - the thing a colonization is decided on - is read the same way on both kinds
        /// of card.
        ///
        /// Somebody ELSE's colony - an enemy outpost sitting on a free world of a system the player
        /// owns - reads the SAME way (owner ruling 2026-08-27, replacing the deliberate skip this
        /// carried until then). The game draws that card's ring from the foreign colony and draws
        /// THEIR units in it: the label binds whatever colony the planet holds
        /// (<c>PlanetLabel.BindPlanet</c>: <c>ColonizedPlanet = Planet.ColonizedPlanet</c>), hands it
        /// to the ring unfiltered (<c>PlanetLabel_SystemManagement.Bind</c> :373) and shows that ring
        /// with no ownership test at all (<c>OnBeginShow</c> :496), so
        /// <c>PopulationEnumerator.BuildListOfGuiPopulations</c> lays out the other empire's
        /// affinities through the other empire's own <c>DepartmentOfTheInterior</c>. Mirroring what is
        /// drawn means reading it; only the two things the game refuses there are refused here - the
        /// unit cannot be picked up and the card cannot be dropped on, both of which stay gated on
        /// <see cref="Settled"/>.
        /// </summary>
        private static List<PopulationSlots.Slot> RingSlots(Ring ring, List<Population> units)
        {
            return PopulationMoves.Slots(
                ring.Card == null ? null : ring.Card.Planet,
                ring.Colony,
                ring.Markers,
                AgeWidgets.DrawnCount(MarkerContainer(ring)),
                units
            );
        }

        /// <summary>
        /// WHICH population ring a row is being read from. A planet card draws up to two of them - the
        /// world's own, and the Sanctuary's when a ghost colony is sitting on the same world
        /// (<c>PlanetLabel_SystemManagement.RefreshGhostStatus</c> :1192-1250) - and the game runs both
        /// through the SAME drag machinery: the same client, the same target list
        /// (<c>PlanetLabelsWindow_SystemManagement.GetPopulationDragDropTargets</c> :72 asks both
        /// enumerators), the same order. So the rows are built once and told which ring they are on.
        ///
        /// <see cref="Markers"/> is the enumerator the game is DRAWING (a card swaps between a simple
        /// ring and a detailed one) and decides the slot geometry; <see cref="Target"/> is the one whose
        /// own <c>CanAcceptPopulationDrop</c> answers a drop, which for a planet is always the focused
        /// ring whichever is drawn. <see cref="Colony"/> is whose people fill it - possibly another
        /// empire's, which reads and neither carries nor takes - and <see cref="Destination"/> is the
        /// colony a drop would land on, null wherever the game moves nobody.
        /// </summary>
        private sealed class Ring
        {
            public PlanetLabel_SystemManagement Card;
            public PlanetPopulationEnumerator Markers;
            public PlanetPopulationEnumerator Target;
            public ColonizedPlanet Colony;
            public ColonizedPlanet Destination;
            public string Key;
            public string Scratch;
        }

        /// <summary>The card's own ring - the world's population, exactly as it read before the
        /// Sanctuary band existed.</summary>
        private static Ring PlanetRing(PlanetLabel_SystemManagement label, string key)
        {
            return new Ring
            {
                Card = label,
                Markers = DrawnEnumerator(label),
                Target = label == null ? null : label.PlanetPopulationEnumeratorFocused,
                Colony = Colony(label),
                Destination = Settled(label),
                Key = key + "/population",
                Scratch = string.Empty,
            };
        }

        /// <summary>The Sanctuary's ring, which the game draws only for a ghost colony of the PLAYER's
        /// (<c>RefreshGhostStatus</c> :1217 hides the whole group for anybody else's) and binds through
        /// the card's own drag client (:375).</summary>
        private static Ring GhostRing(PlanetLabel_SystemManagement label, string key)
        {
            ColonizedPlanet ghost = OwnGhost(label);
            return new Ring
            {
                Card = label,
                Markers = label == null ? null : label.GhostPopulationEnumeratorFocused,
                Target = label == null ? null : label.GhostPopulationEnumeratorFocused,
                Colony = ghost,
                Destination = ghost,
                Key = key + "/ghost/population",
                Scratch = "ghost/",
            };
        }

        /// <summary>The Sanctuary sitting on this world when it is the PLAYER's, or null - a rival's
        /// Sanctuary draws its title and its population figure and nothing that can be worked.</summary>
        private static ColonizedPlanet OwnGhost(PlanetLabel_SystemManagement label)
        {
            try
            {
                return PopulationRings.Settled(label == null ? null : label.GhostColonizedPlanet);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The markers of the ring the row is being read from, or null where the game is not
        /// drawing that ring at all.</summary>
        private static AgeTransform MarkerContainer(Ring ring)
        {
            PlanetPopulationEnumerator drawn = ring == null ? null : ring.Markers;
            return drawn == null || !drawn.Shown ? null : drawn.PopMarkersContainer;
        }

        /// <summary>Whichever of the world's two rings the card is drawing - it keeps a simple one for
        /// the ordinary view and a detailed one it swaps in under a mouse.</summary>
        private static PlanetPopulationEnumerator DrawnEnumerator(
            PlanetLabel_SystemManagement label
        )
        {
            if (label == null)
            {
                return null;
            }

            return label.PlanetPopulationEnumeratorSimple != null
                && label.PlanetPopulationEnumeratorSimple.Shown
                ? label.PlanetPopulationEnumeratorSimple
                : label.PlanetPopulationEnumeratorFocused;
        }

        /// <summary>The card's ring as rows, in the bands the ring draws them in
        /// (<see cref="PopulationRings.Add"/>). What is passed is what is the PAGE's own: which
        /// container the ring is drawn in, whose people fill it, where a drop lands, and what this
        /// page's drop does. Everything both pages that draw a ring say alike is said there.</summary>
        private static void AddPopulationSlots(
            GraphBuilder builder,
            Ring ring,
            List<Population> units,
            List<PopulationSlots.Slot> slots,
            bool canCarry
        )
        {
            Ring on = ring;
            PopulationRings.Add(
                builder,
                new PopulationRings.Ring
                {
                    Planet = ring.Card == null ? null : ring.Card.Planet,
                    Colony = ring.Colony,
                    Destination = ring.Destination,
                    Markers = MarkerContainer(ring),
                    Key = ring.Key,
                    Scratch = ring.Scratch,
                    Accepts = cargo => AcceptsPopulation(on, cargo),
                    Drop = (cargo, replaced) => DropPopulation(on, cargo, replaced),
                },
                units,
                slots,
                canCarry
            );
        }

        /// <summary>
        /// Whether this card's ring would take what is being carried, right now - the game's own
        /// answer (<see cref="PopulationMoves.Accepts"/>), which is what every population drop target
        /// on this page advertises itself by.
        ///
        /// With one thing added that the game's own check cannot know: a unit coming OUT of the
        /// SPACEPORT travels by a different route from a unit coming off another planet. The
        /// spaceport's client posts a single order that clamps against the destination's own room and
        /// never swaps (<c>SpaceportSidePanel.ApplyDrop</c> :70-80), while
        /// <c>CanWelcomeSomeOfPopulation</c> accepts a FULL planet on the strength of a swap being
        /// possible - so a full planet would advertise itself to a port-sourced carry and then move
        /// nobody. The gate has to agree with the outcome, so the room is asked here for that route
        /// alone; a planet-sourced drop keeps the game's answer untouched, because there the whole
        /// carry really does move (the surplus is swapped back).
        /// </summary>
        private static bool AcceptsPopulation(Ring ring, CarryItem held)
        {
            Population population = held == null ? null : held.Cargo as Population;
            ColonizedPlanet destination = ring.Destination;
            if (
                population == null
                || destination == null
                || !PopulationMoves.Accepts(ring.Target, population, held.Quantity)
            )
            {
                return false;
            }

            return PopulationMoves.PlanetOf(population) != null
                || PopulationMoves.OntoPlanet(destination, held.Quantity) > 0;
        }

        /// <summary>
        /// Put a carried population unit on this planet, the way the drag does it: the game's own
        /// <c>PopulationEnumerator.DragInfo</c> is filled in exactly as
        /// <c>PopulationEnumerator.OnPopulationMarkerDragStarted</c> fills it, the target's own
        /// <c>CanAcceptPopulationDrop</c> decides, and the SOURCE's own
        /// <c>IDragDropClient.ApplyDrop</c> posts the order - which is what keeps the sound the game
        /// plays and the exact order it builds. Which source that is decides which order: a unit off
        /// another planet's ring goes through the labels window
        /// (<c>OrderTransferPopulationFromPlanetToPlanet</c>), and a unit out of the spaceport through
        /// the spaceport panel (<c>OrderTransferSpaceportPopulation</c>) - the same two clients the
        /// game's own two drags use, rather than one order written twice here.
        ///
        /// <paramref name="replaced"/> is the SWAP: empty for the card's own plain add, and the
        /// affinity standing in a slot for a drop onto that slot. A planet-to-planet order carries it
        /// as its <c>PopulationToRemoveFirst</c>; a drop out of the SPACEPORT ignores it, because the
        /// spaceport's own client ignores it (<c>SpaceportSidePanel.ApplyDrop</c> :70-80 posts one
        /// order and never reads the field), and mirroring what the mouse does there means mirroring
        /// that too.
        ///
        /// The drag info is cleared again whatever happens: it is a static the game's own refresh
        /// reads every frame to draw a unit as already gone, and a stale one would empty a marker the
        /// player is still looking at.
        /// </summary>
        private static DropResult DropPopulation(Ring ring, CarryItem item, StaticString replaced)
        {
            PlanetLabel_SystemManagement label = ring.Card;
            Population population = item == null ? null : item.Cargo as Population;
            ColonizedPlanet destination = ring.Destination;
            ColonizedPlanet source = population == null ? null : SourceOf(destination, population);
            SpaceportSidePanel port =
                population == null || source != null ? null : SpaceportSource(population);
            if (destination == null || (source == null && port == null))
            {
                return DropResult.Refused(null);
            }

            try
            {
                // Out of the spaceport the port clamps against the PLANET's room and never refuses
                // (Spaceport.TransferPopulation :191); planet to planet the whole carry moves, because
                // the game swaps the surplus back rather than dropping it
                // (DepartmentOfTheInterior.TransferPopulationFromPlanetToPlanet).
                int moved = source != null
                    ? item.Quantity
                    : PopulationMoves.OntoPlanet(destination, item.Quantity);
                if (moved <= 0)
                {
                    return DropResult.Refused(null);
                }

                IDragDropClient client = source != null
                    ? (IDragDropClient)
                        Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false)
                    : port;
                if (client == null)
                {
                    return DropResult.Refused(null);
                }

                try
                {
                    PopulationMoves.Fill(
                        source != null
                            ? (ICappedPopulationOwner<Population>)source
                            : port.Spaceport,
                        population,
                        item.Quantity,
                        replaced,
                        true
                    );
                    if (!ring.Target.CanAcceptPopulationDrop())
                    {
                        return DropResult.Refused(null);
                    }

                    client.ApplyDrop(label);
                }
                finally
                {
                    PopulationMoves.Clear();
                }

                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.SystemPopulationMoved,
                        PopulationMoves.Name(population, moved),
                        AgeText.Clean(destination.LocalizedName)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: moving a population unit threw: " + e);
                return DropResult.Refused(null);
            }
        }

        /// <summary>
        /// Which planet the carried unit came off. Found rather than remembered: what is carried is
        /// the game's own <c>Population</c>, and the planet holding it is the one whose own table it
        /// is in.
        ///
        /// The destination's own system is searched first, which is the whole answer for the ordinary
        /// case and is what keeps a unit dropped back on the planet it came from a refusal rather than
        /// an order from a planet to itself. The empire-wide fall-back is for the SANCTUARY ring: a
        /// ghost colony belongs to the ghost's system and not to the one on screen, so a unit carried
        /// off it is in neither of the searched system's tables.
        /// </summary>
        private static ColonizedPlanet SourceOf(ColonizedPlanet destination, Population population)
        {
            try
            {
                ColonizedStarSystem system =
                    destination == null ? null : destination.ColonizedStarSystem;
                if (system == null || population == null)
                {
                    return null;
                }

                for (int i = 0; i < system.PlanetsColonized.Count; i++)
                {
                    ColonizedPlanet planet = system.PlanetsColonized[i];
                    if (planet == null || ReferenceEquals(planet, destination))
                    {
                        continue;
                    }

                    Population held;
                    if (
                        planet.PopulationsByAffinity.TryGetValue(population.Affinity, out held)
                        && ReferenceEquals(held, population)
                    )
                    {
                        return planet;
                    }
                }

                ColonizedPlanet elsewhere = PopulationMoves.PlanetOf(population);
                return ReferenceEquals(elsewhere, destination) ? null : elsewhere;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform StatusWidget(PlanetLabel_SystemManagement label)
        {
            AgePrimitiveLabel status = label.PlanetStatus;
            return status == null ? null : status.AgeTransform;
        }

        /// <summary>The planet cards the page is drawing, left to right. Ordered by where they are on
        /// screen rather than by the order the window pools them in, which is the model's order and
        /// runs the other way.</summary>
        private void Labels(List<PlanetLabel_SystemManagement> into)
        {
            into.Clear();
            PlanetLabelsWindow_SystemManagement window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false)
                : null;
            if (window == null)
            {
                return;
            }

            PlanetLabel_SystemManagement[] labels =
                window.GetComponentsInChildren<PlanetLabel_SystemManagement>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                // Flow control: the kept cards are sorted by rectangle and walked in that order, so a
                // card the window is not drawing would reorder the ones it is.
                if (labels[i] != null && AgeWidgets.Visible(labels[i].AgeTransform))
                {
                    into.Add(labels[i]);
                }
            }

            into.Sort(ByDrawnX);
        }

        private static readonly Comparison<PlanetLabel_SystemManagement> ByDrawnX = (left, right) =>
        {
            float a = left.AgeTransform.GetGlobalPosition().x;
            float b = right.AgeTransform.GetGlobalPosition().x;
            return a.CompareTo(b);
        };
    }
}
