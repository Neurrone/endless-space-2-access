using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using ES2Access.UI.Settings;
using UnityEngine;
using GameGui = Amplitude.Unity.Gui;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE MOD'S SETTINGS WINDOW - the game's own options modal, cloned once and filled with the
    /// mod's categories (<see cref="ModOptionsWindow"/>).
    ///
    /// Why a clone rather than a seventh tab on the game's window: the mod's settings are not the
    /// game's settings, and mixing them would put mod rows behind Apply/Cancel semantics shared with
    /// the video mode. Why the game's window rather than a mod-drawn page: every row type, the tab
    /// bar, the button bar, the confirmation boxes and the skin swap already exist and are already
    /// navigable, so the mod's options screen reads this window with no changes at all.
    ///
    /// Five members of the engine are private and load-bearing here, each measured 2026-08-23:
    /// <c>GuiWindow.Initialize</c> (internal), the <c>Loaded</c> and <c>Name</c> setters (internal),
    /// the <c>GuiWindowsStack</c> getter (internal), and the two window REGISTRIES - the stack's
    /// own <c>guiWindows</c> and the manager's <c>guiWindowsFromBackToFront</c>. Showing and hiding
    /// work without the registries; ESCAPE does not, because the manager dispatches every input
    /// action by walking that list, and an unregistered clone would let Escape fall through and open
    /// the pause menu behind it. On top of those, <see cref="ModOptionsWindow.Load"/> writes three
    /// private members of <c>OptionsModalWindow</c> the original writes itself.
    ///
    /// Built EAGERLY, from the pump, as soon as the gui service has finished loading its own
    /// windows - not lazily on the first open. The window's <c>Loaded</c> flag has to be set before
    /// anything can show it, and its first show reads the tab panels the load coroutine builds; a
    /// lazy build would race that on the very first press. Rebuilding is the same code: a runtime
    /// change makes the gui manager destroy every window in the stack, ours included, and the next
    /// tick finds it gone and builds another.
    ///
    /// Nothing here is a Harmony patch, and nothing here runs in a player's game until they open the
    /// window - the per-frame cost is one Unity null check.
    /// </summary>
    public static class ModOptions
    {
        private const string PrefabPath = "Prefabs/Gui/ModalWindows/OptionsModalWindow";

        /// <summary>How many times a failed build is retried before the mod stops trying. A build
        /// that fails once usually fails every frame, and a log line per frame is worse than no
        /// window.</summary>
        private const int BuildAttempts = 3;

        private static ModOptionsWindow _window;
        private static IList<ModCategory> _categories;
        private static int _attempts;
        private static bool _stopped;

        /// <summary>Whether a game was being played when the window was built - which decides whether
        /// it has a Scanner tab, and so decides when it has to be built again.</summary>
        private static bool _builtInGame;

        /// <summary>
        /// The window's tabs, in the order they are drawn - the player's own scanner categories
        /// first, the mod's key bindings second.
        ///
        /// SCANNER IS IN GAME ONLY, and the window built on the main menu therefore has the one tab
        /// it had before this existed. Two of its pieces exist only in a game: the taxonomy it offers
        /// columns out of is a fact about the galaxy being played, and the box it names a category in
        /// is one the game only registers in game (<c>GuiWindowsStackDefinition</c>). A tab that
        /// could offer neither is a tab with nothing on it. The window is rebuilt when the player
        /// crosses that line (<see cref="Tick"/>), which is what makes this list a straight read of
        /// where the player is standing rather than of when the mod started.
        /// </summary>
        public static IList<ModCategory> Categories
        {
            get
            {
                if (_categories == null)
                {
                    _categories = new List<ModCategory>();
                    if (InGame())
                    {
                        _categories.Add(
                            new ModCategory(
                                ScannerEditor.CategoryName,
                                typeof(IModScannerService),
                                new ModScannerService(),
                                () => ModStrings.Get(ModStrings.ModSettingsScanner),
                                () => ModStrings.Get(ModStrings.ModSettingsScannerDescription),
                                ScannerRows.Fill
                            )
                        );

                        // One tab per slot, straight after the Scanner tab its buttons open them
                        // from. A tab rather than a window of its own: two of the game's modal
                        // windows on one renderer both draw, and the one shown SECOND is the one
                        // hidden behind the other's background (measured 2026-08-24), so a
                        // nested editor would be a page nobody can see.
                        for (int slot = 0; slot < ScannerCustomSlots.Count; slot++)
                        {
                            int at = slot;
                            _categories.Add(
                                new ModCategory(
                                    ScannerEditor.SlotCategory(slot),
                                    typeof(IModSlotsService),
                                    new ModSlotsService(),
                                    () =>
                                        ModStrings.Format(
                                            ModStrings.ModSettingsCustomCategory,
                                            at + 1
                                        ),
                                    () =>
                                        ModStrings.Format(
                                            ModStrings.ModSettingsCustomCategoryDescription,
                                            at + 1
                                        ),
                                    panel => ScannerSlotRows.Fill(panel, at)
                                )
                            );
                        }
                    }

                    _categories.Add(
                        new ModCategory(
                            "Keybinds",
                            typeof(IModKeybindsService),
                            new ModKeybindsService(),
                            () => ModStrings.Get(ModStrings.ModSettingsKeybinds),
                            () => ModStrings.Get(ModStrings.ModSettingsKeybindsDescription),
                            KeybindRows.Fill
                        )
                    );
                }

                return _categories;
            }
        }

        /// <summary>Whether a game is being played, as the tab list is decided by. Wrapped because it
        /// is asked every frame and the gui service is not always there to ask.</summary>
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

        /// <summary>The mod's window, or null while it does not exist - which is also the answer
        /// after the game has destroyed it.</summary>
        public static ModOptionsWindow Window()
        {
            return _window == null ? null : _window;
        }

        /// <summary>Whether <paramref name="window"/> is the mod's rather than the game's. Asked by
        /// the options screen, which reads whichever of the two is showing.</summary>
        public static bool IsOurs(OptionsModalWindow window)
        {
            return window is ModOptionsWindow;
        }

        /// <summary>Build the window when the game is ready for it, and build it again if the game
        /// ever takes it away. Called once a frame from the pump.</summary>
        public static void Tick()
        {
            if (_stopped)
            {
                return;
            }

            // The Scanner tab exists in a game and not on the main menu, so crossing that line means
            // a different window. Rebuilding is what the mod already does when the game destroys the
            // clone, and it is only ever done with the window down - a rebuild under a player
            // standing in it would take the page out from under them.
            if (_window != null && _builtInGame != InGame() && !Shown())
            {
                Shutdown();
            }

            if (_window != null || _attempts >= BuildAttempts || !Ready())
            {
                return;
            }

            _attempts++;
            try
            {
                Build();
            }
            catch (Exception e)
            {
                Log.Error("mod options: building the window threw: " + e);
            }
        }

        /// <summary>Show the window, wearing the skin that matches where the player is standing -
        /// the main menu's out-of-game skin, or the in-game one over the pause menu.</summary>
        public static void Open()
        {
            ModOptionsWindow window = Window();
            if (window == null || !window.Loaded || !Gui.GuiServiceAvailable)
            {
                return;
            }

            try
            {
                window.OutGameSkin = !Gui.IsInGame;
                Gui.GuiService.ShowWindow(window);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: showing the window threw: " + e);
            }
        }

        /// <summary>Show the tab a category was built under - what the Scanner tab's buttons do. The
        /// switch is the radio group's own, which is what makes it the same event a click is.
        /// </summary>
        public static void OpenCategory(string name)
        {
            ModOptionsWindow window = Window();
            if (window == null || window.RadioGroup == null)
            {
                return;
            }

            try
            {
                OptionsTabToggle toggle = ToggleOf(window, name);
                if (toggle != null && toggle.Toggle != null)
                {
                    window.RadioGroup.OnToggleSwitchCb(toggle.Toggle.gameObject);
                }
            }
            catch (Exception e)
            {
                Log.Warn("mod options: switching to " + name + " threw: " + e);
            }
        }

        /// <summary>Whether the mod's settings entry can do anything right now - what its node's
        /// availability reads.</summary>
        public static bool CanOpen()
        {
            ModOptionsWindow window = Window();
            return window != null && window.Loaded;
        }

        /// <summary>Whether the mod's window is on screen. Asked before a rebuild, which must never
        /// happen under a player standing in it.</summary>
        private static bool Shown()
        {
            ModOptionsWindow window = Window();
            try
            {
                return window != null && window.Shown;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>Write the settings file. Called when the window hides, by which point Apply has
        /// committed or Cancel has restored, so this is Apply-to-persist without a hook on either
        /// button.</summary>
        public static void Persist()
        {
            // The editor's copy first: it writes the settings file itself, and it is a no-op unless
            // the player applied something (Cancel has already dropped the copy by now).
            ScannerEditor.Commit();
            ES2Access.UI.Input.ModBindings.Persist();
            ModSettings.Save();
        }

        /// <summary>Give the game back exactly what it had: the window, its registrations, and the
        /// services the categories put in.</summary>
        public static void Shutdown()
        {
            _stopped = true;
            _window = null;
            _attempts = 0;
            ScannerEditor.Forget();
            ScannerRows.Forget();
            ScannerSlotRows.Forget();
            ModRows.Forget();
            RemoveServices();
            DestroyLeftovers();
            _categories = null;
            _stopped = false;
        }

        // ---- building ----

        private static bool Ready()
        {
            try
            {
                GuiManager manager = Manager();
                return manager != null && manager.GuiWindowsLoaded && BootWindow() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Build()
        {
            // A crashed teardown can have left one standing; it is found by NAME, because after a
            // hot reload the old load's types never equal this one's.
            DestroyLeftovers();

            OptionsModalWindow boot = BootWindow();
            GameGui.GuiWindowsStack stack = StackOf(boot);
            if (boot == null || stack == null)
            {
                Log.Warn("mod options: no options window or window stack to clone into");
                return;
            }

            UnityEngine.Object prefab = Resources.Load(PrefabPath);
            if (prefab == null)
            {
                Log.Warn("mod options: no prefab at " + PrefabPath);
                return;
            }

            GameObject clone = UnityEngine.Object.Instantiate(prefab) as GameObject;
            if (clone == null)
            {
                Log.Warn("mod options: " + PrefabPath + " did not instantiate as a GameObject");
                return;
            }

            clone.name = ModOptionsWindow.WindowName;
            clone.transform.SetParent(stack.transform, false);

            OptionsModalWindow original = clone.GetComponent<OptionsModalWindow>();
            ModOptionsWindow window = clone.AddComponent<ModOptionsWindow>();
            // The prefab's serialized references belong to the component the prefab declared, and
            // they do not follow a component swap - so every declared instance field comes across by
            // hand before the original goes.
            CopyFields(original, window);
            UnityEngine.Object.DestroyImmediate(original);

            AddServices();
            Invoke(
                typeof(GameGui.GuiWindow),
                "Initialize",
                window,
                new object[] { stack, true }
            );
            SetPrivateProperty(window, "Name", new StaticString(ModOptionsWindow.WindowName));
            Register(stack, window);

            // Nothing starts a clone's load: the manager's own pass ran at boot. Loaded is set at
            // once because ShowWindow will not touch a window that says it is not.
            window.BeginLoad();
            SetPrivateProperty(window, "Loaded", true);
            _window = window;
            _builtInGame = InGame();
            _attempts = 0;
            Log.Info("mod options: window built with " + Categories.Count + " categories");
        }

        private static void CopyFields(Component from, Component to)
        {
            for (
                Type type = typeof(OptionsModalWindow);
                type != null && type != typeof(MonoBehaviour);
                type = type.BaseType
            )
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                );
                for (int i = 0; i < fields.Length; i++)
                {
                    try
                    {
                        fields[i].SetValue(to, fields[i].GetValue(from));
                    }
                    catch (Exception e)
                    {
                        Log.Warn("mod options: copying " + fields[i].Name + " threw: " + e);
                    }
                }
            }
        }

        private static void Register(GameGui.GuiWindowsStack stack, ModOptionsWindow window)
        {
            // Showing and hiding need neither list. ESCAPE needs the manager's: every InputAction is
            // dispatched by walking it, and a window nobody walks past never sees the key - so the
            // galaxy's Escape would open the pause menu BEHIND the mod's window.
            Add(WindowList(typeof(GameGui.GuiWindowsStack), "guiWindows", stack), window);
            Add(
                WindowList(typeof(GameGui.GuiManager), "guiWindowsFromBackToFront", Manager()),
                window
            );
        }


        private static void Add(IList list, ModOptionsWindow window)
        {
            if (list != null && !list.Contains(window))
            {
                list.Add(window);
            }
        }

        // ---- teardown ----

        private static void DestroyLeftovers()
        {
            GameObject standing = null;
            GameGui.GuiWindowsStack stack = StackOf(BootWindow());
            standing = Unregister(
                WindowList(typeof(GameGui.GuiWindowsStack), "guiWindows", stack)
            ) ?? standing;
            standing =
                Unregister(
                    WindowList(typeof(GameGui.GuiManager), "guiWindowsFromBackToFront", Manager())
                ) ?? standing;

            if (standing == null && stack != null)
            {
                Transform found = stack.transform.Find(ModOptionsWindow.WindowName);
                standing = found == null ? null : found.gameObject;
            }

            if (standing != null)
            {
                UnityEngine.Object.DestroyImmediate(standing);
            }
        }

        /// <summary>Take the mod's window out of one of the game's registries, by NAME. Answers the
        /// GameObject it found, so the caller can destroy it once.</summary>
        private static GameObject Unregister(IList list)
        {
            if (list == null)
            {
                return null;
            }

            GameObject found = null;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                GameGui.GuiWindow entry = list[i] as GameGui.GuiWindow;
                if (entry == null)
                {
                    continue;
                }

                try
                {
                    if (entry.name != ModOptionsWindow.WindowName)
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                found = entry.gameObject;
                list.RemoveAt(i);
            }

            return found;
        }

        private static void AddServices()
        {
            IList<ModCategory> categories = Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                ModCategory category = categories[i];
                if (Services.GetService(category.ServiceType) == null)
                {
                    Services.AddService(category.ServiceType, category.Service);
                }
            }
        }

        private static void RemoveServices()
        {
            if (_categories == null)
            {
                return;
            }

            for (int i = 0; i < _categories.Count; i++)
            {
                try
                {
                    Services.RemoveService(_categories[i].ServiceType);
                }
                catch (Exception e)
                {
                    Log.Warn("mod options: removing a category service threw: " + e);
                }
            }
        }

        // ---- the private members the window is built out of ----

        internal static void AddCategory(
            OptionsModalWindow window,
            int index,
            ModCategory category,
            int count
        )
        {
            Invoke(
                typeof(OptionsModalWindow),
                "AddCategoryToggleAndPanel",
                window,
                new object[] { index, category.Name, category.ServiceType, count }
            );
        }

        internal static OptionsTabPanel PanelOf(OptionsModalWindow window, string category)
        {
            return Entry(window, "tabPanels", category) as OptionsTabPanel;
        }

        internal static OptionsTabToggle ToggleOf(OptionsModalWindow window, string category)
        {
            return Entry(window, "tabToggles", category) as OptionsTabToggle;
        }

        private static object Entry(OptionsModalWindow window, string fieldName, string category)
        {
            IDictionary map =
                GetPrivate(typeof(OptionsModalWindow), fieldName, window) as IDictionary;
            return map == null || category == null || !map.Contains(category)
                ? null
                : map[category];
        }

        internal static void SetPrivate(object target, string fieldName, object value)
        {
            try
            {
                FieldInfo field = typeof(OptionsModalWindow).GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                if (field == null)
                {
                    Log.Warn("mod options: OptionsModalWindow has no field " + fieldName);
                    return;
                }

                field.SetValue(target, value);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: writing " + fieldName + " threw: " + e);
            }
        }

        internal static void SetPrivateProperty(object target, string propertyName, object value)
        {
            try
            {
                for (Type type = target.GetType(); type != null; type = type.BaseType)
                {
                    PropertyInfo property = type.GetProperty(
                        propertyName,
                        BindingFlags.Instance
                            | BindingFlags.NonPublic
                            | BindingFlags.Public
                            | BindingFlags.DeclaredOnly
                    );
                    MethodInfo setter = property == null ? null : property.GetSetMethod(true);
                    if (setter != null)
                    {
                        setter.Invoke(target, new object[] { value });
                        return;
                    }
                }

                Log.Warn("mod options: nothing can set " + propertyName);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: setting " + propertyName + " threw: " + e);
            }
        }

        private static object GetPrivate(Type type, string fieldName, object target)
        {
            try
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                return field == null ? null : field.GetValue(target);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList WindowList(Type type, string fieldName, object target)
        {
            return target == null ? null : GetPrivate(type, fieldName, target) as IList;
        }

        private static void Invoke(Type type, string name, object target, object[] arguments)
        {
            try
            {
                Type[] signature = new Type[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                {
                    signature[i] = arguments[i] == null ? typeof(object) : arguments[i].GetType();
                }

                MethodInfo method = type.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    signature,
                    null
                );
                if (method == null)
                {
                    Log.Warn("mod options: " + type.Name + " has no method " + name);
                    return;
                }

                method.Invoke(target, arguments);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: calling " + name + " threw: " + e);
            }
        }

        private static GameGui.GuiWindowsStack StackOf(OptionsModalWindow window)
        {
            if (window == null)
            {
                return null;
            }

            try
            {
                PropertyInfo property = typeof(GameGui.GuiWindow).GetProperty(
                    "GuiWindowsStack",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                return property == null
                    ? null
                    : property.GetValue(window, null) as GameGui.GuiWindowsStack;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own options window - the one it loaded at boot. Looked up by TYPE,
        /// which cannot answer with the clone: the manager keys that lookup on the exact type, and
        /// the clone's is <see cref="ModOptionsWindow"/>.</summary>
        private static OptionsModalWindow BootWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<OptionsModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiManager Manager()
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
