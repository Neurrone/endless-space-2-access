# Stage 5a — scan mode on the shared components

Branch `scan-modes`, 4 commits, build 0 warnings, `dotnet test` 1187/1187 green, verified live on
`ES2Access-r17`…`r20` against `[Beginner] access test`. Game RUNNING, **scan OFF**, camera home
(`focus [68.861,0,-22.453]`, step 8), cursor `hud:empire/screen/EmpireScreen`, both constellations
expanded / systems collapsed, test bookmark 5 cleared, `TooltipDelay` at the 0 the session found.
`docs/generic/` untouched; `sync-generic-src.ps1` NOT run (no mirrored file changed).

1. `d0edba8` **the level-13 owner fix (option (a))**, `UI/SystemLabelReadout.cs`. TWO things were
   asking: `Picture` now tests the widget's OWN alpha under a `Visible` walk (a retired pool item
   fades ITSELF, so leftovers stay out), and `Unvouched` strips the CARRIER off what it collected so
   `TooltipChildren.Stands` declares it Synthetic — the central gate was dropping the rest with
   `why=ancestor faded to nothing and settled`. Pointer untouched. Plus `ScanIcons`, the same one
   door for the scan label's pictures.
2. `b700836` **a lane's fleet count is band-gated** (`LaneRows`) — the generic fix; the system's own
   count went silent below the lozenge band in stage 1 and the lane's did not.
3. `3098814` **the gate, the composition, the tree, the retirement.** `GalaxyHudScreen.IsActive`
   answers the galaxy overview in EITHER view minus `ScanLensPanels.BattleEnding()`;
   `ScanViewScreen.IsActive` adds `!GalaxyViewLevels.Overview` and keeps the management/planet
   builders. `Screens/Galaxy/ScanLens.cs` = `ScanLensPanels` (title, legend, `scan:system`,
   announcement, arrival gate, battle standdown), worn by both pages. `Screens/Galaxy/ScanRows.cs` =
   the owner-grouped tree. `EnsureBand` and `FollowPlace`'s inside-snap stand down in-mode.
   `PlacedRows` gains `Grouping("owner")`.
4. `f8e5e6f` **doc landings** — `interaction.md`, `test-recipes/galaxy-map.md`, `.../scanner.md`,
   `roadmap.md`.

## Evidence

- **Handover, both ways** (`POST /wait` + `DevProbe.Trace`, per frame): `stack=screen.galaxy:10`
  throughout, cursor never moves (`…/system/535` in, `…/system/141` out), nodes 66→41 and 41→66,
  one line each way. No End-Turn-seated frame. Battle standdown code-only (fixture-blocked):
  `ScanLensPanels.BattleEnding()`, called by both `IsActive`s.
- **THE HEADLINE PAIR — the original defect is dead.** Economy lens, Dusay focused, camera driven to
  `(-10,0,30)` (`focus [68.884,0,-22.45]` → `[-10.003,0,30.002]`): the `/gui/graph` dump comes back
  **byte-identical** and the cursor is still on Dusay.
