using System;

namespace ES2Access.Core.Speech
{
    /// <summary>Which counted form a number calls for in the language being spoken.</summary>
    public enum PluralForm
    {
        /// <summary>The singular sentence.</summary>
        One,

        /// <summary>The Slavic paucal - 2, 3, 4 and the numbers ending in them.</summary>
        Few,

        /// <summary>Everything the other two forms do not claim, which in a two-form language is
        /// every number but one.</summary>
        Many,
    }

    /// <summary>
    /// The plural category a count falls into, per language, following the CLDR cardinal rules for
    /// the languages Endless Space 2 ships in.
    ///
    /// Only the distinctions the mod's templates can express are modelled: CLDR's "zero", "two" and
    /// "other" collapse into <see cref="PluralForm.Many"/>, because no language the game ships
    /// needs them separated. A language not named here - and a null language, which is what a build
    /// running on the compiled-in English strings has - gets English's rule, the two-form default a
    /// translator can always fill in.
    ///
    /// Negative counts take their absolute value's form. Nothing the mod counts is negative, but a
    /// count that arrives negative through a bug should still be a sentence rather than an
    /// exception or a silently wrong form.
    /// </summary>
    public static class PluralRules
    {
        /// <summary>
        /// What a locale file appends to a plural pair's MANY key to carry the paucal form that
        /// <see cref="PluralForm.Few"/> asks for ("system.outposts" -> "system.outposts.few").
        ///
        /// It lives here rather than beside the keys in <see cref="ModStrings"/> because it is not
        /// a key: every public string constant on ModStrings is one, and the translation template
        /// is validated against exactly that set.
        /// </summary>
        public const string FewSuffix = ".few";

        /// <summary>
        /// What a locale file appends to a plural pair's MANY key to carry the sentence a SINGULAR
        /// count other than one calls for ("fleet.route-arrives-turns" ->
        /// "fleet.route-arrives-turns.one").
        ///
        /// It exists because <see cref="PluralForm.One"/> is not "the number one" in every language.
        /// Russian puts 21, 31 and every other n1 in the singular, and a pair whose ONE key is a
        /// sentence with no number in it at all - "Arrives this turn" against "Arrives in {0} turns"
        /// - would then tell a Russian player that a twenty-one turn journey ends this turn. The
        /// paucal has no such problem, because no language's paucal covers one.
        ///
        /// So where the file carries this key, <see cref="ModStrings.PluralKey"/> uses it for exactly
        /// that case: the singular form with a count that is not one. A count of one always takes the
        /// pair's own ONE key, in every language, and a language whose singular covers only one -
        /// Polish, English - never reaches this key at all.
        ///
        /// It hangs off the MANY key for the same reason <see cref="FewSuffix"/> does: the MANY
        /// sentence is the one with the number in it, so it is the sentence this form is written
        /// from.
        /// </summary>
        public const string OneSuffix = ".one";

        public static PluralForm For(string language, int count)
        {
            int n = count == int.MinValue ? int.MaxValue : Math.Abs(count);
            switch (language)
            {
                case "polish":
                    // Polish gives 1 alone the singular; 21, 101 and the rest take the MANY form.
                    if (n == 1)
                    {
                        return PluralForm.One;
                    }

                    return IsFew(n) ? PluralForm.Few : PluralForm.Many;

                case "russian":
                    // Russian, unlike Polish, gives 21, 101 and every other n1 the singular too.
                    if (n % 10 == 1 && n % 100 != 11)
                    {
                        return PluralForm.One;
                    }

                    return IsFew(n) ? PluralForm.Few : PluralForm.Many;

                case "french":
                case "brazilian":
                    // French and Brazilian Portuguese put zero in the singular.
                    return n <= 1 ? PluralForm.One : PluralForm.Many;

                default:
                    return n == 1 ? PluralForm.One : PluralForm.Many;
            }
        }

        // The Slavic paucal: ends in 2, 3 or 4, except the teens, which are all MANY.
        private static bool IsFew(int n)
        {
            int last = n % 10;
            int teens = n % 100;
            return last >= 2 && last <= 4 && (teens < 12 || teens > 14);
        }
    }
}
