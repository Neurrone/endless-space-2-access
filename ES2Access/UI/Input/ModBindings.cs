using System;
using System.Collections.Generic;
using Amplitude.Unity.Input;
using ES2Access.Core.Settings;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI.Settings;
using GameBinding = Amplitude.Unity.Input.InputBinding;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// WHICH KEYS THE MOD'S OWN ACTIONS ARE ON, as something the player can change and the mod can
    /// remember.
    ///
    /// Every action the mod registers has a stable name already (<c>ui.down</c>,
    /// <c>galaxy.scanNext</c>) - that name is the settings key and the identity everything here is
    /// addressed by. What this adds is three things the compiled-in bindings never had:
    ///
    /// - the two SLOTS the game's key-mapping row draws (primary, secondary), expressed as the
    ///   game's own <c>InputBinding</c> so the row needs nothing written for it;
    /// - a STABLE instance per action, because <c>InputBinding</c> has no <c>Equals</c> and the
    ///   options machinery decides "has this changed?" by comparing the value it stored against the
    ///   value it reads back (<c>Option.Changed</c>). A getter minting a fresh object every call
    ///   would light Apply permanently and make Escape always ask about unapplied changes;
    /// - persistence, as the game's own registry string per action
    ///   (<c>InputBinding.ToRegistryString</c>, the exact form its own input manager saves), written
    ///   only where the player has moved a key off its default.
    ///
    /// An action bound to MORE than two chords keeps the extras: they stay live and no row offers
    /// them, because the game's row has exactly two fields. Today that is one action - the inspect
    /// cursor's grow key, which is four chords because "+" is three of them on a common keyboard.
    ///
    /// Static because the input manager it drives is reached through <see cref="ModEntry.Input"/>
    /// and the options rows outlive no game; <see cref="Reset"/> hands all of it back.
    /// </summary>
    public static class ModBindings
    {
        /// <summary>How many slots a key-mapping row draws. The game's own row has two fields and
        /// nothing configures that.</summary>
        public const int Slots = 2;

        private const string SettingPrefix = "keys.";

        private static readonly Dictionary<string, GameBinding> Current =
            new Dictionary<string, GameBinding>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> Defaults =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly List<string> Order = new List<string>();

        /// <summary>The actions a Keybinds row is minted for, in the order the mod registered them -
        /// which groups them by family already (the cursor's keys, then the map's, then the review
        /// buffer's).</summary>
        public static IList<string> ActionKeys
        {
            get { return Order; }
        }

        /// <summary>
        /// Learn the compiled-in bindings and put the player's own back on top of them.
        ///
        /// Called once, straight after the keys are registered and before anything can press one:
        /// the defaults are what a saved binding is compared against (so the file only ever holds
        /// what the player actually moved), and a later build changing a default reaches everybody
        /// who never touched that key.
        /// </summary>
        public static void Install(ModInput input)
        {
            Reset();
            if (input == null)
            {
                return;
            }

            IList<InputAction> actions = input.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                InputAction action = actions[i];
                Order.Add(action.Key);
                GameBinding fromCode = Read(action);
                Defaults[action.Key] = fromCode.ToRegistryString();
                Current[action.Key] = fromCode;
            }

            Restore(input);
        }

        /// <summary>The binding the player's file asks for, applied over the compiled-in one. A line
        /// the game's own parser cannot read is logged and skipped rather than allowed to leave an
        /// action unbound.</summary>
        private static void Restore(ModInput input)
        {
            SettingsFile file = ModSettings.File;
            for (int i = 0; i < Order.Count; i++)
            {
                string actionKey = Order[i];
                string saved = file.Get(SettingPrefix + actionKey);
                if (string.IsNullOrEmpty(saved))
                {
                    continue;
                }

                try
                {
                    Set(input, actionKey, new GameBinding(saved));
                }
                catch (Exception e)
                {
                    Log.Warn(
                        "bindings: '" + saved + "' for " + actionKey + " could not be read: " + e
                    );
                }
            }
        }

        /// <summary>What an action's two slots are on right now - the SAME object every call until
        /// something changes it, which is what the options row's change detection needs.</summary>
        public static GameBinding Of(string actionKey)
        {
            GameBinding binding;
            if (actionKey != null && Current.TryGetValue(actionKey, out binding))
            {
                return binding;
            }

            InputAction action = Find(actionKey);
            binding = action == null ? new GameBinding(actionKey ?? string.Empty, KeyCombination.None, KeyCombination.None) : Read(action);
            if (actionKey != null)
            {
                Current[actionKey] = binding;
            }

            return binding;
        }

        /// <summary>Put an action on different keys, and remember the instance that was handed in -
        /// so that the options window putting its own backup back restores reference identity too,
        /// and the row stops reporting a change.</summary>
        public static void Set(string actionKey, GameBinding binding)
        {
            Set(ModEntry.Input, actionKey, binding);
        }

        private static void Set(ModInput input, string actionKey, GameBinding binding)
        {
            if (string.IsNullOrEmpty(actionKey) || binding == null)
            {
                return;
            }

            Current[actionKey] = binding;
            InputAction action = input == null ? null : input.Find(actionKey);
            if (action == null)
            {
                return;
            }

            action.SetSlot(0, KeyChords.FromCombination(binding.PrimaryKeyCombination));
            action.SetSlot(1, KeyChords.FromCombination(binding.SecondaryKeyCombination));
        }

        /// <summary>
        /// Write every action the player has moved off its default into the settings file, and take
        /// out the ones they have moved back. The caller saves the file.
        /// </summary>
        public static void Persist()
        {
            SettingsFile file = ModSettings.File;
            for (int i = 0; i < Order.Count; i++)
            {
                string actionKey = Order[i];
                string now = Of(actionKey).ToRegistryString();
                string byDefault;
                Defaults.TryGetValue(actionKey, out byDefault);
                file.Set(SettingPrefix + actionKey, now == byDefault ? null : now);
            }
        }

        /// <summary>Whether this is one of the mod's own actions - what tells a key-mapping row of the
        /// mod's Keybinds tab from one of the game's Controls tab, both of which are the same
        /// <c>OptionKeyMappingItem</c> and reach the same commit.</summary>
        public static bool Knows(string actionKey)
        {
            return actionKey != null && Current.ContainsKey(actionKey);
        }

        /// <summary>
        /// WHICH OF THE MOD'S ACTIONS THIS CHORD FIRES, or null where it fires none.
        ///
        /// Asked of the live input layer rather than of the two slots a row draws, because an action
        /// bound to more than two chords keeps the extras: they still fire, so they still shadow, and
        /// a warning built off the rows alone would miss exactly the keys nobody can see.
        /// </summary>
        public static string ActionOn(KeyCombination chord)
        {
            ModInput input = ModEntry.Input;
            if (chord == null || input == null || chord.Equals(KeyCombination.None))
            {
                return null;
            }

            IList<InputAction> actions = input.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                IList<InputBinding> bindings = actions[i].Bindings;
                for (int b = 0; b < bindings.Count; b++)
                {
                    KeyboardBinding keyboard = bindings[b] as KeyboardBinding;
                    if (keyboard != null && chord.Equals(KeyChords.ToCombination(keyboard)))
                    {
                        return actions[i].Key;
                    }
                }
            }

            return null;
        }

        /// <summary>Whether an action is on something other than the keys it shipped on - what the
        /// test report and a future "reset this row" ask.</summary>
        public static bool Moved(string actionKey)
        {
            string byDefault;
            return Defaults.TryGetValue(actionKey, out byDefault)
                && Of(actionKey).ToRegistryString() != byDefault;
        }

        /// <summary>How many chords an action really has. Anything past
        /// <see cref="Slots"/> is live and unrebindable.</summary>
        public static int ChordCount(string actionKey)
        {
            InputAction action = Find(actionKey);
            return action == null ? 0 : action.Bindings.Count;
        }

        /// <summary>What the Keybinds row calls this action, and what its description says. Both
        /// come out of <see cref="ModStrings"/>; an action nobody has written words for reads as its
        /// own name rather than as nothing.</summary>
        public static string Title(string actionKey)
        {
            string key = ModStrings.ActionTitleKey(actionKey);
            return ModStrings.Has(key) ? ModStrings.Get(key) : actionKey;
        }

        public static string Description(string actionKey)
        {
            string key = ModStrings.ActionDescriptionKey(actionKey);
            return ModStrings.Has(key) ? ModStrings.Get(key) : string.Empty;
        }

        public static void Reset()
        {
            Current.Clear();
            Defaults.Clear();
            Order.Clear();
        }

        private static InputAction Find(string actionKey)
        {
            ModInput input = ModEntry.Input;
            return input == null || actionKey == null ? null : input.Find(actionKey);
        }

        /// <summary>An action's first two chords as the game would express them.</summary>
        private static GameBinding Read(InputAction action)
        {
            return new GameBinding(
                action.Key,
                KeyChords.ToCombination(action.Slot(0) as KeyboardBinding),
                KeyChords.ToCombination(action.Slot(1) as KeyboardBinding)
            );
        }
    }
}
