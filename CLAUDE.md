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

- `decompiled/<Assembly>/` — reference-only decompiled game code (gitignored; regenerate with `.\decompile.ps1 [-Assemblies <names>]`; how to research it: `docs/generic/reverse-engineering.md`):
  - `Assembly-CSharp/`: ES2 game code — screens, orders, events, departments
  - `Assembly-CSharp-firstpass/`: the Amplitude engine, including the AGE GUI framework. Organized by namespace folder; global-namespace types (`AgeTransform.cs`, `AgeManager.cs`, `AgeControl*.cs`) sit at the folder root
  - `Amplitude/`: small utility assembly
- `docs/` — ES2-specific research and design notes
- `docs/generic/` — the game-agnostic accessibility modding documentation (the primary goal)
- Reference mods to draw patterns from: `../songs-of-conquest-access`, `../wotr-access`, `../DiscoAccess`, `../tangledeep_access`; Prism speech library source at `../prism`. Only look at reference mods when explicitly directed; the goal is to see how much the generic game mods documentation would help.

## Commands

- Build + deploy: `dotnet build ES2Access/ES2Access.csproj` — copies the plugin to `<game>\BepInEx\plugins\ES2Access` and `prism.dll` to the game root. Game location comes from `GamePaths.props` (gitignored; copy from `GamePaths.props.template`).
- Run: `.\run-game.ps1 [-NoBuild] [-NoSpeech] [-NoDev] [-NoWait] [-LoadSave "<save title>"]` (the last boots straight into a save); `.\wait-game.ps1 <menu|ingame|loading|dialog>` blocks until the game reaches a state
- Tests (offline, no game needed): `dotnet test ES2Access.Tests/ES2Access.Tests.csproj`
- Game log: `<game>\BepInEx\LogOutput.log`

## Autonomous testing via the dev server

While the game runs (dev server enabled), `http://127.0.0.1:8771` serves routes for state,
speech, GUI inspection, a C# REPL, input injection, hot reload, and loading saves. **Read
`docs/dev-loop.md` before testing** — it is the maintained map: full route reference, REPL
gotchas, test recipes, helper inventory, and fixture etiquette.

