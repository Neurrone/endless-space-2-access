using System;
using System.Collections.Generic;
using System.IO;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI.Bookmarks;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// The Bookmarks category's service - a marker with nothing on it, exactly like
    /// <see cref="IModGeneralService"/>. The tab declares no options of its own: what it holds
    /// depends on whether a game is open and whether that campaign's bookmarks have reached the
    /// disk, which no fixed set of C# properties could say.
    ///
    /// It exists because the game's panel refuses to load without a registered service
    /// (<c>OptionsTabPanel.Load</c> logs an error and gives up), and because removing it is how the
    /// tab stops answering after a hot reload.
    /// </summary>
    public interface IModBookmarksService : IService { }

    /// <summary>The Bookmarks category's service itself, holding nothing - see
    /// <see cref="IModBookmarksService"/>.</summary>
    public sealed class ModBookmarksService : IModBookmarksService { }

    /// <summary>
    /// THE BOOKMARKS TAB - where this game's map bookmarks are kept, and how to hand them to
    /// somebody else.
    ///
    /// It holds no setting. Map bookmarks are set on the map and written the moment they are set
    /// (<see cref="MapBookmarkStore"/>); what a player cannot do from the map is find the file or
    /// give it to the friend they just sent a save to. So this page is a sentence saying where the
    /// bookmarks are and two ways of reaching them - the file's text on the clipboard, and the
    /// folder itself.
    ///
    /// FOUR STATES, and the page is built from whichever is true when the window OPENS
    /// (<see cref="Refill"/>, called from <c>ModOptionsWindow.OnBeginShow</c> before the game takes
    /// its backup of every option, so nothing the rebuild adds is backed up already-changed):
    /// no game at all, a campaign the player has never saved, a saved campaign with no bookmarks
    /// yet, and a campaign whose file is on disk. Each is one caption over the buttons that make
    /// sense under it, and the first has no caption and no message at all (owner ruling
    /// 2026-09-02): with a game open there is something to say about THIS campaign, and on the main
    /// menu there is not.
    ///
    /// NOTHING HERE LIGHTS APPLY. Both rows are <c>ModRows.Button</c>, which carries an option
    /// nothing reads, so the window's own "has anything changed" scan finds nothing and Apply stays
    /// unavailable however many times either button is pressed.
    /// </summary>
    public static class BookmarkRows
    {
        /// <summary>Fill the Bookmarks tab. Called when the window builds the panel, and again
        /// every time the window is shown, because what the page says depends on state the player
        /// changes elsewhere.</summary>
        public static void Fill(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                Log.Warn("mod options: the Bookmarks panel is not built, no rows added");
                return;
            }

            _panel = panel;
            try
            {
                List<Option> options = new List<Option>();
                ModRows.Begin(panel);

                string caption = Caption();
                if (caption != null)
                {
                    Add(options, ModRows.Caption(panel, "bookmarksState", caption));
                }

                if (InGame() && MapBookmarkStore.Saved)
                {
                    Add(
                        options,
                        ModRows.Button(
                            panel,
                            panel.Parent,
                            "bookmarksCopy",
                            ModStrings.Get(ModStrings.ModSettingsBookmarksCopy),
                            Copy
                        )
                    );
                }

                // Only where there is something in it: the folder is made by the first write, so
                // sending a player to it before then would open an explorer on nothing - or on a
                // folder that does not exist. This is also what makes the tab EMPTY on the main
                // menu of an install nobody has bookmarked anything in yet.
                if (MapBookmarkStore.FolderHoldsBookmarks)
                {
                    Add(
                        options,
                        ModRows.Button(
                            panel,
                            panel.Parent,
                            "bookmarksOpenFolder",
                            ModStrings.Get(ModStrings.ModSettingsBookmarksOpenFolder),
                            OpenFolder
                        )
                    );
                }

                ModRows.Publish(panel, options);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the Bookmarks tab threw: " + e);
            }
        }

        /// <summary>Build the page again from what is true now - the window being shown again after
        /// the player saved the game, set their first bookmark, or left one campaign for another.
        /// </summary>
        public static void Refill()
        {
            OptionsTabPanel panel = _panel;
            if (panel == null || panel.OptionsTable == null)
            {
                return;
            }

            try
            {
                ModRows.Clear(panel);
                Fill(panel);
                panel.RefreshNow();
            }
            catch (Exception e)
            {
                Log.Warn("mod options: rebuilding the Bookmarks tab threw: " + e);
            }
        }

        /// <summary>Say what a press did. From the pump, never from the press itself: a mouse click
        /// arrives inside the engine's own dispatch, and the two ways of pressing a drawn row must
        /// say the same thing (<see cref="ModRows.Activate"/>).</summary>
        public static void Tick()
        {
            string say = _say;
            _say = null;
            Voice.Say(say, false);
        }

        /// <summary>Mod teardown: hold no panel and no unsaid line across a reload.</summary>
        public static void Forget()
        {
            _panel = null;
            _say = null;
        }

        // ---- what the page says ----

        /// <summary>
        /// The one sentence over the rows, or null for the state that has none.
        ///
        /// The order is the order the questions become answerable: without a game there is no
        /// campaign to say anything about, without a save there is no file name to say, and without
        /// a write there is no file.
        /// </summary>
        private static string Caption()
        {
            if (!InGame())
            {
                return null;
            }

            string path = MapBookmarkStore.Path;
            if (path == null)
            {
                return ModStrings.Get(ModStrings.ModSettingsBookmarksUnsaved);
            }

            return MapBookmarkStore.Saved
                ? ModStrings.Format(ModStrings.ModSettingsBookmarksSavedTo, path)
                : ModStrings.Get(ModStrings.ModSettingsBookmarksNone);
        }

        // ---- what the two buttons do ----

        /// <summary>
        /// THE FILE, AS TEXT, ON THE CLIPBOARD - the whole point of the tab.
        ///
        /// The use case is a save that has changed hands: the bookmarks the sender made are in a
        /// file the receiver has no copy of, and a file is not something a chat window will carry.
        /// So the text is the file itself with one line in front of it saying what to do with it,
        /// and that line names the FILE rather than the folder, because the name is what the
        /// receiver cannot work out for themselves - it carries the campaign's GUID, which is what
        /// binds the bookmarks to the save they came with.
        ///
        /// The line is a comment in the file's own format (<see cref="ES2Access.Core.Settings.SettingsFile"/>
        /// ignores a line starting with <c>#</c>), so pasted text can be saved exactly as it is and
        /// read back with the instruction still in it. Deliberately a plain <c>#</c> and not the
        /// <c>#!</c> the mod stamps its own header with: that mark means "the mod wrote this line
        /// and may rewrite it", and this line belongs to the person who pasted it.
        /// </summary>
        private static void Copy()
        {
            string path = MapBookmarkStore.Path;
            try
            {
                if (path == null || !File.Exists(path))
                {
                    return;
                }

                string header = ModStrings.Format(
                    ModStrings.ModSettingsBookmarksCopyHeader,
                    System.IO.Path.GetFileName(path)
                );
                UnityEngine.GUIUtility.systemCopyBuffer =
                    CommentMark + " " + header + Environment.NewLine + File.ReadAllText(path);
                _say = ModStrings.Get(ModStrings.ModSettingsBookmarksCopied);
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: copying " + path + " to the clipboard threw: " + e);
            }
        }

        /// <summary>Show the player the folder in whatever their desktop opens a folder with. The
        /// row is only drawn when the folder holds a file, so there is nothing to create here.
        /// </summary>
        private static void OpenFolder()
        {
            string folder = MapBookmarkStore.Folder;
            try
            {
                if (folder == null || !Directory.Exists(folder))
                {
                    return;
                }

                Log.Info("bookmarks: opening the bookmarks folder " + folder);
                System.Diagnostics.Process.Start(folder);
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: opening the folder " + folder + " threw: " + e);
            }
        }

        // ---- the machinery ----

        /// <summary>What a comment looks like in the format the bookmarks file is written in. The
        /// mark itself is <see cref="ES2Access.Core.Settings.SettingsFile"/>'s; it is spelled here
        /// rather than borrowed because that class's own constant is its HEADER mark.</summary>
        private const string CommentMark = "#";

        private static void Add(List<Option> options, Option option)
        {
            if (option != null)
            {
                options.Add(option);
            }
        }

        /// <summary>Whether a game is being played, which is what decides whether the page has
        /// anything to say about a campaign. Wrapped because the gui service is not always there to
        /// ask.</summary>
        private static bool InGame()
        {
            try
            {
                return Gui.IsInGame;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static OptionsTabPanel _panel;
        private static string _say;
    }
}
