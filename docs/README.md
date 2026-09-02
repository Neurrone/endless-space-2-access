# ES2 docs — index

One line per file. Charters (what lands where) are in the repo's `CLAUDE.md`.

## The loop

| File | Holds |
|---|---|
| `dev-loop.md` | The dev server, the REPL, the verification helpers, screen-agnostic verification patterns |
| `interaction.md` | The ES2 interaction language: the layer-budget rule, claims and shadowing, and the owner rulings on keys that the code does not state |
| `roadmap.md` | Work remaining and owner rulings pending |
| `../walks/` | The fixture-agnostic regression walk (`walks/README.md`) |

## Game facts

Measured game mechanisms and the mod-policy decisions they forced; anything that turns out
generic is proposed to the owner for `docs/generic/`.

| File | Topic |
|---|---|
| `architecture.md` | The game's own layering and the GOG/Steam store divergence |
| `gui.md` | The AGE GUI framework: window lifecycle, tooltips, focus and text fields, layers and the modal stack, tables and pools, icons, cloning a window |
| `galaxy-map.md` | Galaxy labels and what an empire may know, lanes and the map's drawing, probes and targeting modes and the scan view, camera and view levels |
| `planets.md` | Planet cards, colonies and outposts, the population drag, influence and colonizability, the scanner's kinds |
| `fleets.md` | Fleets and movement, pathfinding and interception, the fleet panel, selection and ship transfer |
| `research.md` | The technology wheel, and the construction and research queues |
| `empire-screens.md` | The icon-strip screens: senate, empire, economy, politics |
| `military.md` | The military screen, the ship designer, and battles |
| `heroes-and-diplomacy.md` | Heroes and the academy, diplomacy and the sweep |
| `notifications.md` | The notification pipeline and its events, popups, show-location, quests and the journal, the tutorial popup, endings, the cutscene videos |
| `install.md` | Store and DLC gating, the game's logger, the Mono runtime under the REPL, the out-game pages and the lobby, chat |
| `saves.md` | The save system: the campaign GUID and its lifecycle, save descriptors and titles, loading one named file |

## Generic

| Folder | Holds |
|---|---|
| `generic/` | The game-agnostic accessibility-modding documentation and its `src/` mirror — the repo's primary deliverable. Changes only when the owner asks |
