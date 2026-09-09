# Performance

An accessibility mod runs *inside* someone's game, every frame, forever. On older Unity Mono
runtimes the garbage collector (Boehm, non-generational, stop-the-world) turns steady
allocation churn into audible hitches — and a mod for blind players competes with the speech
it produces: a hitchy game is also a stuttery screen reader. These rules are distilled from
how the shipped mods keep their per-frame cost invisible, and from a full audit of one of
them (ES2 Access, 2026-09-09: 243 findings across every screen, fixed and proven in three
rounds; see "Measuring" for the proof).

## Never scan the scene per frame

`Resources.FindObjectsOfTypeAll`, `GameObject.Find`, and `GetComponentsInChildren` from a
root are O(scene) — fine once, catastrophic at 60 Hz against a scene with tens of thousands
of objects. Scans belong in exactly three places:

- **On-demand introspection** (dev-server dumps, an explicit user query) — capped and
  depth-limited.
- **One-time discovery on a lifecycle event** — when a screen's "became ready" hook fires,
  find its widgets once, keep the references, and re-validate them cheaply afterwards
  (Unity object destroyed-check) instead of re-finding.
- **A walk memoised for one frame and shared by every asker**
  ([`FrameSweep.cs`](src/engine-example/FrameSweep.cs)). An immediate-mode screen has no
  lifecycle event to hang discovery on: its build runs every frame, and the same subtree is
  asked several times in one frame — once by "is this page still mine", once by the build,
  again by whatever an announcement part resolves to. The sweep is keyed on (root, frame
  count) and nothing else, which is what makes it safe over pooled tables that add and
  retire rows between frames: the first call of the next frame drops the whole table, and
  there is nothing to remember to clear. Its `includeInactive` flag is part of the answer,
  so one sweep never serves both questions. The ES2 audit found 70 raw walks on build paths;
  52 moved behind a sweep, and the rest carry a one-line reason (a constructor, an action
  handler, a per-row ancestor question the game's own table shape makes correct).

If the game gives no event for something, poll a *single cheap field*, not the hierarchy —
Tangledeep's focus watcher edge-detects one pointer the game already maintains, precisely
because the game fires no event when it goes stale.

**And before declaring a lookup too expensive, check what the game already indexes.** A game
that focuses, locates, or refreshes its own widgets keeps exactly the map an adapter needs
(ES2's tech screen holds a public technology→widget dictionary — the one its own Ctrl+Click
locate uses; its star-system page holds one typed container per panel, so a `GetComponentInChildren`
over the whole window was answering a question a field already answered). A feature cut
"because resolving it means an O(n) walk per frame" is often an unread public API away from
O(1); the cost argument should dissolve before it becomes a design constraint.

**Guard the sites with a lint, not a reviewer.** Every scene-walk call outside a constructor
or the sweep's own file sits on an allowlist with a `// walk:` comment saying why it is
one-time or bounded, and the lint fails both ways (an unlisted site, and a stale entry that
would silently vouch for the next line to match it); its failure message ends by saying an
exception must be reported to the owner. ES2: `ES2Access.Tests/Lint/SceneWalkLintTests.cs`,
the same allowlist machinery [tooltips.md](tooltips.md) uses for tooltip reads.

## Prefer events; edge-trigger the rest

Hook the game's own lifecycle signals (visibility changes, event buses, log sinks) and do
work when they fire. For per-turn systems in turn-based games, edge-trigger on the turn
counter — Tangledeep's combat radar renders one audio timeline per turn by comparing
`turnNumber`, rather than recomputing per frame and deduplicating.

A cache on a build path is keyed on game state — the frame count, an object's identity, a
count, a game-owned generation — never on a hook having fired. Hook-carried state may delay
an announcement; it may not change what is built or how much a build costs, because the hook
and the build are two code paths (first entry, hot reload, a missed call) that can disagree.

## Snapshot + reconcile for expensive views

Anything that aggregates the world (a scanner over all map entities, a category browse)
builds a **snapshot on demand**, holds it frozen until an explicit invalidation (map change,
turn elapsed, user rescan), and reconciles selection identity across rebuilds. The stale-data
risk is handled surgically: membership and order stay frozen, but the *displayed* values of
the selected item are re-queried live at speak time — "no stale speech" without touching the
other N hundred entries. Where per-frame tracking of a live population is genuinely needed,
keep one stable proxy object per entity and diff the game's pools against it (wotr-access's
world model) — reusing proxies avoids reallocating the set every frame and gives persistent
attachment points (looping sounds).

**The key of a memo names the game state it was computed from, and the default is the
frame.** A frame-keyed memo can never change an answer (the game does not move between two
reads in one frame) and already collapses the two or three reads a frame makes of the same
thing. A longer key — the turn, a count, a generation — is taken only with evidence from the
game's own code that nothing else moves the value, and the evidence goes in the commit body.
In the ES2 audit that test refused the turn key three times where a comment had promised it:
influence contests re-run on a diplomatic change, marketplace prices move on a buy, a cost's
"turns remaining" moves on a buyout. It accepted the turn once, for rank-history columns the
game freezes at turn end. A memo keyed on a fired hook is the same mistake in another shape.

