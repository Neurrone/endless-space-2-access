# Research

## The Technology Wheel

The game draws its technologies as dots on a wheel: four quadrants, each with five stages running outward from the centre, plus the victory ring. The mod exposes the wheel as a tree: quadrant, then stage, then the technologies of that stage in the order they are drawn. Only the stage you have expanded lists its technologies.

Use `Tab` to cycle through three panels:

- **Status**: the current research and the research queue
- **Suggested**: the technologies the game recommends. On screen these are badges scattered around the wheel; the mod gathers them into one list. Focusing a suggestion moves the view to that technology, and `Enter` expands the branch it lives in and puts focus there
- **Tree**: the wheel itself

The camera follows your position in the tree, because the game only draws the technologies near the centre of the screen. Expanding a quadrant aims the view at that quarter, expanding a stage aims it at that ring, and collapsing everything restores the whole wheel.

A technology reads its state in the game's own words, its cost, its research time, and its queue position if queued. Its connections to other technologies are read from the end you are on.

### Queueing

- `Enter` on a technology queues it, or removes it from the queue if it is already queued
- `Enter` on a queue entry removes it. There is no confirmation; queue it again to undo
- `Alt+Enter` queues a technology at the head of the queue
- To reorder the queue: press `Space` on a queued technology, `Up` or `Down` to the new position, then `Enter` to drop

### Missing Prerequisites

On a control that is disabled because a technology is missing, press `Ctrl+Enter` to jump to that technology. This works on the wheel, on locked rows such as ground troop types, and on cards elsewhere whose action is technology-blocked. The controls still announce themselves as unavailable, and `Enter` on them does nothing.

### Searching

Type to search every technology on the wheel, not just the ones currently drawn. Landing on a result expands the branch it is in.

## The Quest Journal

The quest journal has four panels: the game's side panels, the filters, the quest list, and the detail of the selected quest. A quest's objectives, lore and rewards are in the detail panel and the [review buffer](buffers.md).

The quest you are tracking is also a stop on the HUD of every view, so its current step is one `Tab` away from the map.
