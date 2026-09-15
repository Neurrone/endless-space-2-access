using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>What a planet's orbital card SAYS: the lines its review buffer is made of - the
    /// outputs, the icons it draws without words, the operations running on the world - and the
    /// game-string reading those sentences are built out of.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>What the map is saying about this world in colour alone, as the lines a planet
        /// card's review buffer carries (<see cref="AddSignals"/>) - the one reading on that card
        /// taken off the model rather than off the widgets, because no widget writes any of it and a
        /// keyboard player could reach none of it.</summary>
        private static IList<string> SignalLines(
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            List<string> lines = new List<string>(4);
            AddSignals(lines, system, planet, empire);
            return lines;
        }

        /// <summary>
        /// What the map is saying about a world through pure decoration - a coloured ring on the circle
        /// at systems zoom, with no tooltip on it anywhere (measured: every per-circle feedback image
        /// carries no <c>AgeTooltip</c> at all). A juggernaut terraforming or restoring it, an anomaly
        /// being reduced, a Sanctuary standing on it, a world there is only one of. A sighted player
        /// takes these off the colours; a keyboard player could reach none of them.
        ///
        /// One buffer line each, on the planet's own row and never in its announcement (owner ruling
        /// 2026-08-23), gated on the game's own state and read from the PLANET - so a world says the
        /// same things at every zoom, exactly as its anomalies and curiosities already do.
        ///
        /// Every word is the game's: the three juggernaut sentences it writes on the in-progress
        /// buttons one zoom step in (terraformation, restoration and anomaly reduction), its own
        /// "Remaining turns:" caption, its own Sanctuary sentences, and the title its discovery card
        /// gives a unique world (<c>%PlanetScreenUniquePlanetTitle</c>, read off the unshown prefab -
        /// "Unique Planet").
        ///
        /// A mining probe is already a row VALUE (<c>MiningProbes.Line</c>) and the curiosity ring
        /// already a counted one (<see cref="CuriosityCount"/>), so neither is repeated here.
        /// </summary>
        private static void AddSignals(
            List<string> lines,
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            try
            {
                if (planet == null || !Surveyed(system, empire))
                {
                    return;
                }

                AddTerraformationSignal(lines, planet);
                AddAnomalyReductionSignal(lines, planet);
                AddGhostSignal(lines, planet, empire);
                if (planet.IsUnique)
                {
                    AddLine(lines, Localize("%PlanetScreenUniquePlanetTitle"));
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's map signals threw: " + e);
            }
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
        private static void AddTerraformationSignal(List<string> lines, Planet planet)
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

            string leader = LeaderName(running.Empire);
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
            AddLine(lines, Remaining(sentence, running.GetRemainingTurns()));
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
        private static void AddAnomalyReductionSignal(List<string> lines, Planet planet)
        {
            ReduceAnomalyEmpireLocalAction running = planet.AnomalyReductionInProgress;
            if (running == null || (int)running.State > 2)
            {
                return;
            }

            AddLine(
                lines,
                Remaining(
                    Localize(
                        "%PlanetReduceAnomalyWithJuggernautInProgressDescription",
                        planet.LocalizedName,
                        LeaderName(running.Empire)
                    ),
                    running.GetRemainingTurns()
                )
            );
        }

        /// <summary>A Sanctuary standing on the world - the Umbral Choir's ghost colony. The gate is the
        /// card's own: the ghost exists AND this empire can see the system it belongs to
        /// (<c>PlanetLabel_SystemOrbital.RefreshPlanetInformation</c>), so a hidden one stays
        /// hidden.</summary>
        private static void AddGhostSignal(List<string> lines, Planet planet, Empire empire)
        {
            AddLine(lines, GhostWord(planet, empire));
        }

        /// <summary>The game's own sentence for a Sanctuary on this world, or nothing where the card's
        /// gate hides it - shared with the dot row (<see cref="GhostMark"/>) so the two readings can
        /// never drift into two different words for the one thing.</summary>
        private static string GhostWord(Planet planet, Empire empire)
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
                : Localize("%PlanetStatusGhostByDescription", LeaderName(ghost.Empire));
        }

        /// <summary>An empire as the game names it to this player in this sentence - the leader name
        /// with NO faction, because the three places that draw "%PlanetStatusGhostByDescription" all
        /// build it with <c>iconPrefix:false</c> (<c>PlanetLabel_SystemManagement</c> :1209,
        /// <c>PlanetLabel_SystemOrbital</c> :469, <c>HauntCircleItem</c> :17). The mod follows the
        /// game (owner ruling 2026-09-14).</summary>
        private static string LeaderName(Empire empire)
        {
            return EmpireNames.Named(empire, PlayerEmpire());
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

        /// <summary>What a planet's row says with no card on the screen: what has been found on the
        /// world, and what the map is signalling about it in colour alone
        /// (<see cref="AddSignals"/>). ONE list, so the dedupe that keeps a line from being said twice
        /// works across both halves.</summary>
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

            AddSignals(lines, system, planet, empire);
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