Two smaller shapes recur. A per-frame gather that must stay per-frame (the galaxy's systems,
at every zoom band) still need not re-sort: compare this frame's membership against last
frame's (a count plus an ordered identity compare) and keep the sorted lists when equal.
And a row that finds its widget by scanning a pool of labels with a predicate — allocating a
closure per row and walking the pool per row, quadratic in the map — reads a dictionary
built once per frame beside the sweep that produced the pool.

## Bound immediate-mode rebuilds

Rebuilding an accessible UI tree from live state every tick is a correctness win (no stale
focus by construction) — but its cost must be proportional to the *open screen*, not the
world. Menu-sized trees rebuilt per tick are nothing; never feed an immediate-mode builder an
unbounded world query. For big tabular screens, build from the game's own already-computed
lists, not from scene traversal.

## Row text is built when the row is read

A node declares its words as `Func<string>` parts; the engine calls them for the focused
node only. The most common violation in the audit (about 110 sites reading a widget's whole
text eagerly and 460 localising a caption eagerly, by call shape) was the pattern
`string x = Read(widget); Add(() => x)`: the text is composed for every row on every frame
and then composed again by the lambda when the row is focused. Move the read inside the
lambda. Where the string is used for something else — a region name, a pushed context, a
comparison that decides the node's structure — it stays eager, and the commit says so.

The other half is the **existence gate**: "declare this row only if it has words" was being
answered by composing the whole text (a depth-six walk, a localisation and a cleaning pass
per label, a list and a string builder) and testing it for emptiness. The gate is a
predicate that walks the same nodes with the same visibility tests and stops at the first
label whose *cleaned* text is non-empty — the raw text is not enough, since cleaning turns an
icon-only caption into nothing — and allocates nothing. Its equivalence to the composed test
is an argument, not an assumption: a builder of one non-empty fragment is non-empty, and the
dedupe cannot empty it.

## Resolving a row's widgets and its layout

Two helpers every table runs per cell per frame deserve their own rules:

- **Tooltip resolution.** "Which tooltip does this cell own" was a depth-four subtree walk
  that asked the full ancestor-visibility test at every node — subtree times ancestry, per
  cell, per frame, with no memo. Gate the entry root once with the full test, ask each child
  only its own flag below it (the flags the ancestor walk reads, no more — adding an alpha
  test the old code never made would change the answer), and memoise the resolved list per
  (widget, reach, depth, frame). The existence predicate itself is cheap and must stay so: a
  tooltip's presence is a field read, and no existence question may recompose the tooltip
  through the game's pipeline.
- **Reading order.** Sorting cells into rows and columns called a native `GetComponent` per
  ancestor step, twice per comparison. Measure each cell once (its laid-out rectangle, and
  whatever else the comparison reads) into a scratch array, then sort on the measurements —
  with the *same* sort call and the same tie-breaks, because an unstable sort under a
  different comparison order would reorder equal cells and change what the player walks.
  The cached field an engine keeps (`AgeTransform.AgeControl`, `AgePrimitive` on the AGE
  framework) replaces the native lookup wherever the decompile shows it is assigned once.

## The engine's own frame

The navigator and the graph builder run for every screen, so their silent frame — nothing
pressed, nothing moved — is the floor every page pays. The audit found the ES2 engine making
eight O(N) passes per frame with nothing comparing the render to the previous one. The
fixes, in order of payoff, are generic:

- Data a fallback needs only when a node has *died* (the previous traversal order, for
  "nearest survivor") is computed lazily by that reader, not on every reconcile.
- Focus recovery tries the O(1) structural lookup before the O(N) subject scan, accepting the
  hit when its subject still matches; under one-object-one-node the two orders agree
  everywhere.
- A remembered position for a panel the screen is not showing is skipped before its
  order is re-walked, instead of scanning every frame forever.
- Two wiring passes that each grouped the declared nodes by stop share one grouping.
- The tooltip dedupe resolves each earlier part's text once per compose, not once per
  tooltip line (which had been re-running a simulation cost query per line).
- The per-frame lists the tick allocates are two swapped buffers; a method-group delegate
  is not allocated per frame.
- Chord names and usage hints are memoised on the input table's binding generation and the
  language, not re-localised per frame.

Count the focused node's cost explicitly: label, role, value, cost, tooltip and hint parts
composed for the readout, the live ones again for the change watch, and every allocation
inside them lands on the silent frame.

## Allocation discipline in hot paths

For code that runs every frame (pumps, watchers, claim checks):

- No LINQ, no closures/lambdas that capture (each is an allocation on old Mono), no string
  concatenation — compose strings only when something will actually be spoken.
- Reuse builders and buffers; a speech line allocates when spoken, which is fine — speaking
  is rare on the frame scale. The sin is allocating on the *silent* frames.
