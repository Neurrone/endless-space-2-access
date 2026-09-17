using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude.Unity.Input;
using ES2Access.Core.Util;
using HarmonyLib;
using UnityEngine;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THREE KEYS AT ONCE, ON THE MOD'S OWN ROWS.
    ///
    /// The game's capture widget takes two keys and stops
    /// (<c>AgeControlKeyBindingField.MaximumNumberOfKeysByCombination</c> = 2). Two is enough for the
    /// game's own bindings, and Control+Alt+Left is not: the Control and the Alt fill the list, the
    /// arrow never gets in, and the row commits a chord of two modifiers and no key. So the mod ships
    /// chords its own rebinding page could not capture - the player could read Control+Alt+Left out of
    /// the row and never put it back after changing it.
    ///
    /// The cap is lifted for the MOD's rows only (owner ruling 2026-09-17): the game's Controls tab
    /// keeps its two, because a longer chord there would be committed into the game's own binding
    /// table, which is not this mod's to change the shape of.
    ///
    /// HOW: a prefix on <c>UpdateFocused</c> that, on one of the mod's own fields, does the method's
    /// own work with a cap of four - three modifiers and a key, which is every chord the mod's input
    /// layer can express - and returns false. Not a transpiler, and not a write to the static cap:
    /// the cap is <c>static readonly</c>, so writing it would reach the game's rows as well as the
    /// mod's and would have to be put back on a path that includes every way a capture can end. The
    /// replicated body is four statements long and calls nothing but the field's own privates, which
    /// are reached by reflection resolved once. <c>base.UpdateFocused()</c> is skipped because
    /// <c>AgeControl.UpdateFocused</c> is empty in this build - the one thing that has to be re-checked
    /// if the game is ever patched.
    /// </summary>
    internal static class LongChordCapture
    {
        /// <summary>Three modifiers and a key: what the mod's own <c>KeyboardBinding</c> can hold, so
        /// there is nothing to gain by taking a fifth.</summary>
        private const int MaximumKeys = 4;

        private static readonly ModPatch Patches = new ModPatch(
            "longchordcapture",
            "the mod's key-capture fields"
        );

        /// <summary>The mod's own capture fields. Reference identity, and a page rebuild starts a new
        /// set - a cloned options window leaves its fields behind destroyed.</summary>
        private static readonly HashSet<AgeControlKeyBindingField> Ours =
            new HashSet<AgeControlKeyBindingField>();

        private static FieldInfo _pressed;
        private static FieldInfo _down;
        private static FieldInfo _up;
        private static MethodInfo _scan;

        public static void Install()
        {
            Patches.Install(
                patch =>
                {
                    Resolve();
                    patch.Prefix(
                        AccessTools.Method(
                            typeof(AgeControlKeyBindingField),
                            "UpdateFocused"
                        ),
                        typeof(LongChordCapture),
                        "CaptureLongChords"
                    );
                }
            );
        }

        public static void Remove()
        {
            Patches.Remove();
            Ours.Clear();
        }

        /// <summary>Forget the rows of the page about to be rebuilt.</summary>
        public static void Forget()
        {
            Ours.Clear();
        }

        /// <summary>Both of a mod row's capture fields, as the row is built.</summary>
        public static void Watch(OptionKeyMappingItem item)
        {
            if (item == null)
            {
                return;
            }

            if (item.PrimaryKeyBindingField != null)
            {
                Ours.Add(item.PrimaryKeyBindingField);
            }

            if (item.SecondaryKeyBindingField != null)
            {
                Ours.Add(item.SecondaryKeyBindingField);
            }
        }

        private static void Resolve()
        {
            _pressed = Field("pressedKeyCodes");
            _down = Field("newKeysDown");
            _up = Field("newKeysUp");
            _scan = AccessTools.Method(
                typeof(AgeControlKeyBindingField),
                "ScanNewKeysDownAndUp"
            );
            if (_scan == null)
            {
                throw new MissingMethodException(
                    typeof(AgeControlKeyBindingField).FullName,
                    "ScanNewKeysDownAndUp"
                );
            }
        }

        private static FieldInfo Field(string name)
        {
            FieldInfo found = AccessTools.Field(typeof(AgeControlKeyBindingField), name);
            if (found == null)
            {
                throw new MissingFieldException(
                    typeof(AgeControlKeyBindingField).FullName,
                    name
                );
            }

            return found;
        }

        private static bool CaptureLongChords(AgeControlKeyBindingField __instance)
        {
            if (__instance == null || !Ours.Contains(__instance))
            {
                return true;
            }

            try
            {
                _scan.Invoke(__instance, null);
                List<KeyCode> pressed = (List<KeyCode>)_pressed.GetValue(__instance);
                List<KeyCode> down = (List<KeyCode>)_down.GetValue(__instance);
                List<KeyCode> up = (List<KeyCode>)_up.GetValue(__instance);
                if (down.Count > 0)
                {
                    for (int i = 0; i < down.Count && pressed.Count < MaximumKeys; i++)
                    {
                        pressed.Add(down[i]);
                    }

                    __instance.KeyCombination = new KeyCombination(pressed);
                    if (__instance.Label != null)
                    {
                        __instance.Label.Text = __instance.KeyCombination.ToLocalizedString();
                    }
                }

                if (up.Count > 0)
                {
                    pressed.Clear();
                    if (
                        __instance.UseValidateCallback
                        && __instance.OnValidateObject != null
                        && !string.IsNullOrEmpty(__instance.OnValidateMethod)
                    )
                    {
                        __instance.OnValidateObject.SendMessage(
                            __instance.OnValidateMethod,
                            __instance.gameObject,
                            SendMessageOptions.RequireReceiver
                        );
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Patches.Report("options: capturing a long chord threw", e);
                // Leave the field to the game rather than to a hook that is throwing: two keys is
                // worse than four and better than a capture that never ends.
                return true;
            }
        }
    }
}
