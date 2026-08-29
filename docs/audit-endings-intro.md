# Audit — game-end surfaces and the intro faction video

Decompile + mod-source only (no live game). Cites are `decompiled/Assembly-CSharp/...` unless
marked otherwise. Anything the prefab decides (widget visibility inside a group, extra labels)
is marked PREFAB-UNVERIFIED — no grep can recover it.

---

## A. Every way an ES2 game ends

### A0. The one server chokepoint

`GameServerState_CheckForGameEndingConditions.RunAsync` (`:43-98`) is the only place a game can
end. Each turn it (1) posts `OrderEliminateEmpire` for every empire `MarkedForElimination`
(`:45-53`), then (2) asks `IVictoryManagementService.ServerCheckForVictoryConditions` (`:70`),
which posts a single `OrderAchieveVictory` naming every empire that qualified this turn
(`VictoryManager.cs:314-318`). If nothing qualified the state just falls through to
`GameServerState_Turn_Dump` (`:77-79`) — **there is no draw/stalemate/"turn limit reached with no
winner" surface at all.** A turn-limit game ends only because a victory condition's own
formula fires on `Turn` / `GlobalGameTimerProgress` (`VictoryManager.RefreshInterpreterContext`
`:689-698`), and that goes down the identical order path.

So every ending is one of exactly **two client windows**, plus the score screen they both lead to.

### A1. `VictoryAchievedModalWindow` — victory AND "someone else won"

Shown from `GameClient.AchieveVictoryProcessor` (`GameClient.cs:417-425`): binds the victory type
the PLAYER achieved (empty string if the player is not among the winners) and shows the window.

- Outcome word: `OutcomeTitle` = `%NotificationVictoryAchievedTitle` + `"Victory"`/`"Defeat"`,
  chosen from `victory = (Gui.PlayerEmpire as MajorEmpire).HasAchievedAnyVictory()`
  (`VictoryAchievedModalWindow.cs:94`, `:126`). English: "You are Victorious" /
  "You have been Defeated".
- One paragraph (`DescriptionLabel`) naming the winner and the condition, or a `Multiple` variant
  (`:254-262`); a per-winner-per-victory LIST (`WinnerList`) only when more than one empire
  qualified (`:181-190`).
- `ContinueButton.Visible = session.SessionMode == SessionMode.Single` (`:131-135`) — so **the
  single-player defeat case is a "continue playing" case too**, and multiplayer has no Continue.
- `ScoreScreenButton` ENDS the session: `Disconnect(GameHasEnded)` + `BlackCurtainWindow`
  (`:274-282`). Refused while saving, with the game's own sentence
  (`DisableButtonIfSavingOrNotReady`, `:158`).
- `HandleInput` returns `true` for every action (`:47-50`) — no key dismisses it.

**Coverage: COMPLETE.** `ES2Access/Screens/VictoryAchievedScreen.cs` binds exactly this class,
layer 49, reads `OutcomeTitle` as the screen name, the paragraph as the first node, the winner
list when drawn, and `WindowShape.Controls` for both buttons (invisible Continue drops out by
itself in multiplayer). `Back()` returns false, matching the window's swallow-everything.
No change recommended.

### A2. `EmpireEliminatedNotificationWindow` — the player is knocked out (a DIFFERENT class)

This is the answer to "is defeat the same window?" — **no.** An eliminated player never sees
`VictoryAchievedModalWindow`; they get a *notification*, not a modal.

Path: `OrderEliminateEmpire` → `GameClient.EliminateEmpireProcessor` (`:2292-2316`) sets
`HasBeenEliminated`, marks the lobby slot eliminated, cancels exchanges, and raises
`EventEmpireEliminated` → `NotificationEmpireEliminated` → `EmpireEliminatedNotificationWindow`
(`NotificationEmpireEliminated.cs:52-56`). Who marks an empire: `Empire.MarkedForElimination`
(`GameClient.cs:2837`, the mark order) and `QuestExecutionTreeAction_EliminateEmpire:47`.

When the eliminated empire is the player's, the window changes character:

