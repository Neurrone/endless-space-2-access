# World navigation — maps, zoom tiers, and fog

Making a game's *world view* keyboard-navigable: the map screen where the player selects
places, inspects what is there, and moves between them. Distinct from ordinary page UI
([ui-navigation.md](ui-navigation.md)) because the world is rendered by a camera whose
distance decides what is drawn, and because the world model knows things the player must
not hear ([fog](#fog-discipline)). Proven on ES2 Access's galaxy map (a node-graph world),
including the categorized scanner and free inspection cursor; only tile-signature skips and
audio cues remain unproven — see the last sections.

## World-as-graph: the cursor is the game's own graph

When the game's world is a node graph (star systems joined by lanes, cities joined by
roads), the keyboard model is a cursor over the game's *own* graph, not a 2D grid scanner:

- A **list stop** gives coarse selection (the player's places first, then the rest the game
  shows), with the camera panning to the focused node through the game's own
  "show me this" route.
- Each node is **expandable**: its children are the things the map draws *at* that node, in
  drawn categories (ES2: the system's planets, then its outgoing lanes), walked with the
  ordinary tree keys. Position text and category order come from what is drawn, per the
  standing doctrine. A thing's **parent is the place the game's model stores it** (a fleet
  keeps its lane; a probe keeps only a coordinate); where the model gives no place, give it a
  sibling row or region of its own — never a child of whatever is drawn nearest.
- **Edges are spoken as the map draws them.** An edge to an unrevealed node reads as
  leading somewhere unexplored — a complete phrase from the mod's string table — and is not
  traversable or activatable; a named destination is spoken only when the game draws that
  name.
- Activation follows the game's own click semantics for the node (open its management
  surface, enter its detail view), through deterministic handlers as always.

## Zoom ladders collapse into information surfaces

A camera zoom with N steps is presentation, not information structure. Enumerate the
game's real **information surfaces** — the distinct sets of drawn content (ES2: overview
labels / close-up orbital cards / the management page / the planet page / a discovery
reveal) — and model *those*, as screens or as content sources. Most zoom steps only fade
parts of one surface in and out; the mod's readout for a surface carries its full content
regardless of fade state (pixels, not widget flags, are the per-step truth — the fades are
often prefab-authored and invisible to code:
[reverse-engineering.md](reverse-engineering.md)).

Two rules keep every tier reachable:

- **Expansion moves the camera only while both tiers stay reachable.** "Go in" (tree) and
  "get closer" (camera) are different verbs — ui-navigation.md's expansion rule — so binding
  a camera move to the expand key is allowed exactly while an explicit zoom-out key still
  reaches the other tier with the branch open; otherwise whichever information tier the other
  distance shows becomes unreachable.
- **The camera gets an explicit control**, placed wherever fits the screen's design (ES2: an
  explicit zoom-out key, restoring the previous zoom step *at the focused node*, not at the
  camera's old position); and where a zoom step swaps the SUBJECT rather than the distance,
  that control is an adjustable widget ([widgets.md](widgets.md)).
  Node children then read **whatever the current distance draws**, switching
  content source when the close-up surface exists and falling back to the far readout when
  it does not — never a dead node.

## The camera is a focus visual

Move the camera through the game's own "show me this" route (every game with a "go to
event" button has one), with the game's own framing offsets — never by writing transforms.
Footgun class to expect: zoom-step setters that swap LOD/layer state *without moving the
camera* — always use the game's full request/force route. Verify camera behavior with
zoom-out/zoom-in **crop pairs**, never state readback: the step variable can say one thing
while the pixels say another. And read numeric limits (the last step, the detail
threshold) from the game code that compares against them, not from one observation — a
measured plateau is not a boundary
([making-screens-accessible.md](making-screens-accessible.md)).

## Fog discipline

The world model answers questions the renderer refuses to: adjacency APIs return
never-discovered nodes with full data, and name lookups resolve for anything. Route every
name and fact through the renderer's own visibility predicate — the same doctrine as
[making-screens-accessible.md](making-screens-accessible.md) §0, load-bearing here because
the map is where the model's omniscience leaks first. The filter is what needs the test.

## Overlay / scan modes (proven on ES2's scan view)

