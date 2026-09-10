using ES2Access.Core.Settings;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI.Settings
{
    /// <summary>
    /// WHETHER THE USAGE HINTS ON A CONTROL ARE READ OUT when focus lands on it, on disk and in the
    /// one field the readout of every focused node asks (<see cref="NodeHints.Reading"/>).
    ///
    /// ON unless the settings file says otherwise (owner ruling 2026-09-10). A player arriving at
    /// this mod does not know the gestures, and the hints are how they learn them; turning them off
    /// is what somebody asks for once they do. So the key is written only to say NO: an absent key -
    /// a settings file that has never held it - is on, and a value this build has no name for is on
    /// too, which is what lets a later answer be added without migrating anything.
    ///
    /// NOT CACHED HERE, because Core holds the value itself: the policy static IS the cache, so the
    /// file is read once at mod start (<see cref="Apply"/>) and again after a hot reload, and never
    /// on the readout path. Teardown puts the default back with the rest of the hint state
    /// (<see cref="NodeHints.Reset"/>).
    /// </summary>
    public static class HintSettings
    {
        private const string SettingKey = "hints.read";

        /// <summary>Read what the player chose into the policy Core reads. Mod start, once the
        /// settings file is loaded.</summary>
        public static void Apply()
        {
            NodeHints.Reading = NodeHints.Parse(ModSettings.File.Get(SettingKey));
        }

        /// <summary>Whether a control's usage hints are spoken on arrival.</summary>
        public static HintReading Reading
        {
            get { return NodeHints.Reading; }
            set
            {
                NodeHints.Reading = value;
                ModSettings.File.Set(SettingKey, NodeHints.Stored(value));
            }
        }
    }
}
