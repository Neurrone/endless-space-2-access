# `walks/` — the regression walk

Two commands prove a change altered **no spoken or buffer line** anywhere the fixture can
reach:

```sh
sh walks/walk-all.sh /tmp/before      # with the old build loaded
# ... build, POST /reload ...
sh walks/walk-all.sh /tmp/after       # same game process, new build
sh walks/diffwalks.sh /tmp/before /tmp/after /tmp/diff.txt
```

`diffwalks.sh` prints `total differing lines: N`. **N must be 0** for "nothing changed".
Anything else is a real change to classify (§5).

The walk drives nine families of screens with `POST /input`, saves the mod's whole
accessible tree (`GET /gui/graph?buffers=1`) at ~110 stations, and adds a focused pass over
the Class-backed tooltip carriers, whose text does not exist in an unfocused dump. It works
because the dump is text and stable.

Nothing in the walk is written down about *this* save. The systems, planets, starlanes,
fleets, technologies, heroes, minor empires, notifications and registered screen keys are
all read back from the live game at the moment the walk needs them. Point it at another
save, another faction or another galaxy and it walks that one.

## Files

| File | Purpose |
|---|---|
| `walk-all.sh` | Runs the nine families into one output dir, `--reset` optionally loads a save first, prints the dump count and the skip list |
| `01-galaxy.sh` | Galaxy HUD, map tree (two systems expanded, first revisited — the pooled-row shrink leg), selected-fleet panel, scan view, map + HUD tooltip pass |
| `02-system.sh` | Star-system page for the first owned system, the second, the first again (pool shrink); planet overview; improvements and system-politics modals |
| `03-empire.sh` | Technology wheel, quest journal, empire page, economy, senate, and the government / laws / population modals |
| `04-military.sh` | Military page, fleet-selection modal, ship designer in creation mode, its hull drop list, ground-troop management, battle-tactics deck |
| `05-diplomacy.sh` | Diplomacy page and the minor-civilization window |
| `06-heroes.sh` | Academy page, hero complete list, and hero selection when the empire owns a hero |
| `07-dialogs.sh` | Pause menu, the mod's settings window (both tabs), game options, load/save, rename, journal, recipe creation, non-blocking box |
| `08-notifications.sh` | The notification popup a pending notification raises, and the HUD after the turn-log key |
| `09-bykey.sh` | Every registered mod screen dumped by key — the safety net for everything the fixture cannot open |
| `lib.sh` | Shared helpers: pausing, injecting, dumping, tooltip capture, window open/hide, discovery, type-ahead landing, skip recording |
| `cs/*.cs` | The `/eval` bodies: `tut` (minimise the tutorial), `drain` (close everything), `reset` (normalise the mod's graph state), `sysopen`, `home`, `minor`, `restore` |
| `fixture.env` | The three knobs — see §2 |
| `diffwalks.sh` | Normalised diff of two walk outputs |
| `normalize.sed` | The normalisation rules (§4) |

## 1. Preconditions

* **The game is running with the dev server on** (`devServer = true` under `[Dev]`, or
  `run-game.ps1` without `-NoDev`). `walk-all.sh` refuses to start without it.
* **In game**, on whatever save you mean to walk. The harness never loads one: `--reset` is
  the only path that does, and it is for setting up *before* a pair, never between the two
  halves of one.
* **The tutorial popup is minimised.** Expanded, it eats every injection as `unconsumed`.
  Every family script's prologue checks and does it, so this is automatic — the check is
  `cs/tut.cs`, which minimises any bound, shown `TutorialPopupPanel` whose minimize toggle
  is off and answers `tutorial already minimized` otherwise.
* **The build under test is loaded**: `dotnet build` → `POST /reload` →
  `GET /loader/status` shows `staleBuild:false` and an incremented `modAssemblyName`.
* **The game's own options modal is left on its Video tab.** It remembers its selected tab
  across opens and the walk never touches its tabs, so both halves of a pair dump whatever
  a human last selected. Only the pair matters, but Video is the state these routes were
  built in.

### The same-process constraint

**Both halves of a pair must come from the same game process.** `GraphSheet` row keys derive
from `GetHashCode()`; they survive a hot reload but not a restart, and a restart also
re-instantiates every domain object and can reshuffle hash-keyed rows. So the loop is
build → `/reload` → walk, never build → relaunch → walk.

Capturing a "before" you did not think to take: `git stash push -u -- ES2Access
ES2Access.Tests` → build → `/reload` → walk → `git stash pop` → build → `/reload` → walk.
About 3 minutes of the ~20 the pair costs. Unsafe while another stage is editing the same
trees.

## 2. Configuration (`fixture.env`)

Three knobs, and they are the whole configuration surface. Everything else is discovered.

| Knob | Default | Why it cannot be discovered |
|---|---|---|
| `WALK_HOST` | `http://127.0.0.1:8771` | It is the thing we ask. `ES2ACCESS_DEV_PORT` moves the server; a second instance needs a second port. |
| `WALK_PACE` | `100` (percent) | Scales every settle. How long a star-system page takes to draw is a property of the machine, not of any state the game can be asked for. Raise it on a slow box; too-fast shows up as a self-diff, never as an error. |
| `WALK_SAVE` | empty | `walk-all.sh --reset` only. Empty means the dev server's own default: `POST /loadsave` with an empty body loads the newest save. "Which save is the fixture" is the caller's intent, not a fact of the running game. |

Each is overridable from the environment (`WALK_PACE=150 sh walks/walk-all.sh out/`).

## 3. How discovery replaces names

Node **keys** are mod-authored and stable across saves (`hud:empire/resource/Strategic2`,
`gamemenu:mod-settings`, `system:planet/…`). Node **labels** are localized and
fixture-dependent. So the walk addresses by key and reads the label back:

```sh
snap "$TMP/hud.txt"                                   # dump where we're standing
txt=$(label_nth "$TMP/hud.txt" 'hud:empire/resource/[A-Za-z0-9]*\]' 2)
tland "$txt"                                          # land on it by type-ahead
```

`lib.sh` provides `snap`, `label_of`, `label_nth`, `key_nth`, `nkeys`, `tland` and `fact`
(an `/eval` whose result is echoed, for things no dump shows — how many systems the empire
owns, what its fleets are called).

**The caption rule.** A region's drawn caption is spoken as part of its *first* row
(`Strategic Resources, Titanium, 5, …`), but the type-ahead matches a node's **own** text
only — so the first field of a region's first row is a string type-ahead will never find.
Ask for **row 2** whenever "any row of this table" will do. Nothing in the dump marks a
field as a caption, so this is a discipline, not a parser. Where a region's first row *is*
the target (the galaxy system dossier, `…/tooltip/0`), the route lands on row 2 by text and
steps up onto it.

**Landings, not counted steps.** `ui.home` is context-relative on a tree — it goes to the
start of the current *level*, not of the stop — so a counted arrow walk is not replayable.
Every route lands with `POST /type` and clears with `ui.back`. `tland` returns non-zero on
a 0-result search and does not move the cursor; never follow a failed landing with
`ui.activate`.

**One tree shape, one cursor.** `cs/reset.cs` clears the mod's `GraphState` (`Expanded`,
`StopMemory`, `CurKey`, `KeyOrder`, `NextSuggestedMove`) before each capture, so a dump does
not depend on which branches an earlier walk left open. The galaxy legs use `dump` rather
than `capture` precisely because they *depend* on an expansion they just made.

