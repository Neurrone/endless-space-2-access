# ES2 GUI framework (AGE) — reverse-engineering notes

Documents how the game's UI works, not how the mod works — the AGE framework's own behaviour,
including every measured AGE fact the mod has needed. Sources are cited as
`decompiled/<Assembly>/<File>.cs`; regenerate with `.\decompile.ps1`. Key claims here were
verified directly against the decompiled sources and the live `/gui/game` dump.

ES2 does not use Unity uGUI for its game UI. It uses Amplitude's own **AGE** framework, which
lives in `decompiled/Assembly-CSharp-firstpass/` (global-namespace types like `AgeTransform.cs`
at the folder root, the higher-level window framework under `Amplitude.Unity.Gui/`). The game's
concrete screens are thin subclasses in `decompiled/Assembly-CSharp/`.

## Core types

### AgeTransform (`Assembly-CSharp-firstpass/AgeTransform.cs`)

The AGE equivalent of RectTransform+GameObject. Every widget has one. Useful members:

- `Visible`, `Enable` (bool properties; `Enable` = interactable), `Alpha`, `FadeOnDisable`
- `AgeTooltip` — the attached tooltip component. **In play mode it returns a field cached in
  `Awake` (`privateTooltip`), not a live `GetComponent`** (:387-395, :3772) — so a tooltip added to a
  widget at runtime is invisible to the engine and to every reader until that private field is set
  by reflection. The same caching applies to `AgeControl` and `AgePrimitive`
- `GetGlobalPosition()` → screen `Rect`; `Children`, `GetChildren<T>()`
- `ModifiersRunning` — true while show/hide animations (AgeModifiers) are running
- Table layout helpers: `ReserveChildren`, `RefreshChildrenArray`, `ArrangeChildren`

### Primitives and controls (firstpass root)

- `AgePrimitiveLabel.Text` — the display string of any label. **This is the property to read.**
- `AgePrimitiveImage.Image`/`TintColor`
- `AgeControlButton`, `AgeControlToggle` (`.State`), `AgeControlSlider` (`.CurrentValue`,
  `.MinValue/.MaxValue/.Increment`), `AgeControlDropList` (`.SelectedItem`, `SetLabels`),
  `AgeControlTextField` (`.Label`). Controls derive from `AgeControl` which wraps an
  `AgeTransform`; buttons expose `MouseEnter/MouseLeave/MouseDown` as directly callable methods.

### AgeManager (`Assembly-CSharp-firstpass/AgeManager.cs`, singleton `AgeManager.Instance`)

The mouse/focus authority — no separate event system exists:

- `OverrolledTransform` — the `AgeTransform` currently hovered (public field, settable)
- `FocusedControl` — keyboard-focused `AgeControl` (text fields; has `.IsKeyExclusive`)
- `ActiveControl` — control currently pressed/dragged
- `Cursor` — current cursor position; `MouseCovered` — is the mouse over any UI at all

The game's own `ImageInformationWindow` (`Assembly-CSharp/ImageInformationWindow.cs`) is a debug
overlay that polls `OverrolledTransform` each frame and reads components under it — a proven
in-game pattern for cursor-based inspection without any patching.

## Window lifecycle and readiness

Hierarchy: engine `Amplitude.Unity.Gui.GuiPanel` → `GuiWindow` → game wrappers `GuiWindow`,
`GuiScreen` (full-screen pages), `GuiModalWindow`, `GuiTooltipWindow`, plus `GuiPanel` for
embedded sub-panels (all in `Assembly-CSharp/`).

Lifecycle virtuals (engine `Amplitude.Unity.Gui/GuiPanel.cs`): `Load()` (coroutine) →
`OnBeginShow(instant)` → `OnEndShow()` → `OnBeginHide(instant)` → `OnEndHide()` → `Unload()`.

The game's `GuiWindow` (`Assembly-CSharp/GuiWindow.cs`) adds the two members that matter most
for accessibility:

- **`IsReady`** — `AgeTransform.Visible && AgeTransform.Enable && !AgeTransform.ModifiersRunning`.
  One property meaning "fully shown, animation finished, interactive". This is the
  screen-readiness gate; never announce or drive a window before it.
- **`VisibilityChanged`** (public `EventHandler`) — fired from `OnBeginShow` and `OnEndHide` via
  `protected NotifyVisibilityChanged()`. A single Harmony postfix on `NotifyVisibilityChanged`
  observes every window show/hide in the game.

- **The handover gap is frames, not instants.** Between an opener standing down and the new
  window passing `IsReady` no screen is focused and the mod is deaf. Measured at **~4 frames**
  on the improvements modal; assume the same order elsewhere and design modal opens knowing
  the interval exists.

`GuiManager` (`Assembly-CSharp/GuiManager.cs`) tracks all windows and exposes aggregate state:
`IsAnyScreenVisible`, `VisibleScreen`, `IsAnyModalVisible`, `ModalOnTop`,
`GetFirstVisibleModalWindow()`, and a global `ScreenVisibilityChanged` event. Generic
`ShowWindow<T>()`/`HideWindow<T>()`/`GetWindow<T>()` come from the engine base and are reachable
through `Gui.GuiService`.

## Reading text and localization

- Localization keys start with `%`: `Gui.IsLocalizationKey(s)` (`Assembly-CSharp/Gui.cs`).
- `Gui.Localize(key)` (+ overloads with format parameters) → `AgeLocalizer.Instance.LocalizeString`.
- Static UI copy lives in `GuiElement` database records; `Gui.GetTitle(name)` /
  `Gui.GetDescription(name)` return **unresolved** `%keys`; `Gui.GetLocalizedTitle` /
  `GetLocalizedDescription` resolve them.
- Labels usually hold final text, but tooltips and deferred content sometimes hold raw keys —
  when reading any UI string, check `IsLocalizationKey` and localize if needed.
- **A localization miss ECHOES THE KEY, it does not resolve to empty.** `Gui.Localize` →
  `AgeLocalizer` → `GuiLocalizationProxy.LocalizeString` passes `defaultValue: key`
  (`Amplitude.Unity.Gui.Proxies/GuiLocalizationProxy.cs:21`), so an unregistered
  `%OptionXxxTitle` is DRAWN AND SPOKEN raw. A plain (non-`%`) string written into a label is
  drawn as itself, so nothing a mod writes needs registering in the localization database.
- **AGE localizes label text itself**, so assigning a raw `%key` still DRAWS localized
  (`AgePrimitiveLabel.cs:702-717`) — which means a drawn label is no evidence that a mod's own
  lookup would have resolved.
- **`Gui.GetTitle` can hand back a key that has no translation.** `ShipStatCommandPoints`
  declares `%ShipStatCommandsTitle`, which the corpus no longer has; the engine's own naming
  convention (`"%" + name + "Title"`) resolves it. Anything reading a title through the element
  database needs that fallback, and silence rather than a `%key` as the last resort. The same
  hazard has a second face: `Gui.GetLocalizedTitle` answers for a missing element with a PINK
  DESIGNER PLACEHOLDER or the raw `%Key` rather than failing, so every title read this way is
  tested before it is spoken.
- **A typewriter label already holds all its words.** `AgeModifierTypewriter` does not write
  text a character at a time: it sets the whole string once and advances the label's
  `CurrentLine`/`CurrentCharInLine`, which only the RENDERER honours. So `AgeText.Label` on a
  mid-animation label is complete, and an announcer never has to rebuild the panel's phrasing
  from the model to beat the animation.
- **A refusal's own wording is keyed by its flag**: `%Failure<flag>Description` — so the sentence
  a blocked control shows can be looked up from the flag alone, with no tooltip drawn.

## Enumerating and activating controls

Two proven strategies:

