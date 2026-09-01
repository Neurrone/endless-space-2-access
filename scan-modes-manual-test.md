# Zoom bands and scan lenses — manual test

Everything below needs a human at the machine. It is what six stages of automated
verification could not reach: real key presses (the dev server injects ACTIONS, which press
no key, so anything that branches on a key being physically down is untested), and things no
save in this project contains.

Fixture: `[Beginner] access test`, loaded from the main menu. Start each section from the
galaxy map with the tutorial popup minimised and no notification popup open.

**Before any zoom step below**: the game refuses ALL keyboard zoom while a notification is
current (`GalaxyViewCameraController.CheckInputs`). If PageUp and PageDown do nothing at all,
that is the cause and not the mod — close or dismiss whatever notification is up and try
again.

Spoken lines are quoted as the mod says them. Coordinates and system names come from this
save; on another save the shape is what matters, not the words.

---

## 1. Physical PageDown re-seats the cursor (the headline)

The cursor must never fall out of the map tree when the zoom takes away the row it was
standing on. This was proved through the injected zoom and through the slider; the one route
never proved is the real key.

1. Press Ctrl+G to focus the galactic map.
2. Type `sabel` and press Escape to land on Sabel.
3. Press Right to open it. The camera goes to zoom level 13.
4. Press Down until you reach a fleet row — something like
   *"Neurrone Fleet 1, 2 ships, ..."*.
5. Hold **PageDown** until the game reports zoom level 4 or lower.
6. **Expected**: the mod reads the SYSTEM row out —
   *"Sabel, -35, -5, group, Home System, colonized, expanded, ..."* — with no fleet counts in
   it. **Wrong**: the cursor ends up on the zoom slider, on the End Turn button, or anywhere
   in the HUD.
7. Hold **PageUp** back to level 13. The cursor stays on Sabel's row.

Repeat once from the other end:

8. With the cursor on Sabel's row, hold **PageDown** until the game reports zoom level 1 or 2.
9. **Expected**: *"Serpens, group, +15% Food, collapsed, 2 of 2"* — Sabel's constellation. The
   cursor is inside the tree.

## 2. Physical PageUp / PageDown in scan mode

1. From the galaxy map press Tab until you reach the View Controls, then Enter on the scan
   button. The mod says the lens name — *"Economy scan"* at this zoom.
2. Press Ctrl+G, then Down onto any system row.
3. Hold **PageDown** to the far end. Expect a lens line at each crossing —
   *"Trade scan"*, then *"Diplomacy scan"* — and, at the Diplomacy band, the cursor on the
   system's OWNER row (*"Imperials Neurrone, 0, 0, group, ..."* or
   *"Cravers Leaper (AI), ..."*), never on the slider.
4. Hold **PageUp** back in. The system row comes back.

## 3. The inspect cell and a real zoom key

1. On the galaxy map, press Ctrl+I to arm inspect mode. Expect
   *"Inspect mode, Cursor 1 by 1"* and, if you were closer than level 9, a pull back to
   *"Zoom level 9 of 15, System details"*.
2. Hold **PageUp** to level 13.
3. **Expected**: *"Exited inspect mode"* — and the camera does not move because of the exit.
4. Re-arm with Ctrl+I. Now turn the **mouse wheel** in until the game passes level 13.
5. **Expected**: the same *"Exited inspect mode"*. (The wheel as an exit route has never been
   exercised at all.)
6. Re-arm, then hold **PageDown** to level 1 or 2 and press **Alt+Left** and **Alt+Right** a
   few times. Expect the square to travel and to hear ownership on every square
   (*"In your influence"*, *"Edge of your influence"*, *"Out of Epistis's influence"*). Nothing
   should move the camera.

## 4. The mod's chords really reach the mod

Every one of these was driven as an injected action, never as a key press. For each: press it
and check that the mod acts AND that the game does not also act (the camera should not jump,
no game panel should open).

1. **Ctrl+I** — inspect mode arms.
2. **Ctrl+G** — the map stop takes focus.
3. **Backslash** on a system row — the move gesture; with no fleet selected it should be
   silent and change nothing.