| Behaviour | Cite |
|---|---|
| Pops up unconditionally, ignoring the auto-popup preference | `NotificationEmpireEliminated.cs:36-44` |
| Not dismissible | `NotificationEmpireEliminated.cs:50` (`IsDismissible`) |
| `OnDismissCb` / `OnMinimizeCb` become NO-OPS | `EmpireEliminatedNotificationWindow.cs:67-81` |
| `HandleInput` swallows every action (Escape included) | `EmpireEliminatedNotificationWindow.cs:19-26` |
| Two prefab groups swap: `EliminationGroups` shown, `NormalGroups` hidden | `:52-65` |
| One route out: `ScoreScreenButton` → black curtain, `VictoryScreen.Bind(fromJournal:false)`, `Disconnect(GameHasEnded)` | `:83-92` |

**There is NO spectate-after-defeat mode.** (`Spectator` in this codebase is only an encounter
contender role — `EncounterContenderRole.cs` etc.) The eliminated player is held on this popup
until they press Score screen, which ends the session; they are not kicked to the menu directly.

**Coverage: PARTIAL — three concrete concerns.**

The mod does bind the class: `NotificationScreen.cs:2712-2724` registers a `Variant` whose only
content is a gateway on `ScoreScreenButton` (fallback name `notify.open-score-screen`). So the
one live route IS reachable by keyboard. What is missing or unverified:

1. **The popup never says the game is over.** `NotificationEmpireEliminated.GetDescription()`
   (`:73-81`) returns `%NotificationEmpireEliminatedDescriptionKnown` = *"The empire of {0} has
   been eliminated"* — **the same sentence for the player's own elimination as for an AI's**
   (verified in `Public/Localization/english/ES2_Localization_Locales.xml:4545-4547`). Whatever
   tells the player "this is your defeat" lives in the `EliminationGroups` widgets, and the mod's
   notification reading declares only the shared description label + controls + registry-declared
   tables (`NotificationScreen.Build`, `:262-289`) — arbitrary labels inside a popup-specific group
   are NOT read. PREFAB-UNVERIFIED: needs one live sighting to see what those groups contain.
   Recommended fix shape: add `Tables`/readouts for `EliminationGroups` to the existing variant
   (only the groups the window has made visible), so the elimination text reads where it is drawn.
2. **Dead Dismiss/Minimize.** The base `NotificationWindow` keeps both buttons visible
   (`NotificationWindow.cs:132-165` only touches `ShowLocationButton.Visible`), and the mod declares
   both unconditionally (`NotificationScreen.cs:2175`, `:2261`). If the prefab does not put them in
   `NormalGroups`, a keyboard player gets two nodes that silently do nothing. PREFAB-UNVERIFIED —
   fix only after sighting; if they are visible-but-dead, the right treatment is the mod's existing
   refusal treatment keyed on `GuiNotification.IsDismissible` (which the game already computes),
   not hiding them.
3. **Escape is silent** on the player's own elimination (`HandleInput` returns true and does
   nothing). The mod's `Back()` returns false, so the key reaches the game and dies there. Same
   shape as the already-approved silence on `VictoryAchievedModalWindow`; recording it, not
   proposing a change.

Layer sanity: notification is 18, victory-achieved 49 — if a victory modal and an elimination
popup are up together (the player eliminated on the turn someone won), the modal wins, matching
the engine's draw order. Correct.

### A3. `VictoryScreen` — the score screen, from three doors

Three openers, all ending in the same window:

| Door | Cite |
|---|---|
| Victory/defeat modal's Score screen | `VictoryAchievedModalWindow.cs:96-97` (bind), `:274-282` (disconnect) |
| Elimination popup's Score screen | `EmpireEliminatedNotificationWindow.cs:88` |
| A row in the journal | `GuiTableCellScoreScreenButton.cs:28-37` (`fromJournal: true`) |

