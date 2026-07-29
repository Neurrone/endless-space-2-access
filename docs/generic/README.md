# Screen reader game accessibility modding

Game-agnostic patterns for building screen reader accessibility mods, distilled from shipped
mods (`songs-of-conquest-access`, `wotr-access`, `tangledeep_access`, `DiscoAccess`) and from
building ES2 Access in this repository. The goal: given a new game, get from nothing to "a
blind player can operate this screen" with minimal direction — the human points at targets and
decides genuinely high-level questions; these docs supply everything else.

Prose lives in the docs; working code lives in [`src/`](src/) beside them, grouped by
subsystem — snapshots taken from the ES2 Access implementation, designed to be copied into a
new game's mod nearly verbatim (rename the `ES2Access` namespaces on copy). When the living
implementation that a snapshot came from improves, refresh the snapshot as part of the
doc-update workflow.

## Build order

Work through these in order for a new game; each milestone has an acceptance test.

| Step | Doc | Milestone (acceptance test) |
|---|---|---|
| 1 | [new-game-playbook.md](new-game-playbook.md) | Recon done; loader chosen; questions answered |
| 2 | [project-bootstrap.md](project-bootstrap.md) | Repo builds and deploys; game boots with the plugin loaded |
| 3 | [speech.md](speech.md) | Startup line spoken through a screen reader |
| 4 | [localization.md](localization.md) | Mod phrases flow through the string table |
| 5 | [dev-server.md](dev-server.md) | `/status` answers; `/speech` shows the startup line |
| 6 | [hot-reload.md](hot-reload.md) | `/reload` swaps a rebuilt mod; a broken build is refused |
| 7 | [reverse-engineering.md](reverse-engineering.md) | The game's five chokepoints identified and documented |

Then the first screen, then the navigation layer.

## Planned (written once ES2 proves them)

- `ui-navigation.md` — the accessible screen/widget/focus layer: immediate-mode tree rebuilt
  from live state, tiered focus reconciliation, ancestor-path-diff announcements, input claim
  chains. Sources: wotr-access's graph navigation engine (BCL-pure, unit-tested), SoC's
  widget layer, Tangledeep's overlay dispatcher.
- `world-navigation.md` — exploration cursor vs. categorized scanner (review cursor),
  tile-signature skip navigation, fog-of-war discipline, spatial audio cues.
- `event-narration.md` — turning engine event firehoses into coherent narration: condensation
  passes, buff-churn reconciliation, review buffers.
