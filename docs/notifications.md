# ES2 facts — notifications, quests and endings

The notification pipeline and the events that feed it, what a popup actually draws, the show-location
button, quests and the journal, the tutorial popup, and the end of a game. Index and charter:
`README.md`.

## The notification pipeline and its events

- **The notification pipeline is a closed map the mod has joined** (2026-08-20):
  `IEventService.EventRaised` → `GuiNotificationManager.RecordEventForEmpire` (:742-804)
  looks up `gameEvent.GetType()` EXACTLY (no base-class fallback) in the private ~162-entry
  `guiNotificationTypeByEventType`, `Activator.CreateInstance`s the notification, `Bind`s it
  (`Bind` returning false vetoes creation), inserts by ascending `Priority` (game types use
  only -2/-1/0), and fires the public `PlayerEmpireNotificationsCollectionChanged`;
  `EmpireEvent`s route to the event's empire only (:847-855). The mod's entries
  (`ModNotifications`) are reflected into that dictionary and re-asserted per frame (the
  manager is per-game), and its types override `SkipSerialization => true` — the save writer
  honors `ISerializationFilter` per list element (`BinarySerializer` :643-664), so mod
  notifications never enter a save. On read an unresolvable type name is caught, skipped by
  its length prefix, and left as a NULL list entry (:326-340) that downstream game code would
  NPE on — the opt-out, not the graceful read, is the real safety.
- **`EventSystemBesieged`/`EventSystemBlockaded` discard their aggressor**: `base(empire,
  instigator)` binds the two-arg `EmpireEvent(Empire, params object[])`, so `Instigator` is
  the VICTIM and the aggressor lands in the unused params array; the real aggressor survives
  on `PopulationEventRaisingContext.Instigator`. Check which ctor a `base(...)` binds before
  trusting a field name.
- **The obliterator events say less than their names**: `EventObliteratorFired` carries the
  FIRING fleet's node and routes to the firer only (`FireObliteratorFleetAction` :86);
  `EventObliteratorFireObserved` is the observer's copy; and
  `EventObliteratorProjectileImpactOnStarSystem` fires at impact to the same empires as
  `EventObliteratorVictimReport` two lines earlier (`MoveToObliteratorProjectileAction`
  :160) — mapping it double-notifies the victim.
- **`EventEmpireSeen` is two sightings in one type** — a foreign FLEET rising to Visible
  (`Fleet.cs` :1216, raised from the fleet's own `Visibility_OnLayerChanged`, routed to the
  OBSERVER) and a foreign COLONIZED SYSTEM (`ColonizedStarSystem` :4822) — told apart by the
  entity. The five-step `Layer` ladder re-raises it on Visible→Exposed, so a consumer needs
  its own dedupe. Loading a save raises NO sighting burst. **Mod policy** (owner ruling
  2026-09-02, `EmpireSightedNotification`): only the COLONY half is answered from this event. A
  colony does not move, so the event fires when the player really has discovered one; a FLEET's
  copy fires on the server's every recomputation and routinely announces a ship that was never
  drawn (`galaxy-map.md`, "Galaxy labels and what an empire may know"). Fleet sightings are the
  watch's (below).
- **A foreign fleet is only news once the map has HELD it, and the mod's own settle window is what
  decides that** (owner ruling 2026-09-02, `Core/UI/SettledSight`, driven by `ForeignFleetWatch`).
  Every crossing of the sight boundary — in or out, seen at the `EntityVisibility.SetLayer` write —
  becomes a candidate with a timestamp, and commits only after **2 seconds** with no crossing back;
  a reverse crossing inside the window cancels it silently, so a flash into sight is no sighting and
  a flicker out of it is no loss. One rule covers a same-frame Visible+Known pair in an applied
  batch, a one-second pass through detection range, and a Known→Visible round trip; the window is
  longer than the server's 0.5 s batching cadence on purpose, because the question is what a player
  could have READ off the map rather than what the wire carried. A committed rise raises
  `EventModForeignFleetSighted` (`ForeignFleetSightedNotification`), a committed fall the existing
  `EventModForeignFleetLost` — and a loss can only be committed for a fleet that was committed in
  sight or was in the baseline, so the mod can never report losing something it never reported
  seeing. Measured before/after on the same one-eval Visible+Known pair: the old path produced a
  sighting line plus two lost-sight lines and spoke one of them; the new path produces nothing at
  all, `settling` returning to 0 in the same frame.
