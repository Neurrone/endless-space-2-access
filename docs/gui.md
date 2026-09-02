# ES2 GUI framework (AGE) — reverse-engineering notes

## Core types

- **`AgeTransform.AgeTooltip` returns a field cached in `Awake` (`privateTooltip`), not a live
  `GetComponent`** (:387-395, :3772) — so a tooltip added to a widget at runtime is invisible to
  the engine and to every reader until that private field is set by reflection. Same caching on
  `AgeControl`, `AgePrimitive` and `GuiBehaviour.AgeTransform`, all NULL on prefabs.

## Window lifecycle and readiness

- **`GuiWindow.IsReady`** (`Visible && Enable && !ModifiersRunning`) is the screen-readiness gate;
  never announce or drive a window before it. One Harmony postfix on `NotifyVisibilityChanged`
  observes every window show/hide in the game.
- **The handover gap is frames, not instants.** Between an opener standing down and the new
  window passing `IsReady` no screen is focused and the mod is deaf — measured at **~4 frames**
  on the improvements modal.

## Reading text and localization

- **A localization miss ECHOES THE KEY, it does not resolve to empty.**
  `GuiLocalizationProxy.LocalizeString` passes `defaultValue: key`
  (`Amplitude.Unity.Gui.Proxies/GuiLocalizationProxy.cs:21`), so an unregistered `%OptionXxxTitle`
  is DRAWN AND SPOKEN raw — while a plain (non-`%`) string is drawn as itself, so nothing a mod
  writes needs registering.
- **AGE localizes label text itself**, so assigning a raw `%key` still DRAWS localized
  (`AgePrimitiveLabel.cs:702-717`) — a drawn label is no evidence that a mod's own lookup would
  have resolved.
- **`Gui.GetTitle` can hand back a key that has no translation** (`ShipStatCommandPoints` declares
  `%ShipStatCommandsTitle`, gone from the corpus; the `"%" + name + "Title"` convention resolves
  it), and `Gui.GetLocalizedTitle` answers for a missing element with a PINK DESIGNER PLACEHOLDER
  or the raw `%Key` rather than failing. Every title read this way is tested before it is spoken,
  with silence as the last resort.
- **A typewriter label already holds all its words.** `AgeModifierTypewriter` sets the whole string
  once and advances `CurrentLine`/`CurrentCharInLine`, which only the RENDERER honours — so
  `AgeText.Label` on a mid-animation label is complete and no announcer has to beat the animation.
- **A refusal's own wording is keyed by its flag**: `%Failure<flag>Description`, lookupable from
  the flag alone with no tooltip drawn.
- **A drawn label may be an ELLIPSIS of itself, closed with a period rather than "…"**
  (`AgePrimitiveLabel.ComputeText_AutoTruncateIfNecessary` :720-727 → `AgeUtils.TruncateString`
  :414-430), so a reader that takes the drawn string speaks the column width. `AutoTruncate` is
  per-label, not per-screen: 1787 of this game's 7523 labels carry it and it fires on whichever the
  layout squeezes (85 at once on the empire page, 2026-08-29). **Mod policy: a truncation is a
  rendering artifact and is never spoken** — `AgeText.Label` detects it by MEASUREMENT (drawn string
  ends in the truncation character and is a strict prefix of the assigned one) and reads the
  assigned text.
- **The second kind of truncation cannot be detected that way, because the game truncates BEFORE
  assigning**: `GetFullTitle(label, wordWrap)` composes against the label's width (`GuiShipDesign`
  :766-781, `GuiBattleShip` :395-415, `AdItem.Bind`, `EmpireBanner`, `PlanetLabel` :228), so the
  whole name never reaches `Text`. Ask the MODEL — `GetFullTitle(null, true)`; `GuiBattleShip`'s
  overload dereferences the label unless word wrap is passed.

## Enumerating and activating controls

- Buttons with an empty `OnActivateMethod` are decorative click-shields (`BackgroundGroup*`); a
  press that replays the prefab wiring must skip them.

## Input ownership (`InputManager`, `AgeManager.FocusedControl`)

Escape and RightClick are consumed — focus cleared, nothing reaching the modal underneath —
whenever `FocusedControl` is `IsKeyExclusive` and `StandardCancel` (`InputManager.HandleInput`
:1210). That is the mechanism to hand a widget when the mod must stop the game acting on a key.

- `AgeControlDropList`: `PopupMenu.SetSelection(i)` moves the highlight only — it never writes
  `SelectedItem`; entry labels live in `LabelTable` as **raw `%` loc keys** (only Resolution ships
  plain strings); per-entry enabled state is `PopupMenu.Table.Children[i].Enable`.
