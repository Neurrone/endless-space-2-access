# Planet-card unification — handoff brief

Owner ruling 2026-09-15: the three interactive planet cards are to be read by ONE reader over a
per-prefab adapter, so a gap fixed on one surface is fixed on all and "read what the game draws"
is kept by construction. This file is self-contained for a fresh session; read it, then
`CLAUDE.md`, `docs/dev-loop.md` (whole), `docs/generic/making-screens-accessible.md`,
`docs/generic/widgets.md` and `docs/planets.md` (the card facts) before touching source.

## 1. Why

Every planet-card gap found in the 2026-09-14/15 session was one page's private list of "what a
card is made of" lacking an entry another page had: the empire card said nothing about
colonization state, dropped the deposit amount's caption, never read the specialization picture,
never declared its deposit dossiers; the system page's card lacked the Ctrl+Enter technology jump
its colonize button had; the anomaly rows were controls on one page and text on two. Each was
fixed in place; the lists still drift.

## 2. Scope

Three prefabs, four surfaces:

| Prefab (decompiled/Assembly-CSharp) | Surface | Mod reader today |
|---|---|---|
| `PlanetLabel_SystemManagement` (PlanetLabel subclass) | star-system page, one card per planet | `ES2Access/Screens/SystemManagementScreen.PlanetCards.cs` (1185 lines): `AddPlanet` :69, `PlanetDossiers` :364, `AddDepositDossiers` :414, `PlanetButtons` :452, `AddCuriosities` :497, `OutpostActions` :538, `PlanetDetails` :613, `AddDepletion` :654, `AddOutpost` :675, `AddFidsi` :719, ring `PlanetRing`/`GhostRing`/`AddPopulationSlots` :857-975, drops :975-1100, Sanctuary band `AddGhost`/`GhostDetails` :216-282 |
| `PlanetCard` in `DisplayMode.Actions` AND `.Population` | empire page, the panel a status or population cell slides out | `ES2Access/Screens/EmpireScreen.PlanetCards.cs` (774 lines): `AddCard` :67, `CardDossiers` :140, `CardButtons` :180, `AddCuriosities` :211, `CardDetails` :241, `AddImprovement` :279, `AddFidsi` :305, ring `CardSlots`/`AddPopulations` :346-429, drops and the spaceport ship :452-757 |
| `PlanetLabel_SystemOrbital` | galaxy map, the planet rows under an expanded system | `ES2Access/Screens/Galaxy/PlanetRows.cs` (1236 lines): `AddPlanets` :34, `CardFor` :339, `PlanetDossiers` :398, `AddAnomalyDossiers` :442, `AddDepositDossiers` :546, `OrbitalReadout` :668, `OrbitalActions` :787, `AddCuriosities` :901, `PlanetSizeAndType` :967, `PlanetCarrier` :1011 |

Population mode is the same `PlanetCard` with the deposit, anomaly and curiosity tables and the
action buttons hidden and the ring shown (`PlanetCard.cs:243-262`): "not drawn is null" in the
adapter.

NOT in scope (owner-agreed): `PlanetLabel_SystemManagementScanView` (the lens stamp: outputs,
status mark, synergies; no buttons, tables or tooltips; read as one node in
`ES2Access/Screens/ScanViewScreen.cs:237`), `PlanetLabel_SystemDiscovery` (a cutscene GuiPanel with
authored text, read once as a line by `ES2Access/UI/DiscoveryCards.cs`), the planet page and the
population overview (not cards).

## 3. Shared pieces that already exist (keep, extend, never fork)

`ES2Access/UI/`: `PlanetCardLines.cs` (a table's drawn lines; a deposit with no drawn amount reads
its name alone), `PlanetOutputs.cs` (numbers for a colony, rating pips for an unsettled world),
`PlanetStatusText.cs` (model mirror of `PlanetLabel.RefreshPlanetStatus`, used where a prefab
draws no status label), `PopulationSummary.cs` (the "3 Niris, 2 Kalgeros" and "7 of 8
Population" buffer lines), `PopulationRings.cs` / `PopulationSlots` / `PopulationMoves.cs` (ring
rows, carry and drop arithmetic), `CardActions.cs` (buttons, including `AddAnomalies`: an anomaly
row is a button only while the game draws a live hint on it, else a readout; `CardAction.Hint` /
`HintDrawn` aim the jump at a child), `TechnologyHints.cs` (the drawn-hint predicate, with the
tooltip overload for custom-tooltip hints), `TooltipChildren.cs` (dossier children; the anchor
must be the tooltip's OWN widget), `MiningProbes.cs`, `EmpireNames.cs` (every empire name;
lint-enforced).

## 4. Rulings to preserve (each surface's readout is the oracle)

- The announcement of each surface stays byte-identical except where this brief adds content.
  System page: title, drawn status label, outpost caption; empire: name, model status word,
  type, probe line; orbital: its own readout (`OrbitalReadout`).
