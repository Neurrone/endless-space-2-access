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
- **The exit contract is the mod's job**: an engine's cancel for a focused field typically
  only UNFOCUSES it — the surface around the field stays open, promising controls the keys
  cannot reach. When the field lets go without handing the keyboard anywhere, the mod must
  close or re-seat that surface itself, or the player is left on a live layer with nothing
  to do.
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
| Alt+Up / Alt+Down | Region jump between the current panel's sections (repeating; never crosses a panel — Tab does that): see [ui-navigation.md](ui-navigation.md) |
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
- The collision runs the other way too: before binding a chord that REPLAYS a game handler,
  grep that handler's reachable code for physical-modifier reads (`IsControlKeyDown` and
  kin) — the player is still holding the chord when the handler runs.
- **Never route a keyboard gesture through a handler that gates on POINTER state.** A game
  input handler asking whether the cursor is over the widget, whether a drag is live, or
  which widget is under the mouse refuses every keyboard replay — and lies about it, since
  such handlers return the same value for "refused at the gate" as for "done". Read past the
  gate and call what the handler calls there.
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
- **Escape is the standing carve-out — for the game's own surfaces.** Screens deliberately
  delegate it — dialog cancel, menu close, popup dismiss are the game's own routes and
  better than reimplementations — so Escape is never claimed there. Make the carve-out one
  named constant in the input layer, not a note in each patch. **But a surface the game
  cannot see — a mod-owned menu or panel — must DENY the game the key**, or closing the
  menu also raises the game's pause screen. The denial is a predicate asked *before* the
  press (both sides poll, and the game's scan can run either side of the mod's frame; by
  the time the mod's back-handler has run, the menu is already gone — a release-latched
  flag covers the other ordering). It is also a different question from "did I handle it":
  a game-owned popup can need its back-handler run AND the key still visible to the
  engine, so the deny-predicate is never a mirror of the handler's return value.
- A key the mod deliberately leaves to the game can also be *made* to be consumed by the
  game's own authority: give the game's focus system a key-exclusive widget and its
  dispatcher swallows Escape itself (its mouse flows rely on this). Never depend on
  same-frame ordering between the mod's handler and the game's — releasing that focus in the
  same frame the game would have consumed the key re-opens the leak intermittently; defer
  such state changes by a frame.
- **Liveness-gated suppression has a self-race — the law, not an Escape device: a key the
  mod consumed stays claimed until it is RELEASED.** The claim predicate ("is a mod screen
  focused?") depends on state the consuming action itself can mutate: consuming Enter to
  open a modal stands the opener down mid-frame, the new screen has not arrived, the
  predicate answers "not mine" for the rest of that frame — and the game acts on the very
  key the mod already handled (a Return-bound chat grabbed the keyboard this way, and the
  symptom of such a grab is indistinguishable from the mod crashing: total silence until
  the grabbing key is pressed again). The fix is a consumed-keys latch checked *before*
  the liveness gate, self-clearing on key release; the release-latched Escape flag above
  is the special case of this rule.
- **A screen with nothing to navigate is the suppression's blind spot.** Where the game's
  only interaction is a press-anything handler (a cutscene, a splash), every key the mod
  claims is hidden from the game's matcher and the press does nothing at all. Such a screen
  needs one hook that routes every claimed action to the game's own handler, asked before the
  review-buffer keys, and should leave the typed-letter class to the game entirely.
- **If you suppress a key, you owe a replacement route** — a suppressed key can be the only
  door to one of the game's own surfaces (a chat bound to Enter/Tab). And the suppression
  predicate must be able to answer about a CHORD, not just a key: a per-key claim on Tab
  hides Ctrl+Tab from the game as surely as Tab itself, so "move the game's binding to a
  free chord" does nothing until the chord is handed back explicitly.
- **Remapping a game binding is a decision the mod's developer makes explicitly for their
  game — never an automatic pattern.** When chosen, go through the game's own options API
  rather than shadowing (so the options screen shows it and the player can re-bind), touch
  it only while it still holds the shipped default, and follow the binding wherever the
  player moves it afterwards. Two traps: the binding table may fill AFTER the input service
  is published (an early write gets overwritten), and the write persists into unmodded
  launches — a consequence to state to the player.

## Typed text is not a chord

Type-ahead search ([ui-navigation.md](ui-navigation.md)) reads *text*; everything above
models "action = chord", and the two differ in every mechanism:

- **The claim is over a class of keys** (letters, space), answered before the press like any
  claim — but it needs **no release latch**. The liveness self-race exists because a
  consumed action can mutate the state its own claim predicate reads; typing a character
  into a search mutates nothing the predicate depends on, so the latch rule has a boundary
  here, not an exception. Write that reasoning down where the claim lives.
- **The cost is explicit and global**: claiming letters costs the game its letter hotkeys
  wherever a mod screen is focused. Decide that once and deliberately, and put the escape
  hatch — a per-key carve-out like Escape's — where the next collision will look for it.
- **Characters come from the engine's accumulated-characters API** (Unity: `inputString`),
  never from per-key scanning: layout, typematic repeat and dead keys come free, and a
  chord (Ctrl/Alt held) is not typing. The read allocates per frame on old Mono — gate it
  on the engine's any-key flag.
- **Anything reading raw keys consults the deferred-handover state.** The stand-down above
  covers "the game's text input owns the keyboard *now*"; its mirror is the frames where
  the keyboard is *about to be* elsewhere — a deferred editor or capture handover (the
  late-frame rule below). Both handovers set one screen-level flag, and every raw-key
  reader — type-ahead first among them — asks it before touching a character.

## The engine hands the focused widget its keys late in the frame

In Unity-style engines the GUI framework delivers key events to the focused control in
`LateUpdate` — *after* the mod's `Update`-time action ran. Two corollaries, each of which
caused a shipped bug on ES2 before the rule was recognized:

- **Hand the engine's focus to a text widget only once the activating key is RELEASED — not
  merely on a later frame.** A press lasts many frames, and a field holding the keyboard
  while that key is down is one engine dispatch from acting on it: the Enter that activated
  "edit this field" reaches the field itself and the editor opens and instantly
  commits/closes (a validate handler wired to the field can silently act on stale content).
  The one-frame version of this rule — "the first frame where no key went down" — shipped
  exactly that bug: `anyKeyDown` clears on the press's second frame while the key is still
  physically held. Wait on "the key the mod spent is no longer down" (the consumed-key
  latch already knows which one). Caveat on testing any change here: an injected action
  presses no physical key, so `anyKeyDown`, `GetKey` and the latch all read idle on every
  automated run — this path is provable only with real OS key events (a raw-key dev route)
  or a hand on the keyboard, never with action injection.
- **The key that ENDS an edit is typically consumed above the widget.** An input dispatcher
  (or the engine's focus handling) acts on Escape/cancel before the widget's own key path
  ever runs, so the widget sees only the commit; the engine's focus SETTER is the single
  choke point every way out of an edit passes through — commit, cancel, click-elsewhere —
  and the place to tell them apart. And a cancel that restores text must write it back
  BEFORE the engine's lose-focus handlers run: panels commonly commit whatever they find in
  the box from that handler, so a restore one frame later restores the display and not the
  value.
- **Consider diffing the widget against itself once a frame instead of hooking its key
  path.** For echo and caret-reading, one per-frame reading of (text, caret) catches typed
  characters, deletions, caret moves AND the engine's own held-key repeat — which often runs
  from a coroutine no key dispatch passes through — with no patch and reload safety by
  construction.
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