1. **Enumerate**: from any window root (`Gui.GuiService.GetWindow<T>().AgeTransform` or
   `GuiManager.VisibleScreen`), Unity's `GetComponentsInChildren<AgeControlButton>()` /
   `<AgePrimitiveLabel>()` etc.; filter on `AgeTransform.Visible && Enable`. Beware
   per-skin duplicates and effective visibility (below).
2. **Activate**: click handlers are wired in prefabs to `SendMessage("<HandlerName>", ...)` — the
   convention is `OnClickCb`-style private methods on the owning screen. Invoking the same
   handler method via reflection is byte-for-byte the mouse code path minus hover animation.
   Higher-fidelity simulation (`button.MouseEnter(...)` + `MouseDown(...)`) exists but is rarely
   needed. Prefer the underlying service/order call when identifiable (see
   `architecture.md`).
3. **Generic button press** (works for any AGE button, verified on the options and message-box
   windows): replay the prefab wiring itself —
   `button.OnActivateObject.SendMessage(button.OnActivateMethod, button.gameObject, ...)`,
   which is verbatim what `AgeControlButton.HandleMouseUpOrDown` does. Buttons with an empty
   `OnActivateMethod` are decorative click-shields (`BackgroundGroup*`); skip them.

## Input ownership (`InputManager`, `AgeManager.FocusedControl`)

`InputManager.HandleInput` (Assembly-CSharp, ~line 1210) is the game's keyboard gate: when
`AgeManager.Instance.FocusedControl` is `IsKeyExclusive` (true for `AgeControlDropList`,
`AgeControlKeyBindingField`, `AgeControlTextArea`/chat field) **and** `StandardCancel`
(default true; only the chat field opts out), Escape and RightClick are consumed — focus is
cleared and nothing propagates to the modal underneath. This is the mechanism to hand a
widget when the mod must stop the game from acting on a key: park the game's focus on that
widget, exactly as the game's own mouse flows do.

- `AgeControlDropList`: `OpenPopupMenu`/`ClosePopupMenu(bool)` are private (reflection);
  `PopupMenu.SetSelection(i)` moves the highlight only — it never writes `SelectedItem`;
  `FocusLoss()` closes the popup; entry labels live in `LabelTable` as **raw `%` loc keys**
  (only Resolution ships plain strings) — localize before speaking; per-entry enabled state
  is `PopupMenu.Table.Children[i].Enable`.