Architecture: `ES2Access.Loader` is the actual BepInEx plugin and never reloads — it owns the dev server, `/eval` (vendored `mcs.dll`, a net35 Mono.CSharp), and the mod lifecycle. `ES2Access.dll` is loaded from bytes (never file-locked, so `dotnet build` works while the game runs) and must tear down fully in `ModEntry.Stop` — every feature must be reload-safe. Only `ES2Access.dll` hot-reloads; changes to the loader require a game restart. Harmony instances are created with a unique-per-load id (fixed ids let a stale `UnpatchSelf` strip a newer load's patches).

## Conventions

- Runtime code must stay compatible with Endless Space 2's Unity 5.5 / Mono environment. Assume .NET Framework 3.5 compatibility unless a project is explicitly for tools or tests.
- Avoid redundant null checks and comments that do not add information.
- Prefer deterministic game actions over simulated input where the game exposes a reliable API.
- Name behavior after what the player can do or perceive, not after incidental implementation details.
- All speech goes through `PrismSpeech.Speak(MessageBuilder)` from the per-frame pump in `Plugin.Update`. Harmony hooks and watchers only set state or enqueue — they never speak.
- `ES2Access/Core/` compiles against the BCL only (no Unity, BepInEx, or Harmony) so it stays unit-testable off-engine; `ES2Access.Tests` build-enforces this by compiling `Core/` sources directly.
- Mod-authored spoken phrases come from `ModStrings` keys (translations in `ES2Access/locale/<language>.json`, named after the game's own language names; `english.json` is the template). Never inline English literals in speech, and keep each translatable template a complete phrase — don't glue fragments that grammar would need to inflect. Game-authored text arrives already localized via `Gui.Localize`. `MessageBuilder` pulls its separators and fraction/quantity templates from `ModStrings`.

## Workflow

Read `docs/generic/making-screens-accessible.md` — measure, propose the
model for my approval, implement, verify with evidence pairs, hand over the manual test —
with the tools in `docs/dev-loop.md`. Repo-specific enforcement on top of that process:

- Design approval and every new key binding come from me; an approved design counts as
  measurement-settled for pipelining.
- Evidence pairs use `crop-shot.ps1`; never read full-frame screenshots into context.
- A stage is not done until each of its outputs has landed in the file whose charter fits
  it: a new helper, route, recipe, or key binding in `docs/dev-loop.md` (the toolbox —
  nothing else ever goes there); a game-mechanism finding or the mod-policy decision it
  forces in `docs/es2-facts.md` (or another ES2-specific file under `docs/`); a
  screen-status change or future-feature prep in `docs/roadmap.md`; a game-agnostic lesson
  in the stage report for the proposals ledger, never written into `docs/generic/`
  directly. When in doubt, a pointer line may sit in the convenient file — the content
  goes where its charter says.
- Each implementation round ends with the consolidated manual test handed to me in a
  per-session `.md` file at the repo root, named after the session (e.g.
  `galaxy-review-test-report.md`) and updated as that session's stages land — never left
  buried in subagent reports. It carries the test steps with expected speech, the
  fixture-blocked items, and any open judgment calls.
  - Similarly, per-session documentation and suggested process improvements should be saved to a per-session file like `galaxy-review-proposals.md` and kept current during that session
- Both of those session specific files are never committed

**Generic-docs bar.** The generic docs are optimized for total future reading cost, not
completeness. A proposed change to `docs/generic/` must clear ALL of: (1) game-agnostic —
provoked by a mechanism a second game plausibly shares; ES2-specific lessons go to
`docs/es2-facts.md` or another ES2 doc; (2) paid for — a stage in this repo shipped a
defect or re-derived something the line would have prevented; (3) not already covered —
quote the nearest existing lines and check the reference mods before claiming a gap; a
stage failing to APPLY an existing rule is evidence the rule works, not a gap; (4) cheapest
form — tighten an existing line over adding one, and a rule about mod-framework code goes
in that source file's doc comment. Subagents report every candidate; the main agent applies
this filter and should expect to reject most.

After implementing a feature or major change:

1. Offer to check if the game accessibility modding documentation should be updated
2. If I approve it, consult the generic game accessibility mod documentation and check if the documentation should be improved to assist in future tasks
3. If so, propose what changes you would make
4. If I approve, make the changes
5. Reflect on what I could have done better to facilitate your work for future sessions

## Delegation

Implementation and verification stages run on Opus only; exploration and read-only research
may use Sonnet. If Opus is unavailable, wait or work in the main agent — never substitute a
smaller model for a stage whose output is verification. Stage subagents spawn no subagents
of their own and follow the stage-hygiene rules in `docs/dev-loop.md`.

Briefs state facts with file:line cites or mark them unverified — a wrong premise costs
the stage a re-derivation; interaction designs are stated conditionally on the game's own
model ("if the game's model is select-then-act, keep it"), never prescribed ahead of
measurement. Research subagents get one required doc (`reverse-engineering.md`);
implementation subagents get `docs/dev-loop.md` plus the chapters its index maps to the
task. The main agent globs and pastes verified file lists into research briefs, and
treats a subagent's negative existence claims as unverified. Every subagent report keeps
two closing sections: what the generic docs lacked, and a token audit. Any known-stale
doc line a stage might follow gets an explicit override in the brief until the doc is
fixed.

**If you are a subagent working on this repo:** before touching source, read
`docs/dev-loop.md` and the `docs/generic` chapters its index maps to your task — even if
your brief forgot to say so. The generic docs are the primary deliverable and each stage is
a test of them: your report must include a "what the generic docs lacked or got wrong"
section, or it is incomplete and comes back. Ad-hoc briefing files may supplement the
generic docs, never replace them. Updating the generic docs themselves is main-agent work.

Multi-stage implementation work runs as sequential subagent stages by default, because
stages share the one live game instance and the design of a screen usually depends on live
measurement (rects, frame probes) — an implementer without game access ships the wrong
model. Pipeline two stages (stage N+1 implements in a worktree while stage N owns the live
game for testing) only when N+1's design is already measurement-settled: engine/tooling
work, refactors, or applying a design whose layout and behavior were measured in an earlier
stage. When two stages do overlap, exactly one of them owns the shared hotspot files
(`ModEntry.cs`, `Core/Speech/ModStrings.cs`, `locale/english.json`) and the other must not
touch them; the main agent merges.
