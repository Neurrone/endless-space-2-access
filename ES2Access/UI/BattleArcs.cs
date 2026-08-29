using System;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// A ring split between the two fleets, read as the split it is DRAWN at - "75% is yours, 25% is
    /// the enemy's".
    ///
    /// The gauge writes no figure anywhere. It says what it says with two arcs that share one circle:
    /// <c>BattlePowerGauge.Refresh</c> turns the pair of values into shares of their own total and
    /// writes each share into its sector as <c>MaxAngle = share * 360 - 1</c> (the missing degree is
    /// the gap between the two arcs), so the share a player can see is <c>(MaxAngle + 1) / 360</c> and
    /// nothing else. That is the number this reads, from the sector the game just painted - never the
    /// arithmetic behind it. The mod recomputing a fleet's damage from its modules would be a second
    /// implementation free to drift from the picture, and the figure a player decides whether to fight
    /// on is the one on their screen.
    ///
    /// A side worth nothing lands at <c>MaxAngle = -1</c> and the game stops drawing that half, which
    /// this reads as the 0% it is rather than skipping: "100% is yours, 0% is the enemy's" is what an
    /// unopposed ring means, and a half-said split would leave the listener to guess the other side.
    /// The two halves are each read from their OWN arc rather than one being 100 minus the other, so
    /// what is spoken is what is painted on both sides of the circle.
    ///
    /// The ring's THICKNESS is the gauge's second dimension: <c>Refresh</c> lerps the radial span from
    /// a ratio its host passes in, and each host means something by it - how much damage of that type
    /// there is compared with the other type, how well both fleets do at that range compared with the
    /// other ranges. So the thickest of a set is a fact the picture states and no arc does
    /// (<see cref="Thickest"/>), and a set drawn at one thickness states nothing.
    /// </summary>
    public static class BattleArcs
    {
        /// <summary>The share one arc is drawn at, as whole percent - the inverse of the
        /// <c>share * 360 - 1</c> the gauge wrote into it. Negative angles (an arc the game stopped
        /// drawing) answer 0, and the value is clamped to the circle it came from.</summary>
        public static int Share(AgePrimitiveSector sector)
        {
            try
            {
                return sector == null
                    ? 0
                    : Mathf.RoundToInt(Mathf.Clamp01((sector.MaxAngle + 1f) / 360f) * 100f);
            }
            catch (Exception e)
            {
                Log.Warn("battle arcs: reading an arc threw: " + e);
                return 0;
            }
        }

        /// <summary>
        /// The split a gauge is drawn at, said in <paramref name="key"/>'s own sentence - the player's
        /// share first, the enemy's second, because the left arc is the one the window colours with the
        /// player's empire everywhere this gauge is used.
        ///
        /// Null for a gauge the window has not wired, so a caller can append it blindly.
        /// </summary>
        public static string Shares(BattlePowerGauge gauge, string key)
        {
            if (gauge == null)
            {
                return null;
            }

            try
            {
                return OptionalText.Phrase(
                    key,
                    Share(gauge.LeftPowerGauge),
                    Share(gauge.RightPowerGauge)
                );
            }
            catch (Exception e)
            {
                Log.Warn("battle arcs: reading a gauge threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Which of a set of rings is drawn THICKEST, in that ring's own phrase - the one comparison
        /// the set makes that no single arc does.
        ///
        /// Nothing at all when the widest span is shared, because a tie is a set of rings drawn alike
        /// and there is no "biggest" on the screen to report. The spans come from the sectors the game
        /// sized, and two sized from the same ratio land on the same float, so the tie test is an
        /// epsilon rather than an exact match only to survive the lerp's rounding.
        /// </summary>
        public static string Thickest(BattlePowerGauge[] gauges, string[] keys)
        {
            if (gauges == null || keys == null || gauges.Length != keys.Length)
            {
                return null;
            }

            int best = -1;
            bool tied = false;
            for (int i = 0; i < gauges.Length; i++)
            {
                float span = Span(gauges[i]);
                if (best < 0 || span > Span(gauges[best]) + Epsilon)
                {
                    best = i;
                    tied = false;
                }
                else if (Math.Abs(span - Span(gauges[best])) <= Epsilon)
                {
                    tied = true;
                }
            }

            return best < 0 || tied ? null : OptionalText.Phrase(keys[best]);
        }

        /// <summary>How thick a ring is drawn: the radial span of the arcs, which
        /// <c>BattlePowerGauge.Refresh</c> writes into both sectors alike.</summary>
        private static float Span(BattlePowerGauge gauge)
        {
            try
            {
                AgePrimitiveSector sector = gauge == null ? null : gauge.LeftPowerGauge;
                return sector == null ? 0f : sector.MaxRadius - sector.MinRadius;
            }
            catch (Exception e)
            {
                Log.Warn("battle arcs: measuring a ring threw: " + e);
                return 0f;
            }
        }

        /// <summary>Two rings sized from the same ratio are the same ring as far as a player can see;
        /// this is the width of "the same" in the gauge's own radius units.</summary>
        private const float Epsilon = 0.0001f;
    }
}
