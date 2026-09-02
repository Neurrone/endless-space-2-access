using System.Collections.Generic;

namespace ES2Access.ES2.UI
{
    /// <summary>
    /// Which band of a colony's population ring each SLOT of it belongs to, and which slots hold a
    /// unit.
    ///
    /// The game draws a colony's population as a ring of markers, one per slot, and colours three
    /// kinds of slot differently: the ordinary ones, the ones inside the overpopulation arc, and the
    /// ones the world's current maximum has locked. The colours are the whole of what says so, so a
    /// keyboard player hears the three as three REGIONS instead - and this is the arithmetic that puts
    /// a slot in one of them, off the engine so it can be tested for the shapes no save reaches.
    ///
    /// The ring's own order (<c>PopulationEnumerator.BuildListOfGuiPopulations</c>): one entry per
    /// population UNIT first, grouped by affinity in the colony's own enumeration order, then empty
    /// entries up to the maximum. A slot past the maximum is drawn LOCKED and drawn with NO unit in it
    /// even where one lives there (<c>PopulationEnumerator.RetrievePopulationMarker</c> binds
    /// <c>locked: true</c> with a null population for every index at or past the maximum), which is
    /// why a locked slot here reports no unit: the row says what the ring shows, not what the model
    /// holds.
    ///
    /// The overpopulation band exists only where the game would draw the arc over it
    /// (<c>PlanetPopulationEnumeratorRadial.RefreshOverpopulation</c>: a colony, a system that is
    /// neither exploited nor lost, and an empire that does not use honour). Where it does not, those
    /// ranks are ordinary slots - the caller answers that question and hands the answer in, because
    /// every term of it is the game's.
    /// </summary>
    public static class PopulationSlots
    {
        /// <summary>Which of the ring's three colours a slot is drawn in.</summary>
        public enum Band
        {
            /// <summary>An ordinary slot, inside the world's comfortable maximum.</summary>
            Population,

            /// <summary>A slot under the overpopulation arc: filling it costs the system approval.
            /// </summary>
            Overpopulation,

            /// <summary>A slot past the world's current maximum, which nothing can be put in until the
            /// system's development raises it.</summary>
            Locked,
        }

        /// <summary>One slot of the ring: where it is, which band it is drawn in, and which unit of the
        /// colony fills it (-1 for a slot the ring draws nothing in).</summary>
        public struct Slot
        {
            /// <summary>1-based, counting round the whole ring.</summary>
            public int Rank;

            public Band Kind;

            /// <summary>Index into the caller's unit list, or -1 where the ring draws no unit here.
            /// </summary>
            public int Unit;
        }

        /// <summary>How many slots the ring draws: one per unit and one per point of maximum
        /// population, whichever list is longer - a colony holding more than its maximum keeps drawing
        /// the surplus, as locked slots.</summary>
        public static int Total(int units, int maxPopulation)
        {
            int units2 = units < 0 ? 0 : units;
            int max = maxPopulation < 0 ? 0 : maxPopulation;
            return units2 > max ? units2 : max;
        }

        /// <summary>
        /// The ring's slots, rank 1 upwards.
        ///
        /// <paramref name="units"/> is how many population units the colony holds,
        /// <paramref name="safeMax"/> the last rank outside the overpopulation arc
        /// (<c>ColonizedPlanet.MaxPopulationUnderOverPopulation</c>), and
        /// <paramref name="overpopulationDrawn"/> whether the game would draw that arc at all.
        /// </summary>
        public static void Build(
            int units,
            int maxPopulation,
            int safeMax,
            bool overpopulationDrawn,
            List<Slot> into
        )
        {
            if (into == null)
            {
                return;
            }

            into.Clear();
            int max = maxPopulation < 0 ? 0 : maxPopulation;
            int held = units < 0 ? 0 : units;
            int safe = safeMax < 0 ? 0 : (safeMax > max ? max : safeMax);
            int total = Total(held, max);
            for (int i = 0; i < total; i++)
            {
                Band kind = i >= max
                    ? Band.Locked
                    : (overpopulationDrawn && i >= safe ? Band.Overpopulation : Band.Population);
                into.Add(
                    new Slot
                    {
                        Rank = i + 1,
                        Kind = kind,
                        // A locked slot is drawn empty whatever lives in it, which is the game's own
                        // binding rather than a simplification here.
                        Unit = kind == Band.Locked || i >= held ? -1 : i,
                    }
                );
            }
        }

        /// <summary>
        /// The ring's slots on a world NOBODY has settled, which the game draws too - and draws
        /// differently enough that it is its own arithmetic rather than a call to <see cref="Build"/>
        /// with the terms bent.
        ///
        /// With no colony the enumerator takes its figures from the PLANET
        /// (<c>PlanetPopulationEnumerator.GetPopulationOwnerData</c> :71-75) and hands the ring an
        /// EMPTY population map, so the only entries <c>BuildListOfGuiPopulations</c> adds are the
        /// empty ones it counts out from <paramref name="populationCount"/> up to
        /// <paramref name="maxPopulation"/> - one marker per place there would be room for, none of
        /// them holding anybody. None is locked either: an index is only locked at or past the
        /// maximum (<c>RetrievePopulationMarker</c>) and this list never reaches it. And the
        /// overpopulation arc is not drawn at all, because
        /// <c>PlanetPopulationEnumeratorRadial.RefreshOverpopulation</c> wants a colony.
        ///
        /// So: one ordinary, empty slot per drawn marker, which is exactly what the picture shows.
        /// </summary>
        public static void BuildUnsettled(int populationCount, int maxPopulation, List<Slot> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();
            int held = populationCount < 0 ? 0 : populationCount;
            int max = maxPopulation < 0 ? 0 : maxPopulation;
            for (int i = held; i < max; i++)
            {
                into.Add(
                    new Slot
                    {
                        Rank = into.Count + 1,
                        Kind = Band.Population,
                        Unit = -1,
                    }
                );
            }
        }
    }
}