- **A sighting line is FROZEN at the moment it was earned** (`ForeignFleetSightedNotification`).
  Owner standing, fleet name, the composition phrase (`FleetPhrase.Full(fleet, false)`) and the
  place all travel on the event, read at the commit; neither the log line nor the popup body ever
  re-reads the fleet. What the player was allowed to count aboard a ship at the moment they saw it
  is what the line says forever — a line that re-read the fleet would quietly rewrite itself as the
  fleet grew, moved or vanished. Verified live: title and body identical either side of the fleet
  dropping to `Known`.
- **The turn's moved-fleet diff is armed at the boundary and RUN later** (`ForeignFleetWatch`).
  `GameClientState_Turn_Begin` is too early to diff on — the turn's visibility operations are still
  held server-side and the fleets are still animating — so the sweep waits for the client to reach
  `GameClientState_Turn_Main` AND for 2 seconds in which no watched fleet crosses the sight boundary
  or changes `GalaxyPosition`, capped at 15 seconds after Turn_Main so a galaxy that never settles
  still gets its turn. Measured live 2026-09-02 with a per-frame recorder: the sweep armed at
  Turn_Begin, the batch landed 1.2 s later, a crossing flashed and cancelled inside it, and the
  sweep ran at **+3.8 s / 146 frames** — where the old code diffed in the Turn_Begin frame itself,
  against layers that were still last turn's.
- **The turn number the player reads is `Game.Turn + 1`**; `FleetRoute.DisplayedTurn()` is
  the one shared answer — never a fresh copy of the sum.
- **Whether an arrival opens a popup is decided AFTER the arrival event, in the same call — so it can
  be asked rather than re-derived.** `RecordEventForEmpire` fires
  `PlayerEmpireNotificationsCollectionChanged(Add)` at :790-792 and only then, at :800-803, asks five
  questions before `ToggleGuiNotification`: a non-scan mapping, the player's empire,
  `CanShowNotifications`, `CurrentGuiNotification == null`, and `AutoPopUp || ForceAutoPopup`.
  `ShowGuiNotification` (:511-535) then refuses AGAIN when popping is paused and the notification
  `CanBeDelayed` — a condition invisible from the arrival alone, which is why a copy of the rule in
  the mod would drift. All of it is synchronous, so a handler that only RECORDS at the event and
  reads back from the pump gets the settled answer: `CurrentGuiNotification == n` (the popup that is
  up this instant) or `n.AlreadyRead` (written by `ShowGuiNotification` :532 and never unwritten, so
  a popup shown and closed inside one frame is still caught). **Mod policy**
  (`ModNotifications.Arrived`/`PopsUp`): every notification the player's empire is given is announced
  by its title on arrival — the game's own as well as the mod's — UNLESS a popup of its own is
  coming, which `NotificationScreen` reads out instead.
- **`CurrentGuiNotification` can STAND with no popup drawn, and it disables the game's keyboard
  zoom** (measured 2026-09-01). `GalaxyViewCameraController.CheckInputs` gates every keyboard zoom on
  `Gui.GuiNotificationService.CurrentGuiNotification == null`, so while the field is stale PageUp and
  PageDown do nothing at all on the galaxy map and the mod looks like the culprit. The session found
  it holding a `NotificationPopulationGained` that was in no empire's list and had no window shown
  (every `GuiWindow.Shown` enumerated; only the labels, the HUD and the tutorial were up), and
  `DismissGuiNotification` on it changed nothing — that call is about the LIST. What clears it is the
  manager's own property setter, which is `private set` and raises the same Refresh a real close
  does: `typeof(GuiNotificationManager).GetProperty("CurrentGuiNotification").GetSetMethod(true)
  .Invoke(manager, new object[] { null })`. With it null, a real `POST /key` PageDown moved the rung
  9 → 7 and spoke both band words. **Leave it null at the end of a session**: a stale one makes every
  physical-zoom claim untestable and looks like a mod defect.
