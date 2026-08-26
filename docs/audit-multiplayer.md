# Multiplayer audit — what ES2 has, what the mod covers, what a stage plan looks like

Decompile + mod-source only (no dev server this stage). Every claim below cites
`decompiled/<Assembly>/<File>.cs:<line>` or a game data file under the install's `Public/`.
Line numbers are from the current decompile; member names are the durable part.

---

## 1. What multiplayer ES2 actually has

**Steam lobbies, and nothing else.** No LAN, no direct IP, no asynchronous/hot-seat play.

- The join list is Steam matchmaking with a WORLDWIDE distance filter and a registry-capped
  result count (`JoinGameScreen.RequestLobbyList` :358-380 —
  `AddRequestLobbyListDistanceFilter(k_ELobbyDistanceFilterWorldwide)`), and every row is
  built from Steam lobby metadata (`LobbyInfo` ctor :153-202, ~35 `GetLobbyData` reads).
- Joining is a Steam lobby id handed to the runtime state machine
  (`JoinGameScreen.OpenLobbyAndWatchSessionOpening` :350-356 →
  `PostStateChange(typeof(RuntimeState_Lobby), lobbyInfo.SteamIDLobby, false)`).
- Hosting is the same state machine with a `SessionMode`
  (`MainMenuScreen.OpenLobbyAndWatchSessionOpening` :111-116).
- Steam is a hard requirement: no Steam → the Join button is disabled with
  `FailureFlags.SteamNotRunning` on its tooltip (`JoinGameScreen.RefreshButtons` :256-314),
  and the competitor panel falls back to an AI-only slot list
  (`NewGameCompetitorSlotsPanel.DiscriminatePlayerSlotFromTheOthers` :125-150).
- **There are three session modes, not four.** `SessionMode` is
  `Single/Private/Protected/Public` (`Assembly-CSharp-firstpass/SessionMode.cs`) but
  `Private` is commented out of the setting definition
  (`Public/Settings/GameSettingDefinitions.xml:8-13`): the drop list offers Single,
  **Protected** (= friends-only; `LobbyInfo.InitializeFlags` :236-239 sets `FriendsOnly`,
  and `IsVisible` :120 hides it from non-friends) and **Public**.
- **No password prompt anywhere.** The only password code in the tree is `G2GAuth` /
  `DebugUIWindow_G2GAuth` — the Games2Gether account, a debug-UI-only surface, unrelated to
  lobbies. Access control is Protected-vs-Public plus the host's Kick/Lock.
- Multiplayer can be switched off wholesale:
  `Preferences.EnableMultiplayer = !CommandLineArguments.EnableModdingTools`
  (`EndlessSpace2Application.cs:161`). When false the main-menu entry hides itself
  (`MainMenuItem.cs:92`), `JoinGameScreen.OnBeginShow` hides the window outright (:207-211),
  the SessionMode drop list drops its MP entries (`SettingDropListItem.cs:40`), and MP saves
  become unloadable (`LoadSaveModalWindow.cs:251`). `run-game.ps1:110` passes no arguments,
  so the dev loop has multiplayer ENABLED — but any future `-EnableModdingTools` flag would
  silently delete every surface below.

### The complete screen path

| Step | Window / class | Notes |
|---|---|---|
| Main menu → "Join game" | `MainMenuScreen.OnClickMainMenuJoinGame` :451-455 → `JoinGameScreen` | Escape/right-click returns to the menu (`HandleInput` :164-173) |
| Join list → a lobby | `OnClickJoinCb` :326-332 / `OnLineDoubleClick` :435-441 → `RuntimeState_Lobby` | Mod-config gate: only a `Green` configuration joins; `Yellow` shows a Fix Mods button that hands the required config to `ModdingScreen` (:339-348) |
| Main menu → "New game" | `OpenLobbyAndWatchSessionOpening(SessionMode.Single, false)` :434-437 | The lobby OPENS in Single and is switched to MP from inside it |
| Steam friend invite (overlay) | `RuntimeState_Lobby.OnGameLobbyJoinRequested` :142-168 | Raises `%ConfirmLobbyJoinRequestDescription` as a normal message box, then disconnects/reopens |
| Command line | `MainMenuScreen.OnEndShow` :192-201 (`CommandLineArguments.ConnectLobby`) | A direct-to-lobby boot path |
| The lobby itself | **`NewGameScreen` — the SAME class as single player** | `RuntimeState_Lobby` :283-296 `ShowWindow<NewGameScreen>()` for every non-autostart case |
| Ready/Start → countdown | `SessionState_OpenedAndReady` → `SessionState_OpenedAndCounting` | 10s (`SessionState.StartCountdown` :64-72), 30s for the long variant (:74-82); the lobby UI LOCKS at ≤5s remaining (`SessionState_OpenedAndCounting` :105-108) |
| Synchronizing | `SessionState_Synchronizing` :22,28 | Announces itself only as chat (see §3) |
| Launching | `SessionState_OpenedAndLaunching` :42 → `LoadingWindow` | MP adds a per-player progress list (`LoadingWindow` :135-140, `UpdatePlayerLoadStatus` :293-300) |
| In game | the normal game + `InGameChatWindow`, and `EndTurnWindow`'s MP cluster | see §2 |