- Cache reflection lookups (`FieldInfo`/`PropertyInfo`/compiled accessors) at startup, never
  `GetField` per frame. Cache per-type results in dictionaries (the dev GUI dump memoizes its
  text-property lookup per component type). This is hot-reload-safe as long as the cache
  lives on the same side of the reload boundary as the types it describes: mod-side caches of
  game types are the normal case (game assemblies never reload, and the cache dies with the
  mod assembly and rebuilds once per load). The converse — a *host*-side cache of mod types
  or delegates — is banned by [hot-reload.md](hot-reload.md)'s teardown checklist.
- Watch hidden allocators: `foreach` over some non-generic collections or over an interface
  (the enumerator boxes), boxing value types into `object`, `params` arrays, `Enum.ToString`,
  Unity's `Object.name` (a fresh managed string on every get) and `Type.Name`. A node key
  built by concatenating a boxed GUID and an index per row per frame is all of these at once;
  a switch to constant strings, or a key minted once per entity, is the replacement.
- A game getter can allocate or scan on every get (an `All*` property returning a fresh
  array, a LINQ query behind a `Get*`). Read the decompile before calling one per row per
  frame, and read it once into a snapshot.
- **Anything behind a live announcement part runs at 60 Hz on the focused control.** A live
  part is re-evaluated and string-compared every frame to detect change, so a query, a scan,
  or an allocation behind one is a steady per-frame cost no profiler run will obviously
  attribute to the mod. Expensive lookups go behind on-demand parts (resolved on focus or on
  request), never live ones. So does everything else the focused node says: the navigator
  composes the node's WHOLE readout every frame to decide whether the review buffer still
  matches it, so an on-demand part on the focused control costs what a live one costs. Only
  SECTIONS are landing-only — they resolve when the buffer is actually refilled — which is
  where a part that does real work belongs. Work the readout itself must SAY has nowhere to
  hide: memoize it on the state it was computed from — declaring a part un-watched stops it
  being ANNOUNCED, never being ASKED. Three code comments in ES2 claimed otherwise and were
  wrong; a focused technology was re-reading every arc on the wheel, and a focused priced
  tile was running the treasury's turns-remaining computation, sixty times a second.

## Stagger and cap everything unbounded

Work whose size the game controls gets a cap or a stagger: audio sweeps play one ping per
interval with the gap scaled to crowd size (total sweep time roughly constant); dev dumps cap
node counts; event narration condenses before speaking rather than queueing one line per raw
engine event. When a cap drops content, say so — silent truncation reads as completeness.

A keypress may cost a walk of the world; it may not cost N of them. A cursor skip over N
empty cells was reading every star system once per candidate cell; the read splits into a
gather (everything asked of the game, once per press) and a per-cell test (arithmetic on the
gathered numbers). Objects the mod creates per entity (a hidden tooltip carrier per dossier)
get a ceiling with least-recently-used eviction and a drop on campaign change, never "kept
for the session".

A host call that MAY log is a per-frame cost of its own and can be an uplink: check what the
host does with a log line (disk, telemetry) before provoking one per frame — prefer the quiet
overload or a direct registry read.

**Pick a wait's unit by who is waiting.** A gap the player HEARS — a repeat interval, a
re-announce throttle, a debounce before speaking — is wall-clock seconds; a settle that waits
for the game's own next refresh or layout pass is frames. The two are interchangeable only at
60 Hz, which is exactly where testing happens: a frame-counted speech throttle turned into
seven silent seconds on a 13 fps page.

## Measuring

Cheap signals, no profiler attached: the dev server's `/wait` returns frames-vs-elapsed (a
20 fps ratio during normal play means something is burning frames); long frames during
loading are normal and expected (main-thread requests time out — that is latency, not a
leak). A game's idle frame rate can be low on its own (ES2 idles at 10 to 21 fps on a
capable machine), so the ratio is a change detector, not a budget. When a hitch correlates
with your mod, suspect per-frame allocation first, scans second.

**The budget is one build per screen, timed through the navigator's own render** (the real
expansion set and the existence gate — a bare builder measures a smaller tree): a
`Stopwatch` around two hundred renders from the REPL, best of three after a warm-up run
that includes JIT. Under one millisecond is the bar; over it is a finding, not a note. Time
the whole per-frame cost by invoking the mod's `Update` in the same loop, and a suspect
helper through the screen's own fields. Before handing over a screen, put the number in the
commit body.

**The proof that a cost change changed nothing is a dump pair, not an argument.** With the
old build loaded, walk every screen the regression harness reaches and save the accessible
tree with its buffers; build, hot-reload in the same game process, walk again, diff. The
total must be zero. A non-zero total is classified before a commit is blamed: re-walk the
differing family on the same build, since a game's tutorial sequence advancing mid-walk or a
leftover tooltip window at a station both produce self-diffs; a difference that survives is
bisected by deploying intermediate commits. Size the proof to the blast radius: a shared
helper or an engine change takes the full walk; an addition to one screen takes that screen's
dump. ES2's three rounds over 62 commits each diffed at zero across 112 dumps and 22 tooltip
captures.

The number without a game is a `Stopwatch` around the same loop in a test, which catches
regressions in the engine's projections but not scene walks.