- **A notification that queues behind an open popup is not passed over — the next Dismiss or Minimize
  opens it — so the popup question is about a notification's whole life, not about the frame it
  landed on.** `NotificationWindow.OnDismissCb` (:199-202) and `OnMinimizeCb` (:219-222, which Escape
  and right-click both reach, :93-97) pass `showNextUnread: true`, and `GetNextUnreadGuiNotification`
  (:494-509) answers the first unread notification with `AutoPopUp || ForceAutoPopup`. Measured
  2026-08-28: raising a second notification while a popup was up left it queued and silent from the
  game, and closing that popup opened it. **Mod policy** (owner ruling 2026-08-28): a notification
  that will pop its own window up must not be heard twice, however long the wait — so of the five
  conditions above, only the type's own `AutoPopUp || ForceAutoPopup` is asked, because it is the one
  that outlives the arriving frame; the other four are momentary and would answer "no popup" about a
  notification merely waiting its turn. The accepted cost, named in the same ruling: where popping is
  PAUSED and the notification `CanBeDelayed`, `ShowGuiNotification` refuses and nothing asks again,
  so that arrival is silent and its popup never comes.
- **The SCAN table's notifications never pop up, whatever their `AutoPopUp` setting says.** The
  hacking family is bound into a second dictionary (`BuildGameEventToScanNotificationMapping`
  :172-192) and inserted into a second list (:786-788), and both roads to a popup are shut to it: the
  auto-pop call at :800 is gated on `flag`, a mapping in the NON-scan table, and the queue drain
  reads only `GetPlayerEmpireGuiNotifications` (:496) — which is `guiNotificationsByEmpireIndex[player]`,
  measured 2026-08-28 by reflection to be the main list and NOT the scan one. They fire the same
  `Add` event as everything else, since :790-792 sits outside the flag/flag2 branch. **Mod policy**
  (owner ruling 2026-08-28, `ModNotifications.Standing`): membership of the player's own list is part
  of the popup prediction, so this family is announced on arrival like any other news that draws no
  window. Without it the prediction would have trusted a per-type setting that reads true by default
  and silenced the family outright — news with no popup and no announcement. Code-traced plus the
  list-identity measurement; the family has never been heard live.
- **`CollectionChangeAction.Refresh` is two events under one name** — a stackable notification rebound
  to a newer event (:762-772), whose `Add` was already announced, and the `CurrentGuiNotification`
  setter reporting that some popup just went up or down (:41-48). Mod policy: `Refresh` is never
  spoken; one would repeat the news and the other would read the title of every popup the player
  opens.

## What a notification popup draws

- **A notification popup that draws its own content leaves the shared description UNWRITTEN, and
  the game hides the label rather than clearing it.** `NotificationWindow.Refresh` (:247-251) writes
  `NotificationDescription.Text = GuiNotification.GetDescription()` **only while that label is
  visible**, and a window like `TechnologyUnlockedNotificationWindow` parks the label under a hidden
  `Dummy` and draws cards of its own instead. `NotificationTechnologyUnlocked` overrides only
  `GetTitle()` (:51-55), so both the parked label and `GetDescription()` answer
  `%NotificationTechnologyUnlockedDescription`, which localizes to the template with its hole still
  in it: "Research has been completed: {0}". A second form of the same thing:
  `%NotificationTechnologyStageUnlockedDescription` has NO translation at all, so `Gui.Localize`
  hands back the key (measured). **Mod policy** (`NotificationScreen.Text`): a description whose
  label the player cannot see, or whose text is still a brace-digit template or a raw `%key`, is
  ABSENT — never spoken, never a words node, never buffer content — and such a popup is read off
  what it DRAWS instead. Titles are exempt: every measured popup formats its title properly, and the
  one unfilled title seen (`"Research Complete: {0}"`, mid-browse) was a stale frame, which is why
  the change watcher waits for `IsReady`.
- **The tech popup's "next research" label sits at its prefab placeholder whenever the queue is
  empty — inside a group the game never shows in that state.** `NextTitle` (under
  `NextTechnologyTitle`) holds the skeleton's "Technology Name" at alpha 0 while
  `NextTechnologyGroup` is `Visible = false`; `TechnologyUnlockedNotificationWindow.Refresh`
  (:133-136) rebinds the text via `RefreshTechnologyImageAndTitle` (:182) BEFORE it makes the
  group visible, so no state exists where the placeholder is inside a visible group (measured
  2026-09-02, unshown window, empty research queue). A reader gated on the group's visibility —
  `EmpireDossier.Read`'s `child.Visible` descent, `DrawnRows`' painted test — never meets it.
