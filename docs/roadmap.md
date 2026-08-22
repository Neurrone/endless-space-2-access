# Roadmap — work remaining

Only work still to be done lives here (owner rule). What a fixture cannot show and how to
test a shipped screen: `docs/test-recipes.md`. Mechanisms: `docs/es2-facts.md`. Layers and
keys: `docs/interaction.md`. The shipped index at the bottom is pointers only — a row may
name a screen and its mod file, nothing else; notes about a shipped screen belong in the
files above.

## To build

- **"Tooltips" children (landed 2026-08-22, batch 2) — remaining live checks only**: the
  hero detailed card's four-symbol row (Academy is tutorial-gated on both fixtures), the
  construction line's festival badge and the honor gauge's own dossier (both Hissho). Still
  to decide/do: a system node declares no `NodeVtable.PointsAt`, so the tooltip audit files
  every map node under `unknown` rather than judging its aim; the population side panel's
  party dossiers land as the ROW BELOW their population rather than as children, because a
  side-panel row is a cell and a cell cannot open a subtree; and the population OVERVIEW
  screen's rows have not been given the same treatment (batch 4 owns that screen).
- **One-per-row rollout (landed 2026-08-18) — remaining live checks only**: battle
  popups/screens (all code-only; incl. whether the battle popup speaks its title twice);
  the election wizard incl. the Political Trends label; a hangar with ships; a populated
  Active Events panel; multi-slot recipe projects; the strategics grid; diplomacy side
  panels/metaplot and the three diplomacy modals; the victory family; DLC modals;
  join-game rows; a mod-manager library with a
  mod installed. The marketplace half of the economy page is still 2D — deferred until
  measured.
- Colony info side panel (declared 2026-08-19, forced-show verified): remaining is the
  live sighting of each conditional state — siege/blockade/invasion/conversion/frozen,
  partial ownership, an exploiting system's resources, wrecked arks, temporary effects,
  a Vodyani ark (name + Detach), a ghost system's Decolonize, a Hissho citadel. Only the
  home-system badge has been heard on real data.
- Battle tactics deck influence stock (declared 2026-08-19): the costed state was forced;
  the natural post-change cost and the in-battle drawing ride the battle live-check line
  above.
- Regression walk owed: the research/construction table popups (the other sheet-reading
  family) after the wrapper-descent change to `Columns` — additive-by-construction argument
  only so far; walk one the next time a session has one pending.
- Notification variants awaiting a live sighting (baseline ships; upgrade per popup on
  sighting): election survey; ground-battle outcome selection; SimpleDescription-family
  members with own fields (alliance update, diplomatic relation change, constellation
  event, deed completed); the 9 header-less line-class tables (bailiff + its totals
  footer, law cancelled, population change, trading blockade, treaty cancelled, relics ×2,
  queue-empty, lost-roots connectivity); one-of-N semantics for hand-written choice popups
  (hero recruitment, ground-battle/hacking outcome pickers) + the hacking parameters
  sub-choice and the outcome COUNTDOWN gauge (real-time seconds, auto-picking a default when it
  runs out — es2-facts; needs a `Variant` hook plus a live sighting);
  PirateMissionReportNotificationWindow (fixture-blocked: its `Bind` needs a live
  `AttackSystemPirateDiplomaticAction` — the other five report popups are done);
  DiplomaticInteractionNotificationWindow (MoodMessageLabel, NegotiationContributionPanel).