**The single most important structural fact: there is no separate multiplayer lobby.**
`NewGameScreen` is parameterized on `Session.SessionMode` and `Session.IsHosting`
(`RefreshButtons` :433-454, `CanModifyGameSettings` :227-238, `HandleInput` :207-219). The
mod's `NewGameScreen` therefore already covers ~80% of the multiplayer lobby; what is missing
is a set of conditional widgets, not a screen.

### The multiplayer-only widgets, exhaustively

**Lobby, page level (`NewGameScreen`)**
- `StartButton` / `StopButton` / `ReadyButton` / `UnreadyButton` — one of four, chosen by
  `SessionMode`/`IsHosting`/`LocalPlayerReady` (:437-440). All four sit in one band and all
  four are wired to `OnClickStartCb` :538-541 (`LocalPlayerReady = !LocalPlayerReady`).
- `AfterJoinLoadingPanel` — shown from `OnBeginShow` :363-366 for a non-host, hidden when the
  lobby slots first replicate (`ILobbySlotProvider_OnCollectionChange` :609-614). This is the
  "connecting" state.
- `GuiLocked` — set from the static `SessionState.OnLockLobbyUI` event (:555-559); it
  disables the empire panel outright (`NewGameEmpireSlotPanel.Refresh` :116) and switches off
  Join/Lock/Invite (`CompetitorSlot.RefreshStates` :228-233,
  `NewGameCompetitorSlotsPanel.Refresh` :110-111).
- Exit while hosting with another human present raises a confirmation
  (`%NewGameScreenExitWhileHostingConfirmation`, :209-212).
- Being kicked is a message box plus a return to the out-game state
  (`Session_LobbyChatMessage` :571-593, `%KickedFromLobbyTitle`).

**Lobby, per competitor slot (`CompetitorSlot.RefreshStates` :226-254)**
| Widget | Visible when | Action |
|---|---|---|
| `JoinButton` | slot is free (`IsFree == IsAI`, `LobbySlot.cs:202`), MP, not locked | `SessionHelper.RequestJoinSlot` :402-407 |
| `KickButton` | MP + hosting + slot is human + not the host | `SessionHelper.KickUser` :409-415 |
| `ReadyIconGroup` | slot is human and `IsReady` | readout only |
| `LockToggle` | MP + slot free + not ready + not locked-out | `SessionHelper.RequestLockSlot` :417-423, host-only |
| `EliminatedGroup` | `LobbySlot.IsEliminated` (a rejoined save) | readout only |
| `HostIcon` | `IsSlotOfHost` (:121-131 — true for the human slot in single player too) | readout only |
| `InviteButton` (panel) | MP + hosting | opens the Steam overlay invite dialog (`OnInviteCb` :181-201) |

**Lobby chat (`NewGameChatPanel` : `ChatPanel`)** — the panel is present in single player but
"soft hidden": the lines are invisible, the field disabled, and its text set to
`%ChatDisabledInSinglePlayerTitle` (:43-65). In MP the lines become visible and the field
enabled with `%ClickToStartChattingTitle`.

**In game**
- `InGameChatWindow` (a `GuiWindow` wrapping `InGameChatPanel`) — always shown while the game
  is ready, even in single player (`GuiManager.cs:1579-1580`, unconditional `true`); it sits
  in the `OverlayRenderer` stack above the load/save modal
  (`Public/Gui/GuiWindowsStackDefinition.xml:182`). Two tabs via a `GuiRadioGroup` (Global and
  `ChatTabAlliance`, the latter auto-hidden when the player leaves an alliance,
  `AllianceService_OnAllianceCollectionChange` :468-491), a `NotificationButton` that appears
  for `NotificationShowDuration` when a message lands while discreet (:275-281), and a
  discreet/expanded mode (`SetDiscreet` :127-180).
