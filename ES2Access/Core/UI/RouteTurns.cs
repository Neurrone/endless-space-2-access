using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>How a leg of a route is flown, which is the only thing about a leg that changes the
    /// arithmetic: an ordinary leg is paid for a movement point at a time, and the two shortcuts are
    /// paid for in one go and never stop half way.</summary>
    public enum RouteMethod
    {
        Ordinary,
        Wormhole,
        Portal,
    }

    /// <summary>One step of a route: what it costs, how it is flown, and whether arriving at its far
    /// end fills the tank back up.</summary>
    public struct RouteLeg
    {
        public float Cost;

        public RouteMethod Method;

        /// <summary>The place at the leg's far end refills movement on arrival - a time bubble. The
        /// budget goes back to full there, whatever is left of it.</summary>
        public bool RefillOnArrival;
    }

    /// <summary>
    /// A route's legs, priced against the budget the traveller will actually have when it flies them.
    ///
    /// Pricing takes the budget because two of the three methods are priced FROM it - a wormhole costs
    /// whatever is left, a portal costs the smaller of what is left and a cap - so a route cannot be
    /// priced up front and then walked. The walk asks for each leg in turn, with the budget it has
    /// reached by then.
    /// </summary>
    public interface IRouteLegs
    {
        int Count { get; }

        RouteLeg Leg(int index, float currentPoints);
    }

    /// <summary>One turn of a journey: where it ends and what happened during it.</summary>
    public sealed class RouteTurn
    {
        /// <summary>1-based, and turn 1 is the turn now in progress.</summary>
        public int Number;

        /// <summary>The leg the traveller is standing at the start of when the turn ends - so the place
        /// it spends the turn's end at. -1 where the turn ends part way along a leg, between two places,
        /// which is a stop the map draws as a dot and has no name at all.</summary>
        public int EndLeg;

        /// <summary>The turn the journey ends on. Its <see cref="EndLeg"/> is the leg count, which is
        /// the far end of the last leg - the destination.</summary>
        public bool IsArrival;

        public bool UsesPortal;

        public bool UsesWormhole;
    }

    /// <summary>
    /// Where a journey's turns end, worked out by replaying the game's own path drawing.
    ///
    /// A strategy game with movement points communicates a route's turn structure POSITIONALLY: it
    /// walks the route a point at a time and draws a marker wherever the budget runs out, refilling
    /// there. Nothing anywhere says "three turns" in words - the answer is only in the picture, and a
    /// player who cannot see the picture has no way to ask. So the walk is replayed here, in the same
    /// order and with the same arithmetic, and what the markers MEAN comes out as turns.
    ///
    /// Two things make the replay a walk rather than a division. A leg is paid for in whole points
    /// where the budget is whole and in the leftover fraction where it is not, so where the boundaries
    /// fall depends on the fractions before them; and a leg's price can depend on the budget standing
    /// when it is reached, which is why the legs are asked for one at a time
    /// (<see cref="IRouteLegs"/>).
    ///
    /// Engine-free, so the arithmetic is unit-tested off the game. What a leg costs and what its far
    /// end is called are the caller's business.
    /// </summary>
    public static class RouteTurns
    {
        /// <summary>The game draws no more than this many turn markers, and a journey longer than that
        /// is not a thing anybody plans - the cap is here so a malformed budget cannot spin.</summary>
        public const int MaximumTurns = 99;

        /// <summary>
        /// The turns of a journey, in order, the last of which is the arrival.
        ///
        /// <paramref name="firstProgress"/> is how far along its first leg the traveller already is -
        /// nought for anything standing still. A leg already under way never ends a turn at the place it
        /// left, because it is not there any more.
        ///
        /// <paramref name="pointsAtLegStart"/> is what the budget was when that first leg began, which
        /// only matters where a leg already under way is one of the two shortcuts: those are paid for in
        /// one go, so being half way along one means it has already been paid for. Negative = not known,
        /// and then the budget standing now is used.
        /// </summary>
        public static List<RouteTurn> Walk(
            IRouteLegs legs,
            float current,
            float maximum,
            float firstProgress,
            float pointsAtLegStart
        )
        {
            List<RouteTurn> turns = new List<RouteTurn>(4);
            if (legs == null || legs.Count <= 0)
            {
                return turns;
            }

            RouteTurn turn = new RouteTurn { Number = 1, EndLeg = -1 };
            // A traveller with no tank at all can never finish a turn's budget, so the walk would
            // never end. It is also not a state the game leaves a fleet in.
            if (maximum <= float.Epsilon)
            {
                turn.EndLeg = legs.Count;
                turn.IsArrival = true;
                turns.Add(turn);
                return turns;
            }

            for (int i = 0; i < legs.Count && turns.Count < MaximumTurns; i++)
            {
                float progress = i == 0 ? firstProgress : 0f;
                if (current <= float.Epsilon)
                {
                    current += maximum;
                    // The turn is over: there is nothing left to spend, and the budget only ever comes
                    // back at the end of one. WHERE it is over is the only thing the leg's progress
                    // changes - a traveller standing at this leg's near end spends the night there, one
                    // already under way spends it in the empty space it has reached.
                    //
                    // The game's own renderer omits the marker in the second case, because there is no
                    // sensible place on the map to draw it; its LAST marker then reads one lower than
                    // the number of turns the journey really takes. Measured against the game itself: a
                    // fleet stopped part way down a lane with nothing left was drawn "1" at a
                    // destination it did not reach until the turn after next.
                    turn.EndLeg = progress <= 0f ? i : -1;
                    turns.Add(turn);
                    turn = new RouteTurn { Number = turns.Count + 1, EndLeg = -1 };
                }

                RouteLeg leg = legs.Leg(i, current);
                float cost = leg.Cost;
                if (leg.Method == RouteMethod.Wormhole || leg.Method == RouteMethod.Portal)
                {
                    if (leg.Method == RouteMethod.Portal)
                    {
                        turn.UsesPortal = true;
                    }
                    else
                    {
                        turn.UsesWormhole = true;
                    }

                    if (progress > 0f && pointsAtLegStart >= 0f)
                    {
                        current = pointsAtLegStart;
                    }
                    else if (current <= float.Epsilon)
                    {
                        current += maximum;
                    }

                    if (!float.IsInfinity(cost) && (cost > 0f || leg.Method == RouteMethod.Portal))
                    {
                        current -= cost;
                    }
                }
                else if (cost > 0f && !float.IsInfinity(cost))
                {
                    float flown = progress * cost;
                    while (turns.Count < MaximumTurns)
                    {
                        float part = current - (float)Math.Floor(current);
                        float step =
                            part >= float.Epsilon
                                ? Math.Min(part, cost - flown)
                                : Math.Min(1f, cost - flown);
                        current -= step;
                        flown += step;
                        if (Math.Abs(flown - cost) <= 0.001f)
                        {
                            break;
                        }

                        if (current <= float.Epsilon)
                        {
                            current += maximum;
                            turn.EndLeg = -1;
                            turns.Add(turn);
                            turn = new RouteTurn { Number = turns.Count + 1, EndLeg = -1 };
                        }
                    }
                }

                if (leg.RefillOnArrival)
                {
                    current = maximum;
                }
            }

            turn.EndLeg = legs.Count;
            turn.IsArrival = true;
            turns.Add(turn);
            return turns;
        }
    }
}