- **Four notification windows wire the shared description to a label they left OUT of their
  layout, and two of those still carry the skeleton's key on it.** Measured over every
  `NotificationWindow` instance in the scene (`Resources.FindObjectsOfTypeAll`): 65 of 69 hold
  `NotificationDescription` somewhere under the window's own `AgeTransform`; `DeedCompleted`,
  `QuestCompleted`, `MetaplotBegun` and `MetaplotFinished` do not — `AgeTransform.Parent == null`,
  the GameObject parented to a `Dummy` outside the window, the rect a degenerate `(2,15,45,20)`
  in the corner of the screen. `Refresh`'s visibility gate never fires on them, so
  `DeedCompletedNotificationWindow`'s label still holds the prefab's
  `%NotificationDeedCompletedDescription`, which localizes to **"You have achieved this legendary
  Deed!"** — spoken by the mod on the popup announcing that another empire got there FIRST, because
  the label passes every other test: marked visible, no hidden ancestor (it has no ancestor), and
  a whole sentence rather than a template. `GetDescription()` answers the same key, so the
  notification-side fallback repeats the lie. What the popup actually draws is its
  `QuestDescriptionGroup` (StatusTitle / ObjectiveTitle / Outcome / ObjectiveLore) and nothing
  else — measured against `/gui/age`, there is no success-or-failure sentence anywhere outside it.
  **Mod policy** (`NotificationScreen.DescriptionLabel`/`Held`): a description label the window's
  own tree does not hold is no words label at all, and the question stops there — no fallback to
  `GetDescription()`, because a popup with nowhere to draw a description never showed that
  sentence under any circumstances.
- **41 of the 69 notification prefabs bind a Show Location button their own layout never holds,
  and the game marks it VISIBLE anyway.** `ShowLocationButton` is `[GuiBound]`, so every window
  resolves one; `NotificationWindow.OnBeginShow` (:139-140) then sets its `Visible`/`Enable` from
  `GuiNotification.HasLocation` without asking whether the prefab laid it out. Measured over all
  69 instances: 28 hold it in their bottom button bar (rect ~`(643,575,120,44)`), the other 41
  answer with an orphan — `AgeTransform.Parent == null`, `IsRootTransform` false, rect
  `(0,0,50,50)` — which the engine never draws because rendering walks the tree. Same shape as the
  detached description label above, on a CONTROL: visible, alpha 1, enabled, drawn nowhere.
  **Mod policy** (`NotificationScreen.Painted`): a paint test that walks up the chain must end AT
  the popup's root, not merely run out of parents — an orphan passes every step of the walk and
  then has no root, and the reward for the lenient version was a "Show Location" stop on
  Outpost→Colony and Planet Destroyed that did nothing. `Controls` filters the whole list,
  rails included, through that test.
- **Every notification popup declares its two strips as CONTAINERS, and the popup's own buttons
  join the bottom one.** Measured over all 69 `NotificationWindow` instances in the scene: the
  browsing arrows sit in a `NavigationGroup` and the pop-up-again box in an `AutoPopupGroup`, both
  of them children of `TitleGroup`; `Dismiss`/`Minimize`/`ShowLocation` sit in a `ButtonsGroup`
  (66 of 69 — the other three park a spare `DismissButton` in a `Dummy` beside the description, and
  their `Minimize` is still in `ButtonsGroup`). Of the 98 wired controls the popups add on top of
  that skeleton, **50 are inside `ButtonsGroup`** (`AcceptButton`, `ValidateButton`, `ReplayButton`,
  `AcademyButton`, the quest popup's `PinToggle`, …) and **none is anywhere inside `TitleGroup`**;
  the other 48 sit in content containers of their own (`PoliticalSupportLinesTable`,
  `CompletedTechnologyGroup`, `ChoicesTable`, `ContentGroup`, …). **Mod policy**
  (`NotificationScreen.Sort`): a control is classified by identity plus containment — a base-class
  rail goes to its strip by name whatever the popup did with it, a control the game drew inside
  `TitleGroup` or the button bar goes to that strip, and everything else is body content walked in
  the row it is drawn in. Rectangles cannot answer it: banding against the description swept an
  election survey's four party lines into the top strip (the description is drawn BELOW the chart
  there), and banding against the rails themselves is the same guess with a different ruler.
