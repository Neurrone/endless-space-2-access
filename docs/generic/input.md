# Input — the mod's keys, and the game's

Everything about keys: how the mod reads them, which keys it binds, and — the part that bites
every game — what the game does with the same physical keys. The core finding this doc is
built around: **the mod cannot consume a key the game polls.** Both sides read the same input
state, so "the mod handled it" never stops the game also handling it. Every section below is
either working with that fact or defeating it.

## The mod's input layer

`ModInput` + bindings (`src/graph-ui/ModInput.cs`, `InputBinding.cs`, `KeyboardBinding.cs`,
`OsKeyboard.cs`, `InputAction.cs`, `UiActions.cs`):

- **Exact-modifier chord matching**: Ctrl+A must not also fire bare A, and releasing a
  modifier mid-hold must not convert the chord.
- **Key repeat is mod-side, from the OS typematic settings** (`SystemParametersInfo`, with
  fallbacks), computed from the clock rather than by advancing an interval — no burst after a
  long frame. Navigation actions repeat; review-buffer actions are one-shot on purpose.
- **Stand-down**: the whole layer goes quiet while the game's own text input owns the
  keyboard. Find the game's authoritative "typing now" signal (ES2:
  `FocusedControl.IsKeyExclusive` — the same check the game's shortcut dispatcher uses).
- **The exclusivity signal conflates "the player is typing" with "some widget owns the
  keyboard."** When the mod itself parks the game's focus on a widget (to make the game
  swallow a key — see Escape below), the stand-down check would silence the mod too. Add a
  narrow ownership exemption — an injected predicate ("this focused control is mine"), never
  a type test — and do NOT exempt genuine capture/typing widgets: during a key-rebind capture
  the layer must stand down fully so arrows and Escape are bindable.
- For test injection at the production dispatch point (queue drained inside the input tick,
  honoring the stand-down), see [dev-server.md](dev-server.md).

## Default key bindings

Proven across wotr-access/SoC/ES2 (make rebindable eventually):

| Keys | Action |
|---|---|
| Arrows | Move (repeating); Left/Right adjust sliders, expand/collapse tree groups |
| Shift+Left / Shift+Right | Coarse adjust, ~10 increments (repeating) — see [widgets.md](widgets.md) |
| Tab / Shift+Tab | Cycle tab-stops, landing on the stop's remembered position |
| Enter | Activate (primary); on a key-binding row, start capturing the primary binding |
| Backspace | Secondary action; on a key-binding row, start capturing the secondary binding |
| Escape | Back / close |
| Home / End | First / last |
| Alt+Up / Alt+Down | Region jumps between a screen's visual bands (repeating) |
| Ctrl+Up/Down, Ctrl+Left/Right, Ctrl+Home/End | Review buffer — see [buffers.md](buffers.md) |

Every new binding is approved by the project owner before it ships — a binding is UX surface
a screen reader user must memorize, and there are no "obvious defaults".

There is deliberately no tooltip key — see [tooltips.md](tooltips.md).

## The game hears your keys too

Before shipping any screen, enumerate the game's default bindings that collide with the
mod's. The checklist is minutes in the decompiled input code and it must happen at design
time, not at manual test:

- Grep the decompiled tree for the raw input reads (`Input.GetKey`, `GetKeyDown`,
  `anyKeyDown`) and classify each hit: reached through the game's shared binding dispatcher,
  or a private per-frame poll (camera controllers usually poll privately — and typically
  **ignore modifier keys**, so the mod's Ctrl/Shift/Alt chords hit them too).
- Find every default binding sharing the mod's keys. The recurring offenders: Enter/Tab bound
  to chat or console (ES2 binds StartChatting to both, live even in single-player),
  KeypadEnter to end-turn, bare Up/Down piggybacked by popups for next/previous.
- **The worst case is a text-field grab**: a game binding that focuses a chat/search box
  makes the whole mod stand down (see above) — one Tab press and the mod goes silent until
  Escape. Treat any collision that can move the game's focus as a blocker, not a quirk.

## When bindings collide, the mod's win

The rule is general, decided once, never per feature: while the mod's layer is active, the
game must not see key events for mod-claimed keys. Per-feature workarounds (hide the window
that reacts, special-case one screen) accumulate surprises; the general mechanism is one
patch site per input path (`src/graph-ui/GameKeyStandDown.cs`, an exemplar to imitate):

- Patch the game's **narrowest key-matching predicates**, not its dispatcher or its update
  loop: the shared hotkey matcher (one function every discrete binding passes through), plus
  each private poller the grep above found (camera pan/zoom). A prefix returning
  "not pressed" when the mod claims the key covers every binding at once, honors the
  stand-down (a typing player's keys pass through untouched), and leaves mouse input alone.
- **Escape is the standing carve-out.** Screens deliberately delegate it — dialog cancel,
  menu close, popup dismiss are the game's own routes and better than reimplementations — so
  Escape is never claimed. Make the carve-out one named constant in the input layer, not a
  note in each patch.
- A key the mod deliberately leaves to the game can also be *made* to be consumed by the
  game's own authority: give the game's focus system a key-exclusive widget and its
  dispatcher swallows Escape itself (its mouse flows rely on this). Never depend on
  same-frame ordering between the mod's handler and the game's — releasing that focus in the
  same frame the game would have consumed the key re-opens the leak intermittently; defer
  such state changes by a frame.

## The engine hands the focused widget its keys late in the frame

In Unity-style engines the GUI framework delivers key events to the focused control in
`LateUpdate` — *after* the mod's `Update`-time action ran. Two corollaries, each of which
caused a shipped bug on ES2 before the rule was recognized:

- **Never hand the engine's focus to a text widget on the frame of the activating key.** The
  Enter that activated "edit this field" reaches the field itself the same frame: the editor
  opens and instantly commits/closes (and a validate handler wired to the field can silently
  act on stale content). Record the request and perform the handoff on the first frame where
  no key is down.
- A key-*up* consumer (a rebind capture that ends on release) has the mirror problem: the
  activating key's release lands in the capture. Wait out the release too.

## Suppression patches under hot reload

Harmony patches on the input path must survive the mod's reload cycle: create the Harmony
instance with a **unique-per-load id** (a fixed id lets a stale unpatch strip a newer load's
patches) and unpatch-self on teardown (see [hot-reload.md](hot-reload.md)). Expose the patch
state — per-target prefix count and owner id — on the dev server's status route: after a
reload, the count must still be exactly one and the owner id must be the *new* load's. A
count of zero looks like a test result, not a failure: navigation still works while every key
also fires the game's binding.

Verifying suppression without pressing keys: you cannot tell "prefix returned false" from
"no key was down" by return value. Assert exactly one prefix on the exact `MethodInfo`,
invoke the prefix's own decision predicate against the game's real binding objects, and only
then attribute the observed false. The physical checks that remain (one press per collision:
the chat key, the end-turn key, held arrows over a camera) go on the manual test script.

## Source files

Engine (copy verbatim): `src/graph-ui/InputAction.cs`, `InputBinding.cs`,
`KeyboardBinding.cs`, `OsKeyboard.cs`, `UiActions.cs`. Adapter exemplars (imitate, don't
copy): `src/graph-ui/ModInput.cs` (chords, repeat, stand-down, injection queue),
`src/graph-ui/GameKeyStandDown.cs` (the suppression patches, the Escape carve-out).
Key-rebind capture flows: [widgets.md](widgets.md).
