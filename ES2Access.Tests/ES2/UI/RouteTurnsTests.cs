using System.Collections.Generic;
using ES2Access.ES2.UI;
using Xunit;

namespace ES2Access.Tests.ES2.UI
{
    /// <summary>
    /// Where a journey's turns end. The whole point of replaying the game's own walk rather than
    /// dividing a total by a budget is that the boundaries do not fall where division would put them:
    /// a part-spent budget is spent down to a whole number first, a shortcut swallows the rest of a
    /// turn whatever is left, and a place that refills movement resets the sum in the middle.
    /// </summary>
    public class RouteTurnsTests
    {
        private sealed class Legs : IRouteLegs
        {
            private readonly RouteLeg[] _legs;

            public Legs(params RouteLeg[] legs)
            {
                _legs = legs;
            }

            /// <summary>The budget each leg was priced against, so a test can prove that a leg IS
            /// priced against the budget standing when it is reached.</summary>
            public readonly List<float> PricedAt = new List<float>();

            public int Count
            {
                get { return _legs.Length; }
            }

            public RouteLeg Leg(int index, float currentPoints)
            {
                PricedAt.Add(currentPoints);
                RouteLeg leg = _legs[index];
                // A wormhole costs whatever is left, the way the game prices one.
                if (leg.Method == RouteMethod.Wormhole && leg.Cost < 0f)
                {
                    leg.Cost = currentPoints;
                }

                return leg;
            }
        }

        private static RouteLeg Ordinary(float cost, bool refill = false)
        {
            return new RouteLeg
            {
                Cost = cost,
                Method = RouteMethod.Ordinary,
                RefillOnArrival = refill,
            };
        }

        [Fact]
        public void AJourneyInsideOneTurnsBudgetArrivesOnTurnOne()
        {
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(2f), Ordinary(1f)),
                6f,
                6f,
                0f,
                -1f
            );