- **A notification popup folds a detail panel away by FADING it, and fades ITSELF in on arrival —
  two alpha animations that a single painted-ness rule cannot both serve.** The six report popups
  (`DamageReportNotificationWindow` and its three subclasses, `DisplacementReport`,
  `PirateMissionReport`, plus `ForceTruceProposed`'s two breakdown groups) collapse their detail
  panel with `ReportPanel.StartAllModifiers(forward: false)`: measured on
  `IonWaveReportNotificationWindow`, `ReportPanel` sits at `Visible true, Alpha 0` with all five of
  its children at alpha 1 and every ancestor at alpha 1, so a `Visible`-only read speaks a whole
  "Damage Report" the screen draws nothing of. The popup's ARRIVAL animates the window's own
  transform 0 → 1 while every child stays at alpha 1 (measured: a `POST /wait` on
  `Shown && Alpha <= 0` fires, one on `Shown && IsReady && !Painted(root)` never does over 154
  frames). So the gate is the ENGINE's own child test — `child.Visible && child.Alpha > 0`
  (`AgeWidgets.Paints`, asked per child by `AgeWidgets.DrawnChild`) — applied descending from the
  window root and never to the root itself. `StrictVisibility` is NOT an exemption in it: that flag
  tells the ARRANGER to keep counting a faded child's slot, and the renderer skips the child all the
  same. `NotificationWindow.IsReady` is
  already past the fade, so the arrival announcement is not at risk either way.
- **The report family's breakdown toggle is REAL** (supersedes an earlier "vestigial" reading, which
  was an ask-the-wrong-component error). `ReportPanel.GetComponents<AgeModifier>()` really does answer
  zero, which is what the reversed claim rested on; the animation lives in the transform's modifier
  SET (`AgeFirstModifierSet`), which is what `StartAllModifiers` drives. Measured live 2026-08-15:
  collapsed, `ReportPanel` is `Alpha 0` and a crop of its rectangle is empty sky; activating the
  toggle draws "DAMAGE REPORT" and the ships line in the same rectangle. Never conclude "animates
  nothing" from an absent `AgeModifier` component — ask the panel's ALPHA in both states, and pair
  it with a crop.
- **`GuiWindow.IsReady` is not "painted".** The screen's settled-popup seam fires two READY frames
  after a popup's words settle, and on those frames a popup can still be drawing not one string:
  a `POST /wait` on `DevProbe.NotificationParity().Contains("\"texts\":0")` fires on a live raise,
  and the parity check a moment later is clean. No settled popup of the sixty-four paints fewer
  than four strings, so "paints nothing" is an early frame rather than a finding, and the auto-check
  defers on it (bounded, with a give-up line).
- **`NotificationItem.Bind` sets the icon tooltip's content to `GetTitle()`** — a tooltip
  section on any notification row is always the row's own title again.
- **The Laws Cancelled prefab hangs TWO tooltips per line** — the real `Law` dossier on
  `CancelledLawLine000` itself and a completely empty one (no class, no content, no target)
  on `LawDetails/Icon` — and wraps the whole line in `LawDetails`, a group spanning both
  captioned columns. Empty decoration tooltips on prefab icons likely generalize; the
  never-drawable filter (`AgeWidgets.NeverDraws`) exists because of this window.
- **The construction-completed popup draws a real table**, and its rectangles pair by themselves:
  the caption band (`CollumnNames`: "System" 264–504, "Completed" 502–822, "Next Construction"
  825–1145) x-aligns with each line's three cell groups (`StarSystemInfo` 264–504,
  `CompletedConstructionInfo` 502–812, `NextConstructionInfo` 825–1145), so columns can be paired by
  x-overlap with no per-window knowledge. The line (`CompletedConstructionLine000`) is a wired
  `AgeControlButton` under `ContentSW`'s scroll view, carries an EMPTY `AgeTooltip` of its own —
  which is why the pre-table reading fused all four labels into one row — and hangs the two
  `Constructible` dossiers off the `Icon` inside each figure cell, never on the cell or the line.
  A system with an empty queue hides `NextConstructionInfo` and shows `NoNextConstructionButton`
  (a group, no button component) in the same column instead.
- **The elimination popup's groups hold no text**, and it hides Dismiss and Minimize — so its
  sentence has to ride something else (the mod puts it on the screen name).

## The close-all control and dismissing

