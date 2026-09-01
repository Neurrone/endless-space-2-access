# Roadmap — work remaining

Only work still to be done lives here (owner rule), in three sections: what is left to
build, what waits on an owner ruling, and a pointer index of what has shipped. What a
fixture cannot show and how to test a shipped screen: `docs/test-recipes/README.md`. Mechanisms:
the game-facts topic files indexed in `docs/README.md`. Layers and keys:
`docs/interaction.md`. The shipped index is pointers
only — a row may name a screen and its mod file, nothing else; notes about a shipped screen
belong in the files above.

## To build

- **Zoom bands and scan lenses — what the plan left open.** The plan itself shipped whole
  2026-09-01 (six stages; the spec is `scan-modes-design-proposal.md`, the pointer row is in
  Shipped, the per-screen recipes are in `test-recipes/galaxy-map.md` and
  `.../inspect-and-influence.md`). What is left:
  - **MANUAL, at the machine**: `scan-modes-manual-test.md` — every step only a human can run,
    chiefly the physical PageUp/PageDown re-seat pair, and a FIXTURE-BLOCKED section naming the
    save each blocked item needs.
  - **PINNED, fixture-blocked**: the DRAWN trade-route lines with a real trading company (the
    weave was proved against SYNTHETIC routes and by `TradeWeaveTests` — what a real company still
    has to show is the renderer's three materials, the legend beside them, and a blockade reaching
    the reading the turn it lands); a `GhostFeedback` world, so the dot row's Sanctuary sentence is
    code-only; a "???" system anywhere (the galaxy is 65 Unrevealed / 17 Revealed / 4 Owned with
    nothing between, so no framing can sight one); battle rows and the swap toggle's own UI; every
    hacking table's per-row content, the program costs and the in-map hacking icons (a DLC-owning
    session); empire ordering beyond n = 2; contested-influence RENDERING, whose band placement in
    both modes is still provisional; adrift fleets below level 5; and the `[traitor]` glyph in the
    repartition toggle's failure sentence, which reads as nothing.
  - **FLAGGED, for the owner**: the System lens (levels 11–13) paints NO owner for the surrounding
    systems — measured, all 86 scan labels unpainted at step 11 where the same camera paints 6 at
    step 8 — so the owner grouping the tree keeps there is a deliberate deviation for shape
    continuity rather than parity; the alternative (a flat system list at 11–13) needs a ruling. And
    on a lane carrying several routes every route's line says that LANE's colour, so two routes over
    one blockaded hop both say "mixed"; the per-route alternative would lose the third material the
    picture actually shows.
  - **OPEN, for the owner**: the diplomacy band's SPOKE and BATTLE rows refuse the inspect cell,
    Enter, the leap trail and the restore — the inert default, because whether a row at levels 1–2
    may arm the cell is the question below and nothing was invented for it. At levels 1–2 the tree
    declares only constellation headings (which refuse arming, 2026-08-31) and point bookmarks, so
    Ctrl+I can arm the survey only from a bookmark row — should a constellation heading arm the cell
    at its centroid there, or does entering the survey by zooming out with a live cell suffice? And
    unowned space in the survey says NOTHING about ownership (there is no phrase for "nobody's
    territory" and none was invented) — is the bare pair the wanted answer?
- **Coverage-audit tail (2026-08-24 session; owner-directed):** the last screens with no
  Coverage() run. (1) The battle family — setup, cinematic (`SpaceBattleScreen`), report,
  target selection, ground battle — needs a scratch game engaged with a pirate/major fleet
  (a pirate fleet was one turn away when the 2026-08-24 scratch instance was lost; obey the
  crash rules in `docs/dev-loop.md`). (2) The victory family — `VictoryAchievedScreen`, the
  score screen, the outro cutscene — plus the journal's first finished-game row (which also
  unlocks measuring the journal filter menu's effect on rows and the per-column filter
  buttons with rows): one finished scratch game via the `OrderEliminateEmpire` route
  (test-recipes) closes all of it. (3) A cutscene sighting — no faction intro played on the
  2026-08-24 scratch launch; the colonization-cutscene route (a colony ship settling) is the
  untried alternative. Pending-live checks riding these fixtures: the negotiation window's
  Close button (needs a diplomatic contact), the merged-fleet-lozenge aim re-commit (two
  stacked fleets), the `scan:stats` scroll anchor (a populated stats lens panel), and the
  journal filter menu's effect on rows + its per-column filter buttons with rows present
  (the finished game). MANUAL (physical keys; `POST /key` was refused all session): the
  custom-faction lore box's Escape restoring the pre-edit paragraph and caret, and the
  text-area half of the `AgeControlTextField.KeyDown` hand-over seam.
