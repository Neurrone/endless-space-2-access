using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Speech;
using UnityEngine;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// A binding written out the way the player would SAY it - "Ctrl+Shift+Enter" - in the player's
    /// own language.
    ///
    /// The other half of <see cref="InputBinding.DisplayName"/>, which is English-only and exists for
    /// logs and the dev server. This one is spoken, so every part of it is a translated word: the
    /// modifiers and the joiner come out of <see cref="ModStrings"/> (a language that spells a chord
    /// "Strg und Eingabe" changes three strings, not this file), and the KEY is named by the GAME's own
    /// key-name table where it has a row for it - see <see cref="KeyName"/>.
    ///
    /// A key nothing names falls back to the engine's <c>KeyCode</c> name rather than to silence: the
    /// mod's table only has to grow when a new gesture is bound to a key the game never named, and a
    /// rebind the mod has never seen still reads out.
    ///
    /// It is addressed by (action key, binding index) rather than by a chord, because that is what a
    /// hint declares: the sentence names the ACTION, and re-binding the action re-words the sentence.
    /// The index is load-bearing - the map's off-lane move is the second binding of the SAME action
    /// as the ordinary move (<c>docs/interaction.md</c>).
    /// </summary>
    public static class ChordNames
    {
        /// <summary>The chord at <paramref name="bindingIndex"/> of the action called
        /// <paramref name="actionKey"/>, or null where the action, the binding, or the input manager
        /// itself is not there.
        ///
        /// Remembered: see <see cref="Forget"/> for what the answer is allowed to move with.</summary>
        public static string Of(ModInput input, string actionKey, int bindingIndex)
        {
            if (input == null)
            {
                return null;
            }

            Fresh(input);
            ChordKey key = new ChordKey(actionKey, bindingIndex);
            string remembered;
            if (_chords.TryGetValue(key, out remembered))
            {
                return remembered;
            }

            string composed = Compose(input, actionKey, bindingIndex);
            _chords[key] = composed;
            return composed;
        }

        private static string Compose(ModInput input, string actionKey, int bindingIndex)
        {
            InputAction action = input.Find(actionKey);
            IList<InputBinding> bindings = action == null ? null : action.Bindings;
            if (bindings == null || bindingIndex < 0 || bindingIndex >= bindings.Count)
            {
                return null;
            }

            InputBinding binding = bindings[bindingIndex];
            KeyboardBinding chord = binding as KeyboardBinding;
            if (chord != null)
            {
                return Of(chord);
            }

            // An EMPTY slot - a binding the player cleared - names nothing, exactly as an action
            // that was never bound does.
            return binding == null ? null : binding.DisplayName;
        }

        /// <summary>A control's name with the chord that works on it after it - "Next system
        /// (Alt+Right)" - for the few controls whose gesture is worth hearing on every landing rather
        /// than at the end of the review buffer, where the usage hints live.
        ///
        /// The connective is a template with both parts in it, never a bracket glued on here: a
        /// language that writes the chord first has somewhere to put it. An action nothing is bound to
        /// leaves the name exactly as it was.</summary>
        public static string Label(string label, string actionKey, int bindingIndex = 0)
        {
            if (string.IsNullOrEmpty(label))
            {
                return label;
            }

            ModInput input = ModEntry.Input;
            if (input == null)
            {
                // Nothing to name the chord from, which is the answer an unbound action gives too.
                return label;
            }

            // Before the lookup, not after: the stamp is what says the remembered labels still
            // belong to the bindings they were composed under.
            Fresh(input);
            LabelKey key = new LabelKey(label, actionKey, bindingIndex);
            string remembered;
            if (_labels.TryGetValue(key, out remembered))
            {
                return remembered;
            }

            string chord = Of(input, actionKey, bindingIndex);
            string composed = string.IsNullOrEmpty(chord)
                ? label
                : ModStrings.Format(ModStrings.LabelWithChord, label, chord);
            if (_labels.Count >= MaxLabels)
            {
                _labels.Clear();
            }

            _labels[key] = composed;
            return composed;
        }

        /// <summary>
        /// Drop every remembered chord and label. The composition reads three things: the action
        /// table (a rebind moves it), the mod's own words and the GAME's key-name table (a language
        /// change moves both). The first is watched by <see cref="ModInput.BindingGeneration"/>; the
        /// second has no generation to watch, so <c>ModLocale</c> says so here on the frame it
        /// installs a new language, before any screen builds.
        /// </summary>
        public static void Forget()
        {
            _memoInput = null;
            _chords.Clear();
            _labels.Clear();
        }

        // Composing a chord costs a scan of the whole action table, a StringBuilder, an
        // Enum.ToString, the game's localizer and a clean - and every context label the HUD and the
        // galaxy declare asks for one on every frame they build, silent or not. The answer moves
        // only when a binding does or the language does, so it is kept until one of them says
        // otherwise.
        private static ModInput _memoInput;
        private static int _memoGeneration;
        private static readonly Dictionary<ChordKey, string> _chords =
            new Dictionary<ChordKey, string>();
        private static readonly Dictionary<LabelKey, string> _labels =
            new Dictionary<LabelKey, string>();

        /// <summary>How many composed labels to keep. Capped rather than pruned: most labels are the
        /// mod's own constant words, but a few carry a caption the game rewrites (the end-turn
        /// button's), and forgetting the lot costs one recomposition each.</summary>
        private const int MaxLabels = 512;

        private static void Fresh(ModInput input)
        {
            int generation = input.BindingGeneration;
            if (ReferenceEquals(input, _memoInput) && generation == _memoGeneration)
            {
                return;
            }

            _memoInput = input;
            _memoGeneration = generation;
            _chords.Clear();
            _labels.Clear();
        }

        private struct ChordKey : IEquatable<ChordKey>
        {
            public ChordKey(string actionKey, int slot)
            {
                _actionKey = actionKey;
                _slot = slot;
            }

            private readonly string _actionKey;
            private readonly int _slot;

            public bool Equals(ChordKey other)
            {
                return _slot == other._slot && _actionKey == other._actionKey;
            }

            public override bool Equals(object other)
            {
                return other is ChordKey && Equals((ChordKey)other);
            }

            public override int GetHashCode()
            {
                return (_actionKey == null ? 0 : _actionKey.GetHashCode()) ^ _slot;
            }
        }

        private struct LabelKey : IEquatable<LabelKey>
        {
            public LabelKey(string label, string actionKey, int slot)
            {
                _label = label;
                _actionKey = actionKey;
                _slot = slot;
            }

            private readonly string _label;
            private readonly string _actionKey;
            private readonly int _slot;

            public bool Equals(LabelKey other)
            {
                return _slot == other._slot
                    && _actionKey == other._actionKey
                    && _label == other._label;
            }

            public override bool Equals(object other)
            {
                return other is LabelKey && Equals((LabelKey)other);
            }

            public override int GetHashCode()
            {
                return (_label == null ? 0 : _label.GetHashCode())
                    ^ ((_actionKey == null ? 0 : _actionKey.GetHashCode()) << 1)
                    ^ _slot;
            }
        }

        /// <summary>The same for a binding already in hand.</summary>
        public static string Of(KeyboardBinding chord)
        {
            if (chord == null)
            {
                return null;
            }

            string joiner = ModStrings.Get(ModStrings.KeyChordJoiner);
            StringBuilder name = new StringBuilder();
            if (chord.Ctrl)
            {
                Append(name, ModStrings.Get(ModStrings.KeyCtrl), joiner);
            }

            if (chord.Shift)
            {
                Append(name, ModStrings.Get(ModStrings.KeyShift), joiner);
            }

            if (chord.Alt)
            {
                Append(name, ModStrings.Get(ModStrings.KeyAlt), joiner);
            }

            Append(name, KeyName(chord.Key), joiner);
            return name.ToString();
        }

        // The mod's own words for the keys, for a key the GAME has no word for. Everything else answers
        // with the engine's name for the key, which is a readable word for all but the punctuation keys.
        private static readonly Dictionary<KeyCode, string> Named = new Dictionary<KeyCode, string>
        {
            { KeyCode.Return, ModStrings.KeyEnter },
            { KeyCode.KeypadEnter, ModStrings.KeyEnter },
            { KeyCode.Backslash, ModStrings.KeyBackslash },
        };

        /// <summary>The game's OWN word for the key comes first: it ships a table of 120 key names in
        /// every one of its ten languages (<c>%KeyCode&lt;Name&gt;</c>, the same lookup its options
        /// screen writes a binding out with - <c>KeyCombination.LocalizeKeyCode</c>), so a player hears
        /// the arrows and the modifiers named the way the game they are playing names them, in
        /// languages nobody has translated this mod into. The mod's own table is the fallback for the
        /// keys the game's table has no row for, and the engine's <c>KeyCode</c> name the fallback for
        /// the rest.</summary>
        private static string KeyName(KeyCode key)
        {
            string game = GameName(key);
            if (!string.IsNullOrEmpty(game))
            {
                return game;
            }

            string modKey;
            return Named.TryGetValue(key, out modKey) && ModStrings.Has(modKey)
                ? ModStrings.Get(modKey)
                : key.ToString();
        }

        // A miss answers with the key itself, which is how the game tells one apart (its own localizer
        // returns the key it was given). Nothing is spoken from a raw %key.
        private static string GameName(KeyCode key)
        {
            try
            {
                string localizationKey = "%KeyCode" + key;
                string text = Gui.Localize(localizationKey);
                return string.IsNullOrEmpty(text) || text == localizationKey
                    ? null
                    : AgeText.Clean(text);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static void Append(StringBuilder name, string part, string joiner)
        {
            if (name.Length > 0)
            {
                name.Append(joiner);
            }

            name.Append(part);
        }
    }
}
