using System.Collections.Generic;

namespace ES2Access.UI
{
    /// <summary>
    /// A short list of empires, searched.
    ///
    /// The influence readings carry the empires they have met as a plain list and refer to them by the
    /// game's own <c>Empire.Index</c> - which is what a sampled answer, a tile and a certificate can
    /// all agree on without holding a reference. Turning an index back into the empire, and asking
    /// whether the list already holds one, are the two questions that fall out of that, and they were
    /// written three times between the ground sweep and the system reading.
    ///
    /// A LIST rather than a dictionary on purpose: these hold the empires one cell or one sweep has
    /// actually met, which is a handful, and building a map of them per frame would cost more than
    /// the walk.
    /// </summary>
    public static class EmpireIndex
    {
        /// <summary>The empire in <paramref name="known"/> wearing this index, or null where none
        /// does.</summary>
        public static Empire Find(IList<Empire> known, int index)
        {
            for (int i = 0; known != null && i < known.Count; i++)
            {
                if (known[i] != null && known[i].Index == index)
                {
                    return known[i];
                }
            }

            return null;
        }

        /// <summary>Whether the list already holds this empire - identity, not index, because the
        /// caller has the empire itself in hand.</summary>
        public static bool Holds(IList<Empire> empires, Empire empire)
        {
            for (int i = 0; empires != null && i < empires.Count; i++)
            {
                if (ReferenceEquals(empires[i], empire))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
