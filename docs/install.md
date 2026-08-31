# ES2 facts — the install, the session and chat

Store and DLC gating, what the game's own logger costs, what the Mono runtime does under the
REPL, the out-game pages and the lobby, and the chat panels. Index and charter: `README.md`.

## Store, DLC and the install

- **The STEAM build has NO store code besides Steam** — no GOG/Galaxy/Epic assemblies or
  branches anywhere; the single branch is "did `SteamAPI_Init()` succeed", and the failure path is
  hardcoded on (`enableOfflineModeWhenSteamClientIsDown = true`): services register with
  `IsSteamRunning == false`, all DLC unowned, language forced to English, Join Game refused
  with `SteamNotRunning`. Launching `EndlessSpace2.exe` with Steam closed reproduces the
  whole store-less profile — a free test fixture. The mod calls no Steam API anywhere. (The GOG
  build differs: `architecture.md`.)
- **DLC ownership has exactly one source**: `DownloadableContent.IsSubscribed` →
  `SteamApps.BIsSubscribedApp` (no subclass overrides it). The 367 `*_DLC*` data files
  ship with the base install; only flags gate them. To unhide at runtime, add
  `Subscribed|Installed|Activated` via `AddAccessibility` — NOT `Shared`, which
  `RuntimeState_Lobby:400-430` wipes and re-derives at session creation.
- **What the gates actually read is `IsShared`, and only a NEW game derives it.** The re-derive
  branch runs for a freshly CREATED session (`RuntimeState_Lobby.cs:440-450`); a LOADED game takes
  the save's own `sbs` bitfield instead, and `rdcol` — the flag that would send a load down the
  derive branch — is never written anywhere in the game. So a `Shared` flag set by hand in a loaded
  session STAYS set, which is what makes an unowned expansion's UI sightable at all. Two consumers
  cache the answer at load time (`HackingManager`, `ScanOverlayWindow`), so their branches stay dark
  until the save is loaded again.
- **The `*_DLC*` datatables load whether or not the expansion is owned** — measured: the Juggernaut
  specialization modal reports `GuiSpecializations.Count == 3` on an install owning no expansion. So
  a DLC screen's DATA is measurable here even when its entry point is not.
- **"Steam cloud" saves are not a Steam API**: the toggle writes a registry key that
  redirects the save directory to `<saves>\Cloud` — identical on any store.

## The game's own logging, and what it costs the player

- **`GuiManager.GetWindow(StaticString)` — the BY-NAME lookup — always logs an Error on a miss.**
  `Amplitude.Unity.Gui/GuiManager.cs:167-175`: there is no `reportError` overload for it, unlike the
  by-TYPE `GetWindow<T>(bool)` (:155-165). A mod screen that asks "is my window up?" once a tick
  therefore writes one Error per tick for as long as the registry is filling — measured 208 each for
  `LoadSaveModalWindow` and `OutGameLoadModalWindow` per session, from `LoadSaveScreen.IsActive`.
  `GuiManager.GuiWindowsLoaded` (:40, public) is the gate: with it true, all 170 windows are
  registered (measured in game and at the menu alike). **Mod policy**: by-name lookups go through
  `GameWindows.Named`, which reads the private `guiWindowsByName` registry; every by-type lookup
  passes `false`.
- **Every Error/Exception the game logs is forwarded to Amplitude's telemetry, with its stack.**
  `PrismGameManager.cs:582-617` (`MessageLoggedEventHandler` → `PrismErrorEvent` → `SendEvent`). So a
  mod that logs errors through the GAME's logger is not merely noisy: it is uploading, and the
  `PrismManager.SendEventsCoroutine waited for too long` warnings that accompany a noisy session are
  those sends backing up. The same messages also land in
  `Documents\Endless Space 2\Temporary Files\Diagnostics - *.html`, which a per-frame message fills
  at gigabytes an hour.