- `AgeControlKeyBindingField`: scans every `KeyCode` below the joystick range each focused
  frame (only `Mouse0` forbidden); max 2 simultaneous keys
  (`MaximumNumberOfKeysByCombination == 2`, `:9`), and `KeyCombination(List<KeyCode>)` splits
  modifiers out of the list only when there is more than one key — so a three-key chord (the
  mod ships Ctrl+Shift+Enter and Ctrl+Alt+Enter) reads out correctly and cannot be
  re-captured as itself, and a lone modifier key binds as a one-entry list with no modifier
  mask. **Capture ends on the first key release** (`OnValidateCb` → focus cleared →
  `OnLoseFocusCb` applies if the combo differs from both current bindings, raising
  `%OptionBindingAlreadyBindedConfirmation` on conflicts). The activating keystroke's own
  release therefore kills a capture started synchronously — defer the focus handover until
  two consecutive frames with no key held.
  - **A field losing focus while it holds nothing only sometimes clears the binding.**
    `OnLoseFocusCb` (:80-98) commits the blank only when it differs from BOTH of the row's
    combinations, so a row whose other slot is EMPTY reads "no change" and keeps its key,
    while a row that has both filled loses the one being captured. Measured both ways:
    `ui.up` (Up Arrow, no secondary) survived a focus-null round trip untouched; `ui.activate`
    (Return + KeypadEnter) came back as `ui.activate: , KeypadEnter`. The same equality check
    makes capturing a chord the row's OTHER slot already holds a silent no-op.
  - **Escape during a capture is that ending, and Escape can never be BOUND here.**
    `InputManager.HandleInput` :1210-1226 runs in `Update` and nulls the focused control the
    moment an Escape-bound action fires while a key-exclusive control holds the keyboard; the
    field's own scan is in AgeManager's `LateUpdate`, so the field never sees the key and loses
    focus holding nothing — whereupon the entry above applies: a CLEAR on a row with both slots
    filled, nothing at all on a row whose other slot is empty. Mod policy (owner ruling
    2026-08-24, retiring an earlier restore-on-Escape): what the field does with the nothing it
    is holding is the game's own business, and the mod does nothing but read the cell out
    afterwards (`OptionsScreen.WatchForTheEndOfACapture`), so a mod row and a game row behave
    identically. Verified live with physical keys: Enter on a key cell starts the capture, a
    physical chord commits it and is spoken, and Apply writes `keys.<action>` to `settings.cfg`
    while a rebind back to the default drops the line.
  - **The clear path is the game's own value path**: `Option.Value = new InputBinding(action,
    primary-or-None, secondary-or-None)` then `window.OnOptionChanged(option)` and
    `item.Refresh()`. Verified end to end on the game's Controls tab: Apply lit, the drawn
    field went blank, the live `InputBindingsValidate` read `Validate: , `, and Cancel put
    `Validate: Return` back.
  - **`OptionKeyMappingItem.OnChangeOptionValueConfirmation` is the one commit hook.** It fires
    once per commit, after the value is written, for a game row and a mod row alike, and NOT
    for Cancel's `RestoreSettings` or for Reset to Defaults. Its `MessageBoxResultEventArgs`
    must be checked for `Ok`: it is also the conflict box's callback.
  - **Delete is bound to nothing in the game's 64 input options** (measured by enumerating
    `Option.GetOptions(Services.GetService<IInputOptionsService>(), typeof(
    IInputOptionsService), …)`). The only direct reads of the key in the whole tree are
    `DebugGOMouseMover`, `DirectorController` and `AgeControlTextArea` — the last owns the
    keyboard while it reads it, which stands a mod key layer down anyway.
- `AgeControlScrollView`: scroll by replaying `MouseWheel(increment)` (clamping via private
  `ConstraintAndPlace`, scrollbar sync and `OnScrollObject` notify all included);
  `VirtualArea.Y` units are 1:1 with `GetGlobalPosition()` pixels (measured); wheel constant
  300 × `ScrollFactorVertical` per increment.

## Options window (`OptionsModalWindow`)

- Six categories (Video/Audio/Gameplay/Gui/Controls/Notifications), each an `OptionsTabPanel`
  under `TabPanelsContainer`; rows are `OptionItem` subtypes under `OptionsTable`. The
  deterministic tab switch is `GuiRadioGroup.OnToggleSwitchCb(toggle.gameObject)` — public,
  verbatim the click path (selection tick, underline animation, `OnCategorySwitchCb`).
- The `Option` wrapper (Amplitude.Unity.Options, firstpass) reflects over provider
  properties: `Value`/`Changed`/`Store`/`Restore`/`Commit`; `Latent = true` options (video
  display settings, UI scale) only hit the provider on Apply (`CommitSettings`), which for
  Video also triggers `ChangeDisplaySettings()` and the 15 s
  `%OptionYouHaveNSecondsToValidateDescription` countdown. **Cancelling that countdown
  reverts the latent options and closes the whole window** (back to the menu) — the game's
  own behaviour. `CheckConstraint()` (e.g. `VideoManager.CheckResolution`) disables/hides
  rows cross-option.
- **Two complete per-skin button bars** (`ButtonGroupInGame`/`ButtonGroupOutGame` under the
  `InGameParts`/`OutGameParts` containers): the window's public `ApplyButton`/`CancelButton`/
  `ResetButton`/`ResetInGameButton` fields all point at the **in-game** set, and the window
  writes Apply's `Enable` flag and tooltip to those field instances regardless of skin — the
  drawn out-game Apply never receives them. Discover the drawn buttons by ancestor-chain
  visibility; read Apply's availability from the field instance, press through the drawn
  button's own wiring.
- Row change paths (set control state, then invoke the row's private mouse callback via
  reflection; `parent.OnOptionChanged` keeps Apply and constraints in sync):
  `OptionCheckboxItem.OnSwitchCb`, `OptionSliderItem.OnSliderReleasedCb`,
  `OptionDropListItem.OnEntrySelectedCb` (protected). Slider display format: snap to the
  increment grid, then `ToString(Slider.ValueFormat)` (`"#####0%"` for 0–1 percent sliders,
  fallback `"######0"`).
- **No ES2 option ships as an `OptionTextFieldItem`, and the row type is broken.** The prefab
  ships `UseLoseFocusCallback = true`, `OnLoseFocusMethod = "OnTextFieldFocusLostCb"` (measured
  on `panel.OptionTextFieldPrefab`), so the callback fires whenever the field loses the engine's
  keyboard; what it does is `base.Option.Value = TextFieldLabel;`
  (`OptionTextFieldItem.cs:30`) — the LABEL OBJECT, not its text — and `Option.SetValue`
  swallows the `InvalidCastException` as a logged error, so the row silently keeps its old
  value. The mod mints such rows and patches the commit (`OptionTextFieldCommit`'s class
  comment).
- **`Option.Restore()` with a NULL backup does nothing for a string and logs for a bool.**
  `Restore()` is `Value = backupValue` and `SetValue` runs `Convert.ChangeType(value, type)`:
  `null` to `string` answers null and the `obj != null` guard skips the write, while `null` to
  `bool` throws and is caught and logged. So a row added AFTER `BackupSettings` reads
  `Changed == true` — which is correct, something was added — and Cancel simply does not touch it.
- **The window can be CLONED to host the mod's own settings** — see "Cloning a game window"
  below; the mod's own recipe is `ES2Access/UI/ModOptions/ModOptions.cs`'s class comment.

## Message boxes (`MessageBoxWindow`, `GuiManager.ShowMessage*`)

- No buttons enum: a button is shown iff its caption (`ValidateTitle`/`CancelTitle`/
  `AlternativeTitle`) is non-empty (`MessageBoxWindow.cs:96-98`) — so an informative box with
  nothing to cancel passes `cancelTitle: string.Empty` and draws one Confirm button; captions
  live in sibling labels, not on the buttons. The default title is `%MessageBoxConfirmationTitle`
  ("Confirmation") whatever the type, which is what the game's own informative boxes show too.
  `ShowMessageWithTimeout` never clears `AlternativeTitle`, so a stale third button can leak
  onto a countdown box — declare from live visibility.
- **`ShowMessage` on a box that is ALREADY shown swaps the answer and redraws nothing.**
  `GuiManager.ShowMessage` (:2303-2315) writes `Title`/`Message`/`ActiveEventHandler` and calls
  `ShowWindow`, which is a no-op for a shown window — and `MessageBoxWindow.OnBeginShow` (:71-98)
  is the only thing that copies those properties into the drawn labels. So the box can keep one
  warning's words while its `ActiveEventHandler` has become another's, and confirming what is on
  screen runs the other one's handler (measured 2026-08-24, on the mod's cloned options window).
- `VisibilityChanged` fires (twice) before the captions are written; poll
  `Shown && IsReady` instead. Timeout expiry auto-fires **Cancel**, not OK. The countdown
  rewrites `MessageLabel.Text` every frame. `Gui.GuiService.HideWindow(w)` is the only legal
  hide (`GuiWindow.Show/Hide` throw). `MessageBoxNonBlockingWindow` is a separate,
  non-modal near-twin and is not covered by the modal-layer assumptions.

## Focus, text fields and the engine's own keyboard

- **The game's own key names are a localized table**, so a mod can speak chords out of it.
  `%KeyCode<Name>` — 120 rows in the English corpus and present in all ten languages, e.g.
  `%KeyCodeReturn` "Enter", `%KeyCodeKeypadEnter` "Enter (Keypad)", `%KeyCodeLeftArrow` "Left
  Arrow", `%KeyCodeBackslash` "Backslash" — is what the options screen writes a binding with
  (`Amplitude.Unity.Input/KeyCombination.LocalizeKeyCode` :194-208: `%KeyCode` + the enum name
  through `AgeLocalizer`, a miss answering with the key itself). There are NO rows for the plain
  letter keys, so those still fall back to the engine's `KeyCode` name. Mod policy:
  `ChordNames.KeyName` asks this table first, so a player hears the keys named the way their own
  copy of the game names them.
- **`InputManager` binds no letter keys at all.** The full default table is the F-keys
  (F1-F8 screens), arrows, `KeypadEnter` end turn, `Space`/`Mouse2` scan view, `Return`/`Tab` chat,
  `KeypadMinus` sleep-for-this-turn, `Ctrl+F` search, `PageUp`/`PageDown` zoom, the debug chords, and
  the encounter camera's `Minus,KeypadMinus`/`Plus,KeypadPlus` speed keys — so `Ctrl+I` was free.
  Ctrl+H is `DebugSwitchHighDefinition` (:790), live only in an internal build / with
  `EnableModdingTools` (`GuiManager.cs:2130`); Ctrl+E is `DebugSendNewEvent` (:858) and NO handler
  in the game answers it; Ctrl+N, Ctrl+G, Ctrl+T and Ctrl+Alt+E are bound to nothing at all. (The
  mod's whole binding table is `docs/interaction.md`.)
- **The game's own end-turn key is the keypad Enter** (`InputManager.cs` :215), which the mod
  claims for Activate — so a mod user has no end-turn shortcut unless the mod gives them one
  (Ctrl+Alt+E does, 2026-08-22). Its handler is `EndTurnWindow.HandleInput` :637-654: three gates
  (`Gui.GuiGameWindowService.CanEndTurnByShortcut`, the tutorial's `EndTurnDisabler`, and
  `EndTurnService.Target.CanEndTurn()`), then an armed cursor is put back to `GalaxyCursor` before
  `TryToEndTurn` — a targeting mode left armed would otherwise eat the turn.
- **The four previous/next pairs the page keys drive, and what the game does with each.**
  Star system: `StarSystemScreen.PreviousSystemButton`/`NextSystemButton` (:26,28) are drawn only
  for the player's OWN systems and switched on once a second is colonised (:613-627); they cycle
  `DepartmentOfTheInterior.ColonizedStarSystems` WITH WRAP (`CycleStarSystemHelper` :180-197), and
  the game also wires them to its `Next`/`Previous` bindings, which are the bare Right/Left arrows
  (:206-214) — keys the mod claims on that screen, which is why the pair was unreachable.
  Planet page: `PlanetInfoSidePanel`'s pair. Notification popup: `NotificationWindow`'s pair — the
  game's own Up/Down keys on that window are wired the OTHER WAY ROUND from the buttons, so the
  mod follows the buttons. Academy: `HeroNavigationLeft/RightButton` (:28,30) CLAMP rather than
  wrap (`Enable = selectedHeroId > 0` / `< Heroes.Count - 1`, :210-211).
- **The engine delivers keys to the focused control in `LateUpdate`.** `AgeManager.LateUpdate`
  (:919-923) sends `KeyDown` to `FocusedControl` on any `anyKeyDown` frame, and
  `AgeControlTextField.KeyDown` (:76-81) polls `GetKeyDown` itself on top of that. With
  `RenameModalWindow.OnBeginShow` (:74-82) taking focus SYNCHRONOUSLY, one physical Enter could
  open the box, be delivered to the field, validate and hide it — a rename that committed
  nothing. `GameKeyboardHandover` is the answer (`ES2Access/UI/Input/GameKeyboardHandover.cs`).
- **A text field stands the WHOLE mod layer down.** `AgeControlTextArea.IsKeyExclusive` true is
  the signal; `AgeControlDropList` and the key-binding field are the only other exclusive
  controls in the game.
- **The engine's Escape for a key-exclusive field only UNFOCUSES it** (`InputManager.cs`
  :1210-1241) — it never closes the surface around it — and while a field is exclusive the
  engine swallows every other game hotkey (the camera excepted). So a mod owns the exit from
  any keyboard it hands over.
- **Hiding an AGE window does NOT unfocus its text field** (measured: field `Visible=True`,
  `activeInHierarchy=True`, window `Visible=False`), which kills a mod's key layer
  permanently — and it happens on the rename box's own hide path.
  `AgeManager.FocusedControl`'s setter runs FocusLoss/FocusGain (:277-301), so clearing it is
  the game's own hand-back.
- **A focused text field acts on Return itself, twice over — and the validate is the
  SCREEN's action, not the edit's.** `AgeControlTextField.KeyDown` (firstpass :76-89) fires
  the field's validate callback on `Input.GetKeyDown(Return)`: for `LoadSaveModalWindow`
  that is `OnSaveNameTextFieldValidateCb` (:509-516) → `OnSaveCb()`, which writes the save
  and closes the screen; for `RenameModalWindow` it posts the rename and closes the box.
  Separately, `InputManager.HandleInput` (Assembly-CSharp :1210-1243) swallows `Validate`
  ONLY while a key-exclusive control holds focus — on every frame the field does NOT hold
  it, the window's own Validate handler is live. **Mod policy that follows:** never hand a
  game text field the keyboard while the key that asked for the edit is still down (both
  doors shut by that one rule), and while the mod owns a live edit it takes Return/
  KeypadEnter from the KeyDown dispatch and ends the edit itself, leaving the surface
  standing — the chat box is the single exemption (its Enter sends).

## Windows, layers and the modal stack

- **Every rename in the game is ONE box.** Whatever the opener — a system, a fleet, a ship design, a
  hero — it funnels through `IGuiService.RequestNewName` (`GuiManager.cs:2364`), which shows the one
  `RenameModalWindow` with the caller's callback. So a rename screen is written once and every opener
  inherits it.
- **`RenameModalWindow.CheckButtons` is DEAD in the shipped build.** Its only caller is
  `StartProfanityFiltering`, which compiles to an empty body, so `OnInputChanged` is never invoked,
  the field never turns red and `ValidateButton.Enable` keeps whatever the prefab shipped (true).
  A refusal therefore cannot be expressed through the box; it belongs in `OnInputValidated`, which
  runs on the trimmed text and hides the window itself. `OnInputChanged` must still be non-null
  (`Gui.AssertNotNull`).
- **`GuiManager.ModalOnTop` is the game's record of the topmost SHOWN modal** (:310, written
  :1750-1765). An exclusive stack WITHDRAWS the window underneath (its `Shown` goes false), and
  `ModalOnTop` is null during a modal's own close — the frame on which a window's own flag and
  this record disagree.
- **The icon-strip screens are engine-exclusive.** `BackgroundRenderer` carries
  `GuiWindowsStackExclusive` — a prefab component, no code assigns it — so no two strip screens
  can be shown at once, which is what lets them share one mod layer.
- **`AgeScreen.SortingOrder` is the engine's own draw ladder** (Label 0 … ModalRenderer 5 …
  OverlayRenderer 6). It puts a NOTIFICATION under a modal, which is why a notification layer
  belongs below every modal rather than above them.
- **A `GuiModalWindow` with `HideGuiBehind` SWITCHES OFF every age screen behind its own.**
  `OnEndShow` calls `HideAllAgeScreensBehind`, which writes `Root.Visible = false` on each screen
  before its own in `GuiManager.ageScreens` (`GuiModalWindow.cs:52-99`, `GuiManager.cs:2386-2500`),
  and `AgeTransform.UpdateHierarchy` returns at once on an invisible root — so nothing under those
  screens advances a modifier, never mind draws. **Consequence, measured 2026-08-23:** the options
  window (the game's own as much as a clone of it) is on **`OverlayRenderer`**, which is the LAST
  screen, so while it is up the whole **`ModalRenderer`** stack is frozen. The rename box lives
  there: `RequestNewName` shows it, and it sits at `Alpha 0`, `Enable false`, `ModifiersRunning
  true` forever — never `IsReady`. `ShowAllAgeScreensBehind` + `EnableAllAgeScreensBehind` for the
  box's duration unfreezes it, and both calls are the game's own. What that cannot fix is DRAWING:
  `OverlayRenderer` sorts above `ModalRenderer`, so the box comes up behind the settings window's
  opaque background — operable and readable, invisible.
- Which stack each window is on (probed, not read off the XML): `OptionsModalWindow`,
  `MessageBoxWindow` and `GameMenuModalWindow` are all on `OverlayRenderer` (a plain
  `GuiWindowsStack`, 19 windows); `RenameModalWindow` is on `ModalRenderer` (`GuiWindowsStackModal`,
  the exclusive one). That is why the game's own confirmation box stacks over the options window and
  the rename box cannot. Because the box and the options window share a plain stack there is no
  push/pop of the window behind and no deferred `Visible = false` — measured after a reported
  vanish: answering the overlap box with Confirm, with Cancel, and answering the reset box, each
  left `window.Shown == true` with the cursor back on the control that raised it.
- **Two `GuiModalWindow`s on ONE `GuiWindowsStack` both draw, and the second one shown does not
  come to the front.** Both windows report `Shown`, `Visible` and `Alpha 1`; only one is on screen
  and the other is behind its opaque background, operable but invisible (hiding the front one
  revealed the back one focused, with its own tab selected). `transform.SetAsLastSibling()` on the
  back one changed nothing — AGE does not draw a screen's windows in sibling order. Nor does
  `HideGuiBehind` separate them: it hides screens BEHIND the window's own, and two windows on one
  stack share a screen.
- **A window's own `HandleInput` override can turn its Cancel button into a Confirm.**
  `GuiModalWindow.OnCancelCb` is nothing but `HandleInput(InputAction.Exit)`, so any window that
  overrides Exit to mean something other than "dismiss" silently changes what its Cancel button
  does — and the game's tooltip on that button goes on saying the old thing. Read the override
  before trusting either the key or the button.
- **`ContextualPromptWindow` has NO keyboard dismissal of its own**: it declares no `HandleInput`, and
  `ScanOverlayWindow` — which is up whenever the prompt is — swallows Exit, so the game's only ways
  out are the cross, a right click and a click away. A mod screen over it has to supply the close
  itself.
- **`GameMenuModalWindow.Title` holds `%GameMenuModalWindowTitle`, localizing to "Game\nMenu"** —
  the only place the pause menu names itself; the mod joins the two drawn lines with a space.
- **Tutorial pages declare their own draw layer**: `TutorialPopupLayer` is per-page, and 49 of
  233 pages declare one ABOVE modals — so a tutorial screen has to sit near the top of a mod's
  ladder, with only the error and message boxes above it.
- **A window's `AgeTransform.Enable` lags its `Shown`/`IsReady` by a frame or two** (measured:
  `shown=True ready=False windowEnable=False`, both true two frames later), and the same lag
  exists for a PANEL swapped inside a window that never closes (the faction chooser under the
  custom faction editor). A screen whose arrival needs enablement gates on `Operable` — window
  AND swapped panel — where it has shown the symptom (every control reading "unavailable" once);
  the modal family's plain `Shown && IsReady` gate is otherwise correct as-is.
- **A just-shown AGE panel is `Shown && Visible && !Enable` for ~7 frames (~485 ms measured), and a
  switched-off panel makes every control in it read refused.** Measured 2026-08-24 with a background
  `POST /wait` on the specialization chooser: a screen that takes the panel over in that window
  announces "unavailable" on lines nothing is refusing, and a live watch never takes the word back
  — the corrected state is an empty part, which speaks as silence. Mod policy: a screen polled on a
  game panel gates its `IsActive` on `AgeWidgets.Operable(panel.AgeTransform)`, not on Shown/Visible
  alone (`PlanetConstructiblesScreen`, `TableFilterScreen` are the precedents).
- **A screen whose own buttons rebuild its window must split arrival from departure.** Merging two
  fleets destroys both and builds a third; the window goes not-ready for a frame or two, and a plain
  `Shown && IsReady` gate stood the screen down mid-order — the transcript read "Galaxy" plus the
  whole HUD, then the panel again. Asking `IsReady` once on the way in and only `Shown` thereafter
  (one instance bool) leaves the Garrison order's transcript as two live "unavailable" parts and
  nothing else. Before/after measured on merge vs garrison in the same run.
- **A page comes back ONE FRAME before its own content does**, and that frame is where a cursor
  goes missing. Measured with a per-frame trace (`DevProbe.Trace`) across a technology-wheel round
  trip from the star system page in `unlocked`: f+0 the wheel's window shows (`IsAnyScreenVisible`)
  and the star system screen stops declaring; f+1..3 NO mod screen is active at all (the handover
  gap, three frames); the wheel runs; on the way back three more empty frames, then **f=N
  `screen.star-system` is active declaring 3 nodes, f=N+1 declaring 78**. The three are the shared
  HUD strip (`Screen.BuildShared`'s collapsed-tutorial bar), because `SystemManagementScreen.Build`
  returns early until its planet cards exist while `IsActive` (view level + no modal + window shown)
  is already true. The tutorial SCREEN never enters the stack at any point in the trip — the
  cursor's landing on "Close tutorial" was the shared strip being the whole render for one frame,
  not layer 98 taking focus. The mod policy that follows: shared contributions are skipped when the
  page declared nothing, so "nothing here yet" stays an empty render.
- **`GuiBehaviour.AgeTransform` and `AgeTransform.AgeTooltip` are Awake-cached** — NULL on prefabs;
  instantiate before touching either.
- **`GuiManager.ShownGuiPanels` leaks (parked, unanalysed).** Measured on the turn-4 fixture while
  the planet page was up: the collection held ~80 `FleetsScreen` entries and 2 `StarSystemScreen`
  entries — panels shown without a matching hide, accumulating over a session. Nothing the mod does
  reads that collection, and no symptom has been traced to it; noted here so the next investigation
  starts from a measurement rather than from a fresh surprise.

## Tables, pools and clicks

- **`Visible` is not "drawn", and AGE has three separate retirement styles.**
  1. `AgeTransform.RefreshChildrenIList/Array` leaves the surplus children of a pooled table (a
     competitor slot an empire count no longer needs) flagged `Visible` with `Alpha == 0`.
  2. `ReserveChildren` tables retire rows by FADING them — alpha 0, `Visible` still true.
  3. The scan view's pool parks stale children fully visible outside the table's extents.

  Ask about alpha too — but only `== 0`, since a read-only setting is faded to 0.5 and is still
  drawn. Every per-row read gates on painted-ness. A retired row also keeps its old RECT, which is
  how the planet card's climate table (`PlanetGameplayTypeTable` — the one table on that card whose
  `Load` does NOT set `StrictVisibility`) put the previous planet's biodiversity line on top of the
  curiosity line and banded the two into one drawn row: a faded row is a layout hazard as well as a
  phantom line. A parked item also keeps its old tooltip `Target` wrapper, so a name-by-wrapper
  read resurrects the PREVIOUS binding's name (the galaxy planet card spoke another planet's
  "Dustciduous Trees" deposit); `AgeWidgets.ItemText` enforces the alpha gate centrally, so every
  table read that names items through it is covered. A pooled table can also be retired
  WHOLESALE with its rows left painted: `PlanetLabel_SystemOrbital.RefreshPlanetCuriosities`
  (:1090-1103) sets `PlanetCuriositiesTable.Visible = remaining.Count > 0` and RETURNS before
  refreshing the children, so a planet whose last curiosity was just expedited keeps a child at
  `Visible true, Alpha 1` inside a hidden table (measured on Ita II the turn its expedition
  landed). The table's own visibility is the first gate and painted-ness the second — together
  they are exactly what the engine's own `AgeTransform.GetVisibleChildrenCount` counts
  (`Visible && (StrictVisibility || Alpha > 0)`, :2549-2561), which is the free oracle for any
  count a mod speaks off such a table. The same alpha-0 retirement shows up outside tables too:
  `GuiEffectMapper.UnloadEffects` retires effect lines that way.
- **A `GuiTable` line is a POOL SLOT, not a row.** `LineNNN` names (and positions) are reassigned
  whenever the table refreshes or re-sorts, so a cursor keyed on either sits on a different thing
  a frame later — measured: picking a trait in the custom-faction editor left the next Enter
  picking whatever the re-sort moved under the cursor. Key a line on `GuiTableLine.Data`; with it
  as `ControlId.Referenced` the cursor even follows an entry from one table into the other.
- **`GuiTable`'s can-select flag is recorded as `LinesTable.Enable`** (`GuiTable.Bind` :130, its
  only writer), so an ancestor-walking "is it operable" test conflates read-only with refused. A
  refused ROW is the line's own `AgeTransform.Enable` (`GuiTableEntry.OnBind` :22-27).
- **`GuiTableLine.OnLineSelectionCb` clears `ClickedCell` AFTER notifying** — read the cell,
  then the line, in that order, or the cell is already gone.
- **`GuiRadioGroup` rewires its child toggles and ignores `State`** — the group is the authority,
  not the toggle it holds.
- **The custom faction editor's trait tables declare column `Filters` in the XML that the game never
  draws**: their `GuiTableHeader` prefab wires no `FilterToggle` widget, and `GuiTableHeader.Refresh`
  guards on `FilterToggle != null` — so the journal's `EndGameSummaryTable` is the one table whose
  funnel toggles actually exist (measured 2026-08-24).
- **A table cell in a non-primary column has no backing object.** The mod's `GraphSheet` gives the
  row's reference to column 0 alone (identity is per cell), and scrolling followed that reference —
  so `ui.end` down the KEY column of the 57-row Controls table left the table unscrolled
  (`OptionsTable` global y stayed 170 with the landed row far below the 468px viewport). Fixed
  generally: a non-primary cell carries the row as its `NodeVtable.ScrollAnchor`.
- **AGE clicks PROPAGATE.** `propagateInteraction` defaults true (`AgeControl.cs:19`) and
  `MouseUp` re-delivers up the chain (:170-192); the engine reaches the hit target by
  `SendMessage` (`AgeManager.cs:890` — where the click audio comes from) and the ancestors by a
  plain C# call, with no audio.
- **The click sound is an `AgeAudio` component on the widget's own transform** (posts
  `MouseUpEventID` via the gui audio proxy, `AgeAudio.MouseUp` :191-197, on the engine's
  mouse dispatch — never from the handler). `AgeWidgets.Click` posts the component's down/up
  before dispatching; the generic rule is widgets.md's "a click is more than its handler".
- **`SendMessage` arity is not forgiving, and it bites BOTH ways.** Most game callbacks take
  `(GameObject obj = null)`, but not all (`OnPreviousHullCb()`/`OnNextHullCb()`,
  `ConstructionCompletedNotificationLine.OnSelectSystemCb()`), and `DontRequireReceiver` swallows
  the mismatch silently:
  - `SendMessage(name, sender)` does not reach a zero-argument handler — an `AgeControlButton`
    sends the sender, so such a row simply does nothing.
  - `SendMessage("Cb")` on a handler declared `Cb(GameObject obj = null)` logs "Calling function …
    with no parameters but the function requires 1" and does nothing (measured on
    `OutpostInfoSidePanel.OnClickChangeColonyCb`). **A C# default argument does not save you**:
    `OnShowLocationCb(GameObject obj = null)` compiles to BOTH arities on this runtime (measured:
    `GetMethods` over `QuestBegunNotificationWindow` returns a 0-parameter and a 1-parameter
    `OnShowLocationCb`, both declared by that type), and `SendMessage` resolves by NAME and then
    insists on the arity it found. So "takes no argument" must mean a zero-parameter overload AND
    no one-parameter one (`AgeWidgets.TakesNoArgument`).
  - A double-click handler is the same trap the other way round: `OnLineDoubleClickCb` takes NO
    argument while the engine dispatches with one — and `MilitaryScreen.OnLineDoubleClick` then acts
    on `SelectedFleet`, not on the line it was passed, so the row must be selected first for the
    replay to mean anything.

  `AgeWidgets.Press`/`Toggle`/`Choose`/`Send` resolve the arity (cached) and pick the overload, and
  are the only safe pressing route; the generic rule is widgets.md's arity contract. From `/eval`,
  invoking a private handler by reflection with an explicit `new object[]{ null }` is the reliable
  route.

## Tooltips

`AgeTooltip` is attached to the widget (`someAgeTransform.AgeTooltip`); its `.Content` (often a
`%key`) is populated at bind time, long before hover, so tooltip text can be read without ever
showing the tooltip window.

Tooltips come in two tiers (verified against the tooltip pipeline and concrete bind sites):

- **Simple** (`.Class` unset → `"Simple"` → `PanelFeatureSimple`, whose whole body is
  `SimpleLabel.Text = content`): `.Content` alone is the complete tooltip. All outgame
  tooltips are this tier — main menu items, options rows (`SettingItem`, `OptionsTabToggle`,
  `OptionsModalWindow` buttons).
- **Rich** (`.Class` set — Technology, Improvement, Ship, and dozens more): `.Content` is
  empty (Technology) or a bare title (Improvement, Ship). The substance — description, costs,
  turns, stat effects, hull breakdowns — is generated by **149** `PanelFeature*` classes
  (counted in `decompiled/Assembly-CSharp`, 2026-08-26; 117 of them derive straight from
  `GuiPanelFeature`, which is where the older "~117" figure came from) reading
  provider interfaces (`ITitleProvider`, `IDescriptionProvider`, `IFinalCostsProvider`,
  `IHullInfoProvider`, `IDescriptorEffectProvider`, …) off `.Target`/`.Context`. Those are
  also populated at bind time and readable headlessly, so a faithful spoken rendition queries
  the providers directly (the `ImageInformationWindow` pattern) — speaking `.Content` alone
  would miss essentially everything in a rich tooltip.

Tooltip **positioning**: most tooltips are `AgeTooltipAnchorMode.FREE` — the window is drawn
at `AgeManager.Cursor` plus an offset and clamped on-screen
(`GuiTooltipController.ComputeWindowPosition` / `EnsureWholeTooltipIsOnScreen`); the anchor
rect is never consulted. When showing a tooltip for *keyboard* focus, re-anchor it or it
renders wherever the idle mouse is parked: set `AgeTooltip.AnchorMode` to an anchored mode
(`BOTTOM_LEFT` is the game's common placement) **and set `Anchor` explicitly** — a null
anchor is substituted with the controller's `DefaultAnchor`, a marker sitting at the
bottom-left screen corner. Save and restore both fields when focus leaves (see
`PointerFocus`).

There are **no tooltip→tooltip links and no encyclopedia**: label `[...]` markup is icon-glyph
substitution only, `#RRGGBB#` is inline color, and nothing rendered inside a tooltip is
clickable — so a "read tooltip" key can speak rather than navigate. Narrow exception: a few
widgets inside rich tooltips (political-opinion rows, `IngredientSlot`s, FIDSI icons) carry
their own `AgeTooltip`, which the game shows by hover *replacement* — extra `Target`-driven
data a verbose mode could append, not a navigation model, and in practice unreachable (the
nested-tooltip bullet below).

