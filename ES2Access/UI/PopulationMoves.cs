using System;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Moving POPULATION about: the questions both pages that draw a population ring have to answer,
    /// asked once here instead of twice.
    ///
    /// The game moves population by dragging one marker of a ring onto somewhere else, and it draws
    /// that ring in three places - the star system page's planet cards, that page's spaceport panel,
    /// and the empire page's planet cards. All three share one drag: one static
    /// <c>PopulationEnumerator.DragInfo</c>, one acceptance test per target, and the SOURCE's own
    /// <c>IDragDropClient.ApplyDrop</c> posting the order. So the mod shares them too - which is not
    /// tidiness but correctness: the two screens had drifted into two different answers to "can
    /// anything be picked up here" and one of them was wrong (a one-colony system with a spaceport
    /// offered no carry at all, though the game's own drag offers the panel as a target -
    /// <c>PlanetLabelsWindow_SystemManagement.StartDrag</c> :137-153).
    ///
    /// Nothing here declares a node or speaks: the screens do that. This is the arithmetic, the game's
    /// own tests, and the two calls that fill and empty the drag.
    /// </summary>
    public static class PopulationMoves
    {
        /// <summary>The planet cards the star system page is drawing, swept once per frame: whether
        /// there is anywhere to put a unit down is asked by every ring on the page, and each ring asks
        /// it while the same frame is being built. The window POOLS its cards, so the sweep is kept for
        /// the frame and no longer.</summary>
        private static readonly FrameSweep<PlanetLabel_SystemManagement> Cards =
            new FrameSweep<PlanetLabel_SystemManagement>("population moves");

        /// <summary>What the carried thing IS, so a population unit cannot be dropped into a fleet and
        /// a ship cannot be dropped onto a planet. Shared by every screen that draws a ring.</summary>
        public const string Kind = "population";

        // ---- is there anywhere to put a unit down ----

        /// <summary>
        /// Whether the STAR SYSTEM page offers anywhere to put a unit taken off one of its cards -
        /// which is the composition the game's own drag makes when it builds the target list: every
        /// planet card that would accept the drop, PLUS the spaceport panel whenever that panel is
        /// being drawn (<c>PlanetLabelsWindow_SystemManagement.StartDrag</c> :137-153,
        /// <c>GetPopulationDragDropTargets</c> :67-77).
        ///
        /// The panel half is the one this page used to be missing: a system with a single colony and a
        /// spaceport draws its markers, the mouse can drag one into the port, and the carry key was
        /// silent on every one of them.
        ///
        /// The third half is a SANCTUARY ring on one of the cards: the game's own target list asks each
        /// card's ghost enumerator as well as its own (<c>GetPopulationDragDropTargets</c> :72), so a
        /// single-colony system with a Sanctuary in it is a system with somewhere to put a unit down.
        /// </summary>
        public static bool OnSystemPage(StarSystemScreen window)
        {
            try
            {
                ColonizedStarSystem system = window == null ? null : window.ColonizedStarSystem;
                if (system == null || system.Empire != Gui.PlayerEmpire)
                {
                    return false;
                }

                if (system.PlanetsColonized.Count > 1)
                {
                    return true;
                }

                SpaceportSidePanel port = window.GetSpaceportSidePanel();
                if (port != null && port.Shown && port.Spaceport != null)
                {
                    return true;
                }

                return GhostRingDrawn(window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether any card on the page is drawing a Sanctuary's own population ring, which is
        /// the third kind of place this page can put a unit down.</summary>
        private static bool GhostRingDrawn(StarSystemScreen window)
        {
            PlanetLabelsWindow_SystemManagement labels =
                Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false);
            if (labels == null)
            {
                return false;
            }

            PlanetLabel_SystemManagement[] cards = Cards.Under(labels);
            for (int i = 0; i < cards.Length; i++)
            {
                PlanetLabel_SystemManagement card = cards[i];
                if (
                    card != null
                    && card.Shown
                    && card.GhostPopulationEnumeratorFocused != null
                    && card.GhostPopulationEnumeratorFocused.Shown
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the EMPIRE page offers anywhere to put a unit taken off
        /// <paramref name="from"/>: a second colony in the same system, or a spaceport with another
        /// colonized system of the player's to ship it to
        /// (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :183-199 is the second half).</summary>
        public static bool OnEmpirePage(ColonizedStarSystem from)
        {
            if (from == null)
            {
                return false;
            }

            if (from.PlanetsColonized.Count > 1)
            {
                return true;
            }

            Spaceport port = from.Spaceport;
            if (port == null || !port.IsAvailable())
            {
                return false;
            }

            DepartmentOfTheInterior interior =
                Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>();
            for (int i = 0; interior != null && i < interior.ColonizedStarSystems.Count; i++)
            {
                ColonizedStarSystem other = interior.ColonizedStarSystems[i];
                if (
                    other != null
                    && other.GUID != from.GUID
                    && other.State == StarSystemState.Colony
                )
                {
                    return true;
                }
            }

            return false;
        }

        // ---- the ring's slots ----

        /// <summary>
        /// The SLOTS of a ring the game is drawing, and the unit filling each - contents from the
        /// model, existence from the drawing.
        ///
        /// <paramref name="drawn"/> is how many markers the caller can see the game drawing (each page
        /// finds its own container: a system card swaps between two rings, an empire card has one), and
        /// zero means there is no ring to read whatever the model holds.
        /// <paramref name="units"/> comes back holding one entry per population UNIT in
        /// <c>PopulationsByAffinity</c> order, which is the order the game's own enumerator lays the
        /// markers out in and therefore the order <see cref="Carried"/> counts runs down.
        ///
        /// A world nobody has settled gets a ring too, of empty places, because the enumerator falls
        /// back to the PLANET's figures when there is no colony
        /// (<c>PlanetPopulationEnumerator.GetPopulationOwnerData</c> :71-75) - so how much room a world
        /// has reads the same way on both kinds of card.
        /// </summary>
        public static List<PopulationSlots.Slot> Slots(
            Planet planet,
            ColonizedPlanet colony,
            PlanetPopulationEnumerator markers,
            int drawn,
            List<Population> units
        )
        {
            List<PopulationSlots.Slot> slots = new List<PopulationSlots.Slot>(8);
            try
            {
                if (drawn == 0)
                {
                    return slots;
                }

                if (colony == null)
                {
                    if (planet != null)
                    {
                        PopulationSlots.BuildUnsettled(
                            planet.PopulationCount,
                            planet.MaxPopulation,
                            slots
                        );
                    }

                    return slots;
                }

                foreach (KeyValuePair<StaticString, Population> entry in colony.PopulationsByAffinity)
                {
                    Population population = entry.Value;
                    for (int i = 0; population != null && i < population.Count; i++)
                    {
                        units.Add(population);
                    }
                }

                PopulationSlots.Build(
                    units.Count,
                    colony.MaxPopulation,
                    colony.MaxPopulationUnderOverPopulation,
                    OverpopulationDrawn(markers, colony),
                    slots
                );
            }
            catch (Exception e)
            {
                Log.Warn("population: reading a planet's population slots threw: " + e);
            }

            return slots;
        }

        /// <summary>
        /// Whether the game is drawing the overpopulation arc over this ring, which is what decides
        /// whether the slots past the colony's comfortable maximum are a band of their own - so that a
        /// mode of play where the arc means nothing (an empire that runs on honour, a system somebody
        /// else is exploiting) reads as one plain band of slots exactly as it is drawn.
        ///
        /// Asked of the SECTOR the game draws the arc with, whose visibility
        /// <c>PlanetPopulationEnumeratorRadial.RefreshOverpopulation</c> (:124-136) writes from those
        /// conditions - one question of the drawing instead of four restatements of the rule. The
        /// conditions are still written out below for a ring drawn by an enumerator that has no such
        /// sector: only the RADIAL enumerator draws an arc, and a page whose prefab wires the plain
        /// one has nothing to ask.
        /// </summary>
        public static bool OverpopulationDrawn(
            PlanetPopulationEnumerator markers,
            ColonizedPlanet colony
        )
        {
            try
            {
                PlanetPopulationEnumeratorRadial radial =
                    markers as PlanetPopulationEnumeratorRadial;
                AgePrimitiveSector sector = radial == null ? null : radial.OverPopulationSector;
                if (sector != null && sector.AgeTransform != null)
                {
                    // Banding input: whether the arc is drawn is what splits the ring's slots into an
                    // overpopulation band or leaves them one plain band. The flag the game itself
                    // wrote, not the ancestry - the caller has already established that the ring is
                    // being drawn, and a whole card fading in must not turn a band off.
                    return sector.AgeTransform.Visible;
                }

                ColonizedStarSystem system = colony.ColonizedStarSystem;
                return system != null
                    && system.State != StarSystemState.Lost
                    && !(system is ExploitedStarSystem)
                    && !colony.Empire.CanUseHonor;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's own word for each band of the ring - the three colours a player cannot
        /// see, heard as three regions instead. Shared, because both pages that draw a ring band it the
        /// same way and a band named differently on two pages is two facts about one picture.
        ///
        /// The three words are the GAME's own, taken straight from its localization rather than given
        /// mod keys of their own - an owner ruling of 2026-08-26, and a deliberate departure from this
        /// mod's usual "every phrase it authors is a ModStrings key". The game already draws all three
        /// words for these very things, so borrowing them costs the player no new vocabulary and costs
        /// the translators nothing at all.</summary>
        public static string BandName(PopulationSlots.Band band)
        {
            string key = PopulationBandTitle;
            if (band == PopulationSlots.Band.Overpopulation)
            {
                key = OverpopulationBandTitle;
            }
            else if (band == PopulationSlots.Band.Locked)
            {
                key = LockedBandTitle;
            }

            return AgeText.Title(key);
        }

        private const string PopulationBandTitle = "%PlanetScreenPopulationTitle";
        private const string OverpopulationBandTitle = "%HappinessOverPopulationPenalties";
        private const string LockedBandTitle = "%EconomyLockedTradingCompanySlotTitle";

        // ---- how many one press carries ----

        /// <summary>
        /// How many units one press on a slot picks up - the marker's own RANK, which is exactly what
        /// the game's drag carries (<c>PopulationEnumerator.DragInfo.Quantity =
        /// populationMarker.Rank</c>, :247) and which the game publishes on the marker it drew
        /// (<c>PopulationMarker.Rank</c> :29).
        ///
        /// The ring lays one marker per unit out grouped by affinity, and numbers the markers of one
        /// affinity's contiguous run DOWNWARDS: the first of N carries N, the last carries one
        /// (<c>ShowPopulationMarkers</c> :188-203). So one press moves a WHOLE tail of one affinity
        /// and never mixes two, which is the game's rule rather than a simplification.
        ///
        /// The same run counted off <paramref name="units"/> is the fallback, for the frames where the
        /// ring is not drawing a marker for the slot the row stands for - a refresh in progress, or a
        /// world nobody has settled, whose ring is markers with nobody in them. That list repeats one
        /// <c>Population</c> object per unit in exactly the ring's order, so counting how many of the
        /// same people are at <paramref name="index"/> or after it reproduces the rank.
        /// </summary>
        public static int Carried(PopulationMarker marker, IList<Population> units, int index)
        {
            try
            {
                if (marker != null && marker.Rank > 0)
                {
                    return marker.Rank;
                }
            }
            catch (Exception) { }

            if (units == null || index < 0 || index >= units.Count)
            {
                return 1;
            }

            Population run = units[index];
            int carried = 1;
            for (int i = index + 1; i < units.Count && ReferenceEquals(units[i], run); i++)
            {
                carried++;
            }

            return carried;
        }

        /// <summary>
        /// What to call some population: the game's own word for the affinity, and ALWAYS the count -
        /// "Imperials x 1" as much as "Imperials x 3" (owner ruling 2026-08-29).
        ///
        /// Population is the one cargo in the mod measured in units, so it is the one whose drag
        /// phrases state a number every time (<see cref="MessageBuilder.PushQuantityAlways"/>): the
        /// rows of a ring hand over four, three, two and one, and a bare "Imperials" at the end of
        /// that run reads as a different kind of answer rather than as one. Everything else the mod
        /// drags is a single thing with no count - a module, a ship, a queue line, a tactic card - and
        /// those name themselves plainly, which is what NOT routing them through here already gives
        /// them: the name is composed by the SOURCE at pick-up, so being quantity-bearing is a fact
        /// about the cargo rather than a flag the carry has to carry.
        ///
        /// The count is a parameter and not read off the carry because a DROP reports what really
        /// moved, which the spaceport's clamp can make smaller than what was picked up.
        /// </summary>
        public static string Name(Population population, int quantity)
        {
            try
            {
                string affinity = AgeText.Clean(Gui.GetLocalizedTitle(population.Affinity));
                return new MessageBuilder()
                    .Fragment(affinity)
                    .PushQuantityAlways(quantity)
                    .Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One press's worth of this population, picked up - the game's own two tests for
        /// letting the drag start (<c>PopulationEnumerator.OnPopulationMarkerDragStarted</c>
        /// :239-252), asked of whichever owner is holding it.</summary>
        public static CarryItem Pick(
            ICappedPopulationOwner<Population> owner,
            Population population,
            int quantity
        )
        {
            try
            {
                IPopulationsManagementService populations =
                    Services.GetService<IPopulationsManagementService>();
                if (
                    owner == null
                    || population == null
                    || population.Count <= 0
                    || !owner.CanMovePopulation
                    || owner.Empire != Gui.PlayerEmpire
                    || populations == null
                    || !populations.CanMovePopulation(population.Affinity)
                )
                {
                    return null;
                }

                int carried = quantity < 1 ? 1 : quantity;
                if (carried > population.Count)
                {
                    carried = population.Count;
                }

                return new CarryItem(population, Name(population, carried), Kind, carried);
            }
            catch (Exception e)
            {
                Log.Warn("population: picking a unit up threw: " + e);
                return null;
            }
        }

        // ---- the game's own drag info ----

        /// <summary>
        /// Fill the static the whole drag runs through, exactly as
        /// <c>PopulationEnumerator.OnPopulationMarkerDragStarted</c> fills it - the owner the unit is
        /// leaving, the game's own wrapper for it, how many, and which affinity is being REPLACED at
        /// the destination (empty for a plain add; a hovered marker's affinity for a swap, which is
        /// what the mouse sets on hover, :275).
        ///
        /// <paramref name="inProgress"/> is the difference between DOING the drag and ASKING about
        /// one. The enumerators read every other field only under <c>DragInProgress</c> (:145, :159),
        /// so a fill with the flag FALSE is render-invisible: nothing redraws, no marker empties, and
        /// the acceptance tests - which read the fields directly - still answer. That is what lets a
        /// row ask "would this be taken" while the player is merely standing on it.
        /// </summary>
        public static void Fill(
            ICappedPopulationOwner<Population> owner,
            Population population,
            int quantity,
            StaticString replaced,
            bool inProgress
        )
        {
            PopulationEnumerator.PopulationDragInfo drag = PopulationEnumerator.DragInfo;
            drag.DragInProgress = inProgress;
            drag.SourcePopulationOwner = owner;
            drag.GuiPopulation = Wrap(owner == null ? null : owner.Empire, population);
            drag.Quantity = quantity;
            drag.TransitingPopulation = new TransitingPopulation(population.Affinity, quantity);
            drag.ReplacedPopulationAffinity = replaced;
        }

        /// <summary>Empty it again. Called whatever happened: it is a static the game's own refresh
        /// reads every frame to draw a unit as already gone, and a stale one would empty a marker the
        /// player is still looking at.</summary>
        public static void Clear()
        {
            PopulationEnumerator.PopulationDragInfo drag = PopulationEnumerator.DragInfo;
            drag.DragInProgress = false;
            drag.SourcePopulationOwner = null;
            drag.GuiPopulation = null;
            drag.Quantity = 0;
            drag.TransitingPopulation = null;
            drag.ReplacedPopulationAffinity = StaticString.Empty;
        }

        /// <summary>The game's own wrapper for a population, built the way its own enumerator builds
        /// one - which is what every <c>ApplyDrop</c> reads the affinity out of.</summary>
        public static GuiPopulation Wrap(Empire owner, Population population)
        {
            if (owner == null || population == null)
            {
                return null;
            }

            DepartmentOfTheInterior interior = owner.GetAgency<DepartmentOfTheInterior>();
            PopulationEmpire empire =
                interior == null
                    ? null
                    : interior.GetPopulationByAffinity(population.Affinity) as PopulationEmpire;
            return new GuiPopulation(population, empire, owner);
        }

        // ---- would the game take it ----

        /// <summary>
        /// Whether this planet ring would accept the carried unit RIGHT NOW - the game's own
        /// <c>PlanetPopulationEnumerator.CanAcceptPopulationDrop</c> (:28-35), asked without starting a
        /// drag.
        ///
        /// Asked rather than re-derived because the three things it tests are the three things a
        /// player is owed: a planet of the player's own, NOT the one the unit came off, and room - or
        /// a swap, since <c>CanWelcomeSomeOfPopulation</c> accepts a drop on a FULL planet whenever
        /// somebody there could be sent back the other way (:307-317). Re-deriving that last clause is
        /// how a target ends up advertising itself and then refusing.
        ///
        /// It is what every drop target's <see cref="Core.UI.Graph.NodeVtable.DropAccepts"/> answers
        /// with, so the "drop target" word and the derived drop hint appear on exactly the controls
        /// the game would take the thing on - and, in particular, never on the card or the slot the
        /// unit is being carried off.
        /// </summary>
        public static bool Accepts(
            PlanetPopulationEnumerator markers,
            Population population,
            int quantity
        )
        {
            PopulationEnumerator.PopulationDragInfo drag = PopulationEnumerator.DragInfo;
            if (markers == null || population == null || drag.DragInProgress)
            {
                return false;
            }

            ICappedPopulationOwner<Population> owner = OwnerOf(population);
            if (owner == null)
            {
                return false;
            }

            ICappedPopulationOwner<Population> keptOwner = drag.SourcePopulationOwner;
            TransitingPopulation keptTransiting = drag.TransitingPopulation;
            try
            {
                drag.SourcePopulationOwner = owner;
                drag.TransitingPopulation = new TransitingPopulation(
                    population.Affinity,
                    quantity
                );
                return markers.CanAcceptPopulationDrop();
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                drag.SourcePopulationOwner = keptOwner;
                drag.TransitingPopulation = keptTransiting;
            }
        }

        /// <summary>Which of the player's colonies or spaceports is holding this population. Found
        /// rather than remembered: what is carried is the game's own <c>Population</c> object, and the
        /// owner holding it is the one whose own table it is in - which is also how a unit picked up on
        /// one page and dropped on another is traced.</summary>
        public static ICappedPopulationOwner<Population> OwnerOf(Population population)
        {
            ColonizedPlanet planet = PlanetOf(population);
            return planet != null ? (ICappedPopulationOwner<Population>)planet : PortOf(population);
        }

        /// <summary>Which colony of the empire the unit lives on, or null where it is in a
        /// spaceport.</summary>
        public static ColonizedPlanet PlanetOf(Population population)
        {
            try
            {
                DepartmentOfTheInterior interior =
                    population == null
                        ? null
                        : Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>();
                for (int i = 0; interior != null && i < interior.ColonizedStarSystems.Count; i++)
                {
                    ColonizedStarSystem system = interior.ColonizedStarSystems[i];
                    for (int p = 0; system != null && p < system.PlanetsColonized.Count; p++)
                    {
                        ColonizedPlanet planet = system.PlanetsColonized[p];
                        Population held;
                        if (
                            planet != null
                            && planet.PopulationsByAffinity.TryGetValue(
                                population.Affinity,
                                out held
                            )
                            && ReferenceEquals(held, population)
                        )
                        {
                            return planet;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("population: finding a carried unit's planet threw: " + e);
            }

            return null;
        }

        /// <summary>Which spaceport of the empire the unit is waiting in, or null.</summary>
        public static Spaceport PortOf(Population population)
        {
            try
            {
                DepartmentOfTheInterior interior =
                    population == null
                        ? null
                        : Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>();
                for (int i = 0; interior != null && i < interior.ColonizedStarSystems.Count; i++)
                {
                    ColonizedStarSystem system = interior.ColonizedStarSystems[i];
                    Spaceport port = system == null ? null : system.Spaceport;
                    Population held;
                    if (
                        port != null
                        && port.PopulationsByAffinity != null
                        && port.PopulationsByAffinity.TryGetValue(population.Affinity, out held)
                        && ReferenceEquals(held, population)
                    )
                    {
                        return port;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("population: finding a carried unit's spaceport threw: " + e);
            }

            return null;
        }

        // ---- how many actually move ----

        /// <summary>
        /// How many of <paramref name="carried"/> would really end up in the spaceport.
        ///
        /// The port CLAMPS and never refuses: <c>Spaceport.TransferPopulation</c> :191 moves
        /// <c>min(count, MaxPopulation - PopulationCount)</c> and returns quietly, so a carry of three
        /// onto a port with one free slot moves one and the player is owed that number rather than the
        /// number they were holding.
        ///
        /// A SWAP is posted as two orders in order - one unit of the replaced affinity out of the port
        /// onto the source planet, then the carried units in
        /// (<c>PlanetLabelsWindow_SystemManagement.ApplyDrop</c> :38-44) - so the slot the bounce frees
        /// counts, and it only frees one if the planet taking it back has room, by the very same
        /// clamp.
        /// </summary>
        public static int IntoPort(Spaceport port, ColonizedPlanet bounceTo, int carried)
        {
            try
            {
                int room = port.MaxPopulation - port.PopulationCount;
                if (bounceTo != null)
                {
                    room += OntoPlanet(bounceTo, 1);
                }

                return room <= 0 ? 0 : (carried < room ? carried : room);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>How many of <paramref name="carried"/> a planet would really take out of the
        /// spaceport - the same clamp from the other side (<c>Spaceport.TransferPopulation</c> :191
        /// measures a negative transfer against the PLANET's room).</summary>
        public static int OntoPlanet(ColonizedPlanet planet, int carried)
        {
            try
            {
                int room = planet.MaxPopulation - planet.PopulationCount;
                return room <= 0 ? 0 : (carried < room ? carried : room);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
