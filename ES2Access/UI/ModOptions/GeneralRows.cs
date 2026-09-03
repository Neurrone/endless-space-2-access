using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.Loader;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// The General category's service - a marker with nothing on it, exactly like
    /// <see cref="IModKeybindsService"/>. It declares no options of its own: the tab's rows are
    /// minted one apiece by <see cref="GeneralRows"/>, because a row here answers for a setting the
    /// LOADER owns and a C# property on a service could not reach it.
    ///
    /// It exists because the game's panel refuses to load without a registered service
    /// (<c>OptionsTabPanel.Load</c> logs an error and gives up), and because removing it is how the
    /// tab stops answering after a hot reload.
    /// </summary>
    public interface IModGeneralService : IService { }

    /// <summary>The General category's service itself, holding nothing - see
    /// <see cref="IModGeneralService"/>.</summary>
    public sealed class ModGeneralService : IModGeneralService { }

    /// <summary>
    /// THE GENERAL TAB - the window's first, where a setting that belongs to no other tab lives.
    ///
    /// Two rows, in this order: whether the tooltips the game assembles on hover are read out
    /// once they have drawn (<see cref="ES2Access.UI.Settings.LongTooltipSettings"/>, which is
    /// the mod's own settings file like every other mod setting), then whether the game's cut
    /// scenes are described. The cut-scene setting is the odd one out: it is a BepInEx
    /// config entry the LOADER owns (<c>[Speech] cutsceneDescriptions</c>), and that file stays the
    /// one store - this row is the player's way into it rather than a second copy of it. So the row
    /// writes both halves at once: the live flag the cutscene watcher reads
    /// (<see cref="ES2Access.UI.CutsceneDescriptions.Enabled"/>), and the loader's setting, which
    /// BepInEx writes back to disk on every set.
    ///
    /// Non-latent, like every other mod row: the value moves as the box is ticked, Apply keeps it
    /// and Cancel puts the backup back through this same setter, so the end state is right either
    /// way and neither button needs a hook.
    ///
    /// THE LOADER DOES NOT HOT-RELOAD. A mod built against a newer loader contract than the one
    /// running finds no setter and throws <c>MissingMethodException</c> from the JIT; the write is
    /// caught and logged ONCE so the row still moves the live flag under an old loader, and the
    /// player's next launch reads whatever the file says.
    /// </summary>
    public static class GeneralRows
    {
        public static void Fill(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                Log.Warn("mod options: the General panel is not built, no rows added");
                return;
            }

            try
            {
                List<Option> options = new List<Option>();
                ModRows.Begin(panel);
                Option longTooltips = ModRows.Toggle(
                    panel,
                    "announceLongTooltips",
                    ModStrings.Get(ModStrings.ModSettingsAnnounceLongTooltips),
                    ReadAnnounceLongTooltips,
                    WriteAnnounceLongTooltips,
                    ModStrings.Get(ModStrings.ModSettingsAnnounceLongTooltipsDescription)
                );
                if (longTooltips != null)
                {
                    options.Add(longTooltips);
                }

                Option cutscenes = ModRows.Toggle(
                    panel,
                    "cutsceneDescriptions",
                    ModStrings.Get(ModStrings.ModSettingsCutsceneDescriptions),
                    ReadCutsceneDescriptions,
                    WriteCutsceneDescriptions
                );
                if (cutscenes != null)
                {
                    options.Add(cutscenes);
                }

                ModRows.Publish(panel, options);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the General tab threw: " + e);
            }
        }

        private static bool ReadAnnounceLongTooltips()
        {
            return ES2Access.UI.Settings.LongTooltipSettings.Announced;
        }

        private static void WriteAnnounceLongTooltips(bool wanted)
        {
            ES2Access.UI.Settings.LongTooltipSettings.Announced = wanted;
        }

        private static bool ReadCutsceneDescriptions()
        {
            return ES2Access.UI.CutsceneDescriptions.Enabled;
        }

        private static void WriteCutsceneDescriptions(bool wanted)
        {
            ES2Access.UI.CutsceneDescriptions.Enabled = wanted;
            try
            {
                TellTheLoader(wanted);
            }
            catch (Exception e)
            {
                if (!_loaderTooOld)
                {
                    _loaderTooOld = true;
                    Log.Warn(
                        "mod options: this loader cannot be told about the cutscene setting, so it "
                            + "lives only until the game is restarted: " + e
                    );
                }
            }
        }

        /// <summary>The one call into the loader, on its own so that a loader too old to have the
        /// setter throws HERE - Mono resolves a missing method as the calling method is JITted, so a
        /// caller that also does the live-flag write could lose that write too.</summary>
        private static void TellTheLoader(bool wanted)
        {
            ModHost host = ModEntry.Host;
            if (host != null)
            {
                host.CutsceneDescriptions = wanted;
            }
        }

        /// <summary>Whether the running loader has already refused the write. One line per session,
        /// not one per tick of the box.</summary>
        private static bool _loaderTooOld;

        /// <summary>Mod teardown: a new load must be able to complain about its own loader.
        /// </summary>
        public static void Forget()
        {
            _loaderTooOld = false;
        }
    }
}
