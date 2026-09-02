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
                    // The turn count as a whole phrase, the same one every other countdown in the
                    // galaxy reading uses. The game draws it as a figure beside a turn ICON, and a
                    // picture is not a word: glued to the number it read as "5 Turn", which is the
                    // icon's name standing in for a sentence nobody wrote.
                    message.ListItemForcedComma(
                        ModStrings.Format(
                            ModStrings.GalaxyTurnsRemaining,
                            wrapper.MiningRemainingTurns
                        )
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
        /// symbols, joined the way every other run of fragments in the mod is joined - through the
        /// builder, whose separator a translation owns. Written with hard-coded spaces, this was one
        /// of the few places a language that joins differently could not.</summary>
        private static string Yield(GuiPlanet planet)
        {
            MessageBuilder amounts = new MessageBuilder();
            foreach (KeyValuePair<string, float> item in planet.MiningValuesBySymbol)
            {
                amounts.Fragment(
                    Gui.FormatAmount(item.Value, false, Gui.Rounding.Floor, true, 1)
                );
                amounts.Fragment(item.Key);
            }

            // Cleaned as one string at the end rather than fragment by fragment: the symbols are icon
            // tokens, and the substitution that turns them into words looks at what sits either side
            // of the seam to decide whether a space belongs there.
            return AgeText.Clean(amounts.Build());
        }
    }
}