- **Tooltip features the 2026-08-25 sweep could not prove on real data** (owner ruling:
  BLOCKED until the state exists; their captions and sentence templates are proven, the
  live values are not): `MinigameTeam` (a running minigame — the roster drew empty);
  `HackingOperation` / `HackingOperationStep` / `BeaconDisplacement` (a live hacking
  network — the numbers were hand-fed); `RemainingTurnsNodeRooting` (a really rooted node —
  the toggled variant and a live count); `TimeBubbleHeader` + `Location` (a real time
  bubble); `ShipDesignInfoList` (the pirate diplomacy modal's own provider);
  `AffectedByPlay` (a real play name in a battle — rides the battle fixture above);
  `EffectsInQueue` with real content (measured 2026-08-25: the game's own data gives
  visible in-progress effects ONLY to the Propaganda improvements — outpost panel, and the
  per-party ones need a Dictatorship — every other in-progress descriptor is a bare marker
  or `TooltipHidden`; caption proven, the effect list unproven). The honor-action and
  hacking-program effect-sets sightings are the adjacent item below.
- **Gene Hunters' population badges**: `AssimilationReadyIcon` and `AssimilatedIcon` DO carry
  game sentences (measured 2026-08-24), contradicting the mod comment that says they carry
  none — fix and verify together on a Gene Hunters game (DLC flip makes one startable).
- **"Tooltips" children (landed 2026-08-22) — remaining live checks only**: the construction
  line's festival badge and the honor gauge's own dossier (both Hissho).
- **A hero card's dossiers (`HeroCards.Dossiers`) — sighted on the RECRUITMENT popup only.**
  The other three consumers await a live walk: the Academy's card strip
  (`AcademyScreen.AddCard`), the hero-selection modal (`HeroSelectionScreen.AddCard`) and the
  hero-inspection overview card (`HeroInspectionScreen.BuildCard`). The Academy is blocked on
  every fixture AND on a turn-29 personal save, for a second reason beyond the tutorial gate:
  `AcademyScreen.HeroCardsTable` has no children until the empire OWNS a hero, so the strip
  draws no card to hang dossiers under. Two of the three also need a card the recruitment
  prefab never draws - the health and assignment bands, whose plain-worded explanations become
  nodes there - and the inspection card is the one whose two buttons moved INTO the group's
  actions region, so its walk has to confirm Locate and Inspect still reach the player.
- Cutscene descriptions awaiting a live sighting on the game's OWN trigger: every check so
  far drove `ShowWindow` from the REPL. The colonization scene fires on a real colony
  landing, the intro needs `EnableFactionIntroductionVideos` back on, the outros need a
  finished game, and the three metaplot videos need the Academy questline resolved.
- **One-per-row rollout (landed 2026-08-18) — remaining live checks only**: battle
  popups/screens (all code-only; incl. whether the battle popup speaks its title twice);
  the election wizard incl. the Political Trends label; a hangar with ships; a populated
  Active Events panel; multi-slot recipe projects; the strategics grid; diplomacy side
  panels/metaplot and the three diplomacy modals; the victory family; DLC modals;
  join-game rows; a mod-manager library with a
  mod installed. The marketplace half of the economy page was recut and then restructured on
  the owner's live turn-27 session (2026-08-30): each trading panel is one stop named by the
  game's Sell/Buy word with heading/Filters/Available regions, its trade strip a stop of its
  own named after what is being traded, the sell list and the tax box one per row, the buy
  list still a table (its columns are the section's own declared set), and the price graph a
  table of its own stop with newest-turn-first columns. Focusing a history row also scrolls the
  buy table to that resource's line — implemented, and NEVER ONCE FIRED: no fixture has a buy
  section taller than its viewport, so only the already-visible no-op path has been exercised.
  Remaining there: the
  Ships/Heroes sections — and so both the ship spawn-point picker and a ten-column buy table
  — the tax box's owner form and the graph's no-data branch; no fixture draws any of them
  (`test-recipes/empire-screens.md`, **The marketplace tab**).
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
- **Foreign-system info (2026-08-25 session, landed at parity) — remaining live checks
  only.** The old premise here was wrong: the game shows the side panels for NOBODY on a
  peaceful foreign system (sighted included; the peaceful surface is the label's hover
  tooltip incl. its Defense line, already in the system row's buffer), so what shipped is
  the traitor branch of `Manageable` (the game's own gate; the management child now opens a
  traitor-held foreign system's page). Remaining: the REAL traitor order path (verified only
  by injecting `EmpiresWithTraitors`; needs Penumbra); a MAJOR's foreign colony binding the
  full panel set (a minor's trips a vanilla NRE — `StarSystemScreen.set_ColonizedStarSystem`
  subscribes `DepartmentOfEducation` unguarded, so the page arrives with the previous
  system's panel, same for sighted players); a real ghost system's panel CONTENT (structure
  measured by lend, already declared by the generic walk; its two stops are named
  "Sanctuary population" / "Sanctuary", owner-approved 2026-08-25, heard only in code).
- **Output ratings and the zoom-blind star card (2026-08-25 session, landed) — remaining
  live checks only.** Fixture-blocked branches: a card with a colonization already
  ORDERED must keep reading numbers (the empire card's bind-time gate, code-verified
  only); an unrevealed planet's card must stay silent (the game's `IsNodeRevealed` gate,
  code-verified); the empire screen's own player route (F1 is tutorial-disabled in the
  beginner save — every check went through a forced window build); the discovery
  cutscene's card; a MAJOR's foreign colony and a citadel system's extra manpower row in
  the star tooltip (same fixture debt as the entry above); the organic route to the
  "orbital window survives zooming out" wedge (staged and covered, never re-triggered
  live).
- Space-battle balance sentence + plan chooser (2026-08-29: directional balance naming both
  fleets, the plan carousel as one closed combo row over a chooser child screen, nested
  plan/ship/arena tooltip children): the SETUP popup is verified live, and so is the
  ADVANCED-PLAY modal since its stats band became a pager (2026-08-29: the military page's row
  computes the sentence from the encounter groups, so it reads whatever page the window is
  showing). The remaining two users of `BalanceText` are code-symmetry only and need a battle
  fought — the REPORT popup and the cinematic. Unsighted with it:
  `battle.balance-all` (the wipe-out phrase, needs a side
  reduced to zero military power), the first-non-reinforcement-garrison rule on a side with
  reinforcements or merged fleets (the fixture has one garrison a side), a plan card the game
  draws `NoEffectsLabel` on, and a plan whose three flotilla ranges differ (only a 3x
  "Short Range" card's nested names were dumped).
- Space-battle cinematic, the six lines this report cannot produce (2026-08-30 narration stage,
  r50): the acts, the phases, the losses, the progress quarters, the outcome word and the whole
  exchange of fire are verified on a watched re-run of the Sabel battle, but reinforcements arriving
  mid-fight, a ship repairing, a battle effect, a medal, the two shield-absorption clauses and
  citadel fire have never been heard — that report holds none of what feeds them (the measurements
  are in `test-recipes/battles.md`, **Fixture-blocked**). Needs a battle with reinforcements, shields
  that hold, a medal earned, and a defended system.
- The repair line's own premise is unmeasured (same stage): it is written against a positive `Health`
  delta on an `EncounterShipSection`, chosen because that is the level the stream writes hull at
  (measured on the damage side) and because a module's health is part of its section's — but no
  positive delta occurs on this fixture, so neither the level nor the per-ship summing has been seen
  to be right. First battle with a repair, check the figure against the ship's own health bar.
- Advanced-play fleet arrangement (2026-08-29 stage shipped the ship lock and the carry): a
  successful CROSS-FLOTILLA move and the juggernaut SWAP are both unverified — the fixture has one
  valid flotilla, so every drop it can reach is a refusal. Needs a 5+ CP fleet battle: the manual
  lines are in `test-recipes/battles.md` (ADVANCED block). Two things ride on it — whether
  `BattleGroupSetupPanel`'s roster lines redraw after a drop (only the 2D flotilla cards are proven
  to), and whether the swap is worth advertising, since `DropAccepts` is the game's
  `CanAddShipItem` and that branch succeeds where it says no, so the swap works and no row offers it.
- Ground-battle setup follow-ups (2026-08-25 stage shipped the screen model): live-check the
  defender-side role wording (`%…DefenderDescription` on YOUR side; needs a battle where the
  player defends) and the `[positiveImpactWhite]` half of the manpower marker strip (needs a
  limit-raising tactic selected — Blitz); regression-walk the two space-battle popups (only
  shared code touched is `Value`, default path unchanged).
- Ground-battle REPORT follow-ups (2026-08-25 stage shipped: balance, outcome description,
  strategy dossiers, damage totals + per-source rows, drawn "Remaining" caption): unsighted
  variants — defender-side `ConscriptedPopulationGroup` (mod declares nothing for it today),
  the decisive-outcome Dismiss button (chrome declares it, `NotificationScreen.cs:2995`,
  outside the `own` gate — needs one live confirmation), `DefenderSurrendered` (mod speaks
  real final manpowers where the gauge draws symbolic 1/0), third-party spectator
  (`BattleSubTitle` + the outcome's third-party branch). Owner rulings 2026-08-25: the outcome
  description IS announced on arrival (space-report parity; SHIPPED r13 same day); the
  Remaining-before-Reserve order deviation stays (owner: not important).
- Ground-battle OUTCOME-SELECTION popup: modelled 2026-08-25 (r13; `GroundOutcome` body —
  system header, shared one-of-N via `NotificationScreen.BuildChoices`, the by-name Confirm,
  the countdown declared focus-announced and multiplayer-only) — the ENTIRE live sighting is
  pending (needs a decisive victory). The hacking outcome picker keeps its choice-only
  baseline: its parameters sub-choice and its own outcome countdown still need the same
  treatment.
- Notification variants awaiting a live sighting (baseline ships; upgrade per popup on
  sighting): election survey; SimpleDescription-family
  members with own fields (alliance update, diplomatic relation change, constellation
  event, deed completed); the 9 header-less line-class tables (bailiff + its totals
  footer, law cancelled, population change (SIGHTED 2026-09-01 and its own parity check has a
  finding: `PopulationChangeNotificationWindow` "says a figure with no caption" on a table row —
  the first of these nine to be seen live, and the finding is the work), trading blockade, treaty cancelled, relics ×2,
  queue-empty, lost-roots connectivity); one-of-N semantics for the hacking outcome
  picker — these walks, plus the narrative-event
  choice, now ALSO double as regression checks for the 2026-08-25 choice-card split (title
  announced, card text as buffer lines; all four families share `AddChoices`);
  PirateMissionReportNotificationWindow (fixture-blocked: its `Bind` needs a live
  `AttackSystemPirateDiplomaticAction` — the other five report popups are done);
  DiplomaticInteractionNotificationWindow (MoodMessageLabel, NegotiationContributionPanel).
- Ctrl+L's type dispatch (`NotificationScreen.GoToLocation`) is unproven on the two
  space-battle popups, the two ground-battle popups and the hacking popup — their own
  show-location handlers are code-verified against the decompiled overrides only, and no
  fixture raises the windows.
- Galaxy-label gaps: pin-message editing (the constellation ownership bonus shipped 2026-09-01).
- Input batch (code landed 2026-08-22, LIVE-VERIFIED 2026-08-22 on `[Beginner] test` — recipes in
  `test-recipes/galaxy-map.md`): single-press tree arrows, the six place keys incl.
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
- The camera-rule keys are unproven as PHYSICAL keys (2026-08-23 stage: `POST /key` answered
  409 throughout) — Backslash, Ctrl+L and the scanner chords were exercised only as injected
  actions.
- Scanner (shipped, taxonomy v2 2026-08-16, Contested Influence added 2026-08-21, taxonomy v3
  2026-08-22, three CUSTOM slots AFTER them 2026-08-23, moved from in front 2026-08-24): THIRTEEN categories in the owner's order — systems, colonizable planets, unexplored,
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
  `EmpirePosition.Known` is true — ES2 facts), and quest markers, pins and missiles have never
  been heard at all (no fixture draws one). Open judgment call for the owner: MINOR-faction home
  systems are deliberately NOT in "homeworld" (the diplomacy lens the gate came from iterates
  major empires only) — including them would add ~9 fixture systems to that scope.
- Quest markers are proved by SYNTHETIC markers only: the marker nodes, the open-space rows,
  the scanner category, the inspect-cell reading and the quest locate were all exercised by
  registering markers by hand (recipe in `docs/test-recipes/galaxy-map.md`), because both
  saves carry zero markers on every in-progress quest. Neither save has a mid-lane fleet
  either, so a real free-floating marker on a fleet crossing a lane has never been seen — the
  open-space case was forced with a marker bound to a Ship.
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
  in ES2 facts). Colour, alpha and thickness are now the mod's to choose, and a ~26 px floor keeps a
  1×1 cell visible at full overview. Remaining: the mode's camera does not zoom out with the cursor,
  so a size-11 cell close in is still wider than the viewport (the square's off-screen edges simply
  clip). Its cell reading carries obliterator missiles and ally pins, which remain
  fixture-unreachable and have never been heard.
- Enter on a fleet-only inspect cell once failed to take the selection while an EMPTY
  `GalaxyGarrisonCursor` was already up (seen once; resetting the cursor cleared it) — never
  reproduced, cause unknown.
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
- Fleet-panel focus handover (shipped — `docs/fleets.md`), still unverified: a fleet
  destroyed in combat while its panel is up, and the obliterator paths (no Behemoth in
  any save). All three fixture saves are missing from disk (`test-recipes/fixtures.md`).
- Chat: the child screen ships whole (tabs, message log, box, the page-level new-message
  button) and is verified in a single-player fixture; what no fixture here has shown is the
  MULTIPLAYER half — the alliance tab, the new-message button actually being raised (in
  single player a Global line on the Global tab never raises it, so its page-level stop is
  unverified live), whispers and alliance sending.
- Assigned-governor side panel: `Special` case for its three bare readouts (needs a save
  with a governor).
- Skill-tree type-ahead: EVERY screen now searches what its collapsed branches would declare
  (`GraphBuilder.ExpandAll`), so this should be covered by construction - it is unverified
  live because no fixture reaches a hero with a skill tree.
- Galaxy-map audit remainder (2026-08-20): `StarSystemLabel`'s 32 public widget fields are
  now fully covered (bars wording awaits owner approval; exploration-winner and shared-system
  readings are fixture-blocked to mid-game saves). Still open from the audit: the Riftborn
  time bubble (needs a Riftborn fixture) and the latent hacking beacon (deferred with
  Penumbra).
- Hero skill page: of the base-class `PanelFeatureEffectsSets` tooltip variants, the
  honor-action and hacking-program ones are still UNSIGHTED — one `DevProbe.Tooltip()` on
  each should answer `"effect-sets"` with no "Level" line (the planet and skill variants
  were sighted 2026-08-25, tooltip sweep; a "hero" variant does not exist — the `Hero`
  class carries no effect-sets feature). The relics box/region and a two-mastery starting
  skill also await a fixture (Nakalim/Templar hero).
- Hero skill page: the heading now speaks twice on arrival (screen name or FollowPage line,
  then the tree stop's "Skill Tree" context) — user ruling pending on which line yields.
- `DevProbe.Tooltip()` answered `{"error":"No token to close. Path ''."}` on the research
  wheel's play-deck child (2026-08-22); the cause was never chased, and the workaround is
  the one retained in `docs/test-recipes/galaxy-map.md`.
- Panel-feature components are largely unjudged: of the 149 classes (`gui.md`), seven have typed readers
  and three more were source-checked and cleared; the rest fall to the default reader, which
  names itself in the log and in `DevProbe.Tooltip().defaultRead`. The next play session's
  `defaultRead` list is the work queue, not a desk review.
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
- The star-system page declares nothing while it is BETWEEN systems, so the cursor migrates
  to a HUD stop for a moment and that migration is announced — one stray line between the
  name and the landing. Fixing it wants a screen-level "my content is between pages, hold
  the cursor" gate the navigator does not have yet.
- General open-row hardening: a screen's own catch should close what it opened (or the
  builder tolerate it). The two known instances are gone — the `system:side/2/Key/2`
  duplicate-id pair is fixed by `SidePanels.PathKey`, and the empire open-row crash is
  structurally removed (side panels emit linear, 2026-08-18) — but an unbalanced
  `PushContext` after a swallowed throw remains the lesser surviving form.
- **The caption-only-row sweep is not finished.** The rule (a drawn caption names its block and is
  a row only where it carries a tooltip — the `Captions` doc comment,
  `ES2Access/UI/Captions.cs`) has been applied to
  minor diplomacy, the population overview, the economy/senate/recipe/negotiation headings, and
  (2026-08-22) the Academy and pirate diplomacy ACTION bands — neither window opens in
  `[Beginner] test`, so the Academy's stays prefab-verified while the pirate window's was confirmed
  live on 2026-08-30 (on a save that has contacted a Lair). The Academy's own "Status" caption over its
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
- ~~`ScanNotificationItemsPanel`/`ScanNotificationItem`~~ — the hacking/scan chip row in Scan View
  is a stop of its own as of 5b (`scan:notifications`, a row per drawn chip). Its per-chip content
  stays unverified: nothing has ever put a chip in it.
- The map fleet lozenge's two ship-kind badges (`ExplorationShipIcon`/`ColonyShipIcon`, "One of
  these ships is an exploration/colony ship.") are drawn beside the `GuiFleetGroup` dossier the
  fleet row already carries in full, and neither sentence is anywhere in the mod. The fix is the
  shape `GlobalHud.AddScreenToggles` uses — every tooltip inside the lozenge, first speaking, the
  rest reviewable. Owner call on the shape.
- The pinned-quest panel's OWN sentence is in its buffer, but the panel's tooltip still reads
  `uncovered` in `DevProbe.Coverage()`: the node deliberately AIMS at the objective label's
  tooltip, which is the only one pointing at what the panel draws. Tooling gap, not a
  player-facing one.
- The discovery/colonization cutscene card still speaks item NAMES only (`DiscoveryCards.Read`);
  a passive announcement has no node for a dossier to hang on.
- `HauntCirclesTable` (`HauntCircleItem`) at systems zoom is unmeasured — no fixture draws one.
- Fixture-blocked live and proved by structure only: the planet card's three in-progress
  buttons, the pirate lair, `OutpostCancelIcon`, `HauntIcon`, and every signal line in
  `AddSignals` (no juggernaut, no ghost colony, no unique world, no outpost in trouble on
  `[Beginner] test`). Two more of the same shape from 2026-08-27: an ELIMINATED empire's
  diplomacy card (every empire in both saves is alive, so only the unmet half of the
  `IsKnown && !HasBeenEliminated` gate has been heard), and a technology stage whose new
  unlock `actions` region also holds a DEED node (Military II draws no readable deed).
- A curiosity in neither of the two Curiosities columns (one already being expedited, or
  quest-locked) does not exist on `[Beginner] test` — both columns are unproven against a
  real one.
- Boot-proof owed: the 208+208 "Could not find GuiWindow named 'LoadSaveModalWindow' /
  'OutGameLoadModalWindow'" errors per session are written while the window registry fills, so
  the fix can only be proved by the next COLD launch — count them in the newest
  `Documents\Endless Space 2\Temporary Files\Diagnostics - *.html`.
- Custom scanner categories: SHIPPED whole 2026-08-23, and DRAWN 2026-08-24 - the model, the
  synthesis and the six quick keys (stage 3); the editor (stage 4); and the rebuild that made it a
  page a sighted player can see (stage 5), then folded onto ONE page (stage 6): the window has two tabs
  — Scanner and Controls — and the Scanner tab holds three drawn headers that open and shut in
  place, every row the game's own widget, under the window's own Apply/Cancel. The columns come from
  the game's DATABASES, so the tab exists on the main menu too (stage 6). The
  player's own categories come LAST in the category cycle, not first (stage 5 - first was where an
  unconfigured slot answered the very first scanner press with "none found"). What is left is
  MANUAL: the six physical quick keys, the typing half of a text row (the harness can write a game
  field's text but cannot press a key at it), and the Scanner tab REACHED FROM THE MAIN MENU.
- Rebindable mod keys: SHIPPED 2026-08-23 - the mod's own Controls tab (stage 2a), the
  three-column binding table with Delete-to-clear, the Escape cancel and the two-way mod/game
  overlap warning (stage 2b, rulings 6/9/10), on the game's own Controls tab alike; and, from stage
  6, a capture that lands on the chord already bound still SPEAKS, the overlap warning has a Cancel
  that reverts, and Reset to Defaults resets the mod's keys. The physical-key half is VERIFIED as of
  stage 7 (2026-08-24, `POST /key` with the game foregrounded): Enter to capture, a chord committing
  and being spoken, Escape cancelling a capture, and the reported vanish - which was the clone being
  asked for input AHEAD of the message box, fixed by registering it beside the game's own options
  window (ES2 facts). What is left is the MANUAL pass over the physical LETTER keys
  (type-ahead on the settings pages).
- The contextual prompt's component tables: modelled from the four data-defined shapes, but no
  fixture draws a table with ROWS — re-measure when one can be sighted.
- `StockAndNet` now exists in three copies (GlobalHud, EconomyScreen, JuggernautSpecializationScreen)
  — hoist the visibility-correct one and drop the others.
- **Expansion surfaces — each needs its expansion's CONTENT to sight** (2026-08-12 audit,
  `docs/audit-dlc-mechanics.md`). Whether a session can reach them is a property of the machine and
  the save, never of this file: probe it live at the start of a stage
  (`docs/test-recipes/fixtures.md`, "Which expansions this session can reach") and pick the work accordingly. Some of these are measurable without the
  content at all, because the `*_DLC*` datatables bind a window on their own — which is how the
  Behemoth modal got measured:
  - Behemoth family (Supremacy): the specialization modal is a MODEL now, not a floor (layer 29 —
    the datatables load whether or not the content is active, so the three specializations and the
    six named resources were measurable). What still needs a real Behemoth: a TAKEABLE card,
    Confirm, and the toolbar route into the modal; then the rest of the family.
  - Hacking subsystem (Penumbra): dashboard, processing-power/operations banner, traitors
    banner, program panel, operation route-building — one large stage, NOT to be written
    blind; needs a save actually running a hacking operation.
  - Traitor victim-side actions (Reveal/Kill/Remove in the population side panel) +
    pirate-lair and destroyed-planet orbital labels — small, shares the fixture wait.
  - Umbral Choir ghost-system page (Penumbra): the Ghost state is a THIRD mode of the
    star system page beside Outpost and Colony — two side panels of bare-icon controls +
    a growth gauge.