- **Which wrapper constructors log.** `GuiWrapper.Bind` (`GuiWrapper.cs:118-128`) warns only when
  `EnforceValidGuiElement` is true, and `GuiUnlock` is the ONLY type that overrides it to true — so
  `GuiPlanet`, `GuiAnomaly`, `GuiCuriosity`, `GuiResource`, `GuiResourceDepositGroup`, `GuiStarSystem`,
  `GuiProbe`, `GuiTimeBubble`, `GuiFleetGroup` are all silent to construct. What is NOT silent:
  `GuiQuest` (`:112`, "Could not find a valid QuestGuiElement named …" — the batch-8 hotfix),
  the BY-NAME overloads of `GuiAnomaly` (`:79`) and `GuiResource` (`:70`) — always pass the
  DEFINITION — and `Gui.GetTitle`/`Gui.GetDescription` (`Amplitude.Unity.Gui/Gui.cs:254-282`), which
  warn for a missing element; read `Gui.GetGuiElement(name).Title` instead where the caller can cope
  with null. `Gui.AssertNotNull` builds a `StackTrace` on null but logs nothing.

## The Mono runtime under the REPL

- **The AI threads' type scan races the REPL's emitted types.** `POST /loadsave` issued into a
  turn the game is still resolving (the end-turn button reading "Pending") kills the process
  outright: the 2026-08-24 crash dump has the AI threads walking the whole app domain in
  `mono_class_is_subclass_of` while `/eval`'s `mcs` TypeBuilders are still unfinished types in
  that domain. Nothing in the mod can guard it — the rule is to never load a save across a
  pending turn.
- **An order the game only posts from inside its own turn phase wedges the turn machinery for
  good** when posted from `/eval`: `OrderSpawnPirateLair` (2026-08-24) left the fixture unable to
  finish a turn at all, unrecoverable by reload. Grant the order's precondition and let the game
  post it, or spend the turns.

## Out-game pages and the lobby

- **The out-game pages, measured** (DLC browser, mod manager, disclaimer, credits):
  `DownloadableContentType` 1 is `Personal`, which the game words as "Add-on" — the browser's own
  type column, not an ownership state. `AvailableModItem` leaves a DOWNLOADING row's toggles
  enabled and its handler only logs (a game bug: the click does nothing), so the mod speaks that as
  the refusal it meant. `ModdingSelectedModPanel` swaps its two branches by ALPHA alone — both stay
  `Visible`, so a reader gates on `SelectedGuiMod` instead. `DisclaimerModalWindow.HandleInput`
  returns true for EVERY action and acts on none, so Escape cannot dismiss it and only its own two
  buttons can (Decline quits the game). The credit roll is 598 items and exits itself after
  ≈8.5 minutes.
- **`FactionChoiceModalWindow` keeps its hull set in the private `filteredShipHulls` and its
  position in `currentHull`; `OnNextHullCb`/`OnPreviousHullCb` are the only movers and they wrap**
  — the mod's hull pager steps those callbacks, shortest way round. Its overridden Exit routes to
  its Validate handler, which is the opposite of every other modal picker in the game.
- **A `SettingItem` carries TWO tooltips and only one of them moves with the value.**
  `SettingTitle.AgeTransform.AgeTooltip.Content` — what the setting IS — is written once at `Bind`
  :33, while `CurrentSettingTooltip.Content` is rewritten for every value the setting lands on
  (`SettingSliderItem.CurrentValue` :52-55, `SettingCheckBoxItem.Refresh` :50-53,
  `SettingDropListItem.Refresh` :72-75, each also setting `DirtyTarget`). The options screen's
  `OptionItem` has ONE tooltip, written at `Load` :23 and never rewritten. That difference is the
  whole discriminator for which settings re-read the game's sentence after a change
  (`SettingRows.SayValueTooltip` — `ES2Access/Screens/SettingRows.cs`): the new-game lobby, the advanced settings modal and
  the pause menu's settings panels all do, and the options screen deliberately does not, because
  there the sentence has not changed and a re-read would repeat it on every keypress.
- **A lobby has chat history the moment it becomes multiplayer**: switching Session Mode posts
  `%LobbyChatRenamed` through the chat service, so the log is never empty in a session that was
  ever MP.
- `EnableFactionIntroductionVideos` is FALSE in this install, so the faction intro cutscenes
  cannot be sighted here at all.
- The save system — campaign identity, descriptors, loading one named file — is `docs/saves.md`.

## Chat

- **Game chat text carries `#RRGGBB#` colour markup** — cleaned like any other game text before
  speaking.
