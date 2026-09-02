# ES2 interaction language — the rulings the code does not carry

Which keys exist, what each one does, which layer a screen sits on and which chord a row of the
Controls tab draws are all facts of the source (`ES2Access/ModEntry.cs`, `ES2Access/UI/Input/`,
`override int Layer` under `ES2Access/Screens/`) and of the doc comments beside them. Only the
rulings with no home in the code are written down here. Bindings and layer numbers need owner
approval before they land.

## Layers

**A layer number is allocated by the main agent when a stage is briefed, never claimed inside a
stage** — pipelined stages cannot see each other's claims, and three of them once picked the same
number independently.

**Mutually exclusive screens may share a number** (owner rule 2026-08-12): a window on the game's
own exclusive modal stack voids any layer constraint against its stack-mates.

## Gestures

**Enter is click parity everywhere**, the destructive clicks included, and there are no
mod-invented action menus: a control's extra buttons are child nodes. The two things that displace
a node's click are a live drag landing on a control that takes the cargo and a targeting cursor the
game has armed.

**Each of the mod's chords means the game's own gesture and nothing else** — the right click, the
Alt-click, the Ctrl-click, the second click. The Alt-click is the one chord whose keys are not its
gesture's (Alt+Enter is Unity's own fullscreen toggle, below every managed layer), and the cost of
that move is that a game handler reading a physically held Alt no longer sees it: any such click
must be WIRED, never left to the plain-click fall-back.

**There is no reorder chord**: moving an item within its list is a drag like any other.

**Usage hints are hand-picked, not a policy.** There is no runtime dedup, so each new hint is an
owner decision about that one context, and a context whose own game tooltip already states its
gesture stays silent. Hints are buffer-only; the few control NAMES that carry a chord are read on
every landing, which is the cost the owner accepted for them.

## Authoring cautions

**Structural keys are paths, and that is load-bearing**: landings read a target's ancestry out of
the `/`-separated key, so renaming a key's HEAD silently breaks every programmatic landing into
that branch.

**Adding a tree LEVEL obliges the type-ahead scope to grow a range for the newly-hideable tier** —
a scope that does not loses the tier from search with nothing in any dump to show it.
