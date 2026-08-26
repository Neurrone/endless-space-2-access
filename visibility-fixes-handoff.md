# Visibility fixes — handoff

Written 2026-08-27 for a fresh session to execute autonomously. Dickson approved the
direction (central unpainted-node gate + one blessed child-enumeration helper + regression
measurement) on 2026-08-27. This file is the brief; the repo's process docs still govern HOW
work runs: read `docs/dev-loop.md` before touching the live game, and `CLAUDE.md`'s
Delegation section before spawning stages. All file:line cites below were verified
2026-08-26/27 — re-verify any cite before editing around it; several files changed the same
day.

## 1. Motivation — the bug class this closes

The AGE engine pools table rows: a table filled with `ReserveChildren` +
`RefreshChildrenIList` never shrinks. Retired surplus rows keep `Visible == true` and are
faded to `Alpha == 0` (`decompiled/Assembly-CSharp-firstpass/AgeTransform.cs:2382-2412`,
quoted in `AgeWidgets.Painted`'s doc comment). Any mod walk that gates on `Visible` therefore
declares ghost nodes or reads stale text from rows the player cannot see — and the ghosts
are navigation-history-dependent (the pool must grow on one binding, then shrink on the
next), so they never show up in a fresh-state scan, only in play.

Four instances were found and fixed one at a time in the 2026-08-26 session (commit hashes
in §9):

1. **Side-panel population circles** — two alpha-0 rows from a previously viewed system
   announced as nameless "button, 2" / "button, 1". Fix: `SidePanels.Collect` /
   `HasGroupChild` child gates switched from `Visible` to `Paints` (entry gate stays
   `Visible`); `DevProbe.Ghosts()` (`ES2Access/Dev/GhostAudit.cs`) added as the
   declared-but-unpainted audit.
2. **Planet-card info tables and deposits** — bare "3"/"2" buffer lines (deposit captions
   live on the tooltip wrapper, and are now read from it) and `AddWidgetLines` child reads
   gated on `Paints`; deposit dossiers added to `PlanetDossiers`.
3. **Curiosities table** — a retired curiosity item declared as a nameless unavailable
   button. Fix: `AddCuriosities` gate `Visible` → `Painted`, matching `AddAnomalyHints`.
4. **Outpost actions strip** — `OutpostActionsTable` walk had NO per-item gate at all; a
   card following one with more actions would declare surplus ticks wearing the other
   outpost's action names. Fix: `Painted` per item.

Each fix was one line plus verification, but the discovery cost was a user bug report per
instance. A read-only research pass (2026-08-27) sized the remaining exposure: **43 of the
62 files that declare graph nodes directly contain no alpha/paint test at all** (including
`UI/Cells.cs`, `UI/CardActions.cs`, `Core/UI/GraphSheet.cs`, and 32 screens), and the
codebase uses four different visibility tests that disagree. Dickson's ruling: stop fixing
walks individually; take the existence decision away from walks entirely.

## 2. The four tests, and why one test everywhere is impossible

| Test | Semantics | Correct for | Cite |
|---|---|---|---|
| `AgeWidgets.Visible` | ancestry walk of `Visible` flags, ignores alpha | screen/panel ROOTS only | `UI/AgeWidgets.cs:38` |
| `AgeWidgets.Paints` | one step: `Visible && Alpha > 0` (the renderer's own early-out; subtree pruning comes from recursion) | CHILDREN inside a trusted root — the pooled-row case | `UI/AgeWidgets.cs:105-119` |
| `AgeWidgets.Painted` | ancestry + alpha | a LEAF asked in isolation (ancestor might itself be a retired block) | `UI/AgeWidgets.cs:77` |
| `TableSheet.Lines` arranger rule | `Visible && (StrictVisibility \|\| Alpha > 0)` — exempts strict tables | UNRESOLVED — conflicts with `Paints`'s doc ("strict visibility is no exemption for the renderer"); §5 item 5 settles it with pixels | `UI/TableSheet.cs:1343-1364` |

The root-vs-child split is measured, not stylistic: the game fades a WINDOW ROOT in on
arrival while every child stays at alpha 1 (`Screens/NotificationScreen.cs:4985-4986`,
measured). An alpha-aware test at the root blanks the whole screen for the length of every
arrival animation; an alpha-blind test at a child admits every pooled ghost. So the answer
to "same test everywhere" is: same test **per role**, chosen once in shared code — walks
stop making the choice at all (§3).

Content gates are a different question from existence and all stay: `AgeWidgets.ItemText`'s
alpha check (`UI/AgeWidgets.cs:1529`), `PaintedText`/`PaintedLines` (`:1456`, `:1379`) and
their callers, `TableSheet`'s deep-cell painted-only pass (`UI/TableSheet.cs:1176-1189`),
`ScanViewScreen`'s label readers. Availability gates (`CardActions.AddRefusable`'s
deliberate `Visible`-only — a refusing control is drawn) also stay.

## 3. The approved design

### 3a. Central gate

A drop predicate applied in `GraphBuilder.MakeNode`
(`ES2Access/Core/UI/Graph/GraphBuilder.cs:381-397`). `Core/` is BCL-only (build-enforced),
so the builder takes an injected predicate over the `object`-typed handles nodes already
carry; the engine-side resolver is supplied from `GraphNavigator`'s three `new GraphBuilder`
sites (`UI/GraphNavigator.cs:195` InspectRender, `:1039` BuildRender, `:1903` DeepRender —
the only production construction sites; tests construct their own).

- **Carrier resolution** must be ONE shared engine-side helper, factored out of
  `NotificationAudit.Carrier` (`Dev/NotificationAudit.cs:889-903`, `WidgetOf` `:1381-1391`,
  `AimOf` `:861-873`) so the gate and `Ghosts()` can never disagree: `ControlId.Reference`
  as widget/component first, else the `PointsAt` tooltip's `AgeTransform`. **Never read
  `ScrollAnchor`** — model-keyed nodes (map places, `GraphSheet` rows keyed by domain
  objects) resolve carrier-less and MUST pass ungated, exactly as the audit counts them
  `synthetic` today (`Dev/GhostAudit.cs:118-123`).
- **Test: `Paints` on the carrier alone** (one step), never `Painted` — renders rebuild
  EVERY frame while a screen is focused (`ModEntry.cs:1151` → `ScreenManager.Tick` →
  `EnsureFocus` → `KeyGraph.Rerender` → `BuildRender`), ~49 screens gate activity on
  `window.Shown` and can build mid-fade, and five span fades deliberately
  (`ImprovementsModalScreen.cs:73`, `PlanetConstructiblesScreen.cs:81,342`,
  `ElectionScreen.cs:160`, `HeroSelectionScreen.cs:144`, `NotificationScreen.cs:197`).
- **Row/group safety**: dropping a row's last cell today throws `"Row cannot be empty"`
  (`GraphBuilder.EndRow:311-319`), which `BuildRender` catches by blanking the whole render
  (`UI/GraphNavigator.cs:1044-1048`). The gate must suppress the way `AddItem`'s existing
  suppression works (`GraphBuilder.cs:324`), and a dropped `BeginGroup` header must
  suppress its subtree (`:246-251` is only coherent for the suppressed case).
- **Audit bypass**: `InspectRender` must build UNGATED (or `Ghosts()` rewritten to compare
  gated vs ungated) — otherwise the audit goes permanently clean and stops detecting.
- **Telemetry — this is the standing tripwire Dickson asked for**: every drop the gate
  makes is logged (screen key, node key, carrier path, why), deduped per screen+key so
  per-frame rebuilds don't spam. Future leaks surface in `GET /log?grep=` instead of user
  bug reports. Keep the log line greppable and stable.
- **ScratchTooltips carriers are safe under the gate** — verified: a scratch carrier is
  parented outside the widget tree, `Parent` stays null, defaults `visible=true`/`alpha=1`
  (`UI/ScratchTooltips.cs:222-263`; `firstpass/AgeTransform.cs:3204-3212`, `:1829-1836`,
  `:144`, `:153`), so `Paints` answers true. Carrier-aimed node sites to re-verify live:
  `Screens/GalaxyHudScreen.cs:4232,5670,5756,5900,6883`,
  `Screens/SystemManagementScreen.cs:1543,1581,1613`.

### 3b. Consolidation (the "one test" half)

- Add ONE blessed child-enumeration helper to `AgeWidgets` (shape it during
  implementation: an indexed iteration that applies `Paints` per child internally —
  remember the REPL/`foreach` generic poison applies only to eval, but shipped code should
  still iterate by index per existing convention). Migrate the hand-rolled
  `for (…Children…)` + `Visible` walks onto it.
- Remove the walk-level gates the central gate makes redundant (verified list, cites as of
  2026-08-27): `SidePanels.Collect` child gates (`UI/SidePanels.cs:316,392,400,453,465`),
  `TooltipChildren.AddPlain`/`AddPlainInside` (`:320,:366`),
  `GlobalHud.AddPendingNotifications` (`:2449`), `SystemManagementScreen`
  curiosity/outpost/deposit gates (`:690,:763,:814,:861`), `ElectionScreen`
  (`:687,:739,:1125,:1135`), `GalaxyHudScreen`
  (`:1140,:4202,:5634,:5656,:5711,:5733,:6204,:6778`), `LawsScreen` (`:420,:429`),
  `MinorFactionDiplomacyScreen` (`:289,:304,:572`), `PopulationScreen` (`:379,:633`),
  `PlanetOverviewScreen` (`:569,:583`), `DLCScreen:247`, `ModdingConfigScreen`
  (`:195,:356`), `ResourcesExportModScreen` (`:337,:453,:484,:593`), `SenateScreen:236`,
  `LawCards:123`, `HeroInspectionScreen:1878`. Removal is the LAST step, after the gate is
  proven — each removal is a behavior no-op only if the gate really covers that node's
  existence; where a gate also affects buffer/content lines, split it, don't delete it.
- One caution from instance 4's sweep: the population/spaceport marker walks correctly use
  `Visible` because `PopulationEnumerator.HideAllPopulationMarkers` (`decompiled:…:228`)
  retires by `Visible = false`, not alpha — the helper handles that fine (`Paints` includes
  the `Visible` check), but don't "fix" walks whose current test is already right for a
  different retirement style; just migrate them.
- Rewrite `AgeWidgets.Visible`'s doc comment to say roots only, pointing at the helper for
  children.

## 4. Hazards the implementation must respect

1. **Fades** — covered by `Paints`-one-step (§3a). Confirm with measurement item 1 before
   trusting it beyond the notification popup.
2. **Cursor flicker** — a node vanishing for one frame moves the cursor permanently and
   re-announces: `KeyGraph.Reconcile` (`Core/UI/Graph/KeyGraph.cs:82-148`) tier 3 seats the
   nearest survivor and nothing pulls the cursor back when the node returns. Whole-render
   disappearance is safe (`Rerender` returns false on an empty build, `:66-76`). Whether
   any alpha-pulsing widget (repeating `AgeModifierAlpha`) actually reaches 0 and carries a
   node is prefab data — measurement item 3 is the only way to know.
3. **The `StrictVisibility` conflict** (§2 table, last row) — measurement item 5 decides
   which rule is wrong; do not resolve it by reasoning. If the resolution changes any
   spoken/buffer line on a table screen, report it to Dickson before shipping that part.
4. **Empty-row / group-header throws** — §3a; after the gate is in, item 7 sweeps for the
   silent blank-screen failure.

## 5. Live measurement battery (run before flipping the gate on)

Build the gate behind a flag first; run these with the dev-loop tooling (`DevProbe.Trace`
via `POST /wait` for per-frame recording; stage-hygiene rules apply).

1. **Arrival-fade shape per screen family.** For the `Shown`-gated and `Operable`-gated
   screens: trace an open, recording window-root `Alpha`/`ModifiersRunning` vs a
   representative child's `Alpha` per frame. Confirms the
   `NotificationScreen.cs:4985` shape (root animates, children stay 1) generalizes — the
   design's safety rests on it.
2. **Empty-render frames under the gate.** For the five fade-spanning screens (§3a list):
   trace declared-node count across an open with the gate flagged on; confirm the cursor
   returns to the same node with no re-announcement (`_lastSpokenKey` unchanged).
3. **Pulsing widgets.** `/eval` sweep of focused screens' trees for `AgeModifierAlpha`
   with `Repeating == true` (print Start/End alpha, duration, and whether the widget
   carries a declared node); `POST /wait` on `widget.Alpha <= 0f` for ~5 s where found.
4. **ScratchTooltips-aimed nodes.** Galaxy map at a zoom where deposit/anomaly icons are
   culled: `DevProbe.Ghosts()` must show those dossier nodes located-and-painted, and the
   gate must not drop them.
5. **`StrictVisibility` pixels.** Find a live strict table with faded surplus rows (print
   `StrictVisibility` + per-child `Visible`/`Alpha` for the systems/fleets/controls
   tables); `crop-shot.ps1` the row rects. The pixels decide `TableSheet.Lines` vs
   `Paints`.
6. **Sheet cells.** Confirm no `GraphSheet` row cell is dropped that a crop shows drawn
   (they are carrier-less under the resolver rule and must pass; this item is the proof).
7. **Throw sweep.** Walk every screen family with `POST /input`; `GET /log?grep=` for
   `Build threw` — an `EndRow` or duplicate-id throw blanks a screen silently.
8. **Full regression diff.** The `docs/dev-loop.md` §2 route "Proving a refactor changed no
   spoken or buffer line": per-family `GET /gui/graph?buffers=1` into `before/`, identical
   walk into `after/`, diff with instance-hash ids normalised — plus the focused second
   pass over Class-backed tooltip nodes, which the unfocused diff cannot prove. The
   before-capture MUST be taken from the pre-gate build (the `git stash` loop in the same
   §2 pattern, single session for any sheet baseline).

Also rerun `DevProbe.Ghosts()` on each visited screen (expect 0), and `dotnet test`
(baseline 2026-08-26: 926 passed).

## 6. Fixtures and game state

`.\run-game.ps1 -NoSpeech -NoWait -LoadSave "[Beginner] test"` boots to in-game;
fixture inventory is `docs/test-recipes/fixtures.md`. NEVER create or advance saves —
report "verification blocked on game state" instead. Minimize the tutorial popup first.
Quit the game (`POST /quit`, poll the process) when live work is done. Ghost-node repro,
if needed for the audit's own before/after: view a system whose side panel/planet cards
held MORE rows, then a smaller one (the 2026-08-26 save's Dusay → Ita → Heka route did
exactly this; ghosts exist only after a pool shrinks).

## 7. What needs Dickson vs what does not

Already approved: the gate design (§3a), the consolidation (§3b), removing the redundant
walk gates, the drop log. Needs Dickson before shipping: any spoken/buffer line that the
measurements show CHANGING (esp. the `StrictVisibility` resolution), any new key binding
(none expected), anything a measurement contradicts in this file — report the contradiction,
don't silently adapt. Do not update `docs/generic/` (owner-gated). Chartered doc homes for
outputs: new helper contracts in their own doc comments (+ one-line `docs/dev-loop.md` §1
row for dev-facing ones), mechanism findings in the `docs/` topic file that fits,
screen-status changes in `docs/roadmap.md`.

## 8. Definition of done

- Gate on by default, flag removed or defaulted on; drop log in place and quiet across a
  full screen-family walk except for genuine ghosts.
- The measurement battery ran; each item's outcome recorded (a finding, or "clean") in the
  chartered docs; contradictions reported.
- Redundant walk gates removed; child walks on the shared helper; `Visible` doc says roots
  only.
- §5 item 8 diff shows no spoken/buffer regression on any walked family.
- `Ghosts()` clean everywhere visited; `dotnet test` green; hot reload verified
  (`modAssemblyName` incremented) after every build interpreted.

## 9. The commits behind §1

- `5c9a029` — "The side panels read only what paints, and Ghosts() audits what does not":
  §1 instance 1 plus the `DevProbe.Ghosts()` audit. The minimal exemplar of the class and
  its walk-level fix shape (entry gate `Visible`, child gate `Paints`).
- `5d57343` — "A planet card reads its ring as slots, and names what its icons only draw":
  §1 instances 2–4 (deposit captions/dossiers, info-table gates, curiosity ghost, ungated
  outpost-actions strip) plus the slot-rows feature the ghosts were discovered during.
  `git show 5d57343 -- ES2Access/Screens/SystemManagementScreen.cs` shows several
  walk-level gates this handoff's central gate will make redundant.
- `d7cda6e` — "Space belongs to the management screen": unrelated to visibility, but it
  touched the same screen the same day; diff against it, not around it, when re-verifying
  §3b's cites in `SystemManagementScreen.cs`.
