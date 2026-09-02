using ES2Access.Core.Settings;
using ES2Access.Core.UI;
using ES2Access.ES2.UI;

namespace ES2Access.UI.Settings
{
    /// <summary>
    /// THE THREE CUSTOM SCANNER CATEGORIES, ON DISK - the settings file's half of
    /// <see cref="ScannerCustomSlots"/>.
    ///
    /// One key per slot (<c>scanner.custom.1</c> … <c>scanner.custom.3</c>), each holding one
    /// encoded category (<see cref="ScannerCustomCodec"/>) and removed entirely where the slot is
    /// empty - so a file that has never had one says nothing about them, and a downgrade cannot
    /// invent one.
    ///
    /// The decoded slots are held here rather than re-read per press: the scanner asks for them on
    /// every keystroke and decoding three strings sixty times a second is the kind of cost the
    /// scanner has spent a stage removing. The cache is a mod-assembly static, so a hot reload drops
    /// it and the next ask decodes the file again.
    ///
    /// <para>WHERE THE SAVE HAPPENS, and the seam the editor will move: <see cref="Set"/> writes the
    /// file immediately, which is what makes <c>/eval</c> and the test recipes able to configure a
    /// slot and reload. The editor sub-screen (stage 4) holds its edits in a
    /// <see cref="ScannerCustomSlots.Copy"/> and commits them through <see cref="Replace"/> when the
    /// options window's Apply lands - the same Apply-to-persist the rest of the mod's settings have.
    /// Nothing else here changes when that arrives.</para>
    /// </summary>
    public static class ScannerCustomSettings
    {
        private const string SettingPrefix = "scanner.custom.";

        private static ScannerCustomSlots _slots;

        /// <summary>The three slots as the file has them. The same instance every call, so a caller
        /// may mutate a category in place and persist it with <see cref="Save"/>.</summary>
        public static ScannerCustomSlots Slots
        {
            get { return _slots ?? (_slots = Read()); }
        }

        /// <summary>What is in one slot, or null where it is empty.</summary>
        public static ScannerCustomCategory Slot(int slot)
        {
            return Slots.Slot(slot);
        }

        /// <summary>Put a category in a slot - or null to clear it - and write the file.</summary>
        public static bool Set(int slot, ScannerCustomCategory category)
        {
            if (!Slots.Set(slot, category))
            {
                return false;
            }

            Save();
            return true;
        }

        public static bool Clear(int slot)
        {
            return Set(slot, null);
        }

        /// <summary>Take a whole edited set - what the editor's Apply hands over - and write it.
        /// </summary>
        public static void Replace(ScannerCustomSlots slots)
        {
            _slots = slots ?? new ScannerCustomSlots();
            Save();
        }

        /// <summary>Write the three slots into the settings file and save it.</summary>
        public static void Save()
        {
            SettingsFile file = ModSettings.File;
            for (int i = 0; i < ScannerCustomSlots.Count; i++)
            {
                file.Set(SettingPrefix + (i + 1), ScannerCustomCodec.Encode(Slots.Slot(i)));
            }

            ModSettings.Save();
        }

        /// <summary>Drop the cache, so the next ask reads the file again - mod teardown, and a test
        /// that has written the file behind this.</summary>
        public static void Reset()
        {
            _slots = null;
        }

        private static ScannerCustomSlots Read()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            SettingsFile file = ModSettings.File;
            for (int i = 0; i < ScannerCustomSlots.Count; i++)
            {
                slots.Set(i, ScannerCustomCodec.Decode(file.Get(SettingPrefix + (i + 1))));
            }

            return slots;
        }
    }
}