- Galaxy-label gaps: constellation ownership bonus; pin-message editing.
- Input batch (code landed 2026-08-22, LIVE-VERIFIED 2026-08-22 on `[Beginner] test` — recipes in
  `test-recipes.md` "The input batch"): single-press tree arrows, the six place keys incl.
  Ctrl+Alt+E, Alt+Left/Right paging on four screens, the star-system pair as nodes, chord labels,
  the election winner rows. Verified by injection: the arrows one press each way on the galaxy
  tree and the research wheel (`OnFollow` lane travel unchanged), all six place keys (landing
  identical to Tab's, silent and cursor-unmoved where the stop is absent), paging on the system
  page (Dusay ↔ Heka), the planet page (wraps) and a notification popup (silent, pair switched
  off), every chord label off the game's own `%KeyCode*` names, and `ui.endTurn` (turn 21 → 22,
  itself silent, the game's idle-system prompt on the first press). Still open: the CHORD half is
  UNPROVEN — `POST /key` answers 409 while the desktop is locked, and `DevProbe.Chord` cannot
  answer it (its `Claimed` is asked per KeyCode, and type-ahead claims every letter on every mod
  screen), so "Ctrl+G reaches the mod, not the game" is a manual-test line; whether a childless
  expandable group occurs in play at all (no fixture system has one — even the SPECIAL node has
  lane children, so the empty-group branch is still unit-test-only); whether the encounter cameras
  need to join `GameKeyStandDown` for Ctrl+Alt+E's `E` (no battle in the fixture); and the
  ELECTION winner rows (`screen.election` is inactive in the fixture). The Academy's own strip
  arrows ARE now declared as nodes (owner decision, landed 2026-08-22) — structure verified on a
  forced show, behaviour fixture-blocked (the save has no heroes, so both arrows read unavailable).
- Scanner (shipped, taxonomy v2 2026-08-16, Contested Influence added 2026-08-21, taxonomy v3
  2026-08-22): THIRTEEN categories in the owner's order — systems, colonizable planets, unexplored,
  anomalies, curiosities, luxury resources, strategic resources, contested influence, fleets,
  probes, ally pins, obliterator missiles, quest
  markers (system-anchored ones only, owner's ruling 2026-08-17: a marker with no system is not
  listed, since the go-to would have nowhere to land) — the "all"-only ones skipped while
  empty. Systems include SPECIAL nodes (the tree's 13 rows, not 12) and have seven
  subcategories: all / friendly / neutral / enemy / homeworld / minor factions / special, with
  many-to-many membership. The six v3 categories were LIVE-VERIFIED 2026-08-22 on `[Beginner] test`:
  colonizable (7 unoccupied / 1 occupied, both counts matching an `IsColonizable` oracle),
  unexplored (2 lanes, numbering identical to the tree's), anomalies (12 in "all" = 10 kinds),
  curiosities (16 = 5 kinds), luxury (10 = 2 kinds), strategic (5 = 3 kinds), all kinds
  alphabetical, memory by NAME proved across a category round trip, and Alt+Home landing on the
  planet's/lane's own node with the camera brought in (fleet, probe: node focus + camera slide;
  inspect cursor up: cell jump only). Cost: 32 ms on the session's first press, 5-8 ms after, 30
  colonizability checks (= 19 planet TYPES + 11 unsettled able worlds, against 33 planets).
  CONTESTED INFLUENCE has no rows on this fixture either (169 ground tiles swept, 0 contested), so
  what its live pass proved is the empty-skip, not the row wording.
  Remaining: FOREIGN homeworlds have never been heard (no fixture where
  `EmpirePosition.Known` is true — es2-facts), and quest markers, pins and missiles have never
  been heard at all (no fixture draws one). Open judgment call for the owner: MINOR-faction home
  systems are deliberately NOT in "homeworld" (the diplomacy lens the gate came from iterates
  major empires only) — including them would add ~9 fixture systems to that scope.
- Star-system page re-arrival (found 2026-08-22, PRE-EXISTING - the same on the game's own arrow
  button as on Alt+Left/Right): turning the page announces the screen TWICE ("Star system", the
  landing, again) and seats the cursor on the HUD's `hud:view-title/scan` rather than on the new
  system's own content. The planet page and the notification popup do neither. Worth one look at
  the arrival gate the recipe already warns about ("the page arrives in pieces").
- Ally pins and obliterator missiles are label-free too (2026-08-16, owner-ruled): enumerated from
  the simulation under the game's own knowledge gates, every word recomposed from the entity, and
  the pin's dismiss routed through the game's own two orders instead of its button. Nothing about
  the three open-space kinds moves with the camera any more. Still never heard — no fixture draws
  a pin or a missile. Open: whether a reader should obey the player's global
  `ShowRequestToggle` ("draw the pins") switch; it is currently not obeyed.
- Mining probes (shipped 2026-08-16): the planet rows on the galaxy map and the empire screen say
  the sentence the game keeps in the planet dossier, with the game's own gates. Fixture-blocked
  live: no save has a mining probe.
- Inspect mode (shipped): the drawn cursor is a MOD-DRAWN screen-space square (2026-08-17,
  `InspectMarker` — the borrowed line/circle/quad renderers are all retired and the saga is closed
  in es2-facts). Colour, alpha and thickness are now the mod's to choose, and a ~26 px floor keeps a
  1×1 cell visible at full overview. Remaining: the mode's camera does not zoom out with the cursor,
  so a size-11 cell close in is still wider than the viewport (the square's off-screen edges simply
  clip). Its cell reading carries obliterator missiles and ally pins, which remain
  fixture-unreachable and have never been heard.
- Scan management-lens remainder (unverifiable at turn 1): the trade-quality dial
  (geometry-only rating, no tooltip), the empire-rank bar graph + global-rank histogram,
  the ghost/traitor lines. The governor panel is MODELLED (batch D) but has only ever been
  sighted against a hand-bound hero — a save with a real assigned governor would confirm the
  FIDSI half, which reads "None" for every hero that adds nothing.
- Planet lens: the remains table is modelled (batch D) and no fixture has a planet with
  `Remains` — its lines have only been read against a hand-written item.
- Targeting-cursor remainder (Escape-on-TakeSystem and armed-Backslash SHIPPED, 30d23f9):
  `HonorActionCursor` fleet/docking targets need a `ConfirmAt` overload for fleet
  cursor-targets; `HackingOperationCursor` route building rides the Penumbra wait; the
  instruction banner can speak the previous mode's caption for a frame (cheap fix: skip
  an instruction that is not the current cursor's).
- Targeting-cancel fleet swap (measured 2026-08-20, owner-ruled leave-at-parity): a
  cancel at a multi-fleet system hands the panel to the slot's first fleet, not the
  actor. Fix only if it becomes a problem — the issue, mechanism and preferred fix
  (Harmony postfix) are in `docs/fleet-selection-cancel-swap.md`.
- Chat: the child screen ships whole (tabs, message log, box, the page-level new-message
  button) and is verified in a single-player fixture; what no fixture here has shown is the
  MULTIPLAYER half — the alliance tab, the new-message button actually being raised (in
  single player a Global line on the Global tab never raises it, so its page-level stop is
  unverified live), whispers and alliance sending.
- Assigned-governor side panel: `Special` case for its three bare readouts (needs a save
  with a governor).
- The planet page's population-entry click has no opener node yet (the window itself,
  StarSystemPopulationModalWindow, is covered — SystemPoliticsScreen binds it).
- Skill-tree type-ahead: a TypeAheadScope so search reaches skills in collapsed branches.
- Galaxy-map audit remainder (2026-08-20): `StarSystemLabel`'s 32 public widget fields are
  now fully covered (bars wording awaits owner approval; exploration-winner and shared-system
  readings are fixture-blocked to mid-game saves). Still open from the audit: the Riftborn
  time bubble (needs a Riftborn fixture) and the latent hacking beacon (deferred with
  Penumbra).
- Hero skill page (2026-08-20): the base-class `PanelFeatureEffectsSets` tooltip variants
  (hero, planet colonization, honor actions, hacking programs) read through the new
  `effect-sets` typed reader but are UNSIGHTED — one `DevProbe.Tooltip()` on any of them
  should answer `"effect-sets"` with no "Level" line. The relics box/region and a
  two-mastery starting skill also await a fixture (Nakalim/Templar hero).
- Hero skill page: the heading now speaks twice on arrival (screen name or FollowPage line,
  then the tree stop's "Skill Tree" context) — user ruling pending on which line yields.
- Constellations, remaining edges (feature shipped 2026-08-20): (a) Alt+Home onto a
  constellation with inspect live lands the cell on the ROUNDED representative point,
  which can fall just outside the hull and speak "Out of X constellation" on arrival
  at X (measured 0.286 outside on Andromeda) — owner ruling wanted on landing on a
  contained cell instead; (b) an explored constellation with NO perceived member has
  no tree row, so Alt+Home onto it is a silent no-op — fixture-only shape in
  practice, but FocusNode-on-undeclared-forever is unruled; (c) the constellation
  tooltip's first lines lead with the inline icon word ("Crown Owner: No owner") —
  wording nit awaiting owner taste; (d) constellation-to-constellation direct
  boundary crossing (no outside cell between) has no live evidence.
- Election, vote breakdown: the "Political Trends" caption is now declared as the bars'
  region label (one-per-row rollout, 2026-08-18) — code-only, and the drawn child's name
  is a guarded guess. On the first real election turn: check the bars arrive under the
  word, and walk the wizard's flattened bands (step 0 remains code-verified only; step 1
  sighted 2026-08-16).
- `screen.victory` announces the raw key `%VictoryScreenPlayingPlayerTitle` — the mod's
  lookup does not resolve it even though the drawn label localizes (the AGE
  draw-time-localization trap, live-caught).
- Notification arrival-focus race: a popup's first build can run before its description
  label is visible, landing arrival focus on the first control instead of the words (why
  the elimination sentence had to ride the screen name).
- Departing-fade stand-down: the spurious "unavailable" frame when a game confirmation
  opens over a mod screen (general fix).
- General open-row hardening: a screen's own catch should close what it opened (or the
  builder tolerate it). The two known instances are gone — the `system:side/2/Key/2`
  duplicate-id pair is fixed by `SidePanels.PathKey`, and the empire open-row crash is
  structurally removed (side panels emit linear, 2026-08-18) — but an unbalanced
  `PushContext` after a swallowed throw remains the lesser surviving form.
- **The caption-only-row sweep is not finished.** The rule (a drawn caption names its block and is
  a row only where it carries a tooltip — `Captions`, `docs/interaction.md`) has been applied to
  minor diplomacy, the population overview, and the economy/senate/recipe/negotiation headings. Left
  as they are, with reasons: `SidePanels.Effects` (:411-450) declares a `PanelFeatureEffects`
  caption unconditionally, because that collector fills a flat cell list and has no builder to push
  a level on — converting it means giving the side-panel walk a region-aware path, and every
  icon-strip screen rides it; `GovernmentScreen.BuildHeading`, `ImprovementsModalScreen.BuildSummary`
  and `SystemPoliticsScreen.BuildHeading` declare a WINDOW title rather than a block caption (and
  the last of those is known to carry a tooltip); `ElectionScreen`'s `PanelTitleLabel` is the step's
  own question, which is content. `LawsScreen`, `PlanetOverviewScreen` and
  `HeroInspectionScreen`'s `TitleLabel` reads are the THING's name, not a caption.
- `ScanNotificationItemsPanel`/`ScanNotificationItem` — the hacking/scan chip row in Scan
  View is uncovered by the mod (found during the notifications session; not part of the
  main notification strip or its stops).
- Possible `GalaxyScanner` Fleets-category defect: it reads `FleetPresence.Drawing()`
  (a view-side repository), which measured EMPTY for the player's own fleets parked at a
  system (notifications session, Stage 2) — investigate whether the scanner drops parked
  fleets.
- Rebindable mod keys (long-standing, from input.md).
- The contextual prompt's component tables: modelled from the four data-defined shapes, but no
  fixture draws a table with ROWS — re-measure when one can be sighted.
- `StockAndNet` now exists in three copies (GlobalHud, EconomyScreen, JuggernautSpecializationScreen)
  — hoist the visibility-correct one and drop the others.
- **Expansion surfaces — UNSIGHTABLE here until the DLCs are installed** (2026-08-12 audit,
  `audit-dlc-mechanics.md` at the repo root; none of the four expansions has a depot in
  this install, so every item below is code-verified at best — except where the `*_DLC*`
  datatables alone are enough to bind a window, which is how the Behemoth modal got measured):
  - Behemoth family (Supremacy): the specialization modal is a MODEL now, not a floor (layer 29 —
    the datatables load unowned, so the three specializations and the six named resources were
    measurable here). What still waits for the DLC is only what needs a real Behemoth: a TAKEABLE
    card, Confirm, and the toolbar route into the modal; then the rest of the family.
  - Hacking subsystem (Penumbra): dashboard, processing-power/operations banner, traitors
    banner, program panel, operation route-building — one large stage, NOT to be written
    blind; wait for the DLC.
  - Traitor victim-side actions (Reveal/Kill/Remove in the population side panel) +
    pirate-lair and destroyed-planet orbital labels — small, shares the fixture wait.
  - Umbral Choir ghost-system page (Penumbra): the Ghost state is a THIRD mode of the
    star system page beside Outpost and Colony — two side panels of bare-icon controls +
    a growth gauge.
- Spaceport population (batch G): the panel's rows, the pick-up and the drop through
  `SpaceportSidePanel.ApplyDrop` are MODELLED, and no save can draw the panel at all
  (es2-facts: `IsAvailable()` wants a system improvement). The rows and the pick-up were proved
  against a lent binding; the round trip — Space in the spaceport, Enter on a planet card, the
  `OrderTransferSpaceportPopulation` it posts — waits for a save with a spaceport. The OTHER
  direction is not built: the game also drops a PLANET's population INTO the panel
  (`PlanetLabelsWindow_SystemManagement.StartDrag` :144-148 adds the panel as a target), which needs
  the same fixture and an owner ruling on which node in the panel takes the drop.
- Riftborn time bubble on the galaxy map: a `GalaxyEntity` with a disk renderer — no
  widget, no label window; making it discoverable is real map-tree modelling.
- Faction sightings needing a non-UE base-game save (code landed drawn-gated, unsighted):
  Lumeris — tech buy-out on the HUD research line (+ scrap↔sell swap rows); Cravers —
  planet depletion status + hunting-grounds decay; Vodyani — ark-as-colony walk
  (`ExploitedStarSystem`: does the model need to change at all?), ark docking slots as a
  drag target, ark population (full list: `audit-factions.md` §4 stages C-D).

- The go-and-look audit's remaining bucket, DECLINED in the batch-F stage because every one
  of these effects already has a key: map labels' `OnRequestManagementView` (the label's own
  management button is declared), the map's own double click on a system/fleet/lane (Enter
  and Backslash carry it), and the two banner cyclers. They are worth revisiting only if a
  keyboard player reports reaching one of them by no other route.

