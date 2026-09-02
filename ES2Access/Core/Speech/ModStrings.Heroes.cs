namespace ES2Access.Core.Speech
{
    /// <summary>
    /// HEROES AND POPULATION: what a hero's own pages say, and the words a population
    /// unit and its slots are moved and counted by.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
        // A hero's own window. Its heading is the game's own and is declared where it is drawn, so the
        // screen name is only a fallback for the frames before the window has written one - and on the
        // skill page the heading is what names the wheel, which is why the wheel needs no word here. A
        // skill dot needs two: the game paints the level a skill stands at, and the level a pending pick
        // would take it to, as a ring of coloured arcs round the dot and writes no number anywhere.
        public const string ScreenHeroInspection = "screen.hero-inspection";
        public const string HeroSkillLevel = "hero.skill-level";
        public const string HeroSkillPending = "hero.skill-pending";

        /// <summary>A ring of the wheel, which the game names nowhere: it draws the rings as concentric
        /// arcs and writes only the threshold each one opens at, as a legend with a leader line pointing
        /// into the wheel. So the mod says which ring it is, and says the threshold as the sentence the
        /// leader line makes for anyone who can follow it.</summary>
        public const string HeroSkillRing = "hero.skill-ring";
        public const string HeroSkillRingPoint = "hero.skill-ring-point";
        public const string HeroSkillRingPoints = "hero.skill-ring-points";

        public const string ScreenGovernment = "screen.government";
        public const string ScreenLaws = "screen.laws";
        public const string ScreenPopulation = "screen.population";

        /// <summary>The two markers the population screen draws on a people's row for a gene hunter
        /// (Horatio) and nowhere else: one says this people has already been spliced into the empire's
        /// own, the other that there are now enough of them to splice. The game draws both as bare
        /// pictures with no text and no tooltip on them (<c>PopulationAffinityFilter</c>
        /// :87-99 only flips <c>Visible</c>), so these are the only words there are for them.</summary>
        public const string PopulationAssimilated = "population.assimilated";
        public const string PopulationReadyForAssimilation = "population.ready-for-assimilation";

        // --- screens batch (2026-08-22) ---

        /// <summary>One mark on the population overview's collection track: how many of a people it
        /// takes, and whether the empire has that many. The game draws the number and shows the answer
        /// by fading the circle, and writes neither in words anywhere - so both phrases are the mod's,
        /// each a whole sentence rather than a number with a word stuck on the end.</summary>
        public const string PopulationThresholdReached = "population.threshold-reached";
        public const string PopulationThresholdNotReached = "population.threshold-not-reached";

        // --- end screens batch ---
    }
}