- `AgeControlKeyBindingField`: **capture ends on the first key release**, so the activating
  keystroke's own release kills a capture started synchronously — defer the handover until two
  consecutive frames with no key held. `KeyCombination(List<KeyCode>)` splits modifiers out only
  when there is more than one key, so a three-key chord reads out correctly and cannot be
  re-captured as itself.
  - **A field losing focus while it holds nothing only sometimes clears the binding.**
    `OnLoseFocusCb` (:80-98) commits the blank only when it differs from BOTH of the row's
    combinations: `ui.up` (no secondary) survived a focus-null round trip untouched, while
    `ui.activate` (Return + KeypadEnter) came back as `ui.activate: , KeypadEnter`. The same
    equality check makes capturing a chord the row's OTHER slot already holds a silent no-op.
  - **Escape during a capture is that ending, and Escape can never be BOUND here**: `HandleInput`
    :1210-1226 runs in `Update` and nulls the focused control, while the field's own scan is in
    AgeManager's `LateUpdate`, so the field never sees the key. Mod policy (owner ruling
    2026-08-24, retiring an earlier restore-on-Escape): what the field does with the nothing it
    holds is the game's own business; the mod only reads the cell out afterwards, so a mod row and
    a game row behave identically.
  - **The clear path is the game's own value path**: `Option.Value = new InputBinding(...)`, then
    `window.OnOptionChanged(option)` and `item.Refresh()` — verified end to end on Controls.
  - **`OptionKeyMappingItem.OnChangeOptionValueConfirmation` is the one commit hook.** It fires
    once per commit, after the value is written, for game and mod rows alike, and NOT for Cancel's
    `RestoreSettings` or Reset to Defaults. Its `MessageBoxResultEventArgs` must be checked for
    `Ok`: it is also the conflict box's callback.
  - **Delete is bound to nothing in the game's 64 input options** (measured over
    `Option.GetOptions`); the only direct readers of the key are `DebugGOMouseMover`,
    `DirectorController` and `AgeControlTextArea`.
- `AgeControlScrollView`: `VirtualArea.Y` units are 1:1 with `GetGlobalPosition()` pixels
  (measured); replay `MouseWheel(increment)`, 300 × `ScrollFactorVertical` per increment.

## Options window (`OptionsModalWindow`)

- `Latent = true` options only hit the provider on Apply, which for Video triggers the 15 s
  `%OptionYouHaveNSecondsToValidateDescription` countdown. **Cancelling that countdown reverts the
  latent options and closes the whole window** — the game's own behaviour.
- **Two complete per-skin button bars** (`ButtonGroupInGame`/`ButtonGroupOutGame`): the window's
  public `ApplyButton`/`CancelButton`/`ResetButton`/`ResetInGameButton` fields all point at the
  **in-game** set, and Apply's `Enable` flag and tooltip are written to those instances regardless
  of skin — the drawn out-game Apply never receives them. Read availability from the field
  instance; press through the drawn button's own wiring.
- **No ES2 option ships as an `OptionTextFieldItem`, and the row type is broken**: the prefab's
  lose-focus callback does `base.Option.Value = TextFieldLabel;` (`OptionTextFieldItem.cs:30`) —
  the LABEL OBJECT, not its text — and `Option.SetValue` swallows the `InvalidCastException` as a
  logged error, so the row silently keeps its old value. The mod mints such rows and patches the
  commit (`OptionTextFieldCommit`).
- **`Option.Restore()` with a NULL backup does nothing for a string and logs for a bool**:
  `Convert.ChangeType(null, string)` answers null and the `obj != null` guard skips the write,
  while `null` to `bool` throws and is caught. So a row added AFTER `BackupSettings` reads
  `Changed == true` and Cancel simply does not touch it.

## Message boxes (`MessageBoxWindow`, `GuiManager.ShowMessage*`)

- A button's caption lives in a sibling label, not on the button, and `ShowMessageWithTimeout` never
  clears `AlternativeTitle`, so a stale third button can leak onto a countdown box — declare the
  buttons from live visibility.
- **`ShowMessage` on a box that is ALREADY shown swaps the answer and redraws nothing.**
  `GuiManager.ShowMessage` (:2303-2315) writes `Title`/`Message`/`ActiveEventHandler` and calls
  `ShowWindow`, a no-op for a shown window, while `MessageBoxWindow.OnBeginShow` (:71-98) is the
  only thing that copies those into the drawn labels — so confirming what is on screen can run
  another warning's handler (measured 2026-08-24).
- `VisibilityChanged` fires (twice) before the captions are written; poll `Shown && IsReady`
  instead. Timeout expiry auto-fires **Cancel**, not OK. `Gui.GuiService.HideWindow(w)` is the only
  legal hide (`GuiWindow.Show/Hide` throw). `MessageBoxNonBlockingWindow` is a non-modal near-twin
  outside the modal-layer assumptions.

## Focus, text fields and the engine's own keyboard

- **The game's own key names are a localized table** — `%KeyCode<Name>`, 120 rows in English and
  present in all ten languages (`Amplitude.Unity.Input/KeyCombination.LocalizeKeyCode` :194-208, a
  miss answering with the key itself). There are NO rows for the plain letter keys, which fall back
  to the engine's `KeyCode` name.
- **`InputManager` binds no letter keys at all** beyond `Ctrl+F` and the debug chords: Ctrl+H is
  `DebugSwitchHighDefinition` (:790), live only with `EnableModdingTools`; Ctrl+E is
  `DebugSendNewEvent` (:858) and NO handler answers it; Ctrl+I, Ctrl+N, Ctrl+G, Ctrl+T and
  Ctrl+Alt+E are bound to nothing.
- **The game's own end-turn key is the keypad Enter** (`InputManager.cs` :215), which the mod
  claims — so a mod user has no end-turn shortcut unless the mod gives them one.
  `EndTurnWindow.HandleInput` :637-654 puts an armed cursor back to `GalaxyCursor` before
  `TryToEndTurn`; a targeting mode left armed would otherwise eat the turn.
