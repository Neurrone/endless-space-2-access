# Fleets and Ship Design

## Selecting a Fleet

Press `Enter` on a fleet's entry in the galaxy tree to select it. The mod announces this, for example `Fleet panel open for 1st Patriots Navy`. The fleet panel adds three stops to the Tab cycle, and the map stays reachable, since destinations are chosen on it:

- **Fleet management**: the fleet's own line and the other fleets at the same location, with command points, movement points, and, while it is moving, `En route to Rigel, arrives in 3 turns`
- **Ships**: the ships in the fleet and the toolbar above them
- **Fleet actions**: what the fleet can do — expeditions, probe launches, and so on

Six actions pick their target inside the system rather than arming a targeting mode: Colonize, Super Colonize, Destroy Planet, Start Expedition, Launch Mining Probe and Reclaim Mothership. Each says where it will put you ("moves focus to the first curiosity in the system"), and pressing it opens the system and lands the cursor on that control — `Enter` there gives the real order, and `Up`/`Down` reach the alternatives when the system has several. If the system offers no such target, the branch opens and the cursor stays where it was.

Selection works like mouse clicks: `Ctrl+Enter` adds a fleet to the selection or removes it, `Shift+Enter` extends the selection. Orders apply to every selected fleet.

## Sending a Fleet

`\` (backslash) is the map's right click, which is the move order. With a fleet selected, press it on the destination: a system's entry, a starlane, or a starlane's `Go to` child entry.

- A successful order is announced, for example `Send fleet 1st Patriots Navy here`, or `Send the 3 selected fleets here`.
- A refused order speaks the game's own reason, for example `There is no path leading to this node, your ships need warp (free) movement`. When several fleets are refused for the same reason, it is spoken once.
- `\` on the system the fleet is already at does nothing.
- `\` on the starlane the fleet is currently flying orders it to stop at the next system.
- `Ctrl+\` is the game's Ctrl+right-click: with free-movement technology, it plots a direct course off the starlanes. Without the technology it behaves like plain `\`.

Redirecting a moving fleet announces the new order only; there is no cancellation message.

### Route Previews

With a fleet selected, every destination you focus includes a route preview, for example `Rigel, group, outpost, 4 turns, 21.5 movement`. The review buffer holds the turn-by-turn itinerary: `Turn 3: Dusay`, `Turn 4: Rigel (destination)`, with `Uses portal` or `Uses wormhole` where a turn uses one. If the destination is unreachable, the refusal reason replaces the turn count.

While the fleet is moving, its own entry reads its destination, remaining movement and arrival countdown, which drops each turn. Arrival is not announced. A cancelled route is: an interception says who intercepted and where; other cancellations say the route was cancelled.

## Targeting Modes

Some fleet actions arm a targeting mode instead of acting at once — launching a probe, placing an ally coordination request, taking a system. The game announces the mode in its own words, for example `Left Click to launch a probe, Esc/Right Click to cancel`. While a mode is armed:

- `Enter` on a map entry confirms the target; the entry's normal click is not performed.
- `\` is the mode's right click — cancel for most modes.
- A completed action is announced, for example `Probe launched towards Dusay, 1 probe remaining`. An invalid target does nothing, as with the mouse.
- The map panel names itself with the mode's instruction instead of "Map" while the mode is armed, so tabbing out and back re-reads it.
- Arming a mode ends inspect mode if it was up, with "Exited inspect mode" spoken first. You can re-enter inspect mode while aiming: sweep to your target, `Enter` lands on it and exits inspect, and a second `Enter` confirms the mode there. With both up, the first `Escape` exits inspect and the next cancels the mode.
- Opening a target picker window (Attack, Invade) ends inspect mode rather than pausing it.
- If a mode has no cancel of its own, `Escape` runs the mode's cancel rather than opening the pause menu.

While a mode is armed, the review buffer of any target you focus holds the game's requirement text for it, for example `Must be a Academy Owned System`.

A probe launch also offers a `Launch towards` group with the eight compass bearings, for aiming into open space where no starlane goes. When the mode arms, focus moves straight to this group's first bearing (north), wherever you were reading — walk the bearings with `Up` and `Down`, `Enter` launches.

## Moving Ships Between Fleets

To move a ship: press `Space` on the ship, move to the fleet line that should receive it, and press `Enter` to drop. The game decides whether the transfer is allowed and says so. `Ctrl+Alt+Enter` (double click) on a ship opens its design.

## The Military Screen

The military screen is the fleet manager. Use `Tab` to cycle through six panels: your fleets, actions on the selected fleet, its ships, your ship designs, actions on the selected design, and the empire's military overview. Fleet rows form a table; `Ctrl+Alt+Enter` on a row shows that fleet on the map.

Wherever the game needs you to pick a fleet, a fleet-selection window opens with its entries and buttons.

## The Ship Designer

The designer has the hull and its slots, the module catalogue, the design's statistics, and the buttons. Two things to know:

- The game's module tiles respond only to double clicks. `Enter` on a module does nothing; `Ctrl+Alt+Enter` places it.
- Modules can also be dragged: press `Space` on a module, move to the slot, press `Enter`.

The detailed-statistics toggle expands the three detail panels and stays on between visits.

## Battle Tactics

The tactics screen has a heading, the plays available to you, the deck you are building, and buttons. Cards move by drag in both directions: `Space` on an available play then `Enter` on the deck adds it; `Space` on a deck card then `Enter` on the available list removes it. Nothing is committed until you press Confirm.

## Ground Troops

The Manage button in the manpower box opens the troop window. It has three columns — Composition, Type, Evolution — with one row per troop type. The columns are Tab stops; the troop types are the rows inside them.

- The composition gauge is a slider: `Left` and `Right` press the game's minus and plus buttons. If the game cannot rebalance, the value simply repeats.
- A locked troop type speaks its reason on its name; `Ctrl+Enter` there jumps to the missing technology.
- A locked upgrade says what it is waiting for.
- Nothing is committed until Confirm. Close or Reset discards every change.

## Behemoths

The Behemoth specialization window (Supremacy DLC) has a heading, the specialization cards, their resource costs, and buttons. The wider Behemoth mechanics have had very little testing; treat them as unverified.
