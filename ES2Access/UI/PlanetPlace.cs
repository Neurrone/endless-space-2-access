using System;
using ES2Access.Core.Speech;
using ES2Access.Screens;

namespace ES2Access.UI
{
    /// <summary>
    /// ONE way of saying where a world is, for every surface that names one.
    ///
    /// The galaxy names most of its planets after the star they orbit - "Auriga III" - and a phrase
    /// that then added the system would say the star twice. Some do not: a colony the player has
    /// renamed, and the unique worlds the game ships with names of their own, carry nothing that says
    /// which sky they are in. So the rule is the drawn name's own (owner ruling 2026-09-10): the
    /// planet's name, and the system after it ONLY where the name does not already begin with the
    /// star's. A thing standing ON a world is then "&lt;thing&gt; on &lt;this phrase&gt;" wherever it is said -
    /// the scanner's anomalies, curiosities and deposits, a quest pin planted on a world, and the
    /// colonizable worlds.
    ///
    /// The system's name is the map's, which means this may only be asked about a system the map is
    /// naming: the caller gates on that (<see cref="MapVisibility.Perceived"/>), and a place the
    /// picture is withholding is said in the mod's own unexplored words instead.
    /// </summary>
    internal static class PlanetPlace
    {
        /// <summary>Where a world is, as one phrase. Null where the map has no name for the world at
        /// all.</summary>
        public static string Of(StarSystemNode system, Planet planet, Empire empire)
        {
            string name = GalaxyHudScreen.PlanetName(system, planet, empire);
            if (string.IsNullOrEmpty(name) || system == null)
            {
                return name;
            }

            string star = AgeText.Clean(system.LocalizedName);
            if (
                string.IsNullOrEmpty(star)
                || name.StartsWith(star, StringComparison.CurrentCulture)
            )
            {
                return name;
            }

            return ModStrings.Format(ModStrings.GalaxyPlanetPlace, name, star);
        }
    }
}
