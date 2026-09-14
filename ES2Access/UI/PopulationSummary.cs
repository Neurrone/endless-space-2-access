using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Who lives on a colony and how full it is - the two lines a planet card's population ring draws
    /// as a picture and says in no words at all.
    ///
    /// A card's ring is a row per SLOT (<see cref="PopulationRings"/>), which is the right shape for
    /// moving people about and the wrong one for the question a player arriving at a card asks: how
    /// many live here, of what kinds, and is there room. Answering it by walking seven rows and adding
    /// them up is work a sighted player does at a glance, so the card's buffer says it outright - in
    /// the game's own words for each people and its own word for the population symbol.
    ///
    /// THE MAXIMUM IS THE REAL ONE (owner ruling 2026-09-14): <c>MaxPopulation</c>, the number of
    /// slots the ring draws, and not <c>MaxPopulationUnderOverPopulation</c> - the comfortable figure
    /// the game measures its overpopulation penalty against. The ring's own rows already say which
    /// slots are the uncomfortable ones, by the band they read in.
    ///
    /// Main-thread only.
    /// </summary>
    public static class PopulationSummary
    {
        /// <summary>The symbol the game ends a population figure with, which is the only word it
        /// writes for one. Read through <see cref="AgeText.Clean"/>, which is what names it.</summary>
        public const string PopulationIcon = "[population]";

        /// <summary>
        /// The colony's two lines, appended in the order they read: who lives there, then how full it
        /// is. Nothing at all for a world nobody has settled - its ring is all empty slots and its own
        /// rows already say so.
        /// </summary>
        public static void Add(List<string> lines, ColonizedPlanet colony)
        {
            if (lines == null || colony == null)
            {
                return;
            }

            try
            {
                PlanetCardLines.AddLine(lines, Peoples(colony));
                PlanetCardLines.AddLine(
                    lines,
                    ModStrings.Format(
                        ModStrings.FractionUnit,
                        colony.PopulationCount,
                        colony.MaxPopulation,
                        AgeText.Clean(PopulationIcon)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("population: reading a colony's people threw: " + e);
            }
        }

        /// <summary>How many people of each kind live on the colony, as one comma-joined line - the
        /// model's own order, which is the order the ring seats them in. Each item is a complete
        /// phrase rather than a number glued to a name, because the count and the noun agree in half
        /// the languages this mod ships.</summary>
        private static string Peoples(ColonizedPlanet colony)
        {
            Dictionary<StaticString, Population> people = colony.PopulationsByAffinity;
            if (people == null || people.Count == 0)
            {
                return null;
            }

            MessageBuilder builder = new MessageBuilder();
            foreach (KeyValuePair<StaticString, Population> entry in people)
            {
                Population population = entry.Value;
                if (population == null || population.Count <= 0)
                {
                    continue;
                }

                string name = PopulationRings.PopulationName(population);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                builder.ListItem(
                    ModStrings.Format(ModStrings.SystemPopulationPeople, population.Count, name)
                );
            }

            return builder.Build();
        }
    }
}
