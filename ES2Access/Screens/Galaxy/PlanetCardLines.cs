using System;
using System.Collections.Generic;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.PlanetCards;

namespace ES2Access.Screens
{
    /// <summary>What a planet's row on the MAP says that its card does not: what has been found on the
    /// world. What the map signals in colour alone is no longer the map's own reading - every surface
    /// that draws a planet card carries it now (<see cref="PlanetSignals"/>).</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>What a planet's row says with no card on the screen: what has been found on the
        /// world, and what the map is signalling about it in colour alone
        /// (<see cref="PlanetSignals"/>). ONE list, so the dedupe that keeps a line from being said
        /// twice works across both halves.</summary>
        private static IList<string> PlanetLines(
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            List<string> lines = new List<string>(4);
            IList<string> anomalies = AnomalyLines(system, planet, empire);
            for (int i = 0; anomalies != null && i < anomalies.Count; i++)
            {
                AddLine(lines, anomalies[i]);
            }

            PlanetSignals.Add(lines, planet, empire);
            return lines;
        }

        /// <summary>
        /// What has been found on a world, from the planet rather than from the card.
        ///
        /// The card draws the row only when the camera is in on the system, so gating on the row left
        /// a planet's anomalies readable at one zoom and gone at another. The gate that has to stay is
        /// the FOG's: a system nobody has surveyed shows grey unknowns, and the card hides this row
        /// for exactly that reason (<c>PlanetLabel_SystemOrbital.RefreshAsUnrevealedNode</c>) - which
        /// is the same threshold the scanner asks (<see cref="Surveyed"/>).
        /// </summary>
        private static IList<string> AnomalyLines(
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            try
            {
                if (planet == null || !Surveyed(system, empire))
                {
                    return null;
                }

                List<string> lines = new List<string>(planet.Anomalies.Count);
                for (int i = 0; i < planet.Anomalies.Count; i++)
                {
                    Anomaly anomaly = planet.Anomalies[i];
                    AddLine(
                        lines,
                        AgeText.Clean(new GuiAnomaly(anomaly.AnomalyDefinition, planet).Title)
                    );
                }

                return lines;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's anomalies threw: " + e);
                return null;
            }
        }
    }
}
