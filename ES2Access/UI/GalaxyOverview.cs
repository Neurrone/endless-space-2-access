using System;
using Amplitude.Unity.Framework;
using ES2Access.Core.Map;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The galaxy as a whole, said on request: what it was generated as, how far across it is, and
    /// where its middle lies from home (<see cref="GalaxyMapText"/> writes the sentence).
    ///
    /// The engine half of that reading. The shape and the size are the SETUP's own two settings,
    /// which the game keeps as lobby data and shows the player in the game's own words on the lobby
    /// screen (<c>GuiLobbyInfo.GalaxyShape</c>); the same two localisation keys are used here, so the
    /// gesture says whatever the new-game screen said. The extent and the middle are measured off the
    /// galaxy's outline, and it is the outline <see cref="ProbeContext"/> already caches - one hull
    /// per game, so a keypress costs an array read.
    ///
    /// Nothing at all is the answer whenever a part is missing: a game with no home system yet, or a
    /// galaxy whose middle rounds onto home. The sentence ends on where the middle lies FROM home,
    /// and no wording has been chosen for either case, so the gesture stays silent rather than
    /// speaking half of one.
    /// </summary>
    internal static class GalaxyOverview
    {
        /// <summary>The lobby settings the galaxy was generated from. Lowercase: these are the keys
        /// the session really carries, and the CamelCase constants beside them
        /// (<c>GameSettingsConstants</c>) read back empty.</summary>
        private const string ShapeSetting = "galaxyshape";
        private const string SizeSetting = "galaxysize";

        /// <summary>How the lobby screen turns one of those settings into a word the player reads
        /// (<c>GuiLobbyInfo.GalaxyShapeLoc</c>, <c>GuiLobbyInfo.GalaxySizeLoc</c>).</summary>
        private const string ShapeTitle = "%SettingGalaxyShape{0}Title";
        private const string SizeTitle = "%SettingGalaxySize{0}Title";

        /// <summary>The whole overview in one sentence, or null when there is nothing honest to say.
        /// </summary>
        public static string Sentence()
        {
            try
            {
                StarSystemNode home = HomeSystemNode();
                ConvexHull galaxy = ProbeContext.Outline();
                if (home == null || galaxy == null || galaxy.Count == 0)
                {
                    return null;
                }

                return GalaxyMapText.Summary(
                    Setting(ShapeSetting, ShapeTitle),
                    Setting(SizeSetting, SizeTitle),
                    galaxy,
                    new MapPoint(home.GalaxyPosition.X, home.GalaxyPosition.Y),
                    AgeText.Clean(home.LocalizedName)
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the galaxy's own overview threw: " + e);
                return null;
            }
        }

        /// <summary>One setup setting as the game's own word for it. The raw value is a name the
        /// player never sees ("Spiral2"), and where the game has no word for it the raw value is
        /// better than an unresolved key.</summary>
        private static string Setting(string setting, string titleFormat)
        {
            Amplitude.Unity.Session.ISessionService sessions =
                Services.GetService<Amplitude.Unity.Session.ISessionService>();
            Amplitude.Unity.Session.Session session = sessions == null ? null : sessions.Session;
            string value = session == null ? null : session.GetLobbyData<string>(setting);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            string localized = AgeText.Clean(Gui.Localize(string.Format(titleFormat, value)));
            return string.IsNullOrEmpty(localized) || localized[0] == '%' ? value : localized;
        }

        private static StarSystemNode HomeSystemNode()
        {
            Empire empire = Gui.PlayerEmpire;
            DepartmentOfTheInterior interior =
                empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
            return interior == null ? null : interior.HomeSystemNode;
        }
    }
}
