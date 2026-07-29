# Performance

An accessibility mod runs *inside* someone's game, every frame, forever. On older Unity Mono
runtimes the garbage collector (Boehm, non-generational, stop-the-world) turns steady
allocation churn into audible hitches — and a mod for blind players competes with the speech
it produces: a hitchy game is also a stuttery screen reader. These rules are distilled from
how the shipped mods keep their per-frame cost invisible.

## Never scan the scene per frame

`Resources.FindObjectsOfTypeAll`, `GameObject.Find`, and `GetComponentsInChildren` from a
root are O(scene) — fine once, catastrophic at 60 Hz against a scene with tens of thousands
of objects. Scans belong in exactly two places:

- **On-demand introspection** (dev-server dumps, an explicit user query) — capped and
  depth-limited.
- **One-time discovery on a lifecycle event** — when a screen's "became ready" hook fires,
  find its widgets once, keep the references, and re-validate them cheaply afterwards
  (Unity object destroyed-check) instead of re-finding.

If the game gives no event for something, poll a *single cheap field*, not the hierarchy —
Tangledeep's focus watcher edge-detects one pointer the game already maintains, precisely
because the game fires no event when it goes stale.

## Prefer events; edge-trigger the rest

Hook the game's own lifecycle signals (visibility changes, event buses, log sinks) and do
work when they fire. For per-turn systems in turn-based games, edge-trigger on the turn
counter — Tangledeep's combat radar renders one audio timeline per turn by comparing
`turnNumber`, rather than recomputing per frame and deduplicating.

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

## Bound immediate-mode rebuilds

Rebuilding an accessible UI tree from live state every tick is a correctness win (no stale
focus by construction) — but its cost must be proportional to the *open screen*, not the
world. Menu-sized trees rebuilt per tick are nothing; never feed an immediate-mode builder an
unbounded world query. For big tabular screens, build from the game's own already-computed
lists, not from scene traversal.

## Allocation discipline in hot paths

For code that runs every frame (pumps, watchers, claim checks):

- No LINQ, no closures/lambdas that capture (each is an allocation on old Mono), no string
  concatenation — compose strings only when something will actually be spoken.
- Reuse builders and buffers; a speech line allocates when spoken, which is fine — speaking
  is rare on the frame scale. The sin is allocating on the *silent* frames.
- Cache reflection lookups (`FieldInfo`/`PropertyInfo`/compiled accessors) at startup, never
  `GetField` per frame. Cache per-type results in dictionaries (the dev GUI dump memoizes its
  text-property lookup per component type).
- Watch hidden allocators: `foreach` over some non-generic collections, boxing value types
  into `object`, `params` arrays, `Enum.ToString`.

## Stagger and cap everything unbounded

Work whose size the game controls gets a cap or a stagger: audio sweeps play one ping per
interval with the gap scaled to crowd size (total sweep time roughly constant); dev dumps cap
node counts; event narration condenses before speaking rather than queueing one line per raw
engine event. When a cap drops content, say so — silent truncation reads as completeness.

## Measuring

Cheap signals, no profiler attached: the dev server's `/wait` returns frames-vs-elapsed (a
20 fps ratio during normal play means something is burning frames); long frames during
loading are normal and expected (main-thread requests time out — that is latency, not a
leak). When a hitch correlates with your mod, suspect per-frame allocation first, scans
second.
