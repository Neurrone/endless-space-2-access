# ES2 GUI framework (AGE) — reverse-engineering notes

Documents how the game's UI works, not how the mod works. Sources are cited as
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

## Tooltips

`AgeTooltip` is attached to the widget (`someAgeTransform.AgeTooltip`); its `.Content` (often a
`%key`) is populated at bind time, long before hover, so tooltip text can be read without ever
showing the tooltip window. Display goes through `GuiTooltipWindow.Bind(...)` composing
`GuiPanelFeature`s from a `GuiTooltipDescription` template. Disabled-button explanations get
appended into `AgeTooltip.Content` by `Gui.FormatButtonHint` — surface these when announcing
disabled controls.

Tooltips come in two tiers (verified against the tooltip pipeline and concrete bind sites):

- **Simple** (`.Class` unset → `"Simple"` → `PanelFeatureSimple`, whose whole body is
  `SimpleLabel.Text = content`): `.Content` alone is the complete tooltip. All outgame
  tooltips are this tier — main menu items, options rows (`SettingItem`, `OptionsTabToggle`,
  `OptionsModalWindow` buttons).
- **Rich** (`.Class` set — Technology, Improvement, Ship, and dozens more): `.Content` is
  empty (Technology) or a bare title (Improvement, Ship). The substance — description, costs,
  turns, stat effects, hull breakdowns — is generated by ~117 `PanelFeature*` classes reading
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
data a verbose mode could append, not a navigation model.

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
   `es2-architecture.md`).
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
  frame (only `Mouse0` forbidden — Escape IS bindable, hence a genuine engine-side race
  between "cancel capture" and "bind Escape"); max 2 simultaneous keys; **capture ends on the
  first key release** (`OnValidateCb` → focus cleared → `OnLoseFocusCb` applies if the combo
  differs from both current bindings, raising `%OptionBindingAlreadyBindedConfirmation` on
  conflicts). The activating keystroke's own release therefore kills a capture started
  synchronously — defer the focus handover until two consecutive frames with no key held.
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
  fallback `"######0"`). No ES2 option uses `OptionTextFieldItem`.
- **The window can be CLONED to host the mod's own settings** — a second instance whose
  component is a mod subclass overriding `Load()`. The recipe (prefab path, the component
  swap and field copy, the five reflected engine members, the two window registries Escape
  needs, minting rows from many providers, teardown by name) lives in
  `ES2Access/UI/ModOptions/ModOptions.cs`'s class comment and the facts behind it in
  `docs/es2-facts.md` ("The options window, cloned").

## Message boxes (`MessageBoxWindow`, `GuiManager.ShowMessage*`)

- No buttons enum: a button is shown iff its caption (`ValidateTitle`/`CancelTitle`/
  `AlternativeTitle`) is non-empty; captions live in sibling labels, not on the buttons.
  `ShowMessageWithTimeout` never clears `AlternativeTitle`, so a stale third button can leak
  onto a countdown box — declare from live visibility.
- `VisibilityChanged` fires (twice) before the captions are written; poll
  `Shown && IsReady` instead. Timeout expiry auto-fires **Cancel**, not OK. The countdown
  rewrites `MessageLabel.Text` every frame. `Gui.GuiService.HideWindow(w)` is the only legal
  hide (`GuiWindow.Show/Hide` throw). `MessageBoxNonBlockingWindow` is a separate,
  non-modal near-twin and is not covered by the modal-layer assumptions.

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