4. **Ctrl+L** on a turn-log row — goes to that location.
5. The scanner chords (category next/previous, next/previous find, go to) on the map stop.
6. **Right** and **Left** on the tree — one press, one step. Right on a system opens it; Left
   closes it and hands back the zoom you expanded from.

## 5. The zoom slider says how to work it

1. Press Tab to the View Controls and stand on the zoom slider. It announces
   *"Zoom, slider, N of 15, ⟨band⟩"*.
2. Read its review buffer (the buffer keys). The last line must be
   **"Left Arrow or Right Arrow to change zoom"**.
3. Press **Left** and **Right** — real arrow keys. The rung must move one step each way.
4. Enter scan mode and stand on the slider again. The same buffer line must now read
   **"Left Arrow or Right Arrow to change lens"**, and the arrows must step the lens.

## 6. A star lane is a button

1. Ctrl+G, land on any system, Right to open it.
2. Down to a star lane row. It must announce the word **button** —
   *"Starlane 1 to Primus, northeast, button, 5 of 8"*.
3. Press **Enter**. Expected: the cursor arrives at the far system and the whole path is read
   out (*"... Primus, 17, 21, group, colonized, expanded, ..."*).

## 7. A unique world says so at dot distance

1. Ctrl+G, type `dusay`, Escape, Right to open it, then hold **PageDown** back to about zoom
   level 9 with the branch still open.
2. Down to the planet rows.
3. **Expected**: *"Raia, Colonized, Unique Planet"*. The two worlds beside it say only
   *"Dusay I, Inhospitable"* and *"Dusay II, Inhospitable"*.
4. Enter scan mode and do the same at the Trade or Economy lens. Raia must still say
   *"Unique Planet"*, and it must keep saying it with the camera anywhere on the map — pan
   away from Dusay and re-read the row.

## 8. Heard through a real screen reader

Nothing in this plan has been listened to. Every line above was read out of the mod's own
transcript. One pass with the screen reader actually running, at ordinary speech rate, is
what says whether the diplomacy band's rows, the trade-route lines on a lane and the new
button word are comfortable to listen to or merely correct.

---

## Fixture-blocked — what each item needs

None of these can be tested on `[Beginner] access test`. Each line names the one thing a save
or an install must have.

| Blocked | What it needs |
|---|---|
| The hacking family's live content — allocation cells, running-operation lines, traitor-empire rows, program costs and per-program tooltips, the in-map hacking icons, the scan notification chips | An install that OWNS Penumbra (DLC18 / `DLCUC`) and a save actually running a hacking operation. Structure was proved by forcing the widgets on; every table was empty because no such object exists |
| The trade-route weave on real routes — the renderer's three lane materials, the legend beside them, a blockade reaching the reading the turn it lands | A save where `DepartmentOfCommerce.TradingCompanies.Count > 0`. The weave itself was proved against SYNTHETIC routes injected into the department |
| Battle rows on the diplomacy band | A save with a fight in orbit while the scan lens is up |
| The watching-empire swap toggle's own UI path | A met major whose HOME system the player has EXPLORED — the game draws the toggle nowhere else. (Here, Leaper is known through the colony Kais and its home is dark, so only the player's own disabled toggle draws) |
| Empire ordering on the diplomacy band with three or more centres | A save that has met three or more major empires with known centres. With n = 2 the ordering rule is unexercised |
| Contested-influence rendering, and therefore which band it belongs to in either mode | A save with at least one contested ground tile. This fixture has zero |
| "???" unexplored systems in the tree and under the Unexplored heading | A save with a system at exploration state Localized, Identified or PartiallyRevealed. This galaxy is 65 Unrevealed / 17 Revealed / 4 Owned with nothing between, so no camera framing can sight one |
| Adrift fleets below zoom level 5 | A save with an adrift fleet |
| The dot row's Sanctuary sentence (`GhostFeedback`) | An Umbral Choir game with a ghost colony (Penumbra). The unique half of the same pair IS verified — §7 above |
| Quest markers, ally pins, obliterator missiles at their band | A save that draws one. All three were modelled against synthetic instances |
