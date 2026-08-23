using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using Amplitude.Unity.Gui;

namespace ES2Access.UI
{
    /// <summary>
    /// Looking a game window up without telling the game something is wrong.
    ///
    /// The engine's by-NAME lookup has no quiet overload: <c>GuiManager.GetWindow(StaticString)</c>
    /// logs an Error for every miss, and the game forwards every Error - with its stack - to
    /// Amplitude's telemetry and into the session's diagnostics HTML
    /// (<c>PrismGameManager.MessageLoggedEventHandler</c>). A mod screen that asks "is my window up?"
    /// once a tick therefore writes hundreds of error reports about itself while the window registry
    /// is still filling, which is a cost the player pays in disk and in upload for a question with a
    /// perfectly ordinary answer. The by-TYPE lookup does have a quiet overload
    /// (<c>GetWindow&lt;T&gt;(false)</c>) and every caller in this mod uses it; this class is for the
    /// two windows that can only be told apart by name.
    ///
    /// So the registry is read directly. The dictionary it lives in is resolved once per
    /// <c>GuiManager</c> - the field lookup is the expensive half - and the windows themselves are
    /// never cached, because a window that has been destroyed between scenes would go on being
    /// answered.
    /// </summary>
    public static class GameWindows
    {
        /// <summary>The registered window with this name, or null - never an error in the game's log.
        /// </summary>
        public static GuiWindow Named(string name)
        {
            try
            {
                if (!Gui.GuiServiceAvailable || string.IsNullOrEmpty(name))
                {
                    return null;
                }

                GuiManager gui = Gui.GuiService as GuiManager;
                Dictionary<StaticString, GuiWindow> registry = Registry(gui);
                GuiWindow window;
                return registry != null && registry.TryGetValue(new StaticString(name), out window)
                    ? window
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the game has finished registering its windows. Below this every lookup is
        /// a miss, and a state probe that answers from one is describing the loading screen rather
        /// than the game.</summary>
        public static bool Loaded()
        {
            try
            {
                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null && gui.GuiWindowsLoaded;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Let go of the resolved registry - the mod is going away.</summary>
        public static void Shutdown()
        {
            _for = null;
            _registry = null;
        }

        private static GuiManager _for;
        private static Dictionary<StaticString, GuiWindow> _registry;
        private static FieldInfo _field;

        private static Dictionary<StaticString, GuiWindow> Registry(GuiManager gui)
        {
            if (gui == null)
            {
                return null;
            }

            if (ReferenceEquals(gui, _for) && _registry != null)
            {
                return _registry;
            }

            if (_field == null)
            {
                _field = typeof(GuiManager).GetField(
                    "guiWindowsByName",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
            }

            _for = gui;
            _registry =
                _field == null
                    ? null
                    : _field.GetValue(gui) as Dictionary<StaticString, GuiWindow>;
            return _registry;
        }
    }
}