- Fleet-route surfaces awaiting a live sighting (unit-tested, fixture-blocked in
  `[Beginner] test`): "Uses portal" / "Uses wormhole" on the preview and their itinerary
  mentions (no portal, no wormhole tech); the time-bubble start refill; a real citadel
  interception and a real mid-route invalidation for `FleetRouteWatch` (both driven only via
  `/eval` so far); a positive free-movement route for Ctrl+Backslash (tech not researchable
  from the REPL); the "Ground battle, attacker ⟨empire⟩" named form (a real battle cannot be
  forced — the bare "Ground battle" form is verified); a special node at exploration exactly
  2 named by the reverted gate (states only raise; needs a save that reaches it).
- Trade-route lanes awaiting a live sighting with a REAL trading company (shipped 2026-08-14
  verified against the renderer with an injected one): an external-subsidiary route and a
  simulation-set HQ/subsidiary blockade; an explored special node speaking its kind ("Solar
  Nebula" — shipped 2026-08-14, no fixture reveals one); the obliterator's armed-mode buffer
  readout with a real Behemoth (mechanism shipped and verified via take-system; the message
  branch — ETA, star odds, protection warning — is unsighted, no Behemoth fixture).

- Type-ahead does not index PROBES: a probe under a collapsed system's branch is not
  findable by typing (fleets are — `TypeAheadScope` indexes fleets only). Surfaced when
  probes moved from the top-level open-space region to their nearest star (2026-08-14).

## To decide (owner)


- The drawn-heading lookup renames two out-game pages: "Multiplayer room" and "Asset export".
  Keep the game's drawn headings or the mod's older names?
- Mod-authored wordings awaiting sign-off: "Ring {0} of {1}" for the skill wheel's rings
  (the game names them nowhere); "Sent {0} to {1} by spaceport" (the empire page's
  population shipment); and the scan labels' 2px RelationBar (own/allied/enemy/pirate/
  minor) — currently unread, needs five words or a colour mapping.
