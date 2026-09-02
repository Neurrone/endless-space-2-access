using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;

namespace ES2Access.UI
{
    /// <summary>
    /// The five outputs a planet card draws, in both of the shapes it draws them in - written as
    /// numbers for a colony (<see cref="Numbers"/>), rated as a row of pips for a world nobody has
    /// settled (<see cref="Ratings"/>). Both shapes bind the output's title to its figure with a
    /// colon (owner ruling 2026-08-25), so a player hears one sentence per output whichever card
    /// they are on.
    ///
    /// The three planet cards that switch shapes - the orbital one on the map, the management page's and
    /// the empire page's - draw a colony's outputs as written numbers and an unsettled world's as a row
    /// of five pips per output (<c>PlanetLabel_SystemOrbital.RefreshFIDSI</c>,
    /// <c>PlanetLabel_SystemManagement.RefreshScoreLine</c>, <c>PlanetCard.RefreshScoreLine</c>, all
    /// landing in <c>FidsScoreLine.RefreshSimple</c>), and none of those prefabs draws the caption that
    /// would say which band the pips mean. A keyboard player could reach none of it, so the reading is
    /// composed here once and all three screens use it: one line per output, in the card's own drawn
    /// order, saying the game's title for the output, how many cells are lit, and the band the game's
    /// own thresholds put the value in (owner ruling 2026-08-25 - the title is bound to the count by a
    /// colon, and the band follows after a comma).
    ///
    /// Computed from the PLANET's simulation value and the thresholds table - the game's own
    /// arithmetic in <c>FidsScoreLine.Refresh</c>, cell <c>i</c> lit above <c>thresholds[i]</c>'s floor
    /// and the band the one whose range holds the value - and never from the drawn cells' alpha, so a
    /// row reads the same whatever the card's fade-in animation is doing. The band is the game's word
    /// and the game's alone: none of the three prefabs captions the row (measured: every
    /// <c>FidsScoreLine.Caption</c> on them is null, and the simple refresh all three use would have
    /// written "None" on all five), so nothing here is a paraphrase of drawn text.
    ///
    /// The two cutscene-ish cards that draw the same pip table - the planet page's
    /// <c>PlanetLabel_PlanetOverview</c> and the discovery flythrough's
    /// <c>PlanetLabel_SystemDiscovery</c> - are deliberately NOT callers: their prefab does caption
    /// each row and their refresh writes the band into it, so the screens that read them read the
    /// game's own drawn words instead (measured 2026-08-25).
    ///
    /// Whether the card is drawing this shape at all is the CALLER's question, because the screens
    /// gate it differently: the map hides the whole table for a world the empire has not surveyed.
    /// </summary>
    public static class PlanetOutputs
    {
        /// <summary>
        /// One line per output the card WRITES as a number - a colony's, and the potential of a world
        /// the planet page draws numbers for. The caller picks the simulation object, because the
        /// screens differ on which one the card is reading (the colony's, the colony a pending
        /// colonization is about to make, or the planet's own), and it gates the shape.
        ///
        /// The figure is the game's own formatting of the value - grouped and floored, the way every
        /// card writes it.
        /// </summary>
        public static IList<string> Numbers(
            Amplitude.Unity.Simulation.SimulationObject simulation,
            FidsiEnumerator fidsi
        )
        {
            List<string> lines = new List<string>();
            if (simulation == null || fidsi == null || fidsi.FidsiProperties == null)
            {
                return lines;
            }

            int count = Math.Min(fidsi.DisplayedProperties, fidsi.FidsiProperties.Count);
            for (int i = 0; i < count; i++)
            {
                GuiSimulationProperty property = fidsi.FidsiProperties[i];
                if (property == null)
                {
                    continue;
                }

                string line = Number(property.Name, simulation);
                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>One written output, for a screen that declares its five as nodes of their own
        /// rather than as lines of a card.</summary>
        public static string Number(
            Amplitude.StaticString name,
            Amplitude.Unity.Simulation.SimulationObject simulation
        )
        {
            return ModStrings.Format(
                ModStrings.CaptionedColon,
                Localized(name, true),
                Amount(simulation.GetPropertyValue(name))
            );
        }

        /// <summary>A figure the way the game writes it on the card.</summary>
        private static string Amount(float value)
        {
            try
            {
                return Gui.FormatAmount(value, true, Gui.Rounding.Floor, false, 0);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One line per rated output, empty where the card is not rating any. The caller adds
        /// them to the card's lines under its own policy.</summary>
        public static IList<string> Ratings(
            Planet planet,
            FidsiEnumerator fidsi,
            FidsiParametersGuiElement parameters
        )
        {
            List<string> lines = new List<string>();
            if (fidsi == null || fidsi.FidsiProperties == null)
            {
                return lines;
            }

            FidsiParametersGuiElement.FidsThreshold[] thresholds =
                parameters == null ? null : parameters.FidsThresholds;
            Amplitude.Unity.Simulation.SimulationObject simulation =
                planet == null ? null : planet.SimulationObject;
            if (thresholds == null || thresholds.Length < 2 || simulation == null)
            {
                return lines;
            }

            int count = Math.Min(fidsi.DisplayedProperties, fidsi.FidsiProperties.Count);
            for (int i = 0; i < count; i++)
            {
                GuiSimulationProperty property = fidsi.FidsiProperties[i];
                if (property == null)
                {
                    continue;
                }

                float value = simulation.GetPropertyValue(property.Name);
                int lit = 0;
                string band = thresholds[0].Caption;
                for (int cell = 1; cell < thresholds.Length; cell++)
                {
                    if (value > thresholds[cell].MinValue)
                    {
                        lit++;
                        if (value <= thresholds[cell].MaxValue)
                        {
                            band = thresholds[cell].Caption;
                        }
                    }
                }

                string line = new MessageBuilder()
                    .ListItem(
                        ModStrings.Format(
                            ModStrings.CaptionedColon,
                            Localized(property.Name, true),
                            ModStrings.Format(ModStrings.Fraction, lit, thresholds.Length - 1)
                        )
                    )
                    .ListItem(Localized(band, false))
                    .Build();
                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>The game's own words for a property or a band, never a phrase of this mod's - and
        /// nothing at all where the corpus has no such string, which is <see cref="AgeText.Title"/>'s
        /// guard.</summary>
        private static string Localized(string key, bool asTitle)
        {
            try
            {
                return AgeText.Title(asTitle ? Gui.GetLocalizedTitle(key) : key);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
