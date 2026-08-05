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
| `GET /gui/graph?edges=1&buffers=1` | **The accessible tree, wholesale**: the focused screen's whole graph in navigation order, one line per control reading exactly what arriving on it would speak, stop/region boundary markers, focus marker, node id per line; `edges=` adds each direction's destination label, `buffers=` each node's review lines. Side-effect-free (a throwaway render — no focus visuals run, the cursor does not move; two calls answer identically). Collapses walk-and-listen loops into one read. Compose each line by diffing against the previous line with the announcer itself — the dump then reads as the walk would sound, headings where they'd be heard. (Lineage: wotr-access's `/gui`, tangledeep's overlay dump with edges.) |
| `POST /input` | One action key through the **production dispatch point** — a queue drained inside the input layer's tick, honoring the stand-down — never a direct navigator call. The response attributes the outcome (`consumed (navigator/buffers)` / `unconsumed` / `standing down: …`) and carries the speech it caused; an unknown key answers with every registered action (self-documenting). A screen that answers `/eval` but not `/input` is a screen whose keys don't reach it. |
| `POST /loadsave` | Body = save title (empty = newest). Loads from the menu, or tears down a running session via the game's own in-session path; answers a retryable `[not ready] …` until it can act, so the launch script polls from cold boot straight into a fixture (tangledeep/wotr's convention). Two ready-states per game: "menu can start a load" and "session must disconnect first". |
| `GET /speech?since=N&wait=MS` | Long-poll: blocks until the next spoken line (released on that frame; also released by a reload). Replaces every sleep-then-poll. |

`/status` also carries the **patch tripwire** when the mod ships input-suppression patches:
per-target prefix count + owner id (see [input.md](input.md) — a silently stripped patch is
otherwise indistinguishable from a working one).

**Compile-checked probe helpers beat REPL scripts** (wotr-access's `DevSurvey` pattern): the
recurring questions — focused screen, screen stack, one-word game state, save list, camera,
drawn-tooltip measurements — live as public static methods in the mod returning JSON, called
as `/eval` one-liners. Logic is compiler-checked with full internals access; the REPL sends
ten characters. A one-word `State()` (`menu|loading|ingame|dialog`) also powers wait scripts:
poll it at 0.3 s with a **dead-process fail-fast** (a crashed game must be a distinct exit
code within a second, not a timeout minutes later), and print the *current* state on timeout
— the difference between a useful and a useless timeout message.

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

Two rules keep a dump honest, both learned from silent wrong-negatives that misdirected
whole sessions:

- **Prune on what a node IS, never on what a cut-short walk did not find.** Any dump that
  both prunes uninteresting nodes and truncates by depth has the bug latent: at the depth
  frontier a node is judged "says nothing" with its children deliberately unread, and the
  pruning cascades until a visibly drawn window answers empty. Keep frontier nodes and mark
  the cutoff (`more: true`) as part of the dump format.
- **A selector must match, or say it did not.** An empty body is never a valid answer to a
  `window=`-style selector: a genuinely absent name gets a loud miss listing what exists,
  and an answer emptied by other parameters says *which* parameter emptied it. Document
  what the selector matches against and in what order (registered window → shown panel →
  by-name subtree search is the proven ladder).

## Route contracts

Every route declares its query-parameter vocabulary at registration, and the server rejects
anything undeclared — 400 naming the unknown parameter and listing the route's own. A
silently ignored parameter is indistinguishable from a broken feature and can shape an
entire session's workflow around a typo. Thread the vocabulary through the mod's route
registration so the loader enforces it uniformly without depending on mod types. The same
defect wears a second hat: **silent fallback on parameter parsing** (`QueryInt(name,
fallback)` making `visibleOnly=false` mean true). Parse helpers are try-parse variants so
the handler can 400 on an unparseable value, and flag parameters accept `1/0/true/false`.

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

**Loop economics, measured**: an autonomous verification session's tokens and wall time both
scale near-linearly with **tool-call count** (each HTTP probe carries reasoning overhead
around it), so the lever is fewer, bigger calls — the wholesale graph dump over per-control
walks, probe helpers over probe chains, and repetitive checks batched into one script that
prints a table. Images are the other measured sink: **never pull a full-resolution frame
into an agent's context** — crop the screenshot to the region under discussion first (a
40 KB crop versus a 600 KB frame, and the crop *is* the evidence's region indication).

**Frame-by-frame transition probes**: to measure a delay or catch a transition, wait for the
negative first and the positive second (after a focus move: wait `!tooltipShown`, then
`tooltipShown` — the second wait's frame count is the delay). A negative that never
satisfies is itself a result: the hide and re-show happened inside one frame.

**Launcher discipline** (all learned from real failures): a single-instance lock (PID file;
stale locks auto-cleared; **match same-session processes only** — an orphan in the services
session blocks every launch forever and cannot be killed); wait for the old process *and*
the dev port to free before relaunching (a kill returns before teardown, and two servers
racing one port makes the loser's game silently server-less); build after the kill so the
DLL is unlocked; abort the launch on build failure — never silently run a stale DLL.

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
  dispatched action exercises only the mod's half. See [input.md](input.md) — the collision
  checklist and the suppression doctrine.
- **Keys whose job is handing control back to the game.** An injected action is dropped
  when no mod screen is focused, so any key that returns control to the game (Escape out of
  a game view, back on a game-owned page) is structurally untestable in-harness: only the
  *destination* can be proven, by calling the game's own handler directly. That routing
  goes on the manual script as a category, not per-screen.
- **Perceptual invariants.** Re-check per screen with measured rects and screenshots: the
  focused item is scrolled into view (long lists!), focus visuals track the cursor, speech
  does not lag held-key repeat. Existence checks lie; rects don't. One timing caveat for
  tooltip evidence: pointer-anchored tooltips move between focus events, so capture the
  crop rect in the same frame as the probe that measured it — a stale rect crops empty sky.

The manual test script then covers exactly this list — each entry becomes a step with what
to press, what should be heard, and what a sighted observer should see.

## Source files

[`src/dev-server/`](src/dev-server/) — server core (`DevServer.cs`, `DevHttpServer.cs`),
main-thread queue, evaluator, raw GUI dump (`GuiDump.cs`), frame-exact waits
(`PredicateWaits.cs`), log tap, ring buffer, and the mod-registered routes (`ModRoutes.cs`,
`SpeechLog.cs`). [`AgeDump.cs`](src/dev-server/AgeDump.cs) is ES2 Access's interpreted dump —
game-specific by nature, included as the model to imitate, not code to copy. Launch/test
script: [`src/bootstrap/run-game.ps1`](src/bootstrap/run-game.ps1).