            Assert.Single(turns);
            Assert.Equal(1, turns[0].Number);
            Assert.True(turns[0].IsArrival);
            Assert.Equal(2, turns[0].EndLeg);
        }

        [Fact]
        public void RunningOutExactlyAtAPlaceEndsTheTurnThere()
        {
            // Three points, two legs of three: the first leg empties the tank exactly at the middle
            // place, so the turn ends AT it rather than half way down the second lane.
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(3f), Ordinary(3f)),
                3f,
                3f,
                0f,
                -1f
            );

            Assert.Equal(2, turns.Count);
            Assert.Equal(1, turns[0].EndLeg);
            Assert.False(turns[0].IsArrival);
            Assert.Equal(2, turns[1].Number);
            Assert.True(turns[1].IsArrival);
        }

        [Fact]
        public void RunningOutPartWayDownALaneEndsTheTurnNowhere()
        {
            List<RouteTurn> turns = RouteTurns.Walk(new Legs(Ordinary(5f)), 3f, 3f, 0f, -1f);

            Assert.Equal(2, turns.Count);
            Assert.Equal(-1, turns[0].EndLeg);
            Assert.True(turns[1].IsArrival);
        }

        [Fact]
        public void APartSpentBudgetIsSpentDownToAWholeNumberFirst()
        {
            // 2.5 points left of 3, one lane costing 4. The walk spends the half first and then whole
            // points, so it stops after 2.5 - one turn boundary - and finishes on turn 2. Dividing
            // 4 by 3 would have said the same thing here; the fraction matters where it changes which
            // side of a place the boundary falls, which the next test pins down.
            List<RouteTurn> turns = RouteTurns.Walk(new Legs(Ordinary(4f)), 2.5f, 3f, 0f, -1f);

            Assert.Equal(2, turns.Count);
        }

        [Fact]
        public void TheFractionDecidesWhichSideOfAPlaceTheNightIsSpent()
        {
            // Two lanes of 2. With a whole 4 points the fleet flies both and arrives on turn 1...
            Assert.Single(RouteTurns.Walk(new Legs(Ordinary(2f), Ordinary(2f)), 4f, 4f, 0f, -1f));

            // ...and with 3.5 of the same budget it runs out half way down the SECOND lane, which is
            // nowhere, rather than at the place between them.
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(2f), Ordinary(2f)),
                3.5f,
                4f,
                0f,
                -1f
            );

            Assert.Equal(2, turns.Count);
            Assert.Equal(-1, turns[0].EndLeg);
        }

        [Fact]
        public void APlaceThatRefillsMovementResetsTheSum()
        {
            // Two lanes of 3 on a budget of 3: ordinarily two turns. The middle place is a time bubble,
            // so the tank is full again there and the whole journey is one turn.
            Assert.Single(
                RouteTurns.Walk(new Legs(Ordinary(3f, refill: true), Ordinary(3f)), 3f, 3f, 0f, -1f)
            );
        }

        [Fact]
        public void AWormholeCostsWhateverIsLeftAndTheNextLegStartsANewTurn()
        {
            Legs legs = new Legs(
                new RouteLeg { Cost = -1f, Method = RouteMethod.Wormhole },
                Ordinary(1f)
            );
            List<RouteTurn> turns = RouteTurns.Walk(legs, 6f, 6f, 0f, -1f);

            Assert.Equal(2, turns.Count);
            Assert.True(turns[0].UsesWormhole);
            // The turn ends at the wormhole's far end - a place with a name - and not mid-lane.
            Assert.Equal(1, turns[0].EndLeg);
            Assert.True(turns[1].IsArrival);
            // Priced against the budget standing when it was reached, which is the whole reason legs
            // are asked for one at a time.
            Assert.Equal(6f, legs.PricedAt[0]);
        }

        [Fact]
        public void AWormholeOnTheLastLegArrivesOnTheTurnItIsEntered()
        {
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(new RouteLeg { Cost = -1f, Method = RouteMethod.Wormhole }),
                6f,
                6f,
                0f,
                -1f
            );

            Assert.Single(turns);
            Assert.True(turns[0].UsesWormhole);
            Assert.True(turns[0].IsArrival);
        }

        [Fact]
        public void APortalIsReportedOnTheTurnItIsUsed()
        {
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(
                    Ordinary(3f),
                    new RouteLeg { Cost = 2f, Method = RouteMethod.Portal },
                    Ordinary(2f)
                ),
                3f,
                3f,
                0f,
                -1f
            );

            // Turn 1 ends at the portal's mouth, turn 2 goes through it and runs out one point down
            // the lane beyond, turn 3 arrives - and the portal is reported on the turn it was flown.
            Assert.Equal(3, turns.Count);
            Assert.False(turns[0].UsesPortal);
            Assert.True(turns[1].UsesPortal);
            Assert.False(turns[2].UsesPortal);
        }

        [Fact]
        public void AFleetWithNothingLeftSpendsThisTurnWhereItStands()
        {
            List<RouteTurn> turns = RouteTurns.Walk(new Legs(Ordinary(1f)), 0f, 3f, 0f, -1f);

            Assert.Equal(2, turns.Count);
            Assert.Equal(0, turns[0].EndLeg);
            Assert.Equal(1, turns[1].EndLeg);
            Assert.True(turns[1].IsArrival);
        }

        [Fact]
        public void ALegAlreadyUnderWayEndsItsTurnNowhereRatherThanAtThePlaceItLeft()
        {
            // Out of movement, half way down a lane costing 4 with a tank of 4. This turn is over where
            // it stands - nowhere, so no itinerary line - and the remaining half is flown next turn.
            List<RouteTurn> turns = RouteTurns.Walk(new Legs(Ordinary(4f)), 0f, 4f, 0.5f, -1f);

            Assert.Equal(2, turns.Count);
            Assert.Equal(-1, turns[0].EndLeg);
            Assert.True(turns[1].IsArrival);
        }

        [Fact]
        public void AStoppedFleetHalfWayDownALaneCountsTheTurnItCannotMoveIn()
        {
            // The measured case (fixture "[Beginner] test", turn 5): a fleet with nothing left, 36% of
            // the way along an 8.27 lane, tank 3. It cannot move at all this turn; next turn it flies 3
            // and is still short; the turn after that it arrives. Three turns, not the two the game's
            // own path markers are numbered with.
            List<RouteTurn> turns = RouteTurns.Walk(new Legs(Ordinary(8.27f)), 0f, 3f, 0.3629f, -1f);

            Assert.Equal(3, turns.Count);
            Assert.Equal(-1, turns[0].EndLeg);
            Assert.Equal(-1, turns[1].EndLeg);
            Assert.True(turns[2].IsArrival);
        }

        [Fact]
        public void APlaceFlownStraightThroughIsReachedOnTheTurnItIsPassed()
        {
            // The measured route (Primus -> Dusay -> Rigel, budget 6 of 6 with 4.67 left, legs 13.33 and
            // 8.27). The fleet reaches Dusay part way through turn 3 and carries straight on, spending
            // that night in empty space; the itinerary still has to say "turn 3: Dusay", because when
            // the fleet GETS somewhere is the question, not where it sleeps.
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(13.33f), Ordinary(8.27f)),
                4.666434f,
                6f,
                0f,
                -1f
            );

            Assert.Equal(4, turns.Count);
            Assert.Empty(turns[0].Reached);
            Assert.Empty(turns[1].Reached);
            Assert.Equal(new[] { 1 }, turns[2].Reached);
            Assert.Equal(-1, turns[2].EndLeg);
            Assert.Equal(new[] { 2 }, turns[3].Reached);
            Assert.True(turns[3].IsArrival);
        }

        [Fact]
        public void SeveralPlacesReachedInOneTurnAreOneTurnsWorth()
        {
            // Four short hops on a budget that covers all of them: one turn, four places reached, which
            // the caller renders as a single line.
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(1f), Ordinary(1f), Ordinary(1f), Ordinary(1f)),
                6f,
                6f,
                0f,
                -1f
            );

            Assert.Single(turns);
            Assert.Equal(new[] { 1, 2, 3, 4 }, turns[0].Reached);
        }

        [Fact]
        public void APlaceReachedExactlyAsTheBudgetRunsOutBelongsToTheTurnThatGotThere()
        {
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(3f), Ordinary(3f)),
                3f,
                3f,
                0f,
                -1f
            );

            Assert.Equal(2, turns.Count);
            Assert.Equal(new[] { 1 }, turns[0].Reached);
            Assert.Equal(1, turns[0].EndLeg);
            Assert.Equal(new[] { 2 }, turns[1].Reached);
        }

        [Fact]
        public void NoRouteIsNoTurns()
        {
            Assert.Empty(RouteTurns.Walk(new Legs(), 3f, 3f, 0f, -1f));
        }

        [Fact]
        public void ATravellerWithNoTankAtAllStillTerminates()
        {
            List<RouteTurn> turns = RouteTurns.Walk(new Legs(Ordinary(3f)), 0f, 0f, 0f, -1f);

            Assert.Single(turns);
            Assert.True(turns[0].IsArrival);
        }

        // ---- which turn a journey reaches a given place on ----
        //
        // The question a fleet row hanging under a system asks: the row says when the fleet gets
        // HERE, which is rarely the turn the whole journey ends on.

        [Fact]
        public void APlaceTheTurnEndsAtIsReachedOnThatTurn()
        {
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(3f), Ordinary(3f)),
                3f,
                3f,
                0f,
                -1f
            );

            Assert.Equal(1, RouteTurns.ReachedOn(turns, 1));
            Assert.Equal(2, RouteTurns.ReachedOn(turns, 2));
        }

        [Fact]
        public void APlaceFlownStraightThroughIsReachedOnTheTurnThatPassedIt()
        {
            // The same measured route as above: turn 3 reaches Dusay and then spends the night in
            // empty space beyond it, so Dusay is reached on 3 even though no turn ends there.
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(13.33f), Ordinary(8.27f)),
                4.666434f,
                6f,
                0f,
                -1f
            );

            Assert.Equal(3, RouteTurns.ReachedOn(turns, 1));
            Assert.Equal(-1, turns[2].EndLeg);
        }

        [Fact]
        public void TheJourneysOwnStartIsReachedOnTheFirstTurn()
        {
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(3f), Ordinary(3f)),
                3f,
                3f,
                0f,
                -1f
            );

            Assert.Equal(1, RouteTurns.ReachedOn(turns, 0));
        }

        [Fact]
        public void TheLastPlaceIsReachedOnTheArrivalTurn()
        {
            List<RouteTurn> turns = RouteTurns.Walk(
                new Legs(Ordinary(13.33f), Ordinary(8.27f)),
                4.666434f,
                6f,
                0f,
                -1f
            );

            Assert.Equal(turns[turns.Count - 1].Number, RouteTurns.ReachedOn(turns, 2));
            Assert.True(turns[turns.Count - 1].IsArrival);
        }

        [Fact]
        public void APlaceOffTheRouteHasNoTurn()
        {
            List<RouteTurn> turns = RouteTurns.Walk(new Legs(Ordinary(3f)), 3f, 3f, 0f, -1f);

            Assert.Equal(0, RouteTurns.ReachedOn(turns, 7));
            Assert.Equal(0, RouteTurns.ReachedOn(null, 1));
        }
    }
}