- `EndTurnWindow`'s multiplayer cluster (fields :58-114):
  - `SyncGroup` / `SyncIcon` / `SyncTooltip`, visible only in MP (:734); the tooltip text is
    the `SyncStatus<SynchronizationState>` extended GUI element's description
    (`RefreshSyncState` :1249-1269).
  - `DesyncButton` — enabled only on `SynchronizationState.ChecksumMismatch` and only for the
    host (:1268).
  - `CompetitorsCircularTable` of `CompetitorOrbitalSlot`s, MP-only (:735): ready/unready icon
    per player, a tooltip of "leader and faction — `%PlayerSync<State>Title`", and a click that
    pre-fills a whisper into the chat field (`CompetitorOrbitalSlot.OnSlotCb`).
  - `PlayersListPanel` — **shown only while the physical mouse is inside the end-turn button's
    radius** (`SpecificUpdate` :906-921). Per player: faction icon, leader name, score,
    diplomacy icon and a status word (`PlayerStatusLine.Refresh`).
  - The timers: `GlobalTimerLabel` (:826-827) and the `CommonTimer`/`Overtime`/`LastPlayer`
    arcs, driven by lobby data.
  - `EndTurnTitle` becomes `%EndTurnWaitingTitle` in `GameClientState_Turn_Finished`
    (`RefreshEndTurnLabel` :1123-1160) — the "waiting for other players" state.
- `LoadingWindow.PlayerLoadStatusGroup` — MP-only per-player load progress (:135-140).
- `GameMenuModalWindow` — host-specific quit confirmations
  (`%GameMenuQuitAsHostConfirmDescription` :251-262); `LoadSaveModalWindow` routes a
  non-host's save request through the server (:348-357); `VictoryAchievedModalWindow` hides
  Continue in MP (:132-135).
- **Diplomacy with a human** is the SAME window, minus the AI-only affordances:
  `NegotiationModalWindow` hides `DealApprovalGroup` (the mood gauge and smileys) when the
  partner is not AI-controlled (:666-675), and both the AI evaluation feedback (:973-1010)
  and Suggest Terms (:1076-1092) are AI-only. Nothing else differs; the pending-contract
  notification a human's proposal raises is `DiplomaticInteractionNotificationWindow`,
  already on `docs/roadmap.md`.
- **Desync / disconnect dialogs are ordinary message boxes.**
  `GameClientState_DisconnectedFromServer` :48-79 localizes
  `%DisconnectedFromServer<GameDisconnectionReason>` (13 reasons incl. `Desync`, `TimedOut`,
  `HostLeft`) and shows it through `ShowMessageNonBlocking`. `RuntimeState_Lobby` :755-780
  shows `%CannotJoinSession<LobbyFlag>` and
  `%WarningSessionRuntimeConfigurationMismatch` through `ShowMessage`.

### Deterministic action layer (§3 chokepoint), for free

Every lobby mutation is a string on the Steam lobby chat channel via `SessionHelper`
(`SessionHelper.cs:7-62`): `q:/join/<slot>`, `q:/lock/<slot>/<bool>`, `q:/faction/…`,
`q:/color/…`, `q:/name/…`, `k:/<steamId>/<reason>` for a kick. The mod does not need any of
this — every one of them is behind a declared button whose own handler the mod can replay —
but it is the answer if a lobby action ever has no button (e.g. an eliminated slot).

---

## 2. Mod coverage today