The first two disconnect with `GameHasEnded` → `GameClientState_DisconnectedFromServer` maps that
reason to `GuiRedirection.EndGameMenu` (`:87-90`) → `GameClientState_Release.ReleaseAndRedirect`
posts `RuntimeState_OutGame` with `typeof(VictoryScreen)` and hides all modals (`:76-84`). The
mod's `VictoryScreen` (layer 0, with the main menu) is correct for that: it REPLACES the menu.

Outcome on this page: drawn as artwork (`BackgroundImage` = `VictoryScreen{condition}{Victory|Defeat}`,
`VictoryScreen.cs:73-75`) **but also as text** — `VictoryScreenScoresPanel.ShowVictoryStatus`
writes `ScoresScreenTitle` = `%VictoryScreen{Victory|Defeat}{Player}Title` ("You are victorious!" /
"You have been defeated!") and a lore paragraph (`VictoryScreenScoresPanel.cs:170-178`;
localization at `ES2_Localization_Locales.xml:8390`, `:8476`). The mod's `Panel()` reads every line
of the visible panel via `WindowShape.Readouts`, so **the outcome does reach the player** as a
panel line. Optional polish only: hoisting it into `ScreenName`.

Exit routes are visibility-driven (`Bind`, `:83-86`): `BackToMenuButton` when not from the journal,
`BackToJournalButton` when it is, `GoToJournalButton` only when `!fromJournal &&
!Preferences.EnableModdingTools`. All read off what is drawn — the mod needs no case analysis.
`HandleInput` on Exit = `BackToPreviousMenu` (`:104-112`), matching the mod's `Back() == false`.

**Coverage: COMPLETE for the shape.** The unmodelled score graphs/podiums are a declared,
owner-visible stopping point in the screen's own doc comment.

### A4. The victory OUTRO video (a `CutsceneModalWindow`, played over the score screen)

`VictoryScreen.OnBeginShow` (`:165-171`) plays a video when `victory && !fromJournal`:
`ShowMetaplotVideo()` first (only for `MetaplotState.LostBack` + success, `autoHide: false`,
`:243-262`), else `ShowFactionVideo()` (`:275-305`, `autoHide: true`, subtitles specifier =
metaplot state). `WatchVideoButton.Visible = victory` replays it (`:171`, `:238-241`).
Never played for an eliminated player (`playerEmpire.IsEliminated` short-circuits `victory`, `:158-163`).

**Coverage: COMPLETE by inheritance.** Same `CutsceneModalWindow` the mod's `CutsceneScreen`
models at layer 47, which sits above the score screen at 0, so the subtitles read and the score
screen is merely covered. Subtitle files exist for the outros
(`EndlessSpace2_Data/StreamingAssets/Movies/Factions/*_Outro.LostBack.Subtitles-english.xml`).
Note the `autoHide: false` metaplot chain: skipping it does not hide the window, it unloads the
video and immediately chains into the faction video (`UnloadVideo` → `ActionVideoPlaybackComplete`
→ `OnMetaplotVideoFinished` → `ShowFactionVideo`), so `Shown` stays true across the seam and the
mod's cutscene screen does not blink. Verified by reading, not live.

### A5. `JournalModalWindow` — DEFECT: the past-game score screens are keyboard-unreachable

`Public/Gui/GuiElements[Tables].xml:148` — the `EndGameSummaryTable`'s last column is
`Prefab="Prefabs/Gui/Table/TableCellScoreScreenButton"`, `DisableSorting="true"`, header caption
`%EndGameSummaryTableEndGameSummaryTitle` = "Details", tooltip "Click on the **buttons** to see
advanced stats on the game". That cell class carries the two per-row actions:
`OnScoreScreenCb` (opens the score screen for that finished game) and `OnDeleteEntryCb` (deletes
the entry behind a confirmation) — `GuiTableCellScoreScreenButton.cs:28-45`. `interactiveCells`
defaults to `false` in `EndGameSummaryGuiTable.Bind` (`JournalModalWindow.cs:40`,
`GuiTable.cs:124`) but that flag never reaches these buttons — the base `GuiTableCell.Bind`
ignores its `interactive` argument (`GuiTableCell.cs:39-57`) and so does this subclass — so the
buttons are live for a mouse.

