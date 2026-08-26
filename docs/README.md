# ES2 docs — index and charters

One line per file: what it holds, and therefore where new content of that kind lands.

## The loop

| File | Charter |
|---|---|
| `dev-loop.md` | ONLY the loop: dev-server routes, the verification helpers, REPL gotchas, screen-agnostic verification patterns. Stays under ~300 lines |
| `interaction.md` | The ES2 interaction language — layer numbers, the key map, claim rules. Every new layer or key binding lands here |
| `roadmap.md` | Work remaining, owner rulings pending, and a pointer index of what shipped. Screen-status changes and future-feature prep land here |
| `test-recipes/` | Per-screen recipes and fixture limits, one file per screen family — see `test-recipes/README.md` |

## Game facts

A game-mechanism fact lands in the topic file that fits; anything that turns out generic is
proposed to the owner for `docs/generic/`.

| File | Topic |
|---|---|
| `architecture.md` | The game's own layering and the GOG/Steam store divergence |
| `gui.md` | The AGE GUI framework: types, window lifecycle, tooltips, focus and text fields, layers and the modal stack, tables and pools, icons, options, cloning a window |
| `galaxy-map.md` | Galaxy labels and what an empire may know, lanes and the map's drawing, probes and targeting modes and the scan view, camera and view levels |
| `planets.md` | Planet cards, colonies and outposts, the population drag, the system-selection window, influence and colonizability, the scanner's kinds |
| `fleets.md` | Fleets and movement, pathfinding and interception, the fleet panel, selection and ship transfer, the targeting-cancel fleet swap |
| `research.md` | The technology wheel, and the construction and research queues |
| `empire-screens.md` | The icon-strip screens: senate, empire, economy, politics |
| `military.md` | The military screen, the ship designer, and battles |
| `heroes-and-diplomacy.md` | Heroes and the academy, diplomacy and the sweep |
| `notifications.md` | The notification pipeline and its events, popups, show-location, quests and the journal, the tutorial popup, endings |
| `install.md` | Store and DLC gating, the game's logger, the Mono runtime under the REPL, the out-game pages and the lobby, chat |

## Research

| File | Topic |
|---|---|
| `census-screens.md` | The window census: every GUI class, one verdict each |
| `audit-dlc-mechanics.md` | DLC and expansion mechanics, non-faction |
| `audit-endings-intro.md` | Game-end surfaces and the intro faction video |
| `audit-factions.md` | Faction-specific accessibility surface |
| `audit-multiplayer.md` | Multiplayer surfaces, coverage and a stage plan |
| `audit-numeric-labels.md` | Nodes whose spoken text can be a bare number |

## Generic

| Folder | Charter |
|---|---|
| `generic/` | The game-agnostic accessibility-modding documentation and its `src/` mirror — the repo's primary deliverable. Changes only when the owner asks |
