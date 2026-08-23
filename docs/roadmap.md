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
  every map node under `unknown` rather than judging its aim. BOTH the other open items are
  CLOSED by batch 7 (2026-08-22): the population side panel's party dossiers are now children of
  their population row, not the row below it (`Cells.Declare` lets a cell open a subtree), verified
  live on Dusay - "Imperials, 3, collapsed" opens onto "Tooltips, Industrialists" with
  `DevProbe.Tooltip()` `shown:true`; and the parity audit now counts a node that declares its AIM at
  a dossier as covering it (`NotificationAudit.Covering`), so the population overview's six party
  dossiers moved out of `uncovered` (0 there now, nothing else rose).
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
- Skill-tree type-ahead: batch 8 gave EVERY screen a search over what its collapsed branches
  would declare (`GraphBuilder.ExpandAll`), so this should now be covered by construction - it is
  unverified live because no fixture reaches a hero with a skill tree.
- The planet page's population-entry click has no opener node yet (the window itself,
  StarSystemPopulationModalWindow, is covered — SystemPoliticsScreen binds it).
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
  minor diplomacy, the population overview, the economy/senate/recipe/negotiation headings, and
  (2026-08-22) the Academy and pirate diplomacy ACTION bands — the last two prefab-verified only,
  since neither window opens in `[Beginner] test`. The Academy's own "Status" caption over its
  relation panel is NOT converted: its stop still reads the mod's `academy.relation`, and swapping it
  for the game's word is an owner call bundled with the rest of that screen's wording. Left
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
- Faction sightings needing another base-game save (code landed drawn-gated, unsighted):
  United Empire — the Influence tech buy-out on the HUD research line, which the
  NON-tutorial UE affinity grants and the tutorial one has commented out, so this save
  cannot draw it (es2-facts); Lumeris — the scrap↔sell swap rows; Cravers —
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

## Batch 7 (2026-08-22) — go to location

**Landed and verified live**: one landing on the galaxy page (`GalaxyHudScreen.GoTo` over
`MapLandings.Decide`) used by the game's locate, the scanner's Alt+Home, starlane travel and the new
Ctrl+L; quest markers as NODES (system children and open-space rows) off one enumeration; Ctrl+L as
the game's own show-location; the population side panel's party dossiers as Tooltips children; the
parity audit counting an aim as coverage.

**A landing's first utterance no longer precedes the camera** — one of the two pre-existing defects
recorded in the previous session. `Screen.LandingSuspended` covers the camera flight and a
twenty-frame tail, and a suspended frame holds even a control already declared. Fixed and proved on
Osulo I (test-recipes).

**Still OPEN, untouched by batch 7**: turning the STAR-SYSTEM page announces the screen twice and
seats the cursor on the scan button (the other pre-existing defect from the previous session).
FIXED in batch 8, below.

**Fixture-blocked, verified only through synthetic markers**: everything about quest markers. Both
saves have ZERO markers on every in-progress quest, so the marker nodes, the open-space rows, the
scanner category, the inspect-cell reading and the quest locate were all proved by registering
markers by hand (recipe in `docs/test-recipes.md`). The one thing no synthetic marker could produce
is a real free-floating marker on a fleet crossing a lane — neither save has a mid-lane fleet either,
so the open-space case was forced with a marker bound to a Ship.

**Unproven**: the two space-battle popups, the two ground-battle popups and the hacking popup's own
show-location handlers (no fixture raises them) — code-verified against their decompiled overrides
only. Ctrl+L on those windows would exercise `NotificationScreen.GoToLocation`'s type dispatch.

## Batch 8 (2026-08-22) — follow-ups and the feature audit

**Landed and verified live**: the colonizable-planet scanner row's short resource names with zero
outputs dropped; the galaxy landing's camera made INSTANT (`GalaxyViewLevels.SnapTo`, with the
map's forced-zoom bookkeeping and `RestoreZoom` intact); type-ahead searching everything a
collapsed branch WOULD declare on every screen (`GraphBuilder.ExpandAll` + `SearchScope.Extend`);
the star-system page named "⟨system⟩, System management"; a typed reader for the battle-tactics
deck ("Flotilla 1 Short Range"); the fallback tooltip reader naming itself
(`TooltipFeatures.DefaultRead`, `DevProbe.Tooltip().defaultRead`); the minor gauge's four bands
named "CORDIAL (25)" and its points row captioned "Relationship".

