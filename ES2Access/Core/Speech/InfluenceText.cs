using System.Collections.Generic;
using ES2Access.Core.UI;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The sentences the mod says about influence, composed away from the game so the wording rules can
    /// be checked without a galaxy: which way a radius is going, and how a crowd of contesters reads as
    /// one line. Where those numbers and names come from is <c>ES2Access.UI.SystemInfluence</c>.
    /// </summary>
    public static class InfluenceText
    {
        /// <summary>
        /// A colony's reach and where it will be next turn.
        ///
        /// The two figures are compared AS SPOKEN - the rounded text, never the raw floats - so a
        /// radius creeping up by a thousandth says "no change" rather than promising the player growth
        /// they will never hear arrive. Any drift big enough to be heard is named in the direction it
        /// is going, because a second number with no direction word is just another number.
        ///
        /// And a reach that speaks as nothing IS nothing: the same rule read the other way. A place
        /// whose figures both round to zero says nothing at all, exactly as a place with no reach at
        /// all does - the game hands out radii of 1E-08 (ES2's pirate bases) and "Influence radius:
        /// 0.0, no change next turn" is a sentence about nothing.
        /// </summary>
        public static string Radius(float now, float next)
        {
            string here = Amount(now);
            string there = Amount(next);
            if (string.Equals(here, there))
            {
                if (Amount(0f).Equals(here))
                {
                    return null;
                }

                return ModStrings.Format(ModStrings.GalaxySystemInfluenceSteady, here, there);
            }

            return ModStrings.Format(
                next > now
                    ? ModStrings.GalaxySystemInfluenceGrowing
                    : ModStrings.GalaxySystemInfluenceShrinking,
                here,
                there
            );
        }

        /// <summary>
        /// Everyone reaching for a place, as one line, in the order they are given.
        ///
        /// Two templates rather than a conjunction glued between names: the single form, and a list
        /// form whose first slot is everybody but the last - so the joining word sits inside a
        /// translated sentence and a language that puts it elsewhere can. The names before the last are
        /// joined with the mod's own list separator, which is the same join every other spoken list in
        /// the mod uses.
        /// </summary>
        public static string Contested(IList<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return null;
            }

            if (names.Count == 1)
            {
                return ModStrings.Format(ModStrings.GalaxySystemInfluenceContested, names[0]);
            }

            return ModStrings.Format(
                ModStrings.GalaxySystemInfluenceContestedList,
                SpokenList.Items(names, names.Count - 1),
                names[names.Count - 1]
            );
        }

        /// <summary>
        /// WHOSE INFLUENCE THE CELL IS STANDING IN, as the inspect cursor crosses into it.
        ///
        /// Two things separate this from the node reading above. It is about an AREA, so there is a
        /// third thing to say beyond who and whether: how much of the cell they hold - all of it, or
        /// only part of it, which is what standing on a border sounds like. And several empires holding
        /// parts of one cell is ONE line, joined the way the contested line joins its crowd, because
        /// two sentences about the same square of sky read as two squares.
        ///
        /// The player alone is a sentence of its own - "your influence" is what the player is called
        /// everywhere else in the mod - but in a list they are named like anybody else, which is the
        /// same rule the contested line follows.
        /// </summary>
        public static string Cell(InfluenceCover cover, IList<string> names, bool playerAlone)
        {
            if (cover == InfluenceCover.None || names == null || names.Count == 0)
            {
                return null;
            }

            // A cell PROVABLY inside one empire's influence has exactly one owner - that is what the
            // proof says - so there is no list form of the "in" sentence and no key for one.
            if (cover == InfluenceCover.Whole)
            {
                return playerAlone
                    ? ModStrings.Get(ModStrings.GalaxyInspectInfluenceInYou)
                    : ModStrings.Format(ModStrings.GalaxyInspectInfluenceIn, names[0]);
            }

            return Whose(
                ModStrings.GalaxyInspectInfluenceEdge,
                ModStrings.GalaxyInspectInfluenceEdgeYou,
                ModStrings.GalaxyInspectInfluenceEdgeList,
                names,
                playerAlone
            );
        }

        /// <summary>Stepping out of influenced space into nobody's - which names what was LEFT, since
        /// the space arrived in has no owner to name and "out of yours" is the only thing about it the
        /// player did not already know. The same rule the constellation crossing follows.</summary>
        public static string Left(IList<string> names, bool playerAlone)
        {
            return Whose(
                ModStrings.GalaxyInspectInfluenceOut,
                ModStrings.GalaxyInspectInfluenceOutYou,
                ModStrings.GalaxyInspectInfluenceOutList,
                names,
                playerAlone
            );
        }

        /// <summary>One sentence for a crowd of empires: the single form, the player's own form, and a
        /// list form whose first slot is everybody but the last - the same three-template shape
        /// <see cref="Contested"/> uses, so the joining word always sits inside a translated sentence.
        /// </summary>
        private static string Whose(
            string one,
            string you,
            string list,
            IList<string> names,
            bool playerAlone
        )
        {
            if (names == null || names.Count == 0)
            {
                return null;
            }

            if (playerAlone && names.Count == 1)
            {
                return ModStrings.Get(you);
            }

            if (names.Count == 1)
            {
                return ModStrings.Format(one, names[0]);
            }

            return ModStrings.Format(
                list,
                SpokenList.Items(names, names.Count - 1),
                names[names.Count - 1]
            );
        }

        /// <summary>A radius as the player hears it: one decimal, and the trailing zero KEPT - the two
        /// figures in the sentence are compared by ear, and "6.6, growing to 7" invites the wrong
        /// arithmetic. The running culture writes the separator, as the game's own number formatting
        /// does (<c>Amplitude.Extensions.FloatExtensions</c>).</summary>
        private static string Amount(float value)
        {
            return value.ToString("0.0");
        }
    }
}
