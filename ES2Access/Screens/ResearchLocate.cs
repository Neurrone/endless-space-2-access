using System;
using ES2Access.UI;
using HarmonyLib;

namespace ES2Access.Screens
{
    /// <summary>
    /// The technology the GAME has just taken the player to, remembered until the mod's cursor can be
    /// put on it.
    ///
    /// The game has one way of saying "the thing you are missing is over there": Control and a click on
    /// a button it has switched on only so it can explain itself (<c>GuiButtonHint.ActivateHint</c> -
    /// the colonize button, the marketplace's buy line) opens the wheel already looking at the
    /// technology, and a technology-unlocked notification does the same. All of them call
    /// <c>TechnologyScreen.FocusTechnology</c>, which zooms the viewport onto the dot and pulses it -
    /// and leaves nothing behind afterwards: with the page already up it acts at once, and with the page
    /// closed it stashes the technology in a private field that its own show coroutine consumes the
    /// moment the window appears (<c>TechnologyScreen.cs</c> :154-167, :776-790). So there is no state
    /// to read a frame later, and the only place to hear about it is the call itself.
    ///
    /// Which is what this is: the game's own locate, remembered, so that the cursor lands where the
    /// viewport is looking instead of wherever the player last left it (the wheel keeps its place on
    /// purpose - <see cref="Screen.KeepStateOnPop"/> - which is right for a page reopened and wrong for
    /// a page opened AT something).
    ///
    /// One shot: the technology is taken by the first read and gone, and the research screen forgets an
    /// unclaimed one when it closes, so a locate that never got a page to land on cannot fire on some
    /// unrelated visit later.
    /// </summary>
    internal static class ResearchLocate
    {
        private static readonly ModPatch Patches = new ModPatch(
            "researchlocate",
            "the game's locate-a-technology call"
        );

        private static GuiTechnology2 _wanted;

        public static void Install()
        {
            Patches.Install(
                patch =>
                    patch.Postfix(
                        AccessTools.Method(
                            typeof(TechnologyScreen),
                            "FocusTechnology",
                            new[] { typeof(GuiTechnology2) }
                        ),
                        typeof(ResearchLocate),
                        "Remember"
                    )
            );
        }

        public static void Remove()
        {
            Patches.Remove();
            _wanted = null;
        }

        /// <summary>The technology the game last asked to be looked at, and it is nobody's after that.
        /// </summary>
        public static GuiTechnology2 Take()
        {
            GuiTechnology2 wanted = _wanted;
            _wanted = null;
            return wanted;
        }

        /// <summary>Drop a locate nobody came to collect - the page it was meant for has closed.
        /// </summary>
        public static void Forget()
        {
            _wanted = null;
        }

        private static void Remember(GuiTechnology2 guiTechnology)
        {
            try
            {
                _wanted = guiTechnology;
            }
            catch (Exception e)
            {
                Patches.Report("remembering the technology the game located threw", e);
            }
        }
    }
}