**The star-system page turn is fixed** — the second of the two pre-existing defects carried since
the previous session. One screen announcement per turn and the cursor lands in the new system's
content instead of on the view-title scan button. Remaining rough edge: while the page is between
systems it declares nothing, so the cursor migrates to a HUD stop for a moment and that migration is
announced — one stray line between the name and the landing. Fixing it wants a screen-level "my
content is between pages, hold the cursor" gate the navigator does not have yet.

**Fixture-blocked in batch 8, FIXED in batch 9 (2026-08-23):** the deposit/star dossiers a system's
map label carries beyond the star's own now come from the planets rather than from the icons the
camera happens to be drawing, so "antimatter" is findable on the galaxy at every zoom and the
search's reach into map-label dossiers is proved on the map as well as on the research wheel's
unlock children and the population panel's parties.

**Unjudged, deliberately**: of the 119 panel-feature component classes, seven have typed readers and
three more were source-checked and cleared; the rest fall to the default reader, which now names
itself in the log and in `DevProbe.Tooltip()`. The next play session's `defaultRead` list is the
work queue, not a desk review.

## Batch 9 (2026-08-23) — galaxy content from data, and the game's own logging

**Landed and verified live**: a system's deposit and star dossiers built from
`node.Planets[*].ResourceDeposits` and `GuiStarSystem.Instantiate` rather than from what the camera
draws, aimed at the game's own icon where there is one and at a mod-owned carrier
(`ScratchTooltips`) where there is not — words byte-identical either side of a zoom; a planet row's
size-and-type, curiosity count and anomalies from the planet, so they read the same with the card
drawn and without it; the battle-tactics flotilla rows joined with a colon
("Flotilla 1: Short Range"); two new scanner columns under Curiosities (Explorable / Insufficient
Expedition Power); type-ahead stepping closing the branches the previous landing opened; the two
by-name `GuiWindow` lookups and the `/state` probe no longer writing hundreds of Errors into the
game's diagnostics and telemetry.

**Boot-proof pending**: the 208+208 "Could not find GuiWindow named 'LoadSaveModalWindow' /
'OutGameLoadModalWindow'" errors per session are written while the window registry fills, so the fix
can only be proved by the NEXT cold launch — count them in the newest
`Documents\Endless Space 2\Temporary Files\Diagnostics - *.html`.

**Untested in this fixture**: the mod's carrier for a PLANET dossier — the planet circles are drawn
at every zoom `[Beginner] test` reaches, so the fallback never fires (the star and deposit carriers
were both exercised, the star one by a direct call). A curiosity in neither of the two new columns
(one already being expedited, or quest-locked) does not exist on this save either.

## Batch 10 (2026-08-23) — one camera rule, and the carrier's place

**Landed and verified live**: the galaxy camera follows the cursor by ONE rule
(`GalaxyHudScreen.OnFocusVisual` → `Place` → `FollowPlace`, over a new per-screen
`Screen.OnFocusVisual` hook), replacing the system row's own `PanTo`, `OnExpand`'s `ZoomTo` (system
nodes no longer override `OnExpand` at all) and the go-to landing's own `SnapTo`. Gate = the place
the camera was last ASKED for and how close, kept mod-side and cleared on `OnPop`, because
`FocusedStarSystemNode` was measured to be where the camera IS and to lag a flight (es2-facts). A
Backslash zoom-out now survives the rest of a system being read; a collapse still zooms out and also
drops "inside", so re-opening comes back in; a landing moves regardless of the record. The mod's
tooltip carrier draws at the screen's BOTTOM-LEFT (`TOP_LEFT` anchor at the corner, so a panel of any
height sits inside the screen) with its words unchanged.

**Unproven**: nothing here was pressed as a PHYSICAL key — `POST /key` answered 409 (the game was not
the foreground window) for the whole stage, so Backslash, Ctrl+L and the scanner chords were exercised
only as injected actions. The mod's carrier for a PLANET dossier still never fires on this fixture,
so the bottom-left placement was proved on a deposit dossier only (one carrier, `TOP_LEFT`, drawn at
`0,420,240,380` on a 1280x800 screen).
