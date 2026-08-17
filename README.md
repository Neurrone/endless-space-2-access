# Endless Space 2 Access

A screen reader accessibility mod for [Endless Space 2](https://store.steampowered.com/app/392110/Endless_Space_2/), a turn-based 4X strategy game. It narrates the game's screens and gives every one of them keyboard navigation, so that a blind or visually impaired player can run an empire without a mouse.

## Features

- Full keyboard operation of the game: every screen the mod covers is walked with Tab and the arrow keys, and every command is a key
- Tree-based reading of each screen: panels are Tab stops, and what is inside them is a tree you expand, collapse and search
- A galaxy map read as a tree of places, each with coordinates measured from your home system, where a starlane is something you travel down rather than a line you look at
- A scanner that answers "what is near me" by category, nearest first, without changing anything on screen
- An inspect mode that sweeps a square of galaxy with the arrow keys and reads out everything inside it, fog included
- The game's own scan mode made usable: the lens is announced whenever the zoom changes it, and its legend is readable
- Review buffers for re-reading long tooltips, dossiers and the chat log a line at a time
- Type-ahead search on every list and tree: start typing and the focused panel jumps to what you named
- Keyboard drag and drop for the queues, populations, ships and cards the game only lets a mouse move
- Speech through your own screen reader via Prism, with SAPI as a fallback

## Documentation

Installation instructions and the player's manual are at
**<https://neurrone.github.io/endless-space-2-access/>**.

## Links

- [Latest release](https://github.com/Neurrone/endless-space-2-access/releases/latest)
- [Endless Space 2 on Steam](https://store.steampowered.com/app/392110/Endless_Space_2/)
- [Endless Space 2 on GOG](https://www.gog.com/en/game/endless_space_2)

## Development

The repo has two deliverables: the mod itself (`ES2Access/`, `ES2Access.Loader/`), and the
game-agnostic accessibility modding documentation in `docs/generic/` — reusable patterns for
screen reader game accessibility mods, for which ES2 is the test vehicle.

`ES2Access.Loader` is the permanent BepInEx plugin (dev server, `/eval`, mod lifecycle);
`ES2Access.dll` holds the accessibility features and hot-reloads.

### Prerequisites

- **Endless Space 2**, any desktop store. The game runs Unity 5.5 / Mono, so the mod targets
  .NET Framework 3.5.
- **.NET SDK 8** — builds the mod and runs the offline tests.
- **.NET SDK 10+** and **ilspycmd ≥ 10** (`dotnet tool install -g ilspycmd`) — only for
  decompiling. Version 9.x stack-overflows on `Assembly-CSharp-firstpass`; pin a version if a
  plain install resolves a failing one, e.g. `--version 10.1.1.8388`.
- **BepInEx 5.4.x, win x64** — extract
  [a release](https://github.com/BepInEx/BepInEx/releases) into the game folder so that
  `winhttp.dll` sits next to `EndlessSpace2.exe`. BepInEx 5, not 6: the game is Unity Mono,
  and the exe is 64-bit.
- A screen reader. The vendored `prism.dll`, deployed to the game root by the build, routes
  speech to the active screen reader or to SAPI.

### Setup

1. `Copy-Item GamePaths.props.template GamePaths.props` and edit `GameDir` if the game is not
   at the default Steam path. The file is gitignored and machine-specific.
2. Build and deploy: `dotnet build ES2Access/ES2Access.csproj` — copies the plugin to
   `<game>\BepInEx\plugins\ES2Access` and `prism.dll` to the game root. Safe to run while the
   game is running, because the mod DLL is loaded from bytes and never file-locked.
3. Offline tests, no game needed: `dotnet test ES2Access.Tests/ES2Access.Tests.csproj`
4. Decompile the game for reference: `.\decompile.ps1` — writes `decompiled/<Assembly>/`
   (gitignored). See `docs/generic/reverse-engineering.md`.
5. Launch: `.\run-game.ps1` (flags: `-NoBuild -NoSpeech -NoDev -NoWait -LoadSave "<save title>"`).
   First-boot sanity check: `<game>\BepInEx\LogOutput.log` should show the chainloader
   discovering `ES2Access.Loader`.

### Development loop

While the game runs, a dev server at `http://127.0.0.1:8771` provides state inspection, speech
capture, a C# REPL, input injection, hot reload and save loading. Read `docs/dev-loop.md` — it
is the loop itself (helpers in `docs/helpers.md`, per-screen recipes in
`docs/test-recipes.md`). Only `ES2Access.dll` hot-reloads (`POST /reload` after a build);
changes to `ES2Access.Loader` need a game restart.

### Repo layout

- `ES2Access/` — the hot-reloadable mod. `Core/` compiles against the BCL only, so it stays
  unit-testable off-engine.
- `ES2Access.Loader/` — the permanent BepInEx plugin.
- `ES2Access.Tests/` — offline unit tests for `Core/`.
- `ES2Access/locale/` — mod speech strings per language (`english.json` is the template).
- `docs/generic/` — the game-agnostic accessibility modding documentation.
- `docs/` — ES2-specific research, architecture notes and the screen roadmap.
- `docs_src/` — the player-facing manual (mdBook; `mdbook build docs_src`).
- `vendor/` — `prism.dll` (speech) and `mcs.dll` (net35 Mono.CSharp for the REPL).
