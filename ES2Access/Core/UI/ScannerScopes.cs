using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// Which of a category's subcategories a thing belongs to, and how many things each subcategory
    /// holds.
    ///
    /// Membership is MANY-TO-MANY, which is the whole reason this is not a single index. A system can
    /// be the enemy's and be their capital at the same time, and a player sweeping "homeworld" wants
    /// it there as much as a player sweeping "enemy" does. So a thing carries a SET of subcategories -
    /// one bit each - and the counts table counts a thing once per subcategory it is in. Nothing
    /// downstream may assume the row adds up: "all" is the size of the category, not the sum of the
    /// scopes below it.
    ///
    /// Bit zero is "all", set on everything, so the row's first column needs no special case and the
    /// cursor's "a category always has somewhere to land" rule holds by construction.
    ///
    /// Engine-free, because the membership RULES are the part with no audible failure mode: a special
    /// node quietly counted as neutral, or a homeworld quietly missing from its own scope, sounds
    /// exactly like a galaxy that is shaped that way.
    /// </summary>
    public static class ScannerScopes
    {
        /// <summary>Everything in the category. Subcategory zero of every taxonomy.</summary>
        public const int All = 0;

        // The affiliation trio, which is purely about OWNERSHIP: whose the thing is and how the
        // player stands to them. A thing nobody owns is neutral; a thing that is not owned at all -
        // a phenomenon rather than a possession - is in none of the three.
        public const int Friendly = 1;
        public const int Neutral = 2;
        public const int Enemy = 3;

        /// <summary>An empire's home system, the player's own included.</summary>
        public const int Homeworld = 4;

        /// <summary>A system a minor faction lives on. An OWNERSHIP filter laid over the affiliation
        /// trio rather than a fourth member of it: a minor faction's system is neutral - that is the
        /// diplomatic answer and it stays true - and it is also findable as "one of theirs", which is
        /// the question a player asks when they are looking for someone to assimilate.</summary>
        public const int MinorFaction = 5;

        /// <summary>A special node - a nebula, a black hole, an asteroid field.</summary>
        public const int Special = 6;

        /// <summary>
        /// A curiosity's two questions beyond WHAT it is (owner ruling 2026-08-23), laid over the
        /// kind columns the way the affiliation trio is laid over a system's.
        ///
        /// EXPLORABLE is the game's own <c>CanBeSearched</c> asked of the empire; LOW POWER is the
        /// one refusal a player can do something about - the empire's expedition power is below the
        /// curiosity's difficulty, which is what the card draws a padlock for. A curiosity refused
        /// for any other reason is in neither, and is still in "all".
        ///
        /// The bit NUMBERS are shared with the affiliation trio on purpose: a set of scopes only ever
        /// means anything inside its own category, and giving each taxonomy its own numbering would
        /// make the sets impossible to compare with the column indexes they stand for.
        /// </summary>
        public const int Explorable = 1;
        public const int LowExpeditionPower = 2;
        public const int CuriosityWidth = 3;

        /// <summary>A curiosity's memberships: always "all", plus whichever of the two it answers.
        /// </summary>
        public static int Curiosity(bool explorable, bool lowPower)
        {
            int scopes = Bit(All);
            if (explorable)
            {
                scopes |= Bit(Explorable);
            }

            return lowPower ? scopes | Bit(LowExpeditionPower) : scopes;
        }

        /// <summary>How many subcategories a category has that splits by affiliation and nothing
        /// else (fleets, probes).</summary>
        public const int AffiliationWidth = 4;

        /// <summary>How many the star systems have: the affiliation trio plus the three that ask a
        /// different question about the same place.</summary>
        public const int SystemWidth = 7;

        /// <summary>How many a category has that is only ever asked "what is there" (quest markers,
        /// ally pins, obliterator missiles): one, which is "all".</summary>
        public const int SingleWidth = 1;

        /// <summary>
        /// A settleable world's two scopes: one standing free, one somebody else is already on.
        ///
        /// The one taxonomy with no "all", and deliberately - the two halves answer different
        /// questions ("where can I send a colony ship" against "whose world is worth taking"), and a
        /// column holding both would be a list a player never wants. Every world is in exactly one,
        /// so nothing is lost by the absence.
        /// </summary>
        public const int Unoccupied = 0;
        public const int Occupied = 1;
        public const int ColonizableWidth = 2;

        /// <summary>A settleable world's membership: the one scope it is in.</summary>
        public static int Colonizable(bool occupied)
        {
            return Bit(occupied ? Occupied : Unoccupied);
        }

        /// <summary>
        /// Whether a thing belongs in a column of a category whose columns are the KINDS of thing it
        /// found - one per anomaly definition, per curiosity, per resource, worked out from what is
        /// out there rather than written down anywhere.
        ///
        /// Column zero is "all" whatever the kinds turn out to be, so a category always has somewhere
        /// to land; every other column holds the things whose kind it is NAMED after, which is why the
        /// comparison is against the label the player hears and not against an index: the list is
        /// sorted by that name, so the index a kind sits at moves as the galaxy changes.
        /// </summary>
        public static bool HoldsKind(string kind, int subcategory, string label)
        {
            return subcategory == All || (kind != null && kind == label);
        }

        public static int Bit(int scope)
        {
            return 1 << scope;
        }

        public static bool Holds(int scopes, int scope)
        {
            return (scopes & Bit(scope)) != 0;
        }

        /// <summary>A thing whose only question is whether it exists.</summary>
        public static int Only()
        {
            return Bit(All);
        }

        /// <summary>A thing that is owned and nothing more - a fleet, a probe.</summary>
        public static int Owned(int affiliation)
        {
            return Bit(All) | Bit(affiliation);
        }

        /// <summary>
        /// A star system's memberships.
        ///
        /// A SPECIAL node is in "all" and "special" and nowhere else. It is not neutral: neutral is a
        /// statement about who holds a place, and a nebula is not held by anybody - putting it there
        /// would make "neutral" mean "everything left over" and quietly pad the one scope a player
        /// sweeps to find somewhere to settle.
        ///
        /// An ordinary system is in its affiliation, and ALSO in "homeworld" when it is an empire's
        /// capital and in "minor factions" when a minor faction lives on it - all at once, which is
        /// the case this whole set exists for. Neither of those two takes a system out of its
        /// affiliation: "neutral" answers how the player stands to whoever holds a place, and a minor
        /// faction's system is neutral whether or not it is also findable as one of theirs.
        /// </summary>
        public static int System(int affiliation, bool special, bool homeworld, bool minor)
        {
            if (special)
            {
                return Bit(All) | Bit(Special);
            }

            int scopes = Bit(All) | Bit(affiliation);
            if (homeworld)
            {
                scopes |= Bit(Homeworld);
            }

            return minor ? scopes | Bit(MinorFaction) : scopes;
        }

        /// <summary>One row of the counts table: how many of these things each subcategory holds,
        /// counting a thing once per subcategory it is in.</summary>
        public static int[] Tally(IList<int> scopes, int width)
        {
            int[] row = new int[width];
            for (int i = 0; scopes != null && i < scopes.Count; i++)
            {
                for (int scope = 0; scope < width; scope++)
                {
                    if (Holds(scopes[i], scope))
                    {
                        row[scope]++;
                    }
                }
            }

            return row;
        }
    }
}