- **The previous/next pairs do not agree with each other.** `StarSystemScreen`'s pair (:26,28) is
  drawn only for the player's OWN systems, cycles with WRAP (`CycleStarSystemHelper` :180-197), and
  is also wired to the bare Right/Left arrows (:206-214) — keys the mod claims, which is why it was
  unreachable; `NotificationWindow`'s own Up/Down keys are wired the OTHER WAY ROUND from its
  buttons, so the mod follows the buttons; the academy's pair CLAMPs rather than wraps (:210-211).
- **The engine delivers keys to the focused control in `LateUpdate`** (`AgeManager.LateUpdate`
  :919-923), and `AgeControlTextField.KeyDown` (:76-81) polls `GetKeyDown` itself on top of that.
  With `RenameModalWindow.OnBeginShow` (:74-82) taking focus SYNCHRONOUSLY, one physical Enter
  could open the box, be delivered to the field, validate and hide it — a rename that committed
  nothing (`ES2Access/UI/Input/GameKeyboardHandover.cs` is the answer).
- **A text field stands the WHOLE mod layer down**; `AgeControlTextArea`, `AgeControlDropList` and
  the key-binding field are the only key-exclusive controls in the game.
- **The engine's Escape for a key-exclusive field only UNFOCUSES it** (`InputManager.cs`
  :1210-1241) — it never closes the surface around it, and meanwhile the engine swallows every
  other game hotkey (camera excepted). So a mod owns the exit from any keyboard it hands over.
- **Hiding an AGE window does NOT unfocus its text field** (measured: field `Visible=True`,
  window `Visible=False`), which kills a mod's key layer permanently — and it happens on the rename
  box's own hide path. `AgeManager.FocusedControl`'s setter runs FocusLoss/FocusGain (:277-301), so
  clearing it is the game's own hand-back.
- **A focused text field acts on Return itself, and the validate is the SCREEN's action, not the
  edit's**: `AgeControlTextField.KeyDown` (firstpass :76-89) fires the field's validate callback,
  which for `LoadSaveModalWindow` is `OnSaveNameTextFieldValidateCb` (:509-516) → `OnSaveCb()`,
  writing the save and closing the screen; and `InputManager.HandleInput` swallows `Validate` ONLY
  while a key-exclusive control holds focus, so on every other frame the window's own Validate
  handler is live. **Mod policy:** never hand a game text field the keyboard while the key that
  asked for the edit is still down, and while the mod owns a live edit it takes Return/KeypadEnter
  from the KeyDown dispatch and ends the edit itself — the chat box is the single exemption.

## Windows, layers and the modal stack

- **Every rename in the game is ONE box**: every opener funnels through
  `IGuiService.RequestNewName` (`GuiManager.cs:2364`), so a rename screen is written once and every
  opener inherits it.
- **`RenameModalWindow.CheckButtons` is DEAD in the shipped build** — its only caller,
  `StartProfanityFiltering`, compiles to an empty body, so `OnInputChanged` never runs and
  `ValidateButton.Enable` keeps the prefab's true. A refusal belongs in `OnInputValidated`, which
  runs on the trimmed text and hides the window itself; `OnInputChanged` must still be non-null.
- **`GuiManager.ModalOnTop` is the game's record of the topmost SHOWN modal** (:310, written
  :1750-1765). An exclusive stack WITHDRAWS the window underneath (its `Shown` goes false), and
  `ModalOnTop` is null during a modal's own close — the frame on which a window's own flag and this
  record disagree.
- **The icon-strip screens are engine-exclusive**: `BackgroundRenderer` carries
  `GuiWindowsStackExclusive` — a prefab component, no code assigns it — so no two strip screens can
  be shown at once, which is what lets them share one mod layer.
- **`AgeScreen.SortingOrder` is the engine's own draw ladder** (Label 0 … ModalRenderer 5 …
  OverlayRenderer 6). It puts a NOTIFICATION under a modal, so a notification layer belongs below
  every modal.
- **A `GuiModalWindow` with `HideGuiBehind` SWITCHES OFF every age screen behind its own**
  (`GuiModalWindow.cs:52-99` → `HideAllAgeScreensBehind`, `GuiManager.cs:2386-2500`), and
  `AgeTransform.UpdateHierarchy` returns at once on an invisible root — nothing under those screens
  advances a modifier. **Consequence, measured 2026-08-23:** the options window is on
  `OverlayRenderer`, the LAST screen, so while it is up the whole `ModalRenderer` stack is frozen
  and the rename box shown there sits at `Alpha 0`, `Enable false`, `ModifiersRunning true` forever,
  never `IsReady`. `ShowAllAgeScreensBehind` + `EnableAllAgeScreensBehind` unfreeze it; what that
  cannot fix is DRAWING — `OverlayRenderer` sorts above `ModalRenderer`, so the box comes up behind
  the settings window's opaque background, operable and readable but invisible.
- Which stack each window is on (probed, not read off the XML): `OptionsModalWindow`,
  `MessageBoxWindow` and `GameMenuModalWindow` on `OverlayRenderer` (a plain `GuiWindowsStack`, 19
  windows); `RenameModalWindow` on `ModalRenderer` (`GuiWindowsStackModal`, the exclusive one).
  Because the box and the options window share a plain stack there is no push/pop of the window
  behind: answering the overlap box either way, and the reset box, each left `window.Shown == true`
  with the cursor back on the control that raised it.
- **Two `GuiModalWindow`s on ONE `GuiWindowsStack` both draw, and the second one shown does not come
  to the front.** Both report `Shown`, `Visible` and `Alpha 1`; the other is behind an opaque
  background, operable but invisible. `SetAsLastSibling()` changed nothing — AGE does not draw a
  screen's windows in sibling order — and `HideGuiBehind` cannot separate them, since two windows on
  one stack share a screen.
