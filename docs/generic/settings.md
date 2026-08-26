# The mod's own settings UI

- **Clone the game's options window; don't build foreign UI.** The player already knows
  its idioms, and the clone inherits styling, input routing, and your own declarations.
  The cloning mechanics — registration, modal-stack position, how rows are minted — are
  engine-specific: research once and record in the game's GUI doc (ES2:
  `docs/gui.md` § Cloning a game window in that repo). Two traps that generalize: the
  window MANAGER's iteration order decides who gets input, so measure where a registered
  clone lands rather than assuming append is fine; and a generic refresh/apply/reset
  handler walks only rows of the game's own option-item type — a row that isn't one is
  silently skipped by machinery you didn't know existed.
- **No options window worth cloning?** wotr-access's declarative settings tree is the
  alternative: one declaration serves as persistence, the settings screen's data source,
  and the rebinding UI.
- **The mod framework's config file is the store.** One options table, read at load,
  written on Apply; live-apply what is cheap. Snapshot on open so Cancel restores; an
  empty panel can never be dirty — derive "changed" from the snapshot, not from events.
- **Reset-to-defaults must re-aim at the mod's table.** A cloned reset button still
  points at the game's option registry; left alone it resets the GAME's settings.
- **Key rebinding is a capture problem.** Decide what ENDS a capture (which key commits,
  which cancels) and keep that ending key from leaking onward to the game on the same
  press ([input.md](input.md)'s release rules). Respect the engine's chord-size limit;
  check collisions against BOTH tables — the game's and the mod's. The capture path
  only exists physically: verify with real key events, never injected actions.
- **Measure the engine's text-field commit path before using it.** An option row type
  the game itself never ships can be wired but broken — the one widget nobody at the
  studio ever exercised.
- **Value changes speak.** The settings window is a screen like any other: declare rows
  as real widgets ([widgets.md](widgets.md)) — never scrape it.
