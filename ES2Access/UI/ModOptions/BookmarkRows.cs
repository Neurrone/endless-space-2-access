using System;
using System.Collections.Generic;
using System.IO;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Options;
using ES2Access.Core.Settings;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.ES2.Bookmarks;
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
    /// THE BOOKMARKS TAB - where this game's map bookmarks are kept, how to hand them to somebody
    /// else, and how to take somebody else's.
    ///
    /// It holds no setting. Map bookmarks are set on the map and written the moment they are set
    /// (<see cref="MapBookmarkStore"/>); what a player cannot do from the map is find the file, give
    /// it to the friend they just sent a save to, or put the one that friend sent back where it
    /// belongs. So this page is a sentence saying where the bookmarks are and three buttons - the
    /// file's text on the clipboard, the clipboard back into the file it names
    /// (<see cref="Import"/>), and the folder itself.
    ///
    /// THE IMPORT ROW IS DRAWN ALWAYS, the main menu included, because the paste says which campaign
    /// it belongs to and bookmarks usually arrive before the save they go with is ever loaded. The
    /// other two rows are still gated on there being something for them to act on.
    ///
    /// FOUR STATES, and the page is built from whichever is true when the window OPENS
    /// (<see cref="Refill"/>, called from <c>ModOptionsWindow.OnBeginShow</c> before the game takes
    /// its backup of every option, so nothing the rebuild adds is backed up already-changed):
    /// no game at all, a campaign the player has never saved, a saved campaign with no bookmarks
    /// yet, and a campaign whose file is on disk. Each is one SENTENCE - a row of its own the cursor
    /// stops on (owner ruling 2026-09-17), not a caption naming a section - over the buttons that
    /// make sense under it, and the first has no sentence at all (owner ruling
    /// 2026-09-02): with a game open there is something to say about THIS campaign, and on the main
    /// menu there is not.
    ///
    /// NOTHING HERE LIGHTS APPLY. Every row is a <c>ModRows.Button</c>, which carries an option
    /// nothing reads, so the window's own "has anything changed" scan finds nothing and Apply stays
    /// unavailable however many times any of them is pressed.
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
                    Add(options, ModRows.Sentence(panel, "bookmarksState", caption));
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

                // ALWAYS, the main menu included (owner ruling 2026-09-17): what is on the clipboard
                // says which campaign the paste belongs to, so there is nothing about the game being
                // played - or about there being one - that the row depends on.
                Add(
                    options,
                    ModRows.Button(
                        panel,
                        panel.Parent,
                        "bookmarksImport",
                        ModStrings.Get(ModStrings.ModSettingsBookmarksImport),
                        Import
                    )
                );

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
            // A paste the player said Ok to is written here, from the pump, so that what it changes
            // - the page, the box that reports it - follows in this same tick.
            BookmarkImport confirmed = _confirmed;
            _confirmed = null;
            if (confirmed != null)
            {
                Write(confirmed);
            }

            // The page first, and only then the box: an import changes which rows the page has
            // (a campaign with no file has one now), and rebuilding it from inside the press would
            // destroy the very row that dispatched it.
            if (_refill)
            {
                _refill = false;
                Refill();
            }

            string say = _say;
            _say = null;
            Voice.Say(say, false);

            string box = _box;
            _box = null;
            if (box != null)
            {
                Box(box);
            }

            string ask = _ask;
            _ask = null;
            if (ask != null)
            {
                Ask(ask);
            }
        }

        /// <summary>Mod teardown: hold no panel, no unsaid line and no unshown box across a reload.
        /// </summary>
        public static void Forget()
        {
            _panel = null;
            _say = null;
            _box = null;
            _ask = null;
            _pending = null;
            _confirmed = null;
            _refill = false;
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
        /// THE FILE, AS TEXT, ON THE CLIPBOARD - what a save that has changed hands needs, because
        /// the bookmarks the sender made are in a file the receiver has no copy of and a file is not
        /// something a chat window will carry.
        ///
        /// The text is the file EXACTLY as it is on disk (owner ruling 2026-09-17), with nothing
        /// added in front of it. Nothing needs to be: the file says which campaign and which faction
        /// it belongs to in two keys of its own (<see cref="BookmarkFile"/>), so the receiver pastes
        /// it into <see cref="Import"/> and the mod works out the name and the folder itself.
        /// </summary>
        private static void Copy()
        {
            string path = MapBookmarkStore.Path;
            try
            {
                if (path == null || !File.Exists(path))
                {
                    _say = ModStrings.Get(ModStrings.ModSettingsBookmarksCopyFailed);
                    return;
                }

                UnityEngine.GUIUtility.systemCopyBuffer = File.ReadAllText(path);
                _say = ModStrings.Get(ModStrings.ModSettingsBookmarksCopied);
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: copying " + path + " to the clipboard threw: " + e);
                _say = ModStrings.Get(ModStrings.ModSettingsBookmarksCopyFailed);
            }
        }

        /// <summary>
        /// SOMEBODY ELSE'S BOOKMARKS, OFF THE CLIPBOARD AND INTO THE FILE THEY BELONG IN - the other
        /// half of <see cref="Copy"/>, and what a save that has changed hands needs on the receiving
        /// machine.
        ///
        /// Which file that is comes from the paste itself (<see cref="BookmarkImport"/>), never from
        /// which campaign is being played: the whole point is that the bookmarks usually arrive
        /// BEFORE the save they go with is ever loaded. So the row is drawn on the main menu too, and
        /// the three landings it can have - this campaign, another campaign, no game at all - differ
        /// only in what the player is told about when they will see them.
        ///
        /// Everything the player is told goes in the game's own message box rather than being
        /// spoken, because a count is a fact they may want to read twice.
        ///
        /// TWO STEPS (owner ruling 2026-09-18): the paste is read and the player is ASKED - how many
        /// bookmarks, and where they would go - in a box with a Cancel on it, and nothing touches
        /// the disk until they say Ok. The write then runs from the pump (<see cref="Tick"/>), like
        /// the page rebuild and for the same reason: not from inside the box's own button.
        /// </summary>
        private static void Import()
        {
            try
            {
                BookmarkImport import;
                switch (BookmarkImport.Read(Clipboard(), out import))
                {
                    case BookmarkImportReading.Empty:
                        _box = ModStrings.Get(ModStrings.ModSettingsBookmarksImportEmpty);
                        return;
                    case BookmarkImportReading.NotBookmarks:
                        _box = ModStrings.Get(ModStrings.ModSettingsBookmarksImportNotBookmarks);
                        return;
                }

                _pending = import;
                _ask = Question(import.Campaign == MapBookmarkStore.Campaign, import.Count);
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: reading the clipboard threw: " + e);
                _box = ModStrings.Get(ModStrings.ModSettingsBookmarksImportFailed);
            }
        }

        /// <summary>The player's answer to the question. Ok hands the paste to the pump to write;
        /// anything else drops it, and nothing is said - a cancelled import has nothing to report.
        /// </summary>
        private static void Confirmed(object sender, MessageBoxResultEventArgs e)
        {
            BookmarkImport pending = _pending;
            _pending = null;
            if (pending != null && e != null && e.Result == MessageBoxResult.Ok)
            {
                _confirmed = pending;
            }
        }

        /// <summary>Put a paste the player said Ok to where it belongs, and tell them where it
        /// landed.</summary>
        private static void Write(BookmarkImport import)
        {
            try
            {
                string folder = MapBookmarkStore.Folder;
                string name = import.FileName;
                if (folder == null || name == null)
                {
                    _box = ModStrings.Get(ModStrings.ModSettingsBookmarksImportFailed);
                    return;
                }

                string path = System.IO.Path.Combine(folder, name);
                SettingsFile target = SettingsFileOnDisk.Read(path, "bookmarks");
                bool playing = import.Campaign == MapBookmarkStore.Campaign;
                int imported;
                if (playing)
                {
                    // The tile the player hears is measured from their own empire's home, so the
                    // one-place-one-slot rule can only be asked in full for the campaign being
                    // played (MapBookmarks.SetAloneExactly is what the other two landings get).
                    GalaxyPosition origin = GalaxyCoordinates.Origin();
                    imported = import.MergeInto(target, true, origin.X, origin.Y);
                }
                else
                {
                    imported = import.MergeInto(target, false, 0f, 0f);
                }

                if (!SettingsFileOnDisk.Write(path, target, "bookmarks"))
                {
                    _box = ModStrings.Get(ModStrings.ModSettingsBookmarksImportFailed);
                    return;
                }

                if (playing)
                {
                    // The map's own digits read the store, not the file, so a campaign being played
                    // takes its new slots now rather than on the next load.
                    MapBookmarkStore.Reload();
                }

                _box = Landed(playing, imported);
                _refill = true;
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: importing the clipboard threw: " + e);
                _box = ModStrings.Get(ModStrings.ModSettingsBookmarksImportFailed);
            }
        }

        /// <summary>What an import that landed is told the player, by where the bookmarks went.
        /// </summary>
        private static string Landed(bool playing, int imported)
        {
            if (playing)
            {
                return ModStrings.Plural(
                    ModStrings.ModSettingsBookmarksImportedOne,
                    ModStrings.ModSettingsBookmarksImportedMany,
                    imported
                );
            }

            if (InGame())
            {
                return ModStrings.Plural(
                    ModStrings.ModSettingsBookmarksImportedOtherOne,
                    ModStrings.ModSettingsBookmarksImportedOtherMany,
                    imported
                );
            }

            return ModStrings.Plural(
                ModStrings.ModSettingsBookmarksImportedNoGameOne,
                ModStrings.ModSettingsBookmarksImportedNoGameMany,
                imported
            );
        }

        /// <summary>What the player is asked before a paste is written, by where it would go - the
        /// same three situations <see cref="Landed"/> tells, as a question.</summary>
        private static string Question(bool playing, int count)
        {
            if (playing)
            {
                return ModStrings.Plural(
                    ModStrings.ModSettingsBookmarksImportAskOne,
                    ModStrings.ModSettingsBookmarksImportAskMany,
                    count
                );
            }

            if (InGame())
            {
                return ModStrings.Plural(
                    ModStrings.ModSettingsBookmarksImportAskOtherOne,
                    ModStrings.ModSettingsBookmarksImportAskOtherMany,
                    count
                );
            }

            return ModStrings.Plural(
                ModStrings.ModSettingsBookmarksImportAskNoGameOne,
                ModStrings.ModSettingsBookmarksImportAskNoGameMany,
                count
            );
        }

        /// <summary>Whatever is on the clipboard, or nothing at all where the desktop will not say.
        /// </summary>
        private static string Clipboard()
        {
            try
            {
                return UnityEngine.GUIUtility.systemCopyBuffer;
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: reading the clipboard threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The game's own message box, with one button on it - the same box the scanner's editor
        /// says a name clash in (<see cref="ScannerEditor"/>), and for the same reason: the game has
        /// no INFORMATIVE title of its own, so the box wears the confirmation heading and answers
        /// with Ok alone, an empty cancel caption being how the window hides its second button.
        /// </summary>
        private static void Box(string message)
        {
            try
            {
                Gui.GuiService.ShowMessage(
                    message,
                    MessageBoxType.INFORMATIVE,
                    null,
                    "%MessageBoxConfirmationTitle",
                    "%MessageBoxOkTitle",
                    string.Empty
                );
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: the import's message box would not open: " + e);
            }
        }

        /// <summary>The same box with BOTH buttons on it, for the question an import asks first:
        /// Ok writes the paste, Cancel drops it (<see cref="Confirmed"/>).</summary>
        private static void Ask(string question)
        {
            try
            {
                Gui.GuiService.ShowMessage(
                    question,
                    MessageBoxType.INFORMATIVE,
                    Confirmed,
                    "%MessageBoxConfirmationTitle",
                    "%MessageBoxOkTitle",
                    "%MessageBoxCancelTitle"
                );
            }
            catch (Exception e)
            {
                _pending = null;
                Log.Warn("bookmarks: the import's question box would not open: " + e);
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
        private static string _box;
        private static bool _refill;

        /// <summary>The paste the question is about, held from the press to the answer; the answer,
        /// held from the box's button to the pump; and the question itself, unasked until the pump.
        /// </summary>
        private static BookmarkImport _pending;
        private static BookmarkImport _confirmed;
        private static string _ask;
    }
}
