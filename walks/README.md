# `walks/` — the regression walk

A walk records what the mod would say for a screen — the whole accessible tree it declares,
every node's spoken line and review-buffer lines — as text, so two builds can be compared by
diff. It does not press every control or test behavior; it proves that a change altered no
spoken or buffer line except the ones it meant to.

```sh
sh walks/run.sh /tmp/before senate/laws-modal.sh   # one scenario, with the old build loaded
# ... build, POST /reload ...
sh walks/run.sh /tmp/after senate/laws-modal.sh    # same game process, new build
sh walks/diffwalks.sh /tmp/before /tmp/after /tmp/diff.txt
```

`diffwalks.sh` prints `total differing lines: N`. **N must be 0** for "nothing changed".
Anything else is a real change to classify (§5).

A walk is **sized to the change**: a target is one scenario file, a folder (every scenario of
that screen), or several of either. `walk-all.sh` is the one entry point that walks
everything, and CLAUDE.md gates running it on the owner's approval. Nothing here is written
down about *this* save: systems, planets, fleets, technologies, heroes, minor empires,
notifications and screen keys are all read back from the live game at the moment the walk
needs them.

## Layout

One folder per major screen; one script per screen or modal. A scenario over a surface the
game pools also runs that surface's A-B-A rebind check (§9), so the check cannot be forgotten.

