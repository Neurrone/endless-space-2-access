# Narrating game events

Games push events at the player — notifications, popups, turn reports, combat results.
The work is a pipeline problem: find where events become UI, narrate arrivals once, and
give every announcement a reviewable home. Research first (ES2's answers:
`docs/notifications.md` in that repo): where is the one service every event flows
through? which events interrupt vs queue? does the game draw its own log?

- **One hook at the service, never one per screen.** Every game funnels events through a
  single notification/event service before any window opens. Hook arrival there for the
  announcement; per-family code handles only the popups' content. A per-screen approach
  misses every event whose window the player never opens.
- **Announce the arrival; make the content reviewable.** Two surfaces: a short arrival
  utterance (the event's own title), and the full content re-readable after speech has
  moved on — the popup as an ordinary screen, plus an on-demand turn log
  ([buffers.md](buffers.md) holds the buffer mechanics). An event that only interrupts
  is lost to a player mid-task.
- **Speak what the popup DRAWS, never what the model claims.** Shared description fields
  go unwritten when a window draws its own cards instead: the getter answers a template
  with the hole still in it ("Research completed: {0}") or a raw localization key.
  Policy: a description the player cannot see drawn, or still template-shaped, is
  ABSENT — read the drawn content. Titles are usually safe; verify per family.
- **One window class, many prefabs.** A single notification class typically serves
  dozens of event kinds via prefab variants — model family by family, and expect two
  prefabs sharing one class to caption the same field differently. A
  drawn-caption-with-fallback policy survives that; per-family caption wiring does not.
- **Choice popups are sub-screens** ([widgets.md](widgets.md)), and the game may retire
  a losing choice by FADING it (still `Visible`, alpha 0) — declare only what is
  painted. A choice that commits on one keypress is destructible: guard the walk that
  tests it.
- **"Show location" moves the accessible cursor**, not just the camera — an event that
  points into the world must land the player where they can read what is there.
- **Self-audit the family.** One popup shape × dozens of families rots silently: a
  parity check (painted-but-unsaid, spoken-but-undrawn, promised tooltips) run
  automatically on every popup catches the rot; record what the audit structurally
  cannot see beside the audit itself.