- Riftborn time bubble on the galaxy map: a `GalaxyEntity` with a disk renderer — no
  widget, no label window; making it discoverable is real map-tree modelling.
- Faction sightings needing another base-game save (code landed drawn-gated, unsighted):
  United Empire — the Influence tech buy-out on the HUD research line, which the
  NON-tutorial UE affinity grants and the tutorial one has commented out, so this save
  cannot draw it (ES2 facts); Lumeris — the scrap↔sell swap rows; Cravers —
  planet depletion status + hunting-grounds decay; Vodyani — ark-as-colony walk
  (`ExploitedStarSystem`: does the model need to change at all?), ark docking slots as a
  drag target, ark population (full list: `docs/audit-factions.md` §4 stages C-D).

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

- Map bookmarks shipped 2026-08-31 (below); what no stage could reach is a MANUAL pass, written
  out as player steps in `bookmarks-manual-test.md`: a point-bookmark SET from a free fleet or
  probe (no fixture draws one), the first-save flush on a never-saved campaign, and the physical
  Shift+digit set made with the inspect cell live (the desktop was locked for stage C's `/key`
  pass — the injected path is proven, the real keypress is not).

- The balance-bar captions in a tooltip's ship/fleet stat block (`TooltipFeatures.Balance`) are
  sighted on the `ShipDesignHeroRecruitement`, `ShipDesign` and `Ship` classes only. Still to
  sight: the `Garrison` class (`PanelFeatureMilitaryPowerBalance`, reached from `FleetLine` /
  `GarrisonsLabelButton` — neither the map's fleet group nor the military screen's fleet rows
  draws it), and the `WreckedMothership` / `WreckedMothershipVampirilis` classes. All three go
  through the same helper, so what is unsighted is the PREFAB question: whether those prefabs
  draw a caption beside each bar, which would make the added one a double-name.

