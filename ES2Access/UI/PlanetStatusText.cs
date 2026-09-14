using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;

namespace ES2Access.UI
{
    /// <summary>
    /// What has become of a planet - colonized, an outpost, destroyed, too hostile, not allowed yet,
    /// or free to settle - in the game's own words.
    ///
    /// The star system page does not need this: its card DRAWS the state as a label with the sentence
    /// on the label's tooltip, and the drawn text is the oracle there
    /// (<c>PlanetLabel.RefreshPlanetStatus</c> :232-331 writes both). The EMPIRE page's card conveys
    /// exactly the same fact by TINTING the planet's name and nothing else
    /// (<c>PlanetCard.RefreshPlanetName</c> :441-465) - the player's colour for a colony, the
    /// colonizable colour where the planet may be settled, the uncolonizable one otherwise - so a
    /// keyboard player heard "einstein III, Ice" where a sighted one saw a world greyed out as
    /// unreachable.
    ///
    /// A colour is not text to read, so the state is re-derived here from the model, branch for branch
    /// in the same order the game's own refresh takes them, localizing the SAME keys it localizes -
    /// including the failure formatter that appends the missing-technology sentence and the DLC16
    /// sentences about restoring a destroyed world. Nothing is invented: every string is one the game
    /// would have written into that label and its tooltip on the other page.
    ///
    /// Main-thread only.
    /// </summary>
    public static class PlanetStatusText
    {
        /// <summary>The word the game puts on the status label - "Colonized", "Inhospitable" - or null
        /// for a planet the model cannot answer for. A key the string files never answered is silence
        /// rather than a "%key" read aloud (<see cref="AgeText.Title"/>).</summary>
        public static string Title(Planet planet)
        {
            try
            {
                string title;
                string description;
                Read(planet, out title, out description);
                return title;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The sentence the game puts on the status label's tooltip, as the spoken lines a
        /// review buffer walks - the same split the drawn tooltip gets
        /// (<see cref="AgeText.Lines"/>), because the game writes its reason and its hint as separate
        /// lines of one string.</summary>
        public static IList<string> Description(Planet planet)
        {
            try
            {
                string title;
                string description;
                Read(planet, out title, out description);
                return AgeText.Lines(description);
            }
            catch (Exception)
            {
                return new string[0];
            }
        }

        /// <summary>
        /// <c>PlanetLabel.RefreshPlanetStatus</c> (:232-331) without the drawing: the branches in the
        /// game's own order, so a planet that is both a colony and unreachable reads as the game reads
        /// it.
        ///
        /// ONE failure list across both colonization checks and never cleared between them, which is
        /// what the game does: neither check clears what it is handed and neither adds anything while
        /// it succeeds (<c>Planet.IsEmpireAbleToColonize</c> :903-919,
        /// <c>IsEmpireAllowedToColonize</c> :809), so the list the second check formats its sentence
        /// from holds exactly the reasons the game's own would.
        /// </summary>
        private static void Read(Planet planet, out string title, out string description)
        {
            title = null;
            description = null;
            if (planet == null)
            {
                return;
            }

            ColonizedPlanet colony = planet.ColonizedPlanet;
            ColonizedStarSystem system = colony == null ? null : colony.ColonizedStarSystem;
            if (system != null)
            {
                bool mine = system.Empire == Gui.PlayerEmpire;
                if (system.State == StarSystemState.Colony)
                {
                    Say(
                        mine ? "%PlanetStatusColonizedByMeTitle" : "%PlanetStatusColonizedByEnemyTitle",
                        mine
                            ? "%PlanetStatusColonizedByMeDescription"
                            : "%PlanetStatusColonizedByEnemyDescription",
                        out title,
                        out description
                    );
                    return;
                }

                if (system.State == StarSystemState.Outpost)
                {
                    Say(
                        mine ? "%PlanetStatusOutpostTitle" : "%PlanetStatusOutpostByTitle",
                        mine ? "%PlanetStatusOutpostDescription" : "%PlanetStatusOutpostByDescription",
                        out title,
                        out description
                    );
                    return;
                }
            }

            List<FailureInfo> failures = new List<FailureInfo>();
            if (planet.IsDestroyed)
            {
                title = AgeText.Title("%PlanetStatusDestroyedTitle");
                description = AgeText.Clean(Destroyed(planet));
                return;
            }

            if (!planet.IsEmpireAbleToColonize(Gui.PlayerEmpire, failures))
            {
                title = AgeText.Title("%PlanetStatusHostileTitle");
                description = AgeText.Clean(
                    Gui.FormatFailureInfos("%PlanetStatusHostileDescription", failures)
                );
                return;
            }

            if (!planet.IsEmpireAllowedToColonize(Gui.PlayerEmpire, failures))
            {
                title = AgeText.Title("%PlanetStatusUnavailableTitle");
                description = AgeText.Clean(
                    Gui.FormatFailureInfos("%PlanetStatusUnavailableDescription", failures)
                );
                return;
            }

            Say(
                "%PlanetStatusColonizableTitle",
                "%PlanetStatusColonizableDescription",
                out title,
                out description
            );
        }

        private static void Say(
            string titleKey,
            string descriptionKey,
            out string title,
            out string description
        )
        {
            title = AgeText.Title(titleKey);
            description = AgeText.Title(descriptionKey);
        }

        /// <summary>What the game says about a world somebody has blown up, with the two sentences the
        /// Hisshos expansion adds about putting it back (<c>PlanetLabel.RefreshPlanetStatus</c>
        /// :284-292): whether it CAN be restored depends on the planet being an ordinary one, and the
        /// sentence names the module that would do it.
        ///
        /// The expansion's name is written out because the game keeps it on an internal field
        /// (<c>DownloadableContent16.ReadOnlyName</c>), which another assembly cannot read.</summary>
        private static string Destroyed(Planet planet)
        {
            string text = Gui.Localize("%PlanetStatusDestroyedDescription");
            IDownloadableContentService service =
                Services.GetService<IDownloadableContentService>();
            if (service == null || !service.IsShared(Hisshos))
            {
                return text;
            }

            return planet.IsUnique
                ? text + Gui.Localize("%PlanetStatusUniquePlanetCantBeRestoredDescription")
                : text
                    + Gui.Localize(
                        "%PlanetStatusCanBeRestoredDescription",
                        Gui.GetLocalizedTitle("ModuleSupportJuggernautTerraformer2", true)
                    );
        }

        private static readonly Amplitude.StaticString Hisshos = new Amplitude.StaticString(
            "DLCHisshos"
        );
    }
}