Measured behaviour of the drawing pipeline:

- **A block caption's WORD and its EXPLANATION sit on different widgets, and the prefabs repeat the
  group's name.** The recurring AGE shape is `…Group / TitleGroup [tooltip] / SomeTitle [text]` — the
  sentence is on the wrapper, the words on the label inside it. Measured 2026-08-22 on
  `MinorFactionDiplomacyModalWindow` (`RelationInfo/TitleGroup` = "Displays information about your
  relation with the Minor Civilization" over `RelationInfoTitle` "Diplomatic Relation";
  `ActionsGroup/TitleGroup` over `ActionsTitle` "Actions"), on `PopulationModalWindow`
  (`CollectionUnlockGroup` = `%CollectionUnlockGroupDescription` over `Title` "Collection status"),
  and — tier-zero, from the unshown prefabs — identically on `AcademyDiplomacyModalWindow` and
  `PirateDiplomacyModalWindow`. Two consequences for a reader. `AgeWidgets.TextOf` descends, so passing
  the GROUP to `Captions.Push` gets both halves in one widget; passing the LABEL silently loses the
  sentence (the parity audit files it under `decoration`, and nothing in speech says so). And the name
  `TitleGroup` is worn by three different groups in one window, so `AgeWidgets.ChildNamed` by that name
  answers with whichever the walk reaches first — on the minor window that was the faction banner,
  which named the relation panel "Niris". **Mod policy**: resolve such a caption from the UNIQUE label
  name and take its `Parent`, never by the group's own name.
