using System;
using System.Reflection;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI.Input;
using HarmonyLib;
using GameBinding = Amplitude.Unity.Input.InputBinding;
using KeyCombination = Amplitude.Unity.Input.KeyCombination;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// ONE OF THE MOD'S KEYS AND ONE OF THE GAME'S ARE THE SAME CHORD - said out loud, in both
    /// directions, and nothing is taken away from anybody.
    ///
    /// The mod shadows the game's keys by design: while one of its screens is focused the game never
    /// sees a chord the mod claims. So an overlap is not a conflict to resolve, it is a consequence
    /// to state - "while the mod's X is active, the game's Y will not fire" - and the binding the
    /// player just made still lands. That is the owner's ruling (2026-08-23, ruling 9), and it is why
    /// this does not go anywhere near the game's own conflict machinery, which resolves an overlap by
    /// STEALING the chord from the other action.
    ///
    /// The game's own check cannot do this work in either direction. The mod's rows carry
    /// <c>AcceptsMultipleKeys</c> (ruling 10), which makes the game's row skip its already-bound
    /// lookup on commit and makes a mod row invisible to that lookup as a steal target
    /// (<c>OptionKeyMappingItem</c> :83-98, :138-142) - deliberately, so no mod row can be stolen from
    /// and mod rows never warn about each other. And the lookup only ever scans the ONE tab the row
    /// is in, so the game's Controls tab could not see a mod row even without that flag.
    ///
    /// So the hook is the commit itself, which both windows share: every key-mapping row - the game's
    /// and the mod's - writes its new value in <c>OnChangeOptionValueConfirmation</c> (:147-166), once
    /// per commit and after the value has landed. The prefix remembers what the row held, the postfix
    /// says something only about a chord the commit actually ADDED, so re-committing a row does not
    /// re-announce the slot nobody touched. Neither Cancel's restore nor Reset to Defaults passes
    /// through here, which is exactly right: they are not the player putting a key somewhere.
    /// </summary>
    internal static class BindingOverlaps
    {
        private static Harmony _harmony;

        public static void Install()
        {
            Remove();

            // A unique id per load, for the reason GameKeyStandDown documents: a fixed id lets the
            // unpatch of the assembly a reload replaced strip this load's patches.
            Harmony harmony = new Harmony(
                "endless.space2.access.bindingoverlaps." + Guid.NewGuid().ToString("N")
            );

            try
            {
                harmony.Patch(
                    Commit(),
                    new HarmonyMethod(Own("RememberWhatItHeld")),
                    new HarmonyMethod(Own("SayWhatItShadows"))
                );
                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, an overlap simply goes unmentioned - worth saying loudly and not worth
                // refusing to start over.
                Log.Error("the key-binding commit could not be patched: " + e);
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception undo)
                {
                    Log.Warn("and the partial patch could not be undone: " + undo.Message);
                }
            }
        }

        public static void Remove()
        {
            Harmony harmony = _harmony;
            _harmony = null;
            if (harmony == null)
            {
                return;
            }

            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception e)
            {
                Log.Error("the key-binding commit could not be unpatched: " + e);
            }
        }

        /// <summary>Internal rather than private so the dev server can report whether it is still
        /// patched.</summary>
        internal static MethodInfo Commit()
        {
            MethodInfo commit = AccessTools.Method(
                typeof(OptionKeyMappingItem),
                "OnChangeOptionValueConfirmation"
            );
            if (commit == null)
            {
                throw new MissingMethodException(
                    typeof(OptionKeyMappingItem).FullName,
                    "OnChangeOptionValueConfirmation"
                );
            }

            return commit;
        }

        private static MethodInfo Own(string name)
        {
            return typeof(BindingOverlaps).GetMethod(
                name,
                BindingFlags.Static | BindingFlags.NonPublic
            );
        }

        private static void RememberWhatItHeld(OptionKeyMappingItem __instance, ref object __state)
        {
            try
            {
                Option option = __instance == null ? null : __instance.Option;
                __state = option == null ? null : option.Value;
            }
            catch (Exception)
            {
                __state = null;
            }
        }

        private static void SayWhatItShadows(
            OptionKeyMappingItem __instance,
            MessageBoxResultEventArgs e,
            object __state
        )
        {
            try
            {
                if (e == null || e.Result != MessageBoxResult.Ok || __instance == null)
                {
                    return;
                }

                Option option = __instance.Option;
                GameBinding now = option == null ? null : option.Value as GameBinding;
                if (now == null)
                {
                    return;
                }

                GameBinding before = __state as GameBinding;
                string action = now.InputAction.ToString();
                bool ours = ModBindings.Knows(action);
                if (!Announce(option, action, ours, now.PrimaryKeyCombination, before))
                {
                    Announce(option, action, ours, now.SecondaryKeyCombination, before);
                }
            }
            catch (Exception thrown)
            {
                Log.Warn("bindings: checking what a new key shadows threw: " + thrown);
            }
        }

        /// <summary>One newly bound chord, checked against the other side. One sentence per commit:
        /// the player pressed one combination, and a second box behind the first would be read as a
        /// second problem.</summary>
        private static bool Announce(
            Option option,
            string action,
            bool ours,
            KeyCombination chord,
            GameBinding before
        )
        {
            if (chord == null || chord.Equals(KeyCombination.None))
            {
                return false;
            }

            if (before != null && (Holds(before, chord)))
            {
                // The row already had this chord in one of its slots: nothing new was bound.
                return false;
            }

            string modAction;
            string gameAction;
            if (ours)
            {
                gameAction = GameActionOn(chord);
                if (gameAction == null)
                {
                    return false;
                }

                modAction = ModBindings.Title(action);
            }
            else
            {
                string mine = ModBindings.ActionOn(chord);
                if (mine == null)
                {
                    return false;
                }

                modAction = ModBindings.Title(mine);
                gameAction = GameTitle(option);
            }

            // One button, not two. The game hides a message box's button by giving it an empty title
            // (MessageBoxWindow.cs :96-98), and this box has nothing to cancel: the binding has
            // already landed and neither answer would take it back.
            Gui.GuiService.ShowMessage(
                ModStrings.Format(ModStrings.NavKeyBindingShadowed, modAction, gameAction),
                MessageBoxType.INFORMATIVE,
                null,
                "%MessageBoxConfirmationTitle",
                "%MessageBoxValidateTitle",
                string.Empty
            );
            return true;
        }

        private static bool Holds(GameBinding binding, KeyCombination chord)
        {
            return chord.Equals(binding.PrimaryKeyCombination)
                || chord.Equals(binding.SecondaryKeyCombination);
        }

        /// <summary>What the GAME calls the action this chord is on, or null where it is on none.
        /// Read from the game's own options rather than from its binding list, because the options
        /// are what carry the localized title the box has to name.</summary>
        private static string GameActionOn(KeyCombination chord)
        {
            object service = Services.GetService<IInputOptionsService>();
            if (service == null)
            {
                return null;
            }

            Option[] options = Option.GetOptions(
                service,
                typeof(IInputOptionsService),
                true,
                true,
                true
            );
            for (int i = 0; i < options.Length; i++)
            {
                GameBinding binding = options[i].Value as GameBinding;
                if (binding != null && Holds(binding, chord))
                {
                    return GameTitle(options[i]);
                }
            }

            return null;
        }

        private static string GameTitle(Option option)
        {
            return option == null
                ? null
                : Gui.Localize("%Option" + option.OptionTypeAttribute.Name + "Title");
        }
    }
}
