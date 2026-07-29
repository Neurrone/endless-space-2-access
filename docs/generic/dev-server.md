# Dev server

An in-process loopback HTTP server is a *requirement*, not a nicety: the developer or agent
building a screen reader mod cannot see the screen or hear the TTS. Both mature reference
mods (tangledeep_access, DiscoAccess) converged on the same answer; ES2 Access's version adds
loader-side survival (see [hot-reload.md](hot-reload.md)).

## Architecture

- HTTP server bound to `127.0.0.1` only. `HttpListener` works on Unity Mono (managed
  implementation — no http.sys URL ACLs); DiscoAccess hand-rolls over `TcpListener` on
  CoreCLR to avoid admin ACLs. Either is fine; loopback-only is not negotiable.
- **Main-thread job queue.** All engine/game access is marshaled to the Unity main thread via
  a job queue drained once per frame; HTTP threads block on an event with a ~5 s timeout →
  503. Crucial operational fact: **during boot/loading, frames can exceed 5 s, so 503 means
  latency, not failure** — retry, and verify state-changing requests (reload) through their
  status endpoint rather than assuming they failed.
- **Concurrent request handling.** Dispatch each request on a pool thread — a long-running
  request (`/wait`) must not block `/speech` or `/status`.
- Frame captures run on a coroutine (`WaitForEndOfFrame` → `ReadPixels` → PNG).
- Force `Application.runInBackground = true` when the server starts — unattended runs are
  driven from another process and the window never has focus.
- A handler exception answers 500 and never kills the accept loop.
- Operational quirk: `HttpListener` answers a bodyless POST with **411 Length Required**
  before your handler ever runs — the request silently does nothing. Always send a body,
  even an empty one (`curl -X POST --data ''`).

## Gating

Off by default for players: an opt-in config setting in the mod-loader's config file
(`devServer = false` default). The launch script writes it true for dev runs. Env vars:
a kill switch (`*_NO_DEV=1`), a port override, and `*_NO_SPEECH=1` (mute voicing; the
`/speech` tap still captures — headless runs need no screen reader installed).

## Endpoint catalog

The contract that has proven out (shapes are JSON):

| Endpoint | Purpose |
|---|---|
| `GET /status` | Mod state: version, speech availability/backend, last spoken line |
| `GET /speech?since=N` | Ring buffer of everything spoken; monotonic cursor (`{entries:[{seq,text}],next}`). The agent's ears. |
| `GET /gui/game?path=&depth=` | **Raw** scene hierarchy dump (names, components, text via reflection), depth- and node-capped. Game-agnostic; works before you understand anything. |
| `GET /gui/<framework>?window=&depth=&visibleOnly=` | **Interpreted** dump of the game's own UI framework (below). ES2 Access: `/gui/age`. |
| `GET /screenshot` | PNG of the rendered frame — for when visual context is needed |
| `GET /log?since=N&grep=` | Loader-log ring buffer over HTTP; no grepping log files on disk |
| `POST /eval?settle=MS&speech=0` | C# REPL (below). Response includes `speech: [...]` — everything the evaluated code caused to be spoken, gathered by waiting for a quiet settle window. The primary announcement-testing tool: drive an action, read what it said, one request. |
| `POST /wait?timeout=MS` | Body = C# bool expression, compiled once, evaluated **every frame** until true/timeout (`{satisfied,frames,elapsedMs}`). Catches single-frame transients external polling cannot see. |
| `POST /reload`, `GET /loader/status` | Hot reload — see [hot-reload.md](hot-reload.md) |
| `POST /quit` | Clean exit (respond first, quit next frame) |

## Two GUI dumps: raw, then interpreted

The raw dump is the day-one tool: it needs zero knowledge of the game and is how the UI
framework gets identified in the first place. Once the framework is understood, build a
second, *interpreted* dump that walks the framework's own widget tree and answers the
questions the raw dump can't: what does each node **say**, and what can the player
**operate**. The two answer different questions; keep both.

What the interpreted dump emits per node, and where each field proved to come from in ES2
Access's `AgeDump` (adapt the sources per game, keep the shape):

- **kind** — button/toggle/slider/dropdown/label/…, from the framework's control class, not
  the Unity component list
- **text** — the final *localized, markup-stripped* display string. Use the framework's own
  localizer and its own markup cleaner rather than reimplementing either. A control's caption
  is usually a child label — search a few levels down, but not into nested controls.
- **tooltip** — read from the widget's tooltip component *without hovering*; frameworks
  populate tooltip content at bind time. Games often append "why this is disabled" into the
  same string — free narration for disabled controls.
- **value** — control state as one readable string ("on", "0.8 of 0..1", "Beautiful (4 of 6)")
- **interactable** — computed over the *ancestor chain*, not the leaf: one disabled/hidden
  ancestor kills a whole subtree without any child's own flags changing
- prune pure decoration (images/frames with no text, tooltip, control or surviving children) —
  that is what turns a 3× oversized tree into a screen-reader-sized one

