using System.Collections.Generic;
using ES2Access.Core.Map;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// One bearing said as what is down it: how much of what a launch that way would reveal is
    /// already known, then the stretches of fog the probe would fly through, and how far the map goes
    /// before it runs out.
    ///
    /// TWO deliveries, not one sentence. The bearing ANNOUNCES the heading and the share and stops
    /// there (<see cref="Label(double, ProbeFootprint)"/>), because a player walks sixteen of these
    /// with a key and "35 percent explored" is enough to skip a bearing or to stay on it; everything
    /// that explains the number is a LINE of the same node's review buffer
    /// (<see cref="Lines"/>), read at the player's own pace on the one bearing they stopped on. The
    /// share leads there too, so the buffer opens on the figure the announcement just gave rather than
    /// on a list of ranges with nothing to weigh them against. It is always said - a flight line with
    /// no fog on it at all can still be a corridor whose flanks are half dark, so "fully explored to
    /// the map edge" is not the same claim and must not silence this one
    /// (<see cref="ProbeFootprint"/>).
    ///
    /// A probe order cannot be recalled and cannot be aimed at anything but a direction, so the choice
    /// is made entirely on what each direction is worth - which a sighted player reads off the fog on
    /// the map in a second and a listener cannot be told at all unless it is said. Every range is
    /// there; nothing is merged into "mostly unexplored" and nothing is dropped for being far away,
    /// because the player is choosing BETWEEN sixteen of these and a summary that hides where the fog
    /// starts hides the whole comparison.
    ///
    /// The word for "unexplored" is said once, at the front of its line, and the ranges follow it as a
    /// list. The stretch that runs off the map is said as running to the map's edge rather than as a
    /// range, because its far end is not a place the fog ends - it is where the galaxy does.
    ///
    /// Then what is unexplored ALONGSIDE the line: a line of its own per side, named by the compass
    /// word of the side it is on, so that a flight line the probe would find nothing on is never
    /// reported as fog. Its ranges never fold into the map's edge - the flight line has already said
    /// where the edge is, and saying it twice makes the second one sound like a different number. Two
    /// sides carrying the identical stretches are one line, naming both.
    ///
    /// Engine-free: it says a <see cref="ProbeCorridorReading"/> and knows nothing about how one was
    /// measured.
    /// </summary>
    public static class ProbeContextText
    {
        /// <summary>What the bearing announces: the heading named with the sixteen-word compass
        /// (<see cref="CompassDirections.KeyForBearing16"/>) and the share, and nothing else.</summary>
        public static string Label(double bearing, ProbeFootprint footprint)
        {
            return Label(ModStrings.Get(CompassDirections.KeyForBearing16(bearing)), footprint);
        }

        /// <summary>The same for a heading the caller has already named - the direction word opens the
        /// announcement, so it is capitalized here rather than by whoever supplies it.</summary>
        public static string Label(string bearingWord, ProbeFootprint footprint)
        {
            return ModStrings.Format(
                ModStrings.GalaxyProbeContext,
                Capitalized(bearingWord),
                Percent(footprint)
            );
        }

        /// <summary>Everything down the heading, a clause per line, for the bearing's review buffer:
        /// the share, then the flight line's own fog and the rim, then a line per side that has fog
        /// beside the line. Never empty - the share and the flight line are always both said.</summary>
        public static IList<string> Lines(
            ProbeCorridorReading reading,
            ProbeFootprint footprint
        )
        {
            List<string> lines = new List<string>(4);
            lines.Add(Percent(footprint));
            lines.Add(FlightLine(reading));
            Alongside(lines, reading);
            return lines;
        }

        /// <summary>The share of what the launch would reveal that the empire already has - the one
        /// figure the choice is made on, which is why both deliveries carry it.</summary>
        public static string Percent(ProbeFootprint footprint)
        {
            return ModStrings.Format(
                ModStrings.GalaxyProbeContextPercentExplored,
                footprint.PercentExplored
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

        /// <summary>What the probe would fly THROUGH - the flight line's own fog and the rim.
        /// </summary>
        private static string FlightLine(ProbeCorridorReading reading)
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

        /// <summary>What the probe would uncover in PASSING, a line per side - or nothing at all,
        /// which is most bearings. The two sides are the heading turned a quarter circle each way;
        /// when they hold the same stretches they are one line naming both, since hearing the same six
        /// numbers twice tells the player nothing the word "both" does not.</summary>
        private static void Alongside(List<string> lines, ProbeCorridorReading reading)
        {
            string clockwise = Ranges(reading.Clockwise);
            string counter = Ranges(reading.CounterClockwise);
            if (clockwise == null && counter == null)
            {
                return;
            }

            if (clockwise == counter)
            {
                lines.Add(
                    ModStrings.Format(ModStrings.GalaxyProbeContextAlongsideBoth, clockwise)
                );
                return;
            }

            Side(lines, reading.Bearing + 90.0, clockwise);
            Side(lines, reading.Bearing - 90.0, counter);
        }

        /// <summary>One side's line, nothing at all when that side has nothing unexplored beside the
        /// line.</summary>
        private static void Side(List<string> lines, double bearing, string ranges)
        {
            if (ranges == null)
            {
                return;
            }

            lines.Add(
                ModStrings.Format(
                    ModStrings.GalaxyProbeContextAlongside,
                    ModStrings.Get(CompassDirections.KeyForBearing16(bearing)),
                    ranges
                )
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
