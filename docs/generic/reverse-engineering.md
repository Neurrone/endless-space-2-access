# Reverse engineering a game for accessibility

Before any feature: map the game's five chokepoints. Every screen and mechanic you make
accessible afterwards is built from these.

## The five chokepoints

**1. UI framework + a reliable readiness signal.** What are the widget types, how is display
text stored, and — critically — what marks a screen *fully shown and interactive* (not
`Awake`, not scene-load; games animate in, bind late, and fire "shown" during init). Look for
the game's own gate: an `IsReady` computed from visible+enabled+animation-done (ES2:
`GuiWindow.IsReady`), an end-of-show callback every screen calls (`OnEndShow`), a coroutine
that flips interactivity (SoC's "screen readiness hooks" discipline), or a window manager
tracking visible screens/modals with events. A single base-class hook that fires for every
window (ES2: `NotifyVisibilityChanged`) is gold. This feeds the screen predicates in
[ui-navigation.md](ui-navigation.md).

**2. Input dispatch.** Where keys become actions: a central handler chain, a binding service,
or raw polling. You need (a) a way to add your own keys without colliding (claim chains:
first claimant consumes, unclaimed keys pass through untouched), and (b) respect for text
fields — check the framework's focused-control/key-exclusive state before claiming. Never
synthesize OS-level key events. Some mods rewrite the game's own binding tables at startup to
evacuate keys they need (Tangledeep's Rewired surgery) — self-healing, re-applied when the
game rebuilds its map. What to do with the findings — collisions, suppression, the mod's own
layer — is [input.md](input.md).

**3. Deterministic actions.** The layer where the UI's click handler actually *does* the
thing: command/order objects posted to a queue (ES2: `Order` + `PostOrder` — validated,
server-checked, identical to the mouse path), service calls (`IEndTurnService.TryToEndTurn`),
MVVM commands (WotR: invoke the exact VM method the click handler is wired to), or named
handler dispatch (ES2's `SendMessage("On<Event>")` to a client object — not just clicks:
drag started/moved/completed, cancel, explore all use the same idiom, so grepping `OnClick`
alone misses half of it). Prefer this
over simulated clicks everywhere; simulate interaction state only when a mouse-built system
demands it — and then prefer patching the *sensor* (make the game read your cursor as the
mouse position, WotR's pointer patch) so all downstream game logic runs untouched.
One reading caveat: handlers often open with a cheat/god-mode branch — read past the guard
before concluding what a handler does for a real player.

**4. An event/narration stream.** Where the game announces to itself that things happened: a
typed event bus (ES2: `IEventService.EventRaised`, ~280 event classes), a combat/game log
sink every message funnels through (Tangledeep's `GameLogWrite` — "the biggest early win"),
or a notification system feeding the game's own notification UI. This powers narration
(event-log buffers: [buffers.md](buffers.md)); expect to de-noise it later.

**5. Localization service.** How a key becomes display text (and the key convention, e.g.
`%`-prefixed), so the mod can localize the same strings the game does and detect
not-yet-resolved keys when reading UI text. Consumed by [localization.md](localization.md)'s
text pipeline (and [icons-and-symbols.md](icons-and-symbols.md) for inline markup).

Also worth mapping early: the service-locator/singleton pattern and its lifecycle (services
may register asynchronously and tear down on returning to menu — re-acquire, never cache),
where per-frame work can safely live, and any **per-class XML/data config registry** the
GUI resolves by name (ES2: `Gui.GetGuiElement(typeName)` — captions, legend text, icons
live there, not in code); it is a chokepoint-adjacent closed set worth checking before
concluding "the game never names this"; and, in any game with a world or a map, the service
the game moves the player's ATTENTION with — locate, reveal, show-this-quest-step. It is
neither an action the mod posts nor an event on the bus, so chokepoints 3 and 4 both miss it,
and every flow where the game leads and the mod's cursor must follow hangs off it (the reveal
trip in [ui-navigation.md](ui-navigation.md)).

## Workflow

1. **Decompile everything relevant** (see [project-bootstrap.md](project-bootstrap.md)) —
   including engine/firstpass assemblies. IL2CPP variant: interop proxy assemblies give
   structure; Cpp2IL dummies give signatures; a Ghidra pipeline gives real method bodies
   (DiscoAccess's approach — "ground truth in one shot" beats live-probing one hypothesis
   at a time). **A game sold on more than one store can differ in its OWN assemblies per
   store** — not just in store SDK DLLs: a top-level type renamed to dodge a store SDK's
   namespace, a store feature's fields stripped from a screen class (ES2's GOG build does
   both). A mod DLL compiled against one store's assemblies fails at RUNTIME on the other
   wherever a member ref names a divergent type — the IL embeds the type name, and no
   build or test on your own machine catches it. Isolate each divergent member behind one
   reflected seam keyed on the stable member NAME, and treat "compiles against each
   store's assemblies" as the completeness check — one clean build proves nothing about
   the store you don't have installed.
2. **Grep for the dispatch idioms.** High-yield patterns: `SendMessage(` (handler-name
   dispatch), `GetService<`/`Instance.` (locators), `Notify(`/`EventRaised` (buses),
   `OnClick`/`Cb` naming, `IsReady`/`VisibilityChanged`/`OnEndShow` (readiness),
   `PostOrder`/`Command` (action layer), `Localize`/`LocalizationKey`. And when a base
   class anchors a family of sibling subclasses, survey the WHOLE family in one pass —
   one regex for their own bound fields over the family's glob — before trusting that the
   prototype generalizes: siblings diverge in shape, not just content. **Enumerate a UI
   framework's whole window universe by transitive subclass CLOSURE** over every class
   declaration, then diff it against the framework's own live registration list: the registry
   being a subset of the closure proves the closure complete, and closure-minus-registry
   isolates the dead code. A direct-base grep under-reports badly (ES2: 45 direct subclasses,
   191 transitive) — and sweep for a SECOND, non-framework UI system while you are there (ES2
   has an immediate-mode debug family none of the window machinery knows about). For anything
   that ENDS a session (victory, defeat, disconnection), find the single server/client state
   that terminates it and enumerate its outcomes from there — the losing path is often a
   different window class from the winning one. The same completeness question about a
   BEHAVIOUR is answered by the service INTERFACE that owns it, never by the verb: a grep for
   the words you would have used returns zero on a game that spells it otherwise, while
   reading the one interface end to end hands over the whole family — including the members no
   verb would find.
3. **Corroborate live** with the dev server ([dev-server.md](dev-server.md)): the raw GUI
   dump confirms the scene structure the decompile implies; the REPL probes services and
   calls candidate APIs against the running game before you build on them. When raw dumps and the mod's interpreted view both exist,
   diff them to find information the game exposes that the mod is dropping.
   And know what each side can prove: **the decompile proves a mechanism exists; only the
   live game proves it works in your calling context.** Two shipped ES2 failures came from
   skipping that: a command-line load path that runs before the save service registers
   (silently falls through on a retail boot), and the engine delivering keys to the focused
   widget in `LateUpdate` — after the mod's `Update` had acted on the same key. Service
   registration timing and frame position (`Update`/`LateUpdate`/coroutine order) are runtime
   questions; check them live before building on a mechanism.
4. **Ask "who writes this?" before interpreting any data collection.** A registry, binding
   table, or symbol map you are tempted to interpret heuristically may be a **closed set**:
   grep for its writers, and one writer loading static data means you can dump it live, diff
   it against the source assets, and enumerate instead of infer. ES2's icon vocabulary went
   through three failed naming heuristics before this ten-minute check showed 382 tokens
   from one writer — see [icons-and-symbols.md](icons-and-symbols.md) for the worked case.
   **And the writer may be the Unity Editor, not code at all**: behavior can live in
   serialized component data on prefabs/scenes (ES2's per-zoom-layer label visibility is
   alpha curves on a modifier component; C# only consumes them). No grep can recover such a
   matrix — the only source of truth is a live GUI dump, or pixels, per state.
5. **Mine the game's own debug tooling.** Games ship internal inspectors (ES2's
   `ImageInformationWindow` polls the hovered widget every frame) — proven, in-tree example
   code for exactly the introspection you need, no patching required.
6. **Write it down as you go**: one game-specific research note per subsystem, stating
   explicitly that it documents *the game*, not the mod. Cite decompiled paths and member
   names — member names survive decompiler upgrades; line numbers don't. Record the game's
   **own call site** for a behavior, not a snippet you derived from it: the call site
   encodes guards and ordering you would rediscover painfully (ES2's focus-a-fleet is a
   five-step dance of camera, view level, cursor and panel; its in-session load must
   disconnect first), it stays checkable as the code evolves, and a pre-derived expression
   has to be re-derived anyway — and can be quietly wrong. When you need the behavior,
   replay that call site verbatim.

## Worked chokepoint maps

| Chokepoint | ES2 (Unity 5.5, AGE) | WotR (Owlcat MVVM) | Tangledeep | Disco (IL2CPP) |
|---|---|---|---|---|
| Readiness | `GuiWindow.IsReady`; `NotifyVisibilityChanged` postfix | VM lifecycle + screen polling | `UIManagerScript` focus watcher (polled — stale on close) | uGUI/EventSystem inspection |
| Input | `InputManager.HandleInput` chain + `AgeManager.FocusedControl` | category claim chain, chord shadowing | Harmony prefix on `UpdateInput` + Rewired map surgery | host pump + injected semantic input |
| Actions | `Order`/`PostOrder`; services; `OnClick<Name>` handlers | VM `Execute`; pointer-sensor patches | game's own confirm path via pass-through activation | module drivers |
| Events | `IEventService.EventRaised` | `EventBus` handlers + log feed | `GameLogWrite` prefix | log/notification readers |
| Localization | `Gui.Localize`, `%key` | `LocalizedString`/`Loc` | game strings reused | game strings + own table |

The pattern to expect: **every game has all five**; they differ only in spelling. Finding
them is days of reading, not weeks — and everything afterwards goes through them.