## 4. Normalisation and the volatile classes

`diffwalks.sh` runs `normalize.sed` over both sides. Four classes vary between two runs of
the same route and would otherwise read as a disaster:

1. **Instance-hash node ids.** `GraphSheet` row keys and drop-list ids derive from
   `GetHashCode()`: `droplist:-191878/2`, `military:row-1360461824c0`,
   `empire:row-359792640c0`, `loadsave:row-1761308160c0`. Rules: `[droplist:<n>/` →
   `[droplist:#/`, `row<5+ digits>` → `row#`.
2. **The HUD wall clock** (`hud:real-time-clock`) — it changes every minute of real time.
3. **`DevProbe.Tooltip()`'s `defaultRead` array**, which *accumulates* class names for the
   life of the session: the same probe run twice can list one more class. Normalised to
   `[#]`.
4. **The focus marker `> `** — not normalised. Instead every `capture` reseats the cursor
   deterministically. A diff showing only `> ` moving means the route lost the cursor;
   read `routelog.txt`, do not blame the change.

Diagnostics are **not** diffed: `ghosts.txt` (`DevProbe.Screen()` + `Ghosts()` per screen,
plus `NotificationParity()` on the popup), `routelog.txt` (`DevProbe.Screen()` at every
labelled waypoint), `index.txt`, `skipped.txt`, `status.json`, `logs/`. They are how you
explain a diff, not part of the regression surface.

## 5. Reading a nonzero diff

Work in this order.

1. **`skipped.txt` differs between the two runs?** The fixture changed under you (a
   notification got dismissed, a save got loaded). Fix that first — `diffwalks.sh` says so
   when it happens. `MISSING IN A/B` lines mean the same thing.
2. **`routelog.txt` for the family that differs.** If the route landed on a different node,
   everything downstream is noise, not signal.
