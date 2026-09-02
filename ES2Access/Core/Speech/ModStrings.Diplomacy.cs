namespace ES2Access.Core.Speech
{
    /// <summary>
    /// THE DIPLOMACY FAMILY: the diplomacy ring, the negotiation table, the pirates, the
    /// academy's diplomacy page and the minor civilizations.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
        // ---- the diplomacy family ----
        // Every window in this family writes its own heading, so each screen name here is only ever
        // spoken in the frames before the game has filled its title in. What the family really needs mod
        // words for is the bands the game draws with no caption at all.

        public const string ScreenDiplomacy = "screen.diplomacy";

        /// <summary>The ring of empires, and the tick box and two window buttons drawn under it. The game
        /// captions neither group.</summary>
        public const string DiplomacyEmpires = "diplomacy.empires";
        public const string DiplomacyControls = "diplomacy.controls";

        /// <summary>The badge the diplomacy ring paints on an empire with NOTHING written on it: either a
        /// truce can be forced with them now, or terms the player has not seen yet have become available.
        /// A wordless mark needs words, and the game has none for it anywhere.</summary>
        public const string DiplomacyNewOptions = "diplomacy.new-options";

        /// <summary>Why an empire refuses to be negotiated with while the ring is centred on somebody
        /// else's relations. The game has a sentence for an unmet empire and for an eliminated one and
        /// says nothing at all about this case, which is reachable only through its own swap mode.
        /// </summary>
        public const string DiplomacyNotYourRing = "diplomacy.not-your-ring";

        /// <summary>Whose relations the ring has just been re-centred on. Swap mode rewrites every card
        /// on the page at once and the only thing that says whose ring it now is, is the name over the
        /// hologram in the middle - so the change is announced. {0} is the empire, as the game writes
        /// it there.</summary>
        public const string DiplomacyViewingFrom = "diplomacy.viewing-from";

        public const string ScreenNegotiation = "screen.negotiation";

        // The negotiation table's bands. The game captions most of them itself - the window heading,
        // the pressure gauge, both term shelves, the contract and its two contributions - and those
        // words are read off the widget with the keys below as the fallback. The bands the game leaves
        // unnamed name themselves from here.

        /// <summary>The band between the two empire banners: what the diplomatic status is, and the
        /// three things that status permits. The game draws the status sentence and the ability icons
        /// and captions neither.</summary>
        public const string NegotiationRelationship = "negotiation.relationship";
        public const string NegotiationDossier = "negotiation.dossier";
        public const string NegotiationPressure = "negotiation.pressure";
        public const string NegotiationMyTerms = "negotiation.my-terms";
        public const string NegotiationTheirTerms = "negotiation.their-terms";
        public const string NegotiationTerms = "negotiation.terms";
        public const string NegotiationContract = "negotiation.contract";
        public const string NegotiationContractBoth = "negotiation.contract-both";
        public const string NegotiationContractMine = "negotiation.contract-mine";
        public const string NegotiationContractTheirs = "negotiation.contract-theirs";
        /// <summary>Where the deal-approval bar STANDS, which is what that band draws and all it draws:
        /// the computer sends written reasons with its evaluation and the window throws them away, so a
        /// number is the honest reading.</summary>
        public const string NegotiationApproval = "negotiation.approval";

        /// <summary>One empire's share of the influence-pressure bar. The gauge is one track split
        /// between the two of them and the game writes no figure anywhere on it, so each side is named
        /// by the banner it is drawn under and given the length it holds.</summary>
        public const string NegotiationPressureShare = "negotiation.pressure-share";

        /// <summary>Where a threshold notch sits along the pressure bar. The game draws the marker at a
        /// place on the track and says only whether it has been reached, so without the place the two
        /// markers are two sentences with nothing to tell them apart or measure the bar against
        /// (owner-reported 2026-08-27: "the thresholds for pressure make no sense currently").</summary>
        public const string NegotiationThresholdAt = "negotiation.threshold-at";
        public const string NegotiationActions = "negotiation.actions";

        /// <summary>The stepper a resource term draws beside its name. The game writes the number in the
        /// box and nothing else, so the box needs naming.</summary>
        public const string NegotiationQuantity = "negotiation.quantity";

        /// <summary>The fallback name of the minor-civilization window, for a build where the game's own
        /// title (<c>%MinorFactionDiplomacyModalWindowTitle</c>) reads back empty. Every band of that
        /// window is named by the game's own caption, so nothing else here belongs to it.</summary>
        public const string ScreenMinorDiplomacy = "screen.minor-diplomacy";

        public const string ScreenPirateDiplomacy = "screen.pirate-diplomacy";
        public const string PiratePower = "pirate.power";
        public const string PirateStanding = "pirate.standing";
        public const string PirateNextFleet = "pirate.next-fleet";

        /// <summary>One mark on the pirate window's additional-firepower track. The game writes the
        /// mark's ORDINAL on the circle and says nothing else about it in words - how near the next
        /// fleet is to it lives only in how far the bar behind the circle has filled, and whether it is
        /// reached only in the circle's alpha - so both phrases are the mod's, each a whole sentence.
        /// The distance is spoken as a percentage of THIS mark's own stretch of the track, which is
        /// what the bar draws; the raw stock and cost are the game's arithmetic, not a reading (owner
        /// ruling 2026-08-30).</summary>
        public const string PirateThresholdReached = "pirate.threshold-reached";
        public const string PirateThresholdProgress = "pirate.threshold-progress";

        public const string ScreenAcademyDiplomacy = "screen.academy-diplomacy";
        public const string ScreenAcademyModal = "screen.academy-modal";
        public const string AcademyRelation = "academy-diplomacy.relation";
        public const string AcademyNamedShips = "academy.named-ships";

        /// <summary>The band of actions, and the pair of stock figures along the bottom edge, that the
        /// minor-faction, pirate and Academy windows all draw. All THREE windows caption the actions
        /// band themselves ("Actions", from three separate keys - corrected 2026-08-22, this comment
        /// used to say only the pirate one did), so the word here is the fallback and the shared name
        /// for the OTHER bands that borrow it: the hangar's command bar and the senate's button
        /// strip.</summary>
        public const string DiplomacyActionsBand = "diplomacy.actions-band";
        public const string DiplomacyTreasury = "diplomacy.treasury";

        /// <summary>One band of a minor faction's relation gauge, named by the relation state it would
        /// buy and the number of relation points where that state starts ("CORDIAL (25)"). A
        /// connective: the state word is the game's (<c>%DiplomaticRelationStateMinor…Title</c>,
        /// read off the band's own description key) and the number is measured off the bar.</summary>
        public const string MinorBand = "minor.band";

        /// <summary>What the relation points and their per-turn trend are ("40 (+7/Turn)"). The game
        /// gives the line no title anywhere - only a sentence explaining what a relation IS - so the
        /// caption is the mod's (owner ruling 2026-08-22).</summary>
        public const string MinorRelationship = "minor.relationship";

        /// <summary>The tutorial picker the game raises over a new game. Only ever spoken if the
        /// window has not written its own heading yet; the heading it draws is what is normally
        /// said.</summary>
        public const string ScreenTutorialSelection = "screen.tutorial-selection";

        /// <summary>The single-player lobby. Only ever spoken if the window has not written its own
        /// heading yet; the heading it draws is what is normally said.</summary>
        public const string ScreenNewGame = "screen.new-game";

        /// <summary>The advanced settings a category's Advanced button opens. Only ever spoken if the
        /// window has not written its own heading yet; it normally names the category itself.</summary>
        public const string ScreenAdvancedSettings = "screen.advanced-settings";

        /// <summary>The faction chooser. Only ever spoken if the window has not written its own
        /// heading yet.</summary>
        public const string ScreenFactionChoice = "screen.faction-choice";
    }
}
