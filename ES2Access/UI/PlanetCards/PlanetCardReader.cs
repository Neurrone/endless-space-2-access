using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;

namespace ES2Access.UI.PlanetCards
{
    /// <summary>
    /// ONE READER FOR EVERY PLANET CARD THIS GAME DRAWS.
    ///
    /// Three prefabs on four surfaces draw the same thing - a world, what has become of it, what has
    /// been found on it, what it makes, who lives there and what can be done about any of it - and
    /// each of them used to be read by a page holding its own list of what a card is made of. Every
    /// gap ever found on one of those surfaces was one list missing an entry another list had. So the
    /// order below is the CANONICAL one, written once: an item is read exactly where the prefab has
    /// the widget for it (<see cref="PlanetCardAdapter"/>) and the game is drawing it, and a gap
    /// closed here is closed on every surface at once.
    ///
    /// The three orders are the announcement, the review buffer and the children, and each is stated
    /// once in the method that composes it. Nothing here asks which page it is on.
    ///
    /// Every part is a <c>Func&lt;string&gt;</c> resolved when the row is read, because a card's
    /// build runs every frame for every planet on the page.
    /// </summary>
    public static class PlanetCardReader
    {
        /// <summary>
        /// One planet card, as a node standing for the PLANET with everything the card draws under it.
        ///
        /// The node is <see cref="Nodes.Synthetic"/> because it stands for the planet rather than for
        /// a widget - the walk over the drawn cards is what says the page is showing it - and it is a
        /// BUTTON exactly where the page gave it a click.
        /// </summary>
        public static void Add(GraphBuilder builder, PlanetCardAdapter card)
        {
            if (card == null || card.Planet == null)
            {
                return;
            }

            try
            {
                string key = card.Key;
                NodeVtable vtable = Vtable(card);
                ControlId id = ControlId.For(card.Planet, key);

                List<CardActions.CardAction> rename = new List<CardActions.CardAction>(1);
                CardActions.AddNamedByMod(rename, card.RenameButton, ModStrings.SystemRenamePlanet);
                List<CardActions.CardAction> state = StateAction(card);
                // The anomalies the game is drawing no technology hint on. One walk of the pooled
                // table decides which side each row falls on, so the actions and the dossiers can
                // never disagree about a row (<see cref="CardActions.AddAnomalies"/>).
                List<CardActions.CardAction> anomalies = new List<CardActions.CardAction>(4);
                List<CardActions.CardAction> buttons = Buttons(card, anomalies);
                List<CardActions.CardAction> outpost = OutpostActions(card);
                List<Population> units = new List<Population>(4);
                PopulationRings.Ring ring = WorldRing(card);
                List<PopulationSlots.Slot> slots = Slots(card, ring, units);
                List<TooltipChildren.Dossier> dossiers = Dossiers(card, anomalies);
                // Flow control: whether the card is a leaf or a group. A card whose ONLY content is a
                // Sanctuary band would otherwise be declared as a leaf and the band never walked into.
                bool ghost = AgeWidgets.Visible(card.GhostGroup);
                if (
                    rename.Count == 0
                    && state.Count == 0
                    && buttons.Count == 0
                    && outpost.Count == 0
                    && slots.Count == 0
                    && dossiers.Count == 0
                    && !ghost
                    && card.AppendChildren == null
                )
                {
                    // Synthetic: the card stands for the PLANET, and the walk over the drawn cards is
                    // what says the page is showing it.
                    builder.AddItem(Nodes.Synthetic(id, vtable));
                    return;
                }

                if (vtable.ControlType == null)
                {
                    vtable.ControlType = ControlTypes.Group;
                }

                // Synthetic for the same reason as the leaf above.
                builder.BeginGroup(Nodes.Synthetic(id, vtable));
                if (builder.IsExpanded(id))
                {
                    // THE CHILDREN, in the order the card draws them: the rename button beside the
                    // title, the population ring in the middle, the state line, the action buttons
                    // along the bottom, the outpost band, the Sanctuary band - and then, as a region
                    // of their own, the dossiers the card draws no words for at all.
                    bool canCarry = card.CanCarry != null && card.CanCarry();
                    object outer = TooltipChildren.Actions(builder, key);
                    CardActions.Emit(builder, key + "/name", rename);
                    PopulationRings.Add(builder, ring, units, slots, canCarry);
                    CardActions.Emit(builder, key + "/state", state);
                    CardActions.Emit(builder, key, buttons);
                    CardActions.Emit(builder, key + "/outpost", outpost);
                    AddGhost(builder, card, canCarry);
                    TooltipChildren.Emit(builder, key, dossiers, outer);
                    if (card.AppendChildren != null)
                    {
                        card.AppendChildren(builder);
                    }
                }

                builder.EndGroup();
            }
            catch (Exception e)
            {
                Log.Warn("planet card: reading a card threw: " + e);
            }
        }

        // ---------------------------------------------------------------- the announcement