| Surface | Mod file | Status |
|---|---|---|
| Join-game list | `ES2Access/Screens/MenuDestinationScreens.cs:236-265` (`JoinGameListScreen`) | **MINIMUM PASS.** `WindowShape.Controls` picks up whatever the window drew with words (Join / Refresh / Back, and Fix Mods when visible). The `GuiTable` of lobbies — the entire content of the page — is not modelled. The class comment already says so. |
| The lobby, single-player parts | `ES2Access/Screens/NewGameScreen.cs` | Shipped and good. Panels from `NewGameScreenGuiElement`, settings via `SettingRows`, the competitor grid as one region per slot, the bottom button row taken from the BAND (so Start/Stop/Ready/Unready all arrive automatically — :376-390, and the class comment already anticipates this). |
| The lobby's MP-only widgets | same | **NOT COVERED.** `BuildCompetitorSlot` :348-360 declares exactly four rows — name, difficulty, faction, colour. `JoinButton`, `KickButton`, `LockToggle`, `ReadyIconGroup`, `EliminatedGroup`, `HostIcon` are absent. Nothing reads `GuiLocked` or `AfterJoinLoadingPanel`. |
| Lobby chat | same, :264-269 | **FIELD ONLY.** `SettingRows.AddTextField(chat.ChatTextField, …)`. `ChatLinesTable` / `ChatLinesScrollView` (the message history) are not declared and nothing narrates an arriving message. |
| Advanced settings incl. every timer | `AdvancedSettingsScreen` + `SettingRows` | Shipped; the MP timer settings are ordinary `SettingItem`s (`Public/Gui/Screens/GuiElements[NewGameScreen].xml:52-69`) and arrive with no extra work. |
| In-game chat | — | **NOT COVERED, AND CURRENTLY UNREACHABLE.** See §4. |
| End-turn MP cluster (sync, desync, per-player ready ring, players list, timers) | `ES2Access/Screens/GlobalHud.cs:1659-1700` | **NOT COVERED.** `Turn()` declares `EndTurnButton` + `RequestToggle` only. `SyncGroup`, `DesyncButton`, `CompetitorsCircularTable`, `PlayersListPanel`, `GlobalTimerLabel` and the arcs are absent. `EndTurnLabel` :1797-1805 DOES read `EndTurnTitle`, so re-reading the button says "waiting" — but the watcher (`AnnounceTurn` :111-130) watches the turn NUMBER, which does not change while you wait, so nothing announces entering or leaving the wait. |
| Coordination requests (alliance pins) | `GlobalHud.AddRequestToggle` :1763-1795, `GalaxyHudScreen` :2377,:2513-2600 | Partly shipped: the toggle and the map pins are modelled. `RequestsListPanel` (the send-a-request panel `EndTurnWindow.RequestTable` holds) is not. Note this is an ALLIANCE feature and alliances exist against AI too, so it is not strictly MP-only. |
| Disconnect / desync / cannot-join dialogs | `MessageBoxScreen`, `NonBlockingMessageScreen`, `ErrorScreen` | **Covered by construction** — they are ordinary `ShowMessage` / `ShowMessageNonBlocking` boxes. Unverified live. |
| Kicked-from-lobby box, join-request confirmation | same | Same: ordinary message boxes. |
| Loading screen per-player progress | `ES2Access/Screens/LoadingScreen.cs` | **NOT COVERED** — the screen reads `Diagnostics.Progress.Message`/`Current` only; `PlayerLoadStatusGroup` is untouched. |
| In-game settings + timer settings (host-only editable) | `ES2Access/Screens/GameMenuScreen.cs:96-105,190` | Shipped, incl. `InGameSettingsPanel` and `InGameTimerSettingsPanel`. The host-vs-client editability is the game's own `Enable`, so a client's read-only rows already read as unavailable. |
| Human-vs-AI negotiation differences | `NegotiationScreen` | Human negotiation is a strict SUBSET of the AI shape (fewer drawn widgets), so it should degrade correctly. Unverified. |
| `ModStrings` | `ES2Access/Core/Speech/ModStrings.cs:513,904` | One MP key: `screen.join-game` = "Join game". Everything else will be new. |

---

## 3. The narration chokepoint: lobby and session events ARE chat messages

This is the highest-leverage finding of the audit. Every session-level event the player needs
to hear is posted as a **system chat message** through `IChatClientService.PostMessage` with
`ChatMessageOption.TypeSystem`:

| Event | Where | Key |
|---|---|---|
| Player entered / left / disconnected / kicked (and the "…Yourself" variants) | `Session.cs:592-649` | `%LobbyChat{Entered,Left,Disconnected,Kicked}[Yourself]` |
| Host migration to you | `Session.OnLobbyOwnerChange` :651-666 | `%LobbyOwnerChangeYourself` |
| A player renamed | `RuntimeState_Lobby.TryChangeName` :827-846 | `%LobbyChatRenamed` |
| Launch countdown ticks (every second under 10, every 5 above) | `SessionState_OpenedAndCounting.Countdown` :84-108 | `%LobbyCountdownUpdate` |
| Synchronizing / synchronized | `SessionState_Synchronizing.cs:22,28` | `%LobbyChatSynchronizing`, `%LobbyChatSynchronized` |
| Whisper target not found | `ChatRecipientHook.cs:39` | `%LobbyUserNotFound` |
| Server-side session messages | `GameServer.cs:5417,5780`, `GameServerState_Transition.cs:28,108` | various |