- **A window's own `HandleInput` override can turn its Cancel button into a Confirm**, because
  `GuiModalWindow.OnCancelCb` is nothing but `HandleInput(InputAction.Exit)` — and the game's
  tooltip on that button goes on saying the old thing.
- **A window's drawn dismissal is wired to ONE OF TWO handler names, and which one is not in the
  class**: measured 2026-08-28, the diplomacy and academy modals and the negotiation window wire
  `OnCloseCb`, while `HeroInspectionModalWindow` and `LawsManagementModalWindow` wire `OnCancelCb`,
  and no window class exposes the button as a field. PRESSING the control is the whole of that
  window's Escape, confirmation branches included, and is the safe way to replay it (calling
  `HandleInput` directly wedged the screen stack once). `NarrativeScreen` draws no dismissal control
  at all.
- **`ContextualPromptWindow` has NO keyboard dismissal of its own**: it declares no `HandleInput`,
  and `ScanOverlayWindow` — up whenever the prompt is — swallows Exit, so a mod screen over it must
  supply the close itself.
- **Tutorial pages declare their own draw layer**: `TutorialPopupLayer` is per-page and 49 of 233
  pages declare one ABOVE modals, so a tutorial screen sits near the top of a mod's ladder.
- **A window's `AgeTransform.Enable` lags its `Shown`/`IsReady` by a frame or two** (measured
  `shown=True ready=False windowEnable=False`, both true two frames later), and the same lag exists
  for a PANEL swapped inside a window that never closes. Gate on `Operable` where that symptom has
  appeared; the modal family's plain `Shown && IsReady` gate is otherwise correct.
- **A just-shown AGE panel is `Shown && Visible && !Enable` for ~7 frames (~485 ms measured), and a
  switched-off panel makes every control in it read refused** — a screen taking the panel over in
  that window announces "unavailable" on lines nothing is refusing, and a live watch never takes
  the word back. Mod policy: gate `IsActive` on `AgeWidgets.Operable(panel.AgeTransform)`, not on
  Shown/Visible alone (`PlanetConstructiblesScreen`, `TableFilterScreen`).
- **A screen whose own buttons rebuild its window must split arrival from departure.** Merging two
  fleets destroys both and builds a third; the window goes not-ready for a frame or two and a plain
  `Shown && IsReady` gate stood the screen down mid-order. Ask `IsReady` once on the way in and only
  `Shown` thereafter.
- **A page comes back ONE FRAME before its own content does**, and that frame is where a cursor goes
  missing: measured per-frame across a technology-wheel round trip, **f=N `screen.star-system`
  declares 3 nodes, f=N+1 declares 78** — the three being the shared HUD strip, because
  `SystemManagementScreen.Build` returns early until its planet cards exist while `IsActive` is
  already true. Mod policy: shared contributions are skipped when the page declared nothing.
- **`GuiManager.ShownGuiPanels` leaks (parked, unanalysed)**: on the turn-4 fixture it held ~80
  `FleetsScreen` and 2 `StarSystemScreen` entries — panels shown without a matching hide. No symptom
  has been traced to it; noted so the next investigation starts from a measurement.

## Tables, pools and clicks

- **`Visible` is not "drawn", and AGE has three separate retirement styles**: surplus children of a
  `RefreshChildrenIList/Array` table stay `Visible` at `Alpha == 0`; `ReserveChildren` tables retire
  rows by FADING them; the scan view's pool parks stale children fully visible outside the table's
  extents. Ask about alpha too — but only `== 0`, since a read-only setting is faded to 0.5 and is
  still drawn. A retired row also keeps its old RECT (the planet card's climate table,
  `PlanetGameplayTypeTable`, whose `Load` alone does NOT set `StrictVisibility`, banded the previous
  planet's biodiversity line onto the curiosity line) and its old tooltip `Target` wrapper, so a
  name-by-wrapper read resurrects the PREVIOUS binding's name. A pooled table can also be retired
  WHOLESALE with its rows left painted: `PlanetLabel_SystemOrbital.RefreshPlanetCuriosities`
  (:1090-1103) hides the table and RETURNS before refreshing children. The mod's row test is
  `Visible && Alpha > 0` on the child alone (`AgeWidgets.Paints`/`DrawnChild`), the RENDERER's own
  early-out; the ARRANGER's `GetVisibleChildrenCount` (:2549-2561) counts
  `Visible && (StrictVisibility || Alpha > 0)` instead, so it is a free oracle only on a non-strict
  table. The same alpha-0 retirement shows up outside tables (`GuiEffectMapper.UnloadEffects`).
- **Alpha is also a STATE-FACE selector, and that face is not a ghost.** An `AgeControlToggle` may
  carry `Off` and `On` caption children with exactly one lit by `State` and the other at alpha 0
  (2026-09-02: five faced toggles in one scene, `On` faded in all five; `DecolonizeToggle` reads
  `Off = "Decolonize"` lit over `On = "Decolonization in 1 Turn"` faded). An alpha-blind scrape
  glues both captions and looks exactly like a pooled ghost — read the state from
  `AgeControlToggle.State` instead.
- **A POOL PARKS WITHOUT UNBINDING: a stale binding is not membership.** `ReserveChildren` only ever
  GROWS a pool (firstpass/AgeTransform.cs:2319-2329) and `RefreshChildrenIList` (:2404-2414) sets
  every child past the list's end to `Alpha = 0` without calling the refresh delegate or the item's
  `Unbind` — so `GuiTerm != null` answers TRUE for a row the game finished with. Measured
  2026-08-28: a shelf of five terms in a table of nine bound children announced "5 of 9"; the laws
  modal reproduces it on demand (All 37 cards → Available 6 strands 31 bound ghosts).
- **A pool's child INDEX is not its place on the screen.** The loading window inserts each new line
  at index 0 and fades the rest (`LoadingWindow.SpecificUpdate` :162-197), and `ArrangeChildren`
  lays child 0 out LAST: measured 2026-09-02, `Item000` at y=702 and `Item004` at y=622, so the
  newest line is the BOTTOM one. Sort by measured position; the collection order is the opposite.
- **A window's own `OnBeginShow` can clear its model without clearing its widgets** — the same
  window clears `progressStrings` every showing but leaves the labels holding the previous load's
  text, so its first frames genuinely DRAW the last load's status list.
- **A scroll view scrolls by moving the TABLE's transform, not by the virtual area's offset.**
  Measured 2026-08-28: `VirtualArea.PixelOffsetTop` stayed 0 throughout while the table's own `y`
  moved 349 → 297. Read the scroll state off the table's rect against the viewport's — and a bare
  `AgeTransform` with no `AgeControlScrollView` component still scrolls this way.
- **A `GuiTable` line is a POOL SLOT, not a row.** `LineNNN` names and positions are reassigned on
  every refresh or re-sort, so a cursor keyed on either sits on a different thing a frame later. Key
  a line on `GuiTableLine.Data`.
- **`GuiTable`'s can-select flag is recorded as `LinesTable.Enable`** (`GuiTable.Bind` :130, its
  only writer), so an ancestor-walking "is it operable" test conflates read-only with refused; a
  refused ROW is the line's own `AgeTransform.Enable` (`GuiTableEntry.OnBind`, GuiTable.cs:22-27).
