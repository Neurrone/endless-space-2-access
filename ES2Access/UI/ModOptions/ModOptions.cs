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

        /// <summary>
        /// The window's four tabs, in the order they are drawn - General first, then the player's
        /// own scanner categories, then the mod's key bindings, then Bookmarks. General is where a
        /// setting that belongs to no other tab lives, and it is first because that is where a
        /// player looks for one; being first also makes it the tab the window OPENS on
        /// (<see cref="ModOptionsWindow.Load"/> shows the first panel). Bookmarks is LAST because it
        /// holds no setting at all (<see cref="BookmarkRows"/>): it says where this campaign's map
        /// bookmarks are kept and offers the two ways of reaching them.
        ///
        /// ALL EXIST EVERYWHERE, main menu included (owner ruling 2026-08-24). The Scanner tab was
        /// in-game only for as long as its columns were a snapshot of the galaxy being played; they
        /// come from the game's DATABASES now (<c>GalaxyScanner.Taxonomy</c>), so there is nothing on
        /// either page that needs a game, and the window no longer has to be rebuilt when the player
        /// crosses that line.
        ///
        /// The keybinds category is named "Controls" - the game's own key for its own key-binding
        /// page - because three of the window's behaviours are wired to that word and all three are
        /// wanted here: the Reset to Defaults buttons are shown for it and nothing else
        /// (<c>OptionsModalWindow.OpenCategory</c> :119-120), leaving with unapplied changes asks the
        /// binding question rather than the generic one (:66-68), and the tab draws itself with
        /// "%OptionToggleControlsTitle", the same words the game's own Controls tab wears in every
        /// language. What the buttons DO is repointed at the mod's own handler
        /// (<see cref="ModOptionsWindow"/>), so the game's own bindings are never touched.
        /// </summary>
        public static IList<ModCategory> Categories
        {
            get
            {
                if (_categories == null)
                {
                    _categories = new List<ModCategory>();
                    _categories.Add(
                        new ModCategory(
                            GeneralCategory,
                            typeof(IModGeneralService),
                            new ModGeneralService(),
                            () => ModStrings.Get(ModStrings.ModSettingsGeneral),
                            // No sentence about itself: the tab is called what it holds and there is
                            // nothing further to say (owner ruling 2026-09-02). A null description is
                            // how a tab asks for NO tooltip - see ModOptionsWindow.Relabel.
                            null,
                            GeneralRows.Fill
                        )
                    );

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

                    _categories.Add(
                        new ModCategory(
                            KeybindsCategory,
                            typeof(IModKeybindsService),
                            new ModKeybindsService(),
                            () => Gui.Localize(ControlsTitleKey),
                            () => Gui.Localize(ControlsDescriptionKey),
                            KeybindRows.Fill
                        )
                    );

                    _categories.Add(
                        new ModCategory(
                            BookmarksCategory,
                            typeof(IModBookmarksService),
                            new ModBookmarksService(),
                            () => ModStrings.Get(ModStrings.ModSettingsBookmarks),
                            () => ModStrings.Get(ModStrings.ModSettingsBookmarksDescription),
                            BookmarkRows.Fill
                        )
                    );
                    // macOS only: the Speech tab configures the mod's own speaking (backend,
                    // voice, rate, volume). On Windows the screen reader owns all of that.
                    if (Platform.IsMacOS)
                    {
                        _categories.Add(
                            new ModCategory(
                                "Speech",
                                typeof(IModSpeechService),
                                new ModSpeechService(),
                                () => ModStrings.Get(ModStrings.ModSettingsSpeech),
                                () => ModStrings.Get(ModStrings.ModSettingsSpeechDescription),
                                SpeechRows.Fill
                            )
                        );
                    }
                }

                return _categories;
            }
        }

        /// <summary>The game's own key for a key-binding page. See <see cref="Categories"/> for why
        /// the mod's tab is called this rather than something of its own.</summary>
        public const string KeybindsCategory = "Controls";

        /// <summary>The General tab's category key - an identifier the window keys its panels and
        /// toggles by, never a spoken word (the words are <see cref="ModStrings.ModSettingsGeneral"/>,
        /// written over the tab by <see cref="ModOptionsWindow"/>).</summary>
        public const string GeneralCategory = "General";

        /// <summary>The Bookmarks tab's category key - an identifier, like
        /// <see cref="GeneralCategory"/>, never a spoken word.</summary>
        public const string BookmarksCategory = "Bookmarks";

        /// <summary>What the game names that page in the player's language, and what its own options
        /// window says about it - the mod's key-binding tab is the game's Controls tab and reads as
        /// one, in every language the game ships.</summary>
        private const string ControlsTitleKey = "%OptionToggleControlsTitle";
        private const string ControlsDescriptionKey = "%OptionToggleControlsDescription";

        /// <summary>Whether a game is being played - which decides which SKIN the window wears, and
        /// nothing else since both tabs exist either way. Wrapped because the gui service is not
        /// always there to ask.</summary>
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

            // Before the build guard below, which returns as soon as the window exists: this is the
            // pump the Bookmarks tab says a press's result from, and a press can only happen once
            // the window is built (ScannerEditor has a tick of its own in ModEntry for the same
            // job; a tab that only needs a line said borrows this one rather than adding another).
            BookmarkRows.Tick();

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

        /// <summary>Whether the mod's settings entry can do anything right now - what its node's
        /// availability reads.</summary>
        public static bool CanOpen()
        {
            ModOptionsWindow window = Window();
            return window != null && window.Loaded;
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
            GeneralRows.Forget();
            BookmarkRows.Forget();
            SpeechRows.Forget();
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
            OptionsModalWindow original = BootWindow();
            Beside(
                WindowList(typeof(GameGui.GuiWindowsStack), "guiWindows", stack),
                window,
                original
            );
            Beside(
                WindowList(typeof(GameGui.GuiManager), "guiWindowsFromBackToFront", Manager()),
                window,
                original
            );
            CountAsAModal(window);
        }

        /// <summary>
        /// MAKE THE GAME COUNT THIS WINDOW AS A MODAL, which is a third registry and a subscription.
        ///
        /// The manager keeps its own list of modal windows and subscribes to each one's
        /// <c>VisibilityChanged</c> ONCE, when it loads the windows at boot
        /// (<c>GuiManager.Load_IGuiGamePanelService</c>). A clone built afterwards is in neither, so
        /// <c>IsAnyModalVisible</c> stayed FALSE the whole time the mod's settings window was up -
        /// and that flag is what the game weighs the tutorial popup against
        /// (<c>TutorialPopupPanel.UpdateLayerAndVisibilityAccordingToOtherWindows</c>). The visible
        /// defect: a minimised tutorial's bar was still drawn, and still declared, over the mod's
        /// settings window, where over the game's own options window the game hides it.
        ///
        /// Every other user of the flag is a place the mod's window ought to count too - the scan
        /// view refusing to toggle, the tutorial's own keys standing down - so this is the game
        /// being told the truth rather than a fix aimed at the tutorial.
        /// </summary>
        private static void CountAsAModal(ModOptionsWindow window)
        {
            try
            {
                GuiManager manager = Manager();
                IList modals = WindowList(typeof(GuiManager), "guiModalWindows", manager);
                if (manager == null || modals == null || modals.Contains(window))
                {
                    return;
                }

                MethodInfo handler = typeof(GuiManager).GetMethod(
                    "ModalWindow_VisibilityChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                if (handler == null)
                {
                    Log.Warn("mod options: GuiManager has no ModalWindow_VisibilityChanged");
                    return;
                }

                window.VisibilityChanged += (EventHandler)
                    Delegate.CreateDelegate(typeof(EventHandler), manager, handler);
                modals.Add(window);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: registering the window as a modal threw: " + e);
            }
        }

        /// <summary>
        /// THE CLONE STANDS WHERE THE WINDOW IT WAS CLONED FROM STANDS, and that placement is what
        /// decides who answers Escape.
        ///
        /// <c>GuiManager.HandleInput</c> (:2058-2063) walks <c>guiWindowsFromBackToFront</c> from the
        /// END backwards and gives the action to the first SHOWN window that takes it, so the last
        /// entry is asked first. Appended, the clone was that entry (170 of 171) - ahead of the
        /// message box at 165 - and an Escape meant for a box the mod had raised over its own window
        /// went to the WINDOW instead. The window, holding an unapplied rebind, answered by raising
        /// "%BindingExitWithoutApplyMessage" through <c>ShowMessage</c>, which on an already-shown box
        /// overwrites <c>ActiveEventHandler</c> and then calls <c>ShowWindow</c> on a window that is
        /// already shown - a no-op, so <c>OnBeginShow</c> never runs and the drawn box keeps the OLD
        /// words. The player then confirmed what they were still being shown, and the answer went to
        /// "discard your changes and close": booted to the menu behind, the rebind gone (measured
        /// 2026-08-24).
        ///
        /// Inserted next to the game's own options window (153 of 171) the clone is asked exactly
        /// where that window is asked: above the pause menu it opens over, below the message box it
        /// raises. Nothing else in the list moves, so no other window's turn changes.
        /// </summary>
        private static void Beside(IList list, ModOptionsWindow window, OptionsModalWindow original)
        {
            if (list == null || list.Contains(window))
            {
                return;
            }

            int at = original == null ? -1 : list.IndexOf(original);
            if (at < 0)
            {
                list.Add(window);
                return;
            }

            list.Insert(at + 1, window);
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
            // Not optional: a destroyed window left in this list is read every time any modal's
            // visibility changes, and reading .Shown on a destroyed component throws.
            standing =
                Unregister(WindowList(typeof(GuiManager), "guiModalWindows", Manager()))
                ?? standing;

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