The service is `ChatManager` (`ChatManager.cs:10-228`), registered as both
`IChatClientService` and `IChatControllerService` (:86-87). It offers exactly what a narrator
and a review buffer need:

- `event OnChatMessageReceived` (:66) — fires for local system messages and networked player
  messages alike, after the sender's name and colour have been resolved (:181-192).
- `ReadOnlyCollection<ChatMessage> ReadOnlyMessages` (:20, :77) — the whole history,
  cleared on session release (:214-217). A buffer needs no shadow copy.
- `ChatMessage` carries `Text`, `Option` (recipient scope + `TypeSystem`/`TypeUser`),
  `PlayerName`, `PlayerColorIndex`, `From`/`To` SteamIDs and `Time`.
  `ChatLine.Refresh` (:89-130) is the game's own formatting: `[PlayerName] text`, colourized
  per recipient scope (`ChatSystem`/`ChatNormal`/`ChatWhisper`/`ChatAlliance`/`ChatParty`).

So **one watcher on `OnChatMessageReceived` narrates the whole of multiplayer's session
life** — joins, leaves, kicks, the countdown, sync, and player chat — and `ReadOnlyMessages`
is the buffer behind it. Nothing else in the tree needs to be watched for lobby membership.

---

## 4. Risks

**R1 — the in-game chat is unreachable while the mod is live (blocker).**
The only route to the chat field is the `StartChatting` action
(`InGameChatPanel.HandleInput` :88-114 → `SetFocus` :116-125), and ES2 binds it to both Enter
and Tab (already recorded in `ES2Access/UI/Input/ModInput.cs:108` and
`ES2Access/Dev/DevProbe.cs:300`). The mod claims both keys on every screen and the game's
matcher is patched to see them as not pressed (`GameKeyStandDown`). In game a mod screen is
always focused, so `StartChatting` is never raised and the player can never open chat. The
suppression is right; what is missing is a REPLACEMENT ROUTE. Any chat stage needs an
owner-approved binding plus a mod-owned surface, and it should call the game's own
`InGameChatPanel.SetFocus()` rather than setting `AgeManager.FocusedControl` by hand (the
panel's own call site also starts the deferred `ActivateChatAtNextFrame` coroutine and leaves
discreet mode, :116-125).

**R2 — the chat field is a keyboard grab, and the stand-down does handle it.**
The prefab component is `AgeControlTextFieldChat`
(`Assembly-CSharp-firstpass/AgeControlTextFieldChat.cs`), a subclass of `AgeControlTextField`
that overrides `IsKeyExclusive => true` and `StandardCancel => false`. `IsKeyExclusive` is
what the mod's `ModInput.GameOwnsKeyboard`/`KeyboardIsElsewhere` (`ModInput.cs:258-290`) test,
so the whole mod layer stands down while the field holds focus — correct and already working.
`StandardCancel => false` means `InputManager` does NOT swallow Escape for this field; the
panel handles its own exit two ways: `HandleInput` returns true for Exit while focused
(`NewGameChatPanel` :35-38, `InGameChatPanel` :97-101) and `OnTextFieldKeyDownCb` clears the
engine focus on a raw `Input.GetKeyDown(KeyCode.Escape)` (`ChatPanel.cs:232-246`). So Escape
gets the player out and the mod layer back — verify live, do not re-implement.