- Marketplace SELL rows: the game's middle-click subtracts the click quantity from the
  pending sell order (`SalableItem.OnMiddleClickCb` :71-77) and no convention chord exists
  for a middle click. Options: check whether the panel's own minus button is already a
  declared control (then this is covered and needs only a recipe line), or model the
  quantity as a Left/Right adjustable node the way the zoom ladder is. (Gesture audit,
  2026-08-14.)
- Coordination-pin drag (ally pings on the map): the game's drag moves the pin to an
  arbitrary world point; a keyboard drop can only land on a NAMED map object — a semantic
  narrowing that needs a ruling before Space/Enter carry it, or the gesture stays declined
  like the pin-message editing above it. (Gesture audit, 2026-08-14.)
- One-row tables still say "1 of 1" (the empire page's single system): the count is the
  answer to "how many rows", so it is spoken even for one. Say if it should be suppressed
  at 1.
- Document-shaped `GraphSheet` regions (battle report lines, negotiation terms,
  notification tables) inherited the row positions the tables gained — rows of the same
  sheet engine. Say if a document-shaped region should stay silent instead.
- Scan-lens hero efficiency speaks the dial's drawn angle as "{0}%" (`scan.hero-efficiency`
  — mod-authored, the game draws no number). Sign off or reword.