- **A failed tooltip request is PARKED for 999 seconds, and only a change of hovered transform
  lifts it.** `GuiTooltipController.Update` (Amplitude.Unity.Gui/GuiTooltipController.cs:214-224):
  when the hover delay elapses and `ReadTooltipInformation()` says no — which it does when the
  tooltip has neither `Content` nor `Target` (:235) — the controller writes
  `timeBeforeShowingTooltip = 999f` instead of retrying. Two things, and only these two, re-ask:
  `AgeManager.OverrolledTransform` becoming a DIFFERENT transform (:191), or the tooltip's
  `Target` being written again, which sets `AgeTooltip.DirtyTarget` and makes :186-190 drop the
  remembered transform. This is a mouse-shaped design — a hand moves, so the edge always comes.
  `PointerFocus.LateTick` re-asserts the SAME transform every frame, so a keyboard user sitting
  still gets neither edge and the tooltip is suppressed for the whole 999 s. Measured live: a
  turn-start notification popup put focus on `notification:CompletedTechnologyTitle`, whose
  `AgeTooltip` carries neither content nor target; the controller parked and was still counting
  down (989 → 975) fifteen seconds later. **Mod policy:** never aim the pointer at a tooltip that
  has neither content nor target, and where the mod holds a hover it must re-issue the request
  itself when the window has not drawn — `AgeTooltip.DirtyTarget = true` is the engine's own
  re-ask signal and needs no reflection, but it resets the countdown, so it must be issued once
  per stall and never per frame.
