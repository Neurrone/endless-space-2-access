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
- `AgeTooltip` — property returning the attached tooltip component (`GetComponent<AgeTooltip>()`)
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

## Enumerating and activating controls

Two proven strategies:

1. **Enumerate**: from any window root (`Gui.GuiService.GetWindow<T>().AgeTransform` or
   `GuiManager.VisibleScreen`), Unity's `GetComponentsInChildren<AgeControlButton>()` /
   `<AgePrimitiveLabel>()` etc.; filter on `AgeTransform.Visible && Enable`.
2. **Activate**: click handlers are wired in prefabs to `SendMessage("<HandlerName>", ...)` — the
   convention is `OnClickCb`-style private methods on the owning screen. Invoking the same
   handler method via reflection is byte-for-byte the mouse code path minus hover animation.
   Higher-fidelity simulation (`button.MouseEnter(...)` + `MouseDown(...)`) exists but is rarely
   needed. Prefer the underlying service/order call when identifiable (see
   `es2-architecture.md`).

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
