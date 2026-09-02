using System;
using System.Collections.Generic;
using ES2Access.Core.UI;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// What the mod says about a battle in its own words - which is only ever what the game writes
    /// down nowhere: how many ships were just lost, how far through the fight is.
    ///
    /// Everything a battle SAYS on screen comes from the game (the outcome word, the phase title, each
    /// ship's status sentence) and is spoken as the game wrote it. What is left here is the counting,
    /// and counting needs a form per number, which is why these are keys rather than glued fragments.
    ///
    /// The narration is written against phrases the build may not carry yet, which is why every one of
    /// them is asked for OPTIONALLY (<see cref="OptionalText.Phrase"/>): a narrator that read
    /// "battle.your-ships-lost" at the player mid-cinematic would be worse than one that said nothing.
    /// </summary>
    public static class BattleText
    {
        /// <summary>The battle narration's name for <see cref="OptionalText.Phrase"/> - the phrase for
        /// <paramref name="key"/>, or null where this build has no such phrase.</summary>
        public static string Optional(string key, params object[] args)
        {
            return OptionalText.Phrase(key, args);
        }

        /// <summary>A counted phrase in the form its number calls for (see
        /// <see cref="ModStrings.Plural"/>), or null where the build has no such phrase.</summary>
        public static string Counted(string oneKey, string manyKey, int count)
        {
            return Optional(ModStrings.PluralKey(oneKey, manyKey, count), count);
        }

        /// <summary>
        /// One line for a burst of things lost at once.
        ///
        /// A single loss is NAMED - the game has a sentence for what happened to that ship, and it is
        /// the interesting thing about it. Several at once are COUNTED instead: a salvo takes four ships
        /// in a second, and reading four names over the top of the next salvo buries the one fact that
        /// matters, which is that four went. So <paramref name="oneKey"/> takes the name and
        /// <paramref name="manyKey"/> takes the count.
        /// </summary>
        public static string Losses(IList<string> names, string oneKey, string manyKey)
        {
            if (names == null || names.Count == 0)
            {
                return null;
            }

            if (names.Count == 1)
            {
                return string.IsNullOrEmpty(names[0]) ? null : Optional(oneKey, names[0]);
            }

            return Optional(manyKey, names.Count);
        }

        /// <summary>
        /// One exchange of fire in one sentence: who shot whom, how often, what got through and of
        /// which kind, and what the shields ate or the shots wasted.
        ///
        /// The HEAD is one whole template rather than a stem with fragments glued after it, because
        /// the count and the damage kind sit inside the clause a translator has to inflect. Which
        /// head is chosen is the honest reading of the tallies and nothing more: a pair that hit
        /// nothing says so, a pair whose damage all went into shields says THAT, a weapon that
        /// answered neither kind loses the type word instead of being assigned one, and both kinds
        /// together are reported as the two sums they are.
        ///
        /// The two CLAUSES after it are the facts that do not change the sentence's shape - the
        /// shots that missed alongside the ones that landed, and the damage the shields took on top
        /// of what got through. They join through the list separator, so a translation decides its
        /// own punctuation.
        /// </summary>
        public static string Volley(FireWatch.Volley volley)
        {
            if (volley == null || string.IsNullOrEmpty(volley.Attacker)
                || string.IsNullOrEmpty(volley.Target))
            {
                return null;
            }

            string head = Head(volley);
            if (string.IsNullOrEmpty(head))
            {
                return null;
            }

            MessageBuilder said = new MessageBuilder().ListItem(head);
            if (volley.Hits > 0 && volley.Misses > 0)
            {
                said.ListItem(
                    Counted(
                        ModStrings.BattleFireMissedClause,
                        ModStrings.BattleFireMissedClauseMany,
                        volley.Misses
                    )
                );
            }

            int absorbed = Whole(volley.Absorbed);
            if (absorbed > 0 && Whole(volley.Damage) > 0)
            {
                said.ListItem(Optional(ModStrings.BattleFireShieldClause, absorbed));
            }

            return said.Build();
        }

        // Every head is a counted pair whose two forms take DIFFERENT arguments - the counted one
        // has the tally in it and the single one has no slot for it - so the count picks the KEY
        // (ModStrings.PluralKey, which knows that Russian's singular covers 21 as well as 1) and the
        // key picked then says which arguments go with it.
        private static string Head(FireWatch.Volley volley)
        {
            string attacker = volley.Attacker;
            string target = volley.Target;
            if (volley.Hits <= 0)
            {
                string missed = ModStrings.PluralKey(
                    ModStrings.BattleFireMissed,
                    ModStrings.BattleFireMissedMany,
                    volley.Misses
                );
                return missed == ModStrings.BattleFireMissed
                    ? Optional(missed, attacker, target)
                    : Optional(missed, attacker, target, volley.Misses);
            }

            int damage = Whole(volley.Damage);
            if (damage <= 0 && Whole(volley.Absorbed) > 0)
            {
                string absorbedKey = ModStrings.PluralKey(
                    ModStrings.BattleFireAbsorbed,
                    ModStrings.BattleFireAbsorbedMany,
                    volley.Hits
                );
                return absorbedKey == ModStrings.BattleFireAbsorbed
                    ? Optional(absorbedKey, attacker, target)
                    : Optional(absorbedKey, attacker, target, volley.Hits);
            }

            int energy = Whole(volley.Energy);
            int projectile = Whole(volley.Projectile);
            bool typed = Whole(volley.Untyped) <= 0;
            if (typed && energy > 0 && projectile > 0)
            {
                string mixed = ModStrings.PluralKey(
                    ModStrings.BattleFireMixed,
                    ModStrings.BattleFireMixedMany,
                    volley.Hits
                );
                return mixed == ModStrings.BattleFireMixed
                    ? Optional(mixed, attacker, target, energy, projectile)
                    : Optional(mixed, attacker, target, volley.Hits, energy, projectile);
            }

            if (typed && energy > 0)
            {
                string energyKey = ModStrings.PluralKey(
                    ModStrings.BattleFireEnergy,
                    ModStrings.BattleFireEnergyMany,
                    volley.Hits
                );
                return energyKey == ModStrings.BattleFireEnergy
                    ? Optional(energyKey, attacker, target, energy)
                    : Optional(energyKey, attacker, target, volley.Hits, energy);
            }

            if (typed && projectile > 0)
            {
                string projectileKey = ModStrings.PluralKey(
                    ModStrings.BattleFireProjectile,
                    ModStrings.BattleFireProjectileMany,
                    volley.Hits
                );
                return projectileKey == ModStrings.BattleFireProjectile
                    ? Optional(projectileKey, attacker, target, projectile)
                    : Optional(projectileKey, attacker, target, volley.Hits, projectile);
            }

            string plain = ModStrings.PluralKey(
                ModStrings.BattleFirePlain,
                ModStrings.BattleFirePlainMany,
                volley.Hits
            );
            return plain == ModStrings.BattleFirePlain
                ? Optional(plain, attacker, target, damage)
                : Optional(plain, attacker, target, volley.Hits, damage);
        }

        /// <summary>A damage figure as a listener wants it: whole points, never a decimal tail. The
        /// game's own gauges floor these; a running commentary rounds, because the difference is
        /// inaudible and the rounding keeps a small hit from reading as nothing.</summary>
        private static int Whole(float value)
        {
            return value <= 0f ? 0 : (int)Math.Round((double)value, MidpointRounding.AwayFromZero);
        }
    }
}
