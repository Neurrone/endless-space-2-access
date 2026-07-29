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

| Milestone | Acceptance test |
|---|---|
| Skeleton plugin loads | Loader log shows the plugin; game unaffected |
| Speech works | Startup line audible via screen reader; visible in the log |
| Dev server up | `/status` answers; `/speech` shows the startup line with speech muted |
| Hot reload works | Rebuild + `/reload` re-announces; a corrupted DLL is refused, old mod survives |
| Chokepoints mapped | The five chokepoints documented with citations (game-specific docs) |
| First screen reads | Screen's content announced; verified via `/speech`, then by the user |

The order matters: speech before dev server (the server's speech tap needs the pipeline),
dev server before everything else (it is how all later work gets verified), reload before
features (iteration speed compounds).

## Working method

- The dev server is the agent's eyes and ears: exercise features through it, read `/speech`
  to verify announcements, `/eval` to probe live state, `/screenshot` when visual context
  helps. See [dev-server.md](dev-server.md) for the loop and its gotchas.
- Anything perceptual — focus behavior, audio timing, how speech "feels" — is verified by the
  user, not by automated probes. Probes can pass by luck; a screen reader user's test is the
  ground truth.
- **Visual claims need measurements, not existence checks.** "The tooltip appeared" verified
  as true twice on ES2 while being wrong twice: once it appeared in the opposite screen
  corner (rendered at the idle mouse), once "absent" was actually present. Verify *where*
  and *what*: compare rects from the interpreted GUI dump against the element the visual
  should attach to, and look at the `/screenshot`. Manual test scripts should carry an
  "what an observer should see" column alongside "what you should hear".
- Prefer the game's own deterministic APIs (orders, services, the handler a button invokes)
  over simulated input, everywhere, from the first feature on.
- Keep a game-specific research doc per subsystem as it gets reverse-engineered ("documents
  the game, not the mod"), citing decompiled files and members.

## Keeping these docs alive

After each feature lands in a game mod, ask: did a pattern here prove wrong or incomplete?
Did a new reusable pattern emerge? Update the generic docs in the same session while the
evidence is fresh — that is their whole purpose.
