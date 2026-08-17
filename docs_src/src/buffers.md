# Buffers

Long tooltips, dossiers and event logs are too long to speak in one breath, so the mod keeps them in review buffers you can read a line at a time.

- `Ctrl+Up`: previous line
- `Ctrl+Down`: next line
- `Ctrl+Left`: previous buffer
- `Ctrl+Right`: next buffer
- `Ctrl+Home`: first line
- `Ctrl+End`: last line

## What is in them

The **UI** buffer is always there. Whenever focus lands somewhere new it is refilled with the full description of what is under the cursor: its name and state, then everything the game's tooltip says about it, then whatever extra facts the mod's reading of that control adds — a system's dossier, a fleet's itinerary, a law's full text, a refusal's reason.

The **Chat** buffer holds everything said in the session, which in a multiplayer game includes the joins, leaves, kicks and countdown messages the game posts as system chat.

Some modes stand their own reading in the buffer while they own the screen. The galaxy's inspect cursor is one: while it is armed, the buffer holds the cell you are on rather than the control you left, and releasing the mode puts the control's own lines back.

## How reading behaves

- Review speech queues rather than interrupting, so stepping down a paragraph reads the paragraph. Moving the cursor interrupts, because you have asked for something newer.
- A line step speaks the line and nothing else — no label, no "line 3 of 7".
- Running off either end re-speaks the last line instead of announcing a boundary. Hearing the same line twice is the signal.