        /// <summary>
        /// THE READOUT, in the canonical order: the planet's name, how big it is and what kind of
        /// world it is, what has become of it, whatever the card is warning about, how an outpost on
        /// it is getting on, how many curiosities are still in orbit, and whether anybody has staked
        /// it with a mining probe.
        ///
        /// Size and type are said as ONE part through the game's own template, which is how the map's
        /// card writes them - a size with no type is half a sentence, and the game already has the
        /// phrase. Where a prefab DRAWS that line the drawn words are the oracle; where it does not,
        /// the same two element titles are composed into the same template.
        ///
        /// The curiosity count and the mining probe are read off the PLANET rather than off the card:
        /// neither is drawn as words anywhere on any of the three prefabs, and both answer the
        /// question a player is walking the cards to ask.
        /// </summary>
        private static NodeVtable Vtable(PlanetCardAdapter card)
        {
            PlanetCardAdapter it = card;
            List<NodeAnnouncement> parts = new List<NodeAnnouncement>(8);
            parts.Add(GraphNodes.LabelPart(() => AgeText.Label(it.NameLabel)));
            parts.Add(GraphNodes.ValuePart(() => SizeAndTypeOf(it)));
            parts.Add(GraphNodes.ValuePart(() => StateOf(it)));
            AddIconPart(parts, card.DecayIcon, DecayLabel);
            AddIconPart(parts, card.OutpostCancelIcon, OutpostAtRiskLabel);
            AddIconPart(parts, card.HauntIcon, () => SanctuaryLabel(it));
            parts.Add(GraphNodes.ValuePart(() => UnmarkedSanctuary(it)));
            // An outpost's card ends in the game's own sentence about how it is getting on ("Colony in
            // 24 Turn"), which is drawn on the card and so is spoken, not buffered.
            parts.Add(GraphNodes.ValuePart(() => Drawn(it.OutpostBottomCaption)));
            parts.Add(GraphNodes.ValuePart(() => OutpostTimerOf(it)));
            parts.Add(GraphNodes.ValuePart(() => CuriosityCount(it.Planet, Gui.PlayerEmpire)));
            // A mining probe is a thing somebody has DONE to this planet, and the game keeps it in the
            // dossier where only a hover finds it. Said on the card so that a rival staking a world in
            // your own system is heard while walking past it.
            parts.Add(GraphNodes.ValuePart(() => MiningProbes.Line(it.Planet), false));

            NodeVtable vtable = new NodeVtable
            {
                Announcements = parts,
                Sections = GraphNodes.Sections(
                    // The card's OWN dossier is a section only where the card is the thing a mouse
                    // rests on to raise it; where the card is a panel with the dossier hung inside it,
                    // it is a child of the Tooltips region instead.
                    card.PlanetTooltipIsCardSection
                        ? GraphNodes.TooltipSection(card.PlanetTooltip)
                        : null,
                    NodeSection.Buffer(() => Details(it))
                ),
                OnActivate = card.OnActivate,
            };
            if (card.OnActivate != null)
            {
                vtable.ControlType = ControlTypes.Button;
            }

            // Where the card's own dossier is the ROW's tooltip, the pointer goes to the widget that
            // dossier hangs on - the game draws it for that widget alone and a pointer resting
            // anywhere else raises something else or nothing. Otherwise it goes to the CARD, which is
            // what puts a mouse inside the card's rectangle - the thing its hover-only widgets (the
            // detailed population ring, the Sanctuary's) are waiting for, and the window the card's
            // own declaration matches. Never at the status label: that label's sentence is the state
            // CHILD's tooltip and is read there (owner ruling 2026-09-15).
            AgeWidgets.PointAt(vtable, CardPointer(card));
            return vtable;
        }

        /// <summary>Where a mouse would rest to see what the card's own readout says it has.</summary>
        private static AgeTransform CardPointer(PlanetCardAdapter card)
        {
            AgeTooltip own = card.PlanetTooltipIsCardSection ? card.PlanetTooltip : null;
            AgeTransform anchor = own == null ? null : own.AgeTransform;
            return anchor ?? card.Root;
        }

        /// <summary>One of the card's wordless warning icons, called what the game calls the STATE it
        /// is warning about (owner ruling 2026-09-15) - the sentence behind it is the icon's own
        /// dossier, read where the player walks onto it (<see cref="AddWarningDossier"/>), because a
        /// paragraph in the middle of a readout is not something a card says at a glance.
        ///
        /// PAINTED is the gate and it has to be: every one of these carries its sentence from the
        /// PREFAB whether or not the card is showing it, so anything reading the tooltip alone would
        /// tell every player that every healthy planet was dying.</summary>
        private static void AddIconPart(
            List<NodeAnnouncement> parts,
            AgeTransform icon,
            Func<string> label
        )
        {
            if (icon == null)
            {
                return;
            }

            AgeTransform it = icon;
            parts.Add(
                // Content, and a DIFFERENT widget than the node stands on: the icon's label is a word
                // of the CARD's readout, so nothing else would stop a healthy world's card reading out
                // the warning its prefab came with.
                GraphNodes.ValuePart(() => AgeWidgets.Painted(it) ? label() : null)
            );
        }

        /// <summary>A world colonized and lost, in the game's own title for that.</summary>
        private static readonly Func<string> DecayLabel = CardActions.GameText("%PlanetLostTitle");

        /// <summary>An outpost the game is warning about - shrinking, starving, or already scheduled
        /// for decolonization. The game writes those three states as three sentences and gives the
        /// icon no title of its own, so the word is the mod's.</summary>
        private static readonly Func<string> OutpostAtRiskLabel = () =>
            ModStrings.Get(ModStrings.GalaxyOutpostAtRisk);

        /// <summary>Whose Sanctuary the haunt icon is warning about, in the game's own title for each
        /// - which is the pair of titles beside the pair of sentences the icon carries.</summary>
        private static string SanctuaryLabel(PlanetCardAdapter card)
        {
            ColonizedPlanet ghost = card.GhostColony;
            return CardActions.GameText(
                ghost != null && ghost.Empire == Gui.PlayerEmpire
                    ? "%PlanetStatusGhostTitle"
                    : "%PlanetStatusGhostByTitle"
            )();
        }

        private static readonly Func<string> SanctuaryTitle = CardActions.GameText(
            "%PlanetStatusGhostTitle"
        );

        /// <summary>The player's own Sanctuary standing on this world where the card shows NO sign of
        /// one - neither the band the system card grows along its bottom (<c>GhostGroup</c>) nor the
        /// haunt icon the map's card wears. The empire page's card is the one: its five outputs are
        /// the GHOST colony's (<c>PlanetCard.PlayerGhostColonizedPlanet</c> :148) and the prefab draws
        /// nothing at all to say so, so the world's numbers came from a colony the reading never
        /// mentioned (owner ruling 2026-09-15).
        ///
        /// Said in the game's own title for the state, and said only here: a prefab that draws the
        /// band or the icon already says it, and the two readings would collide.</summary>
        private static string UnmarkedSanctuary(PlanetCardAdapter card)
        {
            if (card.GhostGroup != null || card.HauntIcon != null)
            {
                return null;
            }

            ColonizedPlanet ghost = card.GhostColony;
            return ghost != null && ghost.Empire == Gui.PlayerEmpire ? SanctuaryTitle() : null;
        }

        /// <summary>What has become of the world: the drawn status label where the prefab has one -
        /// the drawn text is the oracle - and the model's own answer where it draws none.</summary>
        private static string StateOf(PlanetCardAdapter card)
        {
            return card.StatusLabel != null
                ? AgeText.Label(card.StatusLabel)
                : PlanetStatusText.Title(card.Planet);
        }

        private static string SizeAndTypeOf(PlanetCardAdapter card)
        {
            return card.SizeAndTypeLabel != null
                ? AgeText.Label(card.SizeAndTypeLabel)
                : SizeAndType(card.Planet, true);
        }

        private static string OutpostTimerOf(PlanetCardAdapter card)
        {
            return card.OutpostTimer == null ? null : Drawn(card.OutpostTimer);
        }