`ES2Access/Screens/JournalScreen.cs:41-45` sets only `_table.RowName`; it supplies **no
`ActivateCell` and no `ReadCell`**. Per `UI/TableSheet.cs:57-64`, that means the Details cell reads
as an ordinary (empty) value cell and Enter on the row does the row's own click. And the row's own
click goes nowhere: `GuiTableLine.OnLineSelectionCb` / `OnLineDoubleClickCb` `SendMessage`
`"OnLineSelection"` / `"OnLineDoubleClick"` to the selection client (`GuiTableLine.cs:204-214`) and
`JournalModalWindow` implements neither. So today the journal lists finished games and offers no
way to open or delete one — **a dead end, and the only door to a past game's score screen.**

Two consequences to fix together:
1. Give `JournalScreen` a `ReadCell` for the `EndGameSummary` column that declares the cell's own
   buttons (precedent: `EmpireScreen.cs:85` + `ActionCell` at `:362`, which is exactly the
   "cell holds a control" case; note `TableSheet`'s rule that a cell's own button is handed back
   as `AgeWidgets.PressPropagating`, never a bare press).
2. Reconsider the rows-as-radios reading. `canSelect` defaults to true so `LinesTable.Enable` is
   true and `TableSheet` reads each row as a selectable choice, but selecting a journal row does
   nothing at all. This is a case the shared table rule cannot see (the flag is on, the handler is
   absent) and the honest reading is plain lines. Owner call; smallest fix is a per-screen override.

Both are code-verifiable offline and confirmable live with zero fixture cost (the journal is
reachable from the main menu, and a session with no finished games shows the empty-case line the
screen already handles).

### A6. Not endings (checked and excluded)

- `NotificationVictoryImminent` / `NotificationAllianceVictoryImminent` — plain
  `InformationNotificationWindow` warnings raised by `ClientCheckForAlerts`
  (`VictoryManager.cs:429-441`; window binding at `NotificationVictoryImminent.cs:41`). Covered by
  the generic notification screen.
- `NotificationPirateEmpireEliminated` — also `InformationNotificationWindow`
  (`NotificationPirateEmpireEliminated.cs:37`). Not a game end.
- `BlackCurtainWindow` — an empty `GuiWindow` (`BlackCurtainWindow.cs`) used as a transition
  cover. Nothing to read; correctly excluded from the mod because `GuiManager.IsInLoadingWindow`
  counts it (`GuiManager.cs:339`), so the galaxy stands down under it.
- Multiplayer disconnects (`HostLeft`, `Desync`, `TimedOut`) raise the game's own non-blocking
  message box (`GameClientState_DisconnectedFromServer.cs:60-80`) — the mod's
  `NonBlockingMessageScreen` territory, and multiplayer is deferred by the roadmap anyway.

### A7. How a live stage can reach each end surface

All of these are the game's OWN debug call sites (`DebugUIWindow_Victory.cs`), replayed verbatim
through `/eval`. They mutate the session, so: load the `unlocked` save, never save, restart the
game after.

```csharp
// player wins (any enabled condition) — DebugUIWindow_Victory.cs:181
var vm = Services.GetService<IVictoryManagementService>();
var def = null; foreach (var d in vm.VictoryConditionsFilteredThisGame) { def = d; break; }
vm.ForceRiseVictory(def, Gui.PlayerEmpire);          // → VictoryAchievedModalWindow, "Victory"

// player loses because an AI won — same call, another empire → "Defeat" + Continue in single player

// player is eliminated — DebugUIWindow_Victory.cs:100-102
Gui.PlayerEmpire.PlayerControllers.Server.PostOrder(
    new OrderEliminateEmpire(Gui.PlayerEmpire.Index));  // → EmpireEliminatedNotificationWindow
```

Preconditions worth knowing: victory checks are skipped on turn 0 and after
`HasAlreadyWon` (`GameServerState_CheckForGameEndingConditions.cs:61-64`), and
`AchieveVictoryProcessor` refuses an already-eliminated empire (`GameClient.cs:398-401`) — so run
the elimination test LAST.

