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
    /// </summary>
    public static class ModStrings
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

        // What navigation says about a control beyond its own text.
        public const string NavExpanded = "nav.expanded";
        public const string NavCollapsed = "nav.collapsed";
        public const string NavDisabled = "nav.disabled";
        public const string NavHasTooltip = "nav.has-tooltip";
        public const string NavNoDetails = "nav.no-details";

        // The review buffers - the text the player walks line by line.
        public const string BufferUi = "buffer.ui";
        public const string BufferEmpty = "buffer.empty";
        public const string BufferLine = "buffer.line";

        // Screen names, spoken on arrival.
        public const string ScreenMainMenu = "screen.main-menu";

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
            { NavExpanded, "expanded" },
            { NavCollapsed, "collapsed" },
            { NavDisabled, "unavailable" },
            { NavHasTooltip, "has tooltip" },
            { NavNoDetails, "Nothing in here" },
            { BufferUi, "UI" },
            { BufferEmpty, "Buffer empty" },
            { BufferLine, "{0}. {1}" },
            { ScreenMainMenu, "Main menu" },
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

            if (Defaults.TryGetValue(key, out value))
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
            if (Defaults.TryGetValue(key, out fallback) && fallback != template)
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
        /// The compiled-in English template for <paramref name="key"/>. Exposed so translation
        /// files can be validated against the shipped keys and placeholders.
        /// </summary>
        public static bool TryGetDefault(string key, out string template)
        {
            return Defaults.TryGetValue(key, out template);
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