**R3 — MP-only raw input reads.** Only inside the focused field, so all of them are behind the
stand-down: `ChatPanel.OnTextFieldKeyDownCb` reads Escape/Up/Down directly (`:232-246` — Up
and Down are the chat's own command history), and `AgeControlTextArea` reads
Return/KeypadEnter/Delete/arrows/Home/End and `Input.inputString`
(`Assembly-CSharp-firstpass/AgeControlTextArea.cs:227-294`). No MP code polls input outside a
focused text field. `GalaxyViewCameraController`, already patched, is unchanged by MP.

**R4 — `PlayersListPanel` is pointer-gated and the mod has no cursor.**
`EndTurnWindow.SpecificUpdate` :906-921 shows it only while
`AgeManager.Instance.Cursor` is within half the end-turn button's width. The mod never writes
the AGE cursor (grepped: no `AgeCursor` / `Instance.Cursor` writes anywhere in `ES2Access`), so
a `Drawn`-style visible-and-opaque check will never pass. The panel's content must be read
from the model instead — `IPlayerRepositoryService.AsReadOnlyList()` plus
`PlayerHelper.ComputePlayerState(player, endTurnWindow.AreAIsReady)`, which is verbatim what
`PlayerStatusLine.Refresh` does (`PlayerStatusLine.cs:35-70`; `PlayerState` is
`Unset/Ready/Playing/PlayingButInEncounter/ReadyButInEncounter/InTalks`, spoken as
`%PlayerSync<State>Title`, with `%PlayerSyncInEncounterTitle` collapsing the two encounter
states). `CompetitorOrbitalSlot` (the always-visible ring) reads the same expression and is
NOT pointer-gated, so it is the cheaper source for "who is still playing".

**R5 — the lobby's transient states will read as a wall of "unavailable".**
`GuiLocked` disables the empire panel and switches off Join/Lock/Invite, and it arrives
5 seconds before launch; `AfterJoinLoadingPanel` covers a joiner's lobby before the slots
replicate but is a plain `GuiPanel`, so `GuiManager.IsAnyModalVisible` is false and
`AgeWidgets.Operable(window.AgeTransform)` is still true — the mod's `IsActive`
(`NewGameScreen.cs:91-115`) will happily present a lobby whose competitor panels are drawing
nothing (both panels return early while `LobbySlotProvider.Count < 2`,
`NewGameCompetitorSlotsPanel.Refresh` :112-115, `NewGameEmpireSlotPanel.Refresh` :95-98).
Both states need an explicit announcement rather than a silent half-page.

**R6 — hosting Public advertises your lobby worldwide.** `Session.Reopen` is called the moment
the SessionMode setting changes (`NewGameScreen.ModifySetting` :262-280). For testing use
**Protected** (friends-only): strangers' clients filter it out of their lists
(`LobbyInfo.IsVisible` :120).

**R7 — pressing Start in an MP lobby launches the game after 10 seconds** and the lobby locks
at T-5s, so there is a ~5 second window in which Stop (the same `OnClickStartCb`) cancels.
`docs/test-recipes/modals-and-outgame.md`'s "never press Start" rule needs an MP-specific
amendment, not a relaxation.

**R8 — loading a multiplayer save lands in the LOBBY, not in the game.**
`RuntimeState_Lobby` :283-296 shows `NewGameScreen` whenever `SessionMode != Single`, even
with a `GameSaveDescriptor` present. So `POST /loadsave` against an MP save reaches the lobby
and `wait-game.ps1 ingame` would time out. Worth a line in the dev-loop notes once confirmed
live.

---

## 5. What is reachable single-handed

**Reachable solo (given a running Steam client, signed in):**

- The join list against real public lobbies: open it, refresh it, read rows. Contents are
  non-deterministic (someone else's lobbies), so assertions must be shape-level, and the
  Join button's refusals are reachable deliberately (no selection; a modded lobby).
- **Hosting.** Set the lobby's Session panel → SessionMode → Protected. That is an ordinary
  `SettingItem` drop list the mod already models, and `Session.Reopen` makes the page a
  multiplayer lobby: chat enabled with a live line list, `InviteButton` visible,
  Start/Stop instead of Start alone.
- **Every free-slot control**, because `LobbySlot.IsFree == IsAI` (`LobbySlot.cs:202`): all
  seven AI competitor slots draw a `JoinButton` and a `LockToggle` in a hosted MP lobby.
  Lock/unlock is a safe reversible round trip; Join moves the local player between slots.
- **Ready/Stop and the countdown**: press Start, hear the `%LobbyCountdownUpdate` ticks and
  the `GuiLocked` lock, press Stop within ~5s.
- **The whole chat mechanism**: posting a message calls `ReceiveMessage` locally for
  `ScopeLocal` and comes back through `OnChatMessageReceived` either way, so send-and-hear is
  a solo test. `/clear` and the Up/Down history are solo. Every system message in §3 except
  join/leave/kick is solo (countdown, sync, rename).
- **An in-game multiplayer session**: launch the hosted Protected lobby solo with AI
  competitors. `SessionMode != Single` in game, so the sync group, the competitor ready ring,
  the timers (if enabled in advanced settings), the MP loading progress, the host-specific
  quit confirmations, `%EndTurnWaitingTitle`, and the in-game chat window all become live.
  This is the single most valuable fixture and it needs no second person.
- MP saves: save from that session, reload → lands in the lobby with
  `IsSavedGame`/`GameLaunchedOnce` set (R8), which exercises the "game in progress" lobby
  states and `EliminatedGroup` on a slot whose empire died.

**Structurally untestable single-handed** (manual script / a future two-instance test with a
second Steam account):

- `KickButton` and the kicked player's `%KickedFromLobbyTitle` box — needs a second human
  (`RefreshStates` :230 requires `LobbySlot.IsHuman` on a slot that is not the host's).
- The joiner's side: `AfterJoinLoadingPanel`, `ReadyButton`/`UnreadyButton` (both require
  `!IsHosting`), read-only settings as a client, a client's save-through-the-server path.
- Whispers (`/w`, and `CompetitorOrbitalSlot.OnSlotCb`), the Alliance chat tab, incoming
  messages with a real sender name/colour.
- Host migration (`%LobbyOwnerChangeYourself`), desync (`DesyncButton`,
  `SynchronizationState.ChecksumMismatch`), `%DisconnectedFromServer*` for anything but a
  self-inflicted quit, `LobbyFlag.GameIsMigrating` / `VersionMismatch` rows in the join list.
- Human-vs-human diplomacy (the hidden `DealApprovalGroup`, a pending contract waiting on
  another player).
- The Steam overlay invite dialog (it renders outside the game's GUI entirely — an
  accessibility dead end the mod cannot fix; it should at least announce what the button
  does).

---

## 6. Recommended stage plan

Sequential unless marked; all live stages share the one game instance. Owner approval needed
for one new binding (M4) and for the layer number a chat surface would take.

**M0 — gate check (30 min, live, can fold into M1).**
`/eval`: `Services.GetService<ISteamService>().IsSteamRunning`,
`Amplitude.Unity.Framework.Application.Preferences.EnableMultiplayer`, and that setting the
lobby's SessionMode to `Protected` reopens the session (`ng.Session.SessionMode`,
`ng.Session.IsHosting`, `ng.Session.SteamIDLobby`). If Steam is not running, M1 degrades to an
empty table and M2/M3/M5 are blocked — say so before starting them.

**M1 — the join-game list, real model.** Replace `JoinGameListScreen`'s minimum pass.
`TableSheet` over `JoinGameScreen.Table`; ten columns from
`Public/Gui/GuiElements[Tables].xml:60-71`, headers `%JoinGameSessionListTable<Column>Title`.
Three columns need hooks: `PlayerCount` is a `GuiTableCellRatio` drawing `n/m` from
`GuiLobbyInfo`'s `IRatioProvider` (current players / slot count), `Content` is a
`GuiTableCellDownloadableContent` strip of DLC items, `Runtime` is a `GuiTableCellMods` whose
label and tooltip come from `GuiMod.GetModConfigurationState`. Rows the game refuses are
already `TableSheet`'s refused-row case (`JoinGameScreen.Refresh` :236-254 passes
`invalidLobbyInfo` as the disabled set). Plus: the Join/Refresh/FixMods/Back buttons with
their refusal sentences (`Gui.FormatFailure("%JoinGameJoinButtonDescription", …)`), and an
announcement for the async refresh — the Steam callback
(`SteamMatchMaking_CallbackRequestLobbyList` :382-425) is the ONLY thing that says the list
arrived, and `State.Refreshing` disables both buttons meanwhile. Fixture: real public lobbies.