- **There is exactly ONE tooltip window and one controller slot.** `GuiTooltipController` holds a
  single `CurrentTooltipWindow` (:34) and all 146 `GuiTooltipDescription` entries name the same
  `"TooltipWindow"`; moving the hover INSIDE a drawn tooltip REPLACES what is drawn (:191-195).
  So two tooltips can never be shown at once, and a row carrying several can only ever draw the
  one the pointer is sent to — which is why the aim is a declared property of a node
  (`NodeVtable.PointsAt`) rather than something anybody re-derives.
- **A tooltip drawn INSIDE another tooltip cannot be reached at all** (measured 2026-08-22).
  The population dossier's `PanelFeaturePoliticalOpinion` builds a `PoliticalOpinionLine` per party
  and writes a real `Politics`/`GuiPolitics` tooltip onto each (`RefreshPoliticalOpinionLine`), and
  those lines DO hold their target while the parent is drawn — probed live inside the drawn window:
  `Item000 cls=Politics tgt=GuiPolitics title=Industrialists`. But pointing at one replaces the
  tooltip that was drawing it (one `GuiTooltipController.CurrentTooltipWindow`, above), and
  the replacement runs `PanelFeaturePoliticalOpinion.Unbind`, whose `ReleaseData` clears exactly the
  target the new request needs: `DevProbe.Tooltip()` answers `shown:false`. There is no frame ordering
  that wins. **Mod policy:** a nested dossier is read off the game's own wrapper instead — the three
  panel features the `Politics` class is made of are three properties of `GuiPolitics` (`Title`,
  `CategoryTitle`/`Description`, `PoliticsAffectingEvents`), and the parties come off the same
  `IPoliticalOpinionProvider` the drawn block reads — and its node points at the PARENT tooltip so the
  picture stays the page the player is reading (`UI/PoliticsDossier.cs`).
