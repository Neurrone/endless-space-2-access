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
window (ES2: `NotifyVisibilityChanged`) is gold.

**2. Input dispatch.** Where keys become actions: a central handler chain, a binding service,
or raw polling. You need (a) a way to add your own keys without colliding (claim chains:
first claimant consumes, unclaimed keys pass through untouched), and (b) respect for text
fields — check the framework's focused-control/key-exclusive state before claiming. Never
synthesize OS-level key events. Some mods rewrite the game's own binding tables at startup to
evacuate keys they need (Tangledeep's Rewired surgery) — self-healing, re-applied when the
game rebuilds its map.

**3. Deterministic actions.** The layer where the UI's click handler actually *does* the
thing: command/order objects posted to a queue (ES2: `Order` + `PostOrder` — validated,
server-checked, identical to the mouse path), service calls (`IEndTurnService.TryToEndTurn`),
MVVM commands (WotR: invoke the exact VM method the click handler is wired to), or named
handler dispatch (ES2's `SendMessage("OnClick"+name)` → invoke the same method). Prefer this
over simulated clicks everywhere; simulate interaction state only when a mouse-built system
demands it — and then prefer patching the *sensor* (make the game read your cursor as the
mouse position, WotR's pointer patch) so all downstream game logic runs untouched.

**4. An event/narration stream.** Where the game announces to itself that things happened: a
typed event bus (ES2: `IEventService.EventRaised`, ~280 event classes), a combat/game log
sink every message funnels through (Tangledeep's `GameLogWrite` — "the biggest early win"),
or a notification system feeding the game's own notification UI. This powers narration;
expect to de-noise it later.

**5. Localization service.** How a key becomes display text (and the key convention, e.g.
`%`-prefixed), so the mod can localize the same strings the game does and detect
not-yet-resolved keys when reading UI text.

Also worth mapping early: the service-locator/singleton pattern and its lifecycle (services
may register asynchronously and tear down on returning to menu — re-acquire, never cache),
and where per-frame work can safely live.

## Workflow

1. **Decompile everything relevant** (see [project-bootstrap.md](project-bootstrap.md)) —
   including engine/firstpass assemblies. IL2CPP variant: interop proxy assemblies give
   structure; Cpp2IL dummies give signatures; a Ghidra pipeline gives real method bodies
   (DiscoAccess's approach — "ground truth in one shot" beats live-probing one hypothesis
   at a time).
2. **Grep for the dispatch idioms.** High-yield patterns: `SendMessage(` (handler-name
   dispatch), `GetService<`/`Instance.` (locators), `Notify(`/`EventRaised` (buses),
   `OnClick`/`Cb` naming, `IsReady`/`VisibilityChanged`/`OnEndShow` (readiness),
   `PostOrder`/`Command` (action layer), `Localize`/`LocalizationKey`.
3. **Corroborate live.** The dev server's `/gui/game` dump confirms the scene structure the
   decompile implies; `/eval` probes services and calls candidate APIs against the running
   game before you build on them. When raw dumps and the mod's interpreted view both exist,
   diff them to find information the game exposes that the mod is dropping.
4. **Mine the game's own debug tooling.** Games ship internal inspectors (ES2's
   `ImageInformationWindow` polls the hovered widget every frame) — proven, in-tree example
   code for exactly the introspection you need, no patching required.
5. **Write it down as you go**: one game-specific research note per subsystem, stating
   explicitly that it documents *the game*, not the mod. Cite decompiled paths and member
   names — member names survive decompiler upgrades; line numbers don't.

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
