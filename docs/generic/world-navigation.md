# World navigation — maps, zoom tiers, and fog

Making a game's *world view* keyboard-navigable: the map screen where the player selects
places, inspects what is there, and moves between them. Distinct from ordinary page UI
([ui-navigation.md](ui-navigation.md)) because the world is rendered by a camera whose
distance decides what is drawn, and because the world model knows things the player must
not hear ([fog](#fog-discipline)). Proven on ES2 Access's galaxy map (a node-graph world);
the tile-world half is sketched but unproven — see the last section.

## World-as-graph: the cursor is the game's own graph

When the game's world is a node graph (star systems joined by lanes, cities joined by
roads), the keyboard model is a cursor over the game's *own* graph, not a 2D grid scanner:

- A **list stop** gives coarse selection (the player's places first, then the rest the game
  shows), with the camera panning to the focused node through the game's own
  "show me this" route.
- Each node is **expandable**: its children are the things the map draws *at* that node, in
  drawn categories (ES2: the system's planets, then its outgoing lanes), walked with the
  ordinary tree keys. Position text and category order come from what is drawn, per the
  standing doctrine.
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

- **Expansion never moves the camera.** "Go in" (tree) and "get closer" (camera) are
  different verbs — ui-navigation.md's expansion rule. Bind a camera move to the expand key
  and whichever information tier the other distance shows becomes unreachable.
- **The camera gets an explicit control**, placed wherever fits the screen's design (ES2:
  entries in the node's action menu — "show system view" / "return to galaxy view", the
  latter restoring the previous zoom step *at the focused node*, not at the camera's old
  position). Node children then read **whatever the current distance draws**, switching
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

## Overlay / scan modes (design only — not yet proven in ES2)

Strategy maps ship information overlays (ownership, trade, economy views) that recolor the
same map. Model them as a **mode flag plus a content-source swap on the existing
screens**, never as new screen models per mode; where the overlay's content varies with the
zoom layer, the mod selects an overlay type by driving the game's own layer/zoom state
deterministically. Overlay legends ("caption" panels) become a reviewable stop.

## Tile worlds (planned — unproven)

For tile/grid worlds the sketch remains the exploration-cursor + categorized-scanner pair
(Factorio Access lineage): a free cursor over tiles, and a scanner listing entities by
category for non-spatial access, with tile-signature skip navigation and spatial audio
cues. No game in this doc family has shipped it yet; write this section from the first one
that does.

## Source exemplars

ES2 Access (models to imitate — they name ES2 types): `GalaxyHudScreen.cs` (the systems
stop, expandable system nodes, drawn-surface switching for planet children, lane wording),
`GalaxyViewLevels.cs` (view-level queries, camera routes, zoom-step handling with the
limits read from the game's own comparisons).
