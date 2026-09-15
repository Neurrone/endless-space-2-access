using System;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.PlanetCards;

namespace ES2Access.Screens
{
    /// <summary>The planet cards a system row slides out - which cards the panel is drawing, and what
    /// is the PAGE's about each of them: the population drops between them and the shipment to another
    /// system. What a planet card SAYS is <see cref="PlanetCardReader"/>'s, shared with every other
    /// surface that draws one.</summary>
    public sealed partial class EmpireScreen
    {
        /// <summary>The walk the panel's cards are found by, made once per panel per frame. The cards
        /// are POOLED by the panel and rebound as the table's selection moves, so the answer is only
        /// good for the frame it was walked in.</summary>
        private static readonly FrameSweep<PlanetCard> CardsIn = new FrameSweep<PlanetCard>("empire");

        /// <summary>
        /// The planets of the selected system, left to right - which is NOT the order the panel holds
        /// them in: it lays its cards out from the right, so the system's first planet is the rightmost
        /// card. Measured rather than assumed.
        /// </summary>
        private void BuildCards(GraphBuilder builder, StarSystemPlanetCardsPanel panel)
        {
            _cards.Clear();
            try
            {
                PlanetCard[] all = CardsIn.Under(panel);
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

        /// <summary>
        /// One planet card - what this page supplies, over the shared reader.
        ///
        /// THERE IS NO CLICK. The card is a panel of widgets rather than a control, and this page has
        /// no page of its own to open behind it, so the card is a plain group.
        ///
        /// THE CARD ITSELF TAKES NO DROP (owner ruling 2026-08-29). The game's mouse accepts one
        /// anywhere on the card's rectangle, but a keyboard player is walking rows, and a card header
        /// that also swallowed drops made two rows out of one gesture: the header and the free slot
        /// under it both said "drop target" and did different things. So the drop lives on the SLOTS
        /// alone.
        ///
        /// The scratch namespace is this page's own: the star system page parks a carrier per slot of
        /// the same world's ring, and one key for both pages would hand this card's slot 1 whatever
        /// that page's slot 1 was last bound with.
        /// </summary>
        private void AddCard(GraphBuilder builder, PlanetCard label)
        {
            Planet planet = label.Planet;
            if (planet == null)
            {
                return;
            }

            PlanetCard it = label;
            EmpireCardAdapter card = new EmpireCardAdapter(label);
            card.Key = "empire:planet/" + planet.GUID;
            card.RingScratch = "empire/";
            card.CanCarry = () => CanCarry(it);
            card.Accepts = cargo => Accepts(it, cargo);
            card.Drop = (cargo, replaced) => DropOnCard(it, cargo, replaced);
            PlanetCardReader.Add(builder, card);
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
                    // walk: on the population-drop action, one ancestor hop from the card
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