- **`GuiTableLine.OnLineSelectionCb` clears `ClickedCell` AFTER notifying** — read the cell, then
  the line, or the cell is already gone.
- **`GuiRadioGroup` rewires its child toggles and ignores `State`** — the group is the authority.
- **The custom faction editor's trait tables declare column `Filters` the game never draws**: their
  `GuiTableHeader` prefab wires no `FilterToggle`, so the journal's `EndGameSummaryTable` is the one
  table whose funnel toggles actually exist (2026-08-24).
- **AGE clicks PROPAGATE**: `propagateInteraction` defaults true (`AgeControl.cs:19`) and `MouseUp`
  re-delivers up the chain (:170-192), but the engine reaches only the hit target by `SendMessage`
  (`AgeManager.cs:890` — where the click audio comes from) and the ancestors by a plain C# call. The
  click sound is an `AgeAudio` component on the widget's own transform, posted on the engine's mouse
  dispatch and never from the handler (`AgeAudio.MouseUp` :191-197).
- **`SendMessage` arity is not forgiving, and it bites BOTH ways** — `DontRequireReceiver` swallows
  the mismatch silently:
  - `SendMessage(name, sender)` does not reach a zero-argument handler
    (`OnPreviousHullCb()`, `ConstructionCompletedNotificationLine.OnSelectSystemCb()`), so such a
    row simply does nothing.
  - `SendMessage("Cb")` on a handler declared `Cb(GameObject obj = null)` logs "…requires 1" and
    does nothing. **A C# default argument does not save you**: `OnShowLocationCb(GameObject obj =
    null)` compiles to BOTH arities on this runtime (measured: `GetMethods` over
    `QuestBegunNotificationWindow` returns a 0- and a 1-parameter `OnShowLocationCb`, both declared
    by that type), and `SendMessage` resolves by NAME then insists on the arity it found — so "takes
    no argument" must mean a zero-parameter overload AND no one-parameter one.
  - A double-click handler is the trap the other way round: `OnLineDoubleClickCb` takes NO argument
    while the engine dispatches with one — and `MilitaryScreen.OnLineDoubleClick` acts on
    `SelectedFleet`, not the line it was passed, so the row must be selected first.

  From `/eval`, invoking a private handler by reflection with an explicit `new object[]{ null }` is
  the reliable route.

## Tooltips

`AgeTooltip.Content` (often a `%key`) is populated at bind time, long before hover, so tooltip text
can be read without ever showing the tooltip window. It comes in two tiers:

- **Simple** (`.Class` unset → `PanelFeatureSimple`, whose whole body is `SimpleLabel.Text =
  content`): `.Content` alone is the complete tooltip; all outgame tooltips are this tier.
- **Rich** (`.Class` set): `.Content` is empty or a bare title, and the substance is generated by
  **149** `PanelFeature*` classes (counted 2026-08-26) reading provider interfaces
  (`ITitleProvider`, `IDescriptionProvider`, `IFinalCostsProvider`, `IHullInfoProvider`, …) off
  `.Target`/`.Context` — also populated at bind time and readable headlessly, so a faithful spoken
  rendition queries the providers; `.Content` alone would miss essentially everything.

**Mod policy the tiers force (owner ruling 2026-08-28, enforced structurally):** a tooltip's
announcement class is its tier and nothing else — Simple is announced whole on arrival, deduped
against what the node's other parts already say; Rich is buffer-only. No length or per-screen
conditions and no caller-chosen mode: `GraphNodes.ModeFor` derives the class, tooltip-text reads
outside the door files are lint-gated, and `TooltipParity`'s `misclassed` bucket flags a live
violation. A nested entry is named by its hover target's drawn words (a bare figure is not a name),
falling back to the tooltip's title, then its first line.

**Positioning**: most tooltips are `AgeTooltipAnchorMode.FREE`, drawn at `AgeManager.Cursor` with
the anchor rect never consulted, so under *keyboard* focus a tooltip renders wherever the idle mouse
is parked unless it is re-anchored — set `AnchorMode` **and set `Anchor` explicitly**, since a null
anchor is substituted with the controller's `DefaultAnchor`, a marker at the bottom-left screen
corner. Save and restore both fields when focus leaves.

There are **no tooltip→tooltip links and no encyclopedia**: `[...]` markup is icon-glyph
substitution, `#RRGGBB#` is inline color, and nothing rendered inside a tooltip is clickable — so a
"read tooltip" key can speak rather than navigate.

