using System.Collections.Generic;

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

            MessageBuilder others = new MessageBuilder();
            for (int i = 0; i < names.Count - 1; i++)
            {
                others.ListItem(names[i]);
            }

            return ModStrings.Format(
                ModStrings.GalaxySystemInfluenceContestedList,
                others.Build(),
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
