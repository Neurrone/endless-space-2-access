# Stage 4 — the label's pictures as child nodes (+ two stage-3 corrections)

Branch `scan-modes`, 5 commits, build 0 warnings, `dotnet test` 1187/1187 green, verified live on
`ES2Access-r13`…`r16` against `[Beginner] access test`. Game RUNNING, camera home (`focus [68.861, 0,
-22.453]`, step 8), cursor on `hud:empire/screen/EmpireScreen`, cell dismissed, Dusay and Heka
re-collapsed, both constellations expanded, no field focused, `TooltipDelay` at 0 — which is what the
session FOUND (`was:0` on the first call, before anything was set). `docs/generic/` untouched; no
ModStrings key, locale string or `ModEntry` change; no new static state.

1. `296cfb3` **the conversion.** `SystemLabelReadout.IconsAboveDeposits`/`IconsBelowDeposits` collect
   one `TooltipChildren.Dossier` per drawn picture; `SystemRows.SystemDossiers` calls them around the
   existing per-kind deposit collection, so the "Tooltips" region reads in the label's own order.
   `Picture()` is the one door, trying three tooltip kinds in turn: wrapper-named
   (`TooltipChildren.Add`), the game's own sentence (`AddPlain`), and — new — a renderer-assembled
   tooltip whose target is no wrapper (`Assembled`; the invasion icon's ground battle), named off the
   sentence the game wrote into it. `Lines()` loses every converted kind.
2. `a45b9fc` **the constellation bonus** — `ConstellationRows.ConstellationBonus`, a value part gated
   on `!_showsSystems`.
3. `2f0e1b1` **the border leap withdrawn** (a straight revert of `a6e4761`).
4. `a93f1d5` **the inspect cell band-filtered** in `GalaxyInspect.Read`, the one gathering.
5. `2b319ab` **doc landings** — `test-recipes/galaxy-map.md`, `test-recipes/inspect-and-influence.md`,
   `interaction.md`, `roadmap.md`.

## Per-kind coverage

- **Drawn-verified**: construction queue (Dusay), the strip's home icon (Dusay), main deposits (Heka).
- **Code-only, prefab-measured**: the 12 contextual icons and given-to-academy (7 carry a `%key`
  unbound, 5 the game fills at bind), the other 10 standing-strip icons (marketplace, academy,
  trading company, decaying, honor bonuses/defense, golden age, juggernaut citadel, metaplot battle
  rules, latent hacking beacon), the empty-queue cross.
- **Code-only, no instance at all**: secondary deposits, haunt circles, exploration-winner badges,
  king-of-the-hill rows, the rebellion pair, the traitor count.
- **No forced-show ladder was run.** Its tier-0 rung — read the prefab's fields off the unshown
  widget — answered the question on Dusay's own hidden widgets (class, target, content, per icon),
  and the forcing tiers prove STRUCTURE on widgets whose content the game only writes at bind. A
  deliberate substitution, not a skipped gate: those kinds ship unsighted.

## Evidence

- **Queue child, the headline fix.** Route: `ui.focusMap` · `/type "dusay"` · `ui.back` · `ui.right`
  · `SetZoomHere(10)` · `ui.regionNext` · `ui.down`. Focused `DevProbe.Tooltip()` → `class:
  Constructible`: `Infinite Supermarkets` / `System Improvement (Approval)` / description / `Effects:
  +10 Approval` / `Cost: 280 Industry (1 Turn)` / `Political impact: Ecologists` / `Upkeep: 8
  Upkeep`. The focused `/gui/graph?buffers=1` holds all of it under `…/system/535/tooltip/1`, the
  name deduped out of the head. `…/tooltip/2` → `PanelFeatureSimple`, "This is a faction's home
  system". Heka's deposits: `Transvine, exploited` · `Dustciduous Trees, exploited`.
- **Row buffer before → after** (Dusay, focused re-read, so the cross-section dedupe caveat is
  covered). Before: `5 population · Building Infinite Supermarkets, 1 turns · This is a faction's
  home system · 1 friendly ship · Influence radius …`. After: `5 population · 1 friendly ship ·
  Influence radius …` — the two lines are gone and say themselves once each, on their own nodes.
- **`Coverage()` before → after** (a system expanded): `QueuedConstructionGroup` GONE,
  `HomeSystemIconGroup` GONE, `ResourceDepositItem000/001` (3 each) GONE, `tooltipsUncovered` 18 →
  14, no new finding in any bucket. CAVEAT: the runs sit at different camera framings (Dusay vs Heka
  expanded), which is what accounts for several planet/colonize findings on one side only.
- **`TooltipParity()` before → after**: byte-identical — one pre-existing `promised`
  (`hud:empire/research`), one `unknown` (`galaxy:probe/1868`), every other bucket empty.
- **Constellation bonus**: at level 2 the rows read `Corvus, group, +15% Industry` / `Serpens, group,
  +15% Food`; at 3 both are silent about it. The "the tooltip already carries it" claim is VERIFIED,
  not assumed: focused `DevProbe.Tooltip()` on Serpens at level 9 → `class: Constellation`, lines
  including `Constellation control bonus:` / `+15% Food`.
- **Inspect corrections**: per-level cell table and travel-key pairs in
  `test-recipes/inspect-and-influence.md` — same square at 9/7/4/3/1 (fleet drops below 5, lanes
  below 3, bookmark at 1); empty-square Alt+Left and Alt+Right both silent and unmoved; a one-lane
  cell still travels to Talitha.

Walks change by design in two families: the map tree (a system's Tooltips region gains rows, its
buffer loses the converted lines) and the constellation rows at levels 1–2.

## Judgement calls

- **The rebellion is TWO children plus a KEPT buffer line.** The group carries no tooltip; its ring
  and countdown carry one sentence each, and the mod's line is the two NUMBERS (a gauge angle, a turn
  label) that no tooltip holds. Both sentences became nodes; "Rebellion at n percent, m to go" stayed.
