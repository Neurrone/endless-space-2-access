using System;
using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The mod's own spoken strings — everything the mod says that does not come from the game's
    /// text. Two layers: compiled-in English defaults that always exist, and an optional overlay
    /// installed from a translation file. A missing or broken translation therefore degrades to
    /// English instead of to silence or a crash.
    ///
    /// Deliberately BCL-only: it holds no file, engine or JSON knowledge, so <see cref="Core"/>
    /// stays testable offline. The engine side (ModLocale) reads the game's language and calls
    /// <see cref="Install"/>.
    ///
    /// Main-thread only; there is no locking. Speech is composed on the Unity main thread.
    ///
    /// This copy is an EXAMPLE, not a whole game's table. The mechanism is the shipped one and
    /// should be taken verbatim; the keys are only the ones the rest of the snapshots in this
    /// folder speak, plus one screen's worth of illustration. A real mod grows hundreds of
    /// per-screen keys in exactly the shape that example block shows.
    /// </summary>
    public static partial class ModStrings
    {
        public const string StartupReady = "startup.ready";
        public const string FragmentSeparator = "speech.fragment-separator";
        public const string ListSeparator = "speech.list-separator";
        public const string Fraction = "speech.fraction";
        public const string FractionUnit = "speech.fraction-unit";
        public const string Quantity = "speech.quantity";

        // The role words that say what kind of control the player is on.
        public const string ControlButton = "control.button";
        public const string ControlGroup = "control.group";
        public const string ControlTab = "control.tab";
        public const string ControlCheckbox = "control.checkbox";
        public const string ControlSlider = "control.slider";
        public const string ControlComboBox = "control.combo-box";
        public const string ControlEditField = "control.edit-field";

        /// <summary>A box that only takes a number and carries its own stepper: the arrows change the
        /// value where an ordinary edit field's arrows would move a caret, so the role word has to say
        /// so before the player tries.</summary>
        public const string ControlNumericEditField = "control.numeric-edit-field";
        public const string ControlMenuItem = "control.menu-item";

        // One of a set where exactly one is in force. Not a checkbox: activating it can only ever
        // make it the chosen one, and the box the player would expect to untick does not exist.
        public const string ControlRadioButton = "control.radio-button";

        // What navigation says about a control beyond its own text. Each is a whole phrase: a
        // language that negates with more than a leading word has somewhere to put it. The absence
        // of a state gets a key of its own only where the absence is the fact the player needs -
        // an unticked box, or membership of a list several things are picked out of. A group where
        // exactly one is in force (a tab bar, a radio group) says nothing about the others.
        public const string NavExpanded = "nav.expanded";
        public const string NavCollapsed = "nav.collapsed";
        public const string NavChecked = "nav.checked";
        public const string NavUnchecked = "nav.unchecked";
        public const string NavSelected = "nav.selected";

        public const string NavDisabled = "nav.disabled";
        public const string NavNoDetails = "nav.no-details";

        // A grid of cells the player walks with the arrow keys: the role word for the grid itself,
        // and what a cell with nothing drawn in it says. An empty cell needs a word of its own -
        // silence there is indistinguishable from a cell the readout simply failed to reach.
        public const string NavTable = "nav.table";
        public const string NavCellEmpty = "nav.cell-empty";

        // Typing letters on a screen searches what is on it. Both are whole phrases: the text the
        // player typed is quoted inside the sentence, so a language that frames a quotation
        // differently has somewhere to do it.
        public const string SearchNoMatch = "search.no-match";
        public const string SearchCleared = "search.cleared";

        // The review buffers - the text the player walks line by line.
        public const string BufferUi = "buffer.ui";
        public const string BufferEmpty = "buffer.empty";
        public const string BufferLine = "buffer.line";

        // Screen names, spoken on arrival.
        public const string ScreenMainMenu = "screen.main-menu";
        public const string ScreenMessageBox = "screen.message-box";
        public const string ScreenLoading = "screen.loading";

        // How far a load has got, said at the quarter marks.
        public const string LoadingProgress = "loading.progress";
        public const string LoadingProgressBar = "loading.progress-bar";

        // Picking something up and putting it down somewhere else (a ship into another fleet). The
        // words are the DRAG's, because that is the gesture these keys stand in for and the one the
        // game's own tooltips name. The carried thing is named in the mod's sentence but in the game's
        // own words, and each of these is a whole phrase so a language that frames "dragging X"
        // differently has somewhere to do it. Ending a drag without moving anything - the back key,
        // or putting the thing back where it came from - is one phrase and names nothing, because
        // nothing happened to name. A refusal normally speaks the GAME's reason instead; the one here
        // is the fallback for a check that refuses wordlessly.
        public const string DragStarted = "drag.started";
        public const string DragStartedPlain = "drag.started-plain";
        public const string DragDropped = "drag.dropped";
        public const string DragDropRefused = "drag.drop-refused";
        public const string DragCancelled = "drag.cancelled";

        /// <summary>What a control says while it would take the thing the player is holding.</summary>
        public const string DragDropTarget = "drag.drop-target";

        /// <summary>What a control the player could pick something up from says while nothing is being
        /// carried. Not said while something IS held: the useful fact
        /// about a control then is whether the thing can go there.</summary>
        public const string DragDraggable = "drag.draggable";
        public const string DragHint = "drag.drag-hint";
        public const string DragDropHint = "drag.drop-hint";

        // ---- one screen's worth, as the example ----
        // Nothing else in this folder speaks these. They are here to show the shape a screen's block
        // takes: a key for each thing the game draws without words of its own, a whole phrase per key,
        // and a comment saying why the game's own words would not do. A shipped mod has one such block
        // per screen and hundreds of keys in total.

        // The galaxy: the controls the game draws as icons and never names, and the shapes its
        // numbers are spoken in.
        public const string ScreenGalaxy = "screen.galaxy";
        public const string GalaxyTurn = "galaxy.turn";
        public const string GalaxyEndTurn = "galaxy.end-turn";
        public const string GalaxyIdleFleets = "galaxy.idle-fleets";

        // What the camera just did, said back because the player cannot see it move. The game has no
        // words of its own for the pair: what it does write about zooming is the titles of its two
        // camera KEY BINDINGS ("Zoom in (Galaxy)", "Zoom out (Galaxy)"), which name a key rather than
        // report a change, so these are the mod's.
        public const string GalaxyZoomedIn = "galaxy.zoomed-in";
        public const string GalaxyZoomedOut = "galaxy.zoomed-out";

        /// <summary>How many other players the game is still waiting on. A COUNTED phrase, hence a
        /// form per number (see <see cref="Plural"/>) rather than a count glued to a noun.</summary>
        public const string GalaxyPlayerPlaying = "galaxy.player-playing";
        public const string GalaxyPlayersPlaying = "galaxy.players-playing";

        // Two more counted pairs, kept because the plural-pair scan's test names their MANY keys as
        // the sites it has to trace by hand (a pair passed through a helper's parameters rather than
        // written at the call).
        public const string GalaxySystemFriendlyShip = "galaxy.system-friendly-ship";
        public const string GalaxySystemFriendlyShips = "galaxy.system-friendly-ships";
        public const string SystemSupplyingOutpost = "system.supplying-outpost";
        public const string SystemSupplyingOutposts = "system.supplying-outposts";

        // ---- the icon names ----
        // What each picture the game draws is called, substituted into the middle of a sentence the
        // game wrote: "+10 Food per Fertile". They are ordinary translatable strings and live in a
        // table of their own only because a real game has hundreds of them - in the shipped mod they
        // fill a second partial file of this class. Two are kept here for the mechanism's sake.
        public const string IconFood = "icon.food";
        public const string IconIndustry = "icon.industry";

        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { StartupReady, "Endless Space 2 Access ready" },
            { FragmentSeparator, " " },
            { ListSeparator, ", " },
            { Fraction, "{0} of {1}" },
            { FractionUnit, "{0} of {1} {2}" },
            { Quantity, "x {0}" },
            { ControlButton, "button" },
            { ControlGroup, "group" },
            { ControlTab, "tab" },
            { ControlCheckbox, "checkbox" },
            { ControlSlider, "slider" },
            { ControlComboBox, "combo box" },
            { ControlEditField, "edit field" },
            { ControlNumericEditField, "numeric editable" },
            { ControlMenuItem, "menu item" },
            { ControlRadioButton, "radio button" },
            { NavExpanded, "expanded" },
            { NavCollapsed, "collapsed" },
            { NavChecked, "checked" },
            { NavUnchecked, "not checked" },
            { NavSelected, "selected" },
            { NavDisabled, "unavailable" },
            { NavNoDetails, "Nothing in here" },
            { NavTable, "table" },
            { NavCellEmpty, "empty" },
            { SearchNoMatch, "No match for {0}" },
            { SearchCleared, "Search cleared" },
            { BufferUi, "UI" },
            { BufferEmpty, "Buffer empty" },
            { BufferLine, "{0}. {1}" },
            { ScreenMainMenu, "Main menu" },
            { ScreenMessageBox, "Dialog" },
            { ScreenLoading, "Loading" },
            { LoadingProgress, "{0} percent" },
            { LoadingProgressBar, "Loading progress" },
            { DragStarted, "Dragging {0}. {1} to drop, {2} to cancel." },
            { DragStartedPlain, "Dragging {0}" },
            { DragDropped, "Dropped {0}" },
            { DragDropRefused, "{0} cannot go there" },
            { DragCancelled, "Cancelled drag" },
            { DragDropTarget, "drop target" },
            { DragDraggable, "draggable" },
            { DragHint, "Press {0} to pick up {1}." },
            { DragDropHint, "Press {0} to drop {1}." },
            { ScreenGalaxy, "Galaxy" },
            { GalaxyTurn, "Turn {0}" },
            { GalaxyEndTurn, "End turn" },
            { GalaxyIdleFleets, "{0} idle fleets" },
            { GalaxyZoomedIn, "Zoomed in" },
            { GalaxyZoomedOut, "Zoomed out" },
            { GalaxyPlayerPlaying, "{0} player is still playing" },
            { GalaxyPlayersPlaying, "{0} players are still playing" },
            { GalaxySystemFriendlyShip, "{0} friendly ship" },
            { GalaxySystemFriendlyShips, "{0} friendly ships" },
            { SystemSupplyingOutpost, "Supplying {0} outpost" },
            { SystemSupplyingOutposts, "Supplying {0} outposts" },
        };

        private static readonly Dictionary<string, string> IconDefaults = new Dictionary<
            string,
            string
        >
        {
            { IconFood, "Food" },
            { IconIndustry, "Industry" },
        };

        // Keys already complained about, so a per-frame readout warns once, not every frame.
        private static readonly Dictionary<string, bool> Warned = new Dictionary<string, bool>();

        private static Dictionary<string, string> _overrides;

        // The game's language name, kept beside the overlay because the plural rule belongs to
        // the language rather than to the file (PluralRules).
        private static string _language;

        /// <summary>
        /// Make <paramref name="overrides"/> the active translation overlay. Null or empty clears
        /// back to the English defaults, which is also the right result for a language with no
        /// translation file.
        /// </summary>
        public static void Install(IDictionary<string, string> overrides)
        {
            Install(overrides, null);
        }

        /// <summary>
        /// Make <paramref name="overrides"/> the active translation overlay, spoken as
        /// <paramref name="language"/> - the game's own language name, which is also the name of
        /// the translation file. Null or empty overrides clear back to the English defaults; the
        /// language is still recorded, because <see cref="Plural"/>'s rule belongs to the language
        /// rather than to the file.
        /// </summary>
        public static void Install(IDictionary<string, string> overrides, string language)
        {
            Warned.Clear();
            _language = language;
            if (overrides == null || overrides.Count == 0)
            {
                _overrides = null;
                return;
            }

            Dictionary<string, string> copy = new Dictionary<string, string>(overrides.Count);
            foreach (KeyValuePair<string, string> entry in overrides)
            {
                copy[entry.Key] = entry.Value;
            }

            _overrides = copy;
        }

        /// <summary>Drop the overlay, returning to English defaults.</summary>
        public static void Reset()
        {
            _overrides = null;
            _language = null;
            Warned.Clear();
        }

        /// <summary>
        /// The translated string for <paramref name="key"/>, else the English default, else the
        /// key itself so an unknown key is visible in speech rather than silently empty.
        /// </summary>
        public static string Get(string key)
        {
            string value;
            if (_overrides != null && _overrides.TryGetValue(key, out value))
            {
                return value;
            }

            if (TryGetDefault(key, out value))
            {
                return value;
            }

            WarnOnce("get:" + key, "strings: no such key '" + key + "'");
            return key;
        }

        /// <summary>
        /// <see cref="Get"/> plus <c>string.Format</c>. A translation whose placeholders do not
        /// match the English template throws inside Format; that is a broken translation, not a
        /// broken game, so it is logged once and the English template is used instead. Never
        /// throws.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                WarnOnce(
                    "format:" + key,
                    "strings: bad format for key '" + key + "': " + template
                );
            }

            string fallback;
            if (TryGetDefault(key, out fallback) && fallback != template)
            {
                try
                {
                    return string.Format(fallback, args);
                }
                catch (FormatException) { }
            }

            return template;
        }

        /// <summary>
        /// A counted phrase in the form its number calls for, the number filling the chosen
        /// template's <c>{0}</c>.
        ///
        /// Each form is a WHOLE sentence of its own rather than a number glued to a noun, because the
        /// noun agrees with the number in most languages and no template can inflect a fragment handed
        /// to it. Two forms is what English needs and what a translator can always fill in - a language
        /// with a single form writes the same sentence twice.
        ///
        /// The forms BEYOND those two are real, and they live in the locale file rather than in a
        /// call site - English needs none of them and ships none, so no caller passes a third key
        /// and adding a language costs no code. <see cref="PluralKey"/> is where they are chosen,
        /// and is also what a caller uses whose count is not the phrase's only slot.
        /// </summary>
        public static string Plural(string oneKey, string manyKey, int count)
        {
            return Format(PluralKey(oneKey, manyKey, count), count);
        }

        /// <summary>
        /// <see cref="Plural"/>, for the callers that cannot let it do the formatting: a phrase
        /// whose count is not its only slot ("Probe launched towards {0}, {1} probes remaining"),
        /// and one whose two forms take different arguments altogether. Such a caller compares the
        /// answer against the ONE key to know which argument list goes with it.
        ///
        /// Three forms, two of which live in the locale file rather than at the call site. The
        /// paucal is <c>&lt;manyKey&gt;.few</c> (<see cref="PluralRules.FewSuffix"/>). The other is
        /// <c>&lt;manyKey&gt;.one</c> (<see cref="PluralRules.OneSuffix"/>), for the singular with a
        /// count that is not one - Russian's 21, 31 and so on, where a pair whose ONE sentence has
        /// no number in it would otherwise say something untrue. A count of one always takes
        /// <paramref name="oneKey"/>, and a missing form falls back to the key it hangs off, so a
        /// language that carries neither is spoken exactly as it was before either existed.
        /// </summary>
        public static string PluralKey(string oneKey, string manyKey, int count)
        {
            switch (PluralRules.For(_language, count))
            {
                case PluralForm.One:
                    if (count != 1)
                    {
                        string singularKey = manyKey + PluralRules.OneSuffix;
                        if (Has(singularKey))
                        {
                            return singularKey;
                        }
                    }

                    return oneKey;

                case PluralForm.Few:
                    string fewKey = manyKey + PluralRules.FewSuffix;
                    return Has(fewKey) ? fewKey : manyKey;

                default:
                    return manyKey;
            }
        }

        /// <summary>
        /// The compiled-in English template for <paramref name="key"/>. Exposed so translation
        /// files can be validated against the shipped keys and placeholders. The icon names
        /// (<see cref="IconDefaults"/>) are as much a shipped string as any other; they are held
        /// in their own table only because there are hundreds of them.
        /// </summary>
        public static bool TryGetDefault(string key, out string template)
        {
            return Defaults.TryGetValue(key, out template)
                || IconDefaults.TryGetValue(key, out template);
        }

        /// <summary>
        /// Every key the mod ships a phrase for. <see cref="TryGetDefault"/> answers "is this key
        /// ours"; this answers "what are all of them", which is the question the translation
        /// template has to match in BOTH directions - a key missing from english.json is a phrase
        /// no translator is ever offered, and there is no way to see that from the individual
        /// lookups.
        /// </summary>
        public static IEnumerable<string> DefaultKeys()
        {
            foreach (string key in Defaults.Keys)
            {
                yield return key;
            }

            foreach (string key in IconDefaults.Keys)
            {
                yield return key;
            }
        }

        /// <summary>Whether the mod ships a phrase for <paramref name="key"/> at all - asked where a
        /// key is COMPOSED and may legitimately not exist (a plural form the locale file does not
        /// carry), so that <see cref="Get"/>'s warn-once is not spent on a miss that is expected.
        /// </summary>
        public static bool Has(string key)
        {
            string ignored;
            return (_overrides != null && _overrides.TryGetValue(key, out ignored))
                || TryGetDefault(key, out ignored);
        }

        private static void WarnOnce(string warnKey, string message)
        {
            if (Warned.ContainsKey(warnKey))
            {
                return;
            }

            Warned[warnKey] = true;
            Log.Warn(message);
        }
    }
}
