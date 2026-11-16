# GC pause handover

State of the investigation into the second-long freezes, as of 2026-11-16. Written so a fresh
session can resume without the conversation. The preparations (work items 1-4 of section 5) were
implemented on 2026-09-21; on 2026-09-23 the owner took item 7 (section 7, per-build allocation)
ahead of the rate change, and three of its commits landed. On 2026-11-16 two more item-7 commits
landed, and so did the rate change (item 5, ten builds a second) and the heap room check (item 6).
RESUME AT "Where to resume" at the head of section 5. Every experiment below was done from the dev
server's REPL and is gone on a game restart.

Figures come from one mid-game save and TWO machines. Sections 1-3 were measured on the owner's
laptop, which runs the game at 60 fps; section 7's 2026-09-23 figures were measured on a machine
that draws the empire screen at about 20 fps and the galaxy map at 11-12 fps. The mod builds once
per frame, so builds per second, MB/s and the seconds between collections do not transfer between
the two; bytes per build and milliseconds per build do. The 2026-11-16 figures were measured at
about 58 fps on a save whose empire screen declares 261 nodes (the systems table alone 208), so
its per-build bytes are larger than section 7's 101-node figures for the same code. The absolute numbers also scale with how
much live data the save holds. Line numbers drift, so sites are named by symbol first. Cites marked
(sub) came from research subagents and were not re-verified.

## 1. What the problem is

- The freezes are stop-the-world garbage collections. Every frame over 100 ms in every recording was
  a collection (630-940 ms each), and every slow key press overlapped one: 212 of 224 arrow presses
  answered in a median 30 ms, the other 12 waited out a pause (up to 720 ms).
- It is not specific to the empire screen. The empire screen, the military screen and the galaxy map
  collected at the same rate, and the owner confirmed the same lag by hand on the military fleets
  table and on v0.2.0. It has been there as long as the graph has been rebuilt every frame.
- The mod sets how OFTEN collections happen; the game sets how LONG they take. ES2 runs Unity 5.5's
  Mono with the Boehm collector: non-generational, non-incremental. A collection traces all live
  data (about 870 MB here), so its cost does not depend on how little garbage there is.
- Essentially all of the mod's allocation is the per-frame graph build. Frames that do not build
  allocate about 1.4 KB in `ModEntry.Update`. No slow queries (a build is 0.35-0.65 ms), no leak (the
  heap floor after a collection stayed at 868-880 MB for 40 minutes), and key presses add nothing
  measurable beyond their own builds.
- Nothing large is captured by closures. Holding 1500 renders raised the live heap by 333 MB and all
  of it came back on release: a render keeps about 236 KB alive, about 0.9 KB per node.
- The engine design is fine on newer runtimes. Songs of Conquest (Unity 2022.3, incremental collector
  with 3 ms slices, about 290 MB live) rebuilt a 439-node screen every frame, collected every 2.5 s,
  and had no frame over 42 ms. A forced blocking collection there took 91-106 ms. FactorioAccess, the
  engine's origin, never renders per tick at all: `Graph:_with_render` renders inside each input
  handler (`../FactorioAccess/scripts/ui/key-graph.lua`, `devdocs/ui.md`).

## 2. Measured rates

Allocation and collection interval, fresh heap unless noted:

| Configuration | Allocation | Interval between pauses |
|---|---|---|
| Game with the mod's update paused | 2.3-2.9 MB/s | 28 s |
| Mod as shipped (60 builds/s) | 20-26 MB/s | about 5 s |
| 5 builds/s, fresh heap | about 4.7 MB/s | 17 s (steady 16-19 s) |
| Mod as shipped, heap grown by earlier probing | same | 14-17 s |
| 5 builds/s + heap grown 400 MB, owner arrowing | about 6.3 MB/s | 54 s median (52-56 s, 10 samples) |

Pause length did not change with the grown heap: median 785 ms fresh, 713 ms grown.

On the 20 fps machine (2026-09-23), sitting on the empire screen with the start-of-day build:
4.8 MB/s total, 53 s between collections, 250 MB of room per cycle, pauses about 400 ms; at 5
builds/s the same screen allocated 1.6 MB/s, which puts the game's own share on that screen near
0.5 MB/s - the mod was about 90% of allocation there, so per-build savings mapped almost one to
one onto the interval. On the galaxy map the game's own share is larger (section 2's first row).

Cost of one build (count-between-collections method): empire screen about 347-360 KB, military about
275 KB, galaxy map HUD page about 585 KB, battle notification popup about 95 KB. A frame that really
builds costs about 85-100 KB more than the bare build (navigator work done only on a fresh render,
not split further). The global HUD stops cost 107 KB per build on every screen that carries them.

