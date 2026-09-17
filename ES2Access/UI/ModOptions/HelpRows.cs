using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// The Help category's service - a marker with nothing on it, exactly like
    /// <see cref="IModBookmarksService"/>. The tab declares no options of its own; it exists because
    /// the game's panel refuses to load without a registered service
    /// (<c>OptionsTabPanel.Load</c> logs an error and gives up), and because removing it is how the
    /// tab stops answering after a hot reload.
    /// </summary>
    public interface IModHelpService : IService { }

    /// <summary>The Help category's service itself, holding nothing - see
    /// <see cref="IModHelpService"/>.</summary>
    public sealed class ModHelpService : IModHelpService { }

    /// <summary>
    /// THE HELP TAB - where a player who wants the mod's documentation, its community or its author
    /// is sent, since none of the three is anywhere in the game.
    ///
    /// Three rows, each one address opened in whatever the desktop opens a link with. The page has
    /// no state: the same three rows whether or not a game is being played, so it is filled once and
    /// never rebuilt.
    ///
    /// NOTHING HERE LIGHTS APPLY, for the same reason as <see cref="BookmarkRows"/>: every row is a
    /// <c>ModRows.Button</c>, carrying an option nothing reads, so the window's "has anything
    /// changed" scan finds nothing however many times a row is pressed.
    /// </summary>
    public static class HelpRows
    {
        /// <summary>Where the mod documents itself.</summary>
        private const string Homepage = "https://neurrone.github.io/endless-space-2-access/";

        /// <summary>Where its players and its author talk.</summary>
        private const string Discord = "https://discord.gg/4wgAFFyPCH";

        /// <summary>Where the work is paid for.</summary>
        private const string Patreon = "https://patreon.com/NeurronesMods";

        /// <summary>Fill the Help tab. Called when the window builds the panel.</summary>
        public static void Fill(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                Log.Warn("mod options: the Help panel is not built, no rows added");
                return;
            }

            try
            {
                List<Option> options = new List<Option>();
                ModRows.Begin(panel);
                Add(
                    options,
                    ModRows.Button(
                        panel,
                        panel.Parent,
                        "helpHomepage",
                        ModStrings.Get(ModStrings.ModSettingsHelpHomepage),
                        OpenHomepage
                    )
                );
                Add(
                    options,
                    ModRows.Button(
                        panel,
                        panel.Parent,
                        "helpDiscord",
                        ModStrings.Get(ModStrings.ModSettingsHelpDiscord),
                        OpenDiscord
                    )
                );
                Add(
                    options,
                    ModRows.Button(
                        panel,
                        panel.Parent,
                        "helpPatreon",
                        ModStrings.Get(ModStrings.ModSettingsHelpPatreon),
                        OpenPatreon
                    )
                );
                ModRows.Publish(panel, options);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the Help tab threw: " + e);
            }
        }

        private static void OpenHomepage()
        {
            Open(Homepage);
        }

        private static void OpenDiscord()
        {
            Open(Discord);
        }

        private static void OpenPatreon()
        {
            Open(Patreon);
        }

        /// <summary>Hand the address to the desktop's own browser. A machine with nothing registered
        /// for a link - and a Unity build that will not leave the game - is a warning in the log and
        /// nothing else: the player is still on the page they pressed from.</summary>
        private static void Open(string url)
        {
            try
            {
                UnityEngine.Application.OpenURL(url);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: opening " + url + " threw: " + e);
            }
        }

        private static void Add(List<Option> options, Option option)
        {
            if (option != null)
            {
                options.Add(option);
            }
        }
    }
}
