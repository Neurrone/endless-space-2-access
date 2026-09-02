using System;
using System.Globalization;
using System.IO;
using Amplitude.Unity.Framework;
using ES2Access.Core.Bookmarks;
using ES2Access.Core.Settings;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI.Bookmarks
{
    /// <summary>
    /// WHOSE bookmarks these are, and where they are kept - the half of
    /// <see cref="MapBookmarks"/> that had to know about a running game.
    ///
    /// A campaign is identified by the GUID the game mints for it the first time it is saved and
    /// then carries verbatim through every save, save-as and load
    /// (<c>GameManager.UpdateGameSaveDescriptor</c>) - so a save and every descendant of it share one
    /// set of bookmarks, which is what a player means by "my bookmarks in this game". One file per
    /// campaign, <c>bookmarks\&lt;faction&gt;-&lt;guid&gt;.cfg</c> beside the plugin, in the same flat
    /// format the settings file uses and through the same disk half
    /// (<see cref="SettingsFileOnDisk"/>) - deliberately NOT <c>settings.cfg</c>, whose write
    /// lifecycle belongs to the options window.
    ///
    /// The GUID is what IDENTIFIES the campaign; the faction in front of it is there so that a folder
    /// of these files can be read by a person. It is the faction's INTERNAL name
    /// (<c>Faction.Name</c>) and never the localized one, so that switching the game's language does
    /// not fork one campaign's bookmarks into two files, and it is put through
    /// <see cref="FileNameText.Safe"/> because a custom faction's name is whatever the player typed
    /// into the editor. Where nothing survives that - a faction named entirely in punctuation, or no
    /// faction at all yet - the file is the bare <c>&lt;guid&gt;.cfg</c> it always was. The file also
    /// opens with a header comment naming the game in the player's own language
    /// (<see cref="Stamp"/>), refreshed on every write.
    ///
    /// Written on every set, because there is no moment a player would recognise as "saving my
    /// bookmarks"; the file is ten short lines and the write is the same one the settings file makes.
    ///
    /// A campaign that has never been saved has no GUID at all. Its bookmarks live in memory until
    /// the first save (autosave included) mints one, and are written then - and the file is only
    /// written over if the campaign does not already have one, so a save being LOADED can never have
    /// its bookmarks blanked by the empty set the mod was holding while the game came up.
    ///
    /// Which campaign is being played is polled rather than hooked: the GUID appears at the first
    /// save AND at a load, and one question asked once a frame answers both without a patch on
    /// either path. All the state here is this assembly's and goes with a hot reload
    /// (<see cref="Reset"/>); a campaign with a file re-reads it, and one without loses whatever was
    /// held in memory.
    /// </summary>
    internal static class MapBookmarkStore
    {
        private const string FolderName = "bookmarks";

        private static readonly MapBookmarks Slots = new MapBookmarks();

        /// <summary>The file the current campaign's bookmarks were read from, kept so that a write
        /// puts back every line the mod does not own - a comment, a key a newer build wrote.</summary>
        private static SettingsFile _file;

        private static string _directory;
        private static object _game;
        private static string _campaign;
        private static Guid _campaignGuid;
        private static int _frame = -1;

        /// <summary>The ten slots of whichever campaign is being played - empty while none is.
        /// </summary>
        public static MapBookmarks Bookmarks
        {
            get { return Slots; }
        }

        /// <summary>Which campaign the slots belong to, or null for one the player has never saved.
        /// For a probe: nothing in the mod branches on the value.</summary>
        public static string Campaign
        {
            get { return _campaign; }
        }

        /// <summary>
        /// Where this campaign's file is, or null while there is no campaign or no plugin directory
        /// to put it in.
        ///
        /// The name is worked out fresh each time rather than kept, and is NULL - "ask me again" -
        /// for as long as the game has a campaign but no player empire yet, which is what a save
        /// coming up looks like from here. A path answered during that window would name the file
        /// without its faction, and the campaign's bookmarks would be read from a file that does not
        /// exist and then written to one that does.
        /// </summary>
        public static string Path
        {
            get
            {
                string faction;
                if (
                    _campaign == null
                    || string.IsNullOrEmpty(_directory)
                    || !FactionPart(out faction)
                )
                {
                    return null;
                }

                return System.IO.Path.Combine(
                    System.IO.Path.Combine(_directory, FolderName),
                    (faction.Length == 0 ? _campaign : faction + "-" + _campaign) + ".cfg"
                );
            }
        }

        /// <summary>
        /// The folder every campaign's file is kept in, or null while the mod has not been told
        /// where the plugin lives.
        ///
        /// It is answered whether or not the folder exists: nothing creates it until the first
        /// write, so a fresh install has a path here and nothing on disk under it.
        /// </summary>
        public static string Folder
        {
            get
            {
                return string.IsNullOrEmpty(_directory)
                    ? null
                    : System.IO.Path.Combine(_directory, FolderName);
            }
        }

        /// <summary>Whether THIS campaign's file is on disk - false for a campaign the player has
        /// never saved, and false for one whose bookmarks have never been written.</summary>
        public static bool Saved
        {
            get { return Exists(Path); }
        }

        /// <summary>Whether the folder holds a bookmark file of ANY campaign - which is what decides
        /// whether there is anything in it for a player to be sent to.</summary>
        public static bool FolderHoldsBookmarks
        {
            get
            {
                try
                {
                    string folder = Folder;
                    return folder != null
                        && Directory.Exists(folder)
                        && Directory.GetFiles(folder, "*.cfg").Length > 0;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>How much of the faction's internal name the file's own name may carry. Long
        /// enough for any name a person would recognise it by, short enough that the whole path
        /// stays comfortable beside a plugin directory that is already deep.</summary>
        private const int FactionNameLimit = 48;

        /// <summary>
        /// The faction part of the file's name, and whether it can be answered at all.
        ///
        /// False is "not yet" - there is no player empire, so the game is still coming up. An empty
        /// part with TRUE is settled: a faction the game gives no name, or a custom one whose name
        /// has nothing in it a file name may keep, both of which fall back to the bare GUID.
        /// </summary>
        private static bool FactionPart(out string part)
        {
            part = string.Empty;
            try
            {
                Empire empire = Gui.PlayerEmpire;
                if (empire == null)
                {
                    return false;
                }

                Faction faction = empire.Faction;
                if (faction != null)
                {
                    part = FileNameText.Safe(faction.Name.ToString(), FactionNameLimit);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Told where the plugin lives, at mod start.</summary>
        public static void Start(string pluginDirectory)
        {
            _directory = pluginDirectory;
            _game = null;
            _campaign = null;
            _file = null;
            _frame = -1;
            Slots.ReadFrom(null);
        }

        /// <summary>Notice the campaign changing - a save loaded, a new game begun, the player back
        /// at the menu - and the first save of a campaign minting the GUID its file is named after.
        /// Once a frame, from the pump.</summary>
        public static void Tick()
        {
            try
            {
                int frame = UnityEngine.Time.frameCount;
                if (frame == _frame)
                {
                    return;
                }

                _frame = frame;
                object game = Game();
                // Compared as a raw Guid so the once-a-frame question allocates nothing; the
                // string form is only minted when the answer changes.
                Guid campaign = game == null ? Guid.Empty : CampaignGuid();
                if (ReferenceEquals(game, _game) && campaign == _campaignGuid)
                {
                    // Nothing has changed - but the file may still be outstanding, because the name
                    // it goes under needs the player empire and a campaign is identified before one
                    // exists (<see cref="Path"/>). A campaign whose file has neither been read nor
                    // written yet is asked for again, once a frame, until it can be named.
                    if (_campaign != null && _file == null)
                    {
                        Load();
                    }

                    return;
                }

                _campaignGuid = campaign;
                string name = campaign == Guid.Empty ? null : campaign.ToString("N");
                if (!ReferenceEquals(game, _game))
                {
                    // A different campaign is being played: the one before it is forgotten whole,
                    // and this one is whatever its own file says (nothing at all, for a game nobody
                    // has saved yet).
                    _game = game;
                    _campaign = name;
                    _file = null;
                    Slots.ReadFrom(null);
                    if (name != null)
                    {
                        Load();
                    }

                    return;
                }

                // Same game, and a GUID where there was none: its first save has just happened. The
                // bookmarks made before there was a file to put them in get one now - unless the
                // campaign turns out to have a file already, which is the game having told us its
                // GUID late rather than a first save at all, and there the file is the truth.
                _campaign = name;
                _file = null;
                if (name == null)
                {
                    return;
                }

                string path = Path;
                if (Slots.Count > 0 && !Exists(path))
                {
                    _file = SettingsFileOnDisk.Read(path, "bookmarks");
                    Save();
                }
                else
                {
                    Load();
                }
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: noticing which campaign is being played threw: " + e);
            }
        }

        /// <summary>
        /// Put a bookmark in a slot and write the file, if this campaign has one yet.
        ///
        /// One place, one slot: a slot that already held this place is emptied in the same breath and
        /// the file is written ONCE, so the two changes reach the disk together and no crash can leave
        /// a place owned twice or not at all (<see cref="MapBookmarks.SetAlone"/> decides what "this
        /// place" means; the tile it measures is measured from home, which is why the origin comes in
        /// from here).
        ///
        /// Answers the slot that was emptied, or <c>'\0'</c> - what the gesture needs to tell the
        /// player their other bookmark has moved here.
        /// </summary>
        public static char Set(char digit, MapBookmark bookmark)
        {
            GalaxyPosition origin = GalaxyCoordinates.Origin();
            char emptied = Slots.SetAlone(digit, bookmark, origin.X, origin.Y);
            Save();
            return emptied;
        }

        /// <summary>Let go of everything - mod teardown. A campaign with a file will read it again;
        /// one the player has never saved has nothing to read, and its bookmarks are gone.</summary>
        public static void Reset()
        {
            Slots.ReadFrom(null);
            _file = null;
            _directory = null;
            _game = null;
            _campaign = null;
            _campaignGuid = Guid.Empty;
            _frame = -1;
        }

        private static void Load()
        {
            string path = Path;
            if (path == null)
            {
                return;
            }

            _file = SettingsFileOnDisk.Read(path, "bookmarks");
            Slots.ReadFrom(_file);
        }

        private static void Save()
        {
            string path = Path;
            if (path == null)
            {
                return;
            }

            if (_file == null)
            {
                _file = SettingsFileOnDisk.Read(path, "bookmarks");
            }

            Slots.WriteTo(_file);
            Stamp(_file);
            SettingsFileOnDisk.Write(path, _file, "bookmarks");
        }

        /// <summary>
        /// Put the header comment on the file - which game these bookmarks belong to, in the player's
        /// own language, for whoever opens the folder.
        ///
        /// Refreshed on every write rather than written once, so the turn it names is the turn the
        /// bookmarks were last touched on. It says nothing the mod READS: the campaign is identified
        /// by the GUID in the file's name, and this line exists only so that a person can tell one
        /// file from another. Where any of the three parts is missing the header is left exactly as
        /// it is - half a sentence would be worse than the one already there, or than none.
        /// </summary>
        private static void Stamp(SettingsFile file)
        {
            try
            {
                Empire empire = Gui.PlayerEmpire;
                Faction faction = empire == null ? null : empire.Faction;
                GameManager manager =
                    Services.GetService<IGameSerializationService>() as GameManager;
                GameSaveDescriptor descriptor = manager == null ? null : manager.GameSaveDescriptor;
                if (
                    faction == null
                    || descriptor == null
                    || string.IsNullOrEmpty(faction.LocalizedName)
                    || string.IsNullOrEmpty(descriptor.LocalizedTitle)
                )
                {
                    return;
                }

                file.SetHeaderComment(
                    ModStrings.Format(
                        ModStrings.GalaxyBookmarkFileHeader,
                        faction.LocalizedName,
                        descriptor.LocalizedTitle,
                        descriptor.Turn.ToString(CultureInfo.InvariantCulture)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: naming the file's own header threw: " + e);
            }
        }

        private static bool Exists(string path)
        {
            try
            {
                return path != null && File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game being played, as an identity to compare - a save loaded over this one is
        /// a different game with the same class, so nothing but the reference tells them apart (the
        /// test <see cref="ConstellationMap"/> makes for the same reason).</summary>
        private static object Game()
        {
            try
            {
                return Gui.Game;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The campaign's own GUID, or <see cref="Guid.Empty"/> while the game has never been saved.
        ///
        /// It hangs off the game manager's save descriptor, which the manager creates and stamps with
        /// a fresh GUID the first time the game is written and never re-stamps
        /// (<c>GameManager.UpdateGameSaveDescriptor</c>) - so it survives save-as, autosave and every
        /// load of any of them. The manager is reached as the serialization service it publishes,
        /// which is the one name for it that does not need the type resolved from the game assembly.
        /// </summary>
        private static Guid CampaignGuid()
        {
            try
            {
                GameManager manager =
                    Services.GetService<IGameSerializationService>() as GameManager;
                GameSaveDescriptor descriptor =
                    manager == null ? null : manager.GameSaveDescriptor;
                return descriptor == null ? Guid.Empty : descriptor.GUID;
            }
            catch (Exception)
            {
                return Guid.Empty;
            }
        }
    }
}
