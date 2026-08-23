using System;
using System.Reflection;
using ES2Access.Core.Util;
using HarmonyLib;
using UnityEngine;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE GAME'S TEXT-FIELD ROW COMMITS THE WRONG THING, and this is where the mod's rows get a
    /// commit that works.
    ///
    /// <c>OptionTextFieldItem.OnTextFieldFocusLostCb</c> is one line -
    /// <c>base.Option.Value = TextFieldLabel;</c> - and what it assigns is the LABEL OBJECT, not the
    /// text on it. <c>Option.SetValue</c> catches the resulting <c>InvalidCastException</c> and logs
    /// it, so the row is not merely wrong, it is silently wrong: whatever was typed is dropped and
    /// the option keeps its old value. No option the game ships is a text field, which is why nobody
    /// ever noticed.
    ///
    /// The prefix answers ONLY for a row the mod minted (<see cref="ModRows.IsOurText"/>) and hands
    /// every other one straight back to the game: patching a defect is not the same as deciding to
    /// own it, and a row the game ever does declare should behave exactly as the game's build does.
    ///
    /// It runs inside the engine's own focus change, so it does the one thing that has to happen
    /// there - write the text - and nothing else. What the write turns out to MEAN (a category
    /// filled, a keyword added, a name refused) is the setter's business, and anything that changes
    /// the shape of the page is deferred to the pump by <see cref="ScannerEditor"/> rather than done
    /// here, because the field being typed in is mid-<c>FocusLoss</c> as this returns.
    /// </summary>
    internal static class OptionTextFieldCommit
    {
        private static Harmony _harmony;
        private static bool _reportedFailure;

        public static void Install()
        {
            Remove();

            // A unique id per load: a fixed one lets the unpatch of the assembly a reload replaced
            // strip this load's patches.
            Harmony harmony = new Harmony(
                "endless.space2.access.optiontext." + Guid.NewGuid().ToString("N")
            );

            try
            {
                MethodInfo target = AccessTools.Method(
                    typeof(OptionTextFieldItem),
                    "OnTextFieldFocusLostCb"
                );
                if (target == null)
                {
                    throw new MissingMethodException(
                        typeof(OptionTextFieldItem).FullName,
                        "OnTextFieldFocusLostCb"
                    );
                }

                harmony.Patch(
                    target,
                    new HarmonyMethod(
                        typeof(OptionTextFieldCommit).GetMethod(
                            "CommitTheTextRatherThanTheLabel",
                            BindingFlags.Static | BindingFlags.NonPublic
                        )
                    )
                );
                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, every name and keyword the player types is thrown away silently.
                Log.Error("the game's text-field option row could not be patched: " + e);
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
            _reportedFailure = false;
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
                Log.Error("the game's text-field option row could not be unpatched: " + e);
            }
        }

        private static bool CommitTheTextRatherThanTheLabel(
            OptionTextFieldItem __instance,
            GameObject obj
        )
        {
            try
            {
                if (__instance == null || !ModRows.IsOurText(__instance.Option))
                {
                    return true;
                }

                __instance.Option.Value =
                    __instance.TextFieldLabel == null
                        ? string.Empty
                        : AgeText.Clean(__instance.TextFieldLabel.Text) ?? string.Empty;
                return false;
            }
            catch (Exception e)
            {
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    Log.Warn("committing a settings text field threw: " + e);
                }

                return false;
            }
        }
    }
}
