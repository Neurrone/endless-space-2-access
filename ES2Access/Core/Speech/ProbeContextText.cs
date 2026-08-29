using System.Collections.Generic;
using ES2Access.Core.Map;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// One bearing said as what is down it: how much of what a launch that way would reveal is
    /// already known, then the stretches of fog the probe would fly through, and how far the map goes
    /// before it runs out.
    ///
    /// The share comes FIRST and is one number, because a listener choosing between sixteen of these
    /// cannot hold six ranges each in their head: "35 percent explored" is enough to skip a bearing or
    /// to keep listening to it, and everything after it is why. It is always said - a flight line with
    /// no fog on it at all can still be a corridor whose flanks are half dark, so "fully explored to
    /// the map edge" is not the same claim and must not silence this one
    /// (<see cref="ProbeFootprint"/>).
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
    /// Then, and only then, what is unexplored ALONGSIDE the line: a clause of its own, named by the
    /// compass word of the side it is on, so that a flight line the probe would find nothing on is
    /// never announced as fog. Its ranges never fold into the map's edge - the main clause has already
    /// said where the edge is, and saying it twice in one sentence is what makes the second one sound
    /// like a different number. Two sides carrying the identical stretches are said once, as both.
    ///
    /// Engine-free: it says a <see cref="ProbeCorridorReading"/> and knows nothing about how one was
    /// measured.
    /// </summary>
    public static class ProbeContextText
    {
        /// <summary>The whole line for a heading, the bearing named with the sixteen-word compass
        /// (<see cref="CompassDirections.KeyForBearing16"/>).</summary>
        public static string Line(
            double bearing,
            ProbeCorridorReading reading,
            ProbeFootprint footprint
        )
        {
            return Line(
                ModStrings.Get(CompassDirections.KeyForBearing16(bearing)),
                reading,
                footprint
            );
        }

        /// <summary>The same for a heading the caller has already named - the direction word opens the
        /// sentence, so it is capitalized here rather than by whoever supplies it.</summary>
        public static string Line(
            string bearingWord,
            ProbeCorridorReading reading,
            ProbeFootprint footprint
        )
        {
            return ModStrings.Format(
                ModStrings.GalaxyProbeContext,
                Capitalized(bearingWord),
                Context(reading, footprint)
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
        /// already said which way it is talking about. The share the launch would find already known
        /// leads, and the template joins it to the detail, so a language that would rather end on the
        /// summary than open with it can turn the sentence round.</summary>
        public static string Context(ProbeCorridorReading reading, ProbeFootprint footprint)
        {
            return ModStrings.Format(
                ModStrings.GalaxyProbeContextPercentExplored,
                footprint.PercentExplored,
                Line(reading) + Alongside(reading)
            );
        }

        /// <summary>What the probe would fly THROUGH - the flight line's own fog and the rim.
        /// </summary>
        private static string Line(ProbeCorridorReading reading)
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

        /// <summary>What the probe would uncover in PASSING, as its own clause per side - or nothing
        /// at all, which is most bearings. The two sides are the heading turned a quarter circle each
        /// way; when they hold the same stretches they are one clause naming both, since hearing the
        /// same six numbers twice tells the player nothing the word "both" does not.</summary>
        private static string Alongside(ProbeCorridorReading reading)
        {
            string clockwise = Ranges(reading.Clockwise);
            string counter = Ranges(reading.CounterClockwise);
            if (clockwise == null && counter == null)
            {
                return string.Empty;
            }

            if (clockwise == counter)
            {
                return ModStrings.Format(
                    ModStrings.GalaxyProbeContextAlongsideBoth,
                    clockwise
                );
            }

            return Side(reading.Bearing + 90.0, clockwise)
                + Side(reading.Bearing - 90.0, counter);
        }

        /// <summary>One side's clause, empty when that side has nothing unexplored beside the line.
        /// </summary>
        private static string Side(double bearing, string ranges)
        {
            return ranges == null
                ? string.Empty
                : ModStrings.Format(
                    ModStrings.GalaxyProbeContextAlongside,
                    ModStrings.Get(CompassDirections.KeyForBearing16(bearing)),
                    ranges
                );
        }

        /// <summary>Plain ranges, in the same "{a}-{b}" the line's own stretches are said in and joined
        /// the same way - null where there are none, so that a side with nothing to report is silent
        /// rather than said as an empty list.</summary>
        private static string Ranges(IList<UnexploredSpan> spans)
        {
            if (spans.Count == 0)
            {
                return null;
            }

            List<string> ranges = new List<string>(spans.Count);
            for (int i = 0; i < spans.Count; i++)
            {
                ranges.Add(
                    ModStrings.Format(
                        ModStrings.GalaxyProbeContextRange,
                        spans[i].From,
                        spans[i].To
                    )
                );
            }

            return SpokenList.Join(ranges);
        }
    }
}