Root selection mirrors what the player can actually reach, in the game's own input priority:
topmost modal → visible screen → shown panels (filtered by *hierarchy-wide* visibility — UI
managers keep panels on "shown" lists while an ancestor is hidden). A top-level `windows`
summary (name, visible, readiness) plus a `window=<name>` override to dump anything by name,
shown or not, completes it. The per-node `rect` is what turns visual verification into
numbers — "the tooltip window's rect sits at the focused label's rect + offset" is checkable;
"the tooltip window exists" is how false verifications happen. The interpreted dump lives
**mod-side** (it references game assemblies and iterates via hot reload), unlike the raw
dump, which is loader-side and never changes. Later it doubles as the baseline to diff the mod's own accessible tree against, to
find screens or widgets the mod has not covered.

## The REPL

Pick the evaluator by runtime:

- **Old Mono (net35)**: Mono.CSharp built for net35 — build `sinai-dev/mcs-unity`
  (`Release_net35` configuration; the UnityExplorer lineage), verify the output's
  `ImageRuntimeVersion` is `v2.0.50727`. Its MonoMod dependency is satisfied by BepInEx core.
  Vendor the built DLL with a provenance NOTICE (source repo, commit, build configuration).
- **CoreCLR (BepInEx 6/IL2CPP)**: Roslyn scripting (`CSharpScript`) — Mono.CSharp's codegen
  throws under CoreCLR (DiscoAccess's documented finding).

Evaluator facts (Mono.CSharp): persistent `Evaluator` across calls (variables survive within
one mod load); run initial `using`s; success is "input complete AND error count zero", not
the return value. Quirk: bare `Time` binds to `InteractiveBase.Time(Action)` — fully qualify
`UnityEngine.Time`.

Two traps, both verified the hard way:

- **Rebuild the evaluator on every mod reload — merely re-referencing the new assembly does
  nothing.** The importer caches namespaces and type names from referenced assemblies and the
  first registration of a name wins. With hot reload giving each load a fresh assembly
  identity (see hot-reload.md) and old images never unloading, a long-lived evaluator keeps
  resolving mod type names to the *oldest* copy — evals silently run stale code while
  everything reports success. Discard the evaluator and build a fresh one referencing only
  the newest mod assembly, at reload time. REPL variables reset per reload (like the speech
  ring) — document that. Do the evaluator's warmup (one throwaway evaluate + compile) at
  rebuild time, inside the reload frame: otherwise the first post-reload eval pays the
  dynamic-assembly/JIT cost inside a request's main-thread budget and 503s.
- **Do not explicitly reference the BCL** (`mscorlib`, `System`, `System.Core`): the compiler
  imports those itself on first compile, and importing an assembly it has already taken
  registers every type twice. Duplicate types go unnoticed (the namespace keeps the first),
  but duplicate *extension* methods all stay in scope — every LINQ call fails with CS0121
  "the call is ambiguous" between two identical `Enumerable` overloads. Reference only what
  the defaults don't cover: game assemblies, UnityEngine, your loader and mod.

## The test loop

```
run-game.ps1 -NoSpeech -NoWait        # build, deploy, launch
poll /status until it answers          # boot can take a minute
exercise the feature (/eval, endpoints, or in-mod keys later)
read /speech — did it announce correctly?
iterate: dotnet build (mod only) → POST /reload → re-test   # no restart
POST /quit                             # exits within ~10s; poll at 1s
```

Numbers worth knowing per game: boot-to-server time, quit-to-exit time, and that the speech
ring resets on reload.

## What this loop cannot verify

Before handing a feature to the human tester, **list what the harness structurally cannot
reproduce and reason each item through on paper — don't just skip it**. The recurring blind
spots:

- **Physical key timing.** Driving actions over HTTP dispatches them without pressing keys,
  so anything that depends on real key-down/key-up frames never reproduces: held-key repeat,
  chords, and above all interactions with the *game's own* raw-input scanning. Walk the full
  physical sequence — key down → mod acts → key up → game's input handling reacts. (ES2's
  rebind capture ended on the activating Enter's own release; both contributing facts were
  known before handover, and the collision only surfaced in manual testing because HTTP
  presses no keys.)
- **Same-key double handling.** The mod and the game poll the same physical key; a
  dispatched action exercises only the mod's half. See the one-key-two-listeners section in
  [ui-navigation.md](ui-navigation.md).
- **Perceptual invariants.** Re-check per screen with measured rects and screenshots: the
  focused item is scrolled into view (long lists!), focus visuals track the cursor, speech
  does not lag held-key repeat. Existence checks lie; rects don't.

The manual test script then covers exactly this list — each entry becomes a step with what
to press, what should be heard, and what a sighted observer should see.

## Source files

[`src/dev-server/`](src/dev-server/) — server core (`DevServer.cs`, `DevHttpServer.cs`),
main-thread queue, evaluator, raw GUI dump (`GuiDump.cs`), frame-exact waits
(`PredicateWaits.cs`), log tap, ring buffer, and the mod-registered routes (`ModRoutes.cs`,
`SpeechLog.cs`). [`AgeDump.cs`](src/dev-server/AgeDump.cs) is ES2 Access's interpreted dump —
game-specific by nature, included as the model to imitate, not code to copy. Launch/test
script: [`src/bootstrap/run-game.ps1`](src/bootstrap/run-game.ps1).