Measured behaviour of the drawing pipeline:

- **A block caption's WORD and its EXPLANATION sit on different widgets, and the prefabs repeat the
  group's name.** The recurring shape is `…Group / TitleGroup [tooltip] / SomeTitle [text]` —
  sentence on the wrapper, words on the label (2026-08-22 on `MinorFactionDiplomacyModalWindow`,
  `PopulationModalWindow`, and from the unshown prefabs of `AcademyDiplomacyModalWindow` and
  `PirateDiplomacyModalWindow`). So passing the LABEL to a caption reader silently loses the
  sentence, and `TitleGroup` is worn by three different groups in one window, so a lookup by that
  name answers with whichever the walk reaches first (the faction banner, which named the relation
  panel "Niris"). **Mod policy**: resolve such a caption from the UNIQUE label name and take its
  `Parent`.
- **One drawn LINE can carry two tooltips, one enclosing the other, and the engine draws whichever
  is innermost under the pointer** — 2026-08-28 on `HeroInspectionModalWindow`, where `SizeGroup`
  carries `%ShipStatSizeDescription` and the `SizeLabel` inside it `%ShipSizeSmallDescription`.
  Since there is one tooltip window, a node can RAISE only whichever it points at, so declaring both
  promises words the game will never draw. **Mod policy** (owner ruling 2026-08-28): such a line
  converts to the nested-entry pattern BY DEFAULT — outer caption on the line, each inner dossier a
  `TooltipChildren` entry aimed at its own widget; conversions are reported, not assumed.
- **A tooltip is TWO promises through two doors, and a node that makes one without the other is
  silent in a way nothing observable reports.** `NodeVtable.PointsAt` declares WHICH dossier the
  node shows; `NodeVtable.OnFocusVisual` moves the pointer, and moving the pointer is the only thing
  that makes the game draw its tooltip. Wire the first alone and the words read back perfectly while
  the picture never appears (`DevProbe.TooltipPipe` shows `aimed=- want=- win=hidden`).
- **A failed tooltip request is PARKED for 999 seconds, and only a change of hovered transform lifts
  it.** When the hover delay elapses and `ReadTooltipInformation()` says no — which it does when the
  tooltip has neither `Content` nor `Target` — the controller writes `timeBeforeShowingTooltip =
  999f` instead of retrying (`Amplitude.Unity.Gui/GuiTooltipController.cs:214-224`). Only two things
  re-ask: `OverrolledTransform` becoming a DIFFERENT transform (:191), or `Target` being written
  again (`DirtyTarget`, :186-190). This is a mouse-shaped design, so a keyboard user sitting still
  gets neither edge (measured: still counting 989 → 975 fifteen seconds later). **Mod policy:** never
  aim at a tooltip with neither content nor target, and re-issue `DirtyTarget = true` once per stall
  — never per frame, since it resets the countdown.
- **There is exactly ONE tooltip window and one controller slot**: a single `CurrentTooltipWindow`
  (:34), all 146 `GuiTooltipDescription` entries naming the same `"TooltipWindow"`, and moving the
  hover INSIDE a drawn tooltip REPLACES what is drawn (:191-195). Two tooltips can never be shown at
  once, which is why the aim is a declared property of a node rather than re-derived.
- **A tooltip drawn INSIDE another tooltip cannot be reached at all** (measured 2026-08-22): the
  `PoliticalOpinionLine`s in the population dossier DO hold their `Politics`/`GuiPolitics` target
  while the parent is drawn, but pointing at one replaces the tooltip that was drawing it, and the
  replacement's `PanelFeaturePoliticalOpinion.Unbind` → `ReleaseData` clears exactly the target the
  new request needs. There is no frame ordering that wins. **Mod policy:** read a nested dossier off
  the game's own wrapper (`GuiPolitics`, `IPoliticalOpinionProvider` — `UI/PoliticsDossier.cs`) and
  point the node at the PARENT tooltip. **Owner ruling (2026-08-28): a depth-two dossier exists in
  the mod only where a sighted player can reach it normally.**
- **An alpha-0 widget is not hit-testable for the mouse** (`AgeTransform.cs:3448`), so a subtree the
  game reveals on hover carries tooltips a mouse can reach only after the reveal — but
  `PointerFocus.LateTick` writes `OverrolledTransform` DIRECTLY, so the mod can point at one without
  it. A parity walk keeping the engine's transparency gate cannot see them at all.