- Enter on a system-page card opens the planet (the planet's own click, `docs/planets.md`); the
  empire card has no click; the card itself takes NO drop on any page, drops live on the ring
  slots (owner ruling 2026-08-29).
- The Sanctuary sentence names the leader with NO faction (the game draws none). A deposit
  amount is read only where the prefab draws it (system page yes, empire no). Planet size is a
  picture on the empire card, so no size line there. No "comfortable maximum" population line.
- Anomaly rows: a readout with the dossier; a button with Ctrl+Enter and the hint line only while
  hinted.
- Pooled prefabs: gate every read on what the game draws THIS refresh (alpha and Visible per
  item, a drawn sentence, a flag set every refresh), never on a component written in one branch;
  the evidence pair must include a rebind (`docs/dev-loop.md` section 2).
- Tooltips: aim at `tooltip.AgeTransform`, never at a widget beside it (the empire planet dossier
  was mis-aimed at the picture until commit `5a66443`; the parity audit cannot see that class of
  mis-aim because `AuditModel.Covering` accepts a descendant, so prove with `DevProbe.Tooltip()`
  on the focused node).

## 5. Design

- `ES2Access/UI/PlanetCards/` (new): an adapter per prefab (an interface or a struct filled per
  prefab) exposing, each null when the prefab has no such widget or is not drawing it now:
  planet, colony, ghost colony; name label; status source (drawn label plus its tooltip, or null
  meaning use the model reader); type group; size group; gameplay-type table; anomalies table;
  curiosities table; deposits container plus whether an amount is drawn; depletion; improvement
  widget, tooltip or status label; planet tooltip owner; FIDSI enumerator plus score table; ring
  container(s) including the ghost ring; the buttons (colonize, specialization, reduce anomaly,
  terraform, rename, outpost actions, Vodyani and in-progress buttons); the card's own click; the
  drop client (planet-to-planet transfer, spaceport ship) as capabilities the page supplies.
- One reader composing announcement parts, buffer lines (details in drawn order, outputs,
  population summary), dossier children, actions and ring rows from the adapter; every part a
  `Func<string>` evaluated at read time; nothing in a build walks a subtree (`FrameSweep` for the
  card sweep, as today).
- Page residue stays on the page: keys and ids, which container is swept, drop endpoints (the
  empire page's spaceport ship, the system page's transfer), the empire panel's mode, the orbital
  row's map context (dot, unique and ghost marks, wrecks).
- Adapter differences are explicit fields, never a page test inside the reader.

## 6. Parity to deliver with it (the first fixes the shared reader lands)

- Empire card: the deposit items' class-backed dossiers as Tooltips children (the game binds
  `ResourceDepositItem.Tooltip` on every item, `ResourceDepositItem.cs` `Refresh`; today
  `CardDossiers` declares only the planet and improvement dossiers).
- Audit, then match, the remaining per-page differences the adapter makes visible: the colonize
  button's cost (the system page reads "640 Industry"; check whether the empire prefab draws
  one), the depletion line, curiosity rows, outpost actions, the in-progress juggernaut buttons
  on the orbital card. Anything the other prefab does not draw stays absent and is listed as such
  in the report.

## 7. Verification

- Before any source change: full eval-walker dumps (past the 800-line `/gui/graph` cap; the
  walker shape is in `walks/cs/rebind-walk.cs`) of the system page's `system:planet/` nodes, the
  empire page's `empire:detail` nodes in BOTH modes, and the galaxy planet rows of an expanded
  system, with buffers, every group expanded. After: the same; the diff must be exactly the
  section 6 additions.
- `sh walks/10-rebind.sh <dir>`: the planet-card, orbital-card and empire-card legs must read
  zero differing lines.
- `DevProbe.Tooltip()` focused on each dossier child of one card per surface answers
  `shown:true`.
- Stopwatch recipe per surface (system page about 0.6 ms, galaxy HUD about 0.6 ms, empire about
  2.2 ms; the empire cost is structural and shelved in `docs/roadmap.md`: do not chase it, do
  not worsen it).
- `dotnet test ES2Access.Tests/ES2Access.Tests.csproj`: the scene-walk, visibility, empire-name,
  raw-text and tooltip-text lints all gate this; regenerate an allow-list only where a lint's own
  message prescribes it; a scene-walk failure is reported to the owner, never allow-listed.

## 8. Process

Implementation stages run on Opus subagents against the live game (`docs/dev-loop.md` stage
hygiene); the owner launches the game, and a stage never quits it or loads a save; pending
notifications are never dismissed. Hotspot files (`ModEntry.cs`, `ModStrings*.cs`,
`locale/*.json`) are touched by one stage at a time. Commits are unsigned
(`git -c commit.gpgsign=false commit -F <file>`), one per stage, with the timings and the
dump-diff result in the body. Suggested split: (1) adapters and reader with the system page
migrated, byte-identical dump; (2) empire page migrated, byte-identical plus the deposit
dossiers; (3) orbital card migrated; (4) the section 6 audit of remaining differences with an
owner ruling per item.

## 9. Open questions for the owner

- Whether the system page should ALSO read its status from the model (today the drawn label)
  so all three announce through one path, at the cost of losing the drawn text as the oracle.
- Whether the orbital card's readout (its own shape, `OrbitalReadout`) should converge on the
  other two or keep its map-specific wording.