- **An alpha-0 widget is not hit-testable for the mouse** (`AgeTransform.cs:3448`), so a subtree
  the game reveals on hover carries tooltips a mouse can reach only after the reveal — but
  `PointerFocus.LateTick` writes `OverrolledTransform` DIRECTLY, so the mod can point at one
  without the reveal. A parity walk that keeps the engine's own transparency gate cannot see them
  at all; the second pass with the gate off is the only way they are enumerable
  (`TooltipAudit`'s `hidden`).
- **Every tooltip is an ordered list of panel features.** `GuiTooltipWindow.DoBind` resolves
  the tooltip's `Class` through the description database and instantiates one prefab per
  feature under `PanelFeaturesTable`; a feature's SUB-features are added as further siblings in
  the same table, not nested, so the drawn tooltip is always one flat ordered list. `IsSeparator`
  and `IsSpacing` are on Assembly-CSharp's global `GuiPanelFeature`, not on the firstpass
  `Amplitude.Unity.Gui` base — a REPL probe typed to the latter cannot see them.
- **A panel feature can be a whole SECTION rather than a row.** `PanelFeatureModuleEffects` and
  `PanelFeatureHullInfo` build N instances of ONE prefab, and each instance is a complete section —
  heading and all — not a line of a list. A reader that treats the instances as repeated rows
  flattens several sections into one.
- **`CustomFactionDetailsPanel.cs:174` swaps a tooltip's content and class arguments**, so the
  tooltip ends up naming a CLASS that is really a sentence. `GuiTooltipController` looks the class
  up in the `GuiTooltipDescription` database and gives up when it is not there
  (Amplitude.Unity.Gui/GuiTooltipController.cs:239-249), so that tooltip is parked and draws
  nothing whatever points at it. A parity audit must bucket "the game has no description for this
  class" separately from its own findings — it is a defect in the GAME's data (`TooltipAudit`'s
  `undescribed`), and no mod change can make those words appear.
- **A prefab tooltip's `%…` Content can be a placeholder the game overwrites at bind**
  (`NegotiationModalWindow` swaps in the war/influence pressure title and description at bind) — a
  caption test that localizes the prefab key alone answers "no sentence" for a caption that has one.
- **A hint button's tooltip has three parts, in a fixed order**: the button's own description, then
  `"\n\n"` and the failure (`Gui.FormatFailure`, Gui.cs:1072), then — only for a missing technology —
  `"\n" + %MissingTechnologyClickDescription`, appended by `Gui.FormatButtonHint` (Gui.cs:1207).
  So the refusal alone is lines[1..] minus that instruction, which is what `RefusalText.Compose`
  does. Measured on Dusay I: "Colonize the planet…" / "Missing technology Maximized Exploitation" /
  "Hold Control+Click to locate this technology in the technology tree".
- **A hint-blocked button stays `Visible` AND `Enable`.** The game turns its click into
  "jump to the missing technology" instead of disabling it, so `Gui.IsHintActive(transform)`
  is the ONLY discriminator between an offerable button and a blocked one — never gate on
  `Enable`. `Gui.FormatButtonHint` FORCES `Enable = true` as it writes the hint, and only 6 of the
  16 prefabs using the mechanism happen to be honest about their own flag — so the question is
  asked per site, never inherited from a prefab that looked right.
- **The hint exists so a CLICK can explain itself, and that click is the only thing such a control
  does.** `Gui.FormatButtonHint` switches the control on; the click then asks `Gui.IsHintActive` and
  runs `Gui.ActivateHint`, which reads the Ctrl the player is PHYSICALLY holding and jumps the
  technology screen to the missing technology. `AgeWidgets.Offered` answers false for exactly that
  trick, so a mod's own Ctrl-chord fall back to the plain click would replay a no-op — the jump is
  therefore WIRED, once, in `Cells.Add` (`ES2Access/UI/Cells.cs`). A hint hanging off a CHILD widget rather than
  the declared one (the troop rows' locked type) is named by its own screen. The count on this
  install: **101 `GuiButtonHint` instances, 8 of them hint-active** (2026-08-13). One of the eight is
  the marketplace tab, which is hint-active while its own `Enable` is false — reachable from the
  keyboard through the hint even though the mouse cannot click it.
- **A drop list's own tooltip is wired to a message NOTHING receives.** `AgeControlDropList`
  keeps two per-item tables (`TooltipTable` strings, `TooltipTargetTable` targets) and they are
  MUTUALLY EXCLUSIVE: `SetTooltipTargets` clears the string table and vice versa (firstpass
  :255-275). Both are pushed onto the popup's ITEMS the moment the list is filled
  (`UpdateTooltipsInPopup` :532-545 → `AgeControlPopup.SetTooltips` :115-146, one
  `OnSetTooltip`/`OnSetTarget` per item), which is why an item's own `AgeTooltip` is the honest
  place to read an entry's description from whichever table the game used. The CLOSED control is
  a different story and a dead one: `SelectedItem` (:141-158) hands the selected entry's table
  value to `SetTooltip`, whose target overload sends `"OnSetTooltipTarget"` (:547-557) — a message
  no component in either assembly receives — while its string overload IGNORES the receiver it was
  given and sends to the drop list itself (:559-569). So a target-backed list's closed control says
  nothing, in the game as well as in a mod; there is nothing to fix from a mod and nothing to
  invent. Seven game sites use the target table (the ship-hull list and five custom-faction lists).
- In this install the player's registry `TooltipDisplayDelay` is genuinely **0.0**, so
  `TooltipDelay(-1)` restoring to 0 is correct, not a leaked override (`RegisteredTooltipDelay()`
  reads `Application.Registry` and agrees).

## Icons and picture captions

- ES2's icon numbers, for re-verification: 382 registered tokens (single writer
  `AgeManager.CreateSpecialCharactersDictionary` → `AgePrimitiveLabel.SpecialCharacters`,
  keys `"[TOKEN]"` upper-cased), 371 named + 11 nameless colour directives; localization
  corpus 25 821 strings, 1 861 with brackets.
- **A tooltip names its bare numbers out of TWO registries, and only one of them is the icon
  table.** A figure the panel draws beside a picture is captioned by that picture, but the
  pictures come in two kinds: an inline `[token]` inside a label's own text, which
  `IconNames`/`IconTable` resolves, and a standalone `AgePrimitiveImage` the feature binds from a
  field of its own (`HealthIcon`, `MovementPointsIcon`, `ActionPointIcon`, `CommandPointsIcon`,
  the `ValueDuplet.Symbol` of each ship-size count). The second kind is not markup and never
  reaches the token table, so its word is not there to be found: it is the STAT's own title in the
  game's element database (`Gui.GetTitle` + the `"%"+name+"Title"` fallback) or a `%…Title` key
  the game already uses for that column — `%ShipStat*Title`, `%ActionPointTitle`,
  `%ShipSize*Title`, `%FleetListTableCommandPointsTitle`. `DevProbe.UnknownIcons()` is silent
  about these by design; the symptom is a spoken line that is only figures, and the fix is a typed
  reader, never a new icon row.