---

## B. The intro faction video

### B1. The mechanism

- **Option**: `IGameplayOptionsService.EnableFactionIntroductionVideos`, an
  `[OptionTypeToggle("EnableFactionIntroductionVideos", Priority = 20, Default = true)]`
  (`IGameplayOptionsService.cs:9-10`), stored at the registry key built in
  `GameplayOptionsManager.cs:12`. **Default ON.** Subtitles are a second option,
  `DisplaySubtitles`, also `Default = true` (`IGameplayOptionsService.cs:6-7`).
- **Trigger**: `GameClientState_Introduction.Begin` (`:25-53`) — the client state between loading
  and `GameClientState_Turn_Main`. It skips the video for `-novideo` on the command line or any
  non-single session (`:30-44`), then calls `PlayIntroductionVideo()` (`:111-148`), which resolves
  the player faction's affinity `Movies[Gui.MovieType.Intro]` under `streamingAssetsPath` and does
  `GetWindow<CutsceneModalWindow>().ShowWindow(path, OnVideoPlaybackComplete)` (`:141-146`).
- **So it is the SAME surface family the mod already models**: `CutsceneModalWindow`, the mod's
  `CutsceneScreen` at layer 47 — whose doc comment already names "the faction introduction a new
  game opens with" as one of its four cases. Not a distinct window.
- **Subtitles exist for every intro**: 12 `*_Intro.mp4` in
  `EndlessSpace2_Data/StreamingAssets/Movies/Factions/` and a matching
  `*_Intro.Subtitles-english.xml` for all 12. `CutsceneModalWindow.InitializeSubtitles` resolves
  `Path.ChangeExtension(moviePath, ".Subtitles-<language>.xml")` (`:198-240`) and the play loop
  raises/hides `CutsceneSubtitle` per cue (`:264-292` MovieTexture path, `:312-341` AVPro path) —
  which is exactly what `CutsceneScreen.Subtitle` watches. So with both options on, the intro
  video is narrated line by line today.
- **"The video is playing" as a predicate**: three candidates, in order of quality.
  `GuiManager.IsInCutscene` (`GuiManager.cs:341`) — the game's own, `Visible` on either cutscene
  window. `GameClientState_Introduction.VideoInProgress` (a public static, `:12`, `:118`/`:123`,
  cleared in `End`, `:93-97`) — intro-specific. And the mod's own existing test,
  `CutsceneModalWindow.Shown` (`CutsceneScreen.IsActive`), which is the one already in use and the
  earliest to flip (`Shown => (Visible && !Hiding) || Showing`, `Amplitude.Unity.Gui/GuiPanel.cs:69`).

### B2. Is the galaxy readable during the video today? NO — verdict: no bug

`GalaxyHudScreen.IsActive()` (`ES2Access/Screens/GalaxyHudScreen.cs:182-198`) requires
`!gui.IsAnyModalVisible`. The chain that makes that decisive:

1. `CutsceneModalWindow : GuiModalWindow` (`CutsceneModalWindow.cs:15`).
2. Every `GuiModalWindow` is registered into `GuiManager.guiModalWindows` with a
   `VisibilityChanged` subscription at `Load_IGuiGamePanelService` (`GuiManager.cs:1432-1437`),
   which runs from `GuiManager.Load` at app boot (`:1970`) — not per game, so the list is always
   populated before any intro.
3. `GuiModalWindow.OnBeginShow` calls `NotifyVisibilityChanged()` (`GuiModalWindow.cs:62`) →
   `GuiManager.ModalWindow_VisibilityChanged` recomputes `IsAnyModalVisible` from each modal's
   `Shown` (`GuiManager.cs:1750-1764`). So `IsAnyModalVisible` is **true from the first frame of
   the cutscene's show animation**.
4. `Gui.GuiService.ShowWindow` is synchronous for a loaded, game-ready window
   (`GuiWindowsStack.ShowWindow`, firstpass `:77-91`) — no deferral, so there is no gap frame.