- **Every tooltip is a FLAT ordered list of panel features**: sub-features are added as further
  siblings under `PanelFeaturesTable`, not nested. `IsSeparator`/`IsSpacing` are on
  Assembly-CSharp's global `GuiPanelFeature`, not the firstpass base — a REPL probe typed to the
  latter cannot see them.
- **A panel feature can be a whole SECTION rather than a row**: `PanelFeatureModuleEffects` and
  `PanelFeatureHullInfo` build N instances of ONE prefab, each a complete section, heading and all.
  A reader treating them as repeated rows flattens several sections into one.
- **`CustomFactionDetailsPanel.cs:174` swaps a tooltip's content and class arguments**, so it names
  a CLASS that is really a sentence; `GuiTooltipController` gives up when the class is not in the
  description database (:239-249) and the tooltip draws nothing whatever points at it. That is a
  defect in the GAME's data (`TooltipAudit`'s `undescribed`) and no mod change can fix it.
- **A prefab tooltip's `%…` Content can be a placeholder the game overwrites at bind**
  (`NegotiationModalWindow`) — a caption test that localizes the prefab key alone answers "no
  sentence" for a caption that has one.
- **A hint button's tooltip has three parts, in a fixed order**: the button's own description, then
  `"\n\n"` and the failure (`Gui.FormatFailure`, Gui.cs:1072), then — only for a missing technology
  — `"\n" + %MissingTechnologyClickDescription` (`Gui.FormatButtonHint`, Gui.cs:1207). The refusal
  alone is lines[1..] minus that instruction.
- **A hint-blocked button stays `Visible` AND `Enable`** — the game turns its click into "jump to the
  missing technology" instead of disabling it, and `Gui.FormatButtonHint` FORCES `Enable = true` as
  it writes the hint. `Gui.IsHintActive(transform)` is the ONLY discriminator; only 6 of the 16
  prefabs using the mechanism are honest about their own flag, so the question is asked per site.
- **The hint exists so a CLICK can explain itself, and that click is the only thing such a control
  does**: it asks `Gui.IsHintActive` and runs `Gui.ActivateHint`, which reads the Ctrl the player is
  PHYSICALLY holding — so a mod's own chord falling back to the plain click replays a no-op, and the
  jump is WIRED once in `Cells.Add`. On this install, **101 `GuiButtonHint` instances, 8 hint-active**
  (2026-08-13); one of the eight is the marketplace tab, hint-active while its own `Enable` is false
  — reachable from the keyboard though the mouse cannot click it.
- **A drop list's own tooltip is wired to a message NOTHING receives.** The per-item string and
  target tables are MUTUALLY EXCLUSIVE (`SetTooltipTargets` clears the other, firstpass :255-275)
  and both are pushed onto the popup's ITEMS when the list is filled (:532-545 →
  `AgeControlPopup.SetTooltips` :115-146), so an item's own `AgeTooltip` is the honest place to read
  from. The CLOSED control is dead: `SelectedItem` (:141-158) hands the value to `SetTooltip`, whose
  target overload sends `"OnSetTooltipTarget"` (:547-557) — received by no component in either
  assembly — while its string overload ignores the receiver and sends to the drop list itself
  (:559-569). Seven game sites use the target table; there is nothing to fix from a mod.

## Icons and picture captions

- **A tooltip names its bare numbers out of TWO registries, and only one is the icon table.** Beside
  an inline `[token]` (which `IconNames`/`IconTable` resolves) sits a standalone `AgePrimitiveImage`
  the feature binds from a field of its own (`HealthIcon`, `ActionPointIcon`, the
  `ValueDuplet.Symbol` of each ship-size count). The second kind is not markup and never reaches the
  token table: its word is the STAT's own title in the element database (`Gui.GetTitle` + the
  `"%"+name+"Title"` fallback). `DevProbe.UnknownIcons()` is silent about these by design; the
  symptom is a spoken line that is only figures, and the fix is a typed reader, never a new icon row.
- **A few symbols are painted straight into a panel and so are missing from the element-derived
  picture table.** `TurnSymbol` — the hourglass in front of a build's remaining turns — is drawn by
  an `AgePrimitiveImage` no `GuiElement` carries a token for, so it is a HAND-WRITTEN row
  (`TURNSYMBOL=icon.turn`) that a regeneration must keep.

## Cloning a game window

- **Showing needs no registry; ESCAPE does.** `ShowWindow`/`HideWindow` work unregistered, but
  `GuiManager.HandleInput` dispatches every `InputAction` by walking the private
  `guiWindowsFromBackToFront` (`GuiManager.cs:2058-2063`) **from the END backwards**, so an
  unregistered clone never sees Escape and the galaxy's Escape opens the pause menu behind it
  (`:2123`). A clone goes into BOTH that list and `GuiWindowsStack.guiWindows` and comes out of both
  by NAME — inserted BESIDE the window it was cloned from, or it is asked ahead of the message box
  it raises itself.
- **`GetWindow<T>` can never answer with the clone**: the lookup is keyed on `guiWindowsByType` with
  the EXACT type (:154-165), and the clone's type is the mod's subclass — so "the clone if shown,
  else the game's" resolves with no ambiguity.
- **Nothing starts a clone's `Load()`** — `LoadGuiWindows` ran at boot — so the mod starts the
  coroutine itself and sets `Loaded = true` at once, `ShowWindow` refusing a window that says it is
  not loaded. That setter, the `Name` setter, `Initialize(stack, outGame)`, the `GuiWindowsStack`
  getter and the two lists above are all internal or protected.