## To decide (owner)

- The drawn-heading lookup renames two out-game pages: "Multiplayer room" and "Asset export".
  Keep the game's drawn headings or the mod's older names?
- An OUTPOST planet card now reads "Overpopulation, Empty slot 1 of 1". That is exactly what the
  game draws (one empty marker with the orange arc beside it — `docs/planets.md`), and it is what
  mirroring the ring produces, but the slot-rows brief expected outposts to gain no rows at all.
  Keep the drawn shape, or suppress slot rows while `ColonizedStarSystem.State == Outpost`?
- Mod-authored wordings awaiting sign-off: "Ring {0} of {1}" for the skill wheel's rings
  (the game names them nowhere); "Sent {0} to {1} by spaceport" (the empire page's
  population shipment); and the scan labels' 2px RelationBar (own/allied/enemy/pirate/
  minor) — currently unread, needs five words or a colour mapping.
- Marketplace SELL rows: the middle-click subtract is covered — the panel's ± buttons are
  declared nodes ("Click: removes 1 unit" / "adds 1 unit", verified live 2026-08-28) and
  the quantity is an edit field (the 2026-08-27 ruling: no Left/Right adjust outside the
  edit). The recipe line for the marketplace family landed 2026-08-30
  (`test-recipes/empire-screens.md`, **The marketplace tab**). (Gesture audit, 2026-08-14.)