- **The game's close-all notification control carries no words at all.** The strip's
  `BaseTriangleBackground` (an `AgeControlButton` under `NotificationItemsWindow`, found by that
  prefab name — the window binds no field for it) has NO `AgeTooltip` component, no caption and no
  localization key anywhere in the corpus (`%*CloseAll*`/`%*DismissAll*`/`%*ClearAll*` all miss).
  Its only wiring is `OnRightClickMethod=OnCloseAllCb`, and that handler branches on a PHYSICALLY
  held modifier (`NotificationItemsWindow` :237-245): Alt =
  `DismissAllGuiNotifications()` (throws every one of the player empire's notifications away,
  newest-first, through the same `DismissGuiNotification` a single right click uses), Shift =
  `HideAllGuiNotifications()` (only closes the popups that are currently VISIBLE; nothing is
  dismissed). Mod policy: the dismissing branch is a named button of the mod's own
  (`hud.dismiss-all-notifications`); the hiding branch is not offered.
- **`DismissAllGuiNotifications` does not distinguish the mod's notifications from the game's** —
  `GetPlayerEmpireGuiNotifications()` is one list, and mod notifications live in it
  (`ModNotifications`), so the game's own close-all empties the Turn log as well as the icon strip
  in one press. Mod policy (owner ruling 2026-08-24): the mod's "Dismiss all
  notifications" therefore does NOT make that call — it dismisses the game's own notifications one at
  a time through `DismissGuiNotification`, skipping every notification the mod raised, so each of the
  two dismiss-all buttons empties only its own list.

## Show location

- **"Show location" is a PAN and then a TOGGLE, and the toggle is what a keyboard replay must leave
  out.** `NotificationWindow.OnShowLocationCb` (:204-209) is `GuiNotification.ShowLocation()` followed
  by `GuiNotificationManager.ToggleGuiNotification` (:386-406), which HIDES a popup that is showing
  and OPENS one that is not. So pressing the button on an open popup puts it aside, and replaying the
  handler from a CLOSED strip row would open the popup instead of going anywhere. **Mod policy**
  (`NotificationScreen.GoToLocation`): the popup's own key presses the drawn button (toggle included,
  as the mouse does); a strip or turn-log row replays everything the handler does except the toggle.
  Five window families override the callback and each is answered from the NOTIFICATION, since the
  window is shared and bound to whichever notification is up: `QuestBegunNotificationWindow`
  (:205-209) asks `ShowQuestLocation(quest, currentStep)` — and `NotificationQuestBegun.HasLocation`
  is true while it overrides NO `ShowLocation`, so the default route moves nothing at all for it;
  `BattleCommonNotificationWindow` (:79-87) and `BattleSetupNotificationWindow` (:270-278, its button
  disabled while the player is ready, :371) aim at `encounter.Orbit.GalaxyPosition`;
  `GroundBattleNotificationWindow` (:153-163) at `GroundBattle.DefenderNode`; and
  `DefenseHackingProgramEncounteredNotificationWindow` (:27-33) does the ordinary thing and then
  `ToggleScanView()`.
- **Show Location on a foreign-fleet line goes to the last place the fleet was SEEN, never to blank
  sky** (owner ruling 2026-09-02). The sighting line offers the fleet itself only while the player's
  client is still drawing it (`ForeignFleetWatch.Drawn`, the layer of the moment rather than the
  settled answer); once it is not, the button flies to the last node the watch observed the fleet
  standing at while in sight (`ForeignFleetWatch.LastSeen`, refreshed at the commit, at each settled
  sweep and at each quiet-window poll, and never re-read after the fleet goes dark). A fleet only
  ever seen out on a starlane has no such node, and a null location hides the button
  (`NotificationWindow.OnBeginShow` :138-140) rather than offering a pan to nowhere. The moved line
  follows the same rule with its own destination node first; the lost line already did. Measured:
  under the old code the sighting's location was the FLEET while its layer read `Known` — Ctrl+L
  panned to a ship the player could not see; now it answers `StarSystemNode Graffias`, and a
  mid-lane-only sighting answers null and `ui.goToLocation` comes back `unconsumed` with the camera
  unmoved.
- **A notification names its own window in its constructor.** Every `GuiNotification` subclass sets
  `base.NotificationWindow = Gui.GuiService.GetWindow<...>()`, so "would this notification's popup
  draw a show-location button" is a field read away and needs no hand-maintained table of the 28 that
  do. **Mod policy** (`NotificationScreen.DrawsShowLocation`): the test is a LAYOUT walk to the
  window's root, not the painted walk — a closed popup draws nothing at all, so asking the painted
  question of one answers false for every row on the strip, which is exactly where the key lives
  (measured 2026-08-22: the hint and the key vanished from every strip row until the test was split).
  The orphan is still what has to be caught, and an orphan has no parent at all.

