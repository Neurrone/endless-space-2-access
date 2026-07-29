# Endless Space 2 Access

This is a screen reader accessibility mod for Endless Space 2 (ES2), a turn-based 4X strategy game.

## Goals

### Reuseable docs or skills for LLM-assisted game accessibility modding

The primary goal is to generate docs or a skill that could help you with implementing screen reader game accessibility mods for other games more rapidly. For example, it should provide common patterns for common cross-cutting concerns applicable to all games like screen reader integration via Prism, UI implementation, reverse engineering and common tools to help make various types of game mechanics or screens accessible.

I will be pointing you to other game mods that have implemented various things well. The hope is these documents can capture that info so that it is the single place to refer to for such best practices.

It should also help to ensure you know what questions to ask. The ideal goal is for this to help you make large parts of games accessible with direction from me only needed to point you to which screens and mechanics need to be made accessible, or if there are genuine high-level decisions that need to be made.

These documents should be in the `docs/generic` folder. Please have source code in files and reference them from the markdown documentation in cases where it would be better than trying to explain something in pros. If referencing source files this way, they should also be in that folder so that it can be copied to a new game's mod.

### Making Endless Space 2 accessible

This is an important but secondary goal, it is the test vehicle for implementing the above. The objective here is to ensure a screen reader user can operate the game entirely by keyboard

## References

- `decompiled/<Assembly>/` — reference-only decompiled game code (gitignored; regenerate with `.\decompile.ps1 [-Assemblies <names>]`):
  - `Assembly-CSharp/`: ES2 game code — screens, orders, events, departments
  - `Assembly-CSharp-firstpass/`: the Amplitude engine, including the AGE GUI framework. Organized by namespace folder; global-namespace types (`AgeTransform.cs`, `AgeManager.cs`, `AgeControl*.cs`) sit at the folder root
  - `Amplitude/`: small utility assembly
- `docs/` — ES2-specific research and design notes
- `docs/generic/` — the game-agnostic accessibility modding documentation (the primary goal)
- Reference mods to draw patterns from: `D:\source\songs-of-conquest-access`, `D:\source\wotr-access`, `D:\source\DiscoAccess`, `D:\source\tangledeep_access`; Prism speech library source at `D:\source\prism`.

## Commands

- Build + deploy: `dotnet build ES2Access/ES2Access.csproj` — copies the plugin to `<game>\BepInEx\plugins\ES2Access` and `prism.dll` to the game root. Game location comes from `GamePaths.props` (gitignored; copy from `GamePaths.props.template`).
- Run: `.\run-game.ps1 [-NoBuild] [-NoSpeech] [-NoDev] [-NoWait]`
- Tests (offline, no game needed): `dotnet test ES2Access.Tests/ES2Access.Tests.csproj`
- Game log: `<game>\BepInEx\LogOutput.log` (shows Prism backend selection and init errors)

## Autonomous testing via the dev server

While the game runs, `http://127.0.0.1:8771` serves:

- `GET /status` — mod state; `GET /speech?since=N` — ring buffer of everything spoken (works even with speech muted; resets after a reload)
- `GET /gui/game?path=&depth=` — live Unity hierarchy dump; `GET /screenshot` (PNG); `POST /quit`
- `POST /eval?settle=MS&speech=0` — C# REPL against the live game (body = code; persistent state; runs on the main thread). The response's `speech` array lists everything spoken as a result — the primary way to test announcements. REPL quirk: bare `Time` binds to Mono.CSharp's `InteractiveBase.Time(Action)`; write `UnityEngine.Time`.
- `POST /wait?timeout=MS` — body is a C# bool expression, compiled once and evaluated every frame until true or timeout; catches single-frame states external polling misses
- `GET /log?since=N&grep=TEXT` — BepInEx log ring buffer (no need to read LogOutput.log from disk while running)
- `POST /reload` — hot-swaps the mod assembly; `GET /loader/status` — reload counts, last error, and `staleBuild` (true when the DLL on disk is newer than the loaded one — check after building to confirm a reload is actually needed, and after reloading to confirm it took). A broken build is refused and leaves the previous mod running (`failedReloadCount` + `lastReloadError` say why).

Architecture: `ES2Access.Loader` is the actual BepInEx plugin and never reloads — it owns the dev server, `/eval` (vendored `mcs.dll`, a net35 Mono.CSharp), and the mod lifecycle. `ES2Access.dll` is loaded from bytes (never file-locked, so `dotnet build` works while the game runs) and must tear down fully in `ModEntry.Stop` — every feature must be reload-safe. Only `ES2Access.dll` hot-reloads; changes to the loader require a game restart. When Harmony patches arrive, create the Harmony instance with a unique-per-load id (fixed ids let a stale `UnpatchSelf` strip a newer load's patches).

Test loop: `.\run-game.ps1 -NoSpeech -NoWait`, poll `/status` until it answers (boot can take up to a minute), exercise the feature, read `/speech` to verify announcements, `POST /quit` (process exits ~10 s later, poll at 1 s granularity). Iterating on code: `dotnet build ES2Access/ES2Access.csproj` then `POST /reload` — no restart needed. During boot/loading, frames can take >5 s, so main-thread routes (`/status`, `/eval`, `/gui/game`, `/screenshot`) may return 503 — retry, and confirm reloads via `/loader/status` rather than assuming failure.

Gates: the server is **off by default** — it runs only when `devServer = true` under `[Dev]` in `BepInEx\config\endless.space2.access.cfg` (`run-game.ps1` sets this true, or false with `-NoDev`, before launching). `ES2ACCESS_NO_DEV=1` forces it off regardless; `ES2ACCESS_DEV_PORT` overrides the port; `ES2ACCESS_NO_SPEECH=1` mutes voicing but `/speech` still captures.

## Conventions

- Runtime code must stay compatible with Endless Space 2's Unity 5.5 / Mono environment. Assume .NET Framework 3.5 compatibility unless a project is explicitly for tools or tests.
- Uses BepInEx to patch the game with an external command surface.
- Avoid redundant null checks and comments that do not add information.
- Prefer deterministic game actions over simulated input where the game exposes a reliable API.
- Name behavior after what the player can do or perceive, not after incidental implementation details.
- All speech goes through `PrismSpeech.Speak(MessageBuilder)` from the per-frame pump in `Plugin.Update`. Harmony hooks and watchers only set state or enqueue — they never speak.
- `ES2Access/Core/` compiles against the BCL only (no Unity, BepInEx, or Harmony) so it stays unit-testable off-engine; `ES2Access.Tests` build-enforces this by compiling `Core/` sources directly.
- Mod-authored spoken phrases come from `ModStrings` keys (translations in `ES2Access/locale/<language>.json`, named after the game's own language names; `english.json` is the template). Never inline English literals in speech, and keep each translatable template a complete phrase — don't glue fragments that grammar would need to inflect. Game-authored text arrives already localized via `Gui.Localize`. `MessageBuilder` pulls its separators and fraction/quantity templates from `ModStrings`.

## Workflow

After implementing a feature or major change:

1. Offer to check if the game accessibility modding documentation should be updated
2. If I approve it, consult the generic game accessibility mod documentation and check if the documentation should be improved to assist in future tasks
3. If so, propose what changes you would make
4. If I approve, make the changes
5. Reflect on what I could have done better to facilitate your work for future sessions

## Delegation

Do delegate to lower power subagents when appropriate especially for exploring code.

However, updating the game accessibility mod documentation should be done in the main agent.
