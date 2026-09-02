using System;
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
        private static readonly ModPatch Patches = new ModPatch(
            "optiontext",
            "the game's text-field option row"
        );

        public static void Install()
        {
            Patches.Install(
                patch =>
                    patch.Prefix(
                        AccessTools.Method(
                            typeof(OptionTextFieldItem),
                            "OnTextFieldFocusLostCb"
                        ),
                        typeof(OptionTextFieldCommit),
                        "CommitTheTextRatherThanTheLabel"
                    )
            );
        }

        public static void Remove()
        {
            Patches.Remove();
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
                Patches.Report("committing a settings text field threw", e);
                return false;
            }
        }
    }
}
