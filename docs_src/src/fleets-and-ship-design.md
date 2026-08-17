# Fleets and Ship Design

## Selecting a fleet

`Enter` on a fleet's row in the galaxy tree selects it: "Fleet panel open for 1st Patriots Navy". The panel the game slides over the bottom of the map contributes three stops of its own, and the map underneath stays walkable — which it has to, because where you are sending the fleet is on that map.

- **Fleet management**: the fleet's own line and the fleets beside it, with command points, movement points and, while it is under way, "En route to Rigel, arrives in 3 turns"
- **Ships**: the ships in the fleet, and the toolbar over them
- **Fleet actions**: what this fleet can do — expeditions, probe launches, and whatever else it carries

`Ctrl+Enter` adds a fleet to the selection or takes it out, and `Shift+Enter` extends the selection to it, exactly as Ctrl-click and Shift-click do for a mouse. Orders then apply to everything selected.

## Sending a fleet somewhere

`Backslash` is the map's right click, and that is the move order. With a fleet selected, press it on the destination: a system's row, a starlane, or a lane's "Go to Dusay" child.

- A working send says "Send fleet 1st Patriots Navy here", or "Send the 3 selected fleets here"
- A refused one says the game's own reason — "There is no path leading to this node, your ships need warp (free) movement", "This path crosses Closed Borders…", "The star system is frozen in time". Several fleets refused for the same reason say it once
- `Backslash` on the system a fleet is already sitting in is silent, because nothing was refused
- `Backslash` on the lane a fleet is already flying is the game's own "stop at the next system"
- `Ctrl+Backslash` is the game's Ctrl+right-click: with free-movement technology it plots a straight free-move course instead of following the lanes. Without the technology it behaves exactly like plain Backslash, which is what the game itself does

Replacing the route of a moving fleet is silent — you hear the new order and no cancellation.

### Reading a route before you commit

With a fleet selected, any destination you focus carries the route preview: "Rigel, group, outpost, **4 turns, 21.5 movement**". Its review buffer holds the itinerary, one line per turn in which the fleet reaches somewhere: "Turn 3: Dusay", "Turn 4: Rigel (destination)", with "Uses portal" or "Uses wormhole" on the turn that uses one. Where the destination is unreachable, the refusal sentence takes the place of the turn count.

Once the fleet is moving, its own row says "Moving to Rigel, 0 movement points, Arrives in 3 turns", and the countdown drops by one each turn. Arrival is silent — you asked for it. A route the game cancels is not: an interception says who and where, and anything else says the route was cancelled.

## Targeting modes

Some fleet actions arm a mode instead of doing something at once — launching a probe, placing an ally coordination request, taking a system. The game announces it in its own words ("Left Click to launch a probe, Esc/Right Click to cancel"), and while it is up:

- `Enter` on a map node is the confirm, and the node's own click waits
- `Backslash` is the mode's own right click — a cancel for most modes, one waypoint back while a hacking route is being plotted
- A mode that acts says what it did: "Probe launched towards Dusay, 1 probe remaining". A refused target stays silent, as the mouse's click does
- Where the game leaves a mode with no way out (taking a system), `Escape` runs that mode's own cancel rather than opening the pause menu

A probe launch also offers a "Launch towards" group of the eight compass bearings, for aiming into open space where no lane goes.

While a mode is armed, the review buffer of whatever you focus carries the game's own answer for that target — "Must be a Academy Owned System" — and nothing where the game shows nothing.

## Ships between fleets

A ship is moved by carrying it: `Space` on the ship tile, walk to the fleet line that should have it, `Enter` to drop. The game decides whether the transfer is allowed and says so in its own words. A ship tile's second click (`Ctrl+Alt+Enter`) opens that ship's design.

## The military screen

The military screen is the fleet manager, and it walks six panels: your fleets, the actions on the selected fleet, its ships, your ship designs, the actions on the selected design, and the empire's military overview. Fleet rows are a table, so a row's double click (`Ctrl+Alt+Enter`) shows that fleet on the map.

A fleet-selection modal appears wherever the game needs you to pick one: its lines and its buttons.

## The ship designer

The designer walks the hull and its slots, the module catalogue, the design's statistics and the buttons. Two things about it are worth knowing:

- **The game's module tiles are double-click-only.** A single click does nothing at all, so `Enter` on a module is silent and `Ctrl+Alt+Enter` is what places it.
- **Modules are also dragged**: `Space` on a module, walk to the slot, `Enter`. The drop goes through the game's own drop handler.

The detailed-statistics toggle expands the three detail panels, and it stays on between visits.

## Battle tactics

The tactics deck walks its heading, the plays available to you, the deck you are building, and its buttons. Cards are dragged both ways: `Space` on an available play then `Enter` on the deck adds it, and `Space` on a card in the deck then `Enter` back on the available list removes it. Nothing is committed until Confirm.

## Ground troops

The manpower box's Manage button opens the troop window, which the game draws as three columns — Composition, Type, Evolution — with one row per troop type. The columns are the Tab stops and the troop types are the rows inside them.

- The composition gauge is a slider: `Left` and `Right` are the game's own minus and plus buttons, and a press the game cannot balance simply repeats the value
- A locked troop type's reason is spoken on its name, and `Ctrl+Enter` there is the jump to the missing technology
- A locked upgrade says what it is waiting for
- **Nothing is committed until Confirm** — every stepper, lock and tick is local to the window, and Close or Reset throws it away

## Behemoths

The Behemoth specialization modal (Supremacy) walks its heading, the specialization cards, the resources they cost and its buttons. The wider Behemoth mechanics have had very little testing; treat them as unverified.