Strategy maps ship information overlays (ownership, trade, economy views) that recolor the
same map. Model them as a **mode flag plus a content-source swap on the existing
screens**, never as new screen models per mode; where the overlay's content varies with the
zoom layer, the mod selects an overlay type by driving the game's own layer/zoom state
deterministically. Overlay legends ("caption" panels) become a reviewable stop. One screen
serves every lens, and the lens's own **drawn header** is the reliable mode signal — the
captions and legend data can go stale while every lens's window reports itself shown.

## The categorized scanner (proven on ES2's galaxy map)

A scanner answers "what is near me, of kind X" without leaving the thing the player is
standing on. It is a **question, not a mode**: no activation key, nothing to exit, Escape
never involved — its chorded keys are simply live whenever the world view is focused and
inert everywhere else. Three tiers share one key pair, separated by modifier — category,
subcategory, instance — plus one "go to what it points at" key (wotr-access lineage; the
bare pair can stay with the game where the game uses it).

- **Rebuild and re-sort on every press; cache nothing.** The sort key is distance from
  where the player is reading, which moves with every arrow press. Snapshot everything the
  keystroke's *rules* can ask about, not just the current scope — the skip-empty rule is a
  question about scopes the player is not in.
- **The reference point is a ladder**, most specific first: the free inspection cursor if
  one is up, else the focused world stop's own place, else home. The go-to key feeds back
  into it, so a jump becomes the new place to look around from.
- **Cycles skip empty scopes; instance stepping wraps; a re-scope resets to the nearest
  instance** — a position in a rebuilt list is an index, not an identity. Each category
  remembers its own last subcategory; a first visit is "all". The "empty" line is then
  only reachable by standing still while the world empties the parked scope.
- **The first press announces without stepping** — nothing else announces a facility that
  has no entry. Scope lines stay terse ("Category: subcategory" — no counts); instance
  lines carry name, absolute position pair, the offset from the reference as **rounded
  components** ("9 south, 1 west" — the differences of the spoken pairs, so the numbers
  always reconcile with what the rows say), and "n of m"; a zero offset collapses to
  "here".
- **Affiliation is a taxonomy decision, not a lookup.** The game's own map coloring often
  answers a different question (ES2's lozenge calls every minor faction an enemy); the
  mod's categories are the project's own, membership is many-to-many, and any divergence
  from the game's coloring is written down where the next reader will hit it.
- **The scanner is the second enumeration of the world.** Feed it the same drawn lists and
  visibility gates the navigation tree uses — and where the two disagree, the disagreement
  IS a finding (ES2's found fleets no tree branch held, and map nodes the scanner had
  skipped). One trap in that rule: a drawn-label list may be culled by camera LOD; gate on
  the game's information predicate, not its culling, or a whole category vanishes with the
  zoom.
- **Extract the stepping rules into an engine-free, unit-tested module.** Every failure
  here is inaudible — an empty landing, a step that stops dead, an index past a shrunk
  list all sound like "found nothing".

Its partner, the **free inspection cursor** (a togglable mode, unlike the scanner): a
square cell on the world plane in odd sizes, stepping by exactly its size so cells tile
with half-open bounds; the center stays on the integer coordinates the mod speaks. Entry
lands at the focused stop's *own* position; the camera follows every move and a drawn
frame marks the cell; each cell speaks its coordinates, contents, crossing edges, and how
much of it is hidden (as a count of hidden unit tiles, so the player can shrink to
localize); exits restore both focus and camera. Enter acts only when the cell holds one
unambiguous target, silently otherwise — the key is pressed speculatively mid-sweep.

## Tile worlds (partly proven)

The exploration-cursor + categorized-scanner pair (Factorio Access lineage) is no longer
tile-world-only: ES2 shipped both on a graph world as *complements* to the graph cursor,
and the sections above are written from that shipment. Still unproven in this doc family:
tile-signature skip navigation and spatial audio cues — write those from the first game
that ships them.

## Source exemplars

ES2 Access (models to imitate — they name ES2 types): `GalaxyHudScreen.cs` (the systems
stop, expandable system nodes, drawn-surface switching for planet children, lane wording),
`GalaxyViewLevels.cs` (view-level queries, camera routes, zoom-step handling with the
limits read from the game's own comparisons), `GalaxyScanner.cs` + `Core/UI/ScannerCursor.cs`
(the scanner's live half and its engine-free stepping rules), `GalaxyInspect.cs` +
`Core/UI/InspectGrid.cs` (the inspection cursor and its cell geometry).
