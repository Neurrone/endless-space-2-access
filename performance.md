# Per-frame performance: findings and how to measure

Written 2026-09-09 from a review of the 19 commits `e01a1a2..b6d971d` against the per-frame
rules in `docs/generic/performance.md`. The two sibling mods share the graph engine, and the
sibling (Songs of Conquest Access) now measures every screen's build and rejects anything over
one millisecond; this file brings the same discipline here. Delete the findings as they land;
keep the measuring section, or move it into `docs/dev-loop.md` once it has been used.

## Findings, most costly first

Findings 1-3 of the 2026-09-09 review landed on main (the dossier's relation states stamped
in one pass, the curiosity door picked out of the memoised sweep, the adrift phrase inside
its part) and are deleted per the rule above. The full audit of every screen that followed,
with its mechanism-level plan and what has landed, is `performance-audit.md` (untracked).

1. `ES2Access/Screens/AdvancedBattleReportScreen.cs` - `Chips(card)` runs per card per frame
   and reads `card3D.AllShips`, whose getter allocates a new array on every get, plus a list
   per card and a `Visible` ancestor walk per chip. Bounded (cards x ships, single digits);
   it has one caller, once per card per frame, so a memo cannot hit - fold it into the build
   measurement of a battle report rather than fixing it blind.

Not findings, checked: `3aa2e1e` (the reload cursor seat) holds three fields that only decide
where the cursor lands once; nothing in the range adds hook-carried state a build depends on.

## Measuring a screen's build

The dev server's `/wait` frames-vs-elapsed ratio says that something burns frames; this says
which screen and how much. On the screen in question, with the game running:

```
POST /eval?settle=0&speech=0
var scr = ES2Access.ModEntry.Screens.Current; var sw = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 200; i++) { var b = new ES2Access.Core.UI.Graph.GraphBuilder(); scr.Build(b); b.Build(); } sw.Elapsed.TotalMilliseconds / 200.0
```

is one build in milliseconds. Run it twice; the first run of a cold path includes JIT. Take the
best of three or four. A build should be under one millisecond; over that is a finding to fix,
not a note. Time a suspect helper the same way through the screen's own fields, and time the
whole per-frame cost by invoking `ModEntry.Update` through reflection in the same loop.

Before and after every fix: `GET /gui/graph?buffers=1` on the screen (with `edges=1` when the
tree shape is what changed), diffed byte for byte, and the build number in the commit body.

The number without a game is a `Stopwatch` around the same loop in a test, which catches
regressions in `Core/` projections but not Unity walks.

## What CLAUDE.md should say

Add a `## Performance` section to `CLAUDE.md` (Conventions is the neighbour) with these rules,
which are what the findings above violate:

- A screen's `Build` runs every frame. Nothing inside it, inside an `IsActive`, `ScreenName` or
  tooltip-existence predicate, or inside an argument evaluated eagerly for it, may walk a
  subtree (`GetComponentsInChildren`, `GetComponentInChildren`, `GetComponentInParent` per
  row), scan the scene, iterate a whole game collection, or invoke a game refresh. That work
  belongs in a `FrameSweep` keyed on the frame, in a snapshot keyed on something read from the
  game, or inside the part's read-at-announce lambda.
- Text for a row is built when the row is read, never for every row per frame: `Func<string>`
  parts, not strings.
- A cache on a build path is keyed on game state (frame count, object identity, a count, a
  game-owned generation), never on a hook having fired. Remembered state may delay an
  announcement; it may not change what is built or how much a build costs.
- Before handing over a screen, measure one build with the recipe above and put the number in
  the commit message. Over one millisecond is a finding.
- A source lint, not a reviewer, guards the walk sites: every `GetComponentsInChildren` /
  `GetComponentInChildren` / `FindObjectOfType` outside a `FrameSweep` declaration or a
  constructor is on an allowlist with a why-comment, and a lint failure ends with one line
  saying that an exception must be reported to the owner.
