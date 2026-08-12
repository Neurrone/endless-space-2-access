# Endless Space 2 Access

A screen reader accessibility mod for Endless Space 2 (ES2), a turn-based 4X strategy
game, with the goal of making the game fully playable by keyboard with speech output.

The repo has two deliverables:

- **Game-agnostic accessibility modding docs** (`docs/generic/`) — reusable patterns for
  screen reader game accessibility mods: speech via Prism, UI navigation models, reverse
  engineering, dev tooling. This is the primary goal; ES2 is the test vehicle.
- **The ES2 mod itself** (`ES2Access/`, `ES2Access.Loader/`) — a BepInEx plugin pair:
  `ES2Access.Loader` is the permanent plugin (dev server, mod lifecycle); `ES2Access.dll`
  is hot-reloadable and holds the actual accessibility features.

## Developer setup

### Prerequisites

- **Endless Space 2** installed — any desktop store (Steam, GOG). The game runs
  Unity 5.5 / Mono, so the mod targets .NET Framework 3.5. (The game itself has no
  store-specific code paths beyond Steam-present vs Steam-absent, and the mod calls no
  Steam API — see `docs/es2-facts.md`.)
- **.NET SDK 8** — builds the mod and runs the offline tests (`net8.0`).
- **.NET SDK 10** (or newer) — only needed to run `ilspycmd` 10.x for decompiling.
- **ilspycmd** ≥ 10: `dotnet tool install -g ilspycmd`.
  Version 9.x crashes with a stack overflow decompiling `Assembly-CSharp-firstpass`; if a
  plain install resolves a version that fails to install, pin one explicitly, e.g.
  `dotnet tool install -g ilspycmd --version 10.1.1.8388`.
- **BepInEx 5.4.x, win x64** — download `BepInEx_win_x64_<version>.zip` from
  [BepInEx releases](https://github.com/BepInEx/BepInEx/releases) and extract it into the
  game folder, so `winhttp.dll` sits next to `EndlessSpace2.exe` and `BepInEx\core\`
  exists. (BepInEx 5, not 6: the game is Unity Mono. The exe is 64-bit.)
- A screen reader for speech output. The vendored `prism.dll` (deployed to the game root
  by the build) routes speech to the active screen reader or SAPI.

### Setup steps

1. `Copy-Item GamePaths.props.template GamePaths.props` and edit `GameDir` if the game is
   not at the default Steam path. This file is gitignored and machine-specific.
2. Build and deploy: `dotnet build ES2Access/ES2Access.csproj`
   — copies the plugin to `<game>\BepInEx\plugins\ES2Access` and `prism.dll` to the game
   root. Safe to run while the game is running (the mod DLL is loaded from bytes, never
   file-locked).
3. Run the offline tests (no game needed): `dotnet test ES2Access.Tests/ES2Access.Tests.csproj`
4. Decompile the game code for reference: `.\decompile.ps1`
   — writes `decompiled/<Assembly>/` (gitignored, reference-only). Most development work
   consults this constantly; see `docs/generic/reverse-engineering.md`.
5. Launch: `.\run-game.ps1` (flags: `-NoBuild -NoSpeech -NoDev -NoWait -LoadSave "<save title>"`).
   First boot sanity check: `<game>\BepInEx\LogOutput.log` should show the chainloader
   discovering `ES2Access.Loader`.

### Development loop

While the game runs, a dev server at `http://127.0.0.1:8771` provides state inspection,
speech capture, a C# REPL, input injection, hot reload, and save loading. Read
`docs/dev-loop.md` — it is the loop itself: routes, REPL, and verification patterns (helpers live in `docs/helpers.md`, per-screen recipes in `docs/test-recipes.md`).

Only `ES2Access.dll` hot-reloads (`POST /reload` after a build); changes to
`ES2Access.Loader` require a game restart.

## Repo layout

- `ES2Access/` — the hot-reloadable mod. `Core/` compiles against the BCL only (no
  Unity/BepInEx/Harmony) so it stays unit-testable off-engine.
- `ES2Access.Loader/` — the permanent BepInEx plugin: dev server, `/eval`, mod lifecycle.
- `ES2Access.Tests/` — offline unit tests for `Core/`.
- `ES2Access/locale/` — mod speech strings per language (`english.json` is the template).
- `docs/generic/` — the game-agnostic accessibility modding documentation.
- `docs/` — ES2-specific research, architecture notes, and the screen roadmap.
- `vendor/` — `prism.dll` (speech) and `mcs.dll` (net35 Mono.CSharp for the REPL).
