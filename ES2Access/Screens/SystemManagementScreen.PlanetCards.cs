using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.PlanetCards;

namespace ES2Access.Screens
{
    /// <summary>The planets across the middle of the page: which cards the page is drawing, and what
    /// is the PAGE's about each of them - the click, the population drops, the id namespace. What a
    /// planet card SAYS is <see cref="PlanetCardReader"/>'s, shared with every other surface that
    /// draws one.</summary>
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
                //
                // Asked once for the page rather than once per card, and handed to the cards as one
                // of two shared answers, because the question is about the page and the build runs
                // every frame.
                Func<bool> canCarry = PopulationMoves.OnSystemPage(window) ? Carries : CarriesNot;
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

        private static readonly Func<bool> Carries = () => true;
        private static readonly Func<bool> CarriesNot = () => false;

        /// <summary>
        /// One planet card - what this page supplies, over the shared reader.
        ///
        /// ENTER IS THE CARD'S OWN CLICK, which on this page is the planet's own page. The card is an
        /// AGE overlay and carries no click of its own - the click the game answers is the one on the
        /// PLANET behind it (<c>GalaxyPlanetCursorTarget.OnCursorClick</c> :30-53, which asks for
        /// <c>GalaxyViewLevel_PlanetOverview</c> while this view level is up), and that is what
        /// <see cref="GalaxyViewLevels.OpenPlanet"/> posts. Nothing is spoken for it: the page changes
        /// and the page announces itself.
        ///
        /// THE CARD ITSELF TAKES NO DROP (owner ruling 2026-08-29). The game's mouse accepts one
        /// anywhere on the card's rectangle, but a keyboard player is walking rows, and a card header
        /// that also swallowed drops made two rows out of one gesture: the header and the free slot
        /// under it both said "drop target" and did different things. So the drop lives on the SLOTS
        /// alone - an empty one is the plain add, an occupied one the swap - which reaches every
        /// outcome the mouse reaches and says where the people are going. A full planet then offers
        /// only its swaps, and a planet with room offers its free places.
        ///
        /// The card draws up to TWO rings - the world's own and a Sanctuary's - and the game runs both
        /// through the same drag machinery (<c>GetPopulationDragDropTargets</c> :72 asks both
        /// enumerators, and <c>ApplyDrop</c> :18-32 resolves the destination itself). They land on
        /// different colonies, so each is given its own pair of answers here.
        /// </summary>
        private void AddPlanet(
            GraphBuilder builder,
            PlanetLabel_SystemManagement label,
            Func<bool> canCarry
        )
        {
            Planet planet = label.Planet;
            if (planet == null)
            {
                return;
            }

            SystemManagementCardAdapter card = new SystemManagementCardAdapter(label);
            card.Key = "system:planet/" + planet.GUID;
            card.RingScratch = string.Empty;
            card.OnActivate = () => GalaxyViewLevels.OpenPlanet(planet);
            card.CanCarry = canCarry;
            card.Accepts = cargo =>
                AcceptsPopulation(card.RingTarget, card.PlayerColony, cargo);
            card.Drop = (cargo, replaced) =>
                DropPopulation(card, card.RingTarget, card.PlayerColony, cargo, replaced);
            card.GhostAccepts = cargo =>
                AcceptsPopulation(card.GhostRingMarkers, card.PlayerGhostColony, cargo);
            card.GhostDrop = (cargo, replaced) =>
                DropPopulation(card, card.GhostRingMarkers, card.PlayerGhostColony, cargo, replaced);
            PlanetCardReader.Add(builder, card);
        }

        /// <summary>
        /// Whether one of this card's rings would take what is being carried, right now - the game's
        /// own answer (<see cref="PopulationMoves.Accepts"/>), which is what every population drop
        /// target on this page advertises itself by.
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
        private static bool AcceptsPopulation(
            PlanetPopulationEnumerator target,
            ColonizedPlanet destination,
            CarryItem held
        )
        {
            Population population = held == null ? null : held.Cargo as Population;
            if (
                population == null
                || destination == null
                || !PopulationMoves.Accepts(target, population, held.Quantity)
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
        private static DropResult DropPopulation(
            SystemManagementCardAdapter card,
            PlanetPopulationEnumerator target,
            ColonizedPlanet destination,
            CarryItem item,
            StaticString replaced
        )
        {
            PlanetLabel_SystemManagement label = card.Card;
            Population population = item == null ? null : item.Cargo as Population;
            ColonizedPlanet source = population == null ? null : SourceOf(destination, population);
            SpaceportSidePanel port =
                population == null || source != null ? null : SpaceportSource(population);
            if (destination == null || target == null || (source == null && port == null))
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
                    if (!target.CanAcceptPopulationDrop())
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

        /// <summary>The label window's cards, swept once per frame. The window POOLS them - it keeps a
        /// card per planet of the biggest system seen and retires the rest - so the answer is kept for
        /// the frame and no longer.</summary>
        private static readonly FrameSweep<PlanetLabel_SystemManagement> Cards =
            new FrameSweep<PlanetLabel_SystemManagement>("system management");

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

            PlanetLabel_SystemManagement[] labels = Cards.Under(window);
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
