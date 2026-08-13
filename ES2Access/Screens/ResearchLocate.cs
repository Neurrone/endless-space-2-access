using System;
using System.Reflection;
using ES2Access.Core.Util;
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
        private static Harmony _harmony;
        private static GuiTechnology2 _wanted;
        private static bool _reportedFailure;

        public static void Install()
        {
            Remove();

            // A unique id per load, for the reason GameKeyStandDown documents: a fixed id lets the
            // unpatch of the assembly a reload replaced strip this load's patches.
            Harmony harmony = new Harmony(
                "endless.space2.access.researchlocate." + Guid.NewGuid().ToString("N")
            );

            try
            {
                MethodInfo focus = AccessTools.Method(
                    typeof(TechnologyScreen),
                    "FocusTechnology",
                    new[] { typeof(GuiTechnology2) }
                );
                if (focus == null)
                {
                    throw new MissingMethodException(
                        typeof(TechnologyScreen).FullName,
                        "FocusTechnology"
                    );
                }

                harmony.Patch(
                    focus,
                    postfix: new HarmonyMethod(
                        typeof(ResearchLocate).GetMethod(
                            "Remember",
                            BindingFlags.Static | BindingFlags.NonPublic
                        )
                    )
                );
                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, Control-clicking a hint still opens the wheel looking at the technology;
                // what is lost is the cursor following the view. Worth saying and not worth refusing to
                // start over.
                Log.Error("the game's locate-a-technology call could not be patched: " + e);
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception undo)
                {
                    Log.Warn("and the partial patch could not be undone: " + undo.Message);
                }
            }
        }

        public static void Remove()
        {
            Harmony harmony = _harmony;
            _harmony = null;
            _wanted = null;
            _reportedFailure = false;
            if (harmony == null)
            {
                return;
            }

            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception e)
            {
                Log.Error("the game's locate-a-technology call could not be unpatched: " + e);
            }
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
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    Log.Warn("remembering the technology the game located threw: " + e);
                }
            }
        }
    }
}