- **The traitor count is said twice by design**: the row keeps its spoken "n sleepers" (the brief
  protects short drawn numbers) and the child is named the same way, because that is what the mod
  calls the icon — the same shape as the star dossier repeating the system's name.
- **`MinorRelationPraiseGroup`/`MinorRelationQuestStartedGroup` were NOT converted** — tooltip-bearing
  pictures, but off the ruled list and on the population line rather than in the strip. One line each.
- **Drawn means PAINTED, not Visible** (the old reader ignored alpha), which is what makes the
  level-13 behaviour below true and also stops a few icons being read off a faded label.

## What stage 5 must know

- **The one open collision** (roadmap): the label's body lines are painted only to spoken level 12
  (measured: `PopulationAndQueueLine`, `HomeAndTradingLine`, `DepositsMainLine` at alpha 0 at camera
  step 12, alpha 1 at step 11), so the picture nodes vanish at 13 — and stage 2's graded expansion
  lands an expansion from 7–12 at 13. Route to them: expand, then zoom back out with the branch open.
  Parity with the picture was the tie-break against stage 2's flagged "the 7–12-only information
  stays in the row at 13", which this stage took OUT of the row. If convenience should win the fix is
  a hold on the label's lines while the branch is open (`ConstellationLabelHold` is the shape, but it
  forces CULLING and this is an alpha animation) — unruled.
- **A snap landing on a Tooltips child forces level 13** (`EnsureBand`), which is where the pictures
  are not drawn, so type-ahead is not a route to them and any scan landing at a dossier child inherits it.
- **`SystemLabelReadout.Picture` is the door for the scan label's own icons**: hand it the widget and,
  where the mod has its own words, a name func. It handles all three tooltip kinds and dedupes.
- **`GalaxyInspect.Read` is now the only place the cell's band filter lives**, in `BandKind`
  vocabulary — the scan ladder's rows will move it with no second edit.
