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
        /// <summary>
        /// The items as one comma-separated line, in the order they were handed over - the plain list,
        /// without the conjunction <see cref="Join"/> closes an enumeration with.
        ///
        /// The one home for "several things said as one line": a panel's fields, a strip's repeated
        /// items, the arcs drawn from a technology, and the head of an enumeration
        /// (<see cref="Join"/>) are all this. Blank items are dropped and every survivor is trimmed,
        /// because a label the game has blanked keeps the spacing of the text it used to hold and a
        /// list item of one space reads as a stumble between two real ones.
        ///
        /// Nothing to say answers NULL rather than an empty string, and the distinction is load
        /// bearing for a passive announcer: nothing to say means the surface has not been filled in
        /// yet, so the announcer must ask again next frame rather than record a reading it never made.
        /// </summary>
        public static string Items(IList<string> items)
        {
            return Items(items, items == null ? 0 : items.Count);
        }

        /// <summary>The same line made of the FIRST <paramref name="count"/> of them - for a caller
        /// holding back the last item to close the list with.</summary>
        public static string Items(IList<string> items, int count)
        {
            MessageBuilder message = new MessageBuilder();
            for (int i = 0; items != null && i < count && i < items.Count; i++)
            {
                string item = items[i] == null ? null : items[i].Trim();
                if (!string.IsNullOrEmpty(item))
                {
                    message.ListItem(item);
                }
            }

            return message.Build();
        }

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

            return ModStrings.Format(
                ModStrings.ListFinal,
                Items(items, items.Count - 1),
                items[items.Count - 1]
            );
        }
    }
}
