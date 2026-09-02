namespace ES2Access.Core.Speech
{
    /// <summary>
    /// What the mod says about a NOTIFICATION the game raised - the popup's own summary
    /// lines, one phrase per kind of thing that happened.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
        // The notifications the MOD raises into the game's own pipeline, for the eight things the
        // game puts on the event bus and then tells nobody about. Each family has a TITLE - what is
        // spoken as it arrives, and what the popup writes across the top - and, where the game has a
        // second thing to say, a BODY sentence the popup shows under it. The "-plain" forms are the
        // same news with a place the map has not named left out, rather than a sentence with a hole
        // in it. Every popup body is prefixed with the turn it happened on, because a popup reached
        // by Previous/Next from a game notification otherwise carries no clue.
        public const string NotificationTurnPrefix = "notification.turn-prefix";

        public const string NotificationSystemRevealed = "notification.system-revealed";
        public const string NotificationSystemRevealedPlain = "notification.system-revealed-plain";

        /// <summary>The discovery with nothing known about it - what is left when the notification has
        /// outlived the node it was about (a reload replacing the assembly under a standing one is how
        /// this was first seen). The news is still true; only the name is gone.</summary>
        public const string NotificationSystemRevealedUnknown =
            "notification.system-revealed-unknown";
        public const string NotificationSystemRevealedBody = "notification.system-revealed-body";
        public const string NotificationSystemRevealedInConstellation =
            "notification.system-revealed-in-constellation";

        public const string NotificationFleetSighted = "notification.fleet-sighted";
        public const string NotificationFleetSightedNowhere = "notification.fleet-sighted-nowhere";
        public const string NotificationFleetSightedBody = "notification.fleet-sighted-body";
        public const string NotificationFleetSightedBodyNowhere =
            "notification.fleet-sighted-body-nowhere";

        public const string NotificationColonySighted = "notification.colony-sighted";
        public const string NotificationColonySightedBody = "notification.colony-sighted-body";

        public const string NotificationFleetDispatched = "notification.fleet-dispatched";
        public const string NotificationFleetDispatchedTo = "notification.fleet-dispatched-to";
        public const string NotificationFleetDispatchedPlain = "notification.fleet-dispatched-plain";

        public const string NotificationSystemBesieged = "notification.system-besieged";
        public const string NotificationSystemBesiegedPlain = "notification.system-besieged-plain";
        public const string NotificationSystemBesiegedBody = "notification.system-besieged-body";

        public const string NotificationSystemBlockaded = "notification.system-blockaded";
        public const string NotificationSystemBlockadedPlain = "notification.system-blockaded-plain";
        public const string NotificationSystemBlockadedBody = "notification.system-blockaded-body";

        public const string NotificationTradeBlockadeEnded = "notification.trade-blockade-ended";
        public const string NotificationTradeBlockadeEndedPlain =
            "notification.trade-blockade-ended-plain";

        /// <summary>An Obliterator going off. One pair for both firings the player hears about: the
        /// name in front is the player's own fleet when the shot is theirs and the firing empire when
        /// it is somebody else's, which reads the same either way.</summary>
        public const string NotificationObliteratorFired = "notification.obliterator-fired";
        public const string NotificationObliteratorFiredPlain =
            "notification.obliterator-fired-plain";

        /// <summary>A fleet of the player's reaching the place it was sent to. The journey ENDING is
        /// also how a fleet stopped short reads, so the two are separate families with separate
        /// sentences - being stopped keeps the wording it already had
        /// (<see cref="FleetInterceptedAt"/>).</summary>
        public const string NotificationFleetArrived = "notification.fleet-arrived";
        public const string NotificationFleetArrivedPlain = "notification.fleet-arrived-plain";

        /// <summary>Somebody else's fleet going out of sight. Four whole sentences rather than one
        /// with holes in it: the fleet's name and the place it was last seen at are each things the
        /// map may not have known, and a language that inflects around either cannot be handed a
        /// fragment.</summary>
        public const string NotificationFleetLostSight = "notification.fleet-lost-sight";
        public const string NotificationFleetLostSightUnnamed =
            "notification.fleet-lost-sight-unnamed";
        public const string NotificationFleetLostSightNowhere =
            "notification.fleet-lost-sight-nowhere";
        public const string NotificationFleetLostSightUnnamedNowhere =
            "notification.fleet-lost-sight-unnamed-nowhere";

        /// <summary>Somebody else's fleet that was already in sight standing somewhere else when the
        /// turn came round. A fleet under way is at no place the map names, which is what the second
        /// and third forms are for.</summary>
        public const string NotificationForeignFleetMoved = "notification.foreign-fleet-moved";
        public const string NotificationForeignFleetMovedTo = "notification.foreign-fleet-moved-to";
        public const string NotificationForeignFleetMovedAway =
            "notification.foreign-fleet-moved-away";

        /// <summary>One of the player's own systems whose influence lost ground this turn - squares
        /// that were provably inside its reach and that a rival's field now wins. One sentence per
        /// system and taker however many squares changed: a border moving is one piece of news.
        /// </summary>
        public const string NotificationInfluenceGroundLost = "notification.influence-ground-lost";
    }
}
