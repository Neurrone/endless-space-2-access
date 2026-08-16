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

        /// <summary>A special node - a nebula, a black hole, an asteroid field.</summary>
        public const int Special = 5;

        /// <summary>How many subcategories a category has that splits by affiliation and nothing
        /// else (fleets, probes).</summary>
        public const int AffiliationWidth = 4;

        /// <summary>How many the star systems have: the affiliation trio plus the two the map's own
        /// picture adds.</summary>
        public const int SystemWidth = 6;

        /// <summary>How many a category has that is only ever asked "what is there" (quest markers,
        /// ally pins, obliterator missiles): one, which is "all".</summary>
        public const int SingleWidth = 1;

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
        /// An ordinary system is in its affiliation, and additionally in "homeworld" when it is an
        /// empire's capital - both at once, which is the case this whole set exists for.
        /// </summary>
        public static int System(int affiliation, bool special, bool homeworld)
        {
            if (special)
            {
                return Bit(All) | Bit(Special);
            }

            int scopes = Bit(All) | Bit(affiliation);
            return homeworld ? scopes | Bit(Homeworld) : scopes;
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
