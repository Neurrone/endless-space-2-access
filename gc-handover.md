# GC pause handover

State of the investigation into the second-long freezes, as of 2026-09-21. Written so a fresh
session can resume without the conversation. Work items 1-3 of section 5 were implemented on
2026-09-21 (status there); the rate itself is unchanged. Every experiment below was done from the
dev server's REPL and is gone on a game restart.

Figures come from one machine and one mid-game save. They characterise the mechanism; the absolute
numbers scale with how much live data the save holds. Line numbers drift, so sites are named by
symbol first. Cites marked (sub) came from research subagents and were not re-verified.

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
- "Items 1-3 look good" (2026-09-20), meaning work items 1-3 in section 5, including the `Core/`
  change described there. Re-confirm before editing `Core/`: CLAUDE.md requires explicit approval
  for it, and the full walk needs approval each time.
- HUD and scratch-allocation optimisations (section 7) are deferred; at 5 builds/s they are worth a
  few seconds of interval each.
- Every new key binding still needs approval; none is proposed here.

## 5. Work plan, in order

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
2. **The engine's landing path** - DONE (0764a43), full walk NOT yet run (ask first). `Reach` opens
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
   **System-management page turn** - measured, NOT changed: the restore to the same card index
   passes identically at the normal clock and at 1 build/s. `_planets` is refreshed only by `Build`
   (through `Whole()`/`Drawing()`, which is also the readable "window is bound to the new system"
   condition), so the staleness is real but did not produce a failure. Open from that stage:
   (a) PRE-EXISTING, normal clock: turning to a system with FEWER planets than the card index the
   cursor was on announces nothing and leaves the cursor on the previous system's key with that
   card's expansion orphaned - the fallback never fires; (b) at 1 build/s each successful restore
   spoke one extra line first, the PREVIOUS page's tooltip text - unattributed (no old-assembly
   slow-clock reference was taken), belongs with item 4/5.
4. **Smaller render-dependent waits** - NOT started; design awaits the owner. Read on 2026-09-21
   (code only, not live): the map's `_binding` / `_settling` holds and the notification `_seating`
   hold end by letting `EnsureFocus` announce or land off the STANDING render, whose structure (leaf
   or group, "2 of 10") can predate the end of the bind - proposed generic fix: `EnsureFocus`
   invalidates on the frame a screen's `BetweenViews` or `LandingSuspended` turns false (outside
   `Core/`). The locate landing (`GalaxyHudScreen.Landing.cs`, `LocateFrames`) reads `_systems`, which
   only a build fills: invalidate when a locate is first seen. No fix needed: `GalaxyTree.ReadBand`
   (detection and the reconcile it drives happen in the same build), `ZoomLadder.Claim` (re-asks every
   build, arriving page builds at once), the centred-system seat (waits on the camera, ends in a
   landing). Walk hazards met today: a notification popup re-raised between runs is not closed by
   `walks/cs/drain.cs` and silently skips the galaxy leg; `galaxy/map-tree` self-diffs by 500-600
   lines between two runs of one build depending on whether the camera has drawn the planet cards,
   and inherits leftover expansions unless `walks/cs/reset.cs` runs first.
5. **The rate change itself.** `KeyGraph.FrameCounter` is an injected clock (`ModEntry` wires
   `Time.frameCount`); a time-based clock in its place gives a global idle rate with no `Core/` edit.
   Design choice still open between two variants:
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
6. **Heap room check** (section 6), its own small feature.
7. Then measure a normal session and decide on section 7.

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

## 7. Deferred optimisations, with what each would buy

Of about 360 KB allocated per empire-screen build, about 238 KB is kept by the render and about
122 KB is scratch.

- `GraphBuilder.Build()` allocates about 84 KB and keeps about 11 KB: per-build temporary
  dictionaries and lists for wiring edges (nodes per stop, run lists, column map, sibling groups, row
  segments). Reusing them across builds should recover most of about 73 KB, about 20% of a build on
  every screen. `Core/`.
- `NodeVtable` has 35 fields and costs 362 bytes bare; moving rarely used handlers into an object
  allocated on demand saves about 200 bytes per node, about 15% of a build. `Core/`.
- The framework floor for a button node is about 2.0 KB (vtable 362, `GraphNodes.Button` 454,
  `AgeWidgets.Point` 270, `ControlId` with its key string 152, node + `Nodes.Drawn` + `AddItem` +
  share of `Build` about 800). HUD nodes measure 2.5-4.2 KB each. Reusing the HUD's vtables keyed on
  widget identity could take its 107 KB to roughly 35-40 KB (estimate, not prototyped).
- At 5 builds/s each of these moves the interval by a few seconds (55 s to about 58-60 s), so the
  owner deferred them. They also help the builds that key presses cause.

The commit "A node keeps its four arrow-key edges in four slots" (9d83cd6) was an earlier step of the
same kind: about 20% at the full rate.

## 8. Measurement methods and traps

All via `POST /eval` (see `docs/dev-loop.md` for the REPL's own gotchas).

- Slow the build clock without code changes:
  `ES2Access.Core.UI.Graph.KeyGraph.FrameCounter = () => (int)(UnityEngine.Time.realtimeSinceStartup * 5f);`
  Restore with `() => UnityEngine.Time.frameCount`. A hot reload restores it too. Dividing
  `frameCount` instead ties the rate to the frame rate (the map runs at about 30 fps).
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