Owner's verdict after testing by hand: 5 builds/s alone is a clear improvement but not enough;
5 builds/s plus the grown heap is "drastically better" and a short freeze about once a minute is
acceptable.

## 3. How this collector decides when to collect

- In a fresh session the heap has 80-90 MB free above the live data, the heap fills first, and "heap
  full" is the trigger. Interval = free room / allocation rate.
- After the heap is grown, a threshold takes over: collections came after about 345 MB of allocation
  even with about 509 MB free (heap 1389 MB, live about 880 MB, read with
  `mono_gc_get_heap_size` / `mono_gc_get_used_size`). Growing further buys almost nothing. The
  threshold formula (about a third of scanned memory, free space at quarter weight) is from memory of
  this libgc generation and is unverified; the 509-free-versus-345-used observation is measured.
- The heap is not returned to Windows while the process lives: private bytes stayed about 450 MB
  higher after the 400 MB was released and five collections had run, and an earlier grown heap kept
  its size for over 30 minutes.
- Growing by 400 MB (25 blocks of 16 MB, allocated and released) froze the game for 846 ms with one
  collection inside it.

Controls that do NOT exist in this build (checked against the game's own `mono.dll`, 780 exports):
no `GC_INITIAL_HEAP_SIZE` / `GC_FREE_SPACE_DIVISOR` / `GC_MAXIMUM_HEAP_SIZE` strings (only
`GC_DONT_GC`, which disables explicit collections too); no `GC_disable` / `GC_enable` /
`mono_gc_disable` exports (the forum workaround of calling them by address from a PDB is version
specific and was rejected); no incremental mode and no `GarbageCollector.GCMode` (Unity 2018+).
What does exist: `GC.Collect()`, and the two heap-size exports above via `[DllImport("mono")]`
(verified callable from inside the game).

## 4. Owner decisions so far

- Objective: fix every part of the mod that would break if the global build rate dropped to 5 a
  second, prove behaviour identical (e.g. the camera still follows the cursor through the galaxy
  tree), then reduce the rate, then monitor pause frequency.
- "Waiting a budget of N frames is an anti-pattern": fix such waits at the root. A landing whose
  target is known should open its whole path on the next build, not one level per build, and not by
  looping several builds inside one frame.
- Items 1-4 of section 5 were approved and are done, the `Core/` change and one full walk included.
  Any further `Core/` edit and any further full walk need approval again.
- 2026-09-21: the rate change is VARIANT 1 (slow only the build; the rest of `EnsureFocus` stays per
  frame on the last render). The 200 ms staleness of key claims is accepted, no code. The slow-clock
  checks skip battles (the save cannot raise one). The page-turn fallback's "no cards drawn when the
  seat window runs out" caller stays as it is: the game does not take ninety frames to draw the
  cards. The changed galaxy reading (Right into a collapsed system now lands on "group ...
  collapsed", tooltip line after the landing) is correct.
- 2026-09-23: the owner reversed the deferral of section 7 ("not great that the mod still allocates
  so much even at 5 builds/s") and took it first. Approved and landed: the two mechanical `Core/`
  edits (builder scratch reuse, edges as values; da8bc18), HUD declaration retention (cf4c9e3),
  tutorial bar retention (2c98ce1). Declined for now: the notification strip and turn log (about
  2% plus 1.6 KB per pending notification). Proposed and NOT yet approved: the two remaining
  `Core/` wins in section 7. `NodeVtable` slimming was first rejected as too invasive, then
  re-proposed in the form that changes no declaration site (properties over a lazily made
  sub-object); no ruling yet.
- 2026-09-23: measurement windows are one minute each ("do not use a 4 minute timer"); a full
  collection-to-collection interval needs up to two of them back to back (section 8).
- 2026-11-16: approved and landed: table headings kept (799d171) and side-panel lines kept
  (4491530), a time-based build clock at ten builds a second that falls back to every frame below
  10 fps (3b52385), and the heap room check (dd9919f). No setting to turn the heap check off
  ("don't add"). The per-frame wording in CLAUDE.md and docs/generic was updated with approval.
  Not yet asked: the notification rows (section 7, item 3) and render-node pooling (item 7).
- Every new key binding still needs approval; none is proposed here.

## 5. Work plan, in order

**Where to resume (2026-11-16).** Items 1-6 are done. Item 7 has five commits (section 7).
Owed:
- The FULL WALK, which only the owner can approve. It covers da8bc18 (a `Core/` refactor
  claiming no spoken line changed), cf4c9e3 (every HUD page), 799d171/4491530 (every table and
  side-panel page) and 3b52385 (every screen's rate). Walk dumps build fresh through
  `InspectRender`, so the walk guards against regressions but cannot show a rate defect. The
  rate's evidence is speech transcripts: a fixed route on the empire screen and the galaxy tree
  (camera position read after each move, a constellation opened, inspect in and out) was
  identical to the per-frame build at 10/s and at 2/s.
- Live checks nobody has driven yet:
  - the four sites ed48abb fixed (hero inspection page change, a targeting mode dismissing
    inspect, inspect exit with no row to restore, the scanner editor refill);
  - notifications arriving live; end turn (advances the save, so it is the owner's to run);
    options key capture (needs `/key` with the game in front);
  - a real save load through HeapRoom, and HeapRoom's first growth in a fresh process (only the
    reload path grew the heap: 385 MB free, +80 MB);
  - the events that drop a kept set: a save load, a turn end, a rebound resource row, the
    long-tooltip option flipping.
- Not explained: with the mod paused, the game's own allocation on the empire screen read
  3.4 MB/s at the start of the 2026-11-16 session and 7 MB/s at its end, after five hot reloads
  and much REPL measurement. The mod's share at 10/s is about 1.2 MB/s, so the game now
  dominates. Measure again in a fresh process before chasing more mod bytes.

The rest of this paragraph is the 2026-09-23 plan, kept for its reasoning. Items (a) and (b)
are done; the sweep in (a) was done by reading, and the four hazards it found are ed48abb.
Items 1-4 are committed on `main` (f182d85, 6cacc9a, 0764a43, fe54b02, ac7a7c1, 2767e80; doc
wording 2e7a04c). Next, in order:
(a0) the owner's ruling on the two remaining `Core/` wins in section 7 (contexts, vtable slimming);
if approved, one stage, proven like the three commits (bytes per build through the navigator's own
path, a full cycle on the same heap, the screen dump byte-identical); then
(a) the slow-clock live checks of what nobody has driven yet - the camera following the cursor
through the galaxy tree, inspect mode, notifications arriving live, end turn, options key capture
(not battles) - as speech transcripts at 1 build/s against the normal clock, plus a SWEEP rather
than a sample: list every screen whose `Build`, `IsActive` or a gate they call reads screen-private
state written outside the build (a settle counter, a guard, an armed flag) and check each either
invalidates when it changes or sits under a hold whose edges `GraphNavigator.NoteHoldEdge` already
covers (the shape is described under item 4); (b) item 5, variant 1, with a time-based clock;
(c) item 6; (d) item 7. Follow-ups that block nothing: `walks/galaxy/fleet-panel.sh` clicks the
notification strip when it finds no fleet, which opens the popup and ruins every later galaxy
capture of the run; `walks/notifications/popup.sh` empties the turn log through
`HideAllGuiNotifications()`, so the walk changes the fixture it measures; the 25 frame-counted
waits that tick on game flags could become seconds.

**The full walk (2026-09-21), pre-change build ecb10f3 against HEAD ac7a7c1, one game process:** no
differing line attributable to the code. How it had to be read, because the raw total was 2698:
the baseline's galaxy family was re-walked without `fleet-panel` (see the follow-up above), and
`senate/screen` and `system-management/screen` were re-walked on both builds because the game's
tutorial popup (one half) and the re-raised notification popup (the other) stood over a capture.
With the turn-log stop, the players row (AI turn state) and notification instance numbers filtered
from both sides, 60 lines remained in 3 files, all fixture: the end-game journal's scores and row
count, "Next notification ... unavailable" (one pending notification instead of four once the turn
log was emptied), and a focus marker on the absent turn-log stop. `galaxy/fleet-panel` is unproven
in both halves.

The rule behind items 1-4: code that changes what the next build should contain must invalidate the
graph at that moment instead of counting on a build arriving next frame. The graph's own operations
already do (`KeyGraph.SetExpanded` calls `Invalidate()`); the screens' side channels and the ends of
their holds do not. `KeyGraph.Rerender` honours `Invalidate()` whatever the clock, which is why key
presses kept a 29 ms latency at 5 builds/s.

Proof for each item: (a) the sequence still works with builds a full second apart, set from the REPL
(section 8) - record the failure before the fix where there is one; (b) the screen's targeted walk is
identical before and after at the normal clock.

1. **Bookmark landing** - DONE (f182d85). Screens can now drop the standing render
   (`GraphNavigator.InvalidateRender`); `LandInside` does so when it queues the expansion and the
   wait ends where `ApplyPendingExpansions` runs. At 0.5 builds/s: 2 of 3 failed before, 5 of 5
   landed after, near and far band. The map ran at about 13 fps in that session, so the old 12-frame
   budget only failed once builds were 2 s apart.
2. **The engine's landing path** - DONE (0764a43), full walk run (above). `Reach` opens
   the whole path on one build and the request takes back the levels no node carries; Opened spends a
   capped count, Waiting spends real seconds; `FocusNode` invalidates and ignores a repeat of the
   identical request in flight. Found live and fixed in the same commit: a request that ends while
   another screen has the keyboard is cleaned out of its OWN screen's state. Still stale and needing
   the owner's approval to edit: `docs/generic/ui-navigation.md`, "Seating the cursor deliberately",
   the sentence "opens one level of ancestry per build ... Size the budget in frames-of-flight".
   Type-ahead cannot start a deep landing (it searches declared nodes only), so no live route
   exercised three levels through a shut constellation.
3. **Notification popup seating** - DONE (6cacc9a): compared across builds by render identity, a
   build asked for while the seat is owed. The defect was latent on the test save (no failure
   reproduced); the recording showed the seat taken off a render built before the swap.
   **System-management page turn** - the same-card restore needed no change (identical at the
   normal clock and at 1 build/s). `_planets` is refreshed only by `Build` (through
   `Whole()`/`Drawing()`, which is also the readable "window is bound to the new system" condition).
   A PRE-EXISTING normal-clock defect found on the way is fixed (fe54b02): a turn to a system with
   fewer worlds than the card the cursor was on went silent for the swap guard's ninety builds;
   the fallback now counts as the restore being asked for and lands on the planets' first row.
   A card the seat itself opened (`OpenCardBeingSeated`) is handed back once the page has turned
   away from it (2767e80); cards the player opened by hand stay in the set across turns on purpose.
4. **Hold edges and the locate** - DONE (ac7a7c1). `EnsureFocus` invalidates on the pass where the
   focused screen's `BetweenViews` or `LandingSuspended` CHANGES, both edges; the locate landing
   invalidates when a request is first seen. Falling edge: the held announcement is made off a build
   that postdates the hold. Rising edge: a page that answers a hold by declaring nothing does so at
   once - at 1 build/s the system page's old render stood three frames into a turn and `WatchLive`
   spoke the old page's tooltip off it. Deliberately the edge, not the state: `LandingSuspended` is
   true for every galaxy camera move (9 builds before, 11 after, over one flight at 1 build/s). One
   normal-clock reading changed, as the binding hold always intended: Right into a collapsed system
   at overview zoom now lands on the row as a "group ... collapsed" (it read as a leaf off the
   pre-bind render) and the long tooltip line follows the landing instead of preceding it.
   No fix needed (read, not live): `GalaxyTree.ReadBand` (detection and the reconcile it drives
   happen in the same build), `ZoomLadder.Claim` (re-asks every build, arriving page builds at once),
   the centred-system seat (waits on the camera, ends in a landing). The general shape to keep in
   mind for item 5: `Rerender` answers TRUE off a memoized render, so everything after it in
   `EnsureFocus` (`SyncVisual`, `FillBuffer`, `WatchLive`) runs against the old render until the next
   build; any screen-private state that `Build` reads to declare less (a settle, a guard) only takes
   effect on a build, so it must invalidate when it changes unless a hold edge already covers it.
   Walk hazards met: `Gui.GuiNotificationService.HideAllGuiNotifications()` (used by
   `walks/notifications/popup.sh` and by stages to clear a re-raised popup) also empties the turn
   log, which shows up as `hud:turn-log` diffs in every later capture; a notification popup
   re-raised between runs is not closed by `walks/cs/drain.cs` and silently skips the galaxy leg;
   `galaxy/map-tree` inherits leftover expansions unless `walks/cs/reset.cs` runs first, and the
   galaxy inspect cell re-arms itself after an overview view-level request, so a script's Right and
   Down drive the cell unless it presses `ui.back` first.
5. **The rate change itself** - DONE (3b52385, with ed48abb before it): ten builds a second on
   real time, every frame below 10 fps. On the empire screen, 100 renders in 591 frames;
   collections every 22.6 s at the per-frame rate became every 39.5 s on the same heap.
   `KeyGraph.FrameCounter` is an injected clock (`ModEntry` wires
   `Time.frameCount`); a time-based clock in its place gives a global idle rate with no `Core/` edit.
   The owner chose the FIRST of the two variants below (2026-09-21):
   - slow only the build and keep the rest of `EnsureFocus` per frame on the last render (what every
     experiment did; live values stay current because parts are closures read when asked; the tooltip
     path is untouched because `PointerFocus.Tick`, `LateTick` and `WatchDrawn` are per frame and
     outside the pass);
   - skip the whole pass on idle frames (keeps Factorio's "closures are used only right after their
     own render" guarantee, but then needs an in-flight predicate: pending focus/stop/descend,
     unseated cursor, empty build, `_visualKey == null` while focused - `GalaxyHudScreen.FollowCamera`
     and `ElectionScreen` call `ClearVisual()` from `OnUpdate` and rely on a same-frame re-commit -
     a stale buffer, `BetweenViews` / `LandingSuspended`).
   Known exposure in either variant: key claims read the standing render (`DeclaresStop`,
   `TakesCarryKey`, the clear and go-to keys; the code says it is "never more than a frame old").
   Worst case is a key that does nothing or that the game also acts on, since acts re-check and
   rebuild. Owner ruling 2026-09-21: the 200 ms window is accepted, no code. (Refreshing inside the
   claim is not an option anyway: the predicates are polled by the game's key scan several times a
   frame, `ModEntry.BackKeyClaimed` / `GraphNavigator.TakesCarryKey` comments, so a build there
   would run every frame.) About fifteen doc lines assert the per-frame contract
   (`docs/generic/ui-navigation.md`, `docs/generic/performance.md`, `docs/generic/buffers.md`,
   `docs/generic/dev-server.md`, `docs/dev-loop.md`, CLAUDE.md Performance, `docs/galaxy-map.md`,
   `docs/heroes-and-diplomacy.md`) and two comments in `walks/lib.sh` say "rendered on the next frame".
6. **Heap room check** (section 6) - DONE (dd9919f, `UI/HeapRoom.cs`).
7. **Per-build allocation** (section 7) - five commits landed; the rest is listed there.

Of 33 frame-counted waits classified, 25 tick in `OnUpdate` or the per-frame pump, wait on the game
(a readable flag such as `CameraSettling` / `ChangingLevel`, a measured quiet period, or a deliberate
delay) and do not depend on the build rate. They can be converted from frames to seconds as tidying;
none blocks the rate change. A wait that ENDS by asking for a landing inherits item 2's behaviour.

What the audits found fine: all 17 frame-keyed helper caches and `FrameSweep` refresh lazily when
asked; no unit test depends on per-frame rendering (`FrameCounter == null` disables the memo); walk
dumps go through `InspectRender`, which always builds fresh; 31 of 34 `OnUpdate` overrides speak from
game state independently of the build (battles, loading progress, end turn, lobby states); no live
announcement part changes continuously.

Live evidence at a plain 5 builds/s with none of the fixes: a 32-step key route (table moves, stop
changes, opening a screen from the HUD, opening a panel from a table cell, tooltip follow-ups) gave
identical speech transcripts; targeted walks of system-management/screen, notifications,
research/screen and galaxy/map-tree had 0 differing lines against a normal-clock walk (the FIRST
walk of a session differs from later ones by a first-run effect - re-walk before comparing); a
table re-sorted by the game with no input kept the cursor on the same row object and spoke nothing.
Not tested: galaxy camera follow, inspect mode, battles, notifications arriving live, end turn,
options key capture, multiplayer.

## 6. Heap room check - agreed shape

Implemented as below in dd9919f, with a fixed 450 MB target (not a fraction of live data) and no
setting.

- Measure, do not grow unconditionally: the mod hot-reloads often and the heap belongs to the
  process, so a second growth would only waste the player's memory.
- Read `mono_gc_get_heap_size() - mono_gc_get_used_size()` immediately after a collection; mid-cycle
  "used" includes garbage (it read 183 MB free when the true figure was about 509 MB).
- If free is below the target (about 450 MB here; possibly better as a fraction of live data, since
  the threshold appears to scale with it - unverified, check on a larger save), allocate the
  shortfall in 16 MB blocks and release them. Blocks rather than one array because the collector is
  conservative: a stray value pointing into one 400 MB array would pin all of it for the session.
  No need to touch the blocks; the runtime zeroes a new array.
- Run it when a game finishes loading (live data is at its plateau and the forced collection hides in
  the load), again after each load, and at mod start only if a game is already running (hot reload;
  there it can wait for the next natural collection instead of forcing one). Not at the main menu.
- Wrap the P/Invoke so a missing export skips the feature. One file outside `Core/`, no state.
  Consider a setting for players with little RAM: the cost is about 350-450 MB held for the session.
- Untried idea: also call `GC.Collect()` when the player has been idle a couple of seconds or presses
  end turn, so the remaining pauses rarely land on a key press.

## 7. Per-build allocation: what landed on 2026-09-23 and what remains

All figures below are bytes per build through the navigator's own render path (section 8,
"count between collections"), on the 20 fps / 12 fps machine; the interval columns are that
machine's and do not transfer. The empire screen built 101 nodes (about 30 of them HUD), the
galaxy map 49 (most of them HUD). Pause length never moved: about 400-780 ms on the empire screen,
480-650 ms on the map.

| Commit | Empire screen, per build | Empire cycle | Galaxy map, per build | Galaxy cycle |
|---|---|---|---|---|
| start (9243b62) | 222 KB, 0.66 ms | 53 s | not measured | |
| da8bc18 builder scratch reused, `Transition` a struct | 174 KB, 0.56 ms | 64 s | 199 KB, 0.51 ms | 89 s |
| cf4c9e3 HUD declarations kept (`KeptCells`) | 118 KB, 0.47 ms | 86 s | 137 KB, 0.38 ms | 140 s |
| 2c98ce1 tutorial bar kept | 112 KB, 0.46 ms | 92 s | | |

On 2026-11-16 (58 fps, the 261-node empire screen; cycles on a heap with about 325 MB of room):

| Commit | Empire screen, per build | Cycle at one build per frame |
|---|---|---|
| start (1df6434) | 216 KB, 0.97 ms | 20.2 s |
| 799d171 headings kept + 4491530 side-panel lines kept | 132 KB, 0.67 ms | 22.6 s |

Where the 216 KB went before those two commits, part by part through a re-armed builder:
- side panels: 54 KB for 18 lines;
- the systems table's 14 headings: 40 KB, against 37 KB for its 194 replayed cells;
- notification strip: 14 KB for 7 nodes;
- banner: 11 KB; tabs: 6 KB; turn controls: 2 KB;
- `GraphBuilder.Build()` assembling the render: about 50 KB.

A node whose records are made fresh costs 2-3 KB a build. A kept node costs about 0.2 KB,
which is the builder's own bookkeeping. After the two commits the headings cost 6 KB and the
side panels 20 KB.

Each commit's message carries its numbers. Proof for each was the offline suite, the bytes-per-build
loop, one full collection-to-collection cycle on the same heap as the previous build (da8bc18 was
A/B'd by stashing and reloading the old build in the same session), and `GET
/gui/graph?edges=1&buffers=1` byte-identical apart from the wall-clock readout.

What the three commits are, so a reader can judge a regression against them:

- **da8bc18 (`Core/`)**: `GraphBuilder.Rearm` clears the builder's collections instead of remaking
  them, pools `Row` and parent-frame records by index, keeps `Build()`'s grouping/seam/position
  scratch as cleared fields, and caches the auto stop-key strings in a copy-on-grow static array
  (no lock on the per-frame path). Nothing a render holds is reused: `GraphRender`, its
  dictionaries, every `GraphNode`, the caller-owned `Edge`s. `GraphNavigator.BuildRender` keeps one
  builder with a re-entrancy flag; inspection and search builds still make fresh ones. `Transition`
  is a readonly struct held inline in the node's four slots; "not wired" is a null destination.
  Offline: a re-armed builder allocates 258 B/node against 537 fresh.
- **cf4c9e3 (`Screens/GlobalHud.*`, `UI/KeptCells.cs` after 2c98ce1)**: each `GlobalHud`
  instance keeps one set of cells per entry point, keyed on the widget by REFERENCE (Unity's
  `Equals` makes an engine call and calls two destroyed objects equal). The rule: a retained object
  is correct only if every closure captures session objects and reads live state when called, AND
  everything the declaration read EAGERLY still answers the same - the widget's binding (a pooled
  item's resource, a toggle's screen, the pinned quest), the tooltip it was declared with and that
  tooltip's `Class` (which fix the section's mode and cost panel in `GraphNodes.TooltipSection`),
  and the long-tooltip option. The set drops when the entry point reads from a different window,
  panel or empire, and on `Forget()`. A retained vtable may be written per build only by an
  assignment that runs on EVERY build from facts read that build: the players row assigns its
  children (one closure, made once, reading the whisperers when it runs) and its group-or-leaf
  type two-branched, and the turn loop's one-branch `ControlType = Group` write is gone. Each
  construction sits in its own static `...Cell(...)` method so C# allocates the closure
  environment only on a miss. Still per build there: the notification strip and turn log (rows
  keyed per notification, one-branch go-to writes, a per-frame icon memo), the faction panels
  (undrawn on the test save, so unmeasured).
- **2c98ce1**: the collapsed tutorial bar's three records kept in a STATIC `KeptCells<Control>`
  owned by the popup panel (the method is static and shared with the popup page, which keeps
  making fresh records); nothing clears it on page leave, at most three entries.

Where the remaining bytes go, measured per part on the empire screen after cf4c9e3 (loop with
the frame-keyed sweeps warm, so sweep costs are excluded):

- HUD, about 23 KB of 118: banner 10.7 KB (five contexts 4.2, eighteen node floors 3.5, the row
  sort 1.3, toggle badges 0.8), tutorial bar 6.9 (now 1.6), notification strip 3.8 with one pending
  notification (about 1.7 KB per row, 1 KB dismiss-all, a context), turn cluster 2.0 (mostly node
  floors), quest and empty turn log under 0.1.
- Unit costs: a pushed context level 836 B; one node's builder floor (`AddItem`) 197 B; `Build()`'s
  fixed cost about 1.5 KB; a `GraphNodes.Button` vtable without tooltip 775 B; the banner's row sort
  1326 B; chord labels, `ControlId` plus declaration wrapper, `Rearm` negligible. The empire screen
  pushes 10 contexts per build, the galaxy map 9.
- The screen's OWN content is the rest, about 95 KB for 71 nodes, 1.3 KB per node: a vtable, three
  to five closures, an announcement list and a section list per node, remade every frame.

What remains, ranked, with the empire-screen saving:

1. **Contexts in `Core/`** (about 6-8 KB, 5-6%, on every screen): the builder keeps a context
   node's vtable and label closure by label string across re-arms, and skips the edge array a
   non-focusable node never uses; 836 B to roughly 200 B each. Needs approval.
2. **`NodeVtable` slimming in `Core/`** (roughly 10-15 KB, 10%): keep the dozen common fields,
   move the rare handlers behind properties into a sub-object allocated on first write, so every
   `new NodeVtable { ... }` initializer still compiles. Needs approval; no ruling yet.
3. Notification strip and turn log retention keyed per notification (2% plus 1.6 KB per pending
   row): the prune that walks the standing list drops dead entries; the go-to handler and hint
   become two-branched per build. Declined for now. Read-through on 2026-11-16 (not verified
   live) found it small: 100-130 lines in `GlobalHud.Notifications.cs`, no `Core/` edit. Besides
   tooltip and class, each entry must check three things that can change for the same
   notification: its serial (`NotificationSerials.Of`, which changes when a notification leaves
   and is raised again), `NotificationText.Failed` (it flips the first time a title throws), and
   `DrawsShowLocation`. `KeptCells.Find` compares by reference only, so those checks go on the
   cell. Still made per build regardless: the contexts, the icon sweep's array, the `Logged()`
   and `turns` lists, and the per-turn strings. About 7-9 KB a build on the 261-node screen.
4. Banner row sort memoized on the visible set (1.3 KB). Not worth doing alone.
5. Faction panels: unmeasurable on the test save; about fifteen static sites, drawn for some
   factions only.
6. The screens' own tables - four fifths of the build - need the same per-site judgement as the
   HUD, per screen or at the `GraphSheet` level. Not easy; not proposed. (The shared table's
   rows were already replayed, and its headings and the shared side panels are now kept.)
7. **Reusing render nodes** (`Core/`, about 50 KB of render assembly plus about 190 B per
   replayed node). Read-through on 2026-11-16 (not verified live) found it large:
   - Detecting "nothing changed" almost never matches: `PushContext` mints its records on every
     push and most vtables are fresh. `GraphNode`s are made at declaration, before `Build()`.
   - The workable shape is two arenas per `KeyGraph` (render, dictionaries, node pool), building
     into the one that is neither `_current` nor `OrderSource`, with every node field reset on
     rent.
   - It needs a generation number on renders and nodes, because `NotificationScreen._seatRender`
     detects a build by render identity and `GraphAnnouncer`'s parts memo keys on the node.
   - `_lastSpokenNode`, `_visualNode` and `_searchOpened` would hold ids or vtables instead of
     nodes.
   - About 150-250 lines in `Core/` plus 30-60 outside, high risk, a full walk.

At ten builds a second (since 3b52385) the mod's whole share on the empire screen is about
1.2 MB/s, so every item above is now worth a fraction of that. The commit "A node keeps its four arrow-key
edges in four slots" (9d83cd6) was an earlier step of the same kind: about 20% at the full rate.

## 8. Measurement methods and traps

All via `POST /eval` (see `docs/dev-loop.md` for the REPL's own gotchas).

- Slow the build clock without code changes. Never assign a lambda typed into the REPL: on
  2026-11-16 both `() => (int)(UnityEngine.Time.realtimeSinceStartup * 5f)` and
  `() => UnityEngine.Time.frameCount` threw NullReferenceException when called, `Rerender` threw
  every frame, and the loader switched the mod's update off until `POST /reload`. Declare a REPL
  class with a static method instead, bind it with `Delegate.CreateDelegate`, and call the delegate
  once before assigning it:
  ```
  public static class SlowClock {
    public static float Rate = 5f;
    public static int Slow() { return (int)(UnityEngine.Time.realtimeSinceStartup * Rate); }
    public static System.Func<int> Saved;
    public static string Use() {
      var d = (System.Func<int>)System.Delegate.CreateDelegate(typeof(System.Func<int>), typeof(SlowClock).GetMethod("Slow"));
      d(); Saved = ES2Access.Core.UI.Graph.KeyGraph.FrameCounter; ES2Access.Core.UI.Graph.KeyGraph.FrameCounter = d; return "slow " + d();
    }
    public static string Restore() { ES2Access.Core.UI.Graph.KeyGraph.FrameCounter = Saved; return "restored " + Saved(); }
  }
  ```
  then `SlowClock.Use()` and `SlowClock.Restore()`. If the clock was lost anyway, bind the
  `UnityEngine.Time.frameCount` getter the same way, or `POST /reload`, which rewires it. Check
  `GET /log?grep=threw` afterwards. Dividing `frameCount` instead ties the rate to the frame
  rate (the map runs at about 30 fps).
- Pause the mod's whole update: null `_modUpdate` on the `ES2Access.Loader.LoaderPlugin` instance by
  reflection, keep the delegate in a REPL variable, put it back. Harmony patches stay installed, so
  their cost is inside the "game alone" figure. To measure `Update` across REAL frames, pause it and
  drive it from a `POST /wait` predicate that reads `GC.GetTotalMemory` before and after
  `updM.Invoke(null, null)`; calling `Update` repeatedly within one frame skips the memoized build
  and frame-keyed work.
- `GC.GetTotalMemory` deltas UNDERCOUNT when the heap is tight or the total is small: small objects
  fill free slots in blocks already counted as used. It produced two wrong claims in this work (a
  per-key-press cost, and "two thirds of a build is scratch"). Reliable instead: allocation per call
  = room / number of calls between two collections; memory kept per object = the SLOPE between
  holding N and 2N of them across forced collections.
- Tight measurement loops grow the heap for the rest of the session, which lengthens every interval
  afterwards. Judge pause frequency only in a session where none was run. This is also why another
  session recorded 10-13 s intervals at the full rate.
- Key-to-speech latency: stamp each `POST /input` on the client, record `DateTime.UtcNow.Ticks`, the
  collection count and `ModEntry.Speech.LastSpoken`'s hash per frame in the game, match afterwards.
  Send on a FIXED schedule from separate threads: `/input` takes about 450 ms to answer and blocks
  through a pause, so a sender that waits for each reply never sends during one and reports no slow
  presses at all.
- A long eval outlives the dev server's 5 s reply window but still runs: assign its result to a REPL
  variable and read it back. A collection logger that costs nothing per frame: a `/wait` predicate
  that only appends when `GC.CollectionCount(0)` changes, tracking the heap peak in a variable;
  `/wait` caps at 60 s, so loop it from a background script.
- `POST /quit` hung again (process not responding, dev server dead after 150 s) and was terminated,
  as `docs/dev-loop.md` describes.
- A REPL class with `[DllImport("mono")]` statics compiles and works, which is how the heap-size
  exports were verified.
- Bytes per build through the REAL per-frame path (2026-09-23): reflect `GraphNavigator._graph`
  off `ModEntry.Navigator`, then loop `g.Invalidate(); g.Rerender();` counting calls between two
  collections and tracking the `GC.GetTotalMemory(false)` peak; bytes/build = (peak - floor after
  the first collection) / calls. `InspectRender` is NOT that path (it makes a fresh builder). The
  loop keeps every frame-keyed sweep warm, so it excludes sweep costs; the same loop before and
  after a change is still a fair comparison. Confirm one build per frame by swapping the
  `KeyGraph._renderCallback` field for a counting wrapper (`Func<GraphRender>` to a static method)
  over 120 frames from a `/wait` predicate; it read 123 builds in 120 frames.
- One HUD entry point alone: reflect `_hud` off the current screen, call the public entry point on
  a builder re-armed each iteration (`GraphBuilder.Rearm(null, null)`), same counting. A private
  helper taking the HUD's private `Cell` list is reached with `Activator.CreateInstance` over
  `typeof(List<>).MakeGenericType(nestedType)` held as `object`/`IList`. Costs under about 600 B
  per call need a cap of two to four million calls to force a collection; "no collection in N"
  means under room/N, not zero.
- A collection-to-collection interval is the only trustworthy rate figure. Mid-cycle
  `GetTotalMemory` growth undercounts (a one-minute window read 3.1 MB/s where the full cycle gave
  3.84), and a cycle that starts above the floor is shorter than a full one. Log with a per-frame
  `/wait` predicate that appends only when `GC.CollectionCount(0)` changes (room = peak minus the
  used figure right after the collection; pause = `Time.unscaledDeltaTime` on that frame); two
  back-to-back 60 s waits catch one full cycle when cycles are under a minute, three when they run
  longer. Compare builds on the SAME heap in one session: stash, build, `/reload`, measure, pop,
  build, `/reload` (about three minutes; the REPL classes must be re-declared after each reload).
- An `/eval` that outruns the 5 s reply window loses its result unless assigned: `var r = X();`
  then `/wait` on `false` for the expected time, then `/eval r`.
- From the Bash tool, `tasklist /FI ...` is mangled into a Git path ("C:/Program Files/Git/FI");
  poll the process from the PowerShell tool (`Get-Process -Id`). `POST /quit` exited cleanly in
  about 60 s this time.
