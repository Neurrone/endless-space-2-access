using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// Several things said as one enumeration, with the conjunction the last join takes.
    ///
    /// <see cref="MessageBuilder"/> already joins list items with the list separator, and for most of
    /// what the mod says that is enough: a row read as "name, role, state" is a series of facts and
    /// wants no "and" anywhere in it. An enumeration is the other case - six ranges of fog, three
    /// empires - where the final comma alone leaves a listener unsure whether the list has ended, and
    /// the conjunction is what closes it.
    ///
    /// The conjunction is a TEMPLATE holding the already-joined head and the last item rather than a
    /// word glued between them, because a language that does this with a particle, with different
    /// punctuation, or with nothing at all needs the whole join to be its own.
    /// </summary>
    public static class SpokenList
    {
        /// <summary>The items as one phrase, or null if there are none. Empty items are the caller's
        /// business: everything handed in is said.</summary>
        public static string Join(IList<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            if (items.Count == 1)
            {
                return items[0];
            }

            if (items.Count == 2)
            {
                return ModStrings.Format(ModStrings.ListPair, items[0], items[1]);
            }

            MessageBuilder head = new MessageBuilder();
            for (int i = 0; i < items.Count - 1; i++)
            {
                head.ListItem(items[i]);
            }

            return ModStrings.Format(ModStrings.ListFinal, head.Build(), items[items.Count - 1]);
        }
    }
}