- **The manager's MODAL registry is a third registration, and it is built once.**
  `GuiManager.Load_IGuiGamePanelService` walks `guiWindowsFromBackToFront` at boot into the private
  `guiModalWindows`, so a clone built afterwards leaves `IsAnyModalVisible` and `ModalOnTop` as if
  nothing were open — and `IsAnyModalVisible` is what the game weighs the tutorial popup against
  (`TutorialPopupPanel.UpdateLayerAndVisibilityAccordingToOtherWindows`), what `CanToggleScanView`
  asks, and what `AddTutorialKeysIFN` stands down for.
- **Component swap on a prefab clone works, serialized references do not follow it**: every declared
  instance field must be copied from the original component (walking `OptionsModalWindow` up to but
  not including `MonoBehaviour`) before `DestroyImmediate`.
- **A runtime change destroys every window in the stack**, the clone included
  (`GuiWindowsStack.DestroyWindows`), so a clone needs no `RuntimeChange` subscription — a per-frame
  null check and a rebuild is the whole of it.
- **An `OptionItem`'s identity within a page is the ROW's name, not the option's property name** —
  fifty rows minted from one interface property share a property name, and keying on it collapses a
  whole page into one duplicate `ControlId`.
- **`OptionsModalWindow` ties four behaviours to the literal category name `"Controls"`**, nothing
  else configuring them: `OpenCategory` :119-120 and `OnBeginShow` :223-224 show the reset buttons
  only for that name, `HandleInput` :66-68 picks `%BindingExitWithoutApplyMessage`, and
  `OptionsTabToggle.Initialize` draws the tab and tooltip from `%OptionToggle<name>Title`/
  `Description`. A clone naming its key-binding category "Controls" gets all four in every language.
- **The window's reset buttons reset the GAME's bindings** (`OnResetCb` → confirmation →
  `OnResetConfirmation` :353-361 → `IInputOptionsService.ResetToDefaultBindings`), which on a clone
  would silently rewrite a page it is not showing; re-aiming one is two field writes.
- **The tab bar's width is `(TogglesTable.Width − HorizontalSpacing × (n−1)) / n`**
  (`AddCategoryToggleAndPanel` :246). Measured on a clone: `Width` 600, `HorizontalSpacing` 0, so
  five tabs draw at 120px (the game's own six at 100px) and fourteen would draw at 42px.
- **A button cloned out of the window's bar has three children — `Circle`, `Icon`, `Label`.** Hiding
  `Icon` leaves the round frame that says "button" without the cross that says "cancel". Re-parented
  into a rows table it needs the ROW's anchoring (`AttachLeft`/`AttachRight` true, `AttachTop`/
  `AttachBottom` false) or the table stacks every row at the same y.
- **A row action that changes which panel is showing must re-seat a keyboard cursor itself**, because
  focusing a tab IS switching to it: otherwise the row is destroyed, the navigator re-seats onto a
  tab, and the landing switches the page back (2026-08-24).
- `GetMethod("OnCancelCb", Instance|NonPublic)` is AMBIGUOUS on `OptionsModalWindow`
  (`GuiModalWindow.OnCancelCb(GameObject)` is the second overload) — pass `Type.EmptyTypes`.

## The two menus and their entries (adding one of the mod's own)

- **The main menu's entries are DATA**: `ReserveChildren` only ever ADDS children (:2319-2329) and
  `RefreshChildrenArray` hides the ones past the array's end, so appending to
  `MainMenuScreenGuiElement.Entries` is enough — the screen builds, names, binds, places on the
  circle and animates the item. `Bind` rewrites `Gui.GetTitle(Name)`/`GetDescription(Name)` every
  refresh and an unknown name comes back as the raw key, so a mod entry's label and tooltip must be
  written again after each bind — cheap, because `AgePrimitiveLabel.Text`'s setter does nothing when
  the words have not changed (:168-189). An unknown name also gets the `MainMenuDefaultLarge` icon.
- **The main menu's click is a MESSAGE NAMED AFTER THE ENTRY**: `MainMenuItem.OnClickCb` sends
  `"OnClick" + MainMenuEntry.Name` with no argument to the SCREEN's GameObject, so a receiver is a
  parameterless public method on a component there — and one receiver serves the mouse and the
  mod's keyboard activation alike.
- **The ring is a TABLE, not hand-placed rectangles**: `ApplyCircleEvenlySpacedArrangement`
  (:2881-2895) walks `Children` in order giving each the next polar angle, so **where an entry sits
  round the circle is decided by its position in the child list** and any hand-written X or Y is
  overwritten at the next arrange. Place with `SetSiblingIndex` (plus the same move in the AGE
  `Children` list) then `ArrangeChildren()`; removing the child and arranging again restores the
  original ring to the pixel.
- **What `GameMenuModalWindow.Load` does ONCE therefore has to be said again** (:76-95): each item's
  label goes on the side away from the centre (`ShowRightLabel` when `X > CenterX`), and the show
  animation's per-item delays are staggered by index — `spread = Circle`'s first modifier duration
  `/ count`, `delay = window`'s first modifier duration `* 0.5 + spread * 0.5`, `ReverseStartDelay =
  total - delay`. Re-running that arithmetic over the entries that are LEFT reproduces the game's
  own numbers exactly, so it doubles as the restore.