- **Per-lens trees.** L1 Diplomacy: 4 point-bookmark rows, nothing else. L4/L8/L12: the same 9
  headings — `Cravers Leaper (AI)` (known centre north of Neurrone's, so first), `Imperials
  Neurrone`, `minor factions`, `Pirates`, `No owner`, then the 4 bookmarks; systems in reading order
  inside each; no constellations, fleets, probes, missiles, pins, markers, deposits, docks or
  wrecks. `scan:system` at 11–13 only, first in the map stop. `scan:title/lens` + `scan:zoom` and
  the legend's own rows at every rung.
- **Children + expansion in place.** Trade (Primus, L4) and Economy (Dusay, L9): planet DOTS
  (`Dusay I, Inhospitable, 2 curiosities`) + lanes, camera **identical before and after** both
  times; System lens (Sabel, L12): lanes only. Dusay's icon child: `"This is your primary system,
  keep it safe!"`. **Focus = hover**: Byrtus at Trade with `ContentTable` **not painted** (probed)
  still speaks `"Byrtus, -25, -42, group, No owner, collapsed, 11 of 11"`.
- **Scanner.** Trade + Economy `Systems → Colonizable Planets → Unexplored → explorer`; System
  `Systems → Unexplored → explorer` (slot `1 of 28` → `1 of 20`); Diplomacy, cursor on the map stop,
  `"Unexplored: all, none found"`. `galaxy.scanGoTo`: focus `[0.975,0,-4.342]` → `[4.024,0,-35.254]`,
  **`zoomStep` 7 both sides**, seated on Olvaldi. **Bookmarks**: `Heka, -1, -9, group, No owner,
  bookmark 5, collapsed` in-mode; 4 point bookmarks atop the map stop in position order.
- **Level-13 fix.** Expand at 8 → step 12: Tooltips reads `Dusay - Imperials Neurrone` / `Building
  Infinite Supermarkets, 1 turns` / `This is a faction's home system` (was 1, now 3); Heka's
  `Transvine` / `Dustciduous Trees` present; the queue child's `DevProbe.Tooltip()` at 13 is
  `shown:true, class: Constructible` with the whole panel.
- **Reload-restore**: `POST /reload` taken in-mode rebuilds the tree whole. Walks change by design
  in ONE family, `01-galaxy`: a system's Tooltips region at 13, and lane rows that named a fleet
  below the lozenge band.

## Flags and judgement calls

- **NEW PHRASE NEEDED — the ruled "Bookmarks" group is NOT shipped.** No mod word for "Bookmarks"
  as a heading exists (`galaxy.bookmark.*` are all sentences) and none was invented: point bookmarks
  are declared at the TOP level of the map stop in position order, exactly as at levels 1–2 in the
  ordinary view. One approved word closes it. Every other heading REUSES an existing wording —
  `galaxy.scanner.systems-minor-factions` ("minor factions", lowercase because that is the word as
  it exists), `icon.pirates`, the game's `%MarketplaceScreenNoOwnerTitle`,
  `galaxy.scanner.unexplored`, and `GuiEmpire.GetLeaderName` for an empire.
- **RECONCILIATION HOLE AT 1–2 (stated, as briefed)**: a focused system crossing into the Diplomacy
  band lands on the **zoom slider** — outside the tree. Those rungs declare only point bookmarks and
  none precedes the dying row in the previous order, so neither the container tier nor the
  nearest-survivor walk has anything to fall to. **5b's empire list closes it by construction**, so
  it is named rather than patched.
- **Owner headings are NOT in the stars' keys.** Re-heading every system key would make every
  descendant a new node with nothing to reconcile onto; as built, the row the cursor stands on is
  the same row either side of the mode and a dying fleet row seats on its system through the key
  path. Cost: a landing cannot open a heading the player has deliberately closed (they seed open).
- **The arrival gate governs the lens's CONTENT, not the page** — keeping the cursor across the
  change is the ruling's point, so `IsActive` does not wait and the gate sits in the panels.
  **Entering says the lens and leaving says "Galaxy"**, the pair a pushed-and-popped screen used to
  say; nothing else carries it once the page stops changing.
- The heading names an empire slightly more fully than its rows do ("Cravers Leaper (AI)" vs
  "Leaper (AI)"): the row's wording takes a colony GUID a heading has none of. Both game words.
  After a reload taken in-mode the fresh page seats on `scan:title/lens` (its `InitialFocusStop` is
  the empire stop, undeclared in the mode) — not a player path.
- **Scan-mode INSPECT was left alone** (out of scope, 5b/5c): the cell still names probes/missiles/
  pins at Trade and Economy because its filter reads `ZoomBands.MapDetail`, which the scan table has
  at Dot there. One `BandKind` question in `GalaxyInspect.Read` fixes it.

## Retirement gap, and what 5b must know

`scan:routes` — the flat trade-route group — is GONE and the weave replacing it is 5c, so on a save
with a trading company the routes are currently unread. Nothing else lost a reading: the diplomacy
walk's content is 5b's, and the node rows, planet circles and system overview moved rather than went.

`ScanLensPanels` is the home for anything the lens draws round the edges; `BuildScanTree`
(`ScanRows.cs`) is the in-mode spine and already branches on `_showsSystems`, so the Diplomacy
band's empire list slots in where that returns nothing today. `OwnerGroup` already carries the
empire and its `Known` centre, so the diplomacy reading belongs on the heading row — which is what
makes 1–2 and 3+ one continuous shape. `GalaxyHudScreen.Scanning` is the static in-mode predicate.
Exit-scan-first landings hook `GoTo`, where `EnsureBand` now returns early in-mode. The hotspots
(`ModEntry.cs`, `ModStrings.cs`, `english.json`) were NOT touched and are free.
