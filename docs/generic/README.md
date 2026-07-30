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
| 8 | [ui-navigation.md](ui-navigation.md) | First screen keyboard-navigable; announcements verified via `/speech`, then by the user |
| 9 | [input.md](input.md) | Mod keys work everywhere; the game's colliding bindings enumerated and suppressed (mod keys win, Escape delegated) |
| 10 | [buffers.md](buffers.md) + [tooltips.md](tooltips.md) | Focused element's details reviewable line by line; the short/long tooltip rule wired once |
| 11 | [widgets.md](widgets.md) | A full settings-style screen operable: value widgets adjust and announce, popups open as sub-screens, the shared confirmation dialog speaks |
| 12 | [icons-and-symbols.md](icons-and-symbols.md) | Inline icons named from the enumerated table; no bare numbers or dropped nouns in spoken text |

From step 8 onward, every screen runs through
[making-screens-accessible.md](making-screens-accessible.md) — the per-screen loop: measure,
propose the model, get approval, implement, verify with evidence, hand over the manual test.

Cross-cutting, read alongside any feature work: [performance.md](performance.md) — keeping
per-frame cost invisible (no scene scans, snapshot+reconcile, allocation discipline).

## Planned (written once ES2 proves them)

- `world-navigation.md` — exploration cursor vs. categorized scanner (review cursor),
  tile-signature skip navigation, fog-of-war discipline, spatial audio cues.
- `event-narration.md` — turning engine event firehoses into coherent narration: condensation
  passes, buff-churn reconciliation, event-log review buffers (the second sink pattern in
  [buffers.md](buffers.md)).
- `settings.md` — user-facing settings and key rebinding. Adopt wotr-access's declarative
  settings tree: one declaration serves as persistence, the settings screen's data source,
  and the rebinding UI.
- `distribution.md` — release templates, a tester channel via committed build artifacts kept
  fresh by a pre-commit hook (wotr-access), and accessible installers — both SoC and
  wotr-access independently landed on Rust installers after antivirus false-positives killed
  other packagers.