## Quests, the journal and the tutorial

- **The pinned quest panel needs TWO existence answers, and neither is the other.**
  `GuiManager.UpdateGameWindowsVisibility` (:1542) hides `PinnedQuestWindow` outright behind any
  GuiScreen (`isInNormalView && !IsAnyScreenVisible`), while `PinnedQuestWindow.UpdateDisplayedQuest`
  (:60-80) additionally needs an `ActiveQuest`; and `PinnedQuestPanel.OnBeginHide` nulls
  `PinnedQuest` at the START of its fade, which is what makes `window.Shown && panel.PinnedQuest !=
  null` the gate rather than a visibility flag mid-animation. The panel's three click targets are the
  panel itself (`OnClickQuestCb` → journal), `ShowLocationButton` (`OnShowLocationCb`) and
  `UnpinButton` (whose handler lives on the WINDOW, `PinnedQuestWindow.OnUnpinQuestCb`).
- **`AgeWidgets.Operable` answers enablement only — pair it with `Visible` before OFFERING an
  action.** `GuiQuest.SetupMarker` (:419-428) hides the quest's marker button by writing
  `AgeTransform.Visible` and never touches `Enable`, so an `Operable`-only gate put a "Show location"
  entry into the pinned quest's menu for a quest the game draws no marker for (measured: menu read
  "1 of 3" with `hasMarkers=False`). Every menu builder in this repo asks both — `AddOrbitalAction`
  already did.
- **On the quest strip, `Alpha` and `Visible` answer nothing — the BINDING does.**
  `NarrativeScreen.ShowFilteredQuests` (:248-251) unbinds every `QuestCard` before refilling the
  table, so `card.GuiQuest == null` is the game's own "this slot is a leftover". The pooled-slot
  Alpha==0 rule is actively WRONG here: `UpdateCardsAlpha` (:190-220) rewrites the alpha of every
  child each frame to between 0.25 and 1 — the fade over the cards near either edge of the viewport —
  so a faded card is a real card and no card ever reads 0.
- **`GraphNavigator.Alternate` speaks the node's `StateText`.** A node built by a factory that
  supplies one (radio, checkbox, slider) answers the alternate-click chord with the word for the
  PRIMARY action: the quest card's alternate click pinned a quest and said "selected". A node with two
  activations has to own its `StateText` and answer for whichever one just ran — which is a wrapper the
  journal's card carried until the pin became a node of its own. **The real lesson is the one above
  it**: the card had no modified click in the game at all, so the second activation was invented, and
  the honest fix was to declare the control the card DRAWS (`QuestCard.PinToggle`) as a child node and
  leave the chord unwired. The game names that action, though never on the toggle:
  `%NotificationQuestBegunPinTitle` ("Pin Quest"), the button the quest-begun notification offers for
  the same thing, with `%NarrativeScreenActiveQuestPinDescription` as the toggle's own tooltip.
- **The journal's Details column has no text of its own**: its two buttons carry their only
  words on their own tooltips (`%VictoryScreenScoreScreenButtonDescription`,
  `%JournalModalWindowDeleteEntryDescription`), one line each.
- **A collapsed tutorial is a HUD stop, not a tutorial screen.** The game crops the popup
  to its title bar and hides nothing, so `MinimizeToggle.State` is the only signal;
  `TutorialScreen` stands down while it is set and `BuildCollapsedBar` declares the leftover
  bar in `GlobalHud`'s `hud:tutorial` stop, on whichever view level is underneath. The game
  does NOT draw the bar on the planet overview, so that page has no such stop — measured, not
  assumed.
- **A tutorial page's popup layer decides whether a MINIMIZED popup's bar survives a covering
  window**: `UnderScreens`/`FleetsScreen` hide the whole panel; `AboveScreens`/
  `AboveNotifications`/`AboveModalWindows` keep it drawn and clickable (minimizing only crops
  the panel). Counted in `Public\Tutorials\*.xml`: 117/10 vs 41/16/49. Closing a modal a
  tutorial was drawn over makes the popup re-announce its page — the panel is briefly
  un-minimized during the hide.
