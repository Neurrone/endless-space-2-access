using Amplitude.Unity.Event;
using ES2Access.Core.Speech;

namespace ES2Access.UI
{
    /// <summary>
    /// A fleet of the player's reaching where it was sent (<see cref="FleetArrivals"/> is what
    /// notices). No repeat refusal: the same fleet arriving somewhere else three turns later is news
    /// again, and a <c>Subject</c> would have refused it while the first was still standing.
    /// </summary>
    public sealed class FleetArrivedNotification : ModNotification
    {
        private Fleet _fleet;
        private GameNode _at;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModFleetArrived arrived = gameEvent as EventModFleetArrived;
            if (arrived == null || arrived.Fleet == null || !IsPlayer(arrived.Empire))
            {
                return false;
            }

            _fleet = arrived.Fleet;
            _at = arrived.Destination;
            return true;
        }

        protected override string Title()
        {
            string where = PlaceName(_at);
            string fleet = FleetPhrase.Full(_fleet);
            return where == null
                ? ModStrings.Format(ModStrings.NotificationFleetArrivedPlain, fleet)
                : ModStrings.Format(ModStrings.NotificationFleetArrived, fleet, where);
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return (IGameEntityWithGalaxyPosition)_at
                ?? (_fleet == null || _fleet.IsDestroyed ? null : _fleet);
        }
    }

    /// <summary>
    /// A fleet of the player's stopped where it stood. The sentence is the one the mod already said
    /// for this (<c>ModStrings.FleetInterceptedAt</c>), word for word: what is new is that the news
    /// now also has a place in the log and a Show Location button, not that it reads differently.
    /// </summary>
    public sealed class FleetStoppedNotification : ModNotification
    {
        private Fleet _fleet;
        private GameNode _at;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModFleetStopped stopped = gameEvent as EventModFleetStopped;
            if (stopped == null || stopped.Fleet == null || !IsPlayer(stopped.Empire))
            {
                return false;
            }

            _fleet = stopped.Fleet;
            _at = stopped.Where;
            return true;
        }

        protected override string Title()
        {
            string where = PlaceName(_at);
            string fleet = FleetPhrase.Full(_fleet);
            return where == null
                ? ModStrings.Format(ModStrings.FleetIntercepted, fleet)
                : ModStrings.Format(ModStrings.FleetInterceptedAt, fleet, where);
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return (IGameEntityWithGalaxyPosition)_at
                ?? (_fleet == null || _fleet.IsDestroyed ? null : _fleet);
        }
    }

    /// <summary>
    /// Somebody else's fleet coming into sight (<see cref="ForeignFleetWatch"/> is what notices).
    ///
    /// The whole line is FROZEN at the moment the sighting was earned - the owner's standing, the
    /// fleet's name, what was aboard it, and where it was standing - and nothing here ever reads the
    /// fleet again for a word of it. A turn log is a record of what the player was shown, and a line
    /// that re-read the fleet would quietly rewrite itself as the fleet grew, moved or vanished.
    ///
    /// Show Location is the one thing that does look at the fleet, and only to ask whether the
    /// player's client is still DRAWING it: while it is, the button flies to the fleet, and once it
    /// is not, it flies to the last node the fleet was seen standing at instead (owner ruling
    /// 2026-09-02 - the map never pans to blank sky). A fleet only ever seen out on a starlane has
    /// no such node and gets no button at all, which is what a null location does
    /// (<c>NotificationWindow.OnBeginShow</c> :138-140).
    ///
    /// No repeat refusal: a fleet sighted again after really having been lost is news again, and a
    /// <c>Subject</c> would have refused it while the first line was still in the log. What stops
    /// the same fleet being announced twice is the settle window, not this.
    /// </summary>
    public sealed class ForeignFleetSightedNotification : ModNotification
    {
        private Fleet _fleet;
        private Amplitude.Unity.Game.Empire _owner;
        private string _standing;
        private string _name;
        private string _composition;
        private GameNode _where;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModForeignFleetSighted sighted = gameEvent as EventModForeignFleetSighted;
            if (sighted == null || !IsPlayer(sighted.Empire) || sighted.Owner == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(EmpireName(sighted.Owner)))
            {
                return false;
            }

            _fleet = sighted.Fleet;
            _owner = sighted.Owner;
            _standing = sighted.OwnerStanding;
            _name = sighted.FleetName;
            _composition = sighted.Composition;
            _where = sighted.Where;
            return true;
        }

        /// <summary>Whose fleet has been sighted, said the way every other surface says it - which way
        /// the player stands to them in front of their name (<see cref="FleetPhrase.Owned(Fleet)"/>),
        /// so "enemy Leaper (AI) fleet sighted at Heka". The bare name is the fallback for the empire
        /// the phrase cannot place.</summary>
        private string Owner()
        {
            return _standing ?? EmpireName(_owner);
        }

        protected override string Title()
        {
            string where = PlaceName(_where);
            string owner = Owner();
            return where == null
                ? ModStrings.Format(ModStrings.NotificationFleetSightedNowhere, owner)
                : ModStrings.Format(ModStrings.NotificationFleetSighted, owner, where);
        }

        protected override string Body()
        {
            string where = PlaceName(_where);
            string owner = Owner();
            string named = Named();
            return where == null
                ? ModStrings.Format(ModStrings.NotificationFleetSightedBodyNowhere, owner, named)
                : ModStrings.Format(
                    ModStrings.NotificationFleetSightedBody,
                    owner,
                    named,
                    where
                );
        }

        /// <summary>
        /// The sighted fleet as the sentence's own subject - its name, then who is commanding it and
        /// what it is made of, read INSIDE the sentence rather than as sentences trailing after it
        /// (owner ruling 2026-08-26): "The enemy Leaper (AI) fleet 1st Ravaging Horde, Scavenger, was
        /// sighted at Heka."
        ///
        /// Whose it is is left out here - the sentence has already said it in its own slot, and
        /// saying it twice would be the mod stammering.
        ///
        /// The trailing comma is the far side of that appositive, and it is the mod's own list
        /// separator rather than a punctuation mark written into the code, so a language that
        /// separates a list some other way separates this too. It is only added where there IS an
        /// appositive: a fleet the player may not count and that carries no hero is nothing but its
        /// name, and a name needs no comma after it.
        /// </summary>
        private string Named()
        {
            if (string.IsNullOrEmpty(_composition) || _composition == _name)
            {
                return _composition;
            }

            return _composition + ModStrings.Get(ModStrings.ListSeparator).TrimEnd();
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            if (ForeignFleetWatch.Drawn(_fleet))
            {
                return _fleet;
            }

            return (IGameEntityWithGalaxyPosition)ForeignFleetWatch.LastSeen(_fleet) ?? _where;
        }
    }

    /// <summary>
    /// Somebody else's fleet going out of sight (<see cref="ForeignFleetWatch"/> is what notices).
    /// It says only that sight was lost, never WHY: the game's own downgrade does not say whether the
    /// fleet flew away, cloaked, or the thing that was watching it died, and a mod that guessed would
    /// be telling the player something nobody knows.
    ///
    /// Both the name and the place come off the event rather than off the fleet, because by the time
    /// this reads them the fleet is somewhere the player is not allowed to see.
    /// </summary>
    public sealed class ForeignFleetLostNotification : ModNotification
    {
        private Fleet _fleet;
        private Amplitude.Unity.Game.Empire _owner;
        private string _name;
        private GameNode _lastSeen;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModForeignFleetLost lost = gameEvent as EventModForeignFleetLost;
            if (lost == null || !IsPlayer(lost.Empire) || lost.Owner == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(EmpireName(lost.Owner)))
            {
                return false;
            }

            _fleet = lost.Fleet;
            _owner = lost.Owner;
            _name = lost.FleetName;
            _lastSeen = lost.LastSeen;
            return true;
        }

        protected override string Title()
        {
            // Which way the player stands to the OWNER, which is the player's own diplomacy and not
            // something read off a fleet nobody can see any more: the fleet's own object is stale by
            // now, and its name and place come off the event for exactly that reason.
            string owner = FleetPhrase.Owned(_owner) ?? EmpireName(_owner);
            string where = PlaceName(_lastSeen);
            if (string.IsNullOrEmpty(_name))
            {
                return where == null
                    ? ModStrings.Format(
                        ModStrings.NotificationFleetLostSightUnnamedNowhere,
                        owner
                    )
                    : ModStrings.Format(
                        ModStrings.NotificationFleetLostSightUnnamed,
                        owner,
                        where
                    );
            }

            return where == null
                ? ModStrings.Format(ModStrings.NotificationFleetLostSightNowhere, owner, _name)
                : ModStrings.Format(ModStrings.NotificationFleetLostSight, owner, _name, where);
        }

        /// <summary>The place it was last seen at, and never the fleet itself: the fleet is where the
        /// player may not look, and the map would fly to it.</summary>
        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _lastSeen;
        }
    }

    /// <summary>
    /// Somebody else's fleet, in sight before the turn and in sight after it, standing somewhere
    /// else. A fleet that went OUT of sight is the lost-sight family's news and never this one's, and
    /// a fleet that is simply gone belongs to the game's own battle notifications - nothing here
    /// invents a reason for an absence.
    /// </summary>
    public sealed class ForeignFleetMovedNotification : ModNotification
    {
        private Fleet _fleet;
        private Amplitude.Unity.Game.Empire _owner;
        private GameNode _from;
        private GameNode _to;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModForeignFleetMoved moved = gameEvent as EventModForeignFleetMoved;
            if (moved == null || !IsPlayer(moved.Empire) || moved.Owner == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(EmpireName(moved.Owner)))
            {
                return false;
            }

            _fleet = moved.Fleet;
            _owner = moved.Owner;
            _from = moved.From;
            _to = moved.To;
            return PlaceName(_from) != null || PlaceName(_to) != null;
        }

        protected override string Title()
        {
            // The owner's standing, not the fleet's phrase: the fleet is only being said to have
            // MOVED, and nothing new about what it is made of has been shown.
            string owner = FleetPhrase.Owned(_owner) ?? EmpireName(_owner);
            string from = PlaceName(_from);
            string to = PlaceName(_to);
            if (to == null)
            {
                return ModStrings.Format(
                    ModStrings.NotificationForeignFleetMovedAway,
                    owner,
                    from
                );
            }

            return from == null
                ? ModStrings.Format(ModStrings.NotificationForeignFleetMovedTo, owner, to)
                : ModStrings.Format(ModStrings.NotificationForeignFleetMoved, owner, from, to);
        }

        /// <summary>Where it was standing when the turn came round, and never the fleet itself: by
        /// the time anybody presses this the fleet may have moved on or gone out of sight, and the
        /// map would fly to somewhere the player is not allowed to look. A fleet whose new place was
        /// a starlane falls back on the last node it was seen at, which is the same rule the sighting
        /// line follows.</summary>
        protected override IGameEntityWithGalaxyPosition Location()
        {
            return (IGameEntityWithGalaxyPosition)_to ?? ForeignFleetWatch.LastSeen(_fleet);
        }
    }

    /// <summary>
    /// A border of the player's moving the wrong way: squares that were provably inside one of their
    /// systems' influence, and that a rival's field now wins
    /// (<see cref="InfluenceGroundWatch"/> is what notices).
    ///
    /// No repeat refusal, by the owner's ruling: it is news EVERY turn it happens, because a border
    /// still moving after a turn of it moving is exactly the thing the player needs to keep hearing.
    /// The taker is never the player - a square is only lost when somebody ELSE wins a point of it -
    /// so there is no "your empire" form of the sentence.
    /// </summary>
    public sealed class InfluenceGroundLostNotification : ModNotification
    {
        private ColonizedStarSystem _system;
        private Amplitude.Unity.Game.Empire _taker;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModInfluenceGroundLost lost = gameEvent as EventModInfluenceGroundLost;
            if (
                lost == null
                || lost.System == null
                || lost.Taker == null
                || !IsPlayer(lost.Empire)
            )
            {
                return false;
            }

            if (string.IsNullOrEmpty(EmpireName(lost.Taker)))
            {
                return false;
            }

            _system = lost.System;
            _taker = lost.Taker;
            return true;
        }

        protected override string Title()
        {
            return ModStrings.Format(
                ModStrings.NotificationInfluenceGroundLost,
                _system.LocalizedName,
                EmpireName(_taker)
            );
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _system.Node;
        }
    }
}