**M2 — the lobby in multiplayer mode.** All of §1's lobby table, added to the existing
`NewGameScreen`: the six per-slot state widgets (Join, Kick, Lock, ready, eliminated, host),
the slot's name row saying human/AI/free/locked/ready/host, `InviteButton` announcing that it
opens the Steam overlay, and the two transient states from R5 — `AfterJoinLoadingPanel` as a
"connecting" announcement (and probably `IsActive == false` while it is up), `GuiLocked` as a
"lobby locked, launching" announcement instead of thirty "unavailable"s. Verify the bottom-row
band picks up Stop/Ready/Unready as the class comment predicts, and that the swap under a
standing cursor is announced. Fixture: solo-hosted Protected lobby; Kick and the joiner's two
buttons are fixture-blocked.

**M3 — session narration + a chat buffer (the biggest win per line).** One watcher on
`IChatClientService.OnChatMessageReceived`, pumped through the existing speech path, covering
§3's whole table; a review buffer backed by `ReadOnlyMessages`; the lobby chat panel's
`ChatLinesTable` declared as reachable lines. De-noising rules will be needed (a countdown tick
every second is fine; a chat sound per line is the game's, `ChatPanel.AddLine` :142-145).
Design decision for the owner: does an incoming chat message interrupt, queue, or only cue?
Solo-testable end to end.

**M4 — the in-game chat as a reachable surface (needs an owner-approved binding).** Resolves
R1. A mod-owned surface over the galaxy that lists the lines, exposes the two tabs
(`TabsRadioGroup` / `ChatTabs`, alliance tab conditional on `CanShowTab`), the
`NotificationButton`, and hands the keyboard to the field through the game's own
`InGameChatPanel.SetFocus()`. Escape must return through the panel's own
`SetDiscreet(true)`. Note `/w <name>` and `/clear` exist as text commands
(`ChatRecipientHook`, `ChatPanel.OnTextFieldValidateCb` :206-230) and are the only whisper
route a keyboard user has, since the other one is a click on a pointer-driven orbital slot.
Also fix the known "editors end in silence" gap here if it is not fixed by then: after Enter
the field keeps focus and the only feedback that a message was sent is M3's narration.

**M5 — multiplayer turn status.** `GlobalHud.Turn` additions: the sync readout
(`SyncStatus<state>` description), `DesyncButton` (host + checksum mismatch only), the
per-player ready ring read from `CompetitorOrbitalSlot`s, the players list read from the MODEL
per R4, the global timer and the arcs, and a watcher for the wait itself — entering
`%EndTurnWaitingTitle` and "still waiting on N players" — because the existing turn watcher
only sees the turn number change. Fold in `LoadingWindow.PlayerLoadStatusGroup` (small).
Fixture: the solo-hosted session in progress.

**M6 — manual/two-instance script.** Everything in §5's untestable list, written into the
session's manual test report: kick both sides, the joiner's lobby, whispers and the alliance
tab, host migration, a deliberate desync if one can be provoked, and one human-vs-human
negotiation.

Sequencing note: M1 is measurement-independent of M2 (different window, different helper) and
could be pipelined in a worktree, but both touch `ModEntry.cs` /
`ModStrings.cs` / `locale/english.json`, so if they overlap exactly one of them owns those
three files.

---

## 7. Doc placements these findings imply (for the main agent)

- `docs/install.md`: multiplayer is Steam-lobby-only, three session modes with Private
  disabled, no passwords; `EnableMultiplayer` is the inverse of the modding-tools flag;
  session events are system chat messages (§3 table); `LobbySlot.IsFree == IsAI`;
  `PlayersListPanel` is pointer-gated; the 10s countdown with a 5s lock; loading an MP save
  lands in the lobby (R8); `AgeControlTextFieldChat` overrides `StandardCancel` to false.
- `docs/roadmap.md`: replace the one-line "multiplayer join (deferred)" with the M1–M6 stages
  (remaining work only).
- `docs/test-recipes/modals-and-outgame.md`: the hosting recipe (SessionMode → **Protected**, never Public), the
  Start/Stop countdown window, and a correction to the existing line "the multiplayer-only
  states (chat, Join/Kick/Ready, the DLC strip) have no fixture at all" — with Steam running,
  everything except Kick and the joiner's side DOES have a fixture.
- `docs/interaction.md`: the chat binding (M4) once approved, and the chat surface's layer.
- The chat watcher/buffer M3 builds documents itself in its own doc comment.

## 8. Generic-docs candidates (main agent applies the bar)

1. **A claimed key can be the only route to a game surface.** `docs/generic/input.md:71-75`
   treats an Enter/Tab-bound chat purely as a grab hazard ("treat any collision that can move
   the game's focus as a blocker"). It does not say the converse: once the mod suppresses that
   key, the surface it opened is gone, and the mod owes a replacement route. Cheapest form is
   one clause on the existing bullet. Not yet paid for — it becomes paid the moment M4 has to
   invent that route.
2. **A panel gated on the pointer's position is data, not a drawn widget.** No generic line
   covers it; the nearest is `reverse-engineering.md:34-36` (prefer patching the pointer
   sensor so the game reads your cursor as the mouse). Arguably already answered there, so
   likely a REJECT or at most a pointer from `ui-navigation.md`.
3. **One screen serving both single and multiplayer.** The generalizable shape — a session mode
   parameterizing an already-modelled screen, so networked play is conditional widgets rather
   than new screens — is not stated anywhere. Only worth a line if a second game shows the
   same shape; ES2 alone is not evidence.