| Folder | Scenarios |
|---|---|
| `galaxy/` | `hud` (overview dump, two HUD tooltip probes); `map-tree` (a system expanded, its tooltip probes, a second expanded, the first revisited; orbital planet-card rebind); `fleet-panel` (selected-fleet panel; fleet-action rebind); `scan-view` |
| `system-management/` | `screen` (the largest owned system's page, two planet-card tooltip probes, the page turned to the smallest and back; planet-card rebind); `planet-overview` (Enter on a planet row); `improvements-modal`; `politics-modal` |
| `research/` | `screen` (the wheel, a suggested-technology tooltip) |
| `quests/` | `screen` |
| `empire/` | `screen` (a systems-table cell tooltip); `planet-cards` (the cards panel in Actions and Population mode; empire planet-card rebind) |
| `economy/` | `screen`; `marketplace` (section radios and items; radio rebind); `recipe-creation-modal` |
| `senate/` | `screen`; `government-modal`; `laws-modal`; `population-modal` |
| `military/` | `screen` (a fleet-row tooltip); `fleet-selection-modal`; `ship-designer` (creation mode, then its hull drop list); `troop-management-modal`; `battle-tactics-modal` |
| `diplomacy/` | `screen` (a card tooltip; ring-wedge rebind by hover selection); `minor-civilization-modal` |
| `heroes/` | `academy`; `hero-list-modal`; `hero-selection-modal` (only with a hero) |
| `notifications/` | `popup` (only with a pending notification; parity probe; popup-body rebind); `turn-log` |
| `game-menu/` | `pause-menu`; `mod-settings` (both tabs, through the menu entry); `game-options-modal`; `load-save-modal` |
| `end-game/` | `defeat-and-score-screen` (an AI given the score victory; the defeat modal, then the score screen its button opens, read again with a second empire picked and a second figure plotted, then Empire Chronicles into the journal and the journal's Back to the main menu); `elimination` (the player's own empire put out of the game); `journal-modal` (every row, and a row's delete-entry confirmation opened and cancelled); `score-screen-from-journal` (the same page as a stored game, which draws its way back to the journal); `victory-and-cutscene` (the player given the score victory; the won page, whose third button replays the outro cutscene). The three that finish the game reload the save themselves — `endlib.sh` |
| `dialogs/` | `rename-box`; `message-box-non-blocking` — windows shared by several screens |
| `main-menu/` | out of game only, run against a freshly launched game at the menu: `menu`; `mod-settings`; `new-game` (the lobby, faction choice, custom-faction editor, advanced settings); `load-save`; `game-options`; `dlc`; `credits`; `disclaimer`. `menulib.sh` holds their drain helpers |

Root files: `run.sh` (the runner), `walk-all.sh` (the explicit full list, in game), `by-key.sh`
(every registered screen key dumped unfocused, or just the keys named — the baseline for
screens no scenario can open), `lib.sh` (shared helpers), `cs/*.cs` (the `/eval` bodies:
`tut`, `drain`, `reset`, `restore`, `minor`, the two `rebind-*` walker templates, and out of
game `menudrain`, `menuhome`), `fixture.env` (§2), `diffwalks.sh`, `normalize.sed` (§4).

## 1. Preconditions

* **The game is running with the dev server on** (`devServer = true` under `[Dev]`, or
  `run-game.ps1` without `-NoDev`). `run.sh` refuses to start without it.
* **In game**, on whatever save you mean to walk. The harness loads one in two places only:
  `walk-all.sh --reset`, which is for setting up *before* a pair and never between the two
  halves of one, and the `end-game/` scenarios that finish the game, which reload the newest
  save as their own undo (§7). `main-menu/` is the exception: it walks what the game draws
  before a save is loaded.
* **The build under test is loaded**: `dotnet build` → `POST /reload` →
  `GET /loader/status` shows `staleBuild:false` and an incremented `modAssemblyName`.
* **The game's own options modal is left on its Video tab.** It remembers its selected tab
  across opens and the in-game scenario never touches its tabs, so both halves of a pair dump
  whatever a human last selected.
* The tutorial popup is minimised by every `ensure_*` arrival (`cs/tut.cs`); expanded, it
  eats every injection as `unconsumed`.

**Both halves of a pair must come from the same game process.** `GraphSheet` row keys derive
from `GetHashCode()`; they survive a hot reload but not a restart, and a restart also
re-instantiates every domain object. So the loop is build → `/reload` → walk, never build →
relaunch → walk. A "before" you did not think to take: `git stash push -u -- ES2Access
ES2Access.Tests` → build → `/reload` → walk → `git stash pop` → build → `/reload` → walk.
Unsafe while another stage is editing the same trees.

## 2. Configuration (`fixture.env`)

Three knobs, and they are the whole configuration surface. Everything else is discovered.

| Knob | Default | Why it cannot be discovered |
|---|---|---|
| `WALK_HOST` | `http://127.0.0.1:8771` | It is the thing we ask. `ES2ACCESS_DEV_PORT` moves the server; a second instance needs a second port. |
| `WALK_PACE` | `100` (percent) | Scales `pause`, the rare fixed wait a step uses when it has nothing to wait on. |
| `WALK_SAVE` | empty | `walk-all.sh --reset` only. Empty means the newest save. "Which save is the fixture" is the caller's intent. |

## 3. How a scenario runs

**Arrival is idempotent.** A scenario starts with an `ensure_*` helper (`ensure_screen`,
`ensure_modal`, `ensure_galaxy`, `ensure_system`) that proceeds at once when the screen it
wants is already focused with nothing modal over it, and otherwise drains and opens it. A
scenario never closes the screen it walked, so a folder pays for opening its screen once, and
one scenario run alone opens what it needs. `run.sh` drains once at the very end of a run —
in game, every modal and screen closed and the camera at galaxy overview; out of game, back
to the main menu — so the fixture is left as found.

**Waits are predicates.** Every wait is a `POST /wait` the dev server evaluates each frame:
the mod's focused screen key (`onscreen`), a window shown or hidden (`shown`, `hidden`), no
modal on top, or the mod's own dump holding an expected row (`waitdump` — for a surface the
game builds over several frames after its window shows, such as planet cards binding to their
labels). The one fixed interval is the 0.4 s of speech silence the `/input` and `/type`
routes themselves wait for before answering, which the dev loop measured as the necessary
key spacing: a faster loop reports a plausible wrong route rather than failing. The library
adds no sleep of its own on top.

**Discovery replaces names.** Node **keys** are mod-authored and stable across saves
(`hud:empire/resource/Strategic2`, `system:planet/…`); node **labels** are localized and
fixture-dependent. So a route addresses by key and reads the label back (`snap`, `label_of`,
`label_nth`, `key_nth`, `nkeys`), lands on it by type-ahead (`findland`, which walks the
screen's stops and types only at the one whose dump holds the text) and clears with
`ui.back`. A failed landing does not move the cursor: never follow one with `ui.click`.

**The caption rule.** A region's drawn caption is spoken as part of its *first* row
(`Planets, Leo I, …`), but type-ahead matches a node's **own** text only — so the first field
of a region's first row is a string type-ahead will never find. Ask for **row 2** whenever
"any row of this table" will do, or read the first row's second field.

**One tree shape, one cursor.** `capture` runs `cs/reset.cs` first, clearing the mod's
`GraphState` (expansions, stop memory, cursor) so a dump does not depend on what an earlier
step left open. Use `dump` instead where the route *depends* on an expansion it just made
(the galaxy tree).

**Tooltip probes.** A renderer-assembled tooltip has no text until its window draws, so the
unfocused dump cannot see it. `tip` saves the dump and `DevProbe.Tooltip()` for whatever the
cursor is on, with the tooltip delay set to zero for the pass and restored after.

## 4. Normalisation and the volatile classes

`diffwalks.sh` runs `normalize.sed` over both sides. Five classes vary between two runs of
the same route and would otherwise read as a disaster:

1. **Instance-hash node ids.** `GraphSheet` row keys and drop-list ids derive from
   `GetHashCode()`: `droplist:-191878/2`, `military:row-1360461824c0`. Rules:
   `[droplist:<n>/` → `[droplist:#/`, `row<5+ digits>` → `row#`.
2. **The HUD wall clock** (`hud:real-time-clock`) — it changes every minute of real time.
3. **`DevProbe.Tooltip()`'s `defaultRead` array**, which *accumulates* class names for the
   life of the session. Normalised to `[#]`.
4. **The studio's news banner** (`mainmenu:news`) turns its story over every few seconds and
   is absent between stories, which moves every menu entry's ordinal. The news row and its
   buffer line are deleted and the total of every `[mainmenu:` ordinal is normalised.
5. **The focus marker `> `** — not normalised. Every `capture` reseats the cursor
   deterministically; a diff showing only `> ` moving means the route lost the cursor.

Diagnostics are **not** diffed: `ghosts.txt` (`DevProbe.Screen()` + `Ghosts()` per capture,
plus `NotificationParity()` on the popup), `routelog.txt` (`DevProbe.Screen()` at every
labelled waypoint), `index.txt`, `skipped.txt`, `status.json`, `logs/`, `*.aba.txt`. They
are how you explain a diff, not part of the regression surface.

## 5. Reading a nonzero diff

Work in this order.

1. **`skipped.txt` differs between the two runs?** The fixture changed under you. Fix that
   first; `MISSING IN A/B` lines mean the same thing.
2. **`routelog.txt` for the scenario that differs.** If the route landed on a different node,
   everything downstream is noise, not signal.
3. **Only `> ` moved** — cursor loss, see (2). **Only ids differ** — the normaliser missed a
   hash class; add the rule.
4. **A node vanished from one side and nothing replaced it** — a *gate drop*: the mod stopped
   declaring something. Check the same capture's `ghosts.txt` on both sides.
5. **A node's text changed** — a *speech change*. That is the class the walk exists to catch:
   read the line pair and decide whether it is the change you intended.
6. **Tooltip captures differ but the dumps do not** — a Class-backed tooltip changed. Any
   carrier the focused pass does *not* visit is **unproven** by a clean diff, not proven.
7. **The camera.** On the galaxy, planet rows are leaves until the map draws a card for
   them, and it draws cards for the one system the camera is in on. A differ seeing rows turn
   from leaves to groups should suspect the camera, not the change.

## 6. Graceful degradation

No route fails because the fixture lacks something. It detects, skips that capture, records
the reason in `<out>/skipped.txt`, and carries on. **A skip is data, not an error**, and the
two halves of a pair must skip the *same* things.

| Trigger | What is skipped |
|---|---|
| No star system in the map tree | The whole `galaxy/map-tree` scenario |
| System A has no deposit / planet / lane row | That tooltip probe |
| Fewer than two systems in the map tree | The second-system dump and the orbital rebind |
| A HUD region has no second row | That HUD tooltip probe |
| No fleet reachable on the map tree; only one | The fleet panel; the fleet-action rebind |
| The empire owns no colonized system | `system-management/screen`, `planet-overview`, `empire/planet-cards` |
| The page has no planet row 2 or 3 | That planet-card tooltip probe; with no row 2, the planet overview |
| The empire owns one colonized system | The page turn and both planet-card rebinds |
| No suggested technology / fewer than two systems-table or fleet rows | That page's tooltip probe |
| The ship designer declares no hull combo | The hull drop list |
| No minor empire with a system | The minor-civilization window |
| The empire owns no hero | Hero selection |
| The pause menu has no mod-settings entry | Both settings-tab dumps |
| No pending notification; fewer than two | The popup; the popup-body rebind |
| `hud:turn-log` is not declared | Nothing — recorded as a finding about the fixture |
| No economy tab with two selectable marketplace sections | The marketplace scenario's rebind |
| Fewer than three empires on the diplomacy ring | The wedge rebind |
| The screen registry cannot be read out of a bogus-key refusal | `by-key.sh` |
| No major empire but the player's is left in the game | The defeat modal and the score screen it opens |
| The defeat modal draws no score-screen button | The score screen, from the modal |
| The score screen lists fewer than two empires, or fewer than two figures | That second reading of it |
| The journal holds no finished game, or its row button cannot be reached | The score screen, from the journal |
| The score screen drew no way back to the journal | Pressing it |
| The score screen declared no Empire Chronicles button, or the journal no Back | That leg of the way out |
| Delete entry raised no confirmation, or the confirmation declared no Cancel | That box (the box is left through the game's own Exit) |
| Replay cutscene raised no cutscene screen | The replayed cutscene |

## 7. What the walk will not do

It never advances a turn, writes a save, dismisses a notification, or presses Load / Save /
Confirm / Apply / Create / Retrofit / Exit Game. It deletes nothing the player made: the
`end-game/` routes delete the end-game summaries their own finished games wrote, matched by
name against what the journal held before they ran, because finishing a game is a journal
row for good and `journal-modal` dumps every row. It loads a save in two places only:
`walk-all.sh --reset`, and the three `end-game/` scenarios that finish the game — there the
load is not a fixture choice but the one available undo, because a finished game leaves
`run.sh`'s drain nothing to close. The negotiation modal is never opened — closing an
unsigned negotiation posts an order, which is also why a diplomacy wedge is never
activated. The notification popup is closed by hiding its window, never through the
dismiss key, and the `AlreadyRead` flags that browsing it sets are put back — which the
`end-game/` routes also do, because a save load re-raises the turn's popups over the page
they are about to read. `run.sh` ends by draining and running `cs/restore.cs`, which nulls the
focused control and restores the tooltip delay.

## 8. Quirks worth knowing

* **`HandleInput(Exit)` does not reliably hide every modal** (`LawsManagementModalWindow`,
  `GovernmentModalWindow`); `cs/drain.cs` sweeps a 39-name list as a backstop.
* **The mod's settings window remembers its tab across opens**, and a cleared `StopMemory`
  seats the cursor on the *selected* tab — so the route selects the first tab explicitly
  before dumping it, and leaves it there.
* **`POST /eval` bodies that touch `List<GameType>` poison the REPL session** for good. Every
  eval here binds game collections as `System.Collections.IList` and indexes them.
* **Right on an already-expanded map row steps into it and flies nowhere**; `map-tree`
  collapses a row before expanding it, and waits for the focused system to be the target.
* **Landing on rows elsewhere in the map tree pans the camera**, so `map-tree` takes its A
  reading before its tooltip probes, not after.
* **Type-ahead swallows the first character of a digit-leading string** in the `search` echo
  (`1st Patriots Navy` searches as `st Patriots Navy`) — the landing is still right.
* **A modal opened from `/eval`** sets what its opener sets, then shows it. Never close one
  with `HandleInput(Exit)` on a window that was never properly bound — that wedges the stack.

## 9. The A-B-A rebind check

A widget the game pools keeps whatever the previous binding left on its components, so a
capture taken on a fresh binding shows only the fresh case — and a before/after pair of such
captures agrees with itself while both sides are wrong. A scenario over a pooled surface
therefore reads it (**A**), performs the game's own rebinding action onto a second subject
(**B**), performs it back, reads the surface again (**A'**), and diffs A against A' inside
the run (`<scenario>.aba.txt`; the runner prints the line count). Anything that survives the
normaliser is the previous subject's state being read off a rebound widget.

Two rules make each check mechanical rather than judgement: **B must shrink the bound list**
before it grows back (the system with fewer planets, the fleet with fewer actions, the
market section with fewer rows, each found by reading the counts at runtime), and **every
capture prints `Alpha` beside `Visible`** for the pool's own children (`cs/rebind-pool.cs`),
because a retired child parked at alpha 0 draws no text, so a graph dump prunes it and then
agrees with whatever the mod declared. Captures go through an `/eval` walk of the render
(`cs/rebind-walk.cs`), not `GET /gui/graph`: these surfaces run past the dump's 800-line cap.

Pooled surfaces with no check, each because its rebind needs a state the walk may not
produce: the economy tab bar (researching the marketplace technology), the negotiation shelf
and basket (the negotiation modal), the senate and election candidate cards (stepping the
election), the hacking program menus (a hacking target in scan view).