- **`QuestJournal.ActiveTutorial` picks the in-progress tutorial quest by popup layer then
  priority, and `TutorialWindow.Update` re-derives it every frame** — a hand-`Bind` of a different
  tutorial is overwritten next frame; a tutorial is raised only by making its quest win that
  comparison.

## Endings

- **`EndGameSummary` is written at popup-SHOW time**, which is what makes the journal's ending
  entries readable at all. **Its CONSTRUCTOR saves itself**: `new EndGameSummary(game)` writes
  the `.bin` and adds the journal row unless `Game.EndGameSummaryAlreadySaved` or
  `EnableModdingTools` (`EndGameSummary.cs:145-151`). Constructing one and then calling
  `SaveEndGameSummary` registers the SAME instance twice — two rows sharing one row object
  throw `Duplicate control id` and empty the journal.

## Cutscene videos

- **`CutsceneModalWindow.PlayTime` IS the subtitle clock** — a private property its `PlayMovie`
  coroutine advances by `Time.deltaTime`, and the very field it compares each `<Start>`/`<End>`
  against (:245-280). Anything timed INTO the gaps between spoken lines must read it rather than
  the media player's own position: under dropped frames PlayTime falls behind the picture, and the
  subtitles fall behind with it, so a cue timed off anything else drifts into dialogue PlayTime is
  still holding back. It is zeroed just before `OnPlayStarted()` and never reset by `UnloadVideo`,
  so the value read between two videos is the PREVIOUS one's — arm off `OnPlayStarted`, not off
  `ShowWindow`.
- **A cutscene's `subtitlesSpecifier` is never stored** — `ShowWindow` passes it straight into
  `Path.ChangeExtension(moviePath, specifier)` inside `InitializeSubtitles` and keeps nothing.
  Values are `Quest.MetaplotState.ToString()`, so `LostBack` or `LostNotBack`, with `Unfinished`
  folded into `LostNotBack` at the call site (`VictoryScreen.cs:284`). It is also only READ when
  the game's own `DisplaySubtitles` is on, so the loaded subtitle array cannot stand in for it: a
  mod that needs to know which ending is playing must patch `ShowWindow`.
- **Four call sites play a cutscene**, all building an absolute path under
  `Application.streamingAssetsPath`, so the movie's BASENAME is a stable key:
  `GameClientState_Introduction:136` (faction intro, gated on `EnableFactionIntroductionVideos`),
  `DepartmentOfTheInterior:1609` (colonization, on `ColonizationCutsceneModalWindow`),
  `QuestCompletedNotificationWindow:172` (metaplot, on quest completion) and `VictoryScreen`
  :262/:292 (the metaplot-victory video, then the faction outro — the only one passing a
  specifier).
- **Movie filenames use affinity codenames, not the names a player sees.** Templars is Nakalim,
  Terrans is United Empire, Timelords is Riftborn, Vampirilis is Vodyani, Venetians is Lumeris,
  Hisshos is Hissho. Planet types rename four: Swamp is Toxic, Tropical is Mediterranean, Vedt is
  Savannah, Steppe is Steppes, and the gas giants are one word (`GasBurning`). `Terrans_Intro` is
  the ONLY intro for United Empire, Mezari and Sheredyn, while their outros are three distinct
  files (`Terrans_Outro_UE`, `_Mezari`, `_Sheredyn`) — 12 intros and 14 outros for 26 faction
  videos in all.
- **The metaplot has three narrated videos of its own.** `Metaplot_LostBack` (31.2 s) and
  `Metaplot_LostNotBack` (32.0 s) play when the player OPENS the quest-completed notification
  for the final metaplot chapter (`QuestCompletedNotificationWindow.OnBeginShow` →
  `ShowMetaplotMovieIFN`), and the "owes a video" flag is serialized into the save, so it waits
  as long as that notification goes unopened. `Metaplot_LostBackVictory` (40.0 s) plays on the
  victory screen BEFORE the faction outro, and only when the Lost returned AND the player's own
  empire was on the winning team. The branch is a chapter-1 team choice: Rejuvenators restore
  the quest systems (`MetaplotLostBack`), Defenders destroy them (`MetaplotLostNotBack`), and
  the tag goes to EVERY player, so both teams watch the same video. None of the three takes a
  subtitles specifier — each outcome is a separate file, not a second track on one.
