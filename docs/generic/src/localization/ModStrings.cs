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
        public const string NavHasTooltip = "nav.has-tooltip";
        public const string NavNoDetails = "nav.no-details";

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

        // Picking something up and putting it down somewhere else (a ship into another fleet). The
        // words are the DRAG's, because that is the gesture these keys stand in for and the one the
        // game's own tooltips name. The carried thing is named in the mod's sentence but in the game's
        // own words, and each of these is a whole phrase so a language that frames "dragging X"
        // differently has somewhere to do it. Ending a drag without moving anything - the back key,
        // or putting the thing back where it came from - is one phrase and names nothing, because
        // nothing happened to name. A refusal normally speaks the GAME's reason instead; the one here
        // is the fallback for a check that refuses wordlessly.
        public const string CarryCarrying = "carry.carrying";
        public const string CarryDropped = "carry.dropped";
        public const string CarryDropRefused = "carry.drop-refused";
        public const string CarryCancelled = "carry.cancelled";

        /// <summary>What a control says while it would take the thing the player is holding.</summary>
        public const string CarryDropTarget = "carry.drop-target";

        /// <summary>What a control the player could pick something up from says while nothing is being
        /// carried - the drag's half of "has tooltip". Not said while something IS held: the useful fact
        /// about a control then is whether the thing can go there.</summary>
        public const string CarryDraggable = "carry.draggable";

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
            { ControlMenuItem, "menu item" },
            { ControlRadioButton, "radio button" },
            { NavExpanded, "expanded" },
            { NavCollapsed, "collapsed" },
            { NavChecked, "checked" },
            { NavUnchecked, "not checked" },
            { NavSelected, "selected" },
            { NavDisabled, "unavailable" },
            { NavHasTooltip, "has tooltip" },
            { NavNoDetails, "Nothing in here" },
            { SearchNoMatch, "No match for {0}" },
            { SearchCleared, "Search cleared" },
            { BufferUi, "UI" },
            { BufferEmpty, "Buffer empty" },
            { BufferLine, "{0}. {1}" },
            { ScreenMainMenu, "Main menu" },
            { ScreenMessageBox, "Dialog" },
            { ScreenLoading, "Loading" },
            { LoadingProgress, "{0} percent" },
            { CarryCarrying, "Dragging {0}" },
            { CarryDropped, "Dropped {0}" },
            { CarryDropRefused, "{0} cannot go there" },
            { CarryCancelled, "Cancelled drag" },
            { CarryDropTarget, "drop target" },
            { CarryDraggable, "draggable" },
            { ScreenGalaxy, "Galaxy" },
            { GalaxyTurn, "Turn {0}" },
            { GalaxyEndTurn, "End turn" },
            { GalaxyIdleFleets, "{0} idle fleets" },
            { GalaxyZoomedIn, "Zoomed in" },
            { GalaxyZoomedOut, "Zoomed out" },
            { GalaxyPlayerPlaying, "{0} player is still playing" },
            { GalaxyPlayersPlaying, "{0} players are still playing" },
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

        /// <summary>
        /// Make <paramref name="overrides"/> the active translation overlay. Null or empty clears
        /// back to the English defaults, which is also the right result for a language with no
        /// translation file.
        /// </summary>
        public static void Install(IDictionary<string, string> overrides)
        {
            Warned.Clear();
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
        /// with a single form writes the same sentence twice. A language that wants THREE or more
        /// (Russian, Polish, Arabic) is the trigger for real plural rules carried by the locale file;
        /// nothing here anticipates them, which is deliberate.
        /// </summary>
        public static string Plural(string oneKey, string manyKey, int count)
        {
            return Format(count == 1 ? oneKey : manyKey, count);
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