- Coordination-pin drag (ally pings on the map): the game's drag moves the pin to an
  arbitrary world point; a keyboard drop can only land on a NAMED map object — a semantic
  narrowing that needs a ruling before Space/Enter carry it, or the gesture stays declined
  like the pin-message editing above it. (Gesture audit, 2026-08-14.)
- One-row tables still say "1 of 1" (the empire page's single system): the count is the
  answer to "how many rows", so it is spoken even for one. Say if it should be suppressed
  at 1.
- Document-shaped `GraphSheet` regions (negotiation terms, notification tables) inherited
  the row positions the tables gained — rows of the same sheet engine. Say if a
  document-shaped region should stay silent instead. (The advanced battle report's phase
  grid was the third of these and is gone: 2026-08-30 made it a flat list.)
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
| Scan view: the galaxy lenses / the system and planet lenses | GalaxyHudScreen (+`Screens/Galaxy/ScanRows.cs`, `ScanDiplomacy.cs`, `ScanHacking.cs`, ScanLensPanels), ScanViewScreen |
| Senate / government / laws / population / election | SenateScreen, GovernmentScreen, LawsScreen, PopulationScreen, ElectionScreen |
| Empire / economy / recipe | EmpireScreen, EconomyScreen, RecipeCreationScreen |
| Military / fleet-selection / ship designer / troops / tactics deck | MilitaryScreen, FleetSelectionScreen, ShipDesignScreen, TroopManagementScreen, BattleTacticsScreen |
| Academy / hero selection / hero inspection / hero list / academy modals | AcademyScreen, HeroSelectionScreen, HeroInspectionScreen, HeroCompleteListScreen, AcademyModalScreen, AcademyDiplomacyScreen |
| Battles: setup/report popups, cinematics, advanced report/setup/plays | SpaceBattleScreen, GroundBattleViewScreen, AdvancedBattleReportScreen, AdvancedEncounterPlayScreen, BattleNotifications |
| Diplomacy / negotiation / minor / pirate | DiplomacyScreen, NegotiationScreen, MinorFactionDiplomacyScreen, PirateDiplomacyScreen |
| Target pickers / cutscenes / victory trio / journal | TargetSelectionScreen, CutsceneScreen, VictoryScreen, VictoryAchievedScreen, JournalScreen |
| Dialogs: message box / error / non-blocking / game menu / drop list | MessageBoxScreen, ErrorScreen, NonBlockingMessageScreen, GameMenuScreen, DropListScreen |
| The mod's own settings window (the game's options modal, cloned; Scanner and Controls tabs) | `ES2Access/UI/ModOptions/` + OptionsScreen |
| Contextual prompt / Behemoth specialization | ContextualPromptScreen, JuggernautSpecializationScreen |
| Out-game pages: disclaimer / credits / DLC browser / mod manager / join game / asset exporter | DisclaimerScreen, CreditsScreen, DLCScreen, ModdingConfigScreen, MenuDestinationScreens, ResourcesExportModScreen |
| Go to location: one galaxy landing, quest markers as nodes, Ctrl+L | GalaxyHudScreen.GoTo, MapLandings, NotificationScreen |
| Galaxy content from data: deposit/star/planet dossiers at every zoom, the mod's own tooltip carrier | GalaxyHudScreen, ScratchTooltips |
| One camera rule for the galaxy map (cursor-led, no flight; a move by anybody else voids its record — 2026-08-26) | GalaxyHudScreen.OnFocusVisual, Screen.OnFocusVisual, GalaxyViewLevels.Moves |
| Type-ahead over collapsed branches, trail-free | GraphBuilder.ExpandAll, SearchScope.Extend |
| The star-system page turn: one announcement, cursor in the new system | SystemManagementScreen |
| Planet card pages on the map and the system page (buttons, anomalies, signals) | GalaxyHudScreen, SystemManagementScreen |
| Choice cards: title announced, card text as buffer lines; faded cards no longer declared | NotificationScreen.AddChoices, `UI/AgeWidgets.Paints` |
| Deselecting a fleet seats the cursor on the fleet's own row | FleetPanel, GalaxyHudScreen |
| A line clipped by its scrolling window is measured at the window | `UI/AgeWidgets.Clipped` |
| Docked fleets in the scanner and the inspect cursor; minor/Academy fleets read by standing | GalaxyScanner, `UI/FleetPresence` |
| Map bookmarks: ten per campaign, set/jump/home, tree rows and inspect cells, per-campaign file | GalaxyBookmarks, GalaxyHudScreen, GalaxyInspect, `UI/Bookmarks/MapBookmarkStore`, `Core/Bookmarks/` |
| Zoom bands and scan lenses: kind parity per band in both views (tree, scanner, inspect cell), the motion model, the label pictures as child nodes, the galaxy page wearing the lens, the diplomacy band, the hacking family, the trade-route weave | GalaxyHudScreen + `Screens/Galaxy/*`, `Core/UI/Bands.cs`, `UI/ZoomBands.cs`, ScanViewScreen |