5. There is no earlier gap either: the `LoadingWindow` is still up when the cutscene shows and is
   hidden only later, from inside the cutscene's own `PlayMovie` coroutine
   (`CutsceneModalWindow.cs:262`, `:310`). The mod's `LoadingScreen` (layer 60) therefore covers
   the seam, and it sits ABOVE the cutscene screen, so the announcement order is loading → cutscene.
   The galaxy is never the top screen.
6. Even if the galaxy screen were active-but-covered, it would be silent: `ScreenManager` speaks
   only the top screen's name on focus change and runs only the top screen's `OnUpdate`
   (`ES2Access/Screens/ScreenManager.cs:86-100`, `:183-206`), and every galaxy/HUD announcement
   goes through `GalaxyHudScreen.OnUpdate` → `_hud.Update()` / `_fleetPanel.Update()` (`:219-222`).
   Nothing in the mod outside `Screens/` speaks except the navigator and the review buffers, both
   driven by the focused screen (grep: `Voice.Say` outside `Screens/` is only
   `UI/GraphNavigator.cs` and `UI/BufferController.cs`).

Supporting (not load-bearing): the game also switches the world camera off for the whole
introduction state — `Gui.HideGalaxyForOptimization()` on `Begin` and
`ShowGalaxyForOptimization()` on `End` (`GameClientState_Introduction.cs:49`, `:98`;
`Gui.cs:1564-1580`) — so the galaxy is not even rendered while the video plays. Reading it would
have been describing an invisible screen.

**The correct gate is the one already in place**: the galaxy stands down on
`GuiManager.IsAnyModalVisible`, which the intro video sets because it is a `GuiModalWindow`. No
new predicate (`IsInCutscene`, `VideoInProgress`) is needed, and adding one would duplicate the
existing rule. Recommendation: **no code change; verify live and record the finding.**

One caveat about the tail, not the video: `GuiModalWindow.OnBeginHide` also fires
`NotifyVisibilityChanged` (`GuiModalWindow.cs:90`), and at begin-hide `Shown` is already false
(`Visible && !Hiding` fails). So `IsAnyModalVisible` drops the moment the video starts fading out
— both screens flip on the same frame, cutscene popping and galaxy pushing, so the galaxy is
announced one fade early rather than late. That is the roadmap's existing "departing-fade
stand-down" item, not an intro-video bug, and here it is benign (the video is over).

Also pre-existing and worth restating because the intro video is where a player meets it first:
while the mod's cutscene screen has focus it claims Enter, so **Enter no longer skips the video**
(Escape and the mouse still do) — the roadmap's open "Cutscene keys" decision. A 12-faction set
of intro videos is the strongest argument yet for resolving it.

### B3. Live verification recipe (for the stage that owns the game)

1. Confirm both options are on: Options → Gameplay → "Display subtitles" and "Faction
   introduction videos" (`EnableFactionIntroductionVideos`, `DisplaySubtitles`, both default true).
   Do NOT launch with `-novideo`, and check `run-game.ps1` does not pass it.
2. Start a NEW single-player game (multiplayer suppresses the video —
   `GameClientState_Introduction.cs:37-40`). Never save.
3. While the video plays, assert, in this order:
   - the mod's current screen is `screen.cutscene`, and `screen.galaxy` is **not in the stack** at
     all (not merely not-current) — the stack, not just `Current`, is the evidence that answers the
     owner's question;
   - `Gui.GuiGameWindowService.IsAnyModalVisible == true` and `IsInCutscene == true`;
   - the speech log contains only subtitle lines — no empire totals, no system names, no "galaxy".
4. Let the video run to its end (do not skip): assert the stack becomes galaxy (plus whatever
   empire-introduction notifications the state then raises —
   `GameClientState_Introduction.Run:62-75`) and that the galaxy is announced exactly once.
5. Repeat once with Escape pressed mid-video, to check the skip path lands the same way.
6. Optional negative control: turn the option OFF, start another new game, and confirm the galaxy
   is announced with no cutscene screen in between.
