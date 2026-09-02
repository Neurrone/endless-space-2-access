using Amplitude.Unity.Event;

namespace ES2Access.UI
{
    // ---- the mod's own events, for the five things no bus event exists for ----
    //
    // Each is an EmpireEvent so that the game's own dispatch routes it to ONE empire - the player -
    // rather than offering it to every empire in the galaxy in turn
    // (GuiNotificationManager.EventService_EventRaised :847-855). They carry the payload READ AT
    // DETECTION TIME rather than the entity to re-read later: a fleet that has gone out of sight is
    // the whole point of one of them, and its name and its place must be the ones from while it
    // could still be seen.

    /// <summary>A fleet of the player's standing at the place it was sent to.</summary>
    public sealed class EventModFleetArrived : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public GameNode Destination { get; private set; }

        public EventModFleetArrived(
            Amplitude.Unity.Game.Empire empire,
            Fleet fleet,
            GameNode destination
        )
            : base(empire)
        {
            Fleet = fleet;
            Destination = destination;
        }
    }

    /// <summary>A fleet of the player's stopped short of it - a citadel or a guarding fleet took its
    /// movement away where it stood.</summary>
    public sealed class EventModFleetStopped : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public GameNode Where { get; private set; }

        public EventModFleetStopped(Amplitude.Unity.Game.Empire empire, Fleet fleet, GameNode where)
            : base(empire)
        {
            Fleet = fleet;
            Where = where;
        }
    }

    /// <summary>
    /// Somebody else's fleet the player can see, and has been able to see steadily long enough to
    /// have read it off the map (<see cref="ForeignFleetWatch"/> is what notices, and what decides
    /// how long that is).
    ///
    /// EVERYTHING the sentence says travels on the event, read at the moment the sighting was
    /// earned: what the player was allowed to count aboard the fleet at that instant is what the
    /// line says forever, and the fleet may be out of sight again by the time anybody reads it.
    /// </summary>
    public sealed class EventModForeignFleetSighted : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public Amplitude.Unity.Game.Empire Owner { get; private set; }

        /// <summary>Which way the player stood to the owner - "enemy Leaper (AI)" - or null for an
        /// empire the phrase cannot place, which leaves the bare name.</summary>
        public string OwnerStanding { get; private set; }

        public string FleetName { get; private set; }

        /// <summary>The fleet's name and whatever the player was allowed to know was aboard it, as
        /// <c>FleetPhrase.Full(fleet, false)</c> composed it at the sighting.</summary>
        public string Composition { get; private set; }

        /// <summary>Where it was standing, or null for one sighted out on a starlane.</summary>
        public GameNode Where { get; private set; }

        public EventModForeignFleetSighted(
            Amplitude.Unity.Game.Empire empire,
            Fleet fleet,
            Amplitude.Unity.Game.Empire owner,
            string ownerStanding,
            string fleetName,
            string composition,
            GameNode where
        )
            : base(empire)
        {
            Fleet = fleet;
            Owner = owner;
            OwnerStanding = ownerStanding;
            FleetName = fleetName;
            Composition = composition;
            Where = where;
        }
    }

    /// <summary>Somebody else's fleet that the player could see and now cannot.</summary>
    public sealed class EventModForeignFleetLost : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public Amplitude.Unity.Game.Empire Owner { get; private set; }

        /// <summary>The fleet's name as it read while it could still be seen, or null.</summary>
        public string FleetName { get; private set; }

        /// <summary>Where it was standing while it could still be seen, or null for a fleet last
        /// seen out on a starlane.</summary>
        public GameNode LastSeen { get; private set; }

        public EventModForeignFleetLost(
            Amplitude.Unity.Game.Empire empire,
            Fleet fleet,
            Amplitude.Unity.Game.Empire owner,
            string fleetName,
            GameNode lastSeen
        )
            : base(empire)
        {
            Fleet = fleet;
            Owner = owner;
            FleetName = fleetName;
            LastSeen = lastSeen;
        }
    }

    /// <summary>Somebody else's fleet, already in sight, standing somewhere else when the turn came
    /// round.</summary>
    public sealed class EventModForeignFleetMoved : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public Amplitude.Unity.Game.Empire Owner { get; private set; }

        public GameNode From { get; private set; }

        public GameNode To { get; private set; }

        public EventModForeignFleetMoved(
            Amplitude.Unity.Game.Empire empire,
            Fleet fleet,
            Amplitude.Unity.Game.Empire owner,
            GameNode from,
            GameNode to
        )
            : base(empire)
        {
            Fleet = fleet;
            Owner = owner;
            From = from;
            To = to;
        }
    }

    /// <summary>One of the player's own systems whose influence lost ground to a rival over the turn
    /// just ended (<see cref="InfluenceGroundWatch"/> is what notices). One event per system and
    /// taker, however many squares of map changed hands.</summary>
    public sealed class EventModInfluenceGroundLost : EmpireEvent
    {
        public ColonizedStarSystem System { get; private set; }

        /// <summary>The empire whose field now wins ground that was the player's.</summary>
        public Amplitude.Unity.Game.Empire Taker { get; private set; }

        public EventModInfluenceGroundLost(
            Amplitude.Unity.Game.Empire empire,
            ColonizedStarSystem system,
            Amplitude.Unity.Game.Empire taker
        )
            : base(empire)
        {
            System = system;
            Taker = taker;
        }
    }
}
