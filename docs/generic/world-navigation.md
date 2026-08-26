# World navigation — maps, zoom tiers, and fog

Making a game's *world view* keyboard-navigable: the map screen where the player selects
places, inspects what is there, and moves between them. Distinct from ordinary page UI
([ui-navigation.md](ui-navigation.md)) because the world is rendered by a camera whose
distance decides what is drawn, and because the world model knows things the player must
not hear ([fog](#fog-discipline)). Proven on ES2 Access's galaxy map (a node-graph world),
including the categorized scanner and the spatial cursor; only tile-signature skips and
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

- **Expansion moves the camera only while both tiers stay reachable** —
  [ui-navigation.md](ui-navigation.md)'s expansion rule, at its sharpest here.
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
while the pixels say another. And read numeric limits (the last step, the detail threshold)
from the game code that compares against them — the plateau-is-not-a-boundary rule in
[making-screens-accessible.md](making-screens-accessible.md) §1.

## Fog discipline

Route every name and fact through the renderer's own visibility predicate — the fog doctrine
of [making-screens-accessible.md](making-screens-accessible.md) §0, load-bearing here because
the map is where the model's omniscience leaks first.

## Overlay / scan modes (proven on ES2's scan view)

Strategy maps ship information overlays (ownership, trade, economy views) that recolor the
same map. Model them as a **mode flag plus a content-source swap on the existing
screens**, never as new screen models per mode; where the overlay's content varies with the
zoom layer, the mod selects an overlay type by driving the game's own layer/zoom state
deterministically. Overlay legends ("caption" panels) become a reviewable stop. One screen
serves every lens, and the lens's own **drawn header** is the reliable mode signal — the
captions and legend data can go stale while every lens's window reports itself shown.

## The categorized scanner

A scanner answers "what is near me, of kind X" without leaving the thing the player is
standing on.

- **A question, not a mode**: no activation key, nothing to exit, Escape never involved.
- **Its keys belong to the world-view WIDGET, not the screen** — live only while the focused
  stop IS that widget, and gated on that stop in BOTH the claim and the handler, since an
  unclaimed key still reaches a screen-level any-key hook. Leaving the widget **suspends**
  the keys and resets nothing, so the next press resumes rather than re-announcing.
- **Rebuild and re-sort on every press; cache nothing** — gather per press, compose per row:
  a description built for every result and read for one is the scanner's per-frame
  allocation. The sort key is distance from where the player is reading, which moves with
  every arrow press, and the snapshot must cover everything the keystroke's *rules* can ask
  about, not just the current scope — the skip-empty rule asks about scopes the player is
  not in.
- **Cycles skip empty scopes**; a position in a rebuilt list is an index, not an identity, so
  re-seat on the same thing by a stable key.
- **Every press that lands says where it took the player**, the first press included — it
  announces without stepping and still reads the thing it is parked on. A press that stops at
  the scope name reads as broken silence.
- **Affiliation is a taxonomy decision, not a lookup.** The game's own map coloring often
  answers a different question; the mod's categories are the project's own, membership is
  many-to-many, and any divergence from the coloring is written down where the next reader
  will hit it.
- **The scanner is the second enumeration of the world.** Feed it the same drawn lists,
  visibility gates and landing helper the navigation tree uses; where the two disagree, the
  disagreement IS a finding. Gate on the game's information predicate, not its camera
  culling, or a whole category vanishes with the zoom.
- **Extract the stepping rules into an engine-free, unit-tested module.** Every failure here
  is inaudible — an empty landing, a step that stops dead, an index past a shrunk list all
  sound like "found nothing".

## The spatial cursor — when the tree cannot answer "what is over there"

A tree of a map answers "what things exist and what is next to which"; it cannot answer
the question a sighted player settles at a glance — what lies in *that* direction, *that*
far away, whether or not anything is there. Two games (Songs of Conquest's adventure map,
ES2's galaxy — `songs-of-conquest-access` `TileSkipNavigator.cs`, ES2 `Core/UI/InspectGrid.cs` +
`CellSkip.cs`, both unit-tested off-engine) independently converged on the same answer: a
**cell cursor** — a square of map the arrows move, speaking position first and contents
second. The rules both arrived at:

- **The cell is a mode OF the map widget, not of the screen.** Off the map stop it
  suspends (keeping place, size and its drawn square) rather than ends — a mode that
  claims arrows screen-wide takes the screen's sliders with them. Its arming key is a key of
  that widget too. Anything that removes the map ends the mode, and says so.
- **Odd sizes only, step = the cursor's own size.** The centre stays a whole coordinate
  pair and the cells tile with half-open bounds: no strip of map is skipped or heard twice.
  An empty cell speaks its coordinates and stops — the pair alone IS "nothing here", and a
  word for empty would be most of what a sweep says.
- **Entry, feedback, exit.** Entry lands at the focused stop's *own* position; the camera
  follows every move and a drawn frame marks the cell; each cell speaks its coordinates,
  contents and crossing edges; exits restore both focus and camera.
- **Absence the sighted player reads from a wash of color must be spoken explicitly.**
  Fog-of-war is the type case: "nothing there" and "nobody has ever seen there" are
  opposite answers that sound identical as silence. Say how much of the cell is hidden as a
  count of hidden unit tiles, so the player can shrink the cursor to localize it.
- **Refusals split by intent**: a key pressed speculatively mid-sweep (grow past the
  ladder's end, act on an empty cell) refuses silently-but-consumed; a deliberate move
  that cannot happen (off the map's edge) refuses with a word.
- **Skip-to-the-next-difference** (Shift+arrow in both games): compute a **signature** of
  the origin cell — the identity set of everything the cell's reading names, plus
  bucketed states for continuous properties (three-state fog, never the raw count: a raw
  count stops at every step along a gradient), coordinates excluded — then step cell by
  cell and land on the first whose signature differs **from the origin**, not from the
  predecessor. Nothing different all the way out lands on the last in-bounds cell; not
  one step possible is the edge refusal. Say how many alike cells were passed, then read
  the landing.
- **Travel by contents** (jump to a lane's end, a crossing unit's destination): act only
  when the cell's answer is unambiguous, refuse silently otherwise — the key is pressed
  speculatively mid-sweep — never exit the mode on a refusal, and derive the answer only
  from what the map DRAWS for the player, never from sim data, or the key leaks other
  players' information.

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
