# Buffers

The mod uses a buffer system to help with reviewing long tooltips and text elements.

- `Ctrl+Up`: previous line
- `Ctrl+Down`: next line
- `Ctrl+Left`: previous buffer
- `Ctrl+Right`: next buffer
- `Ctrl+Home`: first line
- `Ctrl+End`: last line

The UI buffer is always there. Whenever focus lands somewhere new it is refilled with the full description of what is under the cursor: its name and state, then everything the game's tooltip says about it, then whatever extra facts the mod's reading of that control adds — a system's dossier, a fleet's itinerary, a law's full text, a refusal's reason.

The Chat buffer holds everything said in the session, which in a multiplayer game includes the joins, leaves, kicks and countdown messages the game posts as system chat.