- **`StartChatting`'s default is `Return,Tab`** (`InputManager.cs:269`) — both of the mod's own
  primary keys. Bindings persist to `Settings/Input/…` in the user `Registry.xml`, flushed only on
  a clean quit and re-applied at boot AFTER the input service is published, so a write made during
  start-up can be overwritten: wait for the binding table before writing one. `Option.GetValue` is
  uncached for key mappings, so the options row follows a programmatic change with no refresh of
  its own. The game has exactly **two `ChatPanel`s** — the in-game one over an
  `AgeControlTextFieldChat` (`StandardCancel=false`) and the lobby's over a plain
  `AgeControlTextField` (`StandardCancel=true`) — plus one other chat-TYPE field, the
  coordination-request pin box, which is why a chat reader scopes on `ChatPanel` identity rather
  than on the control's type. `InGameChatWindow` is shown and answers `StartChatting` in single
  player too, so the key is live in every session.
- **A single-player chat line goes out and comes back**: a message sent from the in-game panel is
  stamped `ChatMessageOption.Default` (= `RecipientGlobal|TypeUser|ScopeNetwork`, by
  `ChatDefaultOptionHook`), leaves through `Session.SendLobbyChatMessage` and returns through
  `Session_LobbyChatMessage` → `ReceiveMessage`, arriving on `OnChatMessageReceived` with the
  player's own name filled in from the lobby slot (measured in a Single session: "Neurrone: probe
  alpha"). So single-player chat is a real, readable log rather than a dead box — which is what the
  mod's chat surfaces are gated on (`SessionChat.HasChat`), MP-only having been the earlier policy.
  `ChatManager` keeps the messages until the session is released or `RemoveMessages` is called;
  the PANEL keeps only fifty (`ChatPanel.MaxHistory`, enforced in `AddLine`) and pools those
  widgets, which is the bound the walkable log copies.
- **`InGameChatWindow`'s visibility is `GameReady` and nothing else**:
  `GuiManager.UpdateGameWindowsVisibility` passes a bare `true` for it (:1579-1580) and
  `SetGameWindowVisibility` ANDs in `GameReady`, so "the window is shown" already means "a game is
  running" — no session-mode test needed to keep chat off the menu.
- **The in-game panel is discreet whenever nobody is typing in it and no pointer rests on it**, and
  `SetDiscreet` (:127-180) takes the field with it: `ChatTextField.AgeTransform.Enable` and
  `Label.Visible` both go false, `ChatTabs[i].ShowOrHide` hides the tab bar, and going discreet
  clears the engine's focus if the box still had it. So the field's own enabled flag reports which
  state the panel is in, never whether chat can be opened. `discreet` itself is a private bool on
  `InGameChatPanel` and is the one thing every part of that visibility derives from. `SetFocus`
  (:116-125) is the single way in — the chat key, a tab click (`ChatTab.OnTabCb`) and the
  new-message button (`OnNotificationCb`) all call it, and it focuses the field and un-discreets in
  one call, with `OnValidateObject` nulled for a frame so the keypress that opened the box cannot
  also validate it.
- **The POINTER is what keeps the panel open after the box lets go**: `OnTextFieldLoseFocusCb`
  (:310-317) goes discreet and hides every line unless `IsHoveringThePanel` (private, :493-514 —
  the panel's rect, its scrollbar, and the engine's `OverrolledTransform`) says the cursor is on it.
  A keyboard has no equivalent, so a mod that wants the panel walkable has to BE that pointer
  (`ChatHold`) — and dropping the engine's focus fires this handler first, so the hold is
  re-asserted after, never before. **Mod policy 2026-08-14**: chat is a mod-owned child screen held
  open exactly while it is up.
- **Escape out of the chat box is an ACTION dispatch, not a key delivery.** The field is
  `AgeControlTextFieldChat` with `StandardCancel=false`, so `InputManager.HandleInput` :1228-1239
  hands `InputAction.Exit` down the handler chain, and `InGameChatPanel.HandleInput` :108-112
  answers it with `SetDiscreet(true)`. `ChatPanel.OnTextFieldKeyDownCb` :232-246 has an Escape route
  of its own, but the InputManager's `Update` beats `AgeManager`'s LateUpdate KeyDown by a frame and
  has already dropped the focus that route needs — so the panel's own `HandleInput` is the only
  place that press can be intercepted (measured 2026-08-14).
- **A chat tab that answers `CanShowTab` false is never drawn** even while the panel is open:
  `ChatTab.OnBeginShow` gates on the same predicate, so `CanShowTab` and drawn-ness agree for a
  non-discreet panel and the mod needs only one of them.
