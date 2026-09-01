# ES2 per-screen test recipes — index

How to work each screen family against the live game without damaging the owner's fixture:
openers, safe round trips, reversibility probes, and what each fixture cannot show. The
recipes live in the sibling files below — grep the one whose family you are touching. The
screen-agnostic verification patterns (evidence crops, tooltip audits, silence rules,
etiquette) stay in `docs/dev-loop.md`; `docs/roadmap.md` holds only work remaining plus a
pointer index of shipped screens. A new per-screen recipe or fixture limit lands in the
sibling file for that family, never here.

## The three shared fixture facts

1. **Three saves exist.** `[Beginner] test` (the working fixture, currently turn 21),
   `[Midgame] quests fleets` (fleets in orbit, multi-page tutorials, two idle fleets in one
   berth) and `unlocked` (every screen unlocked, the technologies not, turn 1 — which is why
   so many screens read structurally right and content-poor). A recipe that says "this save"
   without naming one means `unlocked`.
2. **Fixtures are the owner's.** Never create or advance one; a stage blocked on game state
   reports the block. Every probe undoes itself in the same `/eval`, or ends with
   `POST /loadsave`.
3. **Every galaxy session starts by minimizing the tutorial popup**, and tutorial progress
   does NOT live in the save — anything that advances a page is a fixture change that only a
   re-minimize puts back. The one telling of that recipe is in `fixtures.md`.

## The families

| File | What is in it |
|---|---|
| `fixtures.md` | The three saves, the tools that undo a probe, the tutorial-minimize recipe, the modal-stack drain and the full-screen close, the offline proofs, how to read a usage hint |
| `notifications.md` | Popups, the HUD strip, the turn log, the parity probe, Ctrl+L, the two dismiss-alls |
| `battles.md` | The space-battle setup popup, the battle plan chooser, the ADVANCED setup window (plans, stats pager, arena, ship lock and flotilla carry), the ADVANCED report window (balance, tactics, flat phase list, morale, flotilla cards), the ground-battle popups |
| `galaxy-map.md` | The page's screen model (tree shape, the four panels, the HUD empire bands, the turn log stop, quest markers), fog and labels, the camera rule, locates, the scan view, type-ahead, dossiers and tooltips, the tree/place/page keys, usage hints |
| `fleets.md` | Fleet rows in the tree, foreign fleets, ordering and re-routing, targeting modes, the route-loss watcher, the selection chords and drag, the selected-fleet panel |
| `inspect-and-influence.md` | The inspect cell cursor and everything influence-shaped |
| `scanner.md` | The scanner's tiers, categories, oracles, Alt+Home and custom categories |
| `systems-and-planets.md` | The star-system page, the management round trip, orbital and planet cards, population, outposts |
| `empire-screens.md` | Research, quests, senate, economy, military, ship designer, heroes, tables |
| `modals-and-outgame.md` | Opening modals from `/eval`, the election wizard, minor civilizations, the pirate window, the cutscene modal, DLC, the out-game family, chat |
| `mod-settings.md` | The mod's own settings window: key bindings, the Scanner tab, the physical key paths |

Each sibling ends with a `## Fixture-blocked` section gathering that family's blocked items.
Where a spoken usage hint can be heard is in the recipe file of the screen that draws it.