        /// <summary>How big a world is and what kind it is, in the game's own template
        /// (<c>PlanetLabel_SystemOrbital.RefreshPlanetInformation</c> writes exactly this line). An
        /// unsurveyed system's planets keep the game's own "unknown" word for the type, the way the
        /// map's card does.</summary>
        public static string SizeAndType(Planet planet, bool surveyed)
        {
            try
            {
                string size = ElementTitle(planet.Size);
                string type = surveyed
                    ? ElementTitle(planet.Type)
                    : Gui.Localize("%PlanetTypeUnknownTitle");
                return string.IsNullOrEmpty(size) || string.IsNullOrEmpty(type)
                    ? null
                    : AgeText.Clean(Gui.Localize("%PlaneSizeAndTypeFormat", size, type));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// How many curiosities are still standing in orbit, said on the card so that finding one does
        /// not mean opening every planet.
        ///
        /// Counted from the PLANET, not from the ring of icons: the ring is only drawn once the camera
        /// is in on the system, so a count taken off it told the player about a world at one zoom and
        /// nothing at another. The question the count asks is exactly the one the game asks when it
        /// fills the ring (<c>GuiPlanet.GetRemainingCuriosities</c>: every curiosity this empire's
        /// detection lets it SEE), so the number and the buttons agree - and where they briefly do
        /// not, it is because the pooled ring has not caught up with the planet yet.
        /// </summary>
        public static string CuriosityCount(Planet planet, Empire empire)
        {
            try
            {
                int count = 0;
                for (int i = 0; planet != null && i < planet.Curiosities.Count; i++)
                {
                    Curiosity curiosity = planet.Curiosities[i];
                    if (curiosity != null && curiosity.CanBeSeen(empire))
                    {
                        count++;
                    }
                }

                return count == 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.GalaxyPlanetCuriosityOne,
                        ModStrings.GalaxyPlanetCuriosities,
                        count
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A gui element's title without the engine's "cannot find" warning:
        /// <c>Gui.GetTitle</c> logs one for a missing element and the game forwards its logs to
        /// telemetry, which is not a price a readout should pay for asking.</summary>
        private static string ElementTitle(Amplitude.StaticString name)
        {
            try
            {
                Amplitude.Unity.Gui.GuiElement element = Gui.GetGuiElement(name);
                return element == null || string.IsNullOrEmpty(element.Title)
                    ? null
                    : Gui.Localize(element.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- the review buffer

        /// <summary>
        /// THE REVIEW BUFFER, in the canonical order: what the game says about the state where it
        /// says it nowhere on the card, whatever the page reads off the model for things it only
        /// draws as decoration, how worn out the world is, what it makes, what has been found on it,
        /// what it is sitting on, how an outpost on it is getting on, and who lives there.
        ///
        /// Anomalies and deposits are ONE line each rather than a line per item: they are a row of
        /// small icons on the card, read at a glance, and a line apiece turned a world with four
        /// anomalies into four trips down the buffer.
        ///
        /// WHAT KIND OF WORLD IT IS IS NOT A LINE - not its type, not its size, not what living there
        /// is like (owner ruling 2026-09-15). The card's own announcement already says "Large Arid",
        /// and what any of those words MEANS is the reference page behind it, which the card goes on
        /// offering as a dossier (<see cref="Dossiers"/>).
        ///
        /// Everything goes through <see cref="PlanetCardLines.AddLine"/>, so the same words twice -
        /// the game drawing one fact in two of these tables - are said once.
        /// </summary>
        private static IList<string> Details(PlanetCardAdapter card)
        {
            List<string> lines = new List<string>();
            try
            {
                // The state SENTENCE, only where the prefab draws no status label: where it draws one
                // the sentence is that label's own dossier and is read on the state child, which is
                // the thing a mouse would hover to raise it.
                if (card.StatusLabel == null)
                {
                    Add(lines, PlanetStatusText.Description(card.Planet));
                }

                // What the game is signalling about the world in colour alone - a juggernaut at
                // work on it, a Sanctuary on it, a world there is only one of. No widget on any of
                // the three prefabs writes a word of it, so it is read off the planet and carried by
                // every card alike (owner ruling 2026-09-15).
                PlanetSignals.Add(lines, card.Planet, Gui.PlayerEmpire);

                AddDepletion(lines, card);
                AddFidsi(lines, card);
                PlanetCardLines.AddLine(lines, PlanetCardLines.Joined(card.AnomaliesTable));
                PlanetCardLines.AddLine(
                    lines,
                    PlanetCardLines.Joined(card.DepositsGroup, card.DepositsDrawAmount)
                );
                AddImprovement(lines, card);
                AddOutpost(lines, card);
                PopulationSummary.Add(lines, card.Colony);
            }
            catch (Exception e)
            {
                Log.Warn("planet card: reading a card's details threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// How worn out the world is - a mining probe's damage, or a Craver colony eating the planet
        /// it lives on. The game draws this only while the planet is being depleted or already is, so
        /// being drawn is the gate, and it writes the state and how many turns are left on the item
        /// itself with the sentence behind them in its own tooltip.
        ///
        /// A FULLY depleted planet swaps that tooltip for an assembled dossier, whose words do not
        /// exist until the tooltip is drawn - so the state line still reads and the paragraph arrives
        /// when the player looks at it, rather than being invented here.
        /// </summary>
        private static void AddDepletion(List<string> lines, PlanetCardAdapter card)
        {
            PlanetDepletionStatusItem item = card.Depletion;
            // Content: whether the depletion state is one of the card's lines.
            if (item == null || !AgeWidgets.Visible(item.AgeTransform))
            {
                return;
            }

            PlanetCardLines.AddLine(lines, Drawn(item.Title));
            Add(lines, AgeWidgets.TooltipLines(item.Tooltip));
        }

        /// <summary>The planet's five outputs, named by the game's own property titles, in the two
        /// shapes a card draws them in. A colony's are written as numbers and read as numbers, off the
        /// colony's own simulation object. A world nobody has settled gets no numbers at all: the card
        /// hides that row and draws a table of rating pips instead. WHICH shape is drawn is each
        /// prefab's own test (<see cref="PlanetCardAdapter.FidsiDrawsNumbers"/>), because the three
        /// prefabs swap their two strips on different questions.</summary>
        private static void AddFidsi(List<string> lines, PlanetCardAdapter card)
        {
            FidsiEnumerator fidsi = card.Fidsi;
            if (fidsi == null || fidsi.FidsiProperties == null)
            {
                return;
            }

            if (!card.FidsiDrawsNumbers)
            {
                // Flow control: which of the two strips is read - and a card can be drawing neither,
                // which is what a world nobody has surveyed looks like on the map.
                if (!card.FidsiDrawsRatings)
                {
                    return;
                }

                IList<string> ratings = PlanetOutputs.Ratings(
                    card.Planet,
                    fidsi,
                    card.FidsiParameters
                );
                for (int i = 0; i < ratings.Count; i++)
                {
                    PlanetCardLines.AddLine(lines, ratings[i]);
                }

                return;
            }

            Amplitude.Unity.Simulation.SimulationObject simulation = card.FidsiSource;
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

        /// <summary>Which specialization improvement the world has, or that one is being built, or
        /// that there is none. A prefab that WRITES those words is read for them; one that draws only
        /// a picture keeps the name on the wrapper its tooltip points at, which is the only place it
        /// exists.</summary>
        private static void AddImprovement(List<string> lines, PlanetCardAdapter card)
        {
            AgeTransform widget = card.ImprovementWidget;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            if (card.ImprovementDrawsWords)
            {
                PlanetCardLines.Add(lines, widget);
                return;
            }

            PlanetCardLines.AddLine(lines, AgeWidgets.TooltipTitle(card.ImprovementTooltip));
        }

        /// <summary>
        /// The lines an OUTPOST's card carries that nothing else on it says: who owns it (a plain
        /// label the game only draws while the system is an outpost), when the next population unit
        /// arrives and which kind it will be - both of which the card draws as a bare number and a
        /// symbol, so the two sentences the game explains them with are what carries them - then the
        /// help behind the progress caption, whose own words are already spoken as the card's state,
        /// and last the sentence behind a countdown where the prefab draws one of those instead.
        /// </summary>
        private static void AddOutpost(List<string> lines, PlanetCardAdapter card)
        {
            // Content: whether the outpost's progress is among the card's lines - a colonized system
            // draws none of it.
            if (AgeWidgets.Visible(card.OutpostGroup))
            {
                PlanetCardLines.AddLine(lines, Drawn(card.OutpostOwnerLabel));
                Add(lines, AgeWidgets.TooltipLines(Tooltip(card.OutpostOwnerLabel)));

                GrowthGaugeItem growth = card.GrowthLine;
                if (growth != null)
                {
                    PlanetCardLines.AddLine(lines, Drawn(growth.TurnsBeforeNextPop));
                    Add(lines, AgeWidgets.TooltipLines(Tooltip(growth.TurnsBeforeNextPop)));
                    Add(lines, AgeWidgets.TooltipLines(Tooltip(growth.NextPopulationIcon)));
                }

                Add(lines, AgeWidgets.TooltipLines(Tooltip(card.OutpostBottomCaption)));
            }

            // The countdown says a number and nothing else; the sentence the game explains it with is
            // reviewable rather than spoken, because the card already speaks the number.
            if (card.OutpostTimer != null && AgeWidgets.Visible(card.OutpostTimer.AgeTransform))
            {
                Add(lines, AgeWidgets.TooltipLines(card.OutpostTooltip));
            }
        }

        // ---------------------------------------------------------------- the children

        /// <summary>
        /// THE STATE LINE AS A CHILD, where the prefab draws one.
        ///
        /// The status label is a drawn label with a dossier of its own - what the game says about the
        /// state, and which technology would change it - so it is the thing a mouse rests on to raise
        /// that dossier, and it gets the node (owner ruling 2026-09-15). Read as the label's words
        /// with its sentence behind them.
        ///
        /// It is a BUTTON only while the game is hinting a missing technology on it, which is the one
        /// thing that line still DOES: a Ctrl+click jumps to that technology in the research tree
        /// (<c>PlanetLabel_SystemManagement.OnClickPlanetStatusCb</c> :1626-1633). The same shape an
        /// anomaly row gets (<see cref="CardActions.AddAnomalies"/>), and for the same reason - a
        /// dead control announced on every planet is worse than no control.
        ///
        /// Gated on what the game is DRAWING (<see cref="TechnologyHints.Drawn"/>) rather than on the
        /// hint being present: <c>PlanetLabel.RefreshPlanetStatus</c> :296-310 is the only branch that
        /// fills the hint in AND the only one that clears it, and these cards are pooled, so a card
        /// rebound from a hostile world to a colonized one keeps the old technology on the component
        /// for good and the game's own Ctrl+click then jumps somewhere unrelated (measured 2026-09-14).
        /// </summary>
        private static List<CardActions.CardAction> StateAction(PlanetCardAdapter card)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(1);
            AgeTransform status = StatusWidget(card);
            if (status == null)
            {
                return found;
            }

            // And a state line with nothing behind it is not a child either: where the prefab hangs no
            // sentence on the label and wires no technology hint on it - the map's orbital card,
            // measured 2026-09-15 - the only thing the child could say is the word the row has just
            // said, which is the empty group wearing a child.
            if (card.StatusTooltip == null && card.StatusHintWidget == null)
            {
                return found;
            }

            AgePrimitiveLabel label = card.StatusLabel;
            AgeTransform hint = card.StatusHintWidget;
            CardActions.Add(
                found,
                new CardActions.CardAction
                {
                    Widget = status,
                    Label = () => AgeText.Label(label),
                    Tooltip = card.StatusTooltip,
                    Offered = NotOffered,
                    Hint = hint,
                    HintDrawn = hint == null ? null : (Func<bool>)(() => TechnologyHints.Drawn(hint)),
                    HintedOnly = true,
                }
            );
            return found;
        }

        /// <summary>The state line answers no ordinary click in any state - the game answers its click
        /// only while a Control key is physically held.</summary>
        private static readonly Func<bool> NotOffered = () => false;

        /// <summary>
        /// Which of the card's own buttons the game is drawing, in the order the card draws them.
        ///
        /// Colonize is named by a phrase of this mod's because the game draws it as a wordless icon;
        /// the two faction-specific ways of settling a world beside it, the offer to buy an outpost
        /// outright, and the three for a world that is already yours - pick a specialization
        /// improvement, reduce an anomaly, terraform - each name themselves in the sentence their own
        /// tooltip explains them with. Each is kept while DRAWN, because the game switches them off
        /// with the reason appended to that tooltip and a blocked one should refuse rather than vanish.
        ///
        /// Then the row of FLEET actions the map's card draws under the world - a juggernaut sent to
        /// terraform it, restore it, reduce an anomaly, stake it with a mining probe, destroy it -
        /// each named after the fleet action it carries out, because the game draws them as bare icons
        /// with an assembled stat block behind them and names them nowhere else; the button it swaps
        /// in while one of those is already RUNNING, named after what is being done
        /// (<see cref="InProgressName"/>); and the way into a pirate lair's diplomacy.
        ///
        /// THE TWO HALVES NEVER COEXIST ON ONE PREFAB: the colony buttons are the star system and
        /// empire cards', the fleet row and the in-progress buttons the map's. So the sequence below
        /// is each prefab's own drawn order, and the other prefab's members are simply null.
        ///
        /// The anomalies and the curiosities are rows of the card's tables that the game wired as
        /// controls, so they are children like the buttons rather than lines of the buffer.
        ///
        /// <paramref name="anomalyReadouts"/> takes the anomaly rows the game is drawing no
        /// technology hint on, which the card reads among its dossiers instead of among its actions
        /// (<see cref="CardActions.AddAnomalies"/>). A caller that passes none gets the same list
        /// minus those rows, which is the list their indices are counted in either way.
        ///
        /// PUBLIC because the map's camera-seat wait has to name the row it is waiting for by its
        /// INDEX in this very list (<c>GalaxyHudScreen.SeatRow</c>): which buttons a card draws changes
        /// with the world, so a second list written there would name a different button on the next
        /// planet. One question, one home.
        /// </summary>
        public static List<CardActions.CardAction> Buttons(
            PlanetCardAdapter card,
            List<CardActions.CardAction> anomalyReadouts = null
        )
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(8);
            try
            {
                CardActions.AddNamedByMod(found, card.ColonizeButton, ModStrings.SystemColonize);
                // The Vodyani one gets the narrower gate: its refresh sets the hint in one branch
                // (<c>PlanetLabel_SystemOrbital.RefreshVodyaniHintButton</c> :1297-1298) and - unlike
                // its two siblings - never hides the button in the other, so a rebound label can draw
                // the button and carry a technology that has nothing to do with the world under it.
                // The game writes its sentence onto this button's own tooltip, so the drawn test is
                // the right one.
                AgeTransform vodyani = AgeWidgets.Transform(card.VodyaniHintButton);
                CardActions.AddNamedByTooltip(
                    found,
                    card.VodyaniHintButton,
                    vodyani == null ? null : (Func<bool>)(() => TechnologyHints.Drawn(vodyani))
                );
                CardActions.AddNamedByTooltip(found, card.UmbralChoirHintButton);
                CardActions.AddNamedByTooltip(found, card.BuyOutpostButton);
                // The way into a minor civilization's diplomacy, drawn on a world one of them holds.
                // Its own tooltip cannot name it however real the sentence in it: the game gives that
                // one a renderer CLASS (MinorFaction, drawing the faction's panel from the tooltip's
                // Target), and a class-backed tooltip is not the readable kind, so the first line came
                // back null and the button spoke unnamed. It opens the same screen the system label's
                // diplomacy button does, so it takes the same name.
                CardActions.AddNamedByMod(
                    found,
                    card.MinorFactionButton,
                    ModStrings.GalaxySystemDiplomacy
                );
                CardActions.AddRefusableNamedByTooltip(found, card.SpecializationButton);
                CardActions.AddRefusableNamedByTooltip(found, card.ReduceAnomalyButton);
                CardActions.AddRefusableNamedByTooltip(found, card.TerraformButton);
                CardActions.AddAnomalies(found, card.AnomaliesTable, anomalyReadouts);
                CardActions.AddNamedByGame(
                    found,
                    card.TerraformationButton,
                    "%InitiateTerraformPlanetFleetActionTitle"
                );
                CardActions.AddNamedByGame(
                    found,
                    card.RestorationButton,
                    "%InitiateRestorePlanetFleetActionTitle"
                );
                CardActions.AddNamedByGame(
                    found,
                    card.AnomalyReductionButton,
                    "%InitiateReduceAnomalyFleetActionTitle"
                );
                CardActions.AddNamedByGame(
                    found,
                    card.MiningProbeButton,
                    "%LaunchMiningProbeFleetActionTitle"
                );
                CardActions.AddNamedByGame(
                    found,
                    card.DestroyButton,
                    "%DestroyPlanetFleetActionTitle"
                );
                AddInProgress(found, card.InProgressTerraformation);
                AddInProgress(found, card.InProgressRestoration);
                AddInProgress(found, card.InProgressAnomalyReduction);
                // The way into pirate diplomacy, drawn on a world whose system holds a pirate lair
                // (DLC9). The game declares the field as a plain transform and hangs a radial button
                // on it, and keeps the widget drawn while refusing a pirate-hating empire, with the
                // reason written into the same tooltip its name comes from - the refusable treatment.
                if (card.PirateLairGroup != null)
                {
                    CardActions.AddRefusable(
                        found,
                        card.PirateLairGroup,
                        CardActions.NameFromTooltip(card.PirateLairGroup)
                    );
                }

                AddCuriosities(found, card);
            }
            catch (Exception e)
            {
                Log.Warn("planet card: reading a card's buttons threw: " + e);
            }

            return found;
        }

        /// <summary>The game's own sentence for every one of the three in-progress buttons - the same
        /// one on all three because the game itself writes the same one on all three
        /// (<c>PlanetLabel_SystemOrbital</c> :818, :898, :970). It is the LAST resort for their names
        /// now (<see cref="InProgressName"/>) and stays in every one of their dossiers.</summary>
        private const string CancelJuggernautAction =
            "%PlanetCancelJuggernautActionButtonDescription";

        private static readonly Func<string> CancelJuggernautWords = CardActions.GameText(
            CancelJuggernautAction
        );

        private static void AddInProgress(
            List<CardActions.CardAction> found,
            AgeControlButton button
        )
        {
            if (button != null)
            {
                CardActions.AddNamed(found, button, InProgressName(button));
            }
        }

        /// <summary>
        /// What one of the three in-progress buttons is called: WHAT IS BEING DONE, not the fact that
        /// pressing cancels it (owner ruling 2026-08-23).
        ///
        /// A planet being terraformed while one of its anomalies is reduced draws two of these buttons
        /// at once, and the game writes the one sentence (<see cref="CancelJuggernautAction"/>) onto
        /// both - so the card offered two entries the player could not tell apart. The game does name
        /// each action, on the wrapper its own tooltip is pointing at: the terraformation's and the
        /// anomaly reduction's constructible, the restoration's fleet action
        /// (<c>PlanetLabel_SystemOrbital</c> :806-830, :885-900, :960-975 - the player-empire branch,
        /// which is the only one the collector keeps, since a rival's button is drawn switched off).
        ///
        /// Asked at SPEAK time off the tooltip the button is carrying now: the game rebinds that
        /// tooltip every refresh, and a juggernaut that finishes one action and starts another keeps
        /// the same widget. A wrapper that cannot name itself falls back to the shared sentence, which
        /// is what the button said before this rule - never to silence.
        /// </summary>
        private static Func<string> InProgressName(AgeControlButton button)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            return () =>
            {
                string title = AgeWidgets.TooltipTitle(AgeWidgets.Raw(widget));
                return string.IsNullOrEmpty(title) ? CancelJuggernautWords() : title;
            };
        }

        /// <summary>
        /// The curiosities the card is drawing, each one a wired button: a wordless icon kept
        /// CLICKABLE while refused, with the reason in its own tooltip. Named off the wrapper the game
        /// hangs on that tooltip, which is the only place the thing in orbit has a name.
        ///
        /// A card mixes several kinds of item into this one table, so the curiosity items are picked
        /// out by their own component rather than by position; the rest of the table stays a line of
        /// the card's.
        ///
        /// Admission is the gate: the table is pooled, so a card showing fewer curiosities than the
        /// one read before it keeps the surplus items <c>Visible</c> at alpha 0 - and a retired item
        /// has had its tooltip unbound, so it has no name either.
        /// </summary>
        private static void AddCuriosities(
            List<CardActions.CardAction> found,
            PlanetCardAdapter card
        )
        {
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(card.CuriositiesTable);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                if (item != null && IsCuriosity(item))
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
        /// it does, how long it takes and what it costs all live in the wrapper on its own tooltip -
        /// so that wrapper's title is what the node is called and the tooltip is the dossier behind
        /// it. An action the faction cannot have at all the game hides outright; one it is merely
        /// refusing today stays drawn and switched off, and is declared refusing with the game's own
        /// reason.
        ///
        /// The strip is POOLED, so a tick is admitted on the drawing test rather than on the
        /// visibility flag a retired row keeps: an outpost offering fewer actions than the one read
        /// before it would otherwise declare the surplus ticks, still wearing the other outpost's
        /// name - and renumber the real ones.
        /// </summary>
        private static List<CardActions.CardAction> OutpostActions(PlanetCardAdapter card)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            try
            {
                // Flow control: whether the outpost's action list is collected at all - the actions
                // below are NUMBERED by their place in it.
                if (!AgeWidgets.Visible(card.OutpostGroup))
                {
                    return found;
                }

                AgeTransform table = card.OutpostActionsTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    // A row the pool has retired - faded as a ROW while its tick stays at alpha 1 - is
                    // dropped by the collector's own admission filter, which walks the tick's ancestry
                    // and so sees the faded row above it (CardActions.AddToggle).
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
                    card.DecolonizeToggle,
                    CardActions.GameText("%PlanetDecolonizeTitle"),
                    null
                );
            }
            catch (Exception e)
            {
                Log.Warn("planet card: reading an outpost card's actions threw: " + e);
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

        // ---------------------------------------------------------------- the dossiers

        /// <summary>
        /// THE TOOLTIPS REGION, in the canonical order: the planet's own dossier, one per output
        /// figure where the surface declares them, one per warning the card is showing, one per
        /// anomaly the game offers nothing to do about, one per
        /// deposit the world is sitting on, the specialization's, and last the reference pages behind
        /// the words the card draws for what kind of world it is. The warnings come early because they
        /// are the card's STATE - what is happening to this world now.
        ///
        /// A card draws each of these as a picture or a bare figure and keeps everything about what it
        /// MEANS behind a hover, so the buffer carries the captioned figures and this is the page
        /// behind each one.
        ///
        /// The card keeps TWO output strips and swaps them, with the other one left bound to whatever
        /// it last showed, so the strip is taken from whichever is DRAWN and the resolver drops the
        /// pips of the hidden one.
        /// </summary>
        private static List<TooltipChildren.Dossier> Dossiers(
            PlanetCardAdapter card,
            List<CardActions.CardAction> anomalies
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(8);
            try
            {
                if (!card.PlanetTooltipIsCardSection)
                {
                    TooltipChildren.Add(found, card.PlanetTooltip);
                }

                if (card.DeclareFidsiDossiers)
                {
                    TooltipChildren.AddInside(found, card.FidsiScoreTable);
                    TooltipChildren.AddInside(
                        found,
                        card.Fidsi == null ? null : card.Fidsi.AgeTransform
                    );
                }

                AddWarningDossiers(found, card);
                AddAnomalyDossiers(found, anomalies);
                AddDepositDossiers(found, card);
                // Content: which dossiers the card offers. These become a region of the card's own
                // node, not nodes the gate ever sees.
                AgeTransform improvement = card.ImprovementWidget;
                if (AgeWidgets.Visible(improvement))
                {
                    TooltipChildren.Add(found, card.ImprovementTooltip, improvement);
                    TooltipChildren.AddPlain(found, card.ImprovementTooltip, improvement);
                }

                // The reference pages behind the two words the card draws for what kind of world this
                // is. The game writes the element's own Description onto each of those labels' own
                // tooltips, so each is a hover surface of its own and gets a node of its own - the
                // card's line says WHICH type and size, and these say what that means. A label the
                // prefab never draws is dropped by the door's own drawing test.
                TooltipChildren.AddPlain(found, Transform(card.TypeLabel));
                TooltipChildren.AddPlain(found, Transform(card.SizeLabel));
                AddGameplayTypeDossiers(found, card);
            }
            catch (Exception e)
            {
                Log.Warn("planet card: reading a card's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// The page behind each of the card's wordless warning icons - which is where the sentence the
        /// game wrote for it is read, now that the readout says the STATE's own name and not the
        /// paragraph (owner ruling 2026-09-15). Named by that same word, so the ear meets on the icon
        /// what the readout already said the world has.
        ///
        /// PAINTED is the gate, the readout's own: every one of these carries its sentence from the
        /// PREFAB whether or not the card is showing it, so a card that declared them unasked would
        /// offer every healthy world a page about dying.
        /// </summary>
        private static void AddWarningDossiers(
            List<TooltipChildren.Dossier> found,
            PlanetCardAdapter card
        )
        {
            PlanetCardAdapter it = card;
            AddWarningDossier(found, card.DecayIcon, DecayLabel);
            AddWarningDossier(found, card.OutpostCancelIcon, OutpostAtRiskLabel);
            AddWarningDossier(found, card.HauntIcon, () => SanctuaryLabel(it));
        }

        private static void AddWarningDossier(
            List<TooltipChildren.Dossier> found,
            AgeTransform icon,
            Func<string> label
        )
        {
            // Content: whether this warning is one of the card's pages. A dossier is a region of the
            // card's own node, not a node the gate ever sees.
            if (icon != null && AgeWidgets.Painted(icon))
            {
                TooltipChildren.AddPlain(found, AgeWidgets.Raw(icon), icon, label);
            }
        }

        /// <summary>The page behind each anomaly the game is drawing no technology hint on - the
        /// paragraph, the effects and what reducing it would take. The row carries that page and
        /// nothing else there (<see cref="CardActions.AddAnomalies"/>), so it is a dossier of the
        /// card's rather than a control that would announce a role it cannot play; the map's own
        /// planet rows say the same thing the same way where no card is drawn at all
        /// (<c>PlanetRows.AddAnomalyDossiers</c>).
        ///
        /// The pointer goes to the tooltip's OWN widget, which is what the row's node aimed at while
        /// it was an action, and the node's existence is gated on the ROW the walk was holding - a
        /// pooled table retires an item by fading it, and only the widget the walk held answers.</summary>
        private static void AddAnomalyDossiers(
            List<TooltipChildren.Dossier> found,
            List<CardActions.CardAction> anomalies
        )
        {
            for (int i = 0; anomalies != null && i < anomalies.Count; i++)
            {
                CardActions.CardAction anomaly = anomalies[i];
                TooltipChildren.Add(found, anomaly.Tooltip, null, null, null, anomaly.Widget);
            }
        }

        /// <summary>
        /// The page behind each deposit the world is sitting on - what the resource is for, who can
        /// work it and what is stopping them.
        ///
        /// The item draws a picture and a figure, and everything else about the resource is a dossier
        /// the renderer assembles from the wrapper it binds, so the card's line says which resource
        /// and how much of it, and this is where the rest of it is read. The pooled table's retired
        /// items keep the PREVIOUS planet's wrapper on their tooltip, so each item is asked the gate's
        /// own drawing test at ADMISSION - the one place early enough to stop a ghost winning the
        /// dedupe, which the shared door cannot ask for every caller.
        ///
        /// <see cref="PlanetCardAdapter.DepositCarrier"/> is the page that keeps a carrier of its own
        /// for a deposit the game is drawing no item for; where the page has none, a deposit with no
        /// drawn item simply has no dossier.
        /// </summary>
        private static void AddDepositDossiers(
            List<TooltipChildren.Dossier> found,
            PlanetCardAdapter card
        )
        {
            AgeTransform group = card.DepositsGroup;
            // Content: whether the deposits contribute dossiers at all - they become a region of the
            // card's node rather than nodes of their own.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            IList<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                bool drawn = child != null && NodeGate.StillDrawn(child);
                ResourceDepositItem item =
                    drawn ? child.GetComponent<ResourceDepositItem>() : null;
                if (item != null)
                {
                    TooltipChildren.Add(found, item.Tooltip, child);
                }
                else if (card.DepositCarrier != null)
                {
                    TooltipChildren.Add(found, card.DepositCarrier(i));
                }
            }
        }

        /// <summary>
        /// The reference page behind each of the things the game says living on this world is like.
        /// <c>PlanetGameplayTypeItem.Bind</c> writes the element's own Description onto the item's
        /// tooltip, so each drawn item is a hover surface of its own; the card's line already says
        /// which they are.
        ///
        /// NAMED by the same reading that names the card's line (<see cref="AgeWidgets.ItemText"/>),
        /// because the naming ladder cannot reach these words: a card draws these items as icons with
        /// their captions FADED (<c>docs/planets.md</c> - the caption sits at alpha 0 while the item
        /// paints), so every drawn-text rung answers nothing and the entry fell through to reading its
        /// own paragraph as its name (measured 2026-09-15: "Temperate" and "Fertile" announced as the
        /// whole sentence). One question, one home: the line and the entry ask the same reader.
        /// </summary>
        private static void AddGameplayTypeDossiers(
            List<TooltipChildren.Dossier> found,
            PlanetCardAdapter card
        )
        {
            AgeTransform table = card.GameplayTypeTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> children = table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                PlanetGameplayTypeItem item =
                    child == null ? null : child.GetComponent<PlanetGameplayTypeItem>();
                if (item != null && item.Tooltip != null)
                {
                    AgeTransform it = child;
                    TooltipChildren.AddPlain(
                        found,
                        item.Tooltip,
                        AgeWidgets.TooltipOwner(item.Tooltip) ?? child,
                        () => AgeWidgets.ItemText(it)
                    );
                }
            }
        }

        // ---------------------------------------------------------------- the rings

        /// <summary>The card's own ring - the world's population. <see cref="Rings"/> is the shared
        /// walk; what is passed is what is the CARD's: which container the ring is drawn in, whose
        /// people fill it, where a drop lands, and what the page's drop does.</summary>
        private static PopulationRings.Ring WorldRing(PlanetCardAdapter card)
        {
            return new PopulationRings.Ring
            {
                Planet = card.Planet,
                Colony = card.Colony,
                Destination = card.PlayerColony,
                Markers = MarkerContainer(card.RingMarkers),
                Key = card.Key + "/population",
                Scratch = card.RingScratch,
                Accepts = card.Accepts,
                Drop = card.Drop,
            };
        }

        /// <summary>The Sanctuary's ring, which the game draws only for a ghost colony of the
        /// PLAYER's and runs through the card's own drag client.</summary>
        private static PopulationRings.Ring GhostRing(PlanetCardAdapter card)
        {
            ColonizedPlanet ghost = card.PlayerGhostColony;
            return new PopulationRings.Ring
            {
                Planet = card.Planet,
                Colony = ghost,
                Destination = ghost,
                Markers = MarkerContainer(card.GhostRingMarkers),
                Key = card.Key + "/ghost/population",
                Scratch = card.RingScratch + "ghost/",
                Accepts = card.GhostAccepts,
                Drop = card.GhostDrop,
            };
        }

        /// <summary>
        /// The SLOTS of a colony's population ring - the card's middle, read as the ring is drawn
        /// rather than as the model is stored.
        ///
        /// Contents from the model, existence from the drawing. The detailed ring the markers' own
        /// tooltips hang on is only shown under a mouse, so reading a slot's affinity off a marker
        /// would answer nothing while the player is on the keyboard - and equally, a card the game is
        /// drawing no ring on has no slots to offer, whatever the model says the planet could hold.
        /// </summary>
        private static List<PopulationSlots.Slot> Slots(
            PlanetCardAdapter card,
            PopulationRings.Ring ring,
            List<Population> units
        )
        {
            return PopulationMoves.Slots(
                card.Planet,
                ring.Colony,
                card.RingMarkers,
                AgeWidgets.DrawnCount(ring.Markers),
                units
            );
        }

        /// <summary>The markers of a ring, or null where the game is not drawing that ring at
        /// all.</summary>
        private static AgeTransform MarkerContainer(PlanetPopulationEnumerator drawn)
        {
            return drawn == null || !drawn.Shown ? null : drawn.PopMarkersContainer;
        }

        // ---------------------------------------------------------------- the Sanctuary band

        /// <summary>
        /// The SANCTUARY band the card grows along its bottom when a ghost colony is sitting on this
        /// world (<c>PlanetLabel_SystemManagement.RefreshGhostStatus</c> :1192-1250), read in the
        /// order the game draws it: the band's own title, then the Sanctuary's population ring, then
        /// the button that turns one of its people into a sleeper.
        ///
        /// The band is drawn for a RIVAL's Sanctuary too - the group's only test is that the ghost
        /// exists and that the player can see its system (:1194) - and what a rival's draws is the
        /// title and the population figure alone: the game hides the ring, the outputs and the button
        /// for anybody else's (:1217, :1229). So everything below is gated on its own drawn flag and a
        /// rival's band simply reads shorter, with no ownership test written here.
        ///
        /// The title is the band's line and carries the figures the band draws no words for: the
        /// population count, which the game writes as a bare "3/5" beside a symbol, and the five
        /// outputs, which are the same strip of pips the card reads for the world itself. Its own
        /// tooltip is what the game says about the Sanctuary - whose it is, and, for a rival's, how it
        /// could be got rid of.
        ///
        /// THE SANCTUARY'S RING IS HOVER-ONLY (measured 2026-08-29): the game shows the ring and the
        /// outputs strip while the pointer is inside the band's own rectangle and hides them again on
        /// the way out (:648-693), and unlike the world's ring there is no simple one drawn
        /// underneath. So the slots exist exactly while the game draws them, which is the rule every
        /// other row here follows - and it works out, because landing on the band's title is what puts
        /// the pointer inside the band, so the ring is there by the time the player steps down into
        /// it, exactly as it is there for a mouse that has hovered the band.
        ///
        /// CONTENT IS UNVERIFIED (<c>docs/planets.md</c>): a Sanctuary needs a player empire that HAS
        /// ghost systems - the Umbral Choir, a Penumbra faction chosen at new-game time - and no save
        /// in this repo is one, so the band was measured by lending the card a colony and showing the
        /// group. What that proves is the STRUCTURE - which widgets are declared, what the reader
        /// makes of each - and never what a real ghost would say in them.
        /// </summary>
        private static void AddGhost(GraphBuilder builder, PlanetCardAdapter card, bool canCarry)
        {
            try
            {
                // Flow control: whether the band is walked at all. The group is a wired prefab field
                // and so always there; what says a Sanctuary exists is the game drawing it.
                if (!AgeWidgets.Visible(card.GhostGroup))
                {
                    return;
                }

                PlanetCardAdapter it = card;
                string key = card.Key + "/ghost";
                AgeTransform title = Transform(card.GhostTitle);
                if (title != null)
                {
                    AgePrimitiveLabel label = card.GhostTitle;
                    NodeVtable vtable = GraphNodes.Readout(
                        () => AgeText.Label(label),
                        null,
                        () => GhostDetails(it),
                        AgeWidgets.Raw(title)
                    );
                    AgeWidgets.PointAt(vtable, title);
                    builder.AddItem(Nodes.Drawn(ControlId.For(title, key), vtable, title));
                }

                PopulationRings.Ring ring = GhostRing(card);
                List<Population> units = new List<Population>(4);
                List<PopulationSlots.Slot> slots = PopulationMoves.Slots(
                    card.Planet,
                    ring.Colony,
                    card.GhostRingMarkers,
                    AgeWidgets.DrawnCount(ring.Markers),
                    units
                );
                PopulationRings.Add(builder, ring, units, slots, canCarry);

                // The one thing the band can DO, and a standard refusable card action: the game keeps
                // it drawn and switched off with its reason written into its own tooltip by the game's
                // own failure formatter (:1229-1249), so it is declared while drawn and offered while
                // the game offers it, named by the sentence that explains it - the game hangs plain
                // content there with no wrapper and no header line, so asking for a title answered
                // nothing and the row announced itself role-first.
                List<CardActions.CardAction> traitor = new List<CardActions.CardAction>(1);
                CardActions.AddRefusable(
                    traitor,
                    card.GhostTraitorButton,
                    CardActions.NameFromTooltip(card.GhostTraitorButton)
                );
                CardActions.Emit(builder, key, traitor);
            }
            catch (Exception e)
            {
                Log.Warn("planet card: reading a card's Sanctuary band threw: " + e);
            }
        }

        /// <summary>The figures the Sanctuary band draws with no words of its own: how many people
        /// live there out of how many could, and the five outputs the ghost colony is making. The
        /// count the game writes as "3/5" beside a symbol, so it is composed as the fraction it is
        /// with the game's own word for the symbol; the outputs are read exactly as the card's own
        /// strip is.</summary>
        private static IList<string> GhostDetails(PlanetCardAdapter card)
        {
            List<string> lines = new List<string>(6);
            try
            {
                ColonizedPlanet ghost = card.GhostColony;
                if (ghost == null)
                {
                    return lines;
                }

                AgePrimitiveLabel count = card.GhostPopulationCount;
                // Content, and of a DIFFERENT widget than the node stands on: these are lines of the
                // title's buffer, so the gate never sees them and nothing else would stop a rival's
                // hidden figure being read out.
                if (count != null && AgeWidgets.Visible(count.AgeTransform))
                {
                    PlanetCardLines.AddLine(
                        lines,
                        ModStrings.Format(
                            ModStrings.FractionUnit,
                            ghost.PopulationCount,
                            ghost.MaxPopulation,
                            AgeText.Clean(PopulationSummary.PopulationIcon)
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
                FidsiEnumerator fidsi = card.GhostFidsi;
                if (
                    fidsi == null
                    || fidsi.FidsiProperties == null
                    || card.PlayerGhostColony == null
                )
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
                Log.Warn("planet card: reading a Sanctuary's figures threw: " + e);
            }

            return lines;
        }

        // ---------------------------------------------------------------- shared reading

        /// <summary>The widget the card draws its state in, which is what the state child stands on
        /// and where the card's pointer goes.</summary>
        private static AgeTransform StatusWidget(PlanetCardAdapter card)
        {
            return Transform(card.StatusLabel);
        }

        private static AgeTransform Transform(AgePrimitiveLabel label)
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

        /// <summary>A drawn-but-blank label answers null here rather than the empty string
        /// <see cref="AgeWidgets.DrawnLabel"/> keeps the two cases apart with.</summary>
        private static string Drawn(AgePrimitiveLabel label)
        {
            string drawn = AgeWidgets.DrawnLabel(label);
            return string.IsNullOrEmpty(drawn) ? null : drawn;
        }

        /// <summary>A table item the card offers as a button of its own, and so is not a line of the
        /// card's - the curiosities the game mixes into the findings table.</summary>
        private static bool IsCuriosity(AgeTransform item)
        {
            try
            {
                return item != null && item.GetComponent<PlanetCuriosityItem>() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Add(List<string> lines, Func<IList<string>> source)
        {
            if (source == null)
            {
                return;
            }

            try
            {
                Add(lines, source());
            }
            catch (Exception) { }
        }

        private static void Add(List<string> lines, IList<string> from)
        {
            for (int i = 0; from != null && i < from.Count; i++)
            {
                PlanetCardLines.AddLine(lines, from[i]);
            }
        }
    }
}