- Chat polish trio: Escape-out-of-typing is silent when the cursor was already the box
  node (add a confirmation phrase?); Escape closes the panel even under a resting mouse
  (the key was pressed — overrule if the mouse should win); a game-driven page change
  closes chat with the page. All shipped as described; overrule any.

## Shipped (pointers only)

| Screen (game) | Mod file (`ES2Access/Screens/` unless noted) |
|---|---|
| Main menu / options / load-save / loading | MainMenuScreen, OptionsScreen, LoadSaveScreen, LoadingScreen |
| New game lobby / advanced / faction / custom faction / tutorial choice | NewGameScreen, AdvancedSettingsScreen, FactionChoiceScreen, CustomFactionScreen, TutorialSelectionScreen |
| Notifications (all variants) / tutorial | NotificationScreen (+NotificationBody, BattleNotifications), TutorialScreen |
| Galaxy map + HUD / fleet panel | GalaxyHudScreen, GlobalHud, FleetPanel |
| Star system / improvements / system-selection / politics / rename | SystemManagementScreen, ImprovementsModalScreen, SystemSelectionScreen, SystemPoliticsScreen, RenameModalScreen |
| Planet / planet constructibles / discovery cutscene | PlanetOverviewScreen, PlanetConstructiblesScreen, SystemDiscoveryScreen |
| Research / quest journal | ResearchScreen, QuestJournalScreen |
| Scan view | ScanViewScreen |
| Senate / government / laws / population / election | SenateScreen, GovernmentScreen, LawsScreen, PopulationScreen, ElectionScreen |
| Empire / economy / recipe | EmpireScreen, EconomyScreen, RecipeCreationScreen |
| Military / fleet-selection / ship designer / troops / tactics deck | MilitaryScreen, FleetSelectionScreen, ShipDesignScreen, TroopManagementScreen, BattleTacticsScreen |
| Academy / hero selection / hero inspection / hero list / academy modals | AcademyScreen, HeroSelectionScreen, HeroInspectionScreen, HeroCompleteListScreen, AcademyModalScreen, AcademyDiplomacyScreen |
| Battles: setup/report popups, cinematics, advanced report/setup/plays | SpaceBattleScreen, GroundBattleViewScreen, AdvancedBattleReportScreen, AdvancedEncounterPlayScreen, BattleNotifications |
| Diplomacy / negotiation / minor / pirate | DiplomacyScreen, NegotiationScreen, MinorFactionDiplomacyScreen, PirateDiplomacyScreen |
| Target pickers / cutscenes / victory trio / journal | TargetSelectionScreen, CutsceneScreen, VictoryScreen, VictoryAchievedScreen, JournalScreen |
| Dialogs: message box / error / non-blocking / game menu / drop list | MessageBoxScreen, ErrorScreen, NonBlockingMessageScreen, GameMenuScreen, DropListScreen |
| Contextual prompt / Behemoth specialization | ContextualPromptScreen, JuggernautSpecializationScreen |
| Out-game pages: disclaimer / credits / DLC browser / mod manager / join game / asset exporter | DisclaimerScreen, CreditsScreen, DLCScreen, ModdingConfigScreen, MenuDestinationScreens, ResourcesExportModScreen |
