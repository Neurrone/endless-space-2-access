using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The USAGE HINTS a control ends its review buffer with, and the words the keyboard chords
    /// inside them are spelled out of.
    ///
    /// Two families, kept together because neither is any use without the other:
    ///
    /// - the KEY NAMES ("Ctrl", "Enter", "Backslash") and the joiner that strings them into a chord.
    ///   A key the table does not name falls back to the engine's own <c>KeyCode</c> name, so this
    ///   list only has to hold the keys the mod's own gestures actually use - and a translator who
    ///   spells "Ctrl" differently changes it in exactly one place.
    /// - the HINT SENTENCES, each a complete phrase with a <c>{0}</c> the rendered chord drops into.
    ///   The chord is never written into the sentence: it is derived from the mod's own action table
    ///   at read time (<see cref="ES2Access.Core.UI.Graph.NodeHints"/>), so re-binding a gesture
    ///   re-words every hint that names it.
    ///
    /// They live apart from the rest of <see cref="ModStrings"/> for the reason the icon names do:
    /// one family, read together, and easier to translate as a block.
    /// </summary>
    public static partial class ModStrings
    {
        // The keyboard, in the player's language. Modifiers first, then the keys the mod's gestures
        // are bound to.
        public const string KeyCtrl = "key.ctrl";
        public const string KeyShift = "key.shift";
        public const string KeyAlt = "key.alt";
        public const string KeyEnter = "key.enter";
        public const string KeyBackslash = "key.backslash";

        /// <summary>What goes between the parts of a chord. A separator rather than a template, for
        /// the same reason <see cref="ListSeparator"/> is one: a chord has no fixed number of
        /// parts.</summary>
        public const string KeyChordJoiner = "key.chord-joiner";

        // --- input batch (2026-08-22) ---

        /// <summary>A control's name with the chord that works on it - the one place a chord is spoken
        /// in a READOUT rather than at the end of a review buffer, for the handful of controls whose
        /// gesture is the point of them (the paging arrows, the end-turn button). Both parts are
        /// placeholders: a language that says the chord first has somewhere to put it.</summary>
        public const string LabelWithChord = "label.with-chord";

        /// <summary>The star-system page's own previous/next arrows. The game draws them with no title
        /// at all - only a sentence in each one's tooltip - so the mod names them, the way it names the
        /// planet page's pair.</summary>
        public const string SystemPrevious = "system.previous";
        public const string SystemNext = "system.next";

        /// <summary>The academy strip's own previous/next hero arrows. The game titles them no more
        /// than it titles the star-system pair, and gives them no tooltip either.</summary>
        public const string AcademyPrevious = "academy.previous";
        public const string AcademyNext = "academy.next";

        // --- end input batch ---

        // The hint sentences. Every one of them is a whole phrase: the chord is a noun the sentence
        // is built around, and a language that puts it elsewhere has somewhere to put it.
        public const string HintMoveFleetHere = "hint.move-fleet-here";
        public const string HintFreeMovement = "hint.free-movement";
        public const string HintDeselectFleet = "hint.deselect-fleet";
        public const string HintDismiss = "hint.dismiss";
        public const string HintQueueFirst = "hint.queue-first";
        public const string HintAddToSelection = "hint.add-to-selection";
        public const string HintSelectUpToHere = "hint.select-up-to-here";
        public const string HintMissingTechnology = "hint.missing-technology";
        public const string HintShowAndSelectFleet = "hint.show-and-select-fleet";
        public const string HintOpenSystemManagement = "hint.open-system-management";
        public const string HintLoad = "hint.load";
        public const string HintGoToLocation = "hint.go-to-location";

        private static readonly Dictionary<string, string> HintDefaults = new Dictionary<
            string,
            string
        >
        {
            { KeyCtrl, "Ctrl" },
            { KeyShift, "Shift" },
            { KeyAlt, "Alt" },
            { KeyEnter, "Enter" },
            { KeyBackslash, "Backslash" },
            { KeyChordJoiner, "+" },
            { HintMoveFleetHere, "{0} to move the fleet here" },
            { HintFreeMovement, "{0} to use off-lane free movement" },
            { HintDeselectFleet, "{0} to deselect the fleet" },
            { HintDismiss, "{0} to dismiss" },
            { HintQueueFirst, "{0} to queue it first" },
            { HintAddToSelection, "{0} to add to the selection" },
            { HintSelectUpToHere, "{0} to select up to here" },
            { HintMissingTechnology, "{0} to show missing technology" },
            { HintShowAndSelectFleet, "{0} to show and select fleet" },
            { HintOpenSystemManagement, "{0} to open system management screen" },
            { HintLoad, "{0} to load" },
            { HintGoToLocation, "{0} goes to location" },
            // --- input batch (2026-08-22) ---
            { LabelWithChord, "{0} ({1})" },
            { SystemPrevious, "Previous system" },
            { SystemNext, "Next system" },
            { AcademyPrevious, "Previous hero" },
            { AcademyNext, "Next hero" },
            // --- end input batch ---
        };
    }
}
