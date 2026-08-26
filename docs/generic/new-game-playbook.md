# New-game playbook

The operational sequence for starting a screen reader mod for a game that doesn't have one.

## Questions to ask the user first

Ask only what can't be discovered; discover the rest.

1. Which game, and where is it installed (or should install detection find it)?
2. Which reference mods should be mined for patterns, and are any prior attempts off-limits?
3. What is the first target — which screen or mechanic matters most to make accessible?
4. Are there constraints on dev workflow (may the agent launch the game? which machine)?
5. Any known community knowledge: existing accessibility attempts, modding wikis, Discords?

High-level decisions that belong to the user when they arise: speech ownership placement,
distribution/release strategy, which mechanics are worth deep investment vs. a shallow pass.

## Recon (before writing any code)

1. **Engine and runtime.** Executable version resources give the Unity version (or engine).
   For Unity: the `Managed` folder inventory and `mscorlib.dll` tell you Mono vs. IL2CPP and
   the .NET profile → your TFM. An IL2CPP game has `GameAssembly.dll` and no game code in
   `Managed`. This single fact forks the whole toolchain (see the runtime tables in
   [project-bootstrap.md](project-bootstrap.md) and [hot-reload.md](hot-reload.md)).
2. **Loader.** Unity Mono → BepInEx 5. Unity IL2CPP → BepInEx 6 (plugins run on bundled
   CoreCLR). A game-native mod system (e.g. Owlcat games) may be preferable to an injector —
   check what shipped mods for that game use. Inspect the game dir for loaders already present.
3. **Prior art.** If reference mods exist for this game or its engine generation, read their
   build/deploy scripts and entry points before designing anything.
4. **Decompile.** Set up the decompile workflow (see
   [reverse-engineering.md](reverse-engineering.md)) — everything downstream depends on
   readable game source.

## Milestones, in order

Each has a cheap acceptance test; do not move on until it passes.

| Milestone             | Acceptance test                                                                |
| --------------------- | ------------------------------------------------------------------------------ |
| Skeleton plugin loads | Loader log shows the plugin; game unaffected                                   |
| Speech works          | Startup line audible via screen reader; visible in the log                     |
| Dev server up         | `/status` answers; `/speech` shows the startup line with speech muted          |
| Hot reload works      | Rebuild + `/reload` re-announces; a corrupted DLL is refused, old mod survives |
| Chokepoints mapped    | The five chokepoints documented with citations (game-specific docs)            |
| First screen reads    | Screen's content announced; verified via `/speech`, then by the user           |

The order matters: speech before dev server (the server's speech tap needs the pipeline),
dev server before everything else (it is how all later work gets verified), reload before
features (iteration speed compounds). From the first screen on, every screen follows
[making-screens-accessible.md](making-screens-accessible.md) — measure, propose the model,
get approval, implement, verify with evidence, hand over the manual script.

## Fixture saves

- Make a **content-rich fixture save early** — the opening minutes of most games are
  content-poor (one unit, locked buttons, empty lists), so early-game fixtures verify only
  absences: list ordering, focus recipes, and state transitions all need a mid-game state.
- Wire a boot-into-fixture path: a launch-script flag that polls the dev server and posts
  the load ([dev-server.md](dev-server.md) `/loadsave`) — one command from cold start to
  in-game. Drive the load from the mod once its services are ready; games' own command-line
  load arguments tend to run before the save system exists and fail silently (ES2's did).
- When handing a live game between work stages, note what is pending in it (queued dialogs,
  notifications) — the next stage otherwise spends a round rediscovering it.

## The repo's two living documents

How to actually work — the dev server as eyes and ears, evidence over claims, deterministic
APIs, the manual handover — is [making-screens-accessible.md](making-screens-accessible.md)
and [dev-server.md](dev-server.md). What the playbook adds is the two documents each game
repo maintains alongside the code:

- A **game-specific research doc per subsystem** as it gets reverse-engineered ("documents
  the game, not the mod"), citing decompiled files and members.
- A **living dev map** (ES2 Access: `docs/dev-loop.md`) — the work loop itself (dev routes,
  their gotchas, the screen-agnostic verification patterns) plus an INDEX pointing at the
  per-need files that hold the detail: the helper inventory, the layer and key-binding
  reference, the per-screen test recipes, and a chapter index into these generic docs. It
  does not duplicate those files; it says which one to open. It is the first read of every
  work session, and a change is not done until the map and the file its charter names both
  reflect it — that is what keeps each session from re-discovering the last one's tools.