- **A few symbols are painted straight into a panel and so are missing from the element-derived
  picture table.** `TurnSymbol` — the hourglass the construction-completed table draws in front of
  a build's remaining turns — is drawn by an `AgePrimitiveImage` that no `GuiElement` carries a
  token for, so the derivation that built `IconTable.PictureRows` never saw it and
  `DevProbe.UnknownIcons` listed it under `pictures`. It is now a HAND-WRITTEN row
  (`TURNSYMBOL=icon.turn`) and a regeneration must keep it: the picture is the only caption its
  number has, and it is what tells the mod that "3" in that column means turns.

## Cloning a game window

The options modal can be instantiated a second time and given a mod subclass of its component; the
mod's live implementation is `ES2Access/UI/ModOptions/`, whose class comments carry the recipe and
the reasoning. What belongs here is what the ENGINE does, which any second mod would meet.

- **Showing needs no registry; ESCAPE does.** `ShowWindow`/`HideWindow` reach a window
  through its own `GuiWindowsStack` and work unregistered, but `GuiManager.HandleInput`
  dispatches every `InputAction` by walking the private `guiWindowsFromBackToFront`
  (`Assembly-CSharp/GuiManager.cs:2058-2063`) **from the END backwards**, so the LAST entry is
  asked first and an unregistered clone never sees Escape at all — the galaxy's Escape opens the
  pause menu behind it (`:2123`). A clone therefore goes into BOTH `GuiWindowsStack.guiWindows`
  and `GuiManager.guiWindowsFromBackToFront` (both `protected List<GuiWindow>`), and comes out of
  both by NAME — and it must be inserted BESIDE the window it was cloned from rather than
  appended, or it is asked ahead of the message box it raises itself (`ModOptions.Beside`).
- **`GetWindow<T>` can never answer with the clone**: `Amplitude.Unity.Gui.GuiManager` keys
  that lookup on `guiWindowsByType` with the EXACT type (:154-165), and the clone's type is
  the mod's subclass. So "the clone if it is shown, else the game's" resolves with no ambiguity.
- **Five engine members are load-bearing for a clone**, all internal or protected:
  `GuiWindow.Initialize(stack, outGame)`, the `Loaded` setter, the `Name` setter, the
  `GuiWindowsStack` getter, and the two lists above. Nothing starts a clone's `Load()` —
  `GuiManager.LoadGuiWindows` ran at boot — so the mod starts the coroutine itself and sets
  `Loaded = true` at once (`ShowWindow` refuses a window that says it is not loaded).
- **The manager's MODAL registry is a third registration, and it is built once.**
  `GuiManager.Load_IGuiGamePanelService` walks `guiWindowsFromBackToFront` at boot, adds every
  `GuiModalWindow` to the private `guiModalWindows` and subscribes `ModalWindow_VisibilityChanged`
  to each one's public `VisibilityChanged`. A clone built afterwards is in neither, so
  `IsAnyModalVisible` and `ModalOnTop` stay as if nothing were open — and `IsAnyModalVisible` is
  what the game weighs the tutorial popup against
  (`TutorialPopupPanel.UpdateLayerAndVisibilityAccordingToOtherWindows`), what `CanToggleScanView`
  asks, and what `AddTutorialKeysIFN` stands down for.
- **Component swap on a prefab clone works, serialized references do not follow it.**
  `AddComponent` of a hot-reloaded-assembly subclass on the instantiated prefab is fine, but
  every declared instance field must be copied from the original component (walking
  `OptionsModalWindow` up to but not including `MonoBehaviour`) before `DestroyImmediate`.
- **A runtime change destroys every window in the stack**, the clone included
  (`GuiWindowsStack.DestroyWindows`), so a clone needs no `IRuntimeService.RuntimeChange`
  subscription — a per-frame Unity null check and a rebuild is the whole of it.
- **An `OptionItem`'s identity within a page is the ROW's name, not the option's property
  name.** The game names each row `<index><property><kind>`; fifty rows minted from one
  interface property share a property name, and keying on it collapses a whole page into one
  duplicate `ControlId`. `item.name` is the identity.
- **`OptionsModalWindow` ties four behaviours to the literal category name `"Controls"`**, and
  nothing else configures them: `OpenCategory` :119-120 and `OnBeginShow` :223-224 show
  `ResetButton`/`ResetInGameButton` only for that name, `HandleInput` :66-68 picks
  `%BindingExitWithoutApplyMessage` over `%OptionExitWithoutApplyMessage` for it, and
  `OptionsTabToggle.Initialize` draws the tab with `%OptionToggle<name>Title` and its tooltip with
  `%OptionToggle<name>Description` ("Set key bindings"). A clone that names its key-binding
  category "Controls" gets all four in every language the game ships.
- **The window's reset buttons reset the GAME's bindings** (`OnResetCb` → the
  `%OptionBindingResetConfirmation` box → `OnResetConfirmation` :353-361 →
  `IInputOptionsService.ResetToDefaultBindings`), which on a clone would silently rewrite a page it
  is not showing. Every AGE button dispatches by `SendMessage(OnActivateMethod)` to
  `OnActivateObject`, so re-aiming one is two field writes.
- **The tab bar's width is `(TogglesTable.Width − HorizontalSpacing × (n−1)) / n`**
  (`OptionsModalWindow.AddCategoryToggleAndPanel` :246). Measured on a clone: `Width` 600,
  `HorizontalSpacing` 0 — so five tabs draw at 120px each (the game's own six at 100px) and
  fourteen would draw at 42px.
- **A button cloned out of the window's bar has three children — `Circle`, `Icon`, `Label`.**
  Hiding `Icon` leaves the round frame that says "button" without the cross that says "cancel".
  Re-parented into a rows table it must be given the ROW's anchoring (`AttachLeft` and
  `AttachRight` true, `AttachTop` and `AttachBottom` false) or the table leaves every row stacked
  at the same y — the bar pins it to the bar's own corners.
- **A row action that changes which panel is showing must re-seat a keyboard cursor itself**, because
  focusing a tab IS switching to it: otherwise the row the cursor was standing on is destroyed, the
  navigator re-seats onto a tab, and the landing switches the page back (measured 2026-08-24).
- `GetMethod("OnCancelCb", Instance|NonPublic)` is AMBIGUOUS on `OptionsModalWindow`
  (`GuiModalWindow.OnCancelCb(GameObject)` is the second overload) — pass `Type.EmptyTypes`.

## Worked example: the main menu

- Screen: `Assembly-CSharp/MainMenuScreen.cs` (`MainMenuScreen : GuiWindow, IInputHandler`).
- Entries defined by data: `MainMenuScreenGuiElement` (`Entry[]`, names like `MainMenuNewGame`).
- Items: `MainMenuItem` (fields `Button`, `TitleLabel`, `Tooltip`, `SubItemsContainer`) and
  `MainMenuSubItem` for flyout entries. `Bind` sets `TitleLabel.Text = Gui.GetTitle(entry.Name)`
  and `Tooltip.Content = Gui.GetDescription(entry.Name)` (both raw `%keys` here).
- Click dispatch (verified): `MainMenuItem.OnClickCb` →
  `mainMenuScreen.gameObject.SendMessage("OnClick" + MainMenuEntry.Name, DontRequireReceiver)`.
- Handlers: one `OnClickMainMenu<Entry>()` method per entry on `MainMenuScreen`
  (`OnClickMainMenuNewGame`, `...LoadGame`, `...Settings`, `...Exit`, …) — invoke these
  directly (reflection) for deterministic activation, after checking `screen.IsReady` and the
  item's `AgeTransform.Enable`.
