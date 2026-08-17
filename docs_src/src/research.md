# Research

## The technology wheel

The game draws its 385 technologies as dots on a wheel: four quadrants, each of five stages out from the middle plus the victory ring, with arcs between dots that mean something.

A wheel is not a list, so the mod declares it as a tree — quadrant, then stage, then the technologies along that arc, in the order they are drawn clockwise. Only the stage you have opened lists its technologies.

Tab walks three panels:

- **Status**: what is being researched, the queue, and the game's own words for there being no queue
- **Suggested**: the technologies the game's own science department is recommending. The game marks these with a badge on each dot, scattered around a wheel of 385, which is fine to see and no way to find anything — so the mod gathers them into a stop. Each row *is* its dot seen from elsewhere: focusing one takes the view to the technology, and `Enter` opens the branch it lives in and leaves the cursor there
- **Tree**: the wheel itself

The camera follows the tree, because the game only draws what is near the middle of the screen: a technology two rings out does not exist to the renderer until the view has moved onto it. Opening a quadrant aims the view at that quarter, opening a stage aims it at that ring, and closing the last branch puts the whole wheel back.

A technology says its state in the game's own words, what it would cost, how long it would take, and where it sits in the queue if it is in one. Its arcs are spoken from the end you are standing on, and the whole list of one dot's relationships is on the dot.

### Queueing

- `Enter` on a technology queues it, or takes it out of the queue if it is already in — the dot's own click, with the sound and the tutorial event the game attaches to it
- `Enter` on a queue line dequeues it. No confirmation: queue it again to undo
- `Alt+Enter` is the Alt-click, which is the game's "queue this at the head"
- The queue is **reordered by carrying**: `Space` on a queued technology, `Up` or `Down` to the line whose place it should take, `Enter` to drop

### Missing prerequisites

Where the game has left a control switched on only so that a click can explain itself, `Ctrl+Enter` is that explanation — it runs the game's own jump to the technology you are missing. It works on the wheel, on cards elsewhere in the game whose action is technology-blocked, and on locked rows such as a ground-troop type. Those controls still announce themselves as unavailable, and `Enter` on them does nothing, exactly as a plain click does.

### Searching

Typing searches every technology on the wheel, not just the ones on screen — the point of a search here is to reach the thing you cannot find. Landing on a result opens the branch it is buried in.

## The quest journal

The quest journal walks four panels: the side panels the game draws, the filters, the list of quests, and the detail of the one you have selected. A quest's objectives, lore and rewards are in the detail panel and in the review buffer.

The quest you are tracking is also a stop on the HUD of every view level, so its current step is one Tab away from the map.
