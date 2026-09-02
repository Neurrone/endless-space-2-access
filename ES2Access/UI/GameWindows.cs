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
        public static Amplitude.Unity.Gui.GuiWindow Named(string name)
        {
            try
            {
                if (!Gui.GuiServiceAvailable || string.IsNullOrEmpty(name))
                {
                    return null;
                }

                GuiManager gui = Gui.GuiService as GuiManager;
                Dictionary<StaticString, Amplitude.Unity.Gui.GuiWindow> registry = Registry(gui);
                Amplitude.Unity.Gui.GuiWindow window;
                return registry != null && registry.TryGetValue(new StaticString(name), out window)
                    ? window
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The registered window of this type whatever state it is in, or null - the quiet lookup,
        /// which about thirty screens wrote a private copy of.
        ///
        /// Quiet is the reason it is worth a helper at all. The by-NAME lookup logs an Error per miss
        /// and the game forwards Errors, with their stacks, to Amplitude's telemetry and into the
        /// session's diagnostics HTML (see this class's own summary), so a screen asking once a tick
        /// whether its window is up writes hundreds of error reports about itself while the registry is
        /// still filling. The by-TYPE lookup has the quiet overload; this wraps it so no screen has to
        /// remember the <c>false</c>, and swallows the throw a lookup makes before the service exists.
        ///
        /// Any state on purpose: a caller reading a window the game has put away - restoring a page,
        /// reporting on it, patching one of its handlers - wants the object, not the visibility.
        /// <see cref="Shown{T}"/> is the other question, and the copies that conflated the two are why
        /// both are named.
        /// </summary>
        public static T Of<T>()
            where T : Amplitude.Unity.Gui.GuiWindow
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService.GetWindow<T>(false) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The registered window of this type while the game is SHOWING it, else null - the
        /// form a screen's <c>IsActive</c> asks for. Same quiet lookup as <see cref="Of{T}"/>; the
        /// <c>Shown</c> test is folded in because the copies that left it out then had it re-tested by
        /// hand at each call site, and two of them forgot.</summary>
        public static T Shown<T>()
            where T : Amplitude.Unity.Gui.GuiWindow
        {
            try
            {
                T window = Of<T>();
                return window != null && window.Shown ? window : null;
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
        private static Dictionary<StaticString, Amplitude.Unity.Gui.GuiWindow> _registry;
        private static FieldInfo _field;

        private static Dictionary<StaticString, Amplitude.Unity.Gui.GuiWindow> Registry(GuiManager gui)
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
                    : _field.GetValue(gui) as Dictionary<StaticString, Amplitude.Unity.Gui.GuiWindow>;
            return _registry;
        }
    }
}
