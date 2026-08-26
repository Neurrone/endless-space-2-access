using System.Collections.Generic;
using ES2Access.Core.Map;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// One bearing said as what is down it: the stretches of fog a probe launched that way would fly
    /// through, and how far the map goes before it runs out.
    ///
    /// A probe order cannot be recalled and cannot be aimed at anything but a direction, so the choice
    /// is made entirely on what each direction is worth - which a sighted player reads off the fog on
    /// the map in a second and a listener cannot be told at all unless it is said. Every range is
    /// spoken; nothing is merged into "mostly unexplored" and nothing is dropped for being far away,
    /// because the player is choosing BETWEEN sixteen of these and a summary that hides where the fog
    /// starts hides the whole comparison.
    ///
    /// The word for "unexplored" is said once, at the front, and the ranges follow it as a list. The
    /// stretch that runs off the map is said as running to the map's edge rather than as a range,
    /// because its far end is not a place the fog ends - it is where the galaxy does.
    ///
    /// Engine-free: it says a <see cref="ProbeCorridorReading"/> and knows nothing about how one was
    /// measured.
    /// </summary>
    public static class ProbeContextText
    {
        /// <summary>The whole line for a heading, the bearing named with the sixteen-word compass
        /// (<see cref="CompassDirections.KeyForBearing16"/>).</summary>
        public static string Line(double bearing, ProbeCorridorReading reading)
        {
            return Line(ModStrings.Get(CompassDirections.KeyForBearing16(bearing)), reading);
        }

        /// <summary>The same for a heading the caller has already named - the direction word opens the
        /// sentence, so it is capitalized here rather than by whoever supplies it.</summary>
        public static string Line(string bearingWord, ProbeCorridorReading reading)
        {
            return ModStrings.Format(
                ModStrings.GalaxyProbeContext,
                Capitalized(bearingWord),
                Context(reading)
            );
        }

        /// <summary>The direction word with a capital, the template being unable to supply one for a
        /// word it does not contain. Only ever the mod's OWN words; a language that does not case its
        /// letters is unchanged, a character with no upper form being its own.</summary>
        private static string Capitalized(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return word;
            }

            char first = char.ToUpper(word[0]);
            return first == word[0] ? word : first + word.Substring(1);
        }

        /// <summary>What is down the heading, without naming the heading - for a surface that has
        /// already said which way it is talking about.</summary>
        public static string Context(ProbeCorridorReading reading)
        {
            if (reading.Spans.Count == 0)
            {
                return ModStrings.Format(ModStrings.GalaxyProbeContextExplored, reading.Edge);
            }

            bool reachesEdge = reading.ReachesEdge;
            List<string> ranges = new List<string>(reading.Spans.Count);
            for (int i = 0; i < reading.Spans.Count; i++)
            {
                UnexploredSpan span = reading.Spans[i];
                bool last = i == reading.Spans.Count - 1;
                ranges.Add(
                    last && reachesEdge
                        ? ModStrings.Format(
                            ModStrings.GalaxyProbeContextToEdge,
                            span.From,
                            reading.Edge
                        )
                        : ModStrings.Format(ModStrings.GalaxyProbeContextRange, span.From, span.To)
                );
            }

            MessageBuilder message = new MessageBuilder();
            message.Fragment(
                ModStrings.Format(
                    ModStrings.GalaxyProbeContextUnexplored,
                    SpokenList.Join(ranges)
                )
            );

            // The fog stopping short of the rim leaves the rim unsaid, and the rim is the whole point
            // of the line - so it follows the ranges as a clause of its own.
            if (!reachesEdge)
            {
                message.ListItemForcedComma(
                    ModStrings.Format(ModStrings.GalaxyProbeContextEdge, reading.Edge)
                );
            }

            return message.Build();
        }
    }
}