3. **Only `> ` moved** — cursor loss, see (2). **Only ids differ** — the normaliser missed a
   hash class; add the rule.
4. **A node vanished from one side and nothing replaced it** — a *gate drop*: the mod stopped
   declaring something. Check the same screen's `ghosts.txt` on both sides; a node that moved
   from declared to unpainted shows up there.
5. **A node's text changed** — a *speech change*. That is the class the walk exists to catch:
   read the line pair and decide whether it is the change you intended.
6. **Tooltip captures differ but the screen dumps do not** — a Class-backed tooltip changed.
   The unfocused dump cannot see those (empty on both sides, so they cancel), which is why
   the focused pass exists; equally, any carrier the focused pass does *not* visit is
   **unproven** by a clean diff, not proven.
7. **The camera.** The galaxy system row's "Open system" child comes and goes with camera
   distance. Both walks sample the same cursor, so it is stable — but a differ seeing it
   appear or vanish should suspect the camera, not the change.

## 6. Graceful degradation

No route fails because the fixture lacks something. It detects, skips that capture, records
the reason in `<out>/skipped.txt`, and carries on. The contract: **a skip is data, not an
error**, and the two halves of a pair must skip the *same* things — `diffwalks.sh` warns when
they do not.

| Trigger | What is skipped |
|---|---|
| No star system in the map tree | The whole galaxy map leg |
| Type-ahead cannot land on the first system | Its expansion dump and tooltip pass |
| The first system has fewer than two tooltip rows / no planet row / no starlane row | That tooltip capture |
| Fewer than two systems in the map tree | Second-system and pool-shrink map dumps |
| A HUD region has no second row | That HUD tooltip capture |
| No fleet of this empire is reachable on the map tree | The selected-fleet panel |
| The empire owns no colonized system | The whole star-system family |
| The page has no planet row *n* | That planet-card tooltip; and with no row 2, the planet-overview page |
| The empire owns one system | Second-system and pool-shrink page dumps |
| No suggested-technology row / fewer than two systems-table rows / fewer than two fleet rows | That page's tooltip capture |
| The ship designer declares no hull combo | The hull drop list |
| No minor empire with a system | The minor-civilization window |
| The empire owns no hero | Hero selection and hero inspection |
| The pause menu has no mod-settings entry | Both settings-tab dumps |
| The HUD strip holds no pending notification | The notification popup |
| `hud:turn-log` is not declared | Nothing — recorded as a finding about the fixture |
| The screen registry cannot be read out of a bogus-key refusal | The whole by-key walk |

## 7. What the walk will not do

It never advances a turn, loads a save (outside `--reset`), writes a save, dismisses a
notification, or presses Load / Save / Delete / Confirm / Apply / Create / Retrofit /
Exit Game. The negotiation modal is never opened — closing an unsigned negotiation posts an
order. The notification popup is closed by hiding its window, never through the dismiss key,
so the strip is left exactly as found. Each family's epilogue drains modals, re-minimises
the tutorial and returns the camera to galaxy overview.

After a walk, restore what a walk deliberately leaves set:
`sh -c 'curl -s -X POST --data-binary @walks/cs/restore.cs $WALK_HOST/eval'` — it nulls the
focused control and calls `DevProbe.TooltipDelay(-1)` twice (a set delay survives reloads on
purpose, and the restore cache is lost by one).

## 8. Quirks worth knowing

* **`exitwin` is not enough for every modal.** `HandleInput(InputAction.Exit)` does not
  reliably hide `LawsManagementModalWindow` or `GovernmentModalWindow`; every `exitwin` is
  paired with a `hidewin`, and `cs/drain.cs` sweeps a 39-name list as a backstop.
* **The mod's settings window remembers its tab across opens**, and a cleared `StopMemory`
  seats the cursor on the *selected* tab — so the route selects the first tab explicitly
  before dumping it, and leaves it there.
* **`POST /eval` bodies that touch `List<GameType>` poison the REPL session** for good. Every
  eval here binds game collections as `System.Collections.IList` and indexes them.
* **`pause` is a `POST /wait` with a `false` body**, not `sleep`: the Bash tool blocks
  foreground `sleep`, and the wait is evaluated per frame, which is the unit that matters.
* **Type-ahead swallows the first character of a digit-leading string** in the `search` echo
  (`1st Patriots Navy` searches as `st Patriots Navy`) — the landing is still right.
* **A capture that opens a game modal from `/eval`** sets what its opener sets, then shows
  it. Never close one with `w.HandleInput(InputAction.Exit)` on a window that was never
  properly bound — that wedges the screen stack.
