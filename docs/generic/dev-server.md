# Dev server

An in-process loopback HTTP server is a *requirement*, not a nicety: the developer or agent
building a screen reader mod cannot see the screen or hear the TTS. Both mature reference
mods (tangledeep_access, DiscoAccess) converged on the same answer; this repo's version adds
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
| `GET /gui/game?path=&depth=` | Live scene hierarchy dump (names, components, text via reflection), depth- and node-capped. Later: a parallel *interpreted* dump of the mod's own accessible tree, designed to be diffed against the raw dump. |
| `GET /screenshot` | PNG of the rendered frame — for when visual context is needed |
| `GET /log?since=N&grep=` | Loader-log ring buffer over HTTP; no grepping log files on disk |
| `POST /eval?settle=MS&speech=0` | C# REPL (below). Response includes `speech: [...]` — everything the evaluated code caused to be spoken, gathered by waiting for a quiet settle window. The primary announcement-testing tool: drive an action, read what it said, one request. |
| `POST /wait?timeout=MS` | Body = C# bool expression, compiled once, evaluated **every frame** until true/timeout (`{satisfied,frames,elapsedMs}`). Catches single-frame transients external polling cannot see. |
| `POST /reload`, `GET /loader/status` | Hot reload — see [hot-reload.md](hot-reload.md) |
| `POST /quit` | Clean exit (respond first, quit next frame) |

## The REPL

Pick the evaluator by runtime:

- **Old Mono (net35)**: Mono.CSharp built for net35 — build `sinai-dev/mcs-unity`
  (`Release_net35` configuration; the UnityExplorer lineage), verify the output's
  `ImageRuntimeVersion` is `v2.0.50727`. Its MonoMod dependency is satisfied by BepInEx core.
  Vendored here as `vendor/mcs/mcs.dll` with a provenance NOTICE.
- **CoreCLR (BepInEx 6/IL2CPP)**: Roslyn scripting (`CSharpScript`) — Mono.CSharp's codegen
  throws under CoreCLR (DiscoAccess's documented finding).

Evaluator facts (Mono.CSharp): persistent `Evaluator` across calls (variables survive);
reference the game assemblies + your own at init and **re-reference the mod assembly after
every reload**; run initial `using`s; success is "input complete AND error count zero", not
the return value. Quirk: bare `Time` binds to `InteractiveBase.Time(Action)` — fully qualify
`UnityEngine.Time`. The first eval may 503 on cold JIT; retry succeeds.

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

## Reference implementations in this repo

`ES2Access.Loader/Dev/` (server core, queue, evaluator, GUI dump, waits, log tap),
`ES2Access/Dev/` (mod-registered routes, speech log), `run-game.ps1`,
`vendor/mcs/` (NOTICE documents the exact build recipe).
