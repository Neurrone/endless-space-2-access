using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI.PlanetCards
{
    /// <summary>
    /// What the game is saying about a world through pure decoration - a coloured ring on the map's
    /// circle at systems zoom, with no tooltip on it anywhere (measured: every per-circle feedback
    /// image carries no <c>AgeTooltip</c> at all). A juggernaut terraforming or restoring it, an
    /// anomaly being reduced, a Sanctuary standing on it, a world there is only one of. A sighted
    /// player takes these off the colours; a keyboard player could reach none of them.
    ///
    /// Read off the PLANET rather than off any widget, so the same facts are carried by every surface
    /// that draws a planet card and by the map's own dot row alike - a world says the same things at
    /// every zoom and on every page, exactly as its anomalies and curiosities already do (owner ruling
    /// 2026-09-15; they were the map card's alone until then).
    ///
    /// One buffer line each, never in an announcement (owner ruling 2026-08-23).
    ///
    /// Every word is the game's: the three juggernaut sentences it writes on the in-progress buttons
    /// one zoom step in (terraformation, restoration and anomaly reduction), its own "Remaining turns:"
    /// caption, its own Sanctuary sentences, and the title its discovery card gives a unique world
    /// (<c>%PlanetScreenUniquePlanetTitle</c>, read off the unshown prefab - "Unique Planet").
    ///
    /// A mining probe is already a row VALUE (<c>MiningProbes.Line</c>) and the curiosity ring already
    /// a counted one (<c>PlanetCardReader.CuriosityCount</c>), so neither is repeated here.
    /// </summary>
    public static class PlanetSignals
    {
        /// <summary>
        /// The signals this world is carrying, appended to a card's review buffer.
        ///
        /// The gate is the FOG's: a system nobody has surveyed shows grey unknowns and the orbital
        /// card hides its own readings for exactly that reason
        /// (<c>PlanetLabel_SystemOrbital.RefreshAsUnrevealedNode</c>) - the same threshold the map's
        /// scanner asks (<c>GalaxyHudScreen.Surveyed</c>), asked here of the planet's own node so that
        /// a page with no map tree behind it gates identically.
        /// </summary>
        public static void Add(List<string> lines, Planet planet, Empire empire)
        {
            try
            {
                if (planet == null || empire == null || !Surveyed(planet, empire))
                {
                    return;
                }

                AddTerraformation(lines, planet, empire);
                AddAnomalyReduction(lines, planet, empire);
                PlanetCardLines.AddLine(lines, GhostWord(planet, empire));
                if (planet.IsUnique)
                {
                    PlanetCardLines.AddLine(lines, UniqueTitle());
                }
            }
            catch (Exception e)
            {
                Log.Warn("planet card: reading a planet's signals threw: " + e);
            }
        }

        /// <summary>Whether the game has let this empire see what the planets in this world's system
        /// actually are - the circles switch from grey unknowns to real planets at this
        /// threshold.</summary>
        private static bool Surveyed(Planet planet, Empire empire)
        {
            StarSystemNode node = planet.StarSystemNode;
            return node != null
                && (int)node.Exploration[empire] >= (int)EntityExploration.State.Revealed;
        }

        /// <summary>
        /// A juggernaut turning the world into something else, and how long is left.
        ///
        /// Terraformation and RESTORATION are one field of the planet's and two different sentences,
        /// told apart exactly as the game tells them apart - by the tags on the terraformation being
        /// carried out (<c>InitiateRestorationEmpireActionFleetActionDefinition.CheckConstructibleTags</c>:
        /// restoration is the one tagged <c>PlanetTerraformationFromDestroyed</c>, and anything tagged
        /// <c>PlanetTerraformationOnlyViaSystem</c> is neither, which is why the map draws no button
        /// for it). State above 2 is one the game has stopped drawing at all.
        /// </summary>
        private static void AddTerraformation(List<string> lines, Planet planet, Empire looking)
        {
            TerraformPlanetEmpireLocalAction running = planet.TerraformationInProgress;
            if (running == null || (int)running.State > 2)
            {
                return;
            }

            PlanetTerraformationDefinition definition = running.PlanetTerraformationDefinition;
            if (
                definition == null
                || definition.Tags.Contains(
                    InitiateTerraformationEmpireActionFleetActionDefinition.InvalidTag
                )
            )
            {
                return;
            }

            string leader = LeaderName(running.Empire, looking);
            bool restoring = definition.Tags.Contains(
                InitiateTerraformationEmpireActionFleetActionDefinition.DestroyedTag
            );
            string sentence = restoring
                // The game's own call passes two arguments to a template that asks for {0} and {2}, so
                // its own drawing leaves a slot unfilled; the third argument here is the same leader
                // name the slot is asking for, and an unfilled result is dropped by Localize.
                ? Localize(
                    "%PlanetRestoreWithJuggernautInProgressDescription",
                    planet.LocalizedName,
                    leader,
                    leader
                )
                : Localize(
                    "%PlanetTerraformWithJuggernautInProgressDescription",
                    planet.LocalizedName,
                    TerraformationTarget(definition),
                    leader
                );
            PlanetCardLines.AddLine(lines, Remaining(sentence, running.GetRemainingTurns()));
        }

        /// <summary>What the world is being turned INTO, in the game's own alternative title for the
        /// terraformation - the one its own card puts in that slot.</summary>
        private static string TerraformationTarget(PlanetTerraformationDefinition definition)
        {
            try
            {
                IGuiConstructible wrapper =
                    Gui.GuiWrapperProviderService.InstantiateIGuiConstructible(definition);
                return wrapper == null ? null : AgeText.Clean(Gui.Localize(wrapper.AltTitle));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A juggernaut reducing one of the world's anomalies, in the game's own sentence for
        /// the state - the one it writes onto the in-progress button a rival is shown
        /// (<c>PlanetLabel_SystemOrbital.RefreshAnomalyReductionStatus</c> :975), with the arguments in
        /// that call's own order: the planet, then the empire doing it.</summary>
        private static void AddAnomalyReduction(List<string> lines, Planet planet, Empire looking)
        {
            ReduceAnomalyEmpireLocalAction running = planet.AnomalyReductionInProgress;
            if (running == null || (int)running.State > 2)
            {
                return;
            }

            PlanetCardLines.AddLine(
                lines,
                Remaining(
                    Localize(
                        "%PlanetReduceAnomalyWithJuggernautInProgressDescription",
                        planet.LocalizedName,
                        LeaderName(running.Empire, looking)
                    ),
                    running.GetRemainingTurns()
                )
            );
        }

        /// <summary>The game's own sentence for a Sanctuary on this world, or nothing where the card's
        /// gate hides it: the ghost exists AND this empire can see the system it belongs to
        /// (<c>PlanetLabel_SystemOrbital.RefreshPlanetInformation</c>), so a hidden one stays hidden.
        /// Shared with the map's dot row (<c>GalaxyHudScreen.GhostMark</c>) so the two readings can
        /// never drift into two different words for the one thing.</summary>
        public static string GhostWord(Planet planet, Empire empire)
        {
            ColonizedPlanet ghost = planet == null ? null : planet.GhostColonizedPlanet;
            if (
                ghost == null
                || ghost.ColonizedStarSystem == null
                || (int)ghost.ColonizedStarSystem.Visibility[empire]
                    < (int)EntityVisibility.Layer.Known
            )
            {
                return null;
            }

            return ghost.Empire == empire
                ? Localize("%PlanetStatusGhostDescription")
                : Localize("%PlanetStatusGhostByDescription", LeaderName(ghost.Empire, empire));
        }

        /// <summary>The game's own title for a world there is only one of, read off the discovery card
        /// it never shows twice.</summary>
        public static string UniqueTitle()
        {
            return Localize("%PlanetScreenUniquePlanetTitle");
        }

        /// <summary>An empire as the game names it to this player in these sentences - the leader name
        /// with NO faction, because the three places that draw "%PlanetStatusGhostByDescription" all
        /// build it with <c>iconPrefix:false</c> (<c>PlanetLabel_SystemManagement</c> :1209,
        /// <c>PlanetLabel_SystemOrbital</c> :469, <c>HauntCircleItem</c> :17). The mod follows the
        /// game (owner ruling 2026-09-14).</summary>
        private static string LeaderName(Empire empire, Empire looking)
        {
            return EmpireNames.Named(empire, looking);
        }

        /// <summary>A state sentence with the turns left after it, in the game's own caption - the same
        /// pair its in-progress buttons write into their tooltips.</summary>
        private static string Remaining(string sentence, int turns)
        {
            if (string.IsNullOrEmpty(sentence))
            {
                return null;
            }

            string caption = Localize("%PanelFeatureRemainingTurnsTitle");
            return string.IsNullOrEmpty(caption)
                ? sentence
                : new MessageBuilder()
                    .ListItem(sentence)
                    .ListItem(caption + " " + turns)
                    .Build();
        }

        /// <summary>
        /// One of the game's own phrases, filled in and cleaned - and DROPPED where the fill left a
        /// template slot standing.
        ///
        /// A phrase still holding a "{0}" is one the game has not finished writing: the map has one
        /// (its restoration sentence asks for a third argument its own call never passes), and speaking
        /// a slot marker is worse than saying nothing.
        /// </summary>
        private static string Localize(string key, params object[] arguments)
        {
            try
            {
                string text = AgeText.Clean(
                    arguments == null || arguments.Length == 0
                        ? Gui.Localize(key)
                        : Gui.Localize(key, arguments)
                );
                return Unfilled(text) ? null : text;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether a filled-in phrase still carries a slot the game never filled - "{" and a
        /// digit, which is what an unpassed argument leaves behind.</summary>
        private static bool Unfilled(string text)
        {
            for (int i = 0; text != null && i + 1 < text.Length; i++)
            {
                if (text[i] == '{' && text[i + 1] >= '0' && text[i + 1] <= '9')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
