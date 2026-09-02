using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The planet cards a system row slides out, and the carrying of a population unit
    /// between them and to another system.</summary>
    public sealed partial class EmpireScreen
    {
        /// <summary>
        /// The planets of the selected system, left to right - which is NOT the order the panel holds
        /// them in: it lays its cards out from the right, so the system's first planet is the rightmost
        /// card. Measured rather than assumed.
        ///
        /// The card is a readout with its own buttons as child nodes, the same shape the star system
        /// page's cards have, and its population ring is a row per SLOT that a unit can be carried off
        /// or dropped onto - the same gesture and the same shared arithmetic the star system page's
        /// ring uses, through this panel's own drop client.
        /// </summary>
        private void BuildCards(GraphBuilder builder, StarSystemPlanetCardsPanel panel)
        {
            _cards.Clear();
            try
            {
                PlanetCard[] all = panel.GetComponentsInChildren<PlanetCard>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    // The kept cards are SORTED by rectangle below and read in that order, so a card
                    // the panel is not drawing must not be in the list - its stale rectangle would
                    // reorder the ones that are.
                    if (all[i] != null && AgeWidgets.Visible(all[i].AgeTransform) && all[i].Planet != null)
                    {
                        _cards.Add(all[i]);
                    }
                }

                _cards.Sort(LeftToRight);
                for (int i = 0; i < _cards.Count; i++)
                {
                    AddCard(builder, _cards[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading the planet cards threw: " + e);
            }
        }

        private static readonly Comparison<PlanetCard> LeftToRight = (left, right) =>
            left.AgeTransform.GetGlobalPosition().x.CompareTo(
                right.AgeTransform.GetGlobalPosition().x
            );

        private void AddCard(GraphBuilder builder, PlanetCard card)
        {
            PlanetCard it = card;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetNameLabel)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetTypeLabel)),
                    // The same sentence the map's planet rows say, for the same reason: the game keeps
                    // a mining probe in the planet's dossier, where only a hover finds it.
                    GraphNodes.ValuePart(() => MiningProbes.Line(it.Planet), false),
                },
                Sections = GraphNodes.Sections(
                    () => CardDetails(it),
                    AgeWidgets.Raw(it.AgeTransform)
                ),
            };
            // The card itself takes NO drop: the drop lives on its SLOTS, for the reason the star
            // system page's cards do (owner ruling 2026-08-29) - a header that also swallowed drops
            // made two rows out of one gesture.
            AgeWidgets.PointAt(vtable, card.AgeTransform);

            string key = "empire:planet/" + card.Planet.GUID;
            ControlId id = ControlId.For(card.Planet, key);
            List<CardActions.CardAction> buttons = CardButtons(card);
            List<Population> units = new List<Population>(4);
            List<PopulationSlots.Slot> slots = CardSlots(card, units);
            if (buttons.Count == 0 && slots.Count == 0)
            {
                // Synthetic: the card stands for the PLANET, and the walk that found the planet is
                // what vouches for it.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above: the card stands for the planet.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                AddPopulations(builder, key, card, units, slots, CanCarry(card));
                CardActions.Emit(builder, key, buttons);
            }

            builder.EndGroup();
        }

        /// <summary>Which of the card's own buttons the game is drawing. Only colonizing has a word of
        /// the mod's - it is the same wordless button the star system page draws and the same word is
        /// used for it; the other three name themselves in the sentence they explain themselves with.
        /// </summary>
        private static List<CardActions.CardAction> CardButtons(PlanetCard card)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(2);
            try
            {
                CardActions.AddNamedByMod(found, card.ColonizeButton, ModStrings.SystemColonize);
                // Refusable, not merely named: the game keeps these three drawn while switched off,
                // with the reason appended to the naming tooltip, so a blocked one is declared
                // refusing rather than vanishing.
                CardActions.AddRefusableNamedByTooltip(found, card.BuildInfrastructureButton);
                CardActions.AddRefusableNamedByTooltip(found, card.ReduceAnomalyButton);
                CardActions.AddRefusableNamedByTooltip(found, card.TerraformButton);
                AddCuriosities(found, card);
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading a planet card's buttons threw: " + e);
            }

            return found;
        }

        /// <summary>The curiosities the card is drawing, each a wordless icon kept CLICKABLE while
        /// refused with the reason in its own tooltip, named off the wrapper the game hangs there.
        /// </summary>
        private static void AddCuriosities(List<CardActions.CardAction> found, PlanetCard card)
        {
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(card.CuriosityItemsTable);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                // The collected actions are NUMBERED by their place in the list CardActions.Emit
                // builds, and the number is each node's structural key - so an item the card is not
                // drawing must never enter it.
                if (
                    item != null
                    && AgeWidgets.Visible(item)
                    && item.GetComponent<PlanetCuriosityItem>() != null
                )
                {
                    CardActions.AddRefusable(found, item, CardActions.TitleOf(item));
                }
            }
        }

        /// <summary>Everything else the card draws, in the order it draws it: what kind of world it is,
        /// what has been found on it, and its five outputs.</summary>
        private static IList<string> CardDetails(PlanetCard card)
        {
            List<string> lines = new List<string>();
            try
            {
                AddWidgetLines(lines, card.PlanetTypeGroup);
                AddWidgetLines(lines, card.PlanetGameplayTypesTable);
                AddWidgetLines(lines, card.ResourceDepositItemsTable);
                AddWidgetLines(lines, card.AnomalyItemsTable);
                AddFidsi(lines, card);
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading a planet card's details threw: " + e);
            }

            return lines;
        }

        /// <summary>The planet's five outputs, named by the game's own property titles, in the two
        /// shapes the card draws them in. A COLONY's are written as numbers and read as numbers, off
        /// the same simulation object the card reads them from. A world nobody has settled gets no
        /// numbers at all: the card hides that row and draws a table of rating pips instead
        /// (<c>PlanetCard.Bind</c> :231-242, <c>RefreshScoreLine</c> :395-402), which the map's card
        /// and the management page's do too, so the lines of both shapes are composed for all three
        /// in <see cref="PlanetOutputs"/>. Which shape is drawn is the game's own bind-time test -
        /// settled, or a colonization the player has already ordered - so it is the test here rather
        /// than the card's own <c>ColonizedPlanet</c>: a world with a colonization pending is drawn
        /// with the numbers of the colony it is about to be, and that field is still null for it.
        /// </summary>
        private static void AddFidsi(List<string> lines, PlanetCard card)
        {
            FidsiEnumerator fidsi = card.FidsiEnumerator;
            if (fidsi == null || fidsi.FidsiProperties == null || card.Planet == null)
            {
                return;
            }

            if (card.Planet.ColonizedPlanet == null && card.PlayerGhostColonizedPlanet == null)
            {
                IList<string> ratings = PlanetOutputs.Ratings(
                    card.Planet,
                    fidsi,
                    card.FidsiParametersGuiElement
                );
                for (int i = 0; i < ratings.Count; i++)
                {
                    lines.Add(ratings[i]);
                }

                return;
            }

            ColonizedPlanet colony = card.ColonizedPlanet;
            Amplitude.Unity.Simulation.SimulationObject simulation =
                colony != null ? colony.SimulationObject : card.Planet.SimulationObject;
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

        /// <summary>The SLOTS of the ring the card draws in its population mode - contents from the
        /// colony, existence from the drawing, off the arithmetic both pages share
        /// (<see cref="PopulationMoves.Slots"/>).</summary>
        private static List<PopulationSlots.Slot> CardSlots(PlanetCard card, List<Population> units)
        {
            try
            {
                return PopulationMoves.Slots(
                    card.Planet,
                    card.ColonizedPlanet,
                    card.PlanetCardPopulationEnumerator,
                    AgeWidgets.DrawnCount(MarkerContainer(card)),
                    units
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading a planet card's population slots threw: " + e);
                return new List<PopulationSlots.Slot>();
            }
        }

        /// <summary>The container the card draws its population ring in - the card has a single ring,
        /// unlike the star system page's card, which swaps between a simple one and a detailed one.
        /// Whether the game is drawing it at all is not asked here: the readers this is handed to ask
        /// it of the container themselves (<see cref="AgeWidgets.DrawnCount"/>,
        /// <see cref="AgeWidgets.DrawnChildren"/>).</summary>
        private static AgeTransform MarkerContainer(PlanetCard card)
        {
            PlanetPopulationEnumerator enumerator =
                card == null ? null : card.PlanetCardPopulationEnumerator;
            return enumerator == null
                ? null
                : enumerator.PopMarkersContainer ?? enumerator.AgeTransform;
        }

        /// <summary>
        /// A row per SLOT of the ring the card draws, in the three bands it draws them in
        /// (<see cref="PopulationRings.Add"/>) - the same rows the star system page's cards offer,
        /// off the same walk, so a ring reads the same way wherever the game draws it. This page
        /// supplies only what is its own: which container the ring is drawn in, whose people fill it,
        /// where a drop lands, and what its own drop does.
        ///
        /// It was a row per AFFINITY until 2026-08-29, which said who lived on the world and nothing
        /// about how much room there was - the question the ring is on the card to answer - and gave a
        /// player no way to hear that the first marker of a run carries five people and the last one.
        ///
        /// <paramref name="canCarry"/> is where the game would let a drag start off this card AND this
        /// page has somewhere to put the unit down.
        /// </summary>
        private static void AddPopulations(
            GraphBuilder builder,
            string keyPrefix,
            PlanetCard card,
            List<Population> units,
            List<PopulationSlots.Slot> slots,
            bool canCarry
        )
        {
            PlanetCard it = card;
            PopulationRings.Add(
                builder,
                new PopulationRings.Ring
                {
                    Planet = card == null ? null : card.Planet,
                    Colony = card == null ? null : card.ColonizedPlanet,
                    Destination = Settled(card),
                    Markers = MarkerContainer(card),
                    Key = keyPrefix + "/population",
                    // A namespace of this page's own: the star system page parks a carrier per slot
                    // of the same world's ring, and one key for both pages would hand this card's
                    // slot 1 whatever that page's slot 1 was last bound with.
                    Scratch = "empire/",
                    Accepts = cargo => Accepts(it, cargo),
                    Drop = (cargo, replaced) => DropOnCard(it, cargo, replaced),
                },
                units,
                slots,
                canCarry
            );
        }

        // ---- moving a population unit ----

        /// <summary>The colony this card is for, or null - the card of an unsettled world, or of somebody
        /// else's colony, is neither a source nor a target.</summary>
        private static ColonizedPlanet Settled(PlanetCard card)
        {
            try
            {
                return PopulationRings.Settled(card == null ? null : card.ColonizedPlanet);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether a unit can be picked up off this card.
        ///
        /// Two questions, both the game's. Would the drag START here: the markers are interactive only
        /// while the system may move population at all (<c>PlanetPopulationEnumerator.Bind</c> :46) and the
        /// drag itself is refused unless the colony is the player's and may move population
        /// (<c>PopulationEnumerator.OnPopulationMarkerDragStarted</c> :240-252). And is there anywhere on
        /// THIS page to put it: another colony of the same system - the cards beside this one - or another
        /// colonized system in the table, which the source system's spaceport ships it to
        /// (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :167-205 does both).
        /// </summary>
        private static bool CanCarry(PlanetCard card)
        {
            try
            {
                ColonizedPlanet colony = Settled(card);
                PlanetPopulationEnumerator markers =
                    card == null ? null : card.PlanetCardPopulationEnumerator;
                AgeTransform container = markers == null ? null : markers.PopMarkersContainer;
                if (
                    colony == null
                    || !colony.CanMovePopulation
                    || !AgeWidgets.Operable(container)
                )
                {
                    return false;
                }

                return PopulationMoves.OnEmpirePage(colony.ColonizedStarSystem);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether this card would take the carried unit - the game's own
        /// <c>PlanetPopulationEnumerator.CanAcceptPopulationDrop</c>, asked through the render-safe fill
        /// (<see cref="PopulationMoves.Accepts"/>) rather than re-derived here.
        ///
        /// It used to be a copy of that check's three clauses with the quantity hardcoded to one, which
        /// is exactly the shape that goes wrong when the carry grows a quantity: the copy would still be
        /// asking whether ONE unit fits while the player held three. The game's own answer is asked
        /// instead, and it is what keeps the card the unit is being carried OFF from advertising itself.
        /// </summary>
        private static bool Accepts(PlanetCard card, CarryItem held)
        {
            Population population = held == null ? null : held.Cargo as Population;
            return population != null
                && Settled(card) != null
                // This page's drop moves people from one PLANET to another (the panel's own
                // transfer), so a unit with no planet behind it - one waiting in a spaceport - is
                // refused by the drop and must therefore be refused by the gate as well. Not
                // reachable today, since a carry ends when the player leaves the page it started on,
                // but the two halves have to answer the same question whether or not anything asks.
                && PopulationMoves.PlanetOf(population) != null
                && PopulationMoves.Accepts(
                    card.PlanetCardPopulationEnumerator,
                    population,
                    held.Quantity
                );
        }

        /// <summary>
        /// Put a carried unit on this card, the way the game's own drag does it: its
        /// <c>PopulationEnumerator.DragInfo</c> is filled in exactly as
        /// <c>OnPopulationMarkerDragStarted</c> fills it, the target card's own
        /// <c>CanAcceptPopulationDrop</c> decides, and the panel's own
        /// <c>TransferDraggedPopulationToPlanet</c> posts the order - which is what keeps the exact
        /// <c>OrderTransferPopulationFromPlanetToPlanet</c> the game builds
        /// (<c>StarSystemPlanetCardsPanel</c> :245-260, private, so it is reached by reflection).
        ///
        /// The drag info is cleared whatever happens: it is a static the game's own refresh reads every
        /// frame to draw a unit as already gone, and a stale one would empty a marker the player is
        /// still looking at.
        /// </summary>
        private static DropResult DropOnCard(
            PlanetCard card,
            CarryItem item,
            StaticString replaced
        )
        {
            Population population = item == null ? null : item.Cargo as Population;
            ColonizedPlanet destination = Settled(card);
            ColonizedPlanet source = Owner(population);
            PlanetPopulationEnumerator markers =
                card == null ? null : card.PlanetCardPopulationEnumerator;
            StarSystemPlanetCardsPanel panel = Cards(card);
            if (
                population == null
                || destination == null
                || source == null
                || markers == null
                || panel == null
            )
            {
                return DropResult.Refused(null);
            }

            try
            {
                try
                {
                    PopulationMoves.Fill(
                        source,
                        population,
                        item.Quantity,
                        replaced,
                        true
                    );
                    if (!markers.CanAcceptPopulationDrop())
                    {
                        return DropResult.Refused(null);
                    }

                    if (!Transfer(panel, destination))
                    {
                        return DropResult.Refused(null);
                    }
                }
                finally
                {
                    PopulationMoves.Clear();
                }

                // Planet to planet the whole carry moves: the game swaps the surplus back rather than
                // dropping it (DepartmentOfTheInterior.TransferPopulationFromPlanetToPlanet).
                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.SystemPopulationMoved,
                        PopulationMoves.Name(population, item.Quantity),
                        AgeText.Clean(destination.LocalizedName)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: moving a population unit threw: " + e);
                return DropResult.Refused(null);
            }
        }

        /// <summary>Hands the carried unit to the panel's own transfer, and says whether the game had
        /// one to hand it to: a patch that renames the method is a refusal here rather than an
        /// exception, which is the one failure policy <see cref="GameHandlers"/> gives every screen
        /// that reaches into the game this way.</summary>
        private static bool Transfer(
            StarSystemPlanetCardsPanel panel,
            ColonizedPlanet destination
        )
        {
            MethodInfo method = GameHandlers.Method(
                typeof(StarSystemPlanetCardsPanel),
                "TransferDraggedPopulationToPlanet"
            );
            if (method == null)
            {
                return false;
            }

            method.Invoke(panel, new object[] { destination });
            return true;
        }

        private static StarSystemPlanetCardsPanel Cards(PlanetCard card)
        {
            try
            {
                return card == null
                    ? null
                    : card.GetComponentInParent<StarSystemPlanetCardsPanel>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Which colony of the empire the carried unit lives on. Found rather than remembered:
        /// what is carried is the game's own <c>Population</c>, and a unit picked up on the star system
        /// page and dropped here has to be found from scratch.</summary>
        private static ColonizedPlanet Owner(Population population)
        {
            return PopulationMoves.PlanetOf(population);
        }

        /// <summary>Where a carried unit would be SENT by dropping it on this system's population cell,
        /// or null where the game would not take it: its own drop asks for a colonized system of the
        /// player's other than the one the unit is on, and a spaceport at that one able to ship
        /// (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :183-199).</summary>
        private static ColonizedStarSystem Shippable(
            GuiTableCellSystemPopulation cell,
            CarryItem item
        )
        {
            try
            {
                Population population = item == null ? null : item.Cargo as Population;
                GuiColonizedStarSystem wrapper = cell == null ? null : cell.GuiColonizedStarSystem;
                ColonizedStarSystem destination =
                    wrapper == null ? null : wrapper.ColonizedStarSystem;
                ColonizedPlanet source = Owner(population);
                ColonizedStarSystem from = source == null ? null : source.ColonizedStarSystem;
                if (
                    destination == null
                    || from == null
                    || destination.GUID == from.GUID
                    || destination.State != StarSystemState.Colony
                )
                {
                    return null;
                }

                Spaceport port = from.Spaceport;
                if (port == null || !port.IsAvailable())
                {
                    return null;
                }

                // The people board the SOURCE system's spaceport first and a ship carries them on
                // from there, so a FULL port ships nobody however much room the destination has -
                // the order is posted, the port clamps it to nothing
                // (<c>Spaceport.TransferPopulation</c> :191) and the game's own mouse drop is a
                // silent no-op. The room has to be asked HERE and not only at the drop, because this
                // is also what decides whether the row says "drop target": a row that advertises and
                // then refuses is worse than a row that stays quiet (owner-reported 2026-08-29).
                return PopulationMoves.IntoPort(port, null, item.Quantity) > 0 ? destination : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Send the carried unit to this system, which is what the game's own drop on this cell does: the
        /// source system's spaceport is pointed at the destination and then ordered to ship
        /// (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :183-199).
        ///
        /// The game sets the destination BEFORE asking whether a ship can leave, so a refusal there
        /// leaves the spaceport pointed somewhere new. The three reasons that have nothing to do with the
        /// destination (frozen, besieged, blockaded) are therefore asked FIRST and refuse in the game's
        /// own words, changing nothing; the destination is only set once the answer is yes.
        /// </summary>
        private static DropResult Ship(GuiTableCellSystemPopulation cell, CarryItem item)
        {
            ColonizedStarSystem destination = Shippable(cell, item);
            Population population = item == null ? null : item.Cargo as Population;
            ColonizedPlanet source = Owner(population);
            Spaceport port =
                source == null || source.ColonizedStarSystem == null
                    ? null
                    : source.ColonizedStarSystem.Spaceport;
            if (destination == null || port == null)
            {
                return DropResult.Refused(null);
            }

            try
            {
                StaticString failure;
                if (!port.CanCreateAutomatedShip(out failure) && !IsNoDestination(failure))
                {
                    return DropResult.Refused(FailureText(failure));
                }

                port.SetDestination(destination);
                if (!port.CanCreateAutomatedShip(out failure))
                {
                    return DropResult.Refused(FailureText(failure));
                }

                // The order puts the carried people into the source system's own spaceport, which
                // CLAMPS against its free room and never refuses (Spaceport.TransferPopulation :191) -
                // so what is said is what will really board.
                int moved = PopulationMoves.IntoPort(port, null, item.Quantity);
                if (moved <= 0)
                {
                    return DropResult.Refused(null);
                }

                PlayerController controller = Gui.GetActivePlayerController();
                controller.PostOrder(
                    new OrderTransferSpaceportPopulation(
                        controller.Empire.Index,
                        population.Affinity,
                        item.Quantity,
                        source.GUID
                    )
                );
                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.EmpirePopulationSent,
                        PopulationMoves.Name(population, moved),
                        AgeText.Clean(destination.LocalizedName)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: sending a population unit threw: " + e);
                return DropResult.Refused(null);
            }
        }

        private static bool IsNoDestination(StaticString failure)
        {
            return !StaticString.IsNullOrEmpty(failure)
                && failure == FailureFlags.SpaceportNoDestination;
        }

        /// <summary>The game's own sentence for one of its failure flags, in the shape the game itself
        /// builds it (<c>AvailableModItem.Refresh</c> :77) - and nothing at all for a flag the string
        /// files never answered, which is parked text rather than a reason.</summary>
        private static string FailureText(StaticString failure)
        {
            try
            {
                if (StaticString.IsNullOrEmpty(failure))
                {
                    return null;
                }

                return AgeText.Title("%Failure" + failure.ToString() + "Description");
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
