using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The mining probe somebody has fixed to a planet, said the way the game says it.
    ///
    /// The game surfaces this in the planet's own dossier and nowhere else - one panel feature
    /// (<c>PanelFeatureMiningProbe.Bind</c>) that writes a sentence naming whose probe it is, and
    /// then, for the player's OWN probe only, what it is pulling out and how long it has left. So a
    /// player walking the planets hears that a rival has staked one of their worlds without hovering
    /// every planet in the system, which is what a sighted player learns by seeing the ring animate
    /// on the card.
    ///
    /// The GATES are the game's, kept exactly: the owner's leader name is unconditional (the game
    /// writes it for anybody's probe), and the output and the countdown are the player's own probe's
    /// alone. Nothing here reads a number the game would have refused to draw.
    ///
    /// Main-thread only.
    /// </summary>
    public static class MiningProbes
    {
        /// <summary>What a planet's mining probe says, or null where there is none.</summary>
        public static string Line(Planet planet)
        {
            try
            {
                if (planet == null)
                {
                    return null;
                }

                GuiPlanet wrapper = new GuiPlanet(planet);
                if (!wrapper.IsMining)
                {
                    return null;
                }

                GuiEmpire owner =
                    Gui.GuiWrapperProviderService.GetGuiEmpire(wrapper.MiningEmpire);
                MessageBuilder message = new MessageBuilder();
                message.Fragment(
                    AgeText.Clean(
                        Gui.Localize(
                            "%PanelFeatureMiningProbeDescription",
                            owner.GetLeaderName(Gui.PlayerEmpire)
                        )
                    )
                );

                if (owner.Index == Gui.PlayerEmpire.Index)
                {
                    message.ListItemForcedComma(Yield(wrapper));
                    message.ListItemForcedComma(
                        AgeText.Clean(wrapper.MiningRemainingTurns + "[turn]")
                    );
                }

                return message.Build();
            }
            catch (Exception e)
            {
                Log.Warn("planet: reading a mining probe threw: " + e);
                return null;
            }
        }

        /// <summary>What the probe is pulling out, in the game's own amounts and its own resource
        /// symbols, joined the way the feature joins them.</summary>
        private static string Yield(GuiPlanet planet)
        {
            string text = string.Empty;
            foreach (KeyValuePair<string, float> item in planet.MiningValuesBySymbol)
            {
                if (text.Length > 0)
                {
                    text += " ";
                }

                text +=
                    Gui.FormatAmount(item.Value, false, Gui.Rounding.Floor, true, 1)
                    + " "
                    + item.Key;
            }

            return AgeText.Clean(text);
        }
    }
}
